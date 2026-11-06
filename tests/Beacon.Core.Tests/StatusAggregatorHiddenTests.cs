using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 岛/胶囊消费面接组件中心（ISLAND-RING-1 扩围①，2026-11-07 用户令）：勾掉的组件不进聚合——
/// 过滤在 StatusAggregator 源头（隐藏集 provider 实时读 config），而非消费端用滞后快照重投影
/// （实机取证 04:12:39：消费端重投影在聚合事件先于组件状态事件到达时把真实 Error 抹成 0 的竞态）。
/// 行为锁：勾选集变化（provider 返回值变化）→ 聚合发布面随之变化；离线横幅只数仍持有可见组件的
/// 离线连接。持久化走同一 config（HiddenComponents）。
/// </summary>
public sealed class StatusAggregatorHiddenTests : IDisposable
{
    private readonly EventBus _bus = new();
    private readonly StatusAggregator _aggregator;

    public StatusAggregatorHiddenTests() => _aggregator = new StatusAggregator(_bus);

    public void Dispose() => _aggregator.Dispose();

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

    private AggregateStatusChanged? _last;
    /// <summary>unhealthy 集由 ConnectionHealthChanged 填充（WidgetState.ConnectionHealthy 只负责摘除）——
    /// 与生产事件序一致：连接降级先报健康事件，组件状态随后。</summary>
    private void PublishStates(params WidgetState[] states)
    {
        foreach (var state in states)
        {
            _bus.Publish(new ConnectionHealthChanged(
                state.ConnectionId,
                state.ConnectionHealthy ? ConnectionHealthState.Healthy : ConnectionHealthState.Degraded,
                DateTimeOffset.UtcNow));
            _bus.Publish(new WidgetStateChanged(state));
        }
    }
    private List<AggregateStatusChanged> Collect()
    {
        var seen = new List<AggregateStatusChanged>();
        _bus.Subscribe<AggregateStatusChanged>(evt => seen.Add(evt));
        return seen;
    }

    [Fact]
    public void HiddenWidget_ExcludedFromCountsAndOverall()
    {
        var hidden = new List<string> { "w-err" };
        _aggregator.SetHiddenComponentsProvider(() => hidden);
        var seen = Collect();
        PublishStates(State("w-ok", Severity.Success));
        PublishStates(State("w-err", Severity.Error));
        var snapshot = seen[^1];
        Assert.Equal(Severity.Success, snapshot.Overall); // 唯一 error 源被勾掉 → 灯回绿
        Assert.Equal(1, snapshot.Counts[Severity.Success]);
        Assert.Equal(0, snapshot.Counts[Severity.Error]);
    }

    [Fact]
    public void VisibleWidgets_KeepTheirSeverity_WithoutProvider()
    {
        var seen = Collect();
        PublishStates(State("w-ok", Severity.Success));
        PublishStates(State("w-warn", Severity.Warning));
        var snapshot = seen[^1];
        Assert.Equal(Severity.Warning, snapshot.Overall); // 未注入 provider = 历史行为零变化
        Assert.Equal(1, snapshot.Counts[Severity.Warning]);
    }

    [Fact]
    public void AllHidden_ZeroCountsSuccessBaselineAndOfflineZero()
    {
        var hidden = new List<string> { "w-a", "w-b" };
        _aggregator.SetHiddenComponentsProvider(() => hidden);
        var seen = Collect();
        PublishStates(State("w-a", Severity.Critical, "conn-1", healthy: false));
        PublishStates(State("w-b", Severity.Critical, "conn-1", healthy: false));
        var snapshot = seen[^1];
        Assert.Equal(Severity.Success, snapshot.Overall); // 空集=正常基线（聚合器原口径）
        Assert.Equal(0, snapshot.Counts[Severity.Critical]);
        Assert.Equal(0, snapshot.OfflineConnections); // 全勾掉 → 离线横幅也熄
    }

    [Fact]
    public void OfflineConnectionWithoutVisibleWidgets_NotCounted()
    {
        var hidden = new List<string> { "w-a" };
        _aggregator.SetHiddenComponentsProvider(() => hidden);
        var seen = Collect();
        PublishStates(State("w-a", Severity.Success, "conn-dead", healthy: false)); // 唯一组件被勾掉
        PublishStates(State("w-b", Severity.Success, "conn-alive", healthy: false));
        Assert.Equal(1, seen[^1].OfflineConnections); // conn-dead 不再点亮横幅，conn-alive 照数
    }

    [Fact]
    public void OfflineConnection_CountedOnceAcrossItsWidgets()
    {
        var seen = Collect();
        PublishStates(State("w-a", Severity.Success, "conn-dead", healthy: false));
        PublishStates(State("w-b", Severity.Success, "conn-dead", healthy: false));
        Assert.Equal(1, seen[^1].OfflineConnections); // 按连接去重（unhealthy 集合同口径）
    }

    [Fact]
    public void HiddenSetChange_TakesEffectOnNextPublish()
    {
        var hidden = new List<string>();
        _aggregator.SetHiddenComponentsProvider(() => hidden);
        var seen = Collect();
        PublishStates(State("w-err", Severity.Error));
        Assert.Equal(Severity.Error, seen[^1].Overall);

        hidden.Add("w-err"); // 组件中心勾掉（config.HiddenComponents 变化的内存投影）
        _aggregator.Republish(); // 设置页 PinsChanged 同路径：勾选变化即重发布
        Assert.Equal(Severity.Success, seen[^1].Overall); // 勾选变化 → 聚合面随之变化（行为锁核心）
        Assert.Equal(0, seen[^1].Counts[Severity.Error]);
    }
}
