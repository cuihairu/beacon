using Beacon.Core.Events;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-207 验收：失败→受影响 Widget 转陈旧态（灰灯+Last update），恢复自动复位，任何错误不产生未捕获异常。</summary>
public sealed class WidgetHostTests
{
    private sealed class HostFixture
    {
        public EventBus Bus { get; } = new();
        public InMemoryConfigStore Config { get; } = new();
        public InMemoryCacheStore Cache { get; } = new();
        public FakeClock Clock { get; } = new();
        public FakeWidgetProvider Provider { get; } = new()
        {
            DescriptorValue = new WidgetTypeDescriptor { Type = "test.type", DisplayName = "Test" },
        };
        public List<WidgetStateChanged> States { get; } = [];
        public List<ConnectionHealthChanged> Healths { get; } = [];
        private readonly object _gate = new();

        /// <summary>后台刷新循环并发追加事件，轮询断言须用快照枚举（避免 Collection was modified）。</summary>
        public WidgetStateChanged[] StatesSnapshot()
        {
            lock (_gate)
            {
                return [.. States];
            }
        }

        public ConnectionHealthChanged[] HealthsSnapshot()
        {
            lock (_gate)
            {
                return [.. Healths];
            }
        }

        public WidgetHost CreateHost(RefreshScheduler scheduler, IStateStore? stateStore = null)
        {
            Config.ConnectionItems.Add(TestData.Connection());
            Config.WidgetItems.Add(TestData.Widget());
            var host = new WidgetHost(
                Bus, Clock, scheduler, Cache, Config, new InMemorySecretStore(),
                new FakeResolver(new Dictionary<string, IWidgetProvider> { ["test.type"] = Provider }),
                stateStore: stateStore);
            Bus.Subscribe<WidgetStateChanged>(evt =>
            {
                lock (_gate)
                {
                    States.Add(evt);
                }
            });
            Bus.Subscribe<ConnectionHealthChanged>(evt =>
            {
                lock (_gate)
                {
                    Healths.Add(evt);
                }
            });
            return host;
        }
    }

    [Fact]
    public async Task RefreshSuccess_PublishesState_Health_AndCaches()
    {
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        var expected = TestData.State(severity: Severity.Warning, summary: "3 PRs");
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(expected);

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        var stateEvent = Assert.Single(fixture.States);
        Assert.Equal(expected, stateEvent.State);
        var health = Assert.Single(fixture.Healths);
        Assert.Equal(ConnectionHealthState.Healthy, health.State);
        Assert.Same(expected, fixture.Cache.LoadStates("conn-1")["w-1"]);
    }

    [Fact]
    public async Task RefreshSuccess_PublishesThroughStore_SnapshotUpdated()
    {
        // 发布点经 StateStore（Phase 1）：总线事件契约不变，快照同步落 Store
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        var expected = TestData.State(severity: Severity.Info, summary: "5 PRs");
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(expected);

        await host.RefreshWidgetAsync("w-1");

        var stateEvent = Assert.Single(fixture.States);
        Assert.Same(expected, stateEvent.State);
        Assert.Same(expected, store.Get("w-1"));
    }

    [Fact]
    public async Task RefreshTwice_IdenticalContent_SuppressStore_PublishesOnce()
    {
        // 去重开关在真实管线中的行为面（映射与迁移 §6.2 拍板项的前置取证）
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus, suppressUnchanged: true);
        await using var host = fixture.CreateHost(scheduler, store);
        var state = TestData.State(summary: "same");
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(state);

        await host.RefreshWidgetAsync("w-1");
        await host.RefreshWidgetAsync("w-1");

        Assert.Single(fixture.States); // 内容未变：第二次不重发
        Assert.Same(state, store.Get("w-1"));
    }

    [Fact]
    public async Task ConnectionException_MarksCachedStateStale_AndPublishesOffline()
    {
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Cache.SaveState("conn-1", TestData.State(summary: "cached"));
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("network down", ConnectionHealthState.Offline);

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.False(ok);
        var health = Assert.Single(fixture.Healths);
        Assert.Equal(ConnectionHealthState.Offline, health.State);
        var state = Assert.Single(fixture.States).State;
        Assert.True(state.IsStale);
        Assert.False(state.ConnectionHealthy);
        Assert.Equal("cached", state.Summary); // 显示 Last update，不甩异常
    }

    [Fact]
    public async Task ConnectionException_CachedSuccess_EscalatesSeverityToWarning()
    {
        // 连接降级兜底（builtin.connection-degraded）依赖 severity ≥ Warning：
        // 缓存命中路径若沿用 Success，「有数据后断网」的真降级永不开火（CI 场景 F 实证）
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Cache.SaveState("conn-1", TestData.State(severity: Severity.Success, summary: "cached"));
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("network down", ConnectionHealthState.Offline);

        await host.RefreshWidgetAsync("w-1");

        var state = Assert.Single(fixture.States).State;
        Assert.True(state.IsStale);
        Assert.False(state.ConnectionHealthy);
        Assert.Equal(Severity.Warning, state.Severity); // 抬到 Warning，通知规则可开火
        Assert.Equal("cached", state.Summary);
    }

    [Fact]
    public async Task ConnectionException_CachedError_SeverityPreserved()
    {
        // 已是 Warning 以上的缓存只陈旧不降级也不升级
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Cache.SaveState("conn-1", TestData.State(severity: Severity.Error, summary: "cached"));
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("network down", ConnectionHealthState.Offline);

        await host.RefreshWidgetAsync("w-1");

        var state = Assert.Single(fixture.States).State;
        Assert.True(state.IsStale);
        Assert.Equal(Severity.Error, state.Severity);
    }

    [Fact]
    public async Task OfflineThenRecovery_StaleAnnotationPreserved_FreshReplaces()
    {
        // B-206 全周期：断网 → 陈旧态重发布（Last update = 原 FetchedAt 不丢）→ 恢复 → 自动刷新清标注
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        var cached = TestData.State(severity: Severity.Info, summary: "cached"); // FetchedAt=2026-01-01T00:00:00Z
        fixture.Cache.SaveState("conn-1", cached);
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("network down", ConnectionHealthState.Offline);

        await host.RefreshWidgetAsync("w-1");

        var stale = Assert.Single(fixture.States).State;
        Assert.True(stale.IsStale);
        Assert.False(stale.ConnectionHealthy);
        Assert.Equal(cached.FetchedAt, stale.FetchedAt); // 标注 Last update 用原值，不刷新成失败时刻
        Assert.Equal("cached", stale.Summary);

        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(TestData.State(summary: "fresh"));
        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        var fresh = fixture.States[^1].State;
        Assert.False(fresh.IsStale);
        Assert.True(fresh.ConnectionHealthy);
        Assert.Equal("fresh", fresh.Summary);
        Assert.Equal("fresh", fixture.Cache.LoadStates("conn-1")["w-1"].Summary); // 缓存同步替换
    }

    [Fact]
    public async Task ConnectionException_NoCache_PublishesSyntheticWarning()
    {
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("down", ConnectionHealthState.Offline);

        await host.RefreshWidgetAsync("w-1");

        var state = Assert.Single(fixture.States).State;
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("拉取失败：down", state.Summary); // 首错直显（原 "Unable to refresh" 零信息）
        Assert.True(state.IsStale);
    }

    [Fact]
    public async Task UnauthorizedException_PublishesUnauthorizedHealth()
    {
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("bad token", ConnectionHealthState.Unauthorized);

        await host.RefreshWidgetAsync("w-1");

        Assert.Equal(ConnectionHealthState.Unauthorized, Assert.Single(fixture.Healths).State);
    }

    [Fact]
    public async Task UnexpectedError_PublishesDegradedHealth_NoCrash()
    {
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (_, _, _, _) => throw new InvalidOperationException("bug");

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.False(ok);
        Assert.Equal(ConnectionHealthState.Degraded, Assert.Single(fixture.Healths).State);
        Assert.True(Assert.Single(fixture.States).State.IsStale);
    }

    [Fact]
    public async Task NullState_ConditionalNotModified_SkipsPublishAndCache()
    {
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Cache.SaveState("conn-1", TestData.State(summary: "previous"));
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(null); // ETag 304

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        Assert.Empty(fixture.States); // 未重发
        Assert.Equal("previous", fixture.Cache.LoadStates("conn-1")["w-1"].Summary); // 缓存未重写
        Assert.Equal(ConnectionHealthState.Healthy, Assert.Single(fixture.Healths).State);
    }

    [Fact]
    public async Task MissingWidgetOrProvider_SkipsSilently()
    {
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);

        Assert.True(await host.RefreshWidgetAsync("ghost"));       // 配置里不存在
        fixture.Config.WidgetItems.Clear();
        Assert.True(await host.RefreshWidgetAsync("w-1"));         // provider 未注册

        Assert.Empty(fixture.States);
        Assert.Empty(fixture.Healths);
    }

    [Fact]
    public async Task GroupAllFail_TriggersBackoff_HealthDeduped_RecoveryResets()
    {
        var delayer = new FakeDelayer();
        var fixture = new HostFixture();
        fixture.Config.WidgetItems.Add(TestData.Widget("w-2")); // 同连接同 tier → 同组
        await using var scheduler = new RefreshScheduler(
            _ => new RefreshTierPolicy(TimeSpan.FromSeconds(1), JitterFraction: 0),
            delayer.Delay,
            randomSeed: 42);
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("down", ConnectionHealthState.Offline);

        host.Start(); // 启动即 kick → 第一轮立刻执行
        await Poll.UntilAsync(() => fixture.HealthsSnapshot().Any(h => h.State == ConnectionHealthState.Offline)
                                     && delayer.Calls.Count >= 2);

        // 两个 widget 同连接失败 → Offline 健康事件按连接去重，只发一次
        Assert.Single(fixture.HealthsSnapshot());
        // 整组失败 → 调度器进入退避 ×2
        Assert.Equal(TimeSpan.FromSeconds(2), delayer.Calls[1].Requested);

        fixture.Provider.Handler = null; // 恢复
        delayer.Calls[1].Complete();
        await Poll.UntilAsync(() => fixture.HealthsSnapshot().Any(h => h.State == ConnectionHealthState.Healthy));

        var healthy = Assert.Single(fixture.HealthsSnapshot(), h => h.State == ConnectionHealthState.Healthy);
        Assert.Equal("conn-1", healthy.ConnectionId);
        // 成功复位 → 下一轮回到基础周期
        await Poll.UntilAsync(() => delayer.Calls.Count >= 3);
        Assert.Equal(TimeSpan.FromSeconds(1), delayer.Calls[2].Requested);
    }

    [Fact]
    public async Task RefreshCompletingAfterWidgetDeleted_DropsLateResult()
    {
        // P1-01 晚到保护：取数 await 期间组件被删——缓存/状态/健康一律不写回（幽灵快照不得复活）
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        var expected = TestData.State(severity: Severity.Info, summary: "late arrival");
        var release = new TaskCompletionSource();
        fixture.Provider.Handler = async (_, _, _, _) =>
        {
            await release.Task;
            return (WidgetState?)expected;
        };

        var refresh = host.RefreshWidgetAsync("w-1"); // 停在 Provider await：模拟在途拉取
        fixture.Config.WidgetItems.RemoveAll(w => w.Id == "w-1");
        release.SetResult();
        var ok = await refresh;

        Assert.True(ok); // 跳过不当作失败（组退避不受牵连）
        Assert.Null(store.Get("w-1"));
        Assert.Empty(fixture.Cache.LoadStates("conn-1"));
        Assert.Empty(fixture.StatesSnapshot());
        Assert.Empty(fixture.HealthsSnapshot());
    }

    [Fact]
    public async Task RefreshCompletingAfterConnectionDeleted_DropsLateResult()
    {
        // P1-01 晚到保护（连接消失分支）：同上，故障路径的陈旧重发布也不得复活
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        var release = new TaskCompletionSource();
        fixture.Provider.Handler = async (_, _, _, _) =>
        {
            await release.Task;
            throw new ConnectionException("late failure", ConnectionHealthState.Offline);
        };

        var refresh = host.RefreshWidgetAsync("w-1");
        fixture.Config.ConnectionItems.Clear();
        release.SetResult();
        var ok = await refresh;

        Assert.True(ok);
        Assert.Null(store.Get("w-1"));
        Assert.Empty(fixture.StatesSnapshot());
        Assert.Empty(fixture.HealthsSnapshot());
    }

    [Fact]
    public async Task RefreshCompletingAfterConnectionDisabled_DropsLateResult()
    {
        // P1-01 晚到保护（停用分支）：Enabled=false 的连接晚到结果不写回（SetConnectionTypeEnabled 同型）
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        var expected = TestData.State(severity: Severity.Info, summary: "late arrival");
        var release = new TaskCompletionSource();
        fixture.Provider.Handler = async (_, _, _, _) =>
        {
            await release.Task;
            return (WidgetState?)expected;
        };

        var refresh = host.RefreshWidgetAsync("w-1");
        fixture.Config.ConnectionItems[0] = new ConnectionConfig
        {
            Id = "conn-1",
            Type = "github",
            Endpoint = "https://example.invalid",
            Enabled = false,
        };
        release.SetResult();
        var ok = await refresh;

        Assert.True(ok);
        Assert.Null(store.Get("w-1"));
        Assert.Empty(fixture.StatesSnapshot());
    }

    [Fact]
    public async Task WidgetsInvalidated_ClearsStoreSnapshot()
    {
        // P1-01 接线：删除/停用发布的 WidgetsInvalidated 同步清 Store（原先仅聚合器/面板订阅）
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(TestData.State());
        await host.RefreshWidgetAsync("w-1");
        Assert.NotNull(store.Get("w-1"));

        fixture.Bus.Publish(new WidgetsInvalidated(["w-1"]));

        Assert.Null(store.Get("w-1"));
    }

    [Fact]
    public async Task NullStateAfterFailure_LiftsStaleFlags_ContentFrozen()
    {
        // P1-01 新鲜度恢复：失败落陈旧视图后 304（未变化）——连接已健康，灰显必须抬起且内容全冻结
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        var fresh = TestData.State(severity: Severity.Success, summary: "3 PRs");
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(fresh);
        await host.RefreshWidgetAsync("w-1");

        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("down", ConnectionHealthState.Offline);
        await host.RefreshWidgetAsync("w-1");
        Assert.True(store.Get("w-1")!.IsStale); // 前置：失败确实落下陈旧视图

        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(null); // 304：未变化
        var before = fixture.StatesSnapshot().Length;
        await host.RefreshWidgetAsync("w-1");

        var recovered = store.Get("w-1")!;
        Assert.False(recovered.IsStale);
        Assert.True(recovered.ConnectionHealthy);
        Assert.Equal(fresh.FetchedAt, recovered.FetchedAt);        // 内容冻结：恢复不刷时间戳
        Assert.Equal(fresh.Summary, recovered.Summary);
        Assert.Equal(Severity.Success, recovered.Severity);        // 失败期的 Warning 抬升一并复位
        Assert.Equal(before + 1, fixture.StatesSnapshot().Length); // 健康翻转穿过去重发布一次
    }

    [Fact]
    public async Task Start_HydratesStoreFromCache_OfflineShowsOldState()
    {
        // P1-01 启动水合：磁盘缓存 → Store 先于首轮拉取——离线启动不等网络即亮旧数据与时间
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        var cachedState = TestData.State(severity: Severity.Warning, summary: "cached PRs");
        fixture.Cache.SaveState("conn-1", cachedState);
        var block = new TaskCompletionSource(); // 首轮拉取悬停：水合断言不受后台刷新竞态干扰
        fixture.Provider.Handler = async (_, _, _, _) =>
        {
            await block.Task;
            return (WidgetState?)null;
        };

        host.Start();
        try
        {
            var hydrated = store.Get("w-1");
            Assert.NotNull(hydrated);
            Assert.True(hydrated!.IsStale); // 旧数据诚实标陈旧（灰显 + Last update 时间可见）
            Assert.Equal("cached PRs", hydrated.Summary);
            Assert.Equal(cachedState.FetchedAt, hydrated.FetchedAt);
        }
        finally
        {
            block.SetResult();
        }
    }

    [Fact]
    public async Task Start_WithoutCache_LeavesStoreEmpty()
    {
        // 水合只补缓存已有的组件：净态启动不造空壳状态（首拉前 tile 空白属既有契约）
        var fixture = new HostFixture();
        await using var scheduler = new RefreshScheduler();
        var store = new StateStore(fixture.Bus);
        await using var host = fixture.CreateHost(scheduler, store);
        var block = new TaskCompletionSource();
        fixture.Provider.Handler = async (_, _, _, _) =>
        {
            await block.Task;
            return (WidgetState?)null;
        };

        host.Start();
        try
        {
            Assert.Null(store.Get("w-1"));
        }
        finally
        {
            block.SetResult();
        }
    }
}
