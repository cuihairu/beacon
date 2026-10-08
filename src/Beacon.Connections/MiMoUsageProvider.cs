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
/// mimo.usage（positioning P0 #5）：小米 MiMo 开放平台用量卡——**三档探测后如实降级**（2026-10-08 实测）：
/// ①官方用量 API：无（推理域 token-plan-cn.xiaomimimo.com 的 /usages、/usage 均 404，/v1/models 200 有效）；
/// ②控制台口复用：platform.xiaomimimo.com 需登录态，无公开接口形状；③本地统计兜底：Beacon 不经手推理流量，
/// 无本地会话文件可计。故本卡显示**官方模型目录真数据**（/v1/models 模型数），用量口径如实标注
/// 「官方未开放」——不编造任何数字。日后官方开放用量口，可在连接 Settings 填 usage_endpoint
/// （GET + Bearer），命中即切换显示该端点的 JSON 顶层摘要（原样透传不加工）。
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
        return ToCustomState(widget, connection, usageEndpoint, body);
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
            Summary = $"{head} · {catalog.Models} 模型可用 · 用量口径官方未开放",
            DetailUrl = "https://platform.xiaomimimo.com",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["models"] = catalog.Models.ToString("0", CultureInfo.InvariantCulture),
                ["ids"] = string.Join(",", catalog.Ids),
                ["usage_source"] = "unavailable", // 如实口径：官方未开放用量接口，不编数
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

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

    public async Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await new MiMoUsageProvider(_handler!).FetchModelsAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
