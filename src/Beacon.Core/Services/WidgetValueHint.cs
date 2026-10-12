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
    /// <summary>取 provider 主数值：按「显式文本 → 计数 → 已用% → 剩余% → 套餐 → 金额 → token」优先级链。</summary>
    public static string Of(WidgetState state)
    {
        var payload = state.Payload;

        if (Text(payload, "value_text") is { } explicitValue)
        {
            return explicitValue; // provider 显式短文本（如 mimo 无额度口时的「无额度口」——模型数不得冒充额度）
        }
        if (Text(payload, "total_calls") is { } calls)
        {
            return $"{FormatCount(LongOrZero(calls))} 次"; // mimo.usage 本机计数（2026-10-10 用户令：额度位给真实口径）
        }
        if (Text(payload, "open_count") is { } open)
        {
            return $"{open} open"; // github.pull_requests
        }
        if (MultiWindow(payload) is { } windows)
        {
            return windows; // 多窗口额度（2026-10-12 用户令）：bigmodel/ark 已用、kimi 剩余
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
        // models（模型目录数）不再映射数值位：2026-10-10 用户令——模型数不是额度，不得占额度位
        // （mimo 无计数源时由 provider 显式给 value_text「无额度口」，模型数留在 Summary 次行）
        if (state.IsStale)
        {
            return "stale";
        }
        return state.Summary.Length > 8 ? state.Summary[..7] + "…" : state.Summary;
    }

    /// <summary>取非空 payload 值；缺失/空串统一按「无此键」走下一优先级。</summary>
    private static string? Text(IReadOnlyDictionary<string, string> payload, string key)
        => payload.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    // —— 多窗口额度（2026-10-12 用户令「多窗口额度展示」，最初需求的补落）——

    /// <summary>已用口径三窗（bigmodel.usage / ark.usage）：键 → 窗标。</summary>
    private static readonly (string Key, string Label)[] UsedWindows =
        [("rolling_percent", "5h"), ("weekly_percent", "w"), ("monthly_percent", "M")];

    /// <summary>剩余口径三窗（kimi.coding）：键 → 窗标（monthly 预留，接口今天只给 rolling/weekly）。</summary>
    private static readonly (string Key, string Label)[] RemainingWindows =
        [("rolling_remaining_percent", "5h"), ("weekly_remaining_percent", "w"), ("monthly_remaining_percent", "M")];

    /// <summary>
    /// 多窗口形态：同一口径的窗口键任意两个以上非空 → 「5%/5h 7%/w 8%/M」（剩余口径前缀「剩」，
    /// 窗序 5h → 周 → 月，只出实际存在的窗口）；单窗或全缺 → null 走既有单窗优先级链。
    /// 进度条语义不在此处：仍取最小可用窗口（provider 现有 Progress 同源，不变）。
    /// </summary>
    public static string? MultiWindow(IReadOnlyDictionary<string, string> payload)
        => Join(payload, RemainingWindows, "剩") ?? Join(payload, UsedWindows, "");

    private static string? Join(IReadOnlyDictionary<string, string> payload, (string Key, string Label)[] windows, string prefix)
    {
        List<string> segments = [];
        foreach (var (key, label) in windows)
        {
            if (Text(payload, key) is { } value)
            {
                segments.Add($"{prefix}{value}%/{label}");
            }
        }
        return segments.Count >= 2 ? string.Join(" ", segments) : null;
    }

    /// <summary>
    /// 多窗口形态下 tile 是否收起「NN%」进度读数：数值文本已含主位（最小可用）窗口的百分比，
    /// 读数是重复占宽（2026-10-12 用户令）；单窗形态维持读数常驻（现状不变）。
    /// </summary>
    public static bool SuppressProgressReadout(WidgetState state) => MultiWindow(state.Payload) is not null;

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
