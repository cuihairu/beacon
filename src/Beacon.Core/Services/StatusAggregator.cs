using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 状态聚合（B-205，RFC §4.4 / §32）：所有 Widget 压缩为总体 Severity + 分级计数。
/// 总体 = max severity；空集 = Success（正常基线）；变化才发布（去重）。
/// </summary>
public sealed class StatusAggregator : IDisposable
{
    private readonly IEventBus _bus;
    private readonly object _gate = new();
    private readonly Dictionary<string, WidgetState> _states = [];
    private readonly HashSet<string> _unhealthyConnections = [];
    private AggregateStatusChanged? _lastPublished;
    private Func<IReadOnlyCollection<string>>? _hiddenProvider; // 组件中心隐藏集（实时读 config，ISLAND-RING-1）
    private readonly IDisposable _stateSubscription;
    private readonly IDisposable _healthSubscription;
    private readonly IDisposable _invalidationSubscription;

    public StatusAggregator(IEventBus bus)
    {
        _bus = bus;
        _stateSubscription = bus.Subscribe<WidgetStateChanged>(OnWidgetStateChanged);
        _healthSubscription = bus.Subscribe<ConnectionHealthChanged>(OnConnectionHealthChanged);
        _invalidationSubscription = bus.Subscribe<WidgetsInvalidated>(OnWidgetsInvalidated);
    }

    /// <summary>当前聚合快照（胶囊启动时取初值用）。</summary>
    public AggregateStatusChanged Snapshot() => Compute();

    /// <summary>组件中心隐藏集读取器（ISLAND-RING-1 扩围①，2026-11-07 用户令）：勾掉的组件不进
    /// 聚合——在源头过滤而非消费端重投影（消费端用滞后快照重投影会抹掉真实级别，实机取证
    /// 04:12:39 raw Error:1 → filtered err=0 即此竞态）。App 注入 () => config.HiddenComponents。</summary>
    public void SetHiddenComponentsProvider(Func<IReadOnlyCollection<string>>? provider) => _hiddenProvider = provider;

    /// <summary>强制重发布（组件中心勾选变化即调）：隐藏集变了但组件状态没变时聚合快照不变、
    /// 会被去重吞掉——清掉基线让下一 Compute 无条件发布。</summary>
    public void Republish()
    {
        lock (_gate)
        {
            _lastPublished = null;
        }

        PublishIfChanged();
    }

    private void OnWidgetStateChanged(WidgetStateChanged evt)
    {
        lock (_gate)
        {
            _states[evt.State.WidgetId] = evt.State;
            if (evt.State.ConnectionHealthy)
            {
                _unhealthyConnections.Remove(evt.State.ConnectionId);
            }
            PublishIfChanged();
        }
    }

    private void OnConnectionHealthChanged(ConnectionHealthChanged evt)
    {
        lock (_gate)
        {
            var changed = evt.State is ConnectionHealthState.Healthy
                ? _unhealthyConnections.Remove(evt.ConnectionId)
                : _unhealthyConnections.Add(evt.ConnectionId);
            if (changed)
            {
                PublishIfChanged();
            }
        }
    }

    private void OnWidgetsInvalidated(WidgetsInvalidated evt)
    {
        lock (_gate)
        {
            var removed = false;
            foreach (var widgetId in evt.WidgetIds)
            {
                removed |= _states.Remove(widgetId);
            }
            if (removed)
            {
                PublishIfChanged();
            }
        }
    }

    private void PublishIfChanged()
    {
        var snapshot = Compute();
        if (_lastPublished is null || Changed(_lastPublished, snapshot))
        {
            _lastPublished = snapshot;
            _bus.Publish(snapshot);
        }
    }

    private AggregateStatusChanged Compute()
    {
        var hidden = _hiddenProvider?.Invoke() ?? [];
        var counts = new Dictionary<Severity, int>();
        var overall = Severity.Success;
        lock (_gate)
        {
            foreach (var state in _states.Values)
            {
                if (hidden.Contains(state.WidgetId))
                {
                    continue; // 组件中心勾掉：不进任何聚合消费面（灯/托盘/岛环徽标同源同口径）
                }

                counts[state.Severity] = counts.GetValueOrDefault(state.Severity) + 1;
                overall = overall.Max(state.Severity);
            }
        }
        foreach (Severity severity in Enum.GetValues<Severity>())
        {
            counts.TryAdd(severity, 0);
        }
        int offline;
        lock (_gate)
        {
            // 离线连接只数仍持有可见组件的：有组件状态且全部被勾掉的离线连接不再点亮横幅；
            // 尚无任何组件状态的连接（冷启动半）保持历史行为照数（ConnectionHealthChanged 原口径）
            offline = _unhealthyConnections.Count(connection =>
                !_states.Values.Any(state => state.ConnectionId == connection)
                || _states.Values.Any(state => state.ConnectionId == connection && !hidden.Contains(state.WidgetId)));
        }
        return new AggregateStatusChanged(overall, counts, offline, DateTimeOffset.UtcNow);
    }

    private static bool Changed(AggregateStatusChanged left, AggregateStatusChanged right)
        => left.Overall != right.Overall
           || left.OfflineConnections != right.OfflineConnections
           || left.Counts.Count != right.Counts.Count
           || left.Counts.Any(kv => !right.Counts.TryGetValue(kv.Key, out var value) || value != kv.Value);

    public void Dispose()
    {
        _stateSubscription.Dispose();
        _healthSubscription.Dispose();
        _invalidationSubscription.Dispose();
    }
}
