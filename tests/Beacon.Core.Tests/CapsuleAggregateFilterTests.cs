using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 胶囊/岛消费面接入组件中心（ISLAND-RING-1 扩围①，2026-11-07 用户令）：此前 hiddenComponents
/// 只被设置页/浮动 tile/快捷面板消费，岛零命中——勾掉的组件仍在给岛点灯/计徽标/撑进度环。
/// 行为锁：组件中心勾掉某组件 → 其状态不进岛的聚合（级别/计数/进度份额/预览/离线横幅）；
/// 没有任何组件状态可判时原样透传（聚合器口径不回退）。持久化走同一 config（HiddenComponents）。
/// </summary>
public sealed class CapsuleAggregateFilterTests
{
    private static WidgetState State(string id, Severity severity, string connectionId = "conn-1", bool healthy = true)
        => new()
        {
            WidgetId = id,
            WidgetType = "github.pull_requests",
            ConnectionId = connectionId,
            Severity = severity,
            Summary = $"{id} summary",
            ConnectionHealthy = healthy,
        };

    private static AggregateStatusChanged Aggregate(
        Severity overall,
        Dictionary<Severity, int> counts,
        int offline = 0)
        => new(overall, counts, offline, DateTimeOffset.UtcNow);

    private static AppConfig WithHidden(params string[] ids)
    {
        var app = new AppConfig();
        app.HiddenComponents.AddRange(ids);
        return app;
    }

    // —— 透传面：没有任何组件状态可判时不改变聚合器口径 ——

    [Fact]
    public void Filter_NoStatesKnown_PassesThrough()
    {
        var aggregate = Aggregate(Severity.Warning, new Dictionary<Severity, int> { [Severity.Warning] = 2 }, offline: 1);
        var filtered = CapsuleAggregateFilter.Filter(WithHidden("w-1"), aggregate, []);
        Assert.Same(aggregate, filtered); // 透传（同实例）：无状态可判不造数
    }

    // —— 勾掉的组件不进岛的级别/计数 ——

    [Fact]
    public void Filter_HiddenErrorWidget_ExcludedFromCountsAndOverall()
    {
        var aggregate = Aggregate(
            Severity.Error,
            new Dictionary<Severity, int> { [Severity.Success] = 1, [Severity.Error] = 1 });
        var filtered = CapsuleAggregateFilter.Filter(
            WithHidden("w-err"), aggregate,
            [State("w-ok", Severity.Success), State("w-err", Severity.Error)]);
        Assert.Equal(Severity.Success, filtered.Overall); // 唯一 error 源被勾掉 → 灯回绿
        Assert.Equal(1, filtered.Counts[Severity.Success]);
        Assert.Equal(0, filtered.Counts[Severity.Error]);
    }

    [Fact]
    public void Filter_VisibleWidgets_KeepTheirSeverity()
    {
        var aggregate = Aggregate(
            Severity.Warning,
            new Dictionary<Severity, int> { [Severity.Success] = 1, [Severity.Warning] = 1 });
        var filtered = CapsuleAggregateFilter.Filter(
            new AppConfig(), aggregate,
            [State("w-ok", Severity.Success), State("w-warn", Severity.Warning)]);
        Assert.Equal(Severity.Warning, filtered.Overall);
        Assert.Equal(1, filtered.Counts[Severity.Warning]);
    }

    [Fact]
    public void Filter_AllHidden_ZeroCountsAndSuccessBaseline()
    {
        var aggregate = Aggregate(
            Severity.Critical,
            new Dictionary<Severity, int> { [Severity.Critical] = 2 }, offline: 3);
        var filtered = CapsuleAggregateFilter.Filter(
            WithHidden("w-a", "w-b"), aggregate,
            [State("w-a", Severity.Critical, "conn-1", healthy: false), State("w-b", Severity.Critical, "conn-1", healthy: false)]);
        Assert.Equal(Severity.Success, filtered.Overall); // 空集=正常基线（StatusAggregator 同口径）
        Assert.Equal(0, filtered.Counts[Severity.Critical]);
        Assert.Equal(0, filtered.OfflineConnections); // 全勾掉 → 离线横幅也熄
    }

    [Fact]
    public void Filter_CountsAlwaysCoverAllSeverities()
    {
        var aggregate = Aggregate(Severity.Success, new Dictionary<Severity, int> { [Severity.Success] = 1 });
        var filtered = CapsuleAggregateFilter.Filter(
            new AppConfig(), aggregate, [State("w-ok", Severity.Success)]);
        foreach (Severity severity in Enum.GetValues<Severity>())
        {
            Assert.True(filtered.Counts.ContainsKey(severity)); // 消费方 GetValueOrDefault 依赖全键在册
        }
    }

    // —— 离线横幅：只数仍持有可见组件的离线连接 ——

    [Fact]
    public void Filter_OfflineConnectionWithoutVisibleWidgets_NotCounted()
    {
        var aggregate = Aggregate(Severity.Success, new Dictionary<Severity, int>(), offline: 2);
        var filtered = CapsuleAggregateFilter.Filter(
            WithHidden("w-a"), aggregate,
        [
            State("w-a", Severity.Success, "conn-dead", healthy: false), // 唯一组件被勾掉
            State("w-b", Severity.Success, "conn-alive", healthy: false),
        ]);
        Assert.Equal(1, filtered.OfflineConnections); // conn-dead 不再点亮横幅
    }

    [Fact]
    public void Filter_OfflineConnection_CountedOnceAcrossItsWidgets()
    {
        var aggregate = Aggregate(Severity.Success, new Dictionary<Severity, int>(), offline: 1);
        var filtered = CapsuleAggregateFilter.Filter(
            new AppConfig(), aggregate,
        [
            State("w-a", Severity.Success, "conn-dead", healthy: false),
            State("w-b", Severity.Success, "conn-dead", healthy: false), // 同连接第二组件
        ]);
        Assert.Equal(1, filtered.OfflineConnections); // 按连接去重（聚合器 _unhealthyConnections.Count 同口径）
    }
}
