using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// StatusAggregator 失效联动收口（Batch 5）：WidgetsInvalidated 从聚合快照移除对应 widget，
/// 变化才发布（未知 id / 空列表不产生新事件）。
/// </summary>
public sealed class StatusAggregatorInvalidationEdgeTests
{
    private sealed class AggregateRecorder
    {
        public EventBus Bus { get; } = new();
        public List<AggregateStatusChanged> Events { get; } = [];

        public AggregateRecorder() => Bus.Subscribe<AggregateStatusChanged>(Events.Add);
    }

    private static StatusAggregator CreateWithStates(AggregateRecorder recorder)
    {
        var aggregator = new StatusAggregator(recorder.Bus);
        recorder.Bus.Publish(new WidgetStateChanged(TestData.State(widgetId: "w-1", severity: Severity.Warning)));
        recorder.Bus.Publish(new WidgetStateChanged(TestData.State(widgetId: "w-2", severity: Severity.Info)));
        return aggregator;
    }

    [Fact]
    public void Invalidate_RemovesWidgets_AndRepublishes()
    {
        var recorder = new AggregateRecorder();
        using var aggregator = CreateWithStates(recorder);
        Assert.Equal(2, aggregator.Snapshot().Counts.Values.Sum());

        recorder.Bus.Publish(new WidgetsInvalidated(["w-1"]));

        var snapshot = aggregator.Snapshot();
        Assert.Equal(0, snapshot.Counts[Severity.Warning]); // w-1（Warning）已移除
        Assert.Equal(1, snapshot.Counts[Severity.Info]);    // w-2 保留
        Assert.True(recorder.Events.Count >= 1); // 集合变化 → 发布了新聚合事件
    }

    [Fact]
    public void Invalidate_UnknownWidget_IsNoOp_NoPublish()
    {
        var recorder = new AggregateRecorder();
        using var aggregator = CreateWithStates(recorder);
        var before = recorder.Events.Count;

        recorder.Bus.Publish(new WidgetsInvalidated(["ghost"]));
        recorder.Bus.Publish(new WidgetsInvalidated([]));

        Assert.Equal(before, recorder.Events.Count); // 无实际移除 → 去重吞掉
        Assert.Equal(2, aggregator.Snapshot().Counts.Values.Sum());
    }
}
