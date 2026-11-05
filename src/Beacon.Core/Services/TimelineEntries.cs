using Beacon.Core.Models;
using Beacon.Core.Selectors;

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
    /// <summary>时间线展示上限（超出截断，最新优先保留）。PANEL-UPGRADE-1：8 → 30，看板大板面滚动承载。</summary>
    public const int MaxEntries = 30;

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

    /// <summary>看板主区事件动态展示上限（PANEL-UPGRADE-1）：与面板内存流上限分头——
    /// 窗口内存留事件壳，展示按此截；大板面行高下 50 条滚动可读。</summary>
    public const int BoardMaxEntries = 50;

    /// <summary>
    /// 看板事件动态（PANEL-UPGRADE-1 主区，纯函数 Linux 可单测）：来源过滤（QuickPanelFilter 同口径）、
    /// 按时刻降序（输入顺序不作数——面板内存流已新在前，但排序规则收口这里不再重复约定）、
    /// 截到 <paramref name="max"/> 条。行面 时刻/级别/来源/摘要 逐行可读。
    /// </summary>
    public static IReadOnlyList<BoardTimelineEntry> Board(
        IReadOnlyList<BoardEvent> events,
        string? activeSource = null,
        int max = BoardMaxEntries)
        => events
            .Where(evt => QuickPanelFilter.Matches(activeSource, evt.State.WidgetType))
            .OrderByDescending(evt => evt.At)
            .Take(Math.Max(max, 0))
            .Select(evt => new BoardTimelineEntry(
                evt.At.ToLocalTime().ToString("HH:mm:ss"),
                evt.State.Severity,
                evt.Source,
                evt.State.Summary,
                evt.State.WidgetId,
                evt.State))
            .ToList();
}

/// <summary>时间线单条目（本地化时刻 · 级别 · 摘要 · 已读位）。</summary>
public sealed record TimelineEntry(string Time, Severity Severity, string Title, bool Read);

/// <summary>看板事件动态一次状态变化（事件时刻 + 当时快照 + 来源键，来源键 QuickPanelFilter 同口径）。</summary>
public sealed record BoardEvent(DateTimeOffset At, WidgetState State, string Source);

/// <summary>看板事件动态单行（本地化时刻 · 级别 · 来源 · 摘要 · 归属组件 · 事件时点快照——RECENT 行保留事件时点语义）。</summary>
public sealed record BoardTimelineEntry(string Time, Severity Severity, string Source, string Summary, string WidgetId, WidgetState State);
