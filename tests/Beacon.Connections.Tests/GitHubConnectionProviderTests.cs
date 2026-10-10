using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-301：连接测试成功/失败路径（假 Handler 全分支；真实 PAT 手动验证随装机走查）。</summary>
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

    private static SecretStoreStub Secrets(string token = "pat") => new()
    {
        Secrets = { [Connection.CredentialRef!] = token },
    };

    [Fact]
    public async Task TestAsync_Healthy_On200()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "{\"rate\":{}}");
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var health = (await provider.TestAsync(Connection, Context(Secrets()), CancellationToken.None)).Health;

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

            var health = (await provider.TestAsync(Connection, Context(Secrets("bad-token")), CancellationToken.None)).Health;

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

            var health = (await provider.TestAsync(Connection, Context(Secrets()), CancellationToken.None)).Health;

            Assert.Equal(ConnectionHealthState.Offline, health);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task TestAsync_MissingCredential_Unauthorized_WithRealReason()
    {
        // 2026-10-10 修复前：空 PAT 无 Authorization 头打 /rate_limit，匿名 200 误报「连接正常」
        var handler = new FakeHttpMessageHandler();
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var result = await provider.TestAsync(Connection, Context(new SecretStoreStub()), CancellationToken.None);

            Assert.Equal(ConnectionHealthState.Unauthorized, result.Health);
            Assert.Contains("PAT", result.Detail);
            Assert.Empty(handler.Requests); // 短路：不发请求
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task TestAsync_Forbidden403_RateLimitExhausted_MapsOffline()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Forbidden, "{}", null, ("X-RateLimit-Remaining", "0"));
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var result = await provider.TestAsync(Connection, Context(Secrets()), CancellationToken.None);

            Assert.Equal(ConnectionHealthState.Offline, result.Health);
            Assert.Contains("rate limit", result.Detail);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task TestAsync_Forbidden403_Permission_MapsUnauthorized()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Forbidden, "{}"); // 无额度头 → 权限问题（PAT 授权范围）
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var result = await provider.TestAsync(Connection, Context(Secrets()), CancellationToken.None);

            Assert.Equal(ConnectionHealthState.Unauthorized, result.Health);
            Assert.Contains("403", result.Detail);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task TestAsync_Timeout_MapsOffline()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responder = _ => throw new TaskCanceledException("client timeout");
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubConnectionProvider();

            var result = await provider.TestAsync(Connection, Context(Secrets()), CancellationToken.None);

            Assert.Equal(ConnectionHealthState.Offline, result.Health);
            Assert.Contains("timed out", result.Detail);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public void Cache_TokenChange_YieldsNewClient()
    {
        // 2026-10-10 修复前：缓存键只用连接 Id——重录 PAT 后连接测试永远拿旧 token 验
        var secrets = Secrets("pat-a");
        var context = new ConnectionContext { Secrets = secrets };
        try
        {
            var first = GitHubClientCache.Get(Connection, context);
            secrets.Secrets[Connection.CredentialRef!] = "pat-b";
            var second = GitHubClientCache.Get(Connection, context);

            Assert.NotSame(first, second);
            Assert.Same(second, GitHubClientCache.Get(Connection, context)); // 同 token 复用
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }
}
