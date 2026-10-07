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
        var counts = new Dictionary<Severity, int>();
        var overall = Severity.Success;
        lock (_gate)
        {
            foreach (var state in _states.Values)
            {
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
            offline = _unhealthyConnections.Count;
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
