using System.Globalization;
using System.Net;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Connections;

/// <summary>sensenova（商汤）Widget 类型元数据：控制台 Token Plan 积分池额度（多池 × 5h/7d 双窗）。</summary>
public static class SenseNovaWidgetDescriptors
{
    public const string UsageType = "sensenova.tokenplan";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "商汤套餐池额度（Token Plan）",
        PinSupported = true,
        DataKinds = DisplayTemplateKinds.ProgressOnly, // 类型级能力：产百分比（评审 P1 语义描述）
        FloatingOptIn = true,
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "商汤"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（%）", Placeholder: "默认 80"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（%）", Placeholder: "默认 95"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// sensenova.tokenplan（2026-10-13 用户令）：商汤控制台 Token Plan 积分池额度监控。
/// 接口 GET https://platform.sensenova.cn/lite/console/v1/tokenplan/pool-usage，鉴权 Bearer &lt;控制台 JWT&gt;
/// ——这是控制台会话令牌**不是 API Key**；凭据走本仓 CredentialRef/DPAPI 机制（整段只进 secrets，
/// config.json 仅 credentialRef，不进仓库不进日志）。JWT 带 exp：**过期回 401 → 显式 Degraded
/// 「控制台 token 已过期，请重新获取」，绝不静默空数据**。
/// 响应 plan + pools[]：每池 window_5h / window_7d（limit/used/remaining/reset_at）+ 赠额
/// grant_balance / nearest_grant_expiry / nearest_grant_expiring_balance。**口径与 kimi/ark 一致**：
/// 数值全为字符串小数（含千分位），每池算 5h/7d 已用%（used/limit×100），
/// 主数值/进度条/级别取**最差池的最差窗**（已用最高；并列按池序 → 5h → 7d），摘要多池齐显。
/// 赠额到期预警：最近到期日在 7 天内，或即将到期余额占该池赠额 ≤10%（大部分余额即将归零）→ 级别不低于 Warning。
/// 多账号 = 多连接（每连接一个控制台 JWT、各自一组 pools），不写死单账号。
/// </summary>
public sealed class SenseNovaUsageProvider : IWidgetProvider
{
    /// <summary>控制台 Token Plan 积分池额度口（2026-10-13 用户实抓）。</summary>
    public const string DefaultEndpoint = "https://platform.sensenova.cn/lite/console/v1/tokenplan/pool-usage";

    private const string DescriptorType = SenseNovaWidgetDescriptors.UsageType;
    private const string ConsoleUrl = "https://platform.sensenova.cn";
    private const string ExpiredMarker = "HTTP 认证失败 (401";
    private const string ExpiredMessage = "SenseNova 控制台 token 已过期，请重新获取（platform.sensenova.cn 控制台重新登录并复制令牌）";

    private readonly HttpClient? _client;

    public SenseNovaUsageProvider() { }

    public SenseNovaUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => SenseNovaWidgetDescriptors.Usage;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var usage = await FetchUsageAsync(connection, context, cancellationToken).ConfigureAwait(false);
        return ToState(widget, connection, usage);
    }

    /// <summary>拉取并解析积分池额度；JWT 过期抛 Degraded（显式提示），网络/结构错误抛对应健康态。</summary>
    public async Task<PlanUsage> FetchUsageAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await HttpEndpoint.FetchAsync(_client, connection, context, cancellationToken,
                defaultEndpoint: DefaultEndpoint, defaultAuthPrefix: "Bearer ").ConfigureAwait(false);
        }
        catch (ConnectionException exception) when (exception.Message.StartsWith(ExpiredMarker, StringComparison.Ordinal))
        {
            // JWT 有过期时间：401 就是令牌过期——显式 Degraded 提示重取，不静默空跑
            throw new ConnectionException(ExpiredMessage, ConnectionHealthState.Degraded, exception);
        }

        PlanUsage? usage;
        try
        {
            usage = ParseUsage(body);
        }
        catch (JsonException exception)
        {
            // 非 JSON 正文（网关错误页等）：显式 Degraded 不静默
            throw new ConnectionException($"SenseNova 响应非 JSON（接口异常）：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
        if (usage is null)
        {
            throw new ConnectionException("SenseNova 响应不含积分池数据（接口结构可能变化）", ConnectionHealthState.Degraded);
        }
        return usage;
    }

    internal static WidgetState ToState(WidgetConfig widget, ConnectionConfig connection, PlanUsage usage)
    {
        var warnPercent = ReadPercent(widget.Config, "warn_percent", 80);
        var errorPercent = ReadPercent(widget.Config, "error_percent", 95);
        var head = widget.Config.GetValueOrDefault("label") ?? "商汤";

        // 无池 / 无窗口数据：合法状态非故障——Info 明示，不造假红不静默空
        if (usage.Pools.Count == 0)
        {
            return State(widget, connection, Severity.Info, null, $"{head} · 无积分池（确认控制台 Token Plan 订阅状态）",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["plan_name"] = usage.PlanName ?? "" },
                LifecycleState.Unknown);
        }
        if (usage.Worst() is null)
        {
            return State(widget, connection, Severity.Info, null, $"{head} · 积分池无窗口额度数据（接口结构可能变化）",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["pool_count"] = usage.Pools.Count.ToString(CultureInfo.InvariantCulture) },
                LifecycleState.Unknown);
        }

        var (worstPool, worstKey, worstLabel, worstWindow) = usage.Worst()!.Value;
        var percent = worstWindow.UsedPercent!.Value;
        var severity = MapSeverity(percent, warnPercent, errorPercent);
        if (severity < Severity.Warning && PlanUsage.GrantExpiringSoon(usage))
        {
            // 赠额即将归零（≤10% 或 7 天内到期）：级别不低于 Warning，避免「窗口还绿但余额要清零」漏报
            severity = Severity.Warning;
        }

        var grantPool = PrimaryGrantPool(usage);
        var grantTail = grantPool is { } grant
            ? $"剩余 {FormatNumber(grant.GrantBalance!.Value)}"
              + (grant.NearestGrantExpirySeconds is { } expiry ? $" · {FormatDate(expiry)} 到期" : "")
            : null;
        var poolsText = PoolsText(usage);
        var summary = $"{head} · {poolsText}" + (grantTail is { } tail ? " · " + tail : "");

        return State(widget, connection, severity, Math.Clamp(percent, 0, 100) / 100.0,
            summary,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 卡面主数值：最差池的最差窗（池名 + 窗名 + 已用%，用户令 2026-10-13）
                ["value_text"] = $"{worstPool.ShortName} {worstLabel} 已用{FormatPercent(percent)}%",
                ["percent"] = FormatNumber(percent),
                ["worst_pool"] = worstPool.ShortName,
                ["worst_window"] = worstKey,
                ["pool_count"] = usage.Pools.Count.ToString(CultureInfo.InvariantCulture),
                ["pools"] = poolsText,
                ["reset_iso"] = worstWindow.ResetSeconds is { } reset ? FormatDate(reset) : "",
                ["grant_balance"] = grantPool?.GrantBalance is { } grantBalance ? FormatNumber(grantBalance) : "",
                ["grant_expiry"] = grantPool?.NearestGrantExpirySeconds is { } gExp ? FormatDate(gExp) : "",
                ["grant_expiring_soon"] = PlanUsage.GrantExpiringSoon(usage) ? "1" : "",
                ["plan_name"] = usage.PlanName ?? "",
            });
    }

    private static WidgetState State(WidgetConfig widget, ConnectionConfig connection, Severity severity,
        double? progress, string summary, Dictionary<string, string> payload, LifecycleState? lifecycle = null)
        => new()
        {
            WidgetId = widget.Id,
            WidgetType = DescriptorType,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = lifecycle ?? (severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success),
            Progress = progress,
            Summary = summary,
            DetailUrl = ConsoleUrl,
            Payload = payload,
            FetchedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>摘要多池齐显：每池「短名 5h X% · 7d Y%」，池间「 / 」分隔（用户令 2026-10-13）。</summary>
    private static string PoolsText(PlanUsage usage)
    {
        var parts = new List<string>();
        foreach (var pool in usage.Pools)
        {
            var segments = new List<string>();
            if (pool.Window5h?.UsedPercent is { } rolling) segments.Add($"5h {FormatPercent(rolling)}%");
            if (pool.Window7d?.UsedPercent is { } weekly) segments.Add($"7d {FormatPercent(weekly)}%");
            if (segments.Count == 0) continue;
            parts.Add($"{pool.ShortName} {string.Join(" · ", segments)}");
        }
        return parts.Count > 0 ? string.Join(" / ", parts) : "无窗口数据";
    }

    /// <summary>赠额尾注取赠额最大的池（嵌套池不叠加，避免双计）；无赠额 → null。</summary>
    private static PoolUsage? PrimaryGrantPool(PlanUsage usage)
        => usage.Pools.Where(pool => pool.GrantBalance is { } balance && balance > 0)
            .OrderByDescending(pool => pool.GrantBalance!.Value)
            .Cast<PoolUsage?>()
            .FirstOrDefault();

    /// <summary>级别阈值：已用 ≥error → Error，≥warn → Warning，否则 Success。</summary>
    public static Severity MapSeverity(double percent, double warnPercent, double errorPercent)
        => percent >= errorPercent ? Severity.Error
            : percent >= warnPercent ? Severity.Warning
            : Severity.Success;

    /// <summary>解析 pool-usage 响应；pools 缺失 → null（调用方转 Degraded：接口结构变化需人查）。</summary>
    public static PlanUsage? ParseUsage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("pools", out var pools) || pools.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var planName = root.TryGetProperty("plan", out var plan) && plan.ValueKind == JsonValueKind.Object
            && plan.TryGetProperty("name", out var planNameElement) && planNameElement.ValueKind == JsonValueKind.String
                ? planNameElement.GetString()
                : null;
        var list = new List<PoolUsage>();
        foreach (var item in pools.EnumerateArray())
        {
            if (ParsePool(item) is { } pool)
            {
                list.Add(pool);
            }
        }
        return new PlanUsage(planName, list);
    }

    private static PoolUsage? ParsePool(JsonElement item)
    {
        var id = ReadString(item, "id");
        var name = ReadString(item, "name");
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name))
        {
            return null; // 空条目跳过（防 pools 里混入占位项）
        }
        return new PoolUsage(id, name, ParseWindow(item, "window_5h"), ParseWindow(item, "window_7d"),
            ReadDouble(item, "grant_balance"), ReadEpoch(item, "nearest_grant_expiry"),
            ReadDouble(item, "nearest_grant_expiring_balance"), ReadString(item, "pool_type"));
    }

    private static PoolWindow? ParseWindow(JsonElement item, string key)
    {
        if (!item.TryGetProperty(key, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var limit = ReadDouble(element, "limit");
        var used = ReadDouble(element, "used");
        var remaining = ReadDouble(element, "remaining");
        if (limit is null && used is null && remaining is null)
        {
            return null;
        }
        return new PoolWindow(UsedPercent(limit, used, remaining), limit, used, remaining, ReadEpoch(element, "reset_at"));
    }

    /// <summary>已用% = used/limit×100；used 缺失退 (limit−remaining)/limit；limit 缺失或 ≤0 退 null。</summary>
    private static double? UsedPercent(double? limit, double? used, double? remaining)
    {
        if (limit is not { } total || total <= 0)
        {
            return null;
        }
        var effectiveUsed = used ?? (remaining is { } left ? Math.Max(total - left, 0) : null);
        return effectiveUsed is { } consumed ? consumed / total * 100.0 : null;
    }

    /// <summary>数值双态读取（接口全给字符串小数，如 "27997.056"；含千分位时剥离逗号）。</summary>
    private static double? ReadDouble(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var element))
        {
            return null;
        }
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var numeric))
        {
            return numeric;
        }
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        if (text is null)
        {
            return null;
        }
        return double.TryParse(text.Replace(",", "", StringComparison.Ordinal), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>epoch 读取：字符串/数字双态；&gt;1e12 按毫秒折秒；0 及负数 = 接口「无到期」哨兵 → null。</summary>
    private static long? ReadEpoch(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var element))
        {
            return null;
        }
        long? raw = null;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number))
        {
            raw = number;
        }
        else if (element.ValueKind == JsonValueKind.String
            && long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            raw = parsed;
        }
        if (raw is not { } value || value <= 0)
        {
            return null;
        }
        return value > 1_000_000_000_000 ? value / 1000 : value;
    }

    private static string? ReadString(JsonElement item, string property)
        => item.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static double ReadPercent(IReadOnlyDictionary<string, string> config, string key, double fallback)
        => config.TryGetValue(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>已用% 摘要口径：整数四舍五入（用户令 2026-10-13：46.66→47、4.67→5）。</summary>
    private static string FormatPercent(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    /// <summary>数值短格式（0.###：17610.794 原样、46.66→46.7、100→100）。</summary>
    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>到期/重置日（epoch 秒 → 本地 MM-dd）；与 ark FormatReset 同口径。</summary>
    internal static string FormatDate(long epochSeconds)
        => DateTimeOffset.FromUnixTimeSeconds(epochSeconds).LocalDateTime.ToString("MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// 单池单窗额度（UsedPercent = used/limit×100；接口给字符串小数，解析时已转 double）。
/// ResetSeconds = reset_at 秒级 epoch，null = 接口未给。
/// </summary>
public sealed record PoolWindow(double? UsedPercent, double? Limit, double? Used, double? Remaining, long? ResetSeconds);

/// <summary>单个积分池（pools[] 一项）：5h/7d 双窗 + 赠额余额与最近到期。</summary>
public sealed record PoolUsage(string? Id, string? Name, PoolWindow? Window5h, PoolWindow? Window7d,
    double? GrantBalance, long? NearestGrantExpirySeconds, double? NearestGrantExpiringBalance, string? PoolType)
{
    /// <summary>短名（卡面/摘要用）：「积分池」缩为「池」（通用积分池→通用池）；空名退池 id；上限 14 字符。</summary>
    public string ShortName => ShortNameOf(Id, Name);

    /// <summary>最差窗（已用最高；并列 5h 优先）；两窗都无数据 → null。</summary>
    public (string Key, string Label, PoolWindow Window)? WorstWindow()
    {
        (string Key, string Label, PoolWindow Window)? worst = null;
        foreach (var (key, label, window) in new[] { ("5h", "5h", Window5h), ("7d", "7d", Window7d) })
        {
            if (window is not { } candidate || candidate.UsedPercent is not { } percent)
            {
                continue;
            }
            if (worst is null || percent > worst.Value.Window.UsedPercent!.Value)
            {
                worst = (key, label, candidate);
            }
        }
        return worst;
    }

    private static string ShortNameOf(string? id, string? name)
    {
        var raw = name is { Length: > 0 } ? name : id is { Length: > 0 } ? id : "池";
        if (raw.Contains("积分池", StringComparison.Ordinal))
        {
            raw = raw.Replace("积分池", "池");
        }
        return raw.Length <= 14 ? raw : raw[..13] + "…";
    }
}

/// <summary>套餐池集合（plan + pools[]）：最差池的最差窗驱动卡面（已用最高；并列按池序 → 5h → 7d）。</summary>
public sealed record PlanUsage(string? PlanName, IReadOnlyList<PoolUsage> Pools)
{
    /// <summary>最差池的最差窗；全部池无窗口数据 → null（调用方出 Info 卡，不静默空）。</summary>
    public (PoolUsage Pool, string Key, string Label, PoolWindow Window)? Worst()
    {
        (PoolUsage Pool, string Key, string Label, PoolWindow Window)? worst = null;
        foreach (var pool in Pools)
        {
            if (pool.WorstWindow() is not { } candidate)
            {
                continue;
            }
            if (worst is null || candidate.Window.UsedPercent!.Value > worst.Value.Window.UsedPercent!.Value)
            {
                worst = (pool, candidate.Key, candidate.Label, candidate.Window);
            }
        }
        return worst;
    }

    /// <summary>
    /// 赠额到期预警（用户令 2026-10-13）：任一池最近到期日在 7 天内，
    /// 或即将到期余额占该池赠额 ≤10%（大部分余额即将归零）→ 级别不低于 Warning。
    /// </summary>
    public static bool GrantExpiringSoon(PlanUsage usage) => usage.Pools.Any(pool => PoolGrantExpiringSoon(pool));

    internal static bool PoolGrantExpiringSoon(PoolUsage pool)
    {
        if (pool.NearestGrantExpirySeconds is not { } expiry)
        {
            return false;
        }
        if (pool.GrantBalance is { } balance && balance > 0
            && pool.NearestGrantExpiringBalance is { } expiring && expiring > 0
            && expiring / balance <= 0.1)
        {
            return true;
        }
        var daysLeft = (expiry - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / 86400.0;
        return daysLeft >= 0 && daysLeft < 7;
    }
}

/// <summary>sensenova 连接：TestAsync = 走与卡面同一条取数链路（JWT 过期同样报「token 已过期」）。</summary>
public sealed class SenseNovaConnectionProvider : IConnectionProvider
{
    private readonly HttpMessageHandler? _handler;

    public SenseNovaConnectionProvider() { }

    public SenseNovaConnectionProvider(HttpMessageHandler handler) => _handler = handler;

    public string ConnectionType => "sensenova";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await new SenseNovaUsageProvider(_handler!).FetchUsageAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok();
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
