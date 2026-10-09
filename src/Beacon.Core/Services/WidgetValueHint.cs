using System.Globalization;
using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 悬浮 tile 数值位短文本（纯函数，Core 承载供 Linux 单测；App 的 PinTile 只委托）。
/// 各 provider 的 payload 键名互不相同：此前 PinTile 只认 open_count/percent 两键，
/// 九家额度源里七家跌进 Summary[..22] 截断——数字被截没/被 168 DIP 窗宽裁掉，
/// 实测即「悬浮框没有数值」。此处按源逐一映射主数值，兜底截断收紧到 8 字符（窗宽预算内）。
/// </summary>
public static class WidgetValueHint
{
    /// <summary>取 provider 主数值：按「计数 → 已用% → 剩余% → 套餐 → 金额 → token → 目录」优先级链。</summary>
    public static string Of(WidgetState state)
    {
        var payload = state.Payload;

        if (Text(payload, "open_count") is { } open)
        {
            return $"{open} open"; // github.pull_requests
        }
        if (Text(payload, "percent") is { } percent)
        {
            return $"{percent}%"; // bigmodel.usage / ark.usage / http.quota（最差窗口已用）
        }
        if (Text(payload, "weekly_remaining_percent") is { } weekly)
        {
            return $"剩{weekly}%"; // kimi.coding（与卡面剩余口径一致）
        }
        if (Text(payload, "rolling_remaining_percent") is { } rolling)
        {
            return $"剩{rolling}%"; // kimi.coding：仅剩滚动窗口时退而取之
        }
        if (Text(payload, "premium_interactions_entitlement") is { } entitlement
            && Text(payload, "premium_interactions_remaining") is { } remaining
            && long.TryParse(entitlement, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ent))
        {
            // copilot.usage：premium 请求 已用/含额（quota_snapshots 只给 remaining）
            var used = long.TryParse(remaining, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rem)
                ? Math.Clamp(ent - rem, 0, ent)
                : 0;
            return $"{used}/{ent}";
        }
        if (Text(payload, "spent_usd") is { } spent)
        {
            return $"${spent}"; // opencode.usage：自然月消费（美元）
        }
        if (Text(payload, "total") is { } total)
        {
            // deepseek.balance：余额（币种自适应，缺省 CNY 与 provider 口径一致）
            var symbol = payload.GetValueOrDefault("currency") switch
            {
                "USD" => "$",
                "CNY" or null or "" => "¥",
                _ => "",
            };
            return $"{symbol}{total}";
        }
        if (Text(payload, "cost") is { } cost)
        {
            return $"${cost}"; // claude.usage：今日成本（provider 已按 0.00 格式化）
        }
        var tokensIn = Text(payload, "tokens_in");
        var tokensOut = Text(payload, "tokens_out");
        if (tokensIn is not null || tokensOut is not null)
        {
            // claude/codex：进出 token 合计（K 短格式，tile 数值位宽度预算内）
            return $"{FormatCount(LongOrZero(tokensIn) + LongOrZero(tokensOut))} tok";
        }
        if (Text(payload, "models") is { } models)
        {
            return $"{models} 模型"; // mimo.usage：模型目录（官方未开放用量口）
        }
        if (state.IsStale)
        {
            return "stale";
        }
        return state.Summary.Length > 8 ? state.Summary[..7] + "…" : state.Summary;
    }

    /// <summary>取非空 payload 值；缺失/空串统一按「无此键」走下一优先级。</summary>
    private static string? Text(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    /// <summary>token 短格式：1.2M / 45.6K / 890（Invariant，悬浮窗数值列不溢出）。</summary>
    private static string FormatCount(long value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (value / 1_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    private static long LongOrZero(string? value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
}
