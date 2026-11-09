using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Beacon.Core.Tests;

/// <summary>
/// WidgetHost 分支补齐（2026-11-09 覆盖率 98% 收口）：晚到结果闸门（成功/连接异常/意外异常三路 drop）、
/// 连接停用静默跳过、状态未变化（304）不发布、健康去重早退、调度注册的全局/组件间隔分支、日志臂。
/// 注：缺 widget/连接/无 provider 及意外异常主臂已由 WidgetHostEdgePathsTests 覆盖。
/// </summary>
public sealed class WidgetHostCoverageTests
{
    private sealed class Fixture
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

        public WidgetStateChanged[] StatesSnapshot() { lock (_gate) return [.. States]; }
        public ConnectionHealthChanged[] HealthsSnapshot() { lock (_gate) return [.. Healths]; }

        public WidgetHost CreateHost(RefreshScheduler scheduler, bool withLogger = false)
        {
            Config.ConnectionItems.Add(TestData.Connection());
            Config.WidgetItems.Add(TestData.Widget());
            var host = new WidgetHost(
                Bus, Clock, scheduler, Cache, Config, new InMemorySecretStore(),
                new FakeResolver(new Dictionary<string, IWidgetProvider> { ["test.type"] = Provider }),
                logger: withLogger ? NullLogger.Instance : null);
            Bus.Subscribe<WidgetStateChanged>(evt => { lock (_gate) States.Add(evt); });
            Bus.Subscribe<ConnectionHealthChanged>(evt => { lock (_gate) Healths.Add(evt); });
            return host;
        }
    }

    [Fact]
    public async Task Refresh_WidgetRemovedDuringAwait_SuccessDropped()
    {
        // L79 晚到结果闸门真臂（成功路）：await 期间组件被删 → 不写回、返回成功
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (widget, _, _, _) =>
        {
            fixture.Config.WidgetItems.RemoveAll(w => w.Id == widget.Id);
            return Task.FromResult<WidgetState?>(TestData.State());
        };

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        Assert.Empty(fixture.StatesSnapshot());
        Assert.Empty(fixture.HealthsSnapshot());
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("disable")]
    public async Task Refresh_ConnectionGoneDuringAwait_ResultDropped(string mode)
    {
        // L85 连接空/停用真臂（成功路）：await 期间连接被删或停用 → 不写回
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (_, _, _, _) =>
        {
            if (mode == "remove")
            {
                fixture.Config.ConnectionItems.RemoveAll(c => c.Id == "conn-1");
            }
            else
            {
                fixture.Config.UpsertConnection(new ConnectionConfig { Id = "conn-1", Type = "github", Enabled = false });
            }
            return Task.FromResult<WidgetState?>(TestData.State());
        };

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        Assert.Empty(fixture.StatesSnapshot());
    }

    [Fact]
    public async Task Refresh_DisabledConnection_SkipsSilently()
    {
        // L211 连接停用真臂：模块停用静默跳过，不调 provider、不发状态
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Config.UpsertConnection(new ConnectionConfig { Id = "conn-1", Type = "github", Enabled = false });
        var called = false;
        fixture.Provider.Handler = (_, _, _, _) =>
        {
            called = true;
            return Task.FromResult<WidgetState?>(TestData.State());
        };

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        Assert.False(called);
        Assert.Empty(fixture.StatesSnapshot());
    }

    [Fact]
    public async Task Refresh_ProviderReturnsNull_PublishesHealthOnly()
    {
        // L226 状态未变化（304）真臂：不发内容、仅健康恢复
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(null);

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        Assert.Empty(fixture.StatesSnapshot());
        var health = Assert.Single(fixture.HealthsSnapshot());
        Assert.Equal(ConnectionHealthState.Healthy, health.State);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("generic")]
    public async Task Refresh_ExceptionWithWidgetRemoved_Dropped(string kind)
    {
        // L249/L260 晚到结果闸门真臂（异常路）：失败同样晚到 → 不复活幽灵陈旧态
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (widget, _, _, _) =>
        {
            fixture.Config.WidgetItems.RemoveAll(w => w.Id == widget.Id);
            return kind == "connection"
                ? throw new ConnectionException("boom", ConnectionHealthState.Offline)
                : throw new InvalidOperationException("boom");
        };

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.True(ok);
        Assert.Empty(fixture.StatesSnapshot());
    }

    [Fact]
    public async Task Refresh_TwiceSameHealth_PublishesHealthOnce()
    {
        // L275 健康去重早退真臂：同连接同状态第二次不再发事件
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(TestData.State());

        await host.RefreshWidgetAsync("w-1");
        await host.RefreshWidgetAsync("w-1");

        Assert.Single(fixture.HealthsSnapshot()); // 第二次 Healthy==Healthy → 早退
    }

    [Fact]
    public async Task Start_WithLogger_LogsAndRegisters()
    {
        // L98 日志臂 + L147 全局检测间隔正数真臂：Start 带 logger 走非空日志路径
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler, withLogger: true);
        fixture.Config.App.PollIntervalSeconds = 30;
        fixture.Provider.Handler = (_, _, _, _) => Task.FromResult<WidgetState?>(TestData.State());

        host.Start();

        await Poll.UntilAsync(() => host.GetState("w-1") is not null); // Start 即 kick 首轮拉取（异步，轮询）
        Assert.NotNull(host.GetState("w-1"));
    }

    // ---- `_logger?.` 非空臂：现有测试全走空 logger，此处逐条日志路径补带 logger 命中 ----

    [Fact]
    public async Task Refresh_WidgetRemovedDuringAwait_WithLogger_LogsDrop()
    {
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler, withLogger: true);
        fixture.Provider.Handler = (widget, _, _, _) =>
        {
            fixture.Config.WidgetItems.RemoveAll(w => w.Id == widget.Id);
            return Task.FromResult<WidgetState?>(TestData.State());
        };

        Assert.True(await host.RefreshWidgetAsync("w-1")); // L81 LogWarning 非空臂
    }

    [Fact]
    public async Task Refresh_ConnectionDisabledDuringAwait_WithLogger_LogsDrop()
    {
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler, withLogger: true);
        fixture.Provider.Handler = (_, _, _, _) =>
        {
            fixture.Config.UpsertConnection(new ConnectionConfig { Id = "conn-1", Type = "github", Enabled = false });
            return Task.FromResult<WidgetState?>(TestData.State());
        };

        Assert.True(await host.RefreshWidgetAsync("w-1")); // L87 LogWarning 非空臂
    }

    [Fact]
    public async Task Refresh_MissingWidget_WithLogger_LogsSkip()
    {
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler, withLogger: true);

        Assert.True(await host.RefreshWidgetAsync("ghost")); // L202 LogWarning 非空臂
    }

    [Fact]
    public async Task Refresh_NoProvider_WithLogger_LogsSkip()
    {
        var fixture = new Fixture();
        fixture.Config.WidgetItems.Add(TestData.Widget(id: "w-untyped", type: "ghost.type"));
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler, withLogger: true);

        Assert.True(await host.RefreshWidgetAsync("w-untyped")); // L206 LogWarning 非空臂
    }

    [Fact]
    public async Task Refresh_ConnectionException_WithLogger_LogsWarning()
    {
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler, withLogger: true);
        fixture.Provider.Handler = (_, _, _, _) =>
            throw new ConnectionException("down", ConnectionHealthState.Offline);

        Assert.False(await host.RefreshWidgetAsync("w-1")); // L253 LogWarning 非空臂
    }

    [Fact]
    public async Task Refresh_UnexpectedException_WithLogger_LogsError()
    {
        var fixture = new Fixture();
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler, withLogger: true);
        fixture.Provider.Handler = (_, _, _, _) => throw new InvalidOperationException("boom");

        Assert.False(await host.RefreshWidgetAsync("w-1")); // L264 LogError 非空臂
    }

    [Fact]
    public async Task Reload_WidgetIntervalOverride_PositiveAndZero()
    {
        // L153 组件级检测间隔真臂（>0）与假臂（<=0）：注册重建不抛
        var fixture = new Fixture();
        fixture.Config.WidgetItems.Add(new WidgetConfig
        {
            Id = "w-ovr",
            Type = "test.type",
            ConnectionId = "conn-1",
            RefreshIntervalSeconds = 120,
        });
        fixture.Config.WidgetItems.Add(new WidgetConfig
        {
            Id = "w-zero",
            Type = "test.type",
            ConnectionId = "conn-1",
            RefreshIntervalSeconds = 0,
        });
        await using var scheduler = new RefreshScheduler();
        await using var host = fixture.CreateHost(scheduler);
        fixture.Provider.Handler = (widget, _, _, _) => Task.FromResult<WidgetState?>(TestData.State(widgetId: widget.Id, summary: widget.Id));

        host.Reload();

        // 各组件按自身 id 回传（StateStore 默认去重同内容，须区分），断言两档间隔组件都被 kick
        Assert.Contains(fixture.StatesSnapshot(), s => s.State.WidgetId == "w-ovr");
        Assert.Contains(fixture.StatesSnapshot(), s => s.State.WidgetId == "w-zero");
    }
}
