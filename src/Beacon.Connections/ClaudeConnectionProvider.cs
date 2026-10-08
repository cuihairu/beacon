using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>claude 连接：本机会话目录探测（零网络零凭据）。Endpoint 可填自定义会话目录，可空取默认 ~/.claude/projects。</summary>
public sealed class ClaudeConnectionProvider : IConnectionProvider
{
    public string ConnectionType => "claude";

    public Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
        => Task.FromResult(Directory.Exists(ClaudeUsageProvider.ResolveDir(connection))
            ? ConnectionHealthState.Healthy
            : ConnectionHealthState.Degraded);
}
