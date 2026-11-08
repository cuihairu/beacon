using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// HTTP 取数失败面收口：HttpEndpoint 与 Jenkins 的网络异常/超时/状态码分级（Offline/Unauthorized/Degraded），
/// GitHubApiClient PostAsync 全状态矩阵 + HandleStatusAsync 意外状态兜底。这些是调度器指数退避与降级分级的契约面。
/// </summary>
public sealed class HttpFailureEdgeTests
{
    private static ConnectionConfig Connection(string? endpoint = "https://api.example.com", string? credentialRef = null) => new()
    {
        Id = "conn-1",
        Type = "http",
        Endpoint = endpoint,
        CredentialRef = credentialRef,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    private static HttpClient Client(FakeHttpMessageHandler handler) => HttpEndpoint.Create(handler);

    // ---------- HttpEndpoint：网络异常 / 超时 / 状态码 ----------

    [Fact]
    public async Task HttpEndpoint_MissingEndpoint_ThrowsDegraded()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "{}") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            HttpEndpoint.FetchAsync(Client(handler), Connection(endpoint: null), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task HttpEndpoint_NetworkFailure_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => throw new HttpRequestException("connection refused") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            HttpEndpoint.FetchAsync(Client(handler), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task HttpEndpoint_Timeout_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => throw new TaskCanceledException("timed out") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            HttpEndpoint.FetchAsync(Client(handler), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ConnectionHealthState.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.BadGateway, ConnectionHealthState.Offline)]
    public async Task HttpEndpoint_StatusMapping(HttpStatusCode status, ConnectionHealthState expected)
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, "oops") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            HttpEndpoint.FetchAsync(Client(handler), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(expected, exception.Health);
    }

    [Fact]
    public async Task HttpEndpoint_Success_ReturnsBody()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"ok":true}""") };
        var body = await HttpEndpoint.FetchAsync(Client(handler), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("""{"ok":true}""", body);
    }

    [Fact]
    public async Task HttpEndpoint_EndpointOverride_TakesPrecedence()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "overridden") };
        var body = await HttpEndpoint.FetchAsync(
            Client(handler), Connection(endpoint: "https://fallback.example.com"), Ctx(), CancellationToken.None,
            defaultEndpoint: null, defaultAuthPrefix: "Bearer ", endpointOverride: "https://override.example.com/api");

        Assert.Equal("overridden", body);
        Assert.Equal("https://override.example.com/api", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task HttpEndpoint_CredentialSecret_AddsAuthHeader()
    {
        var secrets = new SecretStoreStub();
        secrets.Secrets["conn:http"] = "s3cret";
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "{}") };
        await HttpEndpoint.FetchAsync(
            Client(handler), Connection(credentialRef: "conn:http"), new ConnectionContext { Secrets = secrets }, CancellationToken.None);

        Assert.Equal("Bearer s3cret", handler.Requests[0].Headers.GetValues("Authorization").Single());
    }

    // ---------- GitHubApiClient：PostAsync 矩阵 + 意外状态 ----------

    private static GitHubApiClient Create(FakeHttpMessageHandler handler) => new(handler: handler);

    [Fact]
    public async Task PostAsync_Success_ReturnsTrue()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.NoContent) };
        var ok = await Create(handler).PostAsync("/actions/runs/1/cancel");

        Assert.True(ok);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.Conflict, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.UnprocessableEntity, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.BadRequest, ConnectionHealthState.Degraded)] // 400 → 意外响应
    public async Task PostAsync_StatusMapping(HttpStatusCode status, ConnectionHealthState expected)
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, "oops") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            Create(handler).PostAsync("/actions/runs/1/rerun"));

        Assert.Equal(expected, exception.Health);
    }

    [Fact]
    public async Task PostAsync_Forbidden_QuotaExhausted_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => new FakeHttpResponse(HttpStatusCode.Forbidden, "rate limited", Headers: [("X-RateLimit-Remaining", "0")]),
        };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            Create(handler).PostAsync("/actions/runs/1/cancel"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task PostAsync_Forbidden_QuotaLeft_ThrowsUnauthorized()
    {
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => new FakeHttpResponse(HttpStatusCode.Forbidden, "permission denied", Headers: [("X-RateLimit-Remaining", "500")]),
        };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            Create(handler).PostAsync("/actions/runs/1/cancel"));

        Assert.Equal(ConnectionHealthState.Unauthorized, exception.Health);
    }

    [Fact]
    public async Task PostAsync_NetworkFailure_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => throw new HttpRequestException("connection refused") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            Create(handler).PostAsync("/actions/runs/1/cancel"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task PostAsync_Timeout_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => throw new TaskCanceledException("timed out") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            Create(handler).PostAsync("/actions/runs/1/cancel"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task GetAsync_UnusualStatus_ThrowsDegraded()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.BadRequest, "teapot") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            Create(handler).GetAsync("/user"));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // ---------- Jenkins：网络异常 / 超时 / 未知颜色 ----------

    private static Task<string> JenkinsFetch(FakeHttpMessageHandler handler)
        => JenkinsBuildProvider.FetchAsync(
            "https://jenkins.example.com/job/x/api/json", Connection(), Ctx(), CancellationToken.None,
            client: HttpEndpoint.Create(handler));

    [Fact]
    public async Task JenkinsFetch_NetworkFailure_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => throw new HttpRequestException("connection refused") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() => JenkinsFetch(handler));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task JenkinsFetch_Timeout_ThrowsOffline()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => throw new TaskCanceledException("timed out") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() => JenkinsFetch(handler));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ConnectionHealthState.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    public async Task JenkinsFetch_StatusMapping(HttpStatusCode status, ConnectionHealthState expected)
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, "oops") };
        var exception = await Assert.ThrowsAsync<ConnectionException>(() => JenkinsFetch(handler));

        Assert.Equal(expected, exception.Health);
    }

    [Fact]
    public async Task JenkinsFetch_Success_ReturnsBody()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"color":"blue"}""") };
        var body = await JenkinsFetch(handler);

        Assert.Equal("""{"color":"blue"}""", body);
    }

    [Theory]
    [InlineData("mystery_color")]
    [InlineData("mystery_color_anime")]
    [InlineData(null)]
    [InlineData("")]
    public void MapColor_UnknownColor_FallsBackToWarning(string? color)
    {
        var (lifecycle, severity, phase) = JenkinsBuildProvider.MapColor(color);

        Assert.Equal(Severity.Warning, severity);
        Assert.True(lifecycle is LifecycleState.Unknown or LifecycleState.Running);
        if (color is { Length: > 0 })
        {
            Assert.Equal(color.EndsWith("_anime", StringComparison.Ordinal) ? color[..^"_anime".Length] : color, phase);
        }
    }
}
