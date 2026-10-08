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
/// 账号体系，Key 形如 sk-kimi-*）。接口（dsh-plugin-llm-balance 等社区实现交叉验证）：
/// GET https://api.kimi.com/coding/v1/usages，Authorization: Bearer &lt;Key&gt;。响应顶层 usage =
/// 周限额 { limit, used, remaining, resetTime }，limits[] 为窗口明细（window:{duration,timeUnit} →
/// 5h/weekly 归一化），user.membership.level = 套餐等级。级别按窗口剩余百分比最差者判：
/// ≤ error_percent → Error，≤ warn_percent → Warning，否则 Success。
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
            Progress = null,
            Summary = head + Summarize(usage),
            DetailUrl = "https://www.kimi.com/code",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["level"] = usage.Level ?? "",
                ["rolling_remaining_percent"] = usage.Rolling?.RemainingPercent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["weekly_remaining_percent"] = usage.Weekly?.RemainingPercent?.ToString("0.#", CultureInfo.InvariantCulture) ?? "",
                ["weekly_limit"] = usage.Weekly?.Limit?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                ["weekly_remaining"] = usage.Weekly?.Remaining?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
                ["weekly_reset"] = usage.Weekly?.ResetTime ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>单窗口余量（RemainingPercent = remaining/limit×100）。</summary>
    public sealed record QuotaWindow(double? RemainingPercent, double? Limit, double? Remaining, string? ResetTime);

    /// <summary>归一化后的套餐余量：5h 滚动窗 + 周限 + 套餐等级。</summary>
    public sealed record KimiUsage(string? Level, QuotaWindow? Rolling, QuotaWindow? Weekly)
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

    /// <summary>解析 /v1/usages 响应（纯函数供单测）：limits 明细优先，顶层 usage 兜底为周限。</summary>
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
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var level = root.TryGetProperty("user", out var userElement) && userElement.ValueKind == JsonValueKind.Object
                && userElement.TryGetProperty("membership", out var membershipElement) && membershipElement.ValueKind == JsonValueKind.Object
                && membershipElement.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.String
                    ? levelElement.GetString()
                    : null;

            QuotaWindow? rolling = null, weekly = null;
            if (root.TryGetProperty("limits", out var limits) && limits.ValueKind == JsonValueKind.Array)
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
            var topLevel = ParseWindow(usageElement);
            weekly ??= topLevel; // 顶层 usage = 周限额（limits 未含周窗时兜底）
            return new KimiUsage(level, rolling, weekly);
        }
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
        => item.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var value)
            ? value
            : null;

    /// <summary>级别映射（纯函数供单测）：剩余百分比越低越差；无数据 → Warning（宁报勿漏）。</summary>
    internal static Severity MapSeverity(double? worstRemainingPercent, double warnPercent, double errorPercent)
        => worstRemainingPercent is not { } worst ? Severity.Warning
            : worst <= errorPercent ? Severity.Error
            : worst <= warnPercent ? Severity.Warning
            : Severity.Success;

    /// <summary>摘要尾巴：紧凑窗口余量列表（“ · 5h 剩74% · 周 剩55%”）。</summary>
    internal static string Summarize(KimiUsage usage)
    {
        var parts = new List<string>();
        if (usage.Rolling?.RemainingPercent is { } rolling)
        {
            parts.Add($"5h 剩{Format(rolling)}%");
        }
        if (usage.Weekly?.RemainingPercent is { } weekly)
        {
            parts.Add($"周 剩{Format(weekly)}%");
        }
        return parts.Count == 0 ? "" : " · " + string.Join(" · ", parts);
    }

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}

/// <summary>kimi 连接：TestAsync = GET /v1/usages（Endpoint 可空取默认），Bearer 认证（sk-kimi-* Key）。</summary>
public sealed class KimiConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client;

    public KimiConnectionProvider() { }

    public KimiConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "kimi";

    public async Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
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
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
