using System.Collections.Concurrent;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>按连接缓存客户端：token/endpoint 绑定，ETag 表随连接实例存活。</summary>
internal static class GitHubClientCache
{
    private static readonly ConcurrentDictionary<string, GitHubApiClient> Clients = new();

    public static GitHubApiClient Get(ConnectionConfig connection, ConnectionContext context)
    {
        return Clients.GetOrAdd(connection.Id, _ =>
        {
            var token = context.Secrets.GetAsync(connection.CredentialRef ?? "").GetAwaiter().GetResult();
            return new GitHubApiClient(connection.Endpoint, token);
        });
    }

    /// <summary>测试注入：替换指定连接的客户端（假 Handler）。</summary>
    internal static void SetForTests(string connectionId, GitHubApiClient client) => Clients[connectionId] = client;

    internal static void ClearForTests() => Clients.Clear();
}

/// <summary>GitHub 连接 Provider（B-301）：PAT 校验 + 连接测试（/rate_limit 不占核心配额）。</summary>
public sealed class GitHubConnectionProvider : IConnectionProvider
{
    public string ConnectionType => "github";

    public async Task<ConnectionHealthState> TestAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = GitHubClientCache.Get(connection, context);
            await client.GetAsync("/rate_limit", cancellationToken: cancellationToken).ConfigureAwait(false);
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
