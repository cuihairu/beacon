using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>moonshot（Moonshot 开放平台）Widget 类型元数据（positioning 末条：开放平台余额）。</summary>
public static class MoonshotWidgetDescriptors
{
    public const string BalanceType = "moonshot.balance";

    public static WidgetTypeDescriptor Balance { get; } = new()
    {
        Type = BalanceType,
        DisplayName = "Moonshot 余额（开放平台）",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Default,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Moonshot"),
            new WidgetFieldDescriptor("warn_below", "余额低于此值告警", Placeholder: "默认 20"),
            new WidgetFieldDescriptor("error_below", "余额低于此值报错", Placeholder: "默认 5"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Balance];
}

/// <summary>
/// moonshot.balance（positioning 末条）：Moonshot 开放平台余额监控（区别于 Kimi For Coding 套餐，
/// 两套账号体系）。接口：GET https://api.moonshot.cn/v1/users/me/balance，Authorization: Bearer &lt;Key&gt;。
/// 响应 { code: 0, data: { available_balance } }。级别：available_balance ≤ error_below → Error；
/// ≤ warn_below → Warning；否则 Success。
/// </summary>
public sealed class MoonshotBalanceProvider : IWidgetProvider
{
    /// <summary>Moonshot 开放平台余额端点（全 URL，无 base+path 拆分）。</summary>
    public const string DefaultEndpoint = "https://api.moonshot.cn/v1/users/me/balance";

    private readonly HttpClient? _client;

    public MoonshotBalanceProvider() { }

    public MoonshotBalanceProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => MoonshotWidgetDescriptors.Balance;

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

        var balance = ParseBalance(body);
        if (balance is null)
        {
            throw new ConnectionException("Moonshot 响应不含余额数据（接口结构可能变化）", ConnectionHealthState.Degraded);
        }

        var warnBelow = widget.Config.TryGetValue("warn_below", out var warnRaw) && double.TryParse(warnRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var warn) ? warn : 20;
        var errorBelow = widget.Config.TryGetValue("error_below", out var errorRaw) && double.TryParse(errorRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var error) ? error : 5;
        var severity = MapSeverity(balance.Value, warnBelow, errorBelow);
        var head = widget.Config.GetValueOrDefault("label") ?? "Moonshot";

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
            Summary = Summarize(head, balance.Value),
            DetailUrl = "https://platform.moonshot.cn/console/account",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_source"] = "api",
                ["available_balance"] = balance.Value.ToString("0.00", CultureInfo.InvariantCulture),
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>解析 /v1/users/me/balance 响应（纯函数供单测）：取 data.available_balance。</summary>
    internal static double? ParseBalance(string json)
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
                || !root.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("available_balance", out var balanceElement))
            {
                return null;
            }
            return balanceElement.ValueKind == JsonValueKind.Number && balanceElement.TryGetDouble(out var numeric)
                ? numeric
                : balanceElement.ValueKind == JsonValueKind.String && double.TryParse(balanceElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;
        }
    }

    /// <summary>级别映射（纯函数供单测）：余额击穿下限即升级，未配阈值走默认。</summary>
    internal static Severity MapSeverity(double balance, double warnBelow, double errorBelow)
        => balance <= errorBelow ? Severity.Error
            : balance <= warnBelow ? Severity.Warning
            : Severity.Success;

    /// <summary>摘要：余额金额。</summary>
    internal static string Summarize(string head, double balance)
        => $"{head} · ¥{balance.ToString("0.00", CultureInfo.InvariantCulture)}";
}

/// <summary>moonshot 连接：TestAsync = GET /v1/users/me/balance（Endpoint 可空取默认），Bearer 认证。</summary>
public sealed class MoonshotConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client;

    public MoonshotConnectionProvider() { }

    public MoonshotConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "moonshot";

    public async Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var body = await HttpEndpoint.FetchAsync(
                _client,
                connection,
                context,
                cancellationToken,
                defaultEndpoint: MoonshotBalanceProvider.DefaultEndpoint,
                defaultAuthPrefix: "Bearer ").ConfigureAwait(false);
            // 200 但结构不对（如代理回错误体）不算连通：余额接口语义校验
            return MoonshotBalanceProvider.ParseBalance(body) is null
                ? new ConnectionTestResult(ConnectionHealthState.Degraded, "Moonshot 响应不含余额数据（检查端点是否指向开放平台接口）。")
                : ConnectionTestResult.Ok();
        }
        catch (ConnectionException exception)
        {
            return new ConnectionTestResult(exception.Health, exception.Message);
        }
    }
}
