using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

public sealed class HeraldProviderEdgeCaseTests
{
    [Theory]
    [InlineData(null, "", 120)]
    [InlineData("", "", 120)]
    [InlineData("short", "short", 120)]
    [InlineData("a\r\nb", "a b", 120)]
    [InlineData("abc", "ab…", 2)]
    public void Clip_NormalizesAndTruncates(string? input, string expected, int max)
    {
        Assert.Equal(expected, HeraldFetch.Clip(input, max));
    }

    [Fact]
    public async Task Fetch_404_MapsToOffline()
    {
        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => FetchAsync(HttpStatusCode.NotFound, "missing"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("404", exception.Message);
    }

    [Fact]
    public async Task Fetch_UnexpectedStatus_MapsToOffline()
    {
        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => FetchAsync(HttpStatusCode.InternalServerError, "failed"));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
        Assert.Contains("500", exception.Message);
    }

    [Fact]
    public async Task Fetch_EmptyCredential_OmitsApiKeyHeader()
    {
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "{}"),
        };
        var secretStore = new SecretStoreStub { Secrets = { ["herald:empty"] = "   " } };
        var connection = new ConnectionConfig
        {
            Id = "herald-empty",
            Type = "herald",
            Endpoint = "http://127.0.0.1:8080",
            CredentialRef = "herald:empty",
            Settings = [],
        };

        await FetchAsync(connection, new ConnectionContext { Secrets = secretStore }, handler);

        Assert.False(handler.Requests[0].Headers.Contains("X-API-Key"));
    }

    [Fact]
    public async Task Fetch_MissingCredential_OmitsApiKeyHeader()
    {
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "{}"),
        };
        var connection = new ConnectionConfig
        {
            Id = "herald-missing",
            Type = "herald",
            Endpoint = "http://127.0.0.1:8080",
            CredentialRef = "herald:missing",
            Settings = [],
        };

        await FetchAsync(connection, new ConnectionContext { Secrets = new SecretStoreStub() }, handler);

        Assert.False(handler.Requests[0].Headers.Contains("X-API-Key"));
    }

    [Fact]
    public void ParseIncidents_MissingTitleAndInvalidDate_UsesSafeFallbacks()
    {
        const string json = """{"data":{"incidents":[{"id":"inc-1","opened_at":"not-a-date"},{"id":"inc-2","title":"shown","opened_at":42}]}}""";

        var incidents = HeraldIncidentsProvider.ParseIncidents(json);

        Assert.Equal("inc-1", incidents[0].Title);
        Assert.Null(incidents[0].OpenedAt);
        Assert.Equal("shown", incidents[1].Title);
        Assert.Null(incidents[1].OpenedAt);
    }

    [Fact]
    public void ParseRss_MissingGuidAndDate_UsesTitleIdentity()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel>
              <item><title>fallback identity</title><pubDate>not-a-date</pubDate></item>
              <item><title>dated</title><guid>guid-1</guid></item>
            </channel></rss>
            """;

        var items = HeraldFeedProvider.ParseRss(xml);

        Assert.Equal("fallback identity", items[0].Id);
        Assert.Null(items[0].PublishedAt);
        Assert.Equal("guid-1", items[1].Id);
        Assert.Null(items[1].PublishedAt);
    }

    [Fact]
    public void ParseDeliveries_MissingTotalAndFields_UsesSafeFallbacks()
    {
        const string json = """{"data":{"logs":[{"id":"log-1","created_at":"not-a-date"},{"id":"log-2","created_at":42}]}}""";

        var (total, logs) = HeraldDeliveriesProvider.ParseDeliveries(json);

        Assert.Equal(0, total);
        Assert.Equal("log-1", logs[0].Id);
        Assert.Equal("", logs[0].Provider);
        Assert.Equal("", logs[0].Error);
        Assert.Null(logs[0].CreatedAt);
        Assert.Null(logs[1].CreatedAt);
    }

    private static async Task<string> FetchAsync(HttpStatusCode status, string body)
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        var connection = new ConnectionConfig
        {
            Id = "herald-test",
            Type = "herald",
            Endpoint = "http://127.0.0.1:8080",
            CredentialRef = "conn:herald",
            Settings = [],
        };
        var secretStore = new SecretStoreStub { Secrets = { ["conn:herald"] = "api-key" } };

        return await FetchAsync(connection, new ConnectionContext { Secrets = secretStore }, handler);
    }

    private static async Task<string> FetchAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        FakeHttpMessageHandler handler)
    {
        using var client = new HttpClient(handler);
        return await HeraldFetch.GetAsync(
            $"{connection.Endpoint!.TrimEnd('/')}/api/v1/status",
            connection,
            context,
            CancellationToken.None,
            client);
    }
}
