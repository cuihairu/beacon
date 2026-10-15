using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 1 State Store（redesign-plan §4.1）：快照读取 + 内容级变更检测。
/// 去重默认关（现行为：每次发布）；开启后 FetchedAt 不参与内容比较，
/// IsStale/ConnectionHealthy 翻转仍发布（场景 F5：降级事件不得被吞）。
/// </summary>
public sealed class StateStoreTests
{
    private sealed class StoreFixture
    {
        public EventBus Bus { get; } = new();
        public List<WidgetStateChanged> Published { get; } = [];

        public StateStore CreateStore(bool suppressUnchanged = false)
        {
            var store = new StateStore(Bus, suppressUnchanged);
            Bus.Subscribe<WidgetStateChanged>(evt => Published.Add(evt));
            return store;
        }
    }

    private static WidgetState State(
        string widgetId = "w-1",
        Severity severity = Severity.Success,
        string summary = "OK",
        DateTimeOffset? fetchedAt = null,
        bool isStale = false,
        bool connectionHealthy = true,
        IReadOnlyDictionary<string, string>? payload = null)
        => new()
        {
            WidgetId = widgetId,
            WidgetType = "test.type",
            ConnectionId = "conn-1",
            Severity = severity,
            Summary = summary,
            Progress = 0.5,
            FetchedAt = fetchedAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsStale = isStale,
            ConnectionHealthy = connectionHealthy,
            Payload = payload ?? new Dictionary<string, string>(),
        };

    [Fact]
    public void Update_PublishesStateChangedOnBus()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore();
        var state = State();

        store.Update(state);

        var published = Assert.Single(fixture.Published);
        Assert.Same(state, published.State);
    }

    [Fact]
    public void Snapshot_KeepsLatestPerWidget_GetReturnsIt()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore();
        var first = State(summary: "first");
        var second = State(summary: "second", fetchedAt: first.FetchedAt.AddMinutes(1));

        store.Update(first);
        store.Update(second);

        Assert.Same(second, store.Get("w-1"));
        var snapshot = store.Snapshot();
        Assert.Single(snapshot);
        Assert.Same(second, snapshot["w-1"]);
    }

    [Fact]
    public void Get_UnknownWidgetId_ReturnsNull()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore();

        Assert.Null(store.Get("ghost"));
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void SuppressUnchangedFalse_IdenticalContent_PublishesEachTime()
    {
        // 回退开关开直通（旧现行为锚点）：内容完全相同也逐次发布
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: false);

        store.Update(State());
        store.Update(State(fetchedAt: DateTimeOffset.UtcNow));

        Assert.Equal(2, fixture.Published.Count);
    }

    [Fact]
    public void SuppressUnchangedDefaultsOn_IdenticalContent_PublishesOnce()
    {
        // 契约默认（已代决②）：不传开关即去重
        var fixture = new StoreFixture();
        var store = new StateStore(fixture.Bus);
        fixture.Bus.Subscribe<WidgetStateChanged>(evt => fixture.Published.Add(evt));

        store.Update(State());
        store.Update(State(fetchedAt: DateTimeOffset.UtcNow));

        Assert.Single(fixture.Published);
    }

    [Fact]
    public void DedupOn_OnlyFetchedAtChanged_PublishesOnce_SnapshotKeepsLatest()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);
        var first = State();
        var second = State(fetchedAt: first.FetchedAt.AddMinutes(1));

        store.Update(first);
        store.Update(second);

        var published = Assert.Single(fixture.Published);
        Assert.Same(first, published.State);
        Assert.Same(second, store.Get("w-1")); // 快照仍存最新（FetchedAt 保新）
    }

    [Fact]
    public void DedupOn_SeverityChange_Republishes()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);

        store.Update(State());
        store.Update(State(severity: Severity.Error));

        Assert.Equal(2, fixture.Published.Count);
    }

    [Fact]
    public void DedupOn_StaleFlip_Republishes()
    {
        // 场景 F5 前置：断网降级的陈旧事件不得被去重吞掉
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);

        store.Update(State());
        store.Update(State(isStale: true, connectionHealthy: false));

        Assert.Equal(2, fixture.Published.Count);
        Assert.True(fixture.Published[1].State.IsStale);
    }

    [Fact]
    public void DedupOn_ConnectionHealthyFlip_Republishes()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);

        store.Update(State());
        store.Update(State(connectionHealthy: false));

        Assert.Equal(2, fixture.Published.Count);
    }

    [Fact]
    public void DedupOn_PayloadInsertionOrderIrrelevant_Suppressed()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);

        store.Update(State(payload: new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }));
        store.Update(State(fetchedAt: DateTimeOffset.UtcNow, payload: new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" }));

        Assert.Single(fixture.Published);
    }

    [Fact]
    public void DedupOn_PayloadValueChanged_Republishes()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);

        store.Update(State(payload: new Dictionary<string, string> { ["a"] = "1" }));
        store.Update(State(payload: new Dictionary<string, string> { ["a"] = "2" }));

        Assert.Equal(2, fixture.Published.Count);
    }

    [Fact]
    public void Invalidate_RemovesSnapshotWithoutPublishing_AllowsFreshContentKey()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);
        store.Update(State());
        var before = fixture.Published.Count;

        store.Invalidate(["w-1"]);

        Assert.Null(store.Get("w-1"));
        Assert.Equal(before, fixture.Published.Count); // 失效本身不发布

        store.Update(State()); // 失效后内容键复位：再写同内容仍发布（模块重启用）
        Assert.Equal(before + 1, fixture.Published.Count);
    }

    [Fact]
    public void MultipleWidgets_TrackedIndependently()
    {
        var fixture = new StoreFixture();
        var store = fixture.CreateStore(suppressUnchanged: true);

        store.Update(State(widgetId: "w-1"));
        store.Update(State(widgetId: "w-1")); // 内容未变：抑制
        store.Update(State(widgetId: "w-2")); // 另一组件不受影响

        Assert.Equal(2, fixture.Published.Count);
        Assert.Equal(2, store.Snapshot().Count);
    }

    [Fact]
    public void ContentKey_SeparatorCollisionSafe_SegmentsDistinguished()
    {
        // 长度前缀防分隔符歧义：Summary "a|b" + DetailUrl "c" ≠ Summary "a" + DetailUrl "b|c"
        var left = State(summary: "a|b") with { DetailUrl = "c" };
        var right = State(summary: "a") with { DetailUrl = "b|c" };

        Assert.NotEqual(StateStore.ContentKey(left), StateStore.ContentKey(right));
    }
}
