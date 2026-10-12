using System.Net.Http;
using System.Net.Http.Headers;
using Beacon.Core.Services;
using Microsoft.Extensions.Logging;

namespace Beacon.App.Services;

/// <summary>一次检查的落点：远程 tag（失败为 null）、时间、判定、是否网络层失败。（public：UpdateCheckService.Last/CheckAsync 的公开返回形态，CS0053/CS0050）</summary>
public sealed record UpdateCheckOutcome(
    string? RemoteTag, DateTimeOffset CheckedAt, UpdateCheck.Verdict Verdict, bool NetworkFailure)
{
    /// <summary>网络/HTTP/解析失败的统一降级落点（Offline 口径：不抛异常，只记录）。</summary>
    public static UpdateCheckOutcome Failure(DateTimeOffset at) =>
        new(RemoteTag: null, at, UpdateCheck.Verdict.Incomparable, NetworkFailure: true);
}

/// <summary>
/// 检查更新服务（B-806）：调 GitHub releases/latest 拿 tag_name，与本地 InformationalVersion 比较。
/// 限流口径：GitHub 未认证 60 次/小时/IP——进程生命周期内只缓存一次结果，手动「检查更新」强制刷新；
/// 所有失败（断网/超时/非 200/限流体）都折返 Failure 落点，绝不向 UI 抛异常。
/// </summary>
public sealed class UpdateCheckService
{
    private const string LatestApiUrl = "https://api.github.com/repos/cuihairu/beacon/releases/latest";
    private const string DownloadPageUrl = "https://github.com/cuihairu/beacon/releases/latest";
    private const int TimeoutSeconds = 10;

    private static readonly HttpClient Http = CreateClient();

    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>进程内缓存：上一次检查结果（null = 尚未检查）。启动延迟自检与手动检查共用。</summary>
    public UpdateCheckOutcome? Last { get; private set; }

    public UpdateCheckService(ILogger? logger) => _logger = logger;

    /// <summary>下载页直链（releases/latest 由 GitHub 重定向到最新正式发布）。</summary>
    public static string DownloadPage => DownloadPageUrl;

    /// <summary>
    /// 执行检查。force=false 时直接返回缓存（供启动自检去重：手动先查过就不再打网络）；
    /// force=true 才真正发请求。任何异常都折返 Failure，不抛出。
    /// </summary>
    public async Task<UpdateCheckOutcome> CheckAsync(bool force, CancellationToken ct = default)
    {
        if (!force && Last is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (!force && Last is { } cachedAgain)
            {
                return cachedAgain;
            }

            var at = DateTimeOffset.Now;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            string? tag = null;
            try
            {
                using var response = await Http.GetAsync(LatestApiUrl, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    tag = UpdateCheck.ExtractTagName(body);
                }
                else
                {
                    _logger?.LogWarning(
                        "检查更新失败：HTTP {Status}（限流或网络问题），body 前 200 字：{Body}",
                        (int)response.StatusCode, body.Length > 200 ? body[..200] : body);
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("检查更新超时或被取消（{TimeoutSeconds}s）", TimeoutSeconds);
                return Last = UpdateCheckOutcome.Failure(at);
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogWarning(ex, "检查更新失败：网络不可达");
                return Last = UpdateCheckOutcome.Failure(at);
            }

            if (tag is null)
            {
                // 非 200、限流 body、畸形 JSON 都落到这里。
                return Last = UpdateCheckOutcome.Failure(at);
            }

            var verdict = UpdateCheck.Evaluate(LocalVersion(), tag);
            _logger?.LogInformation("检查更新：本地 {Local}，远程 {Remote} → {Verdict}", LocalVersion(), tag, verdict);
            return Last = new UpdateCheckOutcome(tag, at, verdict, NetworkFailure: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Last ?? UpdateCheckOutcome.Failure(DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "检查更新出现未预期异常，按失败降级");
            return Last = UpdateCheckOutcome.Failure(DateTimeOffset.Now);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>本地版本口径：csproj Version → InformationalVersion（daily-build 会注入 日期+SHA）。</summary>
    public static string LocalVersion() =>
        typeof(UpdateCheckService).Assembly.GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "0.0.0";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
        };
        // GitHub API 强制要求 User-Agent（缺失直接 403）。
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Beacon-Update-Check");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
