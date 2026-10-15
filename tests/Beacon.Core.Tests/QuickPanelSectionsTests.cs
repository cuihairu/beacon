using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>批3-补27 拍板④：Quick Panel 三分区映射表（需要关注 / 正在运行 / 其余不单独成区）。</summary>
public class QuickPanelSectionsTests
{
    private static WidgetState State(
        Severity severity = Severity.Success,
        LifecycleState? lifecycle = null,
        bool isStale = false,
        bool healthy = true) => new()
    {
        WidgetId = "w1",
        WidgetType = "github.actions.runs",
        ConnectionId = "c1",
        Severity = severity,
        Summary = "Build Client · Success",
        Lifecycle = lifecycle,
        FetchedAt = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero),
        IsStale = isStale,
        ConnectionHealthy = healthy,
    };

    [Theory]
    [InlineData(Severity.Warning)]
    [InlineData(Severity.Error)]
    [InlineData(Severity.Critical)]
    public void Warning_and_above_need_attention(Severity severity)
    {
        Assert.Equal(QuickPanelSection.NeedsAttention, QuickPanelSections.Resolve(State(severity: severity)));
    }

    [Theory]
    [InlineData(LifecycleState.Running)]
    [InlineData(LifecycleState.Queued)]
    public void Running_and_queued_without_alerts_are_in_progress(LifecycleState lifecycle)
    {
        Assert.Equal(QuickPanelSection.InProgress, QuickPanelSections.Resolve(State(lifecycle: lifecycle)));
    }

    [Theory]
    [InlineData(Severity.Info, LifecycleState.Success)]
    [InlineData(Severity.Success, LifecycleState.Unknown)]
    public void Settled_states_stay_normal(Severity severity, LifecycleState lifecycle)
    {
        Assert.Equal(QuickPanelSection.Normal, QuickPanelSections.Resolve(State(severity: severity, lifecycle: lifecycle)));
    }

    [Fact]
    public void No_lifecycle_stays_normal()
    {
        Assert.Equal(QuickPanelSection.Normal, QuickPanelSections.Resolve(State()));
    }

    [Fact]
    public void Stale_and_offline_need_attention_even_when_green()
    {
        Assert.Equal(QuickPanelSection.NeedsAttention, QuickPanelSections.Resolve(State(isStale: true)));
        Assert.Equal(QuickPanelSection.NeedsAttention, QuickPanelSections.Resolve(State(healthy: false)));
    }

    [Fact]
    public void Attention_wins_over_in_progress()
    {
        // 运行中超时（Warning）按「需要关注」：用户要处理的是超时，不是看进度
        var state = State(severity: Severity.Warning, lifecycle: LifecycleState.Running);
        Assert.Equal(QuickPanelSection.NeedsAttention, QuickPanelSections.Resolve(state));
    }
}
