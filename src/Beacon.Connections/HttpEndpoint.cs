using System.Net.Http;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>HTTP 接口取数（Status Provider 与连接测试共用）：认证头注入、状态码→健康语义、超时。
/// 健康语义对齐 GitHubApiClient：401/403→Unauthorized、404→Offline、网络不可达/超时→Offline。</summary>
internal static class HttpEndpoint
{
    public static readonly HttpClient Shared = Create(null);

    public static HttpClient Create(HttpMessageHandler? handler)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(15);
        return client;
    }

    public static Task<string> FetchAsync(HttpClient? client, ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken, string? defaultEndpoint = null, string defaultAuthPrefix = "Bearer ", string? endpointOverride = null)
        => FetchCoreAsync(client ?? Shared, connection, context, cancellationToken, defaultEndpoint, defaultAuthPrefix, endpointOverride);

    public static Task<string> FetchAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken, string? defaultEndpoint = null, string defaultAuthPrefix = "Bearer ", string? endpointOverride = null)
        => FetchCoreAsync(Shared, connection, context, cancellationToken, defaultEndpoint, defaultAuthPrefix, endpointOverride);

    private static async Task<string> FetchCoreAsync(HttpClient client, ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken, string? defaultEndpoint, string defaultAuthPrefix, string? endpointOverride)
    {
        // Provider 归一化端点最高（如 deepseek base 语义补路径），其次连接显式端点整串直用，最后 Provider 默认端点
        var endpoint = endpointOverride?.Trim();
        if (string.IsNullOrEmpty(endpoint))
        {
            endpoint = connection.Endpoint?.Trim();
        }
        if (string.IsNullOrEmpty(endpoint))
        {
            endpoint = defaultEndpoint?.Trim(); // Provider 可给默认端点（如 bigmodel 监控接口）
        }
        if (string.IsNullOrEmpty(endpoint))
        {
            throw new ConnectionException("HTTP 连接缺少 Endpoint", ConnectionHealthState.Degraded);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        var credentialRef = connection.CredentialRef;
        if (!string.IsNullOrWhiteSpace(credentialRef))
        {
            var secret = await context.Secrets.GetAsync(credentialRef).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(secret))
            {
                // 认证头可配：auth_header（默认 Authorization）+ auth_prefix（默认 Bearer，可显式置空）
                var header = connection.Settings.GetValueOrDefault("auth_header");
                if (string.IsNullOrWhiteSpace(header))
                {
                    header = "Authorization";
                }
                var prefix = connection.Settings.TryGetValue("auth_prefix", out var configured) ? configured : defaultAuthPrefix;
                request.Headers.TryAddWithoutValidation(header, prefix + secret);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new ConnectionException($"HTTP 接口不可达：{exception.Message}", ConnectionHealthState.Offline, exception);
        }
        catch (System.Threading.Tasks.TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConnectionException("HTTP 请求超时（15s）。", ConnectionHealthState.Offline, exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                throw status switch
                {
                    401 or 403 => new ConnectionException($"HTTP 认证失败 ({status})：检查 API Key 与认证头配置。", ConnectionHealthState.Unauthorized),
                    404 => new ConnectionException("HTTP 接口不存在 (404)：检查 Endpoint。", ConnectionHealthState.Offline),
                    _ => new ConnectionException($"HTTP 接口异常 ({status})。", ConnectionHealthState.Offline),
                };
            }
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
