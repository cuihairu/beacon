using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>ark 连接（火山方舟）：TestAsync = V4 签名调 GetCodingPlanUsage；缺 AK/SK 或凭证无效 → Degraded（显式失败不静默）。</summary>
public sealed class ArkConnectionProvider : IConnectionProvider
{
    private readonly HttpMessageHandler? _handler;

    public ArkConnectionProvider() { }

    public ArkConnectionProvider(HttpMessageHandler handler) => _handler = handler;

    public string ConnectionType => "ark";

    public async Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await new ArkUsageProvider(_handler!).FetchUsageAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
