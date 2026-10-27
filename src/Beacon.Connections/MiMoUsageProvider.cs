using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;

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
        DataKinds = DisplayTemplateKinds.ProgressOnly, // 类型级能力：产百分比（评审 P1 语义描述）
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认开，2026-10-11 用户令）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "MiMo"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// mimo.usage（positioning P0 #5）：小米 MiMo 开放平台用量卡。四档如实降级：
/// ① 控制台套餐用量 API（2026-10-10 批6 实证端点、2026-10-11 用户实抓响应真身：GET
/// platform.xiaomimimo.com/api/v1/tokenPlan/usage，认证=浏览器登录 cookie api-platform_ph——
/// **API Key 双形态（Bearer/cookie/裸头）实测全 401**，key 只授权模型调用，与千问 Token Plan 同构；
/// 连接里录控制台 Cookie（SecretStore，ref 默认 mimo:console）后生效，组件给 已用/总量/百分比
/// （plan-manage 页口径，用户令替代模型数占额度位）。真实响应 data.monthUsage/usage 块 + items[]，
/// percent 字段是 **0-1 比例**（非 0-100），used/limit 自算精确百分比）；
/// ② LiteLLM 网关记账（2026-10-11 修复单）：Settings litellm_spend_url（默认空=不外发）→ GET /spend/logs
/// （Bearer=litellm_key_ref 凭据，可空裸调）折算「本月已用 $X」（网关无额度分母只显已用量；网关无 DB 按明示 Degraded）；
/// ③ 本机计数：连接 Settings 填 usage_endpoint（GET + Bearer）指向计数源，防御解析
/// total_calls/window_calls/window_minutes（{"data":{...}} 包裹剥开）→「本机累计 N 次 · 近M分 K 次」；
/// 非计数但含百分比形状（used/total 等）→ 百分比卡；认不出 → 显式 Degraded（2026-10-13 用户令：
/// 远端原文不再透传上卡面——「最近升级」事件，透传把端点返回的 status/plan 文本印上了卡）；
/// ④ 皆无 → 额度位「无额度口」+ 摘要给控制台 plan-manage 指引
/// （需登录）+ 模型数降次行——模型数是目录不是额度，不得冒充（用户令）。
/// 推理域 7 个候选端点 404、无限流头（2026-10-08 首测 + 2026-10-10 复核 + 批6 真 Key 三验）；
/// 旧防御键名解析保留兜底，认不出显式 Degraded。
/// </summary>
public sealed class MiMoUsageProvider : IWidgetProvider
{
    public const string DefaultEndpoint = "https://token-plan-cn.xiaomimimo.com/v1";

    /// <summary>控制台套餐用量 API（plan-manage 页背后的接口，2026-10-10 前端 bundle 实证）。</summary>
    public const string DefaultConsoleUsageUrl = "https://platform.xiaomimimo.com/api/v1/tokenPlan/usage";

    /// <summary>LiteLLM 网关 key 在 SecretStore 的默认 credentialRef（连接 Settings litellm_key_ref 可覆盖；空 key=裸调）。</summary>
    public const string DefaultLitellmCredentialRef = "mimo:litellm";

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
        var cookie = NormalizeConsoleCookie((await context.Secrets.GetAsync(consoleRef).ConfigureAwait(false))?.Trim());
        if (!string.IsNullOrEmpty(cookie))
        {
            return await FetchConsoleUsageAsync(widget, connection, cookie, cancellationToken).ConfigureAwait(false);
        }
        // 档②：LiteLLM 网关记账（2026-10-11 修复单）。Settings litellm_spend_url 非空才走（默认空=不外发；
        // 本机 127.0.0.1 记账不算外发）；Bearer 从 litellm_key_ref（默认 mimo:litellm）取，空=裸调。
        var litellmUrl = connection.Settings.TryGetValue("litellm_spend_url", out var spendUrl) && spendUrl.Length > 0
            ? spendUrl
            : null;
        if (litellmUrl is not null)
        {
            return await FetchLitellmSpendAsync(widget, connection, context, litellmUrl, cancellationToken).ConfigureAwait(false);
        }
        // 档③：本机计数源
        var usageEndpoint = connection.Settings.TryGetValue("usage_endpoint", out var custom) && custom.Length > 0
            ? custom
            : null;
        if (usageEndpoint is not null)
        {
            return await FetchCustomUsageAsync(widget, connection, context, usageEndpoint, cancellationToken).ConfigureAwait(false);
        }
        // 档④：模型目录 + 控制台指引（如实口径）
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

    /// <summary>
    /// 归一化控制台登录 cookie（2026-10-11 用户问「复制多了也能正确处理吧」→ 补容错）：
    /// 从 DevTools 复制时经常把整行 Cookie（`k1=v1; api-platform_ph=v2; …`）一起粘进来，整行塞进
    /// api-platform_ph 头等于把别的 cookie 喂给服务端（登录态必挂）。含分号按整行解析只取
    /// api-platform_ph 的值；不含分号按裸值用（引号剥掉）；键不存在原样返回，由服务端 401 走既有指引。
    /// </summary>
    internal static string NormalizeConsoleCookie(string? raw)
    {
        var trimmed = raw?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return trimmed;
        }
        if (!trimmed.Contains(';'))
        {
            return TrimQuotes(trimmed);
        }
        foreach (var pair in trimmed.Split(';'))
        {
            var equals = pair.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }
            if (string.Equals(pair[..equals].Trim(), ConsoleCookieHeader, StringComparison.Ordinal))
            {
                return TrimQuotes(pair[(equals + 1)..].Trim());
            }
        }
        return trimmed;
    }

    private static string TrimQuotes(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

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

    /// <summary>
    /// 档② LiteLLM 网关记账（2026-10-11 修复单）：GET litellm_spend_url（/spend/logs 语义）带本月起止
    /// （start_date/end_date + summarize=true，参数名按网关 openapi 实证）折算「本月已用」；额度分母网关
    /// 不提供——只显已用量（如实，不造）。key 走 SecretStore（litellm_key_ref），空=裸调（本机记账常免鉴权）。
    /// 实证（2026-10-11 用户网关）：无 DB 时全部 spend 面回 400 no_db_connection——按 Degraded 明示，不冒充 0。
    /// </summary>
    private async Task<WidgetState> FetchLitellmSpendAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        string spendUrl,
        CancellationToken cancellationToken)
    {
        var keyRef = connection.Settings.TryGetValue("litellm_key_ref", out var customRef) && customRef.Length > 0
            ? customRef
            : DefaultLitellmCredentialRef;
        var key = (await context.Secrets.GetAsync(keyRef).ConfigureAwait(false))?.Trim();
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var separator = spendUrl.Contains('?') ? '&' : '?';
        var url = $"{spendUrl}{separator}start_date={Uri.EscapeDataString(FormatIsoUtc(monthStart))}" +
            $"&end_date={Uri.EscapeDataString(FormatIsoUtc(now))}&summarize=true";

        var body = await SendLitellmAsync(url, key, cancellationToken).ConfigureAwait(false);
        var spend = ParseLitellmSpend(body)
            ?? throw new ConnectionException($"MiMo 网关记账响应不含 spend 值（LiteLLM 版本形状需核对）：{Truncate(body.ReplaceLineEndings(" "))}",
                ConnectionHealthState.Degraded);
        return ToLitellmState(widget, connection, spend, url);
    }

    private async Task<string> SendLitellmAsync(string url, string? key, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(key))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        }
        var client = _client ?? HttpEndpoint.Shared;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // 网关无 DB 是配置缺口不是参数问题（2026-10-11 实证 no_db_connection）：明示给网关侧修
            if (body.Contains("no_db_connection", StringComparison.OrdinalIgnoreCase)
                || body.Contains("No connected db", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConnectionException("MiMo 网关记账不可用：LiteLLM 未接数据库（spend 面需网关 config 接 DB）。",
                    ConnectionHealthState.Degraded);
            }
            var hint = response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                ? "（网关 Key 无效或未授权：核对 litellm_key_ref 指向的 SecretStore 凭据）"
                : "";
            throw new ConnectionException($"MiMo 网关记账 {(int)response.StatusCode}：{Truncate(body)}{hint}",
                response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                    ? ConnectionHealthState.Offline
                    : ConnectionHealthState.Degraded);
        }
        return body;
    }

    /// <summary>解析 spend 流水：根数组 / {"response":[...]} / {"results":[...]} 三形兼容，累计每条数值 spend；认不出（含非 JSON）返回 null。</summary>
    internal static decimal? ParseLitellmSpend(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            JsonElement? array = root.ValueKind == JsonValueKind.Array ? root : null;
            if (array is null && root.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "response", "results" })
                {
                    if (root.TryGetProperty(key, out var nested) && nested.ValueKind == JsonValueKind.Array)
                    {
                        array = nested;
                        break;
                    }
                }
                // 汇总形（summarize）：对象直接带数值 spend
                if (array is null
                    && root.TryGetProperty("spend", out var direct)
                    && direct.ValueKind == JsonValueKind.Number
                    && direct.TryGetDecimal(out var directSpend))
                {
                    return directSpend;
                }
            }
            if (array is not { } items)
            {
                return null;
            }
            decimal sum = 0;
            var found = false;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("spend", out var spendElement)
                    && spendElement.ValueKind == JsonValueKind.Number
                    && spendElement.TryGetDecimal(out var spend))
                {
                    sum += spend;
                    found = true;
                }
            }
            return found ? sum : null;
        }
        catch (JsonException)
        {
            return null; // 非 JSON：交由上层显式 Degraded（带原始响应），不裸抛
        }
    }

    private WidgetState ToLitellmState(WidgetConfig widget, ConnectionConfig connection, decimal spend, string url)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "MiMo";
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = MiMoWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = Severity.Success,
            Lifecycle = LifecycleState.Success,
            // 网关无额度分母：只显已用量（如实，不造）
            Summary = $"{head} · 本月已用 ${spend.ToString("0.00", CultureInfo.InvariantCulture)}（网关记账）",
            DetailUrl = null,
            Progress = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value_text"] = $"${spend.ToString("0.00", CultureInfo.InvariantCulture)}",
                ["spend_usd"] = spend.ToString("0.00", CultureInfo.InvariantCulture),
                ["spend_window"] = "month",
                ["endpoint"] = url,
                ["usage_source"] = "litellm",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    internal static string FormatIsoUtc(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private async Task<string> SendConsoleAsync(string url, string cookie, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(ConsoleCookieHeader, cookie);
        // 控制台接口校验来源页（2026-10-11 实抓请求带 plan-manage referer）
        request.Headers.TryAddWithoutValidation("Referer", "https://platform.xiaomimimo.com/console/plan-manage");
        var client = _client ?? HttpEndpoint.Shared;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // 401=登录态失效（会话 cookie 过期是常态，2026-10-11 用户实测裸 401+loginUrl）：明示+重录指引，不笼统报错
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new ConnectionException(
                    $"MiMo 控制台 {(int)response.StatusCode}：控制台 Cookie 已失效或未录制——浏览器登录 platform.xiaomimimo.com → F12 → 应用 → Cookies → 复制 api-platform_ph → 设置重录。{Truncate(body)}",
                    ConnectionHealthState.Degraded);
            }
            throw new ConnectionException($"MiMo 控制台 {(int)response.StatusCode}：{Truncate(body)}",
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
        // 本机计数形状（防御键名）优先：计数才是真实用量口径
        if (ParseLocalCount(body) is { } count)
        {
            return ToLocalCountState(widget, connection, usageEndpoint, count);
        }
        // 百分比形状（console 防御键名族，used/total 自算优先）：有可用用量数字就出数
        if (ParseConsoleUsage(body) is { } usage)
        {
            return ToCustomPercentState(widget, connection, usageEndpoint, usage);
        }
        // 认不出（2026-10-13 用户令：远端原文不透传上卡面——「最近升级」事件）：显式 Degraded 写明
        // 原因，原始响应只留在错误信息里作诊断，不静默不编数。
        throw new ConnectionException(
            $"MiMo 自定义用量端点无可用用量字段（计数/百分比形状均未命中，核对端点返回形状）：{Truncate(body.ReplaceLineEndings(" "))}",
            ConnectionHealthState.Degraded);
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

    /// <summary>套餐用量分级（console/自定义共用）：≥100 超限才 Error，≥90 快用完 Warning 提醒。</summary>
    private static Severity PlanSeverity(double percent)
        => percent >= ConsoleErrorPercent ? Severity.Error
            : percent >= ConsoleWarnPercent ? Severity.Warning
            : Severity.Success;

    /// <summary>已用/总产量化文案（used/total 齐才给，缺一则空串）。</summary>
    private static string AmountText(MiMoPlanUsage usage)
        => usage.Used is { } usedValue && usage.Total is { } totalValue
            ? $"已用 {FormatTokens(usedValue)} / {FormatTokens(totalValue)} · "
            : "";

    /// <summary>自定义用量端点百分比卡（2026-10-13 用户令替代原样透传）：有可用用量数字就出数，口径注明自定义。</summary>
    internal static WidgetState ToCustomPercentState(WidgetConfig widget, ConnectionConfig connection, string endpoint, MiMoPlanUsage usage)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "MiMo";
        var severity = PlanSeverity(usage.Percent);
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = MiMoWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed : LifecycleState.Success,
            Summary = $"{head} · {AmountText(usage)}{usage.Percent.ToString("0.#", CultureInfo.InvariantCulture)}%（自定义用量端点）",
            DetailUrl = null, // 自定义端点无控制台页可跳，不硬指 plan-manage
            Progress = Math.Clamp(usage.Percent, 0, 100) / 100.0,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value_text"] = $"{usage.Percent.ToString("0.#", CultureInfo.InvariantCulture)}%",
                ["percent"] = usage.Percent.ToString("0.#", CultureInfo.InvariantCulture),
                ["used"] = usage.Used?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["total"] = usage.Total?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["endpoint"] = endpoint,
                ["usage_source"] = "custom_percent",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>控制台套餐用量（百分比必有——缺失或 used/total 都没有 → 解析失败显式报，不编数）。</summary>
    public sealed record MiMoPlanUsage(double? Used, double? Total, double Percent);

    /// <summary>
    /// 控制台套餐用量解析（纯函数供单测）。真实形状（2026-10-11 用户实抓）：
    /// {"code":0,"message":"","data":{"monthUsage":{"percent":0.0007,"items":[{"name":"month_total_token",
    /// "used":90912620,"limit":132000000000,"percent":0.0007}]},"usage":{"items":[...]}}}——
    /// percent 字段是 **0-1 比例**（非 0-100），优先 used/limit 自算精确百分比；monthUsage 缺失退
    /// data.usage（name=plan_total_token 优先）。旧防御键名路径兜底（percentage/used/total 顶层字段，
    /// 数字一律兼容字符串形态），认不出返回 null（显式 Degraded）。
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
                root = envelope; // {"code":0,"message":"","data":{...}} 控制台统一包裹
            }
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            // 真实形状：monthUsage（月度套餐主口径）→ usage（plan_total_token）兜底
            var plan = ParseUsageBlock(root, "monthUsage", "month_total_token")
                ?? ParseUsageBlock(root, "usage", "plan_total_token");
            if (plan is not null)
            {
                return plan;
            }
            // 旧防御键名兜底：百分比 percentage/percent/usage_percent/used_percent；
            // 已用 used_tokens/usedTokens/used/usage/currentValue；总量 total_tokens/totalTokens/total/limit/quota。
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

    /// <summary>真实形状块解析：{percent(0-1 比例), items:[{name,used,limit,percent}]} → 0-100 百分比。</summary>
    private static MiMoPlanUsage? ParseUsageBlock(JsonElement root, string blockKey, string primaryItemName)
    {
        if (!root.TryGetProperty(blockKey, out var block) || block.ValueKind != JsonValueKind.Object
            || !block.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        JsonElement? chosen = null;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            chosen ??= item; // 兜底：首个条目
            if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                && name.GetString() == primaryItemName)
            {
                chosen = item;
                break;
            }
        }
        if (chosen is not { } entry)
        {
            return null;
        }
        var used = ReadDouble(entry, "used");
        var total = ReadDouble(entry, "limit");
        double? percent = used is { } usedValue && total is { } totalValue && totalValue > 0
            ? usedValue * 100 / totalValue // 自算精确值（块级 percent 是舍入过的 0-1 比例）
            : ReadDouble(entry, "percent") is { } ratio ? ratio * 100
            : ReadDouble(block, "percent") is { } blockRatio ? blockRatio * 100
            : null;
        return percent is { } finalPercent ? new MiMoPlanUsage(used, total, finalPercent) : null;
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
        var severity = PlanSeverity(usage.Percent); // 95% 量级是「快用完」不是「已坏」
        var amount = AmountText(usage);
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
