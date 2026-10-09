using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>qwen（阿里千问）Widget 类型元数据（AI Usage 第十家：百炼 Token Plan）。</summary>
public static class QwenWidgetDescriptors
{
    public const string UsageType = "qwen.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "阿里千问用量（百炼 Token Plan）",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "千问"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（%）", Placeholder: "默认 60"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（%）", Placeholder: "默认 90"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// qwen.usage（AI Usage 第十家）：阿里云百炼 Token Plan（Coding Plan 专属 Key，sk-sp- 前缀）。
/// **三档探测后如实降级**（2026-10-09 调研：官方 FAQ 无公开额度查询 REST API；社区实证
/// cc-switch#7484——专属 Key 只授权模型调用（/models 200），控制台查询口不授权）：
/// ①官方用量 API：无——控制台「我的订阅」页是唯一官方入口；
/// ②自定义用量端点：连接 Settings 填 usage_endpoint（GET + Bearer），响应按防御键名解析
/// （已用% / 剩余 credits / 重置时间），命中即显示**额度 + 重置日**（网关/代理口均适用）；
/// ③兜底：官方 /models 模型目录真数据 + 「额度口径官方未开放」如实标注，不编造数字。
/// Token Plan 窗口口径：个人版 7 天固定窗口、团队版月度（官方 FAQ），重置时间以端点数据为准。
/// </summary>
public sealed class QwenUsageProvider : IWidgetProvider
{
    public const string DefaultEndpoint = "https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1";
    public const string ConsoleUrl = "https://bailian.console.aliyun.com/cn-beijing/subscription/overview";

    private readonly HttpClient? _client;

    public QwenUsageProvider() { }

    public QwenUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => QwenWidgetDescriptors.Usage;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var usageEndpoint = connection.Settings.TryGetValue("usage_endpoint", out var custom) && custom.Length > 0
            ? custom
            : null;
        if (usageEndpoint is not null)
        {
            var usage = await FetchCustomUsageAsync(connection, context, usageEndpoint, cancellationToken).ConfigureAwait(false);
            return ToUsageState(widget, connection, usage);
        }
        var models = await FetchModelsAsync(connection, context, cancellationToken).ConfigureAwait(false);
        return ToCatalogState(widget, connection, models);
    }

    /// <summary>官方模型目录（真数据）：GET {endpoint}/models——专属 Key 实测可调（cc-switch#7484）。</summary>
    public async Task<QwenCatalog> FetchModelsAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync(connection, context).ConfigureAwait(false);
        var baseEndpoint = (connection.Endpoint?.Trim().Length > 0 ? connection.Endpoint!.Trim() : DefaultEndpoint).TrimEnd('/');
        var body = await SendAsync($"{baseEndpoint}/models", key, cancellationToken).ConfigureAwait(false);
        return ParseCatalog(body);
    }

    /// <summary>自定义用量端点：GET + Bearer，解析防御键名（已用%/剩余/重置）。</summary>
    public async Task<QwenPlanUsage> FetchCustomUsageAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        string usageEndpoint,
        CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync(connection, context).ConfigureAwait(false);
        var body = await SendAsync(usageEndpoint, key, cancellationToken).ConfigureAwait(false);
        var usage = ParseUsage(body);
        if (usage is null)
        {
            throw new ConnectionException(
                $"千问用量端点响应无可识别字段（需 percent/used_percent 或 remaining/total_credits 之一）：{Truncate(body)}",
                ConnectionHealthState.Degraded);
        }
        return usage;
    }

    private async Task<string> SendAsync(string url, string key, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        var client = _client ?? HttpEndpoint.Shared;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // 401/403=Key 无效（Degraded 配置可修）；5xx/其余=服务侧（Offline）
            throw new ConnectionException($"千问 API {(int)response.StatusCode}：{Truncate(body)}",
                response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                    ? ConnectionHealthState.Offline
                    : ConnectionHealthState.Degraded);
        }
        return body;
    }

    /// <summary>模型目录解析：data[].id；无 data 数组 → 结构异常 Degraded（接口变化需人查，不静默给 0）。</summary>
    public static QwenCatalog ParseCatalog(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = Unwrap(document.RootElement);
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new ConnectionException("千问 /models 响应不含 data 数组（接口结构可能变化）", ConnectionHealthState.Degraded);
        }
        var ids = new List<string>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                ids.Add(id.GetString()!);
            }
        }
        return new QwenCatalog(ids.Count, ids);
    }

    /// <summary>
    /// 用量解析（防御键名，全小写匹配）：已用% ← percent/used_percent/usedPercent；
    /// 或剩余% ← remaining_percent；credits ← remaining_credits|credits_remaining + total_credits|total；
    /// 重置 ← reset_at/reset_date/reset_time/period_end/reset（ISO 8601 或秒级 epoch）。
    /// 一项「已用%」都推不出来 → null（调用方转 Degraded：端点形状不识别，不编数）。
    /// </summary>
    public static QwenPlanUsage? ParseUsage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = Unwrap(document.RootElement);

        double? percentUsed = FirstNumber(root, "percent", "used_percent", "usedpercent", "usage_percent");
        double? remainingPercent = FirstNumber(root, "remaining_percent", "remainingpercent");
        if (percentUsed is null && remainingPercent is { } remaining)
        {
            percentUsed = 100 - remaining;
        }
        if (percentUsed is null)
        {
            return null;
        }

        var reset = FirstText(root, "reset_at", "reset_date", "reset_time", "period_end", "reset");
        var remainingCredits = FirstNumber(root, "remaining_credits", "credits_remaining");
        var totalCredits = FirstNumber(root, "total_credits", "total");

        if (percentUsed is null)
        {
            return null;
        }
        return new QwenPlanUsage(percentUsed.Value, reset, remainingCredits, totalCredits);
    }

    /// <summary>{"data": {...}} 包裹层剥开（OpenAI 风格端点常见），其余原样。</summary>
    private static JsonElement Unwrap(JsonElement root)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            ? data
            : root;

    private static double? FirstNumber(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetDouble(out var value))
                {
                    return value;
                }
            }
        }
        return null;
    }

    private static string? FirstText(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { } text && text.Length > 0)
                {
                    return text;
                }
                // 秒级 epoch（>10^9 视为时间戳，避免与 credits 数值混淆）
                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt64(out var epoch)
                    && epoch > 1_000_000_000)
                {
                    return epoch.ToString("0", CultureInfo.InvariantCulture);
                }
            }
        }
        return null;
    }

    internal static WidgetState ToUsageState(WidgetConfig widget, ConnectionConfig connection, QwenPlanUsage usage)
    {
        var warnPercent = widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, CultureInfo.InvariantCulture, out var warn) ? warn : 60;
        var errorPercent = widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, CultureInfo.InvariantCulture, out var error) ? error : 90;
        var percent = Math.Clamp(usage.PercentUsed, 0, 100);
        var severity = percent >= errorPercent ? Severity.Error
            : percent >= warnPercent ? Severity.Warning
            : Severity.Success;

        var head = widget.Config.GetValueOrDefault("label") ?? "千问";
        var resetText = FormatReset(usage.Reset);
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = QwenWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success,
            Progress = percent / 100.0, // 数值卡进度条：本期已用%（与级别判定同源）
            Summary = $"{head} · 本期已用 {Format(percent)}% · 重置 {resetText}",
            DetailUrl = ConsoleUrl,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = Format(percent),
                ["reset_iso"] = resetText,
                ["remaining_credits"] = usage.RemainingCredits is { } left ? Format(left) : "",
                ["total_credits"] = usage.TotalCredits is { } total ? Format(total) : "",
                ["usage_source"] = "custom",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    internal static WidgetState ToCatalogState(WidgetConfig widget, ConnectionConfig connection, QwenCatalog catalog)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "千问";
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = QwenWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = Severity.Info,
            Lifecycle = LifecycleState.Success,
            Summary = $"{head} · {catalog.Models} 模型可用 · 额度口径官方未开放（连接可配 usage_endpoint）",
            DetailUrl = ConsoleUrl,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["models"] = catalog.Models.ToString("0", CultureInfo.InvariantCulture),
                ["ids"] = string.Join(",", catalog.Ids),
                ["usage_source"] = "unavailable", // 如实口径：官方未开放用量接口，不编数
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>重置时间格式化：ISO 8601 → 本地「MM-dd HH:mm」；秒级 epoch 同样换算；解析失败原样透传。</summary>
    internal static string FormatReset(string? reset)
    {
        if (string.IsNullOrWhiteSpace(reset))
        {
            return "—";
        }
        if (long.TryParse(reset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch) && epoch > 1_000_000_000)
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        if (DateTimeOffset.TryParse(reset, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.LocalDateTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        return reset; // 非时间形状原样显示（端点自定义口径，不加工不猜测）
    }

    internal static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Truncate(string value) => value.Length <= 120 ? value : value[..120];

    public sealed record QwenCatalog(int Models, IReadOnlyList<string> Ids);

    /// <summary>自定义用量端点解析结果：PercentUsed 已用%（必有）；Reset 重置时间原串；credits 可缺。</summary>
    public sealed record QwenPlanUsage(double PercentUsed, string? Reset, double? RemainingCredits, double? TotalCredits);

    private static async Task<string> RequireKeyAsync(ConnectionConfig connection, ConnectionContext context)
    {
        var credentialRef = connection.CredentialRef ?? "qwen:default";
        var key = await context.Secrets.GetAsync(credentialRef).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ConnectionException("缺少阿里千问 API Key：百炼控制台「API-KEY」创建（Token Plan 专属 Key 为 sk-sp- 前缀）后填连接 Token 框",
                ConnectionHealthState.Degraded);
        }
        return key.Trim();
    }
}

/// <summary>qwen 连接（百炼 Token Plan）：测试连通 = GET {endpoint}/models；Key 无效 → Degraded（显式失败不静默）。</summary>
public sealed class QwenConnectionProvider : IConnectionProvider
{
    private readonly HttpMessageHandler? _handler;

    public QwenConnectionProvider() { }

    public QwenConnectionProvider(HttpMessageHandler handler) => _handler = handler;

    public string ConnectionType => "qwen";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await new QwenUsageProvider(_handler!).FetchModelsAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
