using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Connections;

/// <summary>kimi（Kimi For Coding）Widget 类型元数据（positioning P0 #5：AI 套餐额度）。</summary>
public static class KimiWidgetDescriptors
{
    public const string CodingType = "kimi.coding";

    public static WidgetTypeDescriptor Coding { get; } = new()
    {
        Type = CodingType,
        DisplayName = "Kimi For Coding 套餐余量",
        PinSupported = true,
        DataKinds = DisplayTemplateKinds.ProgressOnly, // 类型级能力：产百分比（评审 P1 语义描述）
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认开，2026-10-11 用户令）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Kimi"),
            new WidgetFieldDescriptor("warn_percent", "余量低于此值告警（%）", Placeholder: "默认 30"),
            new WidgetFieldDescriptor("error_percent", "余量低于此值报错（%）", Placeholder: "默认 10"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Coding];
}

/// <summary>
/// kimi.coding（positioning P0 #5）：Kimi For Coding 套餐余量监控（注意与 Moonshot 开放平台是两套
/// 账号体系，Key 形如 sk-kimi-*）。真实接口（2026-10-11 用户实抓）：POST
/// https://www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages —— 控制台会话
/// （auth_header=Cookie，配 http_method=POST/http_body={}），凭据是浏览器 Cookie 不是 sk Key。
/// 社区端点 GET https://api.kimi.com/coding/v1/usages（Bearer sk-kimi-* Key）保留为默认兜底。
/// 响应三种形状：① 控制台会话（auth_header=Cookie，POST 控制台口）——顶层 usages[] 按
/// scope=FEATURE_CODING 选条目，条目级 detail = 周期窗（约一周），limits[] = 5h 滚动窗
/// （window:{duration,timeUnit} 归一化），totalQuota = 总额度；数值为字符串。② 社区端点
/// （Bearer sk-kimi-*，GET coding/v1/usages）——顶层 usages 对象 = 各窗 used_ratio
/// （limit_5h/limit_7d，权威已用占比），顶层 usage = 套餐总额度，limits[] = 窗口明细数值源。
/// ③ 旧形状（顶层 usage 对象 + 顶层 limits[] + user.membership.level，数字值）兼容兜底。
/// 级别按窗口剩余百分比最差者判：≤ error_percent → Error，≤ warn_percent → Warning，否则 Success（不变）。
/// 卡面/多窗口/进度条为**已用口径**（2026-10-13 用户令，与 bigmodel/ark 统一）：已用 = 100 − 剩余
/// （接口只给 remaining/limit，反推来源）；套餐窗是唯一例外（卡面用「剩%」标注，避免与窗口混淆）；
/// 阈值判定语义仍按剩余，字段说明保持「余量低于此值」。
/// 三窗口口径（2026-10-13 用户令）：5h 滚动窗 + 周窗 + 套餐窗（totalQuota 换算）三窗齐显——
/// 摘要/卡面 `5h 窗口已用X% · 周 窗口已用Y% · 套餐 剩Z%`（窗口带「窗口」字样，套餐用「剩%」标注非窗口）；
/// 进度条/主数值取**已用最高的窗口**（= 剩余最低的窗，并列 5h→周→套餐取先），级别判定同源（剩余最差窗），
/// warn/error 阈值仍按剩余百分比（越低越差，语义不变）。
/// </summary>
public sealed class KimiCodingUsageProvider : IWidgetProvider
{
    /// <summary>Kimi For Coding 用量端点（base https://api.kimi.com/coding + /v1/usages）。</summary>
    public const string DefaultEndpoint = "https://api.kimi.com/coding/v1/usages";

    private readonly HttpClient? _client;

    public KimiCodingUsageProvider() { }

    public KimiCodingUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => KimiWidgetDescriptors.Coding;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var body = await HttpEndpoint.FetchAsync(
            _client,
            connection,
            context,
            cancellationToken,
            defaultEndpoint: DefaultEndpoint,
            defaultAuthPrefix: "Bearer ").ConfigureAwait(false);

        var usage = ParseUsages(body);
        if (usage is null)
        {
            throw new ConnectionException("Kimi 响应不含套餐用量数据（接口结构可能变化）", ConnectionHealthState.Degraded);
        }

        var warnPercent = widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, CultureInfo.InvariantCulture, out var warn) ? warn : 30;
        var errorPercent = widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, CultureInfo.InvariantCulture, out var error) ? error : 10;
        var severity = MapSeverity(usage.WorstRemainingPercent, warnPercent, errorPercent);
        var worst = usage.WorstWindow();
        var head = widget.Config.GetValueOrDefault("label") ?? usage.Level ?? "Kimi";

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success,
            // 进度条与其他 provider 同语义（已用占比；bigmodel/ark/mimo/qwen/http.quota 同口径）。
            // 最差窗口优先（2026-10-13 用户令）：取剩余最低窗的已用占比——级别判定同源（WorstRemainingPercent）。
            Progress = worst?.Window.RemainingPercent is { } worstRemaining
                ? Math.Clamp(UsedPercent(worstRemaining), 0, 100) / 100.0
                : null,
            Summary = head + Summarize(usage),
            DetailUrl = "https://www.kimi.com/code",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = worst?.Window.RemainingPercent is { } remainingUsed ? UsedPercent(remainingUsed).ToString("0.#", CultureInfo.InvariantCulture) : "",
                ["worst_window"] = worst?.Key ?? "",
                ["plan_remaining_percent"] = usage.Plan?.RemainingPercent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["level"] = usage.Level ?? "",
                ["rolling_percent"] = usage.Rolling?.RemainingPercent is { } rollingUsed ? UsedPercent(rollingUsed).ToString("0.#", CultureInfo.InvariantCulture) : "",
                ["weekly_percent"] = usage.Weekly?.RemainingPercent is { } weeklyUsed ? UsedPercent(weeklyUsed).ToString("0.#", CultureInfo.InvariantCulture) : "",
                ["rolling_remaining_percent"] = usage.Rolling?.RemainingPercent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["weekly_remaining_percent"] = usage.Weekly?.RemainingPercent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["weekly_limit"] = usage.Weekly?.Limit?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                ["weekly_remaining"] = usage.Weekly?.Remaining?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                ["weekly_reset"] = usage.Weekly?.ResetTime ?? "",
                ["total_limit"] = usage.Total?.Limit?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                ["total_used"] = usage.Total?.Used?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                ["total_remaining"] = usage.Total?.Remaining?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>单窗口余量（RemainingPercent = remaining/limit×100）。</summary>
    public sealed record QuotaWindow(double? RemainingPercent, double? Limit, double? Remaining, string? ResetTime);

    /// <summary>套餐总额度（totalQuota；真实响应数值为字符串）。</summary>
    public sealed record TotalQuota(double? Limit, double? Used, double? Remaining, string? ResetTime);

    /// <summary>归一化后的套餐余量：5h 滚动窗 + 周限 + 套餐等级 + 总额度。</summary>
    public sealed record KimiUsage(string? Level, QuotaWindow? Rolling, QuotaWindow? Weekly, TotalQuota? Total)
    {
        /// <summary>套餐窗（totalQuota 换算剩余%；无 totalQuota 或无数值 → null）。</summary>
        public QuotaWindow? Plan => Total is { } total && total.Limit > 0 && total.Remaining is { } planRemaining
            ? new QuotaWindow(planRemaining / total.Limit * 100, total.Limit, total.Remaining, total.ResetTime)
            : null;

        /// <summary>各窗口最差剩余（级别判定依据，含套餐窗）；全部缺失 → null。100% 是合法值，不折叠。</summary>
        public double? WorstRemainingPercent
        {
            get
            {
                double? worst = null;
                foreach (var candidate in new[] { Rolling?.RemainingPercent, Weekly?.RemainingPercent, Plan?.RemainingPercent })
                {
                    if (candidate is { } value && (worst is not { } current || value < current))
                    {
                        worst = value;
                    }
                }
                return worst;
            }
        }

        /// <summary>最差窗口（剩余最低 = 已用最高；并列按 5h→周→套餐取先）——worst-window 规则唯一口径
        /// （2026-10-13 用户令）：进度条/主数值取它，Key = rolling/weekly/plan（payload worst_window 值）。</summary>
        public (string Key, string Label, QuotaWindow Window)? WorstWindow()
        {
            (string Key, string Label, QuotaWindow Window)? worst = null;
            foreach (var (key, label, window) in new[] { ("rolling", "5h", Rolling), ("weekly", "周", Weekly), ("plan", "套餐", Plan) })
            {
                if (window?.RemainingPercent is not { } remaining)
                {
                    continue;
                }
                if (worst is null || remaining < worst.Value.Window.RemainingPercent)
                {
                    worst = (key, label, window);
                }
            }
            return worst;
        }
    }

    /// <summary>解析 /v1/usages 响应（纯函数供单测）。三种形状：
    /// ① 控制台 POST 口（2026-10-11 实抓）：顶层 usages[] 按 scope=FEATURE_CODING 选条目（无匹配取首个含
    ///    detail 的），条目级 detail = 周期窗，条目内 limits[] = 滚动窗明细，totalQuota = 总额度，数值为字符串；
    /// ② 社区口（2026-10-13 实抓）：顶层 usages 对象 = 各窗 used_ratio（limit_5h/limit_7d，权威已用占比），
    ///    顶层 usage = 套餐总额度，limits[] = 窗口明细（limit/remaining 数值源，duration 容忍字符串）；
    /// ③ 旧形状兜底：顶层 usage = 周限额，顶层 limits[] 为窗口明细。</summary>
    internal static KimiUsage? ParseUsages(string json)
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

            // ① 控制台口：usages[] 按 scope 选条目
            if (root.TryGetProperty("usages", out var usages) && usages.ValueKind == JsonValueKind.Array)
            {
                JsonElement? coding = null;
                foreach (var item in usages.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || !item.TryGetProperty("detail", out var detail) || detail.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    coding ??= item; // 兜底：首个含 detail 的条目
                    if (item.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.String
                        && scope.GetString() == "FEATURE_CODING")
                    {
                        coding = item;
                        break;
                    }
                }
                if (coding is not { } entry)
                {
                    return null;
                }
                var (rolling, weeklyFromLimits) = ParseWindowList(entry);
                var weekly = weeklyFromLimits ?? ParseWindow(entry.GetProperty("detail")); // 条目 detail = 周期窗
                return new KimiUsage(Level: null, rolling, weekly, ParseTotalQuota(root));
            }

            // ② 社区口：usages 对象 = 各窗 used_ratio（权威已用占比）
            var hasUsagesObject = root.TryGetProperty("usages", out var usagesObject) && usagesObject.ValueKind == JsonValueKind.Object;
            var (ratioRolling, ratioWeekly) = hasUsagesObject ? ParseUsedRatioWindows(usagesObject) : (null, null);

            // ② 的数值源 / ③ 旧形状兜底
            if (!root.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var level = ParseMembershipLevel(root);
            var (limitsRolling, limitsWeekly) = ParseWindowList(root);
            // used_ratio 是权威已用占比；limits[] detail 只补 limit/remaining/reset 数值源
            var rollingWindow = MergeWindow(ratioRolling, limitsRolling);
            var weeklyWindow = MergeWindow(ratioWeekly, limitsWeekly);
            // 有 usages 对象时顶层 usage = 套餐总额度（不再当周窗）；无 usages 对象（旧形状）顶层 usage = 周限额
            var total = hasUsagesObject ? ParseTotalQuotaObject(usageElement) : ParseTotalQuota(root);
            if (!hasUsagesObject && weeklyWindow is null)
            {
                weeklyWindow = ParseWindow(usageElement); // 旧形状兜底：窗口源都缺时顶层 usage 当周限额
            }
            return new KimiUsage(level, rollingWindow, weeklyWindow, total);
        }
    }

    /// <summary>user.membership.level → 套餐等级（社区口带等级；控制台口无等级 → null，head 落 label 或「Kimi」）。</summary>
    private static string? ParseMembershipLevel(JsonElement root)
        => root.TryGetProperty("user", out var userElement) && userElement.ValueKind == JsonValueKind.Object
            && userElement.TryGetProperty("membership", out var membershipElement) && membershipElement.ValueKind == JsonValueKind.Object
            && membershipElement.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.String
                ? levelElement.GetString()
                : null;

    /// <summary>usages 对象（社区口 2026-10-13 实抓）→（5h 滚动窗, 周窗）：limit_5h/limit_7d 的 used_ratio 是
    /// 权威已用占比（0-1），剩余% = 100 − used_ratio×100；窗口名后缀非 5h/7d 的键不入摘要（不误标）。</summary>
    private static (QuotaWindow? Rolling, QuotaWindow? Weekly) ParseUsedRatioWindows(JsonElement usages)
    {
        QuotaWindow? rolling = null, weekly = null;
        foreach (var item in usages.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var usedRatio = ReadDouble(item.Value, "used_ratio");
            if (usedRatio is not { } ratio)
            {
                continue;
            }
            var resetTime = item.Value.TryGetProperty("reset_time", out var resetElement) && resetElement.ValueKind == JsonValueKind.String
                ? resetElement.GetString()
                : null;
            var window = new QuotaWindow(Math.Clamp(100 - ratio * 100, 0, 100), null, null, resetTime);
            switch (item.Name)
            {
                case "limit_5h":
                    rolling ??= window;
                    break;
                case "limit_7d" or "limit_1w":
                    weekly ??= window;
                    break;
            }
        }
        return (rolling, weekly);
    }

    /// <summary>合并窗口：used_ratio 换算的剩余百分比为权威口径，limits[] detail 补 limit/remaining/reset 数值源。</summary>
    private static QuotaWindow? MergeWindow(QuotaWindow? ratio, QuotaWindow? detail)
    {
        if (ratio?.RemainingPercent is not { } percent)
        {
            return detail ?? ratio;
        }
        if (detail is null)
        {
            return ratio;
        }
        return new QuotaWindow(percent, detail.Limit ?? ratio.Limit, detail.Remaining ?? ratio.Remaining,
            ratio.ResetTime ?? detail.ResetTime);
    }

    /// <summary>limits[] →（滚动窗, 周窗）：window:{duration,timeUnit} 归一化 5h/weekly，未识别窗口跳过（不误标）。</summary>
    private static (QuotaWindow? Rolling, QuotaWindow? Weekly) ParseWindowList(JsonElement parent)
    {
        QuotaWindow? rolling = null, weekly = null;
        if (parent.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in limits.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("detail", out var detail) || detail.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("window", out var windowElement))
                {
                    continue;
                }
                var label = WindowLabel(windowElement);
                if (label is not ("5h" or "weekly"))
                {
                    continue; // 未识别窗口不入摘要（不误标）
                }
                var window = ParseWindow(detail);
                if (label == "5h")
                {
                    rolling = window;
                }
                else if (weekly is null)
                {
                    weekly = window;
                }
            }
        }
        return (rolling, weekly);
    }

    /// <summary>totalQuota 键 → 总额度（可缺省；全空 → null）。</summary>
    private static TotalQuota? ParseTotalQuota(JsonElement root)
        => root.TryGetProperty("totalQuota", out var total) && total.ValueKind == JsonValueKind.Object
            ? ParseTotalQuotaObject(total)
            : null;

    /// <summary>套餐总额度对象（totalQuota / 顶层 usage）→ TotalQuota（全空 → null）。</summary>
    private static TotalQuota? ParseTotalQuotaObject(JsonElement total)
    {
        var limit = ReadDouble(total, "limit");
        var used = ReadDouble(total, "used");
        var remaining = ReadDouble(total, "remaining");
        if (limit is null && used is null && remaining is null)
        {
            return null;
        }
        var resetTime = total.TryGetProperty("resetTime", out var resetElement) && resetElement.ValueKind == JsonValueKind.String
            ? resetElement.GetString()
            : null;
        return new TotalQuota(limit, used, remaining, string.IsNullOrEmpty(resetTime) ? null : resetTime);
    }

    /// <summary>window:{duration,timeUnit} → 归一化标签（5h/weekly；兼容字符串 window 与字符串 duration；未识别 → null）。</summary>
    private static string? WindowLabel(JsonElement windowElement)
    {
        if (windowElement.ValueKind == JsonValueKind.String)
        {
            var raw = windowElement.GetString();
            return raw == "5h" || raw == "weekly" ? raw : null;
        }
        if (windowElement.ValueKind != JsonValueKind.Object
            || !ReadInt(windowElement, "duration", out var duration)
            || !windowElement.TryGetProperty("timeUnit", out var unitElement) || unitElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return unitElement.GetString() switch
        {
            "TIME_UNIT_HOUR" or "HOUR" => duration + "h",
            "TIME_UNIT_DAY" or "DAY" => duration == 7 ? "weekly" : duration + "d",
            "TIME_UNIT_MINUTE" or "MINUTE" => duration % 60 == 0 ? duration / 60 + "h" : duration + "min",
            "TIME_UNIT_SECOND" or "SECOND" => duration == 18000 ? "5h" : duration == 604800 ? "weekly" : duration + "s",
            _ => null,
        };
    }

    /// <summary>取整数字段：容忍字符串数值（真实响应 duration 可能是 "300"——只认 JSON number 会把 5h 窗口整条丢掉）。</summary>
    private static bool ReadInt(JsonElement item, string property, out int value)
    {
        value = 0;
        if (!item.TryGetProperty(property, out var element))
        {
            return false;
        }
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            value = number;
            return true;
        }
        if (element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    private static QuotaWindow ParseWindow(JsonElement detail)
    {
        double? limit = ReadDouble(detail, "limit");
        double? remaining = ReadDouble(detail, "remaining");
        double? percent = limit is { } l && l > 0 && remaining is { } r ? r / l * 100 : null;
        var resetTime = detail.TryGetProperty("resetTime", out var resetElement) && resetElement.ValueKind == JsonValueKind.String
            ? resetElement.GetString()
            : null;
        return new QuotaWindow(percent, limit, remaining, string.IsNullOrEmpty(resetTime) ? null : resetTime);
    }

    private static double? ReadDouble(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var element))
        {
            return null;
        }
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out var number) => number,
            // 真实响应数值是字符串（"100"），容忍解析
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>级别映射（纯函数供单测）：剩余百分比越低越差；无数据 → Warning（宁报勿漏）。</summary>
    internal static Severity MapSeverity(double? worstRemainingPercent, double warnPercent, double errorPercent)
        => worstRemainingPercent is not { } worst ? Severity.Warning
            : worst <= errorPercent ? Severity.Error
            : worst <= warnPercent ? Severity.Warning
            : Severity.Success;

    /// <summary>摘要尾巴：三窗齐显（2026-10-13 用户令）——窗序固定 5h → 周 → 套餐（自然序，主数值/进度条另取最差窗）；
    /// 窗口段「5h 窗口已用X%」（带「窗口」字样避免与套餐混淆），套餐段「套餐 剩Y%」（剩% 口径，标注非窗口）；
    /// 余 0%（已用 100%）显示「已用尽」（bug 批9 措辞保留）。</summary>
    internal static string Summarize(KimiUsage usage)
    {
        var parts = new List<string>();
        foreach (var (label, window) in new[] { ("5h", usage.Rolling), ("周", usage.Weekly) })
        {
            if (window?.RemainingPercent is { } remaining)
            {
                parts.Add(WindowText(label, window));
            }
        }
        if (usage.Plan?.RemainingPercent is { } planRemaining)
        {
            parts.Add(planRemaining <= 0 ? "套餐 已用尽" : $"套餐 剩{Format(planRemaining)}%");
        }
        return parts.Count == 0 ? "" : " · " + string.Join(" · ", parts);
    }

    private static string WindowText(string label, QuotaWindow window)
    {
        if (window.RemainingPercent is not { } remaining)
        {
            return $"{label} 窗口"; // 无剩余数据不硬造数字（摘要路径只收有剩余的窗，防御兜底）
        }
        return remaining <= 0 ? $"{label} 窗口已用尽" : $"{label} 窗口已用{Format(UsedPercent(remaining))}%";
    }

    /// <summary>已用% = 100 − 剩余%（接口只给 remaining/limit，反推即来源；钳 0..100 防脏数据）。</summary>
    private static double UsedPercent(double remainingPercent) => Math.Clamp(100 - remainingPercent, 0, 100);

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}

/// <summary>kimi 连接：TestAsync = GET /v1/usages（Endpoint 可空取默认），Bearer 认证（sk-kimi-* Key）。
/// 控制台 POST 端点经连接设置覆盖：endpoint + http_method=POST + http_body + auth_header=Cookie。</summary>
public sealed class KimiConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client;

    public KimiConnectionProvider() { }

    public KimiConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "kimi";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await HttpEndpoint.FetchAsync(
                _client,
                connection,
                context,
                cancellationToken,
                defaultEndpoint: KimiCodingUsageProvider.DefaultEndpoint,
                defaultAuthPrefix: "Bearer ").ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
