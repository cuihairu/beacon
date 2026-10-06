using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>GitHub 响应：NotModified = ETag 304（不计数不回调，RFC §5）。</summary>
public sealed record GitHubResponse(bool NotModified, string? Body)
{
    public static GitHubResponse Unchanged() => new(true, null);

    public static GitHubResponse Ok(string body) => new(false, body);
}

/// <summary>
/// 手写 GitHub REST 客户端（B-301，RFC §5）：
/// ETag 条件请求（If-None-Match，304 不计数）、rate-limit 感知（剩余额度低于阈值标记，
/// 额度耗尽抛 ConnectionException 触发调度器指数退避=自动拉长间隔）、
/// 401→Unauthorized、403 分限流/权限、网络/5xx→Offline。
/// 每个连接实例一个客户端（token/endpoint 绑定），ETag 表进程内缓存。
/// </summary>
public sealed class GitHubApiClient
{
    public const int LowQuotaThreshold = 20;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly Dictionary<string, string> _etags = [];
    private readonly object _gate = new();

    public GitHubApiClient(string? endpoint = null, string? token = null, HttpMessageHandler? handler = null)
    {
        var client = new HttpClient(handler ?? new HttpClientHandler())
        {
            BaseAddress = new Uri(endpoint?.TrimEnd('/') ?? "https://api.github.com", UriKind.Absolute),
            Timeout = RequestTimeout,
        };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Beacon/0.1 (+https://github.com/cuihairu/beacon)");
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }
        _http = client;
    }

    /// <summary>最近一次响应的 X-RateLimit-Remaining（-1 = 尚未收到头）。</summary>
    public int RateLimitRemaining { get; private set; } = -1;

    public bool IsLowQuota => RateLimitRemaining is >= 0 && RateLimitRemaining <= LowQuotaThreshold;

    public async Task<GitHubResponse> GetAsync(string path, string? query = null, CancellationToken cancellationToken = default)
    {
        var resource = query is null ? path : $"{path}?{query}";
        var request = new HttpRequestMessage(HttpMethod.Get, resource);
        lock (_gate)
        {
            if (_etags.TryGetValue(resource, out var etag))
            {
                request.Headers.IfNoneMatch.ParseAdd(etag);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new ConnectionException($"GitHub unreachable: {exception.Message}", ConnectionHealthState.Offline, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConnectionException("GitHub request timed out.", ConnectionHealthState.Offline, exception);
        }

        using (response)
        {
            ReadRateLimit(response);
            return await HandleStatusAsync(response, resource, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>POST 封装（B-403：dispatch/rerun/cancel）。2xx 返回 true，错误语义与 GET 一致。</summary>
    public async Task<bool> PostAsync(string path, string? jsonBody = null, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new ConnectionException($"GitHub unreachable: {exception.Message}", ConnectionHealthState.Offline, exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ConnectionException("GitHub request timed out.", ConnectionHealthState.Offline, exception);
        }

        using (response)
        {
            ReadRateLimit(response);
            var status = (int)response.StatusCode;
            return status switch
            {
                >= 200 and < 300 => true,
                401 => throw new ConnectionException("GitHub 认证失败 (401)：PAT 无效或过期。", ConnectionHealthState.Unauthorized),
                403 => throw (RateLimitRemaining == 0
                    ? new ConnectionException("GitHub rate limit 耗尽 (403)。", ConnectionHealthState.Offline)
                    : new ConnectionException("GitHub 拒绝访问 (403)：检查 PAT workflow 授权。", ConnectionHealthState.Unauthorized)),
                404 => throw new ConnectionException("GitHub 资源不存在 (404)：检查仓库/workflow/run 配置。", ConnectionHealthState.Offline),
                409 => throw new ConnectionException("GitHub 冲突 (409)：运行可能已结束。", ConnectionHealthState.Degraded),
                422 => throw new ConnectionException("GitHub 校验失败 (422)：参数不合法。", ConnectionHealthState.Degraded),
                >= 500 => throw new ConnectionException($"GitHub 服务端错误 ({status})。", ConnectionHealthState.Offline),
                _ => throw new ConnectionException($"GitHub 意外响应 ({status})。", ConnectionHealthState.Degraded),
            };
        }
    }

    private void ReadRateLimit(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
            && int.TryParse(values.FirstOrDefault(), out var remaining))
        {
            RateLimitRemaining = remaining;
        }
    }

    private async Task<GitHubResponse> HandleStatusAsync(
        HttpResponseMessage response,
        string resource,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        switch (status)
        {
            case 304:
                return GitHubResponse.Unchanged();
            case >= 200 and < 300:
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var etag = response.Headers.ETag?.Tag;
                if (etag is not null)
                {
                    lock (_gate)
                    {
                        _etags[resource] = etag;
                    }
                }
                return GitHubResponse.Ok(body);
            }
            case 401:
                throw new ConnectionException("GitHub 认证失败 (401)：PAT 无效或过期。", ConnectionHealthState.Unauthorized);
            case 403:
                throw RateLimitRemaining == 0
                    ? new ConnectionException("GitHub rate limit 耗尽 (403)。", ConnectionHealthState.Offline)
                    : new ConnectionException("GitHub 拒绝访问 (403)：检查 PAT 授权范围。", ConnectionHealthState.Unauthorized);
            case 404:
                throw new ConnectionException("GitHub 资源不存在 (404)：检查仓库/分支/workflow 配置。", ConnectionHealthState.Offline);
            case >= 500:
                throw new ConnectionException($"GitHub 服务端错误 ({status})。", ConnectionHealthState.Offline);
            default:
                throw new ConnectionException($"GitHub 意外响应 ({status})。", ConnectionHealthState.Degraded);
        }
    }
}
