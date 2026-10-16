using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 附加提示面的四态判定：Phase 5「统一的空数据、离线与错误状态」。
/// 此前同一条件在各窗口各写一遍且格式不齐：离线有「Last update HH:mm」（胶囊横幅）与
/// 「HH:mm:ss」（tile 工具提示、通知消息）两种时制；无数据时详情窗静默缺 DETAILS 区
/// （或 payload 只有 tile 数值位提示键时画出「区段签 + 空表」）；失败原因无统一提示面。
/// 收编为一个纯函数：UI 侧只把 <see cref="FeedbackKind"/> 映射到 ComponentFeedback 词汇件。
/// 判定优先级 错误 &gt; 离线 &gt; 空数据 &gt; 无——错误说明比「没数据」更能回答「为什么」。
/// </summary>
public static class WidgetFeedback
{
    /// <summary>失败原因键（Provider 可选写入；两者取前者）。</summary>
    public const string ErrorKey = "error";

    /// <summary>失败原因键的别名（与 status_path 类声明同族的长名写法）。</summary>
    public const string ErrorSummaryKey = "error_summary";

    /// <summary>tile 数值位提示键：详情表不重复占行（DetailWindow 同口径），故不计入「有明细」。</summary>
    public const string ValueHintKey = "value_text";

    /// <summary>
    /// 四态判定。
    /// </summary>
    /// <param name="state">Widget 运行时状态。</param>
    /// <returns>
    /// <see cref="FeedbackKind.None"/> = 无附加提示面；否则 <see cref="FeedbackPresentation.Headline"/> 为本地化区段标签、
    /// <see cref="FeedbackPresentation.Caption"/> 为具体内容（离线 = Last update 时戳；错误 = 原因文本；空数据 = null）。
    /// </returns>
    public static FeedbackPresentation Resolve(WidgetState state)
    {
        var reason = PayloadText(state.Payload, ErrorKey) ?? PayloadText(state.Payload, ErrorSummaryKey);
        if (state.Severity is Severity.Error or Severity.Critical && reason is not null)
        {
            return new FeedbackPresentation(FeedbackKind.Error, UiStrings.FeedbackError, reason);
        }

        if (state.IsStale || !state.ConnectionHealthy)
        {
            return new FeedbackPresentation(FeedbackKind.Offline, UiStrings.FeedbackOffline,
                LastUpdate(state.FetchedAt, withSeconds: false));
        }

        if (!HasDetailRows(state.Payload))
        {
            return new FeedbackPresentation(FeedbackKind.Empty, UiStrings.FeedbackEmpty, null);
        }

        return FeedbackPresentation.None;
    }

    /// <summary>
    /// 离线/陈旧的时间戳文案（RFC §6.2.6：显示 Last update 而不甩异常）。
    /// 胶囊横幅与 tile 走分钟制，工具提示与通知消息走秒制——同一定义两处口径不再漂移。
    /// </summary>
    public static string LastUpdate(DateTimeOffset fetchedAt, bool withSeconds = false)
        => "Last update " + fetchedAt.ToLocalTime().ToString(withSeconds ? "HH:mm:ss" : "HH:mm");

    /// <summary>是否还有值得画成详情表的行（value_text 与纯空白值不计）。</summary>
    public static bool HasDetailRows(IReadOnlyDictionary<string, string> payload)
        => payload.Any(pair => pair.Key != ValueHintKey && !string.IsNullOrWhiteSpace(pair.Value));

    private static string? PayloadText(IReadOnlyDictionary<string, string> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }
}

/// <summary>附加提示面的形态与文案（不可变；<see cref="None"/> 为无提示面单例）。</summary>
public sealed record FeedbackPresentation(FeedbackKind Kind, string Headline, string? Caption)
{
    public static readonly FeedbackPresentation None = new(FeedbackKind.None, string.Empty, null);
}

/// <summary>附加提示面四态。</summary>
public enum FeedbackKind
{
    /// <summary>无附加提示面。</summary>
    None,

    /// <summary>空数据：无明细行。</summary>
    Empty,

    /// <summary>离线/陈旧：显示 Last update。</summary>
    Offline,

    /// <summary>错误：显示 Provider 给出的失败原因。</summary>
    Error,
}
