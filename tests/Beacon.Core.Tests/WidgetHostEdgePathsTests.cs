using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// WidgetHost 管线边缘路径收口（Batch 5）：缺 widget/缺连接静默跳过、无 provider 跳过、
/// 非连接异常（InvalidOperationException）降级 Degraded + 首错直显陈旧态。不触碰成功/连接异常主路径。
/// </summary>
public sealed class WidgetHostEdgePathsTests
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

        public WidgetHost CreateHost()
        {
            var host = new WidgetHost(
                Bus, Clock, new RefreshScheduler(), Cache, Config, new InMemorySecretStore(),
                new FakeResolver(new Dictionary<string, IWidgetProvider> { ["test.type"] = Provider }));
            Bus.Subscribe<WidgetStateChanged>(evt => { lock (_gate) States.Add(evt); });
            Bus.Subscribe<ConnectionHealthChanged>(evt => { lock (_gate) Healths.Add(evt); });
            return host;
        }
    }

    [Fact]
    public async Task Refresh_MissingWidget_SkipsSilently()
    {
        var fixture = new Fixture();
        await using var host = fixture.CreateHost();

        var ok = await host.RefreshWidgetAsync("ghost");

        Assert.True(ok);
        Assert.Empty(fixture.StatesSnapshot());
        Assert.Empty(fixture.HealthsSnapshot());
    }

    [Fact]
    public async Task Refresh_MissingConnection_SkipsSilently()
    {
        var fixture = new Fixture();
        fixture.Config.WidgetItems.Add(TestData.Widget(id: "w-orphan", type: "test.type", connectionId: "no-such-conn"));
        await using var host = fixture.CreateHost();

        var ok = await host.RefreshWidgetAsync("w-orphan");

        Assert.True(ok);
        Assert.Empty(fixture.StatesSnapshot());
        Assert.Empty(fixture.HealthsSnapshot());
    }

    [Fact]
    public async Task Refresh_NoProvider_SkipsSilently()
    {
        var fixture = new Fixture();
        fixture.Config.WidgetItems.Add(TestData.Widget(id: "w-untyped", type: "ghost.type"));
        await using var host = fixture.CreateHost();

        var ok = await host.RefreshWidgetAsync("w-untyped");

        Assert.True(ok);
        Assert.Empty(fixture.StatesSnapshot());
        Assert.Empty(fixture.HealthsSnapshot());
    }

    [Fact]
    public async Task Refresh_UnexpectedException_DegradesAndPublishesStale()
    {
        var fixture = new Fixture();
        fixture.Config.ConnectionItems.Add(TestData.Connection());
        fixture.Config.WidgetItems.Add(TestData.Widget());
        fixture.Provider.Handler = (_, _, _, _) => throw new InvalidOperationException("boom");
        await using var host = fixture.CreateHost();

        var ok = await host.RefreshWidgetAsync("w-1");

        Assert.False(ok);
        Assert.Contains(fixture.HealthsSnapshot(), h => h.State == ConnectionHealthState.Degraded);
        var stale = Assert.Single(fixture.StatesSnapshot());
        Assert.True(stale.State.IsStale);
        Assert.False(stale.State.ConnectionHealthy);
        Assert.Contains("boom", stale.State.Summary);
    }
}
