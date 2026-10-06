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

        public WidgetHost CreateHost(RefreshScheduler scheduler)
        {
            Config.ConnectionItems.Add(TestData.Connection());
            Config.WidgetItems.Add(TestData.Widget());
            var host = new WidgetHost(
                Bus, Clock, scheduler, Cache, Config, new InMemorySecretStore(),
                new FakeResolver(new Dictionary<string, IWidgetProvider> { ["test.type"] = Provider }));
            Bus.Subscribe<WidgetStateChanged>(States.Add);
            Bus.Subscribe<ConnectionHealthChanged>(Healths.Add);
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
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult(expected);

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        var stateEvent = Assert.Single(fixture.States);
        Assert.Equal(expected, stateEvent.State);
        var health = Assert.Single(fixture.Healths);
        Assert.Equal(ConnectionHealthState.Healthy, health.State);
        Assert.Same(expected, fixture.Cache.LoadStates("conn-1")["w-1"]);
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
        Assert.Equal("Unable to refresh", state.Summary);
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
        await Poll.UntilAsync(() => fixture.Healths.Any(h => h.State == ConnectionHealthState.Offline)
                                     && delayer.Calls.Count >= 2);

        // 两个 widget 同连接失败 → Offline 健康事件按连接去重，只发一次
        Assert.Single(fixture.Healths);
        // 整组失败 → 调度器进入退避 ×2
        Assert.Equal(TimeSpan.FromSeconds(2), delayer.Calls[1].Requested);

        fixture.Provider.Handler = null; // 恢复
        delayer.Calls[1].Complete();
        await Poll.UntilAsync(() => fixture.Healths.Any(h => h.State == ConnectionHealthState.Healthy));

        var healthy = Assert.Single(fixture.Healths, h => h.State == ConnectionHealthState.Healthy);
        Assert.Equal("conn-1", healthy.ConnectionId);
        // 成功复位 → 下一轮回到基础周期
        await Poll.UntilAsync(() => delayer.Calls.Count >= 3);
        Assert.Equal(TimeSpan.FromSeconds(1), delayer.Calls[2].Requested);
    }
}
