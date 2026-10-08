using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>
/// http.quota（验收反馈 2026-10-08：连上额度源却无组件可展示）：任意 HTTP JSON 额度/配额接口 → 数值卡。
/// 字段映射可配：total_path（必填）+ used_path / remaining_path 二选一 + reset_path（epoch 秒/毫秒/ISO 自动识别），
/// 已用百分比 = used/total（只配 remaining 时 = (total-remaining)/total），进度条走 WidgetState.Progress（0-1）。
/// 级别阈值与 bigmodel.usage 同口径：≥error_percent → Error，≥warn_percent → Warning，缺数据 → Warning（宁报勿漏）。
/// </summary>
public sealed class HttpQuotaProvider : IWidgetProvider
{
    private readonly HttpClient? _client; // null = 共享池（生产路径）；测试注入伪造 handler

    public HttpQuotaProvider() { }

    public HttpQuotaProvider(HttpMessageHandler handler) => _client = HttpEndpoint.Create(handler);

    public WidgetTypeDescriptor Descriptor => HttpWidgetDescriptors.Quota;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var body = await HttpEndpoint.FetchAsync(_client, connection, context, cancellationToken).ConfigureAwait(false);

        QuotaReading reading;
        try
        {
            using var document = JsonDocument.Parse(body);
            reading = ParseQuota(document.RootElement, widget.Config);
        }
        catch (JsonException exception)
        {
            throw new ConnectionException($"HTTP 响应不是合法 JSON：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
        if (reading is null)
        {
            throw new ConnectionException("额度解析失败——total_path 未取到数值，或 used_path/remaining_path 均未配置/取到。", ConnectionHealthState.Degraded);
        }

        var severity = MapSeverity(reading.Percent,
            widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, CultureInfo.InvariantCulture, out var warn) ? warn : 60,
            widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, CultureInfo.InvariantCulture, out var error) ? error : 90);
        var label = widget.Config.GetValueOrDefault("label");
        var unit = widget.Config.GetValueOrDefault("unit") ?? "";
        var head = label is { Length: > 0 } ? label : "额度";
        var summary = $"{head} · 已用 {Format(reading.Percent)}%";
        if (reading.Remaining is { } remaining)
        {
            summary += $" · 剩 {Format(remaining)}{FormatUnit(unit)}";
        }
        if (reading.ResetAt is { } reset)
        {
            summary += $" · {reset.ToLocalTime():MM-dd HH:mm} 重置";
        }

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success,
            Progress = Math.Clamp(reading.Percent, 0, 100) / 100.0, // 数值卡进度条（L0 tile 渲染）
            Summary = summary,
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = Format(reading.Percent),
                ["used"] = reading.Used is { } used ? Format(used) : "",
                ["total"] = Format(reading.Total),
                ["remaining"] = reading.Remaining is { } value ? Format(value) : "",
                ["unit"] = unit,
                ["reset_iso"] = reading.ResetAt is { } at ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "",
                ["endpoint"] = connection.Endpoint ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>解析出的额度读数。Percent = 已用百分比（0-100，可超 100 供严重度判定）。</summary>
    public sealed record QuotaReading(double Percent, double Total, double? Used, double? Remaining, DateTimeOffset? ResetAt);

    /// <summary>按配置的点路径提取额度（纯函数供单测）。total 缺失/非正数，或 used/remaining 均取不到 → null。</summary>
    internal static QuotaReading? ParseQuota(JsonElement root, IReadOnlyDictionary<string, string> config)
    {
        var total = SelectNumber(root, config.GetValueOrDefault("total_path"));
        if (total is not { } totalValue || totalValue <= 0)
        {
            return null;
        }

        double? used = SelectNumber(root, config.GetValueOrDefault("used_path"));
        double? remaining = SelectNumber(root, config.GetValueOrDefault("remaining_path"));
        if (used is not { } usedValue)
        {
            if (remaining is not { } remainingValue)
            {
                return null; // used/remaining 二选一必填
            }
            usedValue = totalValue - remainingValue; // 剩余口径反推已用
        }
        var reset = SelectReset(root, config.GetValueOrDefault("reset_path"));
        return new QuotaReading(usedValue / totalValue * 100.0, totalValue, used, remaining, reset);
    }

    /// <summary>点路径取数（数字或数值字符串，兼容千分位逗号）；路径可空。无路径/取不到 → null。</summary>
    internal static double? SelectNumber(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var raw = HttpJsonPath.Select(root, path);
        if (raw is null)
        {
            return null;
        }
        return double.TryParse(raw.Trim().Trim('"').Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>重置时间：epoch 毫秒（≥1e12）/ epoch 秒（≥1e9）/ 其余按 DateTimeOffset 文本解析；取不到 → null。</summary>
    internal static DateTimeOffset? SelectReset(JsonElement root, string? path)
    {
        var raw = HttpJsonPath.Select(root, path);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var text = raw.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var epoch))
        {
            if (epoch >= 1_000_000_000_000)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)epoch);
            }
            if (epoch >= 1_000_000_000)
            {
                return DateTimeOffset.FromUnixTimeSeconds((long)epoch);
            }
        }
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>级别映射（纯函数供单测，与 bigmodel.usage 同口径）：无数据 → Warning（宁报勿漏）。</summary>
    internal static Severity MapSeverity(double percent, double warnPercent, double errorPercent)
        => percent >= errorPercent ? Severity.Error
            : percent >= warnPercent ? Severity.Warning
            : Severity.Success;

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string FormatUnit(string unit) => unit.Length == 0 ? "" : $" {unit}";
}
