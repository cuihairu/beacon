using System.Collections.Concurrent;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>按连接缓存客户端：token/endpoint 参与缓存键（ETag 表随客户端版本存活）。</summary>
internal static class GitHubClientCache
{
    private static readonly ConcurrentDictionary<string, GitHubApiClient> Clients = new();

    /// <summary>测试注入覆盖表（按连接 Id 直查，与生产键解耦——注入签名不随缓存键变化）。</summary>
    private static readonly ConcurrentDictionary<string, GitHubApiClient> TestOverrides = new();

    public static GitHubApiClient Get(ConnectionConfig connection, ConnectionContext context)
    {
        if (TestOverrides.TryGetValue(connection.Id, out var injected))
        {
            return injected;
        }
        var token = context.Secrets.GetAsync(connection.CredentialRef ?? "").GetAwaiter().GetResult() ?? "";
        // 键含 endpoint+token（2026-10-10）：重录 PAT/改端点后旧客户端（旧 Authorization/ETag 表）自然失效，
        // 此前键只用连接 Id——连接测试永远拿旧 token 验，改完凭据不重启不生效。
        return Clients.GetOrAdd($"{connection.Id}\n{connection.Endpoint}\n{token}", _ => new GitHubApiClient(connection.Endpoint, token));
    }

    /// <summary>测试注入：替换指定连接的客户端（假 Handler）。</summary>
    internal static void SetForTests(string connectionId, GitHubApiClient client) => TestOverrides[connectionId] = client;

    internal static void ClearForTests()
    {
        Clients.Clear();
        TestOverrides.Clear();
    }
}

/// <summary>GitHub 连接 Provider（B-301）：PAT 校验 + 连接测试（/rate_limit 不占核心配额）。</summary>
public sealed class GitHubConnectionProvider : IConnectionProvider
{
    public string ConnectionType => "github";

    public async Task<ConnectionTestResult> TestAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // 缺凭据先短路（2026-10-10）：/rate_limit 匿名也 200，空 PAT 走到请求会误报「连接正常」——
        // 失败路径必须给出录入指引（与 Ark 缺 AK/SK 同口径）
        var token = await context.Secrets.GetAsync(connection.CredentialRef ?? "").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ConnectionTestResult(
                ConnectionHealthState.Unauthorized,
                $"未配置 GitHub PAT：请在连接 {connection.Id} 录入（最小授权 repo + workflow）。");
        }
        try
        {
            var client = GitHubClientCache.Get(connection, context);
            await client.GetAsync("/rate_limit", cancellationToken: cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
