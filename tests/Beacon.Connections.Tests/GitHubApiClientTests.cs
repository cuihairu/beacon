using System.Net;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-301 验收：ETag/401/403（限流 vs 权限）/404/5xx/网络失败分支全覆盖。</summary>
public sealed class GitHubApiClientTests
{
    private static GitHubApiClient CreateClient(FakeHttpMessageHandler handler, string? token = "pat-test")
        => new("https://api.github.test/", token, handler);

    [Fact]
    public async Task FirstGet_ReturnsBody_AndStoresEtag_SecondGetSendsIfNoneMatch()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responder = request =>
        {
            var hasCondition = request.Headers.IfNoneMatch.Any(tag => tag.Tag == "\"v1\"");
            return hasCondition
                ? new FakeHttpResponse(HttpStatusCode.NotModified)
                : new FakeHttpResponse(HttpStatusCode.OK, "{\"a\":1}", Etag: "\"v1\"");
        };
        var client = CreateClient(handler);

        var first = await client.GetAsync("/repos/o/r/pulls");
        var second = await client.GetAsync("/repos/o/r/pulls");

        Assert.False(first.NotModified);
        Assert.Equal("{\"a\":1}", first.Body);
        Assert.True(second.NotModified); // 304：不计数不回调
        Assert.Equal(2, handler.Requests.Count);
        var secondRequest = handler.Requests[1];
        Assert.Contains(secondRequest.Headers.IfNoneMatch, tag => tag.Tag == "\"v1\"");
    }

    [Fact]
    public async Task Unauthorized401_ThrowsUnauthorized()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/user"));

        Assert.Equal(ConnectionHealthState.Unauthorized, exception.Health);
    }

    [Fact]
    public async Task Forbidden403_WithQuotaExhausted_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Forbidden, "{\"message\":\"rate limit\"}", headers: [("X-RateLimit-Remaining", "0")]);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/user"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task Forbidden403_WithQuotaLeft_ThrowsUnauthorized()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Forbidden, "{\"message\":\"forbidden\"}", headers: [("X-RateLimit-Remaining", "42")]);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/user"));

        Assert.Equal(ConnectionHealthState.Unauthorized, exception.Health);
    }

    [Fact]
    public async Task NotFound404_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.NotFound, "{}");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/repos/o/missing"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task ServerError5xx_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.InternalServerError, "{}");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/user"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task NetworkFailure_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responder = _ => throw new HttpRequestException("connection refused");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => client.GetAsync("/user"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task RateLimitHeader_IsParsed_AndLowQuotaFlagged()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "{}", headers: [("X-RateLimit-Remaining", "17")]);
        var client = CreateClient(handler);

        await client.GetAsync("/user");

        Assert.Equal(17, client.RateLimitRemaining);
        Assert.True(client.IsLowQuota); // 17 <= 阈值 20
    }

    [Fact]
    public async Task Success_DoesNotFlagLowQuota()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "{}", headers: [("X-RateLimit-Remaining", "4980")]);
        var client = CreateClient(handler);

        await client.GetAsync("/user");

        Assert.False(client.IsLowQuota);
    }

    [Fact]
    public async Task Token_AttachedAsBearer()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler, token: "ghp_abc");

        await client.GetAsync("/user");

        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("ghp_abc", handler.Requests[0].Headers.Authorization?.Parameter);
    }
}
