using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>mimo（小米 MiMo）Widget 类型元数据（positioning P0 #5：AI Usage 七家之一）。</summary>
public static class MiMoWidgetDescriptors
{
    public const string UsageType = "mimo.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "小米 MiMo 用量（开放平台）",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "MiMo"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// mimo.usage（positioning P0 #5）：小米 MiMo 开放平台用量卡——**官方无额度接口，如实降级**（2026-10-08 首测、
/// 2026-10-10 复核：推理域 token-plan-cn.xiaomimimo.com 的 /usages、/usage、/quota、/balance 等 7 端点全 404，
/// /v1/models 与 /v1/chat/completions 响应均无限流头；控制台需登录态）。
/// 额度位按用户令（2026-10-10）给真实口径：连接 Settings 填 usage_endpoint（GET + Bearer）指向本机计数源，
/// 防御解析 total_calls/window_calls/window_minutes（{"data":{...}} 包裹剥开）→「本机累计 N 次 · 近M分 K 次」，
/// 数据源注明本机计数；无计数键则原样透传（不加工）。两者皆无 → 额度位「无额度口」+ 模型数降 Summary 次行
/// ——模型数是目录不是额度，不得冒充（用户令）。Key 本机（litellm/relay）无调用方：litellm 的
/// mimo-v2.6-flash-free 走 DeepSeek serverless 中转非小米直连，小米 key 真实用量对本机组件不可见。
/// </summary>
public sealed class MiMoUsageProvider : IWidgetProvider
{
    public const string DefaultEndpoint = "https://token-plan-cn.xiaomimimo.com/v1";

    private readonly HttpClient? _client;

    public MiMoUsageProvider() { }

    public MiMoUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => MiMoWidgetDescriptors.Usage;

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
            return await FetchCustomUsageAsync(widget, connection, context, usageEndpoint, cancellationToken).ConfigureAwait(false);
        }
        var models = await FetchModelsAsync(connection, context, cancellationToken).ConfigureAwait(false);
        return ToState(widget, connection, models);
    }

    /// <summary>官方模型目录（真数据）：/v1/models → 模型数与 id 清单。</summary>
    public async Task<MiMoCatalog> FetchModelsAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync(connection, context).ConfigureAwait(false);
        var baseEndpoint = (connection.Endpoint?.Trim().Length > 0 ? connection.Endpoint!.Trim() : DefaultEndpoint).TrimEnd('/');
        var body = await SendAsync($"{baseEndpoint}/models", key, cancellationToken).ConfigureAwait(false);
        return ParseCatalog(body);
    }

    private async Task<WidgetState> FetchCustomUsageAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        string usageEndpoint,
        CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync(connection, context).ConfigureAwait(false);
        var body = await SendAsync(usageEndpoint, key, cancellationToken).ConfigureAwait(false);
        // 本机计数形状（防御键名）优先：计数才是真实用量口径；认不出再原样透传
        return ParseLocalCount(body) is { } count
            ? ToLocalCountState(widget, connection, usageEndpoint, count)
            : ToCustomState(widget, connection, usageEndpoint, body);
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
            throw new ConnectionException($"MiMo API {(int)response.StatusCode}：{Truncate(body)}",
                response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                    ? ConnectionHealthState.Offline
                    : ConnectionHealthState.Degraded);
        }
        return body;
    }

    internal static WidgetState ToState(WidgetConfig widget, ConnectionConfig connection, MiMoCatalog catalog)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "MiMo";
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = MiMoWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = Severity.Info,
            Lifecycle = LifecycleState.Success,
            // 2026-10-10 用户令：模型数不是额度，不得占额度位——额度位给「无额度口」，模型数降 Summary 次行
            Summary = $"{head} · 官方无额度接口 · {catalog.Models} 模型可用",
            DetailUrl = "https://platform.xiaomimimo.com",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value_text"] = "无额度口", // 额度位显式文本（WidgetValueHint 优先取）
                ["models"] = catalog.Models.ToString("0", CultureInfo.InvariantCulture),
                ["ids"] = string.Join(",", catalog.Ids),
                ["usage_source"] = "unavailable", // 如实口径：官方无额度接口（限流头/端点均实测无），不编数
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>本机计数卡：额度位给真实口径——本机累计/窗口内调用次数 + 数据源注明（2026-10-10 用户令）。</summary>
    internal static WidgetState ToLocalCountState(WidgetConfig widget, ConnectionConfig connection, string endpoint, MiMoLocalCount count)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "MiMo";
        var window = count.WindowMinutes is { } minutes && count.WindowCalls is { } windowCalls
            ? $" · 近{minutes:0}分 {windowCalls:0} 次"
            : "";
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = MiMoWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = Severity.Success,
            Lifecycle = LifecycleState.Success,
            Summary = $"{head} · 本机累计 {count.TotalCalls:0} 次{window} · 官方无额度接口（本机计数）",
            DetailUrl = "https://platform.xiaomimimo.com",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["total_calls"] = count.TotalCalls.ToString("0", CultureInfo.InvariantCulture),
                ["window_calls"] = count.WindowCalls?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["window_minutes"] = count.WindowMinutes?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["endpoint"] = endpoint, // 数据源注明：计数来自连接配置的用量端点
                ["usage_source"] = "local_count",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>本机计数形状（防御键名，纯函数供单测）：total_calls 必有，窗口可选；data 包裹层剥开。</summary>
    internal static MiMoLocalCount? ParseLocalCount(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var envelope)
                && envelope.ValueKind == JsonValueKind.Object)
            {
                root = envelope; // {"data":{...}} 包裹层
            }
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var total = ReadLong(root, "total_calls", "calls_total", "total_count", "count");
            if (total is not { } totalCalls)
            {
                return null; // 无计数键：不是本计数形状，交回透传
            }
            return new MiMoLocalCount(
                totalCalls,
                ReadLong(root, "window_calls", "recent_calls"),
                ReadLong(root, "window_minutes", "window_min"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? ReadLong(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property))
            {
                continue;
            }
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numeric))
            {
                return numeric;
            }
            if (property.ValueKind == JsonValueKind.String
                && long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    public sealed record MiMoLocalCount(long TotalCalls, long? WindowCalls, long? WindowMinutes);

    /// <summary>自定义用量端点命中：原样透传 JSON 顶层（不加工不估价），口径标注自定义。</summary>
    internal static WidgetState ToCustomState(WidgetConfig widget, ConnectionConfig connection, string endpoint, string body)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "MiMo";
        var summaryBody = Truncate(body.ReplaceLineEndings(" "));
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = MiMoWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = Severity.Info,
            Lifecycle = LifecycleState.Success,
            Summary = $"{head} · 自定义用量端点 · {summaryBody}",
            DetailUrl = "https://platform.xiaomimimo.com",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_source"] = "custom",
                ["endpoint"] = endpoint,
                ["body"] = summaryBody,
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>模型目录解析：data[].id；无 data 数组 → 结构异常 Degraded（接口变化需人查，不静默给 0）。</summary>
    public static MiMoCatalog ParseCatalog(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new ConnectionException("MiMo /v1/models 响应不含 data 数组（接口结构可能变化）", ConnectionHealthState.Degraded);
        }
        var ids = new List<string>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                ids.Add(id.GetString()!);
            }
        }
        return new MiMoCatalog(ids.Count, ids);
    }

    public sealed record MiMoCatalog(int Models, IReadOnlyList<string> Ids);

    private static async Task<string> RequireKeyAsync(ConnectionConfig connection, ConnectionContext context)
    {
        var credentialRef = connection.CredentialRef ?? "mimo:default";
        var key = await context.Secrets.GetAsync(credentialRef).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ConnectionException("缺少 MiMo API Key：platform.xiaomimimo.com 创建后填连接 Token 框",
                ConnectionHealthState.Degraded);
        }
        return key.Trim();
    }

    private static string Truncate(string value) => value.Length <= 120 ? value : value[..120];
}

/// <summary>mimo 连接（小米开放平台）：测试连通 = GET {endpoint}/models；Key 无效 → Degraded（显式失败不静默）。</summary>
public sealed class MiMoConnectionProvider : IConnectionProvider
{
    private readonly HttpMessageHandler? _handler;

    public MiMoConnectionProvider() { }

    public MiMoConnectionProvider(HttpMessageHandler handler) => _handler = handler;

    public string ConnectionType => "mimo";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await new MiMoUsageProvider(_handler!).FetchModelsAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
