using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>
/// herald 组件类型元数据（2026-10-11 用户拍板「先做 incidents + feed 两张卡」）：
/// Herald 是用户的统一订阅与投递中枢（Go，cuihairu/herald），Beacon 主动拉关心的消息。
/// </summary>
public static class HeraldWidgetDescriptors
{
    public const string IncidentsType = "herald.incidents";
    public const string FeedType = "herald.feed";
    public const string DeliveriesType = "herald.deliveries";

    public static WidgetTypeDescriptor Incidents { get; } = new()
    {
        Type = IncidentsType,
        DisplayName = "Herald 未决事件（告警台账）",
        PinSupported = true,
        FloatingOptIn = true, // 数值类：设置「数量悬浮窗」开关放行（2026-10-11 用户令默认开）
        SuggestedTier = RefreshTiers.Default,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Herald 事件"),
            new WidgetFieldDescriptor("status", "状态筛选", Placeholder: "open（默认）/ acked / resolved / all"),
        ],
    };

    public static WidgetTypeDescriptor Feed { get; } = new()
    {
        Type = FeedType,
        DisplayName = "Herald 订阅源（RSS）",
        PinSupported = true,
        FloatingOptIn = true, // 未读数也是数值
        SuggestedTier = RefreshTiers.Default,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Herald 订阅"),
            new WidgetFieldDescriptor("feed_category", "订阅类目（公开 feed）", Placeholder: "dev"),
            new WidgetFieldDescriptor("feed_token_ref", "私密 feed 令牌凭据引用（与类目二选一，类目优先）", Placeholder: "herald:feed"),
        ],
    };

    public static WidgetTypeDescriptor Deliveries { get; } = new()
    {
        Type = DeliveriesType,
        DisplayName = "Herald 投递失败（送信台账）",
        PinSupported = true,
        FloatingOptIn = true, // 失败计数也是数值
        SuggestedTier = RefreshTiers.Default,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Herald 投递"),
            new WidgetFieldDescriptor("provider", "提供方筛选（空 = 全部）", Placeholder: "feishu / webhook / smtp"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Incidents, Feed, Deliveries];
}

/// <summary>一条事件（台账行裁剪：id/title/level/opened_at，字段见 herald core/incident）。</summary>
public sealed record HeraldIncident(string Id, string Title, string Level, DateTimeOffset? OpenedAt);

/// <summary>一条订阅条目（RSS item 裁剪；Herald 两口都按最新在前返回）。</summary>
public sealed record HeraldFeedItem(string Id, string Title, string? Description, DateTimeOffset? PublishedAt);

/// <summary>一条投递日志（TaskLog 裁剪；重试耗尽的 dead 在日志层也写 failed——都要人管，不区分）。</summary>
public sealed record HeraldDeliveryLog(string Id, string Provider, string Error, DateTimeOffset? CreatedAt);

/// <summary>
/// herald 采样：连接 Endpoint=Herald 基址；Herald 开启 auth.enabled 时凭据=API Key 走 X-API-Key 头
/// （GET 也可 ?api_key=，此处用头），未开 auth 匿名可用。/api/v1/status 与 /feeds/** 恒公开。
/// </summary>
internal static class HeraldFetch
{
    internal static async Task<string> GetAsync(
        string url,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken,
        HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // 凭据可空（Herald 默认匿名）；填了按 API Key 走 X-API-Key（与 JWT 混用时 Herald 先比 API Key）
        if (!string.IsNullOrWhiteSpace(connection.CredentialRef))
        {
            var secret = (await context.Secrets.GetAsync(connection.CredentialRef).ConfigureAwait(false))?.Trim();
            if (!string.IsNullOrEmpty(secret))
            {
                request.Headers.TryAddWithoutValidation("X-API-Key", secret);
            }
        }
        using var response = await (client ?? HttpEndpoint.Shared).SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            throw status switch
            {
                401 or 403 => new ConnectionException($"Herald 认证失败 ({status})：实例开启 auth 时需录 API Key（config.yaml auth.api_keys）。",
                    ConnectionHealthState.Unauthorized),
                404 => new ConnectionException("Herald 端点不存在 (404)：检查 Endpoint 基址与类目/令牌。", ConnectionHealthState.Offline),
                _ => new ConnectionException($"Herald 接口异常 ({status})。", ConnectionHealthState.Offline),
            };
        }
        return body;
    }

    internal static string Clip(string? text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var oneLine = text.ReplaceLineEndings(" ").Trim();
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "…";
    }
}

/// <summary>
/// herald.incidents：事件台账 `/api/v1/incidents?status=open|acked|resolved|all`（响应 {code,data:{incidents[],count}}）。
/// Signal 不是 Dashboard：默认 open（未决要人认领）>0 亮黄、其中 level=critical 亮红；acked/resolved 是回看，恒 Info。
/// 最新一条（id/title/level）进 payload 走 L3，32 DIP 只放计数。
/// </summary>
public sealed class HeraldIncidentsProvider : IWidgetProvider
{
    /// <summary>状态筛选口径：open=未决（默认）/ acked=已认领 / resolved=已解决 / all=全部。</summary>
    private static readonly Dictionary<string, string> StatusWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = "未决",
        ["acked"] = "已认领",
        ["resolved"] = "已解决",
        ["all"] = "全部",
    };

    private readonly HttpClient? _client;

    public HeraldIncidentsProvider() { }

    public HeraldIncidentsProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => HeraldWidgetDescriptors.Incidents;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var status = widget.Config.GetValueOrDefault("status")?.Trim();
        if (string.IsNullOrEmpty(status))
        {
            status = "open";
        }
        if (!StatusWords.TryGetValue(status, out var statusWord))
        {
            throw new ConnectionException($"状态筛选只认 open/acked/resolved/all，填了「{status}」。", ConnectionHealthState.Degraded);
        }

        var baseUri = RequireEndpoint(connection);
        var json = await HeraldFetch.GetAsync($"{baseUri}/api/v1/incidents?status={Uri.EscapeDataString(status.ToLowerInvariant())}&limit=200",
            connection, context, cancellationToken, _client).ConfigureAwait(false);
        var incidents = ParseIncidents(json);

        var critical = incidents.Count(incident => incident.Level.Equals("critical", StringComparison.OrdinalIgnoreCase));
        var severity = incidents.Count == 0 || !status.Equals("open", StringComparison.OrdinalIgnoreCase) ? Severity.Info // 回看口径（acked/resolved）不告警
            : critical > 0 ? Severity.Error
            : Severity.Warning;
        var head = widget.Config.GetValueOrDefault("label") is { Length: > 0 } label ? label : "Herald 事件";

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running : LifecycleState.Success,
            Progress = null,
            Summary = incidents.Count == 0 ? $"{head} · 无{statusWord}" : $"{head} · {statusWord} {incidents.Count}",
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["count"] = incidents.Count.ToString(CultureInfo.InvariantCulture),
                ["critical"] = critical.ToString(CultureInfo.InvariantCulture),
                ["status"] = status.ToLowerInvariant(),
                ["latest_id"] = incidents.Count > 0 ? incidents[0].Id : "",
                ["latest_title"] = incidents.Count > 0 ? HeraldFetch.Clip(incidents[0].Title, 120) : "",
                ["latest_level"] = incidents.Count > 0 ? incidents[0].Level : "",
                ["latest_opened_at"] = incidents.Count > 0 ? incidents[0].OpenedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "" : "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    internal static string RequireEndpoint(ConnectionConfig connection)
    {
        var baseUri = connection.Endpoint?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUri))
        {
            throw new ConnectionException("Herald 连接缺少 Endpoint（基址，如 http://127.0.0.1:8080）。", ConnectionHealthState.Degraded);
        }
        return baseUri;
    }

    /// <summary>解析事件台账 JSON（{code,data:{incidents[],count}}）；opened_at RFC3339，解析不动不强求。</summary>
    internal static List<HeraldIncident> ParseIncidents(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("incidents", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            throw new ConnectionException("Herald 事件响应缺 data.incidents（版本不符或反代 HTML 壳）。", ConnectionHealthState.Degraded);
        }

        var incidents = new List<HeraldIncident>();
        foreach (var element in list.EnumerateArray())
        {
            var id = element.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
            var title = element.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
            var level = element.TryGetProperty("level", out var levelElement) ? levelElement.GetString() ?? "" : "";
            DateTimeOffset? openedAt = null;
            if (element.TryGetProperty("opened_at", out var openedElement)
                && openedElement.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(openedElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                openedAt = parsed;
            }
            incidents.Add(new HeraldIncident(id, title ?? id, level, openedAt));
        }
        return incidents;
    }
}

/// <summary>
/// herald.feed：订阅内容（2026-10-11 用户拍板「主动拉取一些关心的消息」）。公开类目走 `/feeds/{category}.xml`
/// （只发公开内容）；私密 feed 走 `/feeds/private/{token}.xml`——token 即凭据，从 SecretStore 取
/// （feed_token_ref，默认 herald:feed），不落 widgets.json 明文。RSS 按最新在前；未读=自上一次刷新起
/// 新出现的 guid（按 widget 记基线，首刷只记基线不出未读）。
/// </summary>
public sealed class HeraldFeedProvider : IWidgetProvider
{
    /// <summary>Herald 侧 feed 地址段白名单（feedSlug：`^[a-zA-Z0-9._-]{1,64}$`），防路径注入也防把 token 拼坏。</summary>
    internal static bool IsFeedSlug(string value)
    {
        return value.Length is >= 1 and <= 64 && value.All(character
            => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }

    private readonly HttpClient? _client;
    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<string>> _seen = new(StringComparer.Ordinal); // widget.Id → 已见 guid

    public HeraldFeedProvider() { }

    public HeraldFeedProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => HeraldWidgetDescriptors.Feed;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var category = widget.Config.GetValueOrDefault("feed_category")?.Trim();
        string path;
        string head;
        if (!string.IsNullOrEmpty(category))
        {
            if (!IsFeedSlug(category))
            {
                throw new ConnectionException($"feed_category 只允许字母数字._-（1-64 位），填了「{category}」。", ConnectionHealthState.Degraded);
            }
            path = $"/feeds/{category}.xml";
            head = widget.Config.GetValueOrDefault("label") is { Length: > 0 } label ? label : category;
        }
        else
        {
            var tokenRef = widget.Config.GetValueOrDefault("feed_token_ref");
            tokenRef = string.IsNullOrWhiteSpace(tokenRef) ? "herald:feed" : tokenRef.Trim();
            var token = (await context.Secrets.GetAsync(tokenRef).ConfigureAwait(false))?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                throw new ConnectionException($"私密 feed 令牌未录（凭据引用 {tokenRef}；Herald 设置 → RSS surface 取 token）。", ConnectionHealthState.Degraded);
            }
            if (!IsFeedSlug(token))
            {
                throw new ConnectionException("私密 feed 令牌不是合法 feed 地址段（应复制 Herald 签发的 token 本体）。", ConnectionHealthState.Degraded);
            }
            path = $"/feeds/private/{token}.xml";
            head = widget.Config.GetValueOrDefault("label") is { Length: > 0 } privateLabel ? privateLabel : "Herald 订阅";
        }

        var baseUri = HeraldIncidentsProvider.RequireEndpoint(connection);
        var xml = await HeraldFetch.GetAsync($"{baseUri}{path}", connection, context, cancellationToken, _client).ConfigureAwait(false);
        var items = ParseRss(xml);

        var unread = 0;
        lock (_gate)
        {
            var seen = _seen.TryGetValue(widget.Id, out var existing)
                ? existing
                : new HashSet<string>(StringComparer.Ordinal);
            var hasBaseline = _seen.ContainsKey(widget.Id);
            _seen[widget.Id] = seen;
            foreach (var item in items)
            {
                if (!hasBaseline)
                {
                    seen.Add(item.Id); // 首刷：整单入基线，不当未读（装完第一次刷就炸一片不叫通知）
                }
                else if (seen.Add(item.Id))
                {
                    unread++; // 基线已建：新 guid 才算未读
                }
            }
        }

        var latest = items.Count > 0 ? items[0] : null;
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = Severity.Info,
            Lifecycle = LifecycleState.Success,
            Progress = null,
            Summary = latest is null
                ? $"{head} · 空源"
                : unread > 0 ? $"{head} · {unread} 条新 · {HeraldFetch.Clip(latest.Title, 24)}"
                : $"{head} · {items.Count} 条 · {HeraldFetch.Clip(latest.Title, 24)}",
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["unread"] = unread.ToString(CultureInfo.InvariantCulture),
                ["count"] = items.Count.ToString(CultureInfo.InvariantCulture),
                ["latest_title"] = latest is null ? "" : HeraldFetch.Clip(latest.Title, 120),
                ["latest_description"] = latest is null ? "" : HeraldFetch.Clip(latest.Description, 200),
                ["latest_published_at"] = latest?.PublishedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>解析 RSS 2.0（无命名空间，Go xml.Marshal 直出）：channel/item 取 guid/title/description/pubDate，保持最新在前。</summary>
    internal static List<HeraldFeedItem> ParseRss(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new ConnectionException($"Herald 订阅响应不是 RSS：{exception.Message}", ConnectionHealthState.Degraded);
        }

        var items = new List<HeraldFeedItem>();
        var channel = document.Root?.Element("channel");
        if (channel is null)
        {
            throw new ConnectionException("Herald 订阅响应缺 channel（版本不符或反代 HTML 壳）。", ConnectionHealthState.Degraded);
        }
        foreach (var element in channel.Elements("item"))
        {
            var guid = element.Element("guid")?.Value;
            var title = element.Element("title")?.Value ?? "";
            if (string.IsNullOrEmpty(guid))
            {
                guid = title; // 无 guid 以标题作身份（Herald 恒写 guid，此处防御）
            }
            DateTimeOffset? publishedAt = ParseRfc1123Z(element.Element("pubDate")?.Value);
            items.Add(new HeraldFeedItem(guid, title, element.Element("description")?.Value, publishedAt));
        }
        return items;
    }

    /// <summary>
    /// 解析 Go time.RFC1123Z（`Tue, 11 Oct 2026 07:25:02 +0000`，时区无冒号）。
    /// 实证（2026-10-11 /tmp/probe）：带星期名的解析在 globalization-invariant 模式（本机 CI 容器无 ICU）
    /// 全挂——连 "GMT" + TryParseExact 都 False；剥掉「Tue, 」前缀后 TryParse 四种时区写法
    /// （+0000/GMT/-0800/+00:00）全通。解析不动返回 null（不因时间格式抖动甩错）。
    /// </summary>
    internal static DateTimeOffset? ParseRfc1123Z(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var body = text.Trim();
        var comma = body.IndexOf(", ", StringComparison.Ordinal);
        if (comma is > 0 and <= 4) // 星期名 ≤ 4 字符后跟 ", "（Go 恒 3 字符，宽一点防别的源）
        {
            body = body[(comma + 2)..];
        }
        return DateTimeOffset.TryParse(body, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }
}

/// <summary>
/// herald.deliveries：投递失败台账 `/api/v1/logs?status=failed&limit=200[&provider=X]`（响应 {code,data:{total,logs[]}}）。
/// Signal 同构 incidents：failed=该送达的没送达=要人管（补投/查提供方），挂着直到处理——不做增量基线
/// （跨重启状态一致，基线重启即失忆）。日志态词表 pending/success/failed/shadow 四态写死（core/types.go），
/// 重试耗尽的 dead 在日志层也写 failed（runtime/manager.go settle）——都要人管，不区分；shadow 是 dry-run
/// 观察（"never represent delivery"），被 status=failed 过滤天然排除。pending 是排队/投递中瞬态，不报。
/// 计数取响应 total（全窗口过滤总数，logs 数组截断在 limit）；最新一条进 payload 走 L3。
/// </summary>
public sealed class HeraldDeliveriesProvider : IWidgetProvider
{
    private readonly HttpClient? _client;

    public HeraldDeliveriesProvider() { }

    public HeraldDeliveriesProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => HeraldWidgetDescriptors.Deliveries;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var provider = widget.Config.GetValueOrDefault("provider")?.Trim();
        var baseUri = HeraldIncidentsProvider.RequireEndpoint(connection);
        var url = $"{baseUri}/api/v1/logs?status=failed&limit=200";
        if (!string.IsNullOrEmpty(provider))
        {
            url += $"&provider={Uri.EscapeDataString(provider)}";
        }
        var json = await HeraldFetch.GetAsync(url, connection, context, cancellationToken, _client).ConfigureAwait(false);
        var (total, logs) = ParseDeliveries(json);

        var severity = total > 0 ? Severity.Warning : Severity.Info;
        var head = widget.Config.GetValueOrDefault("label") is { Length: > 0 } label ? label : "Herald 投递";
        var latest = logs.Count > 0 ? logs[0] : null;

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Warning ? LifecycleState.Running : LifecycleState.Success,
            Progress = null,
            Summary = total == 0 ? $"{head} · 无失败" : $"{head} · 失败 {total} · {latest!.Provider}",
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["failed"] = total.ToString(CultureInfo.InvariantCulture),
                ["latest_id"] = latest?.Id ?? "",
                ["latest_provider"] = latest?.Provider ?? "",
                ["latest_error"] = latest is null ? "" : HeraldFetch.Clip(latest.Error, 200),
                ["latest_created_at"] = latest?.CreatedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>解析投递日志 JSON（{code,data:{total,logs[]}}）；created_at RFC3339（可带纳秒小数），解析不动不强求。</summary>
    internal static (int Total, List<HeraldDeliveryLog> Logs) ParseDeliveries(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("logs", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            throw new ConnectionException("Herald 投递响应缺 data.logs（版本不符或反代 HTML 壳）。", ConnectionHealthState.Degraded);
        }

        var total = data.TryGetProperty("total", out var totalElement) && totalElement.ValueKind == JsonValueKind.Number
            ? totalElement.GetInt32()
            : 0;
        var logs = new List<HeraldDeliveryLog>();
        foreach (var element in list.EnumerateArray())
        {
            var id = element.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
            var provider = element.TryGetProperty("provider", out var providerElement) ? providerElement.GetString() ?? "" : "";
            var error = element.TryGetProperty("error", out var errorElement) ? errorElement.GetString() ?? "" : "";
            DateTimeOffset? createdAt = null;
            if (element.TryGetProperty("created_at", out var createdElement)
                && createdElement.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(createdElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                createdAt = parsed;
            }
            logs.Add(new HeraldDeliveryLog(id, provider, error, createdAt));
        }
        return (total, logs);
    }
}

/// <summary>herald 连接：Endpoint=基址，凭据可空（匿名）或 API Key（auth.enabled 开启时）。测试打 /api/v1/status（恒公开）。</summary>
public sealed class HeraldConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client;

    public HeraldConnectionProvider() { }

    public HeraldConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "herald";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var baseUri = connection.Endpoint?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUri))
            {
                return new ConnectionTestResult(ConnectionHealthState.Degraded, "缺少 Endpoint（Herald 基址，如 http://127.0.0.1:8080）");
            }
            var body = await HeraldFetch.GetAsync($"{baseUri}/api/v1/status", connection, context, cancellationToken, _client).ConfigureAwait(false);
            // 200 且是 {code,data...} JSON 才算连通（反代 HTML 壳不冒充连通，Jenkins 同口径）
            return body.TrimStart().StartsWith("{") ? ConnectionTestResult.Ok()
                : new ConnectionTestResult(ConnectionHealthState.Degraded, "响应不是 Herald JSON：检查 Endpoint 是否指向实例根地址");
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
