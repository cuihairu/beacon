using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

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
/// 响应顶层 usages[]（按 scope=FEATURE_CODING 选条目）——条目级 detail = 周期窗（约一周），
/// limits[] = 5h 滚动窗（window:{duration,timeUnit} 归一化），totalQuota = 总额度；数值为字符串。
/// 旧形状（顶层 usage 对象 + 顶层 limits[] + user.membership.level，数字值）兼容兜底。
/// 级别按窗口剩余百分比最差者判：≤ error_percent → Error，≤ warn_percent → Warning，否则 Success（不变）。
/// 卡面/多窗口/进度条为**已用口径**（2026-10-13 用户令，与 bigmodel/ark 统一）：已用 = 100 − 剩余
/// （接口只给 remaining/limit，反推来源）；阈值判定语义仍按剩余，字段说明保持「余量低于此值」。
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
            // 主窗=5h 滚动，缺则周窗；级别判定与此无关（仍按剩余最差窗，MapSeverity 未动）。
            Progress = (usage.Rolling?.RemainingPercent ?? usage.Weekly?.RemainingPercent) is { } primaryRemaining
                ? Math.Clamp(UsedPercent(primaryRemaining), 0, 100) / 100.0
                : null,
            Summary = head + Summarize(usage),
            DetailUrl = "https://www.kimi.com/code",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
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
    public sealed record TotalQuota(double? Limit, double? Used, double? Remaining);

    /// <summary>归一化后的套餐余量：5h 滚动窗 + 周限 + 套餐等级 + 总额度。</summary>
    public sealed record KimiUsage(string? Level, QuotaWindow? Rolling, QuotaWindow? Weekly, TotalQuota? Total)
    {
        /// <summary>各窗口最差剩余（级别判定依据）；全部缺失 → null。100% 是合法值，不折叠。</summary>
        public double? WorstRemainingPercent
        {
            get
            {
                double? worst = null;
                foreach (var candidate in new[] { Rolling?.RemainingPercent, Weekly?.RemainingPercent })
                {
                    if (candidate is { } value && (worst is not { } current || value < current))
                    {
                        worst = value;
                    }
                }
                return worst;
            }
        }
    }

    /// <summary>解析 /v1/usages 响应（纯函数供单测）。真实形状（2026-10-11 实抓）：顶层 usages[]
    /// 按 scope=FEATURE_CODING 选条目（无匹配取首个含 detail 的），条目级 detail = 周期窗，
    /// 条目内 limits[] = 滚动窗明细，totalQuota = 总额度，数值为字符串。旧形状（顶层 usage 对象
    /// + 顶层 limits[]，数字值）兜底兼容。</summary>
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

            // 真实形状：usages[] 按 scope 选条目
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

            // 旧形状兜底：顶层 usage = 周限额，顶层 limits[] 为窗口明细
            if (!root.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var level = root.TryGetProperty("user", out var userElement) && userElement.ValueKind == JsonValueKind.Object
                && userElement.TryGetProperty("membership", out var membershipElement) && membershipElement.ValueKind == JsonValueKind.Object
                && membershipElement.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.String
                    ? levelElement.GetString()
                    : null;
            var (legacyRolling, legacyWeekly) = ParseWindowList(root);
            legacyWeekly ??= ParseWindow(usageElement); // 顶层 usage = 周限额（limits 未含周窗时兜底）
            return new KimiUsage(level, legacyRolling, legacyWeekly, ParseTotalQuota(root));
        }
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

    /// <summary>totalQuota:{limit,used,remaining} → 总额度（可缺省；全空 → null）。</summary>
    private static TotalQuota? ParseTotalQuota(JsonElement root)
    {
        if (!root.TryGetProperty("totalQuota", out var total) || total.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var limit = ReadDouble(total, "limit");
        var used = ReadDouble(total, "used");
        var remaining = ReadDouble(total, "remaining");
        return limit is null && used is null && remaining is null ? null : new TotalQuota(limit, used, remaining);
    }

    /// <summary>window:{duration,timeUnit} → 归一化标签（5h/weekly；兼容字符串 window；未识别 → null）。</summary>
    private static string? WindowLabel(JsonElement windowElement)
    {
        if (windowElement.ValueKind == JsonValueKind.String)
        {
            var raw = windowElement.GetString();
            return raw == "5h" || raw == "weekly" ? raw : null;
        }
        if (windowElement.ValueKind != JsonValueKind.Object
            || !windowElement.TryGetProperty("duration", out var durationElement) || !durationElement.TryGetInt32(out var duration)
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

    /// <summary>摘要尾巴：紧凑窗口已用列表（“ · 5h 已用26% · 周 已用45%”，已用=剩余反推，2026-10-13 用户令）；
    /// 余 0%（已用 100%）显示「已用尽」（bug 批9 措辞保留）。</summary>
    internal static string Summarize(KimiUsage usage)
    {
        var parts = new List<string>();
        if (usage.Rolling?.RemainingPercent is { } rolling)
        {
            parts.Add(rolling <= 0 ? "5h 已用尽" : $"5h 已用{Format(UsedPercent(rolling))}%");
        }
        if (usage.Weekly?.RemainingPercent is { } weekly)
        {
            parts.Add(weekly <= 0 ? "周 已用尽" : $"周 已用{Format(UsedPercent(weekly))}%");
        }
        return parts.Count == 0 ? "" : " · " + string.Join(" · ", parts);
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
