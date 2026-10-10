using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>ark（火山方舟）Widget 类型元数据：Coding Plan Pro 额度（positioning P0 #6）。</summary>
public static class ArkWidgetDescriptors
{
    public const string UsageType = "ark.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "方舟 Coding Plan 额度（火山）",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "方舟"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（%）", Placeholder: "默认 60"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（%）", Placeholder: "默认 90"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// ark.usage（positioning P0 #6）：火山方舟 Coding Plan Pro 额度监控。
/// 唯一额度口是控制面 OpenAPI：POST https://open.volcengineapi.com/?Action=GetCodingPlanUsage&amp;Version=2024-01-01&amp;Region=cn-beijing，
/// 空 body；**鉴权必须 AK/SK V4 签名（火山变体）——推理 ARK_API_KEY（ark-*）调管控面被服务端 400
/// InvalidAuthorization 拒绝（2026-10-08 实测），不能互推**。凭据在连接 Token 框一次粘贴
/// "AccessKey:SecretKey"（整段只进 DPAPI，config.json 仅 credentialRef）；
/// Region 可在连接 Settings 里覆盖（默认 cn-beijing）。
/// 响应 Result.QuotaUsage[]：Level=session（5h 窗）/weekly/monthly，Percent 为已用百分比（0-100，
/// 可能是字符串；控制台的请求次数/token 绝对数不经此接口），ResetTimestamp 秒级 epoch（0/-1=暂无重置）。
/// 签名规范：docs.volcengine.com/docs/6369/67269；三份社区实现交叉验证
/// （KS-OTO/tracking-llm-plan-usage、farion1231/cc-switch、nguyenphutrong/quotio）。
/// </summary>
public sealed class ArkUsageProvider : IWidgetProvider
{
    public const string DefaultEndpoint = "https://open.volcengineapi.com/";
    public const string DefaultRegion = "cn-beijing";
    private const string Action = "GetCodingPlanUsage";
    private const string Version = "2024-01-01";

    private readonly HttpClient? _client;

    public ArkUsageProvider() { }

    public ArkUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => ArkWidgetDescriptors.Usage;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var usage = await FetchUsageAsync(connection, context, cancellationToken).ConfigureAwait(false);
        return ToState(widget, connection, usage);
    }

    /// <summary>拉取并解析额度；网络/签名错误抛 ConnectionException（连接健康转 Degraded/Offline）。</summary>
    public async Task<ArkPlanUsage> FetchUsageAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var (accessKey, secretKey) = await SplitCredentialAsync(connection, context).ConfigureAwait(false);
        var region = connection.Settings.TryGetValue("region", out var customRegion) && customRegion.Length > 0
            ? customRegion
            : DefaultRegion;
        var query = $"Action={Action}&Region={region}&Version={Version}";
        // Endpoint 可覆盖（区域/代理/验收 mock）——与其余 Provider 的 HttpEndpoint 口径一致；
        // 签名 host 取实际端点（默认 open.volcengineapi.com）
        var endpoint = (connection.Endpoint?.Trim().Length > 0 ? connection.Endpoint!.Trim() : DefaultEndpoint).TrimEnd('/');
        var host = new Uri(endpoint).Host;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}?{query}");
        var authorization = BuildAuthorization(accessKey, secretKey, host, region, query, DateTimeOffset.UtcNow,
            out var xDate, out var payloadHash);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("X-Date", xDate);
        request.Headers.TryAddWithoutValidation("X-Content-Sha256", payloadHash);
        request.Content = new StringContent("", Encoding.UTF8, "application/json");

        var client = _client ?? HttpEndpoint.Shared;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            // bug 批9 2026-10-10：429 携带配额耗尽码（AccountQuotaExceeded 等，实测周额度尽即此形态）
            // = 窗口额度用尽的权威信号——按该窗已尽返回（带重置时间），连接保持健康；
            // 纯限流型 429（无配额码，如 RPM 限速）不翻转——瞬时限速≠用尽（同千问探针口径），走下方通用异常按 4xx=Degraded。
            if (ParseExhaustedResponse(body) is { } exhausted)
            {
                return exhausted;
            }
        }
        if (!response.IsSuccessStatusCode)
        {
            // 未订阅/凭证无效都在这里显式失败（不静默吞）——连接健康交给异常携带。
            // 4xx（含 400 InvalidAuthorization：凭证/签名问题，火山鉴权失败用 400 回，2026-10-08 实测）
            // 归 Degraded（配置可修），5xx/网络类才 Offline
            throw new ConnectionException($"方舟 API {(int)response.StatusCode}：{Truncate(body)}",
                response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                    ? ConnectionHealthState.Offline
                    : ConnectionHealthState.Degraded);
        }

        var usage = ParseUsage(body);
        if (usage is null)
        {
            throw new ConnectionException("方舟响应不含 QuotaUsage 数据（接口结构可能变化）", ConnectionHealthState.Degraded);
        }
        return usage;
    }

    internal static WidgetState ToState(WidgetConfig widget, ConnectionConfig connection, ArkPlanUsage usage)
    {
        var warnPercent = widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, CultureInfo.InvariantCulture, out var warn) ? warn : 60;
        var errorPercent = widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, CultureInfo.InvariantCulture, out var error) ? error : 90;

        // 无套餐/已回收：合法状态非故障——Info 卡片明示，不造假红
        if (!usage.HasPlan)
        {
            return new WidgetState
            {
                WidgetId = widget.Id,
                WidgetType = DescriptorType,
                ConnectionId = connection.Id,
                Severity = Severity.Info,
                Lifecycle = LifecycleState.Unknown,
                Summary = $"{widget.Config.GetValueOrDefault("label") ?? "方舟"} · 无套餐或已回收（控制台确认订阅状态）",
                DetailUrl = ConsoleUrl,
                Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                FetchedAt = DateTimeOffset.UtcNow,
            };
        }

        var head = widget.Config.GetValueOrDefault("label") ?? "方舟";

        // bug 批9 2026-10-10：429 耗尽但窗口名解析不出（无任何窗口用量数据）——通用「已尽」卡，
        // 绝不显示剩余可用（连剩余数字都没有，谈何剩余）
        if (usage.ExhaustedNote is { } note && usage.Session is null && usage.Weekly is null && usage.Monthly is null)
        {
            return new WidgetState
            {
                WidgetId = widget.Id,
                WidgetType = DescriptorType,
                ConnectionId = connection.Id,
                Severity = Severity.Error,
                Lifecycle = LifecycleState.Failed,
                Summary = $"{head} · 额度用尽（服务端返回 {note}，重置时间未知）",
                DetailUrl = ConsoleUrl,
                Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["exhausted"] = "1",
                    ["exhausted_note"] = note,
                },
                FetchedAt = DateTimeOffset.UtcNow,
            };
        }

        // bug 批9 2026-10-10：任一窗已用 ≥100% 即按「已尽」展示，明确标哪个窗、何时重置——
        // 周/月尽不得被新 5h 窗的低用量掩盖（用户实测：周/月尽、新 5h 窗刚重置，旧码显示健康）。
        // 多窗尽按重置时间最早取首窗命名，全部尽窗列 payload["exhausted_windows"]。
        var exhausted = ExhaustedWindows(usage);
        if (exhausted.Count > 0)
        {
            var (exhaustedName, exhaustedWindow) = exhausted[0];
            var exhaustedResetText = exhaustedWindow.ResetSeconds is { } exhaustedReset ? FormatReset(exhaustedReset) : null;
            return new WidgetState
            {
                WidgetId = widget.Id,
                WidgetType = DescriptorType,
                ConnectionId = connection.Id,
                Severity = Severity.Error, // 已尽恒错误级，不受阈值配置降级
                Lifecycle = LifecycleState.Failed,
                Progress = 1.0, // 数值卡进度条：已尽=满格
                Summary = exhaustedResetText is { } text
                    ? $"{head} · 额度用尽（{exhaustedName}，{text} 重置）"
                    : $"{head} · 额度用尽（{exhaustedName}，待重置）",
                DetailUrl = ConsoleUrl,
                Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["percent"] = Format(exhaustedWindow.Percent), // tile 主数值（已尽窗已用 %）
                    ["exhausted"] = "1",
                    ["exhausted_window"] = exhaustedName,
                    ["exhausted_windows"] = string.Join(",", exhausted.Select(entry => entry.Name)),
                    ["rolling_percent"] = usage.Session is { } sessionWindow ? Format(sessionWindow.Percent) : "",
                    ["weekly_percent"] = usage.Weekly is { } weeklyWindow ? Format(weeklyWindow.Percent) : "",
                    ["monthly_percent"] = usage.Monthly is { } monthlyWindow ? Format(monthlyWindow.Percent) : "",
                    ["reset_iso"] = exhaustedResetText ?? "",
                },
                FetchedAt = DateTimeOffset.UtcNow,
            };
        }

        // 5h session 窗口为主口径（2026-10-09 bug 批3：方舟 coding 是 5 小时窗口额度——
        // 旧码主数值取最差窗口，周/月数字冒充了当前窗口；周/月仍留 payload 供 L3 详情查）
        var primary = usage.Session ?? new ArkUsageWindow(usage.WorstPercent, null);
        var severity = MapSeverity(primary.Percent, warnPercent, errorPercent);
        var resetText = primary.ResetSeconds is { } reset ? FormatReset(reset) : "待重置";
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = DescriptorType,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success,
            // 数值卡进度条：5h 窗口用量（与级别判定同源）
            Progress = Math.Clamp(primary.Percent, 0, 100) / 100.0,
            Summary = $"{head} · 5h {Format(primary.Percent)}% · 重置 {resetText}",
            DetailUrl = ConsoleUrl,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = Format(primary.Percent), // tile 主数值（5h 窗口已用 %）
                ["rolling_percent"] = usage.Session is { } s ? Format(s.Percent) : "",
                ["weekly_percent"] = usage.Weekly is { } w ? Format(w.Percent) : "",
                ["monthly_percent"] = usage.Monthly is { } m ? Format(m.Percent) : "",
                ["reset_iso"] = primary.ResetSeconds is { } epoch ? FormatReset(epoch) : "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    private const string DescriptorType = ArkWidgetDescriptors.UsageType;
    private const string ConsoleUrl = "https://console.volcengine.com/ark";

    private static async Task<(string AccessKey, string SecretKey)> SplitCredentialAsync(ConnectionConfig connection, ConnectionContext context)
    {
        // Token 框一次粘贴 "AccessKey:SecretKey"（整段只进 DPAPI）；火山 AK/SK 是两套独立凭证，
        // 推理 key 已实测不可调管控面，缺一即显式失败（不静默空跑）
        var credentialRef = connection.CredentialRef ?? "ark:default";
        var secret = await context.Secrets.GetAsync(credentialRef).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ConnectionException("缺少火山 AK/SK：连接 Token 框填 AccessKey:SecretKey（控制台「API 访问密钥」创建）",
                ConnectionHealthState.Degraded);
        }
        var separator = secret.IndexOf(':');
        if (separator <= 0 || separator == secret.Length - 1)
        {
            throw new ConnectionException("AK/SK 格式应为 AccessKey:SecretKey（冒号分隔）", ConnectionHealthState.Degraded);
        }
        return (secret[..separator], secret[(separator + 1)..]);
    }

    /// <summary>单窗口用量（Percent 已用 %；ResetSeconds = 重置时刻秒级 epoch，null = 暂无重置）。</summary>
    public sealed record ArkUsageWindow(double Percent, long? ResetSeconds);

    /// <summary>
    /// 三窗口套餐用量；HasPlan=false = 无套餐/已回收（QuotaUsage 空且非 Running）。
    /// ExhaustedNote：429 耗尽码但窗口名解析不出时的兜底口径（bug 批9）——记服务端错误码，HasPlan 含之。
    /// </summary>
    public sealed record ArkPlanUsage(string? Status, ArkUsageWindow? Session, ArkUsageWindow? Weekly, ArkUsageWindow? Monthly, string? ExhaustedNote = null)
    {
        public bool HasPlan => Session is not null || Weekly is not null || Monthly is not null || ExhaustedNote is not null;

        public double WorstPercent
        {
            get
            {
                var worst = 0.0;
                if (Session is { } s)
                {
                    worst = Math.Max(worst, s.Percent);
                }
                if (Weekly is { } w)
                {
                    worst = Math.Max(worst, w.Percent);
                }
                if (Monthly is { } m)
                {
                    worst = Math.Max(worst, m.Percent);
                }
                return worst;
            }
        }
    }

    /// <summary>
    /// 解析 GetCodingPlanUsage 响应。防御点：Percent 数字/字符串双态、ResetTimestamp 0/-1 哨兵、
    /// 包裹层数组名 QuotaUsage（真实字段）+ Usages/Details 防御回退（社区实现三重兼容口径）。
    /// Result/数组缺失 → null（调用方转 Degraded：接口结构变化需人查）；
    /// 数组存在但空（未订阅/已回收 Status=Reclaimed 等价形态）→ HasPlan=false → 渲染「无套餐」而非错误卡。
    /// </summary>
    public static ArkPlanUsage? ParseUsage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("Result", out var result) || !TryGetQuotaArray(result, out var items))
        {
            return null;
        }
        var status = result.TryGetProperty("Status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString()
            : null;
        ArkUsageWindow? session = null, weekly = null, monthly = null;
        foreach (var item in items.EnumerateArray())
        {
            var level = item.TryGetProperty("Level", out var levelElement) ? levelElement.GetString() : null;
            if (level is null || !TryReadPercent(item, out var percent))
            {
                continue;
            }
            var reset = item.TryGetProperty("ResetTimestamp", out var resetElement) && resetElement.TryGetInt64(out var resetSeconds) && resetSeconds > 0
                ? (long?)resetSeconds
                : null;
            var window = new ArkUsageWindow(percent, reset);
            switch (level.ToLowerInvariant())
            {
                case "session":
                    session = window;
                    break;
                case "weekly":
                    weekly = window;
                    break;
                case "monthly":
                    monthly = window;
                    break;
            }
        }
        return new ArkPlanUsage(status, session, weekly, monthly);
    }

    private static bool TryGetQuotaArray(JsonElement result, out JsonElement items)
    {
        foreach (var name in new[] { "QuotaUsage", "Usages", "Details" })
        {
            if (result.TryGetProperty(name, out items) && items.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
        }
        items = default;
        return false;
    }

    private static bool TryReadPercent(JsonElement item, out double percent)
    {
        percent = 0;
        if (!item.TryGetProperty("Percent", out var percentElement))
        {
            return false;
        }
        return percentElement.ValueKind switch
        {
            JsonValueKind.Number => percentElement.TryGetDouble(out percent),
            JsonValueKind.String => double.TryParse(percentElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out percent),
            _ => false,
        };
    }

    /// <summary>
    /// 429 配额耗尽响应解析（bug 批9 2026-10-10）：body 含配额耗尽码（AccountQuotaExceeded/QuotaExceeded/
    /// AllocationQuota/insufficient_quota，忽略大小写）才判「用尽」，返回对应窗 100% 的用量（重置时间尽力解析）；
    /// 窗口名按 weekly/monthly/session 关键字（含中文 周/月/5小时），认不出 → ExhaustedNote 兜底（通用已尽卡）。
    /// 纯限流型 429（无配额码）返回 null 不翻转——瞬时限速≠用尽（同千问探针口径）。
    /// </summary>
    internal static ArkPlanUsage? ParseExhaustedResponse(string body)
    {
        if (body.Length == 0)
        {
            return null;
        }
        var lower = body.ToLowerInvariant();
        var quotaExhausted = lower.Contains("accountquotaexceeded") || lower.Contains("quotaexceeded")
            || lower.Contains("allocationquota") || lower.Contains("insufficient_quota");
        if (!quotaExhausted)
        {
            return null;
        }
        var windowName = lower.Contains("weekly") || lower.Contains("周") ? "weekly"
            : lower.Contains("monthly") || lower.Contains("月") ? "monthly"
            : lower.Contains("session") || lower.Contains("5h") || lower.Contains("5小时") ? "session"
            : null;
        var reset = TryParseResetSeconds(body);
        if (windowName is null)
        {
            return new ArkPlanUsage("AccountQuotaExceeded", null, null, null, ExhaustedNote: "AccountQuotaExceeded");
        }
        var exhaustedWindow = new ArkUsageWindow(100, reset);
        return windowName switch
        {
            "weekly" => new ArkPlanUsage("AccountQuotaExceeded", null, exhaustedWindow, null),
            "monthly" => new ArkPlanUsage("AccountQuotaExceeded", null, null, exhaustedWindow),
            _ => new ArkPlanUsage("AccountQuotaExceeded", exhaustedWindow, null, null),
        };
    }

    /// <summary>
    /// 从耗尽响应正文防御性解析重置时刻（秒级 epoch）：JSON 键直取（ResetTimestamp/reset_timestamp/
    /// resetTime/reset_time/nextResetTime/next_reset_time/ResetAt/reset_at，嵌套对象内递归），
    /// 数值归一（&gt;1e12 按毫秒、&gt;1e9 按秒）或 ISO 字符串；非 JSON 正文（纯文本错误消息）才走正则兜底
    /// （ISO 时间 → 毫秒 epoch → 秒 epoch）。JSON 有效但无重置键 → null——不拿正文其他时间戳凑数（防误取 RequestTime）。
    /// </summary>
    internal static long? TryParseResetSeconds(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var node in DescendantElements(document.RootElement))
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                foreach (var name in ResetJsonKeys)
                {
                    if (!node.TryGetProperty(name, out var element))
                    {
                        continue;
                    }
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var epoch)
                        && NormalizeEpoch(epoch) is { } seconds)
                    {
                        return seconds;
                    }
                    if (element.ValueKind == JsonValueKind.String && element.GetString() is { } text)
                    {
                        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw)
                            && NormalizeEpoch(raw) is { } fromRaw)
                        {
                            return fromRaw;
                        }
                        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment))
                        {
                            return moment.ToUnixTimeSeconds();
                        }
                    }
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return RegexReset(body);
        }
    }

    private static readonly string[] ResetJsonKeys =
        ["ResetTimestamp", "reset_timestamp", "resetTime", "reset_time", "nextResetTime", "next_reset_time", "ResetAt", "reset_at"];

    /// <summary>自身 + 全部后代元素（深度 4 封顶：429 错误体嵌套浅，防深树全扫）。</summary>
    private static IEnumerable<JsonElement> DescendantElements(JsonElement element, int depth = 0)
    {
        if (depth > 4)
        {
            yield break;
        }
        yield return element;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in DescendantElements(property.Value, depth + 1))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in DescendantElements(item, depth + 1))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>epoch 数值归一：&gt;1e12 按毫秒、&gt;1e9 按秒，其余 null（0/-1 哨兵等非时间戳数字不误判）。</summary>
    private static long? NormalizeEpoch(long value)
        => value > 1_000_000_000_000 ? value / 1000
        : value > 1_000_000_000 ? value
        : null;

    private static long? RegexReset(string body)
    {
        // ISO 时间（2026-10-12T00:00Z / 2026-10-12 00:00）
        var iso = Regex.Match(body, @"\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}(:\d{2})?");
        if (iso.Success && DateTimeOffset.TryParse(iso.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment))
        {
            return moment.ToUnixTimeSeconds();
        }
        // 毫秒 epoch（当前年代 13 位）
        var milliseconds = Regex.Match(body, @"\b1[5-9]\d{11}\b");
        if (milliseconds.Success && long.TryParse(milliseconds.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var msValue))
        {
            return msValue / 1000;
        }
        // 秒 epoch（当前年代 10 位）
        var secondsMatch = Regex.Match(body, @"\b1[5-9]\d{8}\b");
        if (secondsMatch.Success && long.TryParse(secondsMatch.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var secValue))
        {
            return secValue;
        }
        return null;
    }

    /// <summary>已用尽窗口（Percent ≥ 100），按重置时间升序（无重置的排末）——主摘要取首窗（最早恢复）。</summary>
    private static List<(string Name, ArkUsageWindow Window)> ExhaustedWindows(ArkPlanUsage usage)
    {
        var windows = new List<(string Name, ArkUsageWindow Window)>();
        if (usage.Session is { } session && session.Percent >= 100)
        {
            windows.Add(("5h 窗", session));
        }
        if (usage.Weekly is { } weekly && weekly.Percent >= 100)
        {
            windows.Add(("周", weekly));
        }
        if (usage.Monthly is { } monthly && monthly.Percent >= 100)
        {
            windows.Add(("月", monthly));
        }
        return windows.OrderBy(entry => entry.Window.ResetSeconds ?? long.MaxValue).ToList();
    }

    /// <summary>级别阈值：最差窗口 ≥error_percent → Error，≥warn_percent → Warning，否则 Success。</summary>
    public static Severity MapSeverity(double worstPercent, double warnPercent, double errorPercent)
        => worstPercent >= errorPercent ? Severity.Error
            : worstPercent >= warnPercent ? Severity.Warning
            : Severity.Success;

    /// <summary>最近一次重置（秒级 epoch → 本地 MM-dd HH:mm）；三窗口都无重置 → null。</summary>
    internal static long? NearestReset(ArkPlanUsage usage)
        => new long?[] { usage.Session?.ResetSeconds, usage.Weekly?.ResetSeconds, usage.Monthly?.ResetSeconds }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .Cast<long?>()
            .FirstOrDefault();

    internal static string FormatReset(long epochSeconds)
        => DateTimeOffset.FromUnixTimeSeconds(epochSeconds).LocalDateTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Truncate(string value) => value.Length <= 180 ? value : value[..180];

    // —— 火山 V4 签名（docs.volcengine.com/docs/6369/67269；scope 终止符 request、算法串无 AWS4 前缀） ——

    internal const string EmptyBodySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>
    /// 构造 Authorization 头。canonical headers 与实际发送头必须逐字一致
    /// （content-type/host/x-content-sha256/x-date 按字母序声明并同序发送）。
    /// xDate/payloadHash 输出供调用方设置同名请求头。
    /// </summary>
    public static string BuildAuthorization(
        string accessKey,
        string secretKey,
        string host,
        string region,
        string query,
        DateTimeOffset utcNow,
        out string xDate,
        out string payloadHash)
    {
        xDate = utcNow.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var shortDate = xDate[..8];
        payloadHash = EmptyBodySha256; // 空 body

        var contentType = "application/json; charset=utf-8";
        var canonicalHeaders =
            $"content-type:{contentType}\n" +
            $"host:{host}\n" +
            $"x-content-sha256:{payloadHash}\n" +
            $"x-date:{xDate}\n";
        var signedHeaders = "content-type;host;x-content-sha256;x-date";

        var canonicalRequest = $"POST\n/\n{query}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var stringToSign = $"HMAC-SHA256\n{xDate}\n{shortDate}/{region}/ark/request\n{Sha256Hex(canonicalRequest)}";

        var kDate = Hmac(secretKey, shortDate);
        var kRegion = Hmac(kDate, region);
        var kService = Hmac(kRegion, "ark");
        var kSigning = Hmac(kService, "request");
        var signature = Hex(Hmac(kSigning, stringToSign));

        return $"HMAC-SHA256 Credential={accessKey}/{shortDate}/{region}/ark/request, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static byte[] Hmac(string key, string value) => Hmac(Encoding.UTF8.GetBytes(key), value);

    private static byte[] Hmac(byte[] key, string value) => new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(value));

    private static string Sha256Hex(string value) => Hex(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Hex(byte[] data) => Convert.ToHexString(data).ToLowerInvariant();
}
