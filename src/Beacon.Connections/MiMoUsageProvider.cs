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
/// mimo.usage（positioning P0 #5）：小米 MiMo 开放平台用量卡。三档如实降级：
/// ① 控制台套餐用量 API（2026-10-10 批6 实证：GET platform.xiaomimimo.com/api/v1/tokenPlan/usage，
/// 认证=浏览器登录 cookie api-platform_ph——**API Key 双形态（Bearer/cookie/裸头）实测全 401**，
/// key 只授权模型调用，与千问 Token Plan 同构；连接里录控制台 Cookie（SecretStore，ref 默认
/// mimo:console）后生效，组件给 已用/总量/百分比（plan-manage 页口径，用户令替代模型数占额度位）；
/// ② 本机计数：连接 Settings 填 usage_endpoint（GET + Bearer）指向计数源，防御解析
/// total_calls/window_calls/window_minutes（{"data":{...}} 包裹剥开）→「本机累计 N 次 · 近M分 K 次」，
/// 无计数键则原样透传（不加工）；③ 两者皆无 → 额度位「无额度口」+ 摘要给控制台 plan-manage 指引
/// （需登录）+ 模型数降次行——模型数是目录不是额度，不得冒充（用户令）。
/// 推理域 7 个候选端点 404、无限流头（2026-10-08 首测 + 2026-10-10 复核 + 批6 真 Key 三验）。
/// 控制台响应字段名未拿真会话验证（无 cookie 无法取真数据）——解析走防御键名，认不出显式 Degraded。
/// </summary>
public sealed class MiMoUsageProvider : IWidgetProvider
{
    public const string DefaultEndpoint = "https://token-plan-cn.xiaomimimo.com/v1";

    /// <summary>控制台套餐用量 API（plan-manage 页背后的接口，2026-10-10 前端 bundle 实证）。</summary>
    public const string DefaultConsoleUsageUrl = "https://platform.xiaomimimo.com/api/v1/tokenPlan/usage";

    /// <summary>控制台登录 cookie 名（前端 bundle 29618 模块实证）；请求头与 cookie 同名。</summary>
    public const string ConsoleCookieHeader = "api-platform_ph";

    /// <summary>控制台 Cookie 在 SecretStore 的默认 credentialRef（连接 Settings console_credential_ref 可覆盖）。</summary>
    public const string DefaultConsoleCredentialRef = "mimo:console";

    private const double ConsoleWarnPercent = 90;
    private const double ConsoleErrorPercent = 100;

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
        // 档①：控制台 Cookie 已录 → plan-manage 口径的真实套餐用量（用户令：已用/总量/百分比）
        var consoleRef = connection.Settings.TryGetValue("console_credential_ref", out var customRef) && customRef.Length > 0
            ? customRef
            : DefaultConsoleCredentialRef;
        var cookie = (await context.Secrets.GetAsync(consoleRef).ConfigureAwait(false))?.Trim();
        if (!string.IsNullOrEmpty(cookie))
        {
            return await FetchConsoleUsageAsync(widget, connection, cookie, cancellationToken).ConfigureAwait(false);
        }
        // 档②：本机计数源
        var usageEndpoint = connection.Settings.TryGetValue("usage_endpoint", out var custom) && custom.Length > 0
            ? custom
            : null;
        if (usageEndpoint is not null)
        {
            return await FetchCustomUsageAsync(widget, connection, context, usageEndpoint, cancellationToken).ConfigureAwait(false);
        }
        // 档③：模型目录 + 控制台指引（如实口径）
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

    /// <summary>档① 控制台套餐用量：GET console_usage_url（默认 plan-manage 页接口），api-platform_ph 头携带登录 cookie。</summary>
    private async Task<WidgetState> FetchConsoleUsageAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        string cookie,
        CancellationToken cancellationToken)
    {
        var url = connection.Settings.TryGetValue("console_usage_url", out var custom) && custom.Length > 0
            ? custom
            : DefaultConsoleUsageUrl;
        var body = await SendConsoleAsync(url, cookie, cancellationToken).ConfigureAwait(false);
        var usage = ParseConsoleUsage(body)
            ?? throw new ConnectionException($"MiMo 控制台响应不含套餐用量字段（键名需按真会话核对）：{Truncate(body.ReplaceLineEndings(" "))}",
                ConnectionHealthState.Degraded);
        return ToConsoleState(widget, connection, url, usage);
    }

    private async Task<string> SendConsoleAsync(string url, string cookie, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(ConsoleCookieHeader, cookie);
        var client = _client ?? HttpEndpoint.Shared;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // 401=登录态失效（cookie 过期，回浏览器重取）；5xx=服务侧
            var hint = (int)response.StatusCode switch
            {
                401 or 403 => "（控制台登录态失效：重新从浏览器复制 api-platform_ph cookie）",
                _ => "",
            };
            throw new ConnectionException($"MiMo 控制台 {(int)response.StatusCode}：{Truncate(body)}{hint}",
                response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                    ? ConnectionHealthState.Offline
                    : ConnectionHealthState.Degraded);
        }
        return body;
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
            // 2026-10-10 用户令：模型数不是额度，不得占额度位——额度位「无额度口」+ 控制台套餐用量指引（批6：URL 页口径说明）
            Summary = $"{head} · 套餐用量见控制台 plan-manage（需登录）· {catalog.Models} 模型可用",
            DetailUrl = "https://platform.xiaomimimo.com/console/plan-manage",
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

    /// <summary>控制台套餐用量（百分比必有——缺失或 used/total 都没有 → 解析失败显式报，不编数）。</summary>
    public sealed record MiMoPlanUsage(double? Used, double? Total, double Percent);

    /// <summary>
    /// 控制台套餐用量解析（纯函数供单测）。真会话响应字段名未验证（无登录 cookie 拿不到真数据）——
    /// 防御键名 + data 包裹剥开：百分比 percentage/percent/usage_percent/used_percent；
    /// 已用 used_tokens/usedTokens/used/usage/currentValue；总量 total_tokens/totalTokens/total/limit/quota。
    /// 百分比缺、used+total 齐 → 自算；数字一律兼容字符串形态。
    /// </summary>
    internal static MiMoPlanUsage? ParseConsoleUsage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var envelope)
                && envelope.ValueKind == JsonValueKind.Object)
            {
                root = envelope; // {"code":200,"data":{...}} 控制台统一包裹
            }
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            var percent = ReadDouble(root, "percentage", "percent", "usage_percent", "used_percent", "usedPercent", "usagePercent");
            var used = ReadDouble(root, "used_tokens", "usedTokens", "used", "usage", "currentValue");
            var total = ReadDouble(root, "total_tokens", "totalTokens", "total", "limit", "quota");
            if (percent is null && used is { } usedValue && total is { } totalValue && totalValue > 0)
            {
                percent = Math.Round(usedValue * 100 / totalValue, 1);
            }
            return percent is { } finalPercent ? new MiMoPlanUsage(used, total, finalPercent) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double? ReadDouble(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property))
            {
                continue;
            }
            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numeric))
            {
                return numeric;
            }
            if (property.ValueKind == JsonValueKind.String
                && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    /// <summary>控制台口径卡：额度位=plan-manage 页真实套餐用量（已用/总量/百分比，用户令替代模型数）。</summary>
    internal static WidgetState ToConsoleState(WidgetConfig widget, ConnectionConfig connection, string url, MiMoPlanUsage usage)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "MiMo";
        // 95% 量级是「快用完」不是「已坏」：≥100（超限）才 Error，≥90 Warning 提醒
        var severity = usage.Percent >= ConsoleErrorPercent ? Severity.Error
            : usage.Percent >= ConsoleWarnPercent ? Severity.Warning
            : Severity.Success;
        var amount = usage.Used is { } usedValue && usage.Total is { } totalValue
            ? $"已用 {FormatTokens(usedValue)} / {FormatTokens(totalValue)} · "
            : "";
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = MiMoWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed : LifecycleState.Success,
            Summary = $"{head} · {amount}{usage.Percent.ToString("0.#", CultureInfo.InvariantCulture)}%（控制台套餐用量）",
            DetailUrl = "https://platform.xiaomimimo.com/console/plan-manage",
            Progress = Math.Clamp(usage.Percent, 0, 100) / 100.0,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value_text"] = $"{usage.Percent.ToString("0.#", CultureInfo.InvariantCulture)}%",
                ["percent"] = usage.Percent.ToString("0.#", CultureInfo.InvariantCulture),
                ["used"] = usage.Used?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["total"] = usage.Total?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["endpoint"] = url,
                ["usage_source"] = "console_api",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>大数中文口径（plan-manage 页同数量级）：≥1亿 → 「X.X亿」，否则千分位原样。</summary>
    internal static string FormatTokens(double value)
        => value >= 100_000_000
            ? $"{(value / 100_000_000).ToString("0.#", CultureInfo.InvariantCulture)}亿"
            : value.ToString("N0", CultureInfo.InvariantCulture);

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
