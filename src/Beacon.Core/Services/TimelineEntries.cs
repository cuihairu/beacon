using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 时间线条目（Phase 5「时间线、表格、图表与趋势组件」，三诊口径「时间线走事件历史」）：
/// 把通知引擎的记录（旧→新）翻成时间线展示序——新在前（时间线自上而下读新事件）、
/// 截到 <see cref="MaxEntries"/> 条、时刻本地化 HH:mm。纯映射不排序改序号：输入的旧→新
/// 顺序即引擎语义，颠倒输出即可，不重复排序规则。空历史/空标题占位照收（时间线空态由
/// ComponentFeedback.Empty 承担，见 App 侧）。
/// </summary>
public static class TimelineEntries
{
    /// <summary>时间线展示上限（超出截断，最新优先保留）。</summary>
    public const int MaxEntries = 8;

    /// <summary>记录 → 时间线条目（新在前）。</summary>
    public static IReadOnlyList<TimelineEntry> Of(IReadOnlyList<NotificationRecord> records, int max = MaxEntries)
    {
        var entries = new List<TimelineEntry>(Math.Min(records.Count, Math.Max(max, 0)));
        for (var index = records.Count - 1; index >= 0 && entries.Count < max; index--)
        {
            var record = records[index];
            entries.Add(new TimelineEntry(
                record.Timestamp.ToLocalTime().ToString("HH:mm"),
                record.Severity,
                record.Title,
                record.Read));
        }
        return entries;
    }
}

/// <summary>时间线单条目（本地化时刻 · 级别 · 摘要 · 已读位）。</summary>
public sealed record TimelineEntry(string Time, Severity Severity, string Title, bool Read);
