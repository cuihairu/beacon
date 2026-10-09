using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-301：连接测试成功/失败路径。</summary>
public sealed class GitHubConnectionProviderTests
{
    private static readonly ConnectionConfig Connection = new()
    {
        Id = "gh-main",
        Type = "github",
        Endpoint = "https://api.github.test/",
        CredentialRef = "github:default",
    };

    private static ConnectionContext Context(SecretStoreStub secrets) => new() { Secrets = secrets };

    [Fact]
    public async Task TestAsync_Healthy_On200()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "{\"rate\":{}}");
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var health = (await provider.TestAsync(Connection, Context(new SecretStoreStub()), CancellationToken.None)).Health;

            Assert.Equal(ConnectionHealthState.Healthy, health);
            Assert.StartsWith("/rate_limit", handler.Requests[0].RequestUri!.PathAndQuery); // 不占核心配额的端点
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task TestAsync_MapsExceptionHealth()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "{}");
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "bad-token", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var health = (await provider.TestAsync(Connection, Context(new SecretStoreStub()), CancellationToken.None)).Health;

            Assert.Equal(ConnectionHealthState.Unauthorized, health);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task TestAsync_NetworkDown_Offline()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responder = _ => throw new HttpRequestException("unreachable");
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var health = (await provider.TestAsync(Connection, Context(new SecretStoreStub()), CancellationToken.None)).Health;

            Assert.Equal(ConnectionHealthState.Offline, health);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }
}
