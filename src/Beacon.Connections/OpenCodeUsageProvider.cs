using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>opencode（OpenCode Go）Widget 类型元数据（AI Usage 第九家）。</summary>
public static class OpenCodeWidgetDescriptors
{
    public const string UsageType = "opencode.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "OpenCode Go 用量",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Default, // 月度预算窗口（resets_at 按 UTC 自然月）
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "默认 OpenCode Go $10/月"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（已用 %）", Placeholder: "默认 80"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（已用 %）", Placeholder: "默认 95"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// opencode.usage（AI Usage 第九家）：OpenCode Go 套餐用量——**档①官方 Console API 直连**（文档面正式接口，
/// 2026-10-09 实测 200）：`GET opencode.ai/console/api/v1/budgets/members`（oc_sk Key Bearer，Console
/// Budgets API，v2 docs/console/api/budgets）。响应按成员给 `limit_micro_cents`（null=无上限）、
/// `spent_micro_cents`、`exceeded`、`resets_at`——金额为 micro-cents 十进制字符串（1 美元 = 1 亿 micro-cents，
/// 官方口径防精度丢失）。个人工作区单成员；多成员聚合（消费求和、任一 null 即无上限、任一 exceeded 即超）。
/// 卡面「Go $10/月」为展示层套餐标注（用户订阅口径）；API 只产用量数字，payload 不掺展示层信息。
/// 级别：exceeded → Error；否则已用百分比过阈值（默认 80/95 可配）；无上限 → Success 基线。
/// </summary>
public sealed class OpenCodeUsageProvider : IWidgetProvider
{
    public const string DefaultEndpoint = "https://opencode.ai/console/api/v1/budgets/members";

    /// <summary>官方口径：100,000,000 micro-cents = 1 美元。</summary>
    public const long MicroCentsPerDollar = 100_000_000;

    private readonly HttpClient? _client;

    public OpenCodeUsageProvider() { }

    public OpenCodeUsageProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => OpenCodeWidgetDescriptors.Usage;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var budgets = await FetchBudgetsAsync(connection, context, cancellationToken).ConfigureAwait(false);
        return ToState(widget, connection, budgets);
    }

    /// <summary>成员预算：GET console/api/v1/budgets/members（Bearer oc_sk Key）。</summary>
    public async Task<IReadOnlyList<OpenCodeBudget>> FetchBudgetsAsync(
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var key = await RequireKeyAsync(connection, context).ConfigureAwait(false);
        var endpoint = connection.Endpoint?.Trim().Length > 0 ? connection.Endpoint!.Trim() : DefaultEndpoint;
        var body = await SendAsync(endpoint, key, cancellationToken).ConfigureAwait(false);
        return ParseBudgets(body);
    }

    private async Task<string> SendAsync(string url, string key, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.ParseAdd("application/json");
        var client = _client ?? HttpEndpoint.Shared;
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // 401/403=Key 无效或为 inference-only 权限（Budgets 需 All 权限，Degraded 配置可修）；
            // 404=端点变动（Degraded）；5xx/其余=服务侧（Offline）
            throw new ConnectionException($"OpenCode API {(int)response.StatusCode}：{Truncate(body)}",
                response.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                    ? ConnectionHealthState.Offline
                    : ConnectionHealthState.Degraded);
        }
        return body;
    }

    /// <summary>解析成员预算数组；非数组/空数组 → 结构异常 Degraded（接口变了要人查，不静默给 0）。</summary>
    public static IReadOnlyList<OpenCodeBudget> ParseBudgets(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new ConnectionException("OpenCode Budgets 响应不含成员数组（接口结构可能变化）", ConnectionHealthState.Degraded);
        }
        var budgets = new List<OpenCodeBudget>();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            budgets.Add(new OpenCodeBudget(
                Email: item.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String ? email.GetString() : null,
                LimitMicroCents: ParseMicroCents(item, "limit_micro_cents"),
                SpentMicroCents: ParseMicroCents(item, "spent_micro_cents") ?? 0,
                Exceeded: item.TryGetProperty("exceeded", out var exceeded) && exceeded.ValueKind == JsonValueKind.True,
                ResetsAt: item.TryGetProperty("resets_at", out var resets) && resets.ValueKind == JsonValueKind.String ? resets.GetString() : null));
        }
        if (budgets.Count == 0)
        {
            throw new ConnectionException("OpenCode Budgets 响应为空数组（无成员数据）", ConnectionHealthState.Degraded);
        }
        return budgets;
    }

    private static long? ParseMicroCents(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }
        // 官方口径十进制字符串（null=无上限）；数字形态兼容
        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var numeric)
                ? numeric
                : null;
    }

    internal static WidgetState ToState(WidgetConfig widget, ConnectionConfig connection, IReadOnlyList<OpenCodeBudget> budgets)
    {
        var head = widget.Config.GetValueOrDefault("label") ?? "OpenCode Go $10/月";
        var warnPercent = widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, CultureInfo.InvariantCulture, out var warn) ? warn : 80;
        var errorPercent = widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, CultureInfo.InvariantCulture, out var error) ? error : 95;

        // 多成员聚合：消费求和；任一无上限即无上限；任一 exceeded 即超
        var spentMicroCents = budgets.Sum(budget => budget.SpentMicroCents);
        long? limitMicroCents = budgets.Any(budget => budget.LimitMicroCents is null) ? null : budgets.Sum(budget => budget.LimitMicroCents);
        var exceeded = budgets.Any(budget => budget.Exceeded);
        var resetsAt = budgets.Select(budget => budget.ResetsAt).FirstOrDefault(reset => reset is not null);

        Severity severity;
        if (exceeded)
        {
            severity = Severity.Error;
        }
        else if (limitMicroCents is long limit)
        {
            var usedPercent = limit > 0 ? spentMicroCents * 100.0 / limit : 0;
            severity = usedPercent >= errorPercent ? Severity.Error
                : usedPercent >= warnPercent ? Severity.Warning
                : Severity.Success;
        }
        else
        {
            severity = Severity.Success;
        }

        var spentDisplay = DollarsDisplay(spentMicroCents);
        var budgetPart = limitMicroCents is long bounded
            ? $"{spentDisplay} / {DollarsDisplay(bounded)}"
            : $"{spentDisplay} · 无上限";
        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = OpenCodeWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed : LifecycleState.Success,
            Summary = $"{head} · {budgetPart}{ResetDisplay(resetsAt)}",
            DetailUrl = "https://opencode.ai/console",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_source"] = "api", // 档①官方 Console Budgets API 直连
                ["spent_micro_cents"] = spentMicroCents.ToString("0", CultureInfo.InvariantCulture),
                ["spent_usd"] = Dollars(spentMicroCents).ToString("0.00", CultureInfo.InvariantCulture),
                ["limit_micro_cents"] = limitMicroCents?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["exceeded"] = exceeded ? "true" : "false",
                ["members"] = budgets.Count.ToString("0", CultureInfo.InvariantCulture),
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    private static double Dollars(long microCents) => microCents / (double)MicroCentsPerDollar;

    private static string DollarsDisplay(long microCents) => $"${Dollars(microCents).ToString("0.00", CultureInfo.InvariantCulture)}";

    /// <summary>重置日（resets_at ISO → UTC 日期 MM-dd 重置；解析失败原样展示，不编）。</summary>
    internal static string ResetDisplay(string? resetsAt)
    {
        if (resetsAt is null)
        {
            return ""; // 预算数据未带重置时间就不显示该段，不编
        }
        if (DateTimeOffset.TryParse(resetsAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var reset))
        {
            return $" · {reset.UtcDateTime:MM-dd} 重置";
        }
        return $" · {resetsAt} 重置";
    }

    private static async Task<string> RequireKeyAsync(ConnectionConfig connection, ConnectionContext context)
    {
        var credentialRef = connection.CredentialRef ?? "opencode:default";
        var key = await context.Secrets.GetAsync(credentialRef).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ConnectionException("缺少 OpenCode Key：opencode.ai Console Keys 创建（oc_sk，Budgets 读取需 All 权限）后填连接 Token 框",
                ConnectionHealthState.Degraded);
        }
        return key.Trim();
    }

    private static string Truncate(string value) => value.Length <= 120 ? value : value[..120];
}

/// <summary>单个成员的月度预算快照（金额 micro-cents；Limit null=无上限）。</summary>
public sealed record OpenCodeBudget(
    string? Email,
    long? LimitMicroCents,
    long SpentMicroCents,
    bool Exceeded,
    string? ResetsAt);

/// <summary>opencode 连接：测试连通 = GET budgets/members；Key 无效/权限不足 → Degraded（显式失败不静默）。</summary>
public sealed class OpenCodeConnectionProvider : IConnectionProvider
{
    private readonly HttpMessageHandler? _handler;

    public OpenCodeConnectionProvider() { }

    public OpenCodeConnectionProvider(HttpMessageHandler handler) => _handler = handler;

    public string ConnectionType => "opencode";

    public async Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await new OpenCodeUsageProvider(_handler!).FetchBudgetsAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
