using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;

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
/// 窗口规格以「窗名 窗口已用 N%」+ 重置时间呈现。
/// **嵌套窗口口径（2026-10-15 用户令，覆盖 2026-10-13 的「最差窗口」规则）**：月 ⊇ 周 ⊇ 5h 为嵌套关系，
/// 外层耗尽 ⇒ 内层必然耗尽，故耗尽判定按 月 → 周 → 5h **外层优先**（不是已用最高——三窗并列 100%
/// 时并列取先会命中 5h，把月度耗尽报成「5h 已用」，界面遂显示内层还有余额）。耗尽恒 Error + 满格，
/// 且**只报真实耗尽层**（不再列内层用量）。正常态（三窗均有余额）5h 窗是限流档位
/// （Z.ai 5h 耗尽触发 300s 降级），故级别/进度条/主数值取展示窗（5h，缺失退周、月），warn/error 阈值作用其上。
/// 判定逻辑见 <see cref="QuotaWindows.Select"/>（Core 承载，可单测）。
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
        var head = widget.Config.GetValueOrDefault("label") ?? usage.Level ?? "GLM";

        // 嵌套窗口判定（月 ⊇ 周 ⊇ 5h）：耗尽态与正常态分叉，级别/进度条/主数值同源。
        var decision = QuotaWindows.Select(usage.Rolling?.Percent, usage.Weekly?.Percent, usage.Monthly?.Percent);

        // 耗尽态：最高级别 + 满格，**只报真实耗尽层**——外层已尽时内层不可能有余额，
        // 再把内层用量列出来即把内层读成「还有余额」（本次缺陷核心），故 payload 不出内层键、
        // 走 value_text 让 tile 主数值直接读耗尽（绕开多窗形态）。
        if (decision.Exhausted is { } layer)
        {
            return new WidgetState
            {
                WidgetId = widget.Id,
                WidgetType = Descriptor.Type,
                ConnectionId = connection.Id,
                Severity = Severity.Error, // 已尽恒错误级，不受阈值配置降级
                Lifecycle = LifecycleState.Failed,
                Progress = 1.0, // 数值卡进度条：已尽=满格
                Summary = $"{head} · {QuotaWindows.Label(layer)} 额度已耗尽"
                    + (ResetOf(layer, usage) is { } reset ? $"（{FormatReset(reset)} 重置）" : "（待重置）"),
                DetailUrl = "https://open.bigmodel.cn/usage",
                Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["value_text"] = $"{QuotaWindows.Label(layer)}额度已尽",
                    ["percent"] = Format(decision.ExhaustedPercent ?? 100), // 耗尽窗已用 %
                    ["percent_window"] = KeyOf(layer),
                    ["exhausted"] = "1",
                    ["exhausted_window"] = QuotaWindows.Label(layer),
                    ["exhausted_reason"] = decision.ExhaustedReason ?? "", // 报真实耗尽层，不伪装成内层
                    ["level"] = usage.Level ?? "",
                    ["reset_iso"] = ResetOf(layer, usage) is { } resetIso ? FormatReset(resetIso) : "",
                },
                FetchedAt = DateTimeOffset.UtcNow,
            };
        }

        // 正常态（含无数据兜底：级别 Warning 宁报勿漏、Progress null 不画条）：
        // 展示窗 = 5h（有额度时看 5h，限流档位），缺失退周、月。
        var severity = MapSeverity(decision.DisplayPercent, warnPercent, errorPercent);
        var displayed = decision.DisplayPercent;
        var displayWindow = decision.Display;
        var displayKey = displayWindow is { } window ? KeyOf(window) : "";
        var resetText = displayWindow is { } dWindow ? FormatResetOrNull(ResetOf(dWindow, usage)) : "";

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success,
            // 数值卡进度条：展示窗已用（与级别判定同源）；无数据 → null（tile 不画条）
            Progress = displayed is { } value ? Math.Clamp(value, 0, 100) / 100.0 : null,
            Summary = head + Summarize(usage, decision),
            DetailUrl = "https://open.bigmodel.cn/usage",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = displayed is { } percent ? Format(percent) : "", // tile 主数值（展示窗已用 %）
                ["percent_window"] = displayKey,
                ["level"] = usage.Level ?? "",
                ["rolling_percent"] = usage.Rolling?.Percent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["weekly_percent"] = usage.Weekly?.Percent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["monthly_percent"] = usage.Monthly?.Percent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["rolling_reset_ms"] = usage.Rolling?.ResetMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["weekly_reset_ms"] = usage.Weekly?.ResetMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["monthly_reset_ms"] = usage.Monthly?.ResetMs?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["reset_iso"] = resetText, // 展示窗重置时间（本地时区 MM-dd HH:mm）
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>单窗口用量。</summary>
    public sealed record UsageWindow(double? Percent, long? ResetMs);

    /// <summary>归一化后的套餐用量：三窗口 + 套餐等级。窗口判定一律走 <see cref="QuotaWindows.Select"/>（嵌套外层优先）。</summary>
    public sealed record PlanUsage(string? Level, UsageWindow? Rolling, UsageWindow? Weekly, UsageWindow? Monthly);

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

    /// <summary>摘要尾巴：展示窗打头（已用 % + 重置时间），其余窗口随后（窗序 5h → 周 → 月，只出实际存在的窗）；
    /// 重置时间取监控接口原生 nextResetTime（epoch ms）——源自带字段，不做窗口文案推算。
    /// 耗尽态不走此处（已尽分支只报真实耗尽层，不列内层）。</summary>
    internal static string Summarize(PlanUsage usage, QuotaWindowDecision decision)
    {
        var windows = new[]
        {
            (Window: QuotaWindow.FiveHour, Value: usage.Rolling),
            (Window: QuotaWindow.Weekly, Value: usage.Weekly),
            (Window: QuotaWindow.Monthly, Value: usage.Monthly),
        };
        var parts = new List<string>();
        if (decision.Display is { } dWindow && decision.DisplayPercent is { } usedPercent)
        {
            parts.Add($"{QuotaWindows.Label(dWindow)} 窗口已用 {Format(usedPercent)}%");
            if (ResetOf(dWindow, usage) is { } displayReset)
            {
                parts.Add($"{FormatReset(displayReset)} 重置");
            }
        }
        foreach (var (window, value) in windows)
        {
            if (value?.Percent is { } percent && window != decision.Display)
            {
                parts.Add($"{QuotaWindows.Label(window)} {Format(percent)}%");
            }
        }
        if (parts.Count == 0)
        {
            foreach (var (_, value) in windows)
            {
                if (value is { } present)
                {
                    return present.ResetMs is { } resetMs ? $" · {FormatReset(resetMs)} 重置" : " · 窗口数据";
                }
            }
            return "";
        }
        return " · " + string.Join(" · ", parts);
    }

    /// <summary>窗 → 重置时间（epoch ms，源自接口 nextResetTime）。</summary>
    private static long? ResetOf(QuotaWindow window, PlanUsage usage) => window switch
    {
        QuotaWindow.Monthly => usage.Monthly?.ResetMs,
        QuotaWindow.Weekly => usage.Weekly?.ResetMs,
        _ => usage.Rolling?.ResetMs,
    };

    /// <summary>窗 → payload percent_window / exhausted_window 键值（5h / weekly / monthly）。</summary>
    private static string KeyOf(QuotaWindow window) => window switch
    {
        QuotaWindow.Monthly => "monthly",
        QuotaWindow.Weekly => "weekly",
        _ => "5h",
    };

    /// <summary>空安全重置文案（接口 nextResetTime=0 已按缺省解析，此处只防空值）。</summary>
    private static string FormatResetOrNull(long? resetMs) => resetMs is { } ms ? FormatReset(ms) : "";

    /// <summary>epoch ms → 本地时区「MM-dd HH:mm」。</summary>
    internal static string FormatReset(long resetMs)
        => DateTimeOffset.FromUnixTimeMilliseconds(resetMs).ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
