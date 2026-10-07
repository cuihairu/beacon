using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>bigmodel 连接（智谱 GLM）：TestAsync = GET 监控端点（Endpoint 可空取默认），裸 Key 认证。</summary>
public sealed class BigModelConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client;

    public BigModelConnectionProvider() { }

    public BigModelConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "bigmodel";

    public async Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await HttpEndpoint.FetchAsync(
                _client,
                connection,
                context,
                cancellationToken,
                defaultEndpoint: BigModelUsageProvider.DefaultEndpoint,
                defaultAuthPrefix: "").ConfigureAwait(false);
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
