using Beacon.Core.Selectors;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// PANEL-UPGRADE-1 看板事件动态（TimelineEntries.Board）：来源过滤（QuickPanelFilter 同口径）、
/// 时刻降序、截断保留最新、字段逐行映射。通知时间线 Of 既有口径在 ComponentDataTests。
/// </summary>
public class TimelineEntriesTests
{
    private static WidgetState State(string widgetId, string widgetType, Severity severity, string summary)
        => new()
        {
            WidgetId = widgetId,
            WidgetType = widgetType,
            ConnectionId = "conn-1",
            Severity = severity,
            Summary = summary,
        };

    private static BoardEvent Event(int second, string widgetId, string widgetType, Severity severity, string summary)
        => new(
            new DateTimeOffset(2026, 11, 5, 10, 0, second, TimeSpan.Zero),
            State(widgetId, widgetType, severity, summary),
            QuickPanelFilter.SourceKey(widgetType));

    [Fact]
    public void Board_EmptyEvents_ReturnsEmpty()
        => Assert.Empty(TimelineEntries.Board([]));

    [Fact]
    public void Board_OrdersNewestFirst_RegardlessOfInputOrder()
    {
        BoardEvent[] events =
        [
            Event(30, "w-a", "machine.cpu", Severity.Success, "late"),
            Event(1, "w-a", "machine.cpu", Severity.Warning, "early"),
            Event(15, "w-a", "machine.cpu", Severity.Error, "mid"),
        ];

        var rows = TimelineEntries.Board(events);

        Assert.Equal(["late", "mid", "early"], rows.Select(row => row.Summary).ToList());
    }

    [Fact]
    public void Board_ActiveSource_FiltersOtherSourcesOut()
    {
        BoardEvent[] events =
        [
            Event(1, "w-ci", "github.actions.runs", Severity.Success, "ci ok"),
            Event(2, "w-pr", "github.pull_requests", Severity.Warning, "pr open"),
            Event(3, "w-usage", "bigmodel.usage", Severity.Error, "quota"),
        ];

        var rows = TimelineEntries.Board(events, activeSource: "CI");

        var row = Assert.Single(rows);
        Assert.Equal("ci ok", row.Summary);
        Assert.Equal("CI", row.Source);
    }

    [Fact]
    public void Board_NullSource_ReturnsAll()
    {
        BoardEvent[] events =
        [
            Event(1, "w-ci", "github.actions.runs", Severity.Success, "ci ok"),
            Event(2, "w-usage", "bigmodel.usage", Severity.Warning, "quota"),
        ];

        Assert.Equal(2, TimelineEntries.Board(events).Count);
    }

    [Fact]
    public void Board_CapsKeepingNewest()
    {
        var events = Enumerable.Range(0, 12)
            .Select(second => Event(second, "w-a", "machine.cpu", Severity.Success, $"e{second}"))
            .ToList();

        var rows = TimelineEntries.Board(events, max: 3);

        Assert.Equal(3, rows.Count);
        Assert.Equal(["e11", "e10", "e9"], rows.Select(row => row.Summary).ToList());
    }

    [Fact]
    public void Board_CarriesTimeSeveritySourceSummaryWidget()
    {
        var at = new DateTimeOffset(2026, 11, 5, 10, 30, 45, TimeSpan.Zero);
        var evt = new BoardEvent(at, State("w-1", "bigmodel.usage", Severity.Error, "quota 95%"), "智谱");

        var row = Assert.Single(TimelineEntries.Board([evt]));

        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", row.Time);
        Assert.Equal(at.ToLocalTime().ToString("HH:mm:ss"), row.Time);
        Assert.Equal(Severity.Error, row.Severity);
        Assert.Equal("智谱", row.Source);
        Assert.Equal("quota 95%", row.Summary);
        Assert.Equal("w-1", row.WidgetId);
    }

    [Fact]
    public void BoardLimits_AreBoardRelaxed()
    {
        // PANEL-UPGRADE-1：详情时间线 8 → 30，看板主区 50 条滚动承载
        Assert.Equal(30, TimelineEntries.MaxEntries);
        Assert.Equal(50, TimelineEntries.BoardMaxEntries);
    }
}
