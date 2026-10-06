using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-205 验收：overall = max severity 表驱动全组合；空集 = Success；变化才发布。</summary>
public sealed class StatusAggregatorTests
{
    public static TheoryData<Severity[], Severity> MaxSeverityCases => new()
    {
        { [Severity.Success], Severity.Success },
        { [Severity.Success, Severity.Info], Severity.Info },
        { [Severity.Info, Severity.Warning], Severity.Warning },
        { [Severity.Warning, Severity.Error], Severity.Error },
        { [Severity.Error, Severity.Critical], Severity.Critical },
        { [Severity.Success, Severity.Warning, Severity.Error, Severity.Critical], Severity.Critical },
    };

    private sealed class AggregateRecorder
    {
        public EventBus Bus { get; } = new();
        public List<AggregateStatusChanged> Events { get; } = [];

        public AggregateRecorder() => Bus.Subscribe<AggregateStatusChanged>(Events.Add);
    }

    private static StatusAggregator CreateWithState(AggregateRecorder recorder, params Severity[] severities)
    {
        var aggregator = new StatusAggregator(recorder.Bus);
        foreach (var (severity, index) in severities.Select((s, i) => (s, i)))
        {
            recorder.Bus.Publish(new WidgetStateChanged(
                TestData.State(widgetId: $"w-{index}", severity: severity)));
        }
        return aggregator;
    }

    [Fact]
    public void EmptySet_IsSuccessBaseline_NoEvents()
    {
        var recorder = new AggregateRecorder();
        using var aggregator = new StatusAggregator(recorder.Bus);

        var snapshot = aggregator.Snapshot();

        Assert.Equal(Severity.Success, snapshot.Overall);
        Assert.Equal(0, snapshot.OfflineConnections);
        Assert.All(Enum.GetValues<Severity>(), severity => Assert.Equal(0, snapshot.Counts[severity]));
        Assert.Empty(recorder.Events); // 构造不发布
    }

    [Theory]
    [MemberData(nameof(MaxSeverityCases))]
    public void Overall_IsMaxSeverity(Severity[] severities, Severity expected)
    {
        var recorder = new AggregateRecorder();
        using var aggregator = CreateWithState(recorder, severities);

        Assert.Equal(expected, aggregator.Snapshot().Overall);
    }

    [Fact]
    public void Counts_IncludeAllFiveSeverities()
    {
        var recorder = new AggregateRecorder();
        using var aggregator = CreateWithState(recorder, Severity.Warning, Severity.Error);

        var counts = aggregator.Snapshot().Counts;

        Assert.Equal(5, counts.Count);
        Assert.Equal(1, counts[Severity.Warning]);
        Assert.Equal(1, counts[Severity.Error]);
        Assert.Equal(0, counts[Severity.Success]);
        Assert.Equal(0, counts[Severity.Info]);
        Assert.Equal(0, counts[Severity.Critical]);
    }

    [Fact]
    public void PublishesOnlyOnChange()
    {
        var recorder = new AggregateRecorder();
        using var aggregator = new StatusAggregator(recorder.Bus);
        var state = TestData.State(severity: Severity.Warning);

        recorder.Bus.Publish(new WidgetStateChanged(state));
        recorder.Bus.Publish(new WidgetStateChanged(state)); // 内容相同 → 去重
        recorder.Bus.Publish(new WidgetStateChanged(state with { Severity = Severity.Error }));

        Assert.Equal(2, recorder.Events.Count);
        Assert.Equal(Severity.Error, recorder.Events[^1].Overall);
    }

    [Fact]
    public void OfflineConnection_Counted_AndDeduped()
    {
        var recorder = new AggregateRecorder();
        using var aggregator = new StatusAggregator(recorder.Bus);

        recorder.Bus.Publish(new ConnectionHealthChanged("conn-1", ConnectionHealthState.Offline, DateTimeOffset.UtcNow));
        recorder.Bus.Publish(new ConnectionHealthChanged("conn-1", ConnectionHealthState.Offline, DateTimeOffset.UtcNow)); // 重复不发布
        recorder.Bus.Publish(new ConnectionHealthChanged("conn-2", ConnectionHealthState.Unauthorized, DateTimeOffset.UtcNow));

        Assert.Equal(2, recorder.Events.Count);
        Assert.Equal(2, recorder.Events[^1].OfflineConnections);

        recorder.Bus.Publish(new ConnectionHealthChanged("conn-1", ConnectionHealthState.Healthy, DateTimeOffset.UtcNow));
        Assert.Equal(3, recorder.Events.Count); // 恢复发布第 3 条
        Assert.Equal(1, recorder.Events[^1].OfflineConnections);
    }

    [Fact]
    public void HealthyWidgetState_ClearsOfflineMarker()
    {
        var recorder = new AggregateRecorder();
        using var aggregator = new StatusAggregator(recorder.Bus);
        recorder.Bus.Publish(new ConnectionHealthChanged("conn-1", ConnectionHealthState.Offline, DateTimeOffset.UtcNow));
        recorder.Events.Clear();

        recorder.Bus.Publish(new WidgetStateChanged(TestData.State() with { ConnectionHealthy = true }));

        Assert.Single(recorder.Events);
        Assert.Equal(0, recorder.Events[0].OfflineConnections);
    }
}
