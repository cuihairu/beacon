using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>http 连接（positioning P0 #3 Generic HTTP）：TestAsync = GET Endpoint，2xx 即 Healthy。</summary>
public sealed class HttpConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client; // null = 共享池（生产路径）；测试注入伪造 handler

    public HttpConnectionProvider() { }

    public HttpConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "http";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await HttpEndpoint.FetchAsync(_client, connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message); // 与 GitHubConnectionProvider 同口径：测试不抛，返回健康态
        }
    }
}
