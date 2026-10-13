using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>bigmodel（智谱 GLM）Widget 类型元数据（positioning P0 #5：AI 套餐额度）。</summary>
public static class BigModelWidgetDescriptors
{
    public const string UsageType = "bigmodel.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "GLM Coding Plan 额度（智谱）",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认开，2026-10-11 用户令）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "GLM"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（%）", Placeholder: "默认 60"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（%）", Placeholder: "默认 90"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// bigmodel.usage（positioning P0 #5）：智谱 GLM Coding Plan 额度监控（国际站 Z.AI 同构，换 Endpoint 即用）。
/// 监控接口：GET {Endpoint}/api/monitor/usage/quota/limit，Authorization 头携带 API Key（无 Bearer 前缀）。
/// 响应 data.limits[]：TOKENS_LIMIT(unit=3,number=5) → 5 小时窗口、(6,1) → 周限、TIME_LIMIT(unit=5) → 月度（MCP
/// 调用数口径，含 usage/currentValue/remaining），每项含 percentage（0-100）与 nextResetTime（epoch ms）。
/// 2026-10-10 用户令+真 Key 实证：窗口上限无绝对 token 口（监控接口只给百分比，1302 限流消息实测无窗口数字），
/// 窗口规格以「窗名 窗口已用 N%」+ 重置时间呈现。多窗口口径（2026-10-13 用户令，覆盖 2026-10-10 的
/// 5h 主位规则）：级别/进度条/主数值一律取**已用最高的窗口**并标窗名（最差窗口优先），
/// warn/error 阈值仍作用在**最差窗口**的已用 % 上，语义不变。
/// </summary>
public sealed class BigModelUsageProvider : IWidgetProvider
{
    /// <summary>智谱国内版监控端点（团队版需在连接 Settings 里加组织/项目头，走自定义 Header 暂不支持）。</summary>
    public const string DefaultEndpoint = "https://open.bigmodel.cn/api/monitor/usage/quota/limit";

    /// <summary>国际站（Z.AI）端点：连接 Endpoint 填这个即可，解析结构相同。</summary>
    public const string ZaiEndpoint = "https://api.z.ai/api/monitor/usage/quota/limit";

    private readonly HttpClient? _client;

    public BigModelUsageProvider() { }

    public BigModelUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => BigModelWidgetDescriptors.Usage;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // 监控接口认证是裸 Key（无 Bearer 前缀）；连接显式配了 auth_prefix 则尊重配置
        var body = await HttpEndpoint.FetchAsync(
            _client,
            connection,
            context,
            cancellationToken,
            defaultEndpoint: DefaultEndpoint,
            defaultAuthPrefix: "").ConfigureAwait(false);

        var usage = ParseUsage(body);
        if (usage is null)
        {
            throw new ConnectionException("智谱响应不含配额数据（接口结构可能变化）", ConnectionHealthState.Degraded);
        }

        var warnPercent = widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, CultureInfo.InvariantCulture, out var warn) ? warn : 60;
        var errorPercent = widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, CultureInfo.InvariantCulture, out var error) ? error : 90;
        var worst = usage.Worst();
        var worstPercent = worst?.Window.Percent;
        var severity = MapSeverity(worstPercent, warnPercent, errorPercent);
        var head = widget.Config.GetValueOrDefault("label") ?? usage.Level ?? "GLM";
        var summary = head + Summarize(usage, worst);

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success,
            // 数值卡进度条：最差窗口已用（与级别判定同源，2026-10-13 用户令）；无数据 → null（tile 不画条）
            Progress = worstPercent is { } value ? Math.Clamp(value, 0, 100) / 100.0 : null,
            Summary = summary,
            DetailUrl = "https://open.bigmodel.cn/usage",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = worstPercent is { } percent ? Format(percent) : "", // tile 主数值（最差窗口已用 %）
                ["percent_window"] = worst?.Key ?? "",
                ["level"] = usage.Level ?? "",
                ["rolling_percent"] = usage.Rolling?.Percent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["weekly_percent"] = usage.Weekly?.Percent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["monthly_percent"] = usage.Monthly?.Percent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["rolling_reset_ms"] = usage.Rolling?.ResetMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["weekly_reset_ms"] = usage.Weekly?.ResetMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["monthly_reset_ms"] = usage.Monthly?.ResetMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["reset_iso"] = worst?.Window.ResetMs is { } reset ? FormatReset(reset) : "", // 最差窗口重置时间（本地时区 MM-dd HH:mm）
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>单窗口用量。</summary>
    public sealed record UsageWindow(double? Percent, long? ResetMs);

    /// <summary>归一化后的套餐用量：三窗口 + 套餐等级。</summary>
    public sealed record PlanUsage(string? Level, UsageWindow? Rolling, UsageWindow? Weekly, UsageWindow? Monthly)
    {
        /// <summary>已用最高的窗口（worst-window 规则唯一口径，2026-10-13 用户令；并列按 5h→周→月取先）；
        /// Key = 5h/weekly/monthly（payload percent_window 值），Label = 窗标；全缺或全部无百分比 → null。</summary>
        public (string Key, string Label, UsageWindow Window)? Worst()
        {
            (string Key, string Label, UsageWindow Window)? worst = null;
            foreach (var (key, label, window) in new[] { ("5h", "5h", Rolling), ("weekly", "周", Weekly), ("monthly", "月", Monthly) })
            {
                if (window?.Percent is not { } candidate)
                {
                    continue;
                }
                if (worst is null || candidate > worst.Value.Window.Percent)
                {
                    worst = (key, label, window);
                }
            }
            return worst;
        }
    }

    /// <summary>解析 quota/limit 响应（纯函数供单测）。结构对齐社区口径：data 包装可选，窗口按 type/unit/number 归类。</summary>
    internal static PlanUsage? ParseUsage(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var inner = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object ? data : root;
            var level = inner.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.String
                ? levelElement.GetString()
                : null;
            if (!inner.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            UsageWindow? rolling = null, weekly = null, monthly = null;
            foreach (var item in limits.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var type = item.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString() : null;
                double? percent = item.TryGetProperty("percentage", out var percentElement) && percentElement.ValueKind == JsonValueKind.Number
                    ? percentElement.GetDouble()
                    : null;
                // nextResetTime=0 是真实接口的「无重置信息」哨兵（月度 MCP 窗常给 0）——按缺省处理，
                // 否则摘要渲染出「01-01 00:00 重置」（38390521909 的 O1 失败输出实证）
                long? resetMs = item.TryGetProperty("nextResetTime", out var resetElement) && resetElement.ValueKind == JsonValueKind.Number && resetElement.TryGetInt64(out var ms) && ms > 0
                    ? ms
                    : null;
                var window = new UsageWindow(percent, resetMs);
                if (type == "TIME_LIMIT")
                {
                    monthly = window; // MCP 月度配额
                }
                else if (type == "TOKENS_LIMIT")
                {
                    var unit = item.TryGetProperty("unit", out var unitElement) && unitElement.ValueKind == JsonValueKind.Number && unitElement.TryGetInt32(out var u) ? u : 0;
                    var number = item.TryGetProperty("number", out var numberElement) && numberElement.ValueKind == JsonValueKind.Number && numberElement.TryGetInt32(out var n) ? n : 0;
                    if (unit == 3 && number == 5)
                    {
                        rolling = window; // 5 小时滚动窗口
                    }
                    else if (unit == 6 && number == 1)
                    {
                        weekly = window; // 周限
                    }
                }
            }
            return new PlanUsage(level, rolling, weekly, monthly);
        }
    }

    /// <summary>级别映射（纯函数供单测）：取最差窗口与阈值比；无数据 → Warning（宁报勿漏）。</summary>
    internal static Severity MapSeverity(double? worstPercent, double warnPercent, double errorPercent)
        => worstPercent is not { } worst ? Severity.Warning
            : worst >= errorPercent ? Severity.Error
            : worst >= warnPercent ? Severity.Warning
            : Severity.Success;

    /// <summary>摘要尾巴：最差窗口打头（已用 % + 重置时间，2026-10-13 用户令），其余窗口随后；
    /// 重置时间取监控接口原生 nextResetTime（epoch ms）——源自带字段，不做窗口文案推算。</summary>
    internal static string Summarize(PlanUsage usage, (string Key, string Label, UsageWindow Window)? worst)
    {
        var parts = new List<string>();
        if (worst is { } w && w.Window.Percent is { } worstPercent)
        {
            parts.Add($"{w.Label} 窗口已用 {Format(worstPercent)}%");
            if (w.Window.ResetMs is { } worstReset)
            {
                parts.Add($"{FormatReset(worstReset)} 重置");
            }
        }
        foreach (var (label, window) in new[] { ("5h", usage.Rolling), ("周", usage.Weekly), ("月", usage.Monthly) })
        {
            if (window?.Percent is { } percent && !ReferenceEquals(window, worst?.Window))
            {
                parts.Add($"{label} {Format(percent)}%");
            }
        }
        if (parts.Count == 0 && (usage.Weekly ?? usage.Monthly) is { } fallback)
        {
            parts.Add(fallback.ResetMs is { } fallbackReset ? $"{FormatReset(fallbackReset)} 重置" : "窗口数据");
        }
        return parts.Count == 0 ? "" : " · " + string.Join(" · ", parts);
    }

    /// <summary>epoch ms → 本地时区「MM-dd HH:mm」。</summary>
    internal static string FormatReset(long resetMs)
        => DateTimeOffset.FromUnixTimeMilliseconds(resetMs).ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
