using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>deepseek（DeepSeek 开放平台）Widget 类型元数据（positioning P0 #5：AI Usage 一级概念）。</summary>
public static class DeepSeekWidgetDescriptors
{
    public const string BalanceType = "deepseek.balance";

    public static WidgetTypeDescriptor Balance { get; } = new()
    {
        Type = BalanceType,
        DisplayName = "DeepSeek 余额（开放平台）",
        PinSupported = true,
        SuggestedTier = RefreshTiers.Default,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "DeepSeek"),
            new WidgetFieldDescriptor("warn_below", "余额低于此值告警", Placeholder: "默认 20（按余额币种）"),
            new WidgetFieldDescriptor("error_below", "余额低于此值报错", Placeholder: "默认 5（按余额币种）"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Balance];
}

/// <summary>
/// deepseek.balance（positioning P0 #5）：DeepSeek 开放平台余额监控。
/// 接口（官方文档 + dsh-plugin-llm-balance 双源验证）：GET {Endpoint}/user/balance，
/// Authorization: Bearer &lt;Key&gt;。响应 is_available + balance_infos[]（currency CNY/USD，
/// total_balance/granted_balance/topped_up_balance 均为字符串金额）。
/// 级别：is_available=false → Error；total ≤ error_below → Error；≤ warn_below → Warning；否则 Success。
/// </summary>
public sealed class DeepSeekBalanceProvider : IWidgetProvider
{
    /// <summary>DeepSeek 开放平台默认端点（base，/user/balance 由 Provider 拼接）。</summary>
    public const string DefaultEndpoint = "https://api.deepseek.com";

    private readonly HttpClient? _client;

    public DeepSeekBalanceProvider() { }

    public DeepSeekBalanceProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => DeepSeekWidgetDescriptors.Balance;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // HttpEndpoint 取整 URL：默认端点直接给全路径（base + /user/balance）
        var body = await HttpEndpoint.FetchAsync(
            _client,
            connection,
            context,
            cancellationToken,
            defaultEndpoint: DefaultEndpoint + "/user/balance",
            defaultAuthPrefix: "Bearer ").ConfigureAwait(false);

        var balance = ParseBalance(body);
        if (balance is null)
        {
            throw new ConnectionException("DeepSeek 响应不含余额数据（接口结构可能变化）", ConnectionHealthState.Degraded);
        }

        var warnBelow = widget.Config.TryGetValue("warn_below", out var warnRaw) && double.TryParse(warnRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var warn) ? warn : 20;
        var errorBelow = widget.Config.TryGetValue("error_below", out var errorRaw) && double.TryParse(errorRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var error) ? error : 5;
        var severity = MapSeverity(balance.IsAvailable, balance.Total, warnBelow, errorBelow);
        var head = widget.Config.GetValueOrDefault("label") ?? "DeepSeek";

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
            Summary = Summarize(head, balance),
            DetailUrl = "https://platform.deepseek.com/usage",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["currency"] = balance.Currency,
                ["total"] = balance.Total.ToString("0.00", CultureInfo.InvariantCulture),
                ["granted"] = balance.Granted?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
                ["topped_up"] = balance.ToppedUp?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
                ["is_available"] = balance.IsAvailable ? "true" : "false",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>余额快照（金额统一 double，接口字符串金额与数字都兼容）。</summary>
    public sealed record AccountBalance(bool IsAvailable, string Currency, double Total, double? Granted, double? ToppedUp);

    /// <summary>解析 /user/balance 响应（纯函数供单测）：取第一个可解析的 balance_infos 条目。</summary>
    internal static AccountBalance? ParseBalance(string json)
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
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("balance_infos", out var infos) || infos.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var isAvailable = !root.TryGetProperty("is_available", out var availableElement) || availableElement.ValueKind != JsonValueKind.False;
            foreach (var item in infos.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("total_balance", out var totalElement))
                {
                    continue;
                }
                double? total = totalElement.ValueKind == JsonValueKind.Number && totalElement.TryGetDouble(out var numeric)
                    ? numeric
                    : totalElement.ValueKind == JsonValueKind.String && double.TryParse(totalElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : null;
                if (total is not { } value)
                {
                    continue;
                }
                var currency = item.TryGetProperty("currency", out var currencyElement) && currencyElement.ValueKind == JsonValueKind.String
                    ? currencyElement.GetString()
                    : null;
                return new AccountBalance(
                    isAvailable,
                    currency ?? "CNY",
                    value,
                    ReadAmount(item, "granted_balance"),
                    ReadAmount(item, "topped_up_balance"));
            }
            return null;
        }
    }

    private static double? ReadAmount(JsonElement item, string property)
        => item.TryGetProperty(property, out var element)
            ? element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var numeric)
                ? numeric
                : element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null
            : null;

    /// <summary>级别映射（纯函数供单测）：不可用/余额击穿下限即升级，未配阈值走默认。</summary>
    internal static Severity MapSeverity(bool isAvailable, double total, double warnBelow, double errorBelow)
        => !isAvailable || total <= errorBelow ? Severity.Error
            : total <= warnBelow ? Severity.Warning
            : Severity.Success;

    /// <summary>摘要：币种符号 + 总余额；含赠送余额时补注（总额已含赠送）。</summary>
    internal static string Summarize(string head, AccountBalance balance)
    {
        var symbol = balance.Currency switch
        {
            "CNY" => "¥",
            "USD" => "$",
            _ => "",
        };
        var amount = symbol + balance.Total.ToString("0.00", CultureInfo.InvariantCulture) + (symbol == "" ? " " + balance.Currency : "");
        return balance.Granted is { } granted && granted > 0
            ? $"{head} · {amount}（含赠 {symbol}{granted.ToString("0.00", CultureInfo.InvariantCulture)}）"
            : $"{head} · {amount}";
    }
}

/// <summary>deepseek 连接：TestAsync = GET /user/balance（Endpoint 可填 base，可空取默认）。</summary>
public sealed class DeepSeekConnectionProvider : IConnectionProvider
{
    private readonly HttpClient? _client;

    public DeepSeekConnectionProvider() { }

    public DeepSeekConnectionProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public string ConnectionType => "deepseek";

    public async Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await HttpEndpoint.FetchAsync(
                _client,
                connection,
                context,
                cancellationToken,
                defaultEndpoint: DeepSeekBalanceProvider.DefaultEndpoint + "/user/balance",
                defaultAuthPrefix: "Bearer ").ConfigureAwait(false);
            return ConnectionHealthState.Healthy;
        }
        catch (ConnectionException exception)
        {
            return exception.Health;
        }
    }
}
