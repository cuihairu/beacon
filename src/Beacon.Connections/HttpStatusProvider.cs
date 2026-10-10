using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Connections;

/// <summary>HTTP Widget 类型元数据（positioning P0 #3 + 验收反馈额度卡）：驱动 Settings 向导与 L0 准入。</summary>
public static class HttpWidgetDescriptors
{
    public const string StatusType = "http.status";
    public const string QuotaType = "http.quota";

    public static WidgetTypeDescriptor Status { get; } = new()
    {
        Type = StatusType,
        DisplayName = "HTTP 状态（自定义接口）",
        PinSupported = true,
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "打包机 A"),
            new WidgetFieldDescriptor("icon", "图标", Placeholder: "默认按连接品牌", Choices: BrandIcons.PickerKeys),
            new WidgetFieldDescriptor("status_path", "状态字段路径", Required: true, Placeholder: "$.status"),
            new WidgetFieldDescriptor("summary_path", "摘要字段路径", Placeholder: "$.message"),
            new WidgetFieldDescriptor("url_path", "详情链接路径", Placeholder: "$.html_url"),
            new WidgetFieldDescriptor("success_values", "成功状态词（逗号分隔）", Placeholder: "默认 success,ok,done,passed,healthy,finished"),
            new WidgetFieldDescriptor("warning_values", "进行中状态词（逗号分隔）", Placeholder: "默认 running,building,pending,queued"),
            new WidgetFieldDescriptor("error_values", "失败状态词（逗号分隔）", Placeholder: "默认 failed,failure,error,critical"),
        ],
    };

    public static WidgetTypeDescriptor Quota { get; } = new()
    {
        Type = QuotaType,
        DisplayName = "HTTP 额度（数值卡，任意配额 JSON）",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "GLM 额度"),
            new WidgetFieldDescriptor("icon", "图标", Placeholder: "默认按连接品牌", Choices: BrandIcons.PickerKeys),
            new WidgetFieldDescriptor("total_path", "总量字段路径", Required: true, Placeholder: "$.data.total"),
            new WidgetFieldDescriptor("used_path", "已用字段路径（与剩余二选一）", Placeholder: "$.data.used"),
            new WidgetFieldDescriptor("remaining_path", "剩余字段路径（与已用二选一）", Placeholder: "$.data.remaining"),
            new WidgetFieldDescriptor("reset_path", "重置时间路径（epoch 秒/毫秒/ISO 自动识别）", Placeholder: "$.data.reset_at"),
            new WidgetFieldDescriptor("unit", "单位（如 tokens / 元）", Placeholder: "tokens"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（已用 %）", Placeholder: "默认 60"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（已用 %）", Placeholder: "默认 90"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Status, Quota];
}

/// <summary>
/// http.status（positioning P0 #3 Generic HTTP）：任意局域网/内部 HTTP JSON 接口 → 统一状态。
/// 状态词→级别映射全部可配（widget.Config 逗号分隔词表），默认口径取 positioning.md §3：
/// running/building → Warning，success/ok → Success，failed/error → Error；空/未知词 → Warning（宁报勿漏）。
/// </summary>
public sealed class HttpStatusProvider : IWidgetProvider
{
    private readonly HttpClient? _client; // null = 共享池（生产路径）；测试注入伪造 handler

    public HttpStatusProvider() { }

    public HttpStatusProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => HttpWidgetDescriptors.Status;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var body = await HttpEndpoint.FetchAsync(_client, connection, context, cancellationToken).ConfigureAwait(false);

        string? status, summary, url;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            status = HttpJsonPath.Select(root, widget.Config.GetValueOrDefault("status_path") ?? "$.status");
            summary = HttpJsonPath.Select(root, widget.Config.GetValueOrDefault("summary_path"));
            url = HttpJsonPath.Select(root, widget.Config.GetValueOrDefault("url_path"));
        }
        catch (JsonException exception)
        {
            throw new ConnectionException($"HTTP 响应不是合法 JSON：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }

        var (lifecycle, severity) = MapStatus(status, SuccessValues(widget), WarningValues(widget), ErrorValues(widget));
        var label = widget.Config.GetValueOrDefault("label");
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = lifecycle,
            Progress = null, // 状态灯语义，无进度百分比
            Summary = label is { Length: > 0 }
                ? summary is { Length: > 0 } ? $"{label} · {summary}" : $"{label} · {status ?? "unknown"}"
                : summary is { Length: > 0 } ? summary : $"HTTP · {status ?? "unknown"}",
            DetailUrl = url,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = status ?? "",
                ["endpoint"] = connection.Endpoint ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    internal static HashSet<string> SuccessValues(WidgetConfig widget) => Words(widget, "success_values", "success,ok,done,passed,healthy,finished");

    internal static HashSet<string> WarningValues(WidgetConfig widget) => Words(widget, "warning_values", "running,building,pending,queued,in_progress,deploying,starting");

    internal static HashSet<string> ErrorValues(WidgetConfig widget) => Words(widget, "error_values", "failed,failure,error,critical,broken");

    private static HashSet<string> Words(WidgetConfig widget, string key, string fallback)
    {
        var raw = widget.Config.GetValueOrDefault(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = fallback;
        }
        return [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(w => w.ToLowerInvariant())];
    }

    /// <summary>状态词→生命周期/级别（表驱动纯函数供单测）。错误词优先判定（fail-loud）。</summary>
    internal static (LifecycleState Lifecycle, Severity Severity) MapStatus(string? status, HashSet<string> success, HashSet<string> warning, HashSet<string> error)
    {
        var key = status?.Trim().ToLowerInvariant() ?? "";
        if (key.Length > 0)
        {
            if (error.Contains(key))
            {
                return (LifecycleState.Failed, Severity.Error);
            }
            if (warning.Contains(key))
            {
                return (LifecycleState.Running, Severity.Warning);
            }
            if (success.Contains(key))
            {
                return (LifecycleState.Success, Severity.Success);
            }
        }
        return (LifecycleState.Unknown, Severity.Warning);
    }
}
