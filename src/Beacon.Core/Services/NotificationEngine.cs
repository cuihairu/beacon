using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;

namespace Beacon.Core.Services;

/// <summary>
/// 通知引擎（B-502，RFC §8）：订阅 WidgetStateChanged，规则表驱动求值
/// （widget 类型/ID + [AtLeast, AtMost] 频带 → toast/sound）。
/// 去重：Cooldown=null = 阈值跨越语义（同一段 Warning 期间只提醒一次，降回后重新武装）；
/// Cooldown 有值 = 时间冷却。记录滚动保留 200 条 + 已读/未读。
/// </summary>
public sealed class NotificationEngine : IDisposable
{
    /// <summary>通知记录滚动上限（RFC B-502）。</summary>
    public const int MaxRecords = 200;

    private readonly IEventBus _bus;
    private readonly INotificationSink _sink;
    private readonly IClock _clock;
    private readonly Func<IReadOnlyList<NotificationRule>> _rulesProvider;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private readonly List<NotificationRecord> _records = [];
    private readonly Dictionary<(string RuleId, string WidgetId), DateTimeOffset> _lastNotified = [];
    private readonly HashSet<(string RuleId, string WidgetId)> _activeCrossings = [];
    private readonly IDisposable _subscription;
    private long _recordSequence;

    public NotificationEngine(
        IEventBus bus,
        INotificationSink sink,
        IClock clock,
        Func<IReadOnlyList<NotificationRule>>? rulesProvider = null,
        ILogger? logger = null)
    {
        _bus = bus;
        _sink = sink;
        _clock = clock;
        _logger = logger;
        _rulesProvider = rulesProvider ?? (() => DefaultNotificationRules.All);
        _subscription = bus.Subscribe<WidgetStateChanged>(OnWidgetStateChanged);
    }

    /// <summary>记录集变化（新增投递 / 已读标记）——App 侧据此持久化 notifications.json（fire-and-forget）。</summary>
    public event Action? RecordsChanged;

    /// <summary>通知中心记录（旧→新）。</summary>
    public IReadOnlyList<NotificationRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return [.. _records];
            }
        }
    }

    /// <summary>启动水合（notifications.json 恢复记录，RFC §8 跨重启）；只回放展示记录，
    /// 不重建冷却/跨越状态——重启后仍在告警期的组件按阈值跨越语义重新提醒一次，属预期。</summary>
    public void Hydrate(IReadOnlyList<NotificationRecord> records)
    {
        lock (_gate)
        {
            _records.Clear();
            _records.AddRange(records.TakeLast(MaxRecords)); // 恢复超限旧数据同样按滚动上限裁齐
        }
    }

    public int UnreadCount
    {
        get
        {
            lock (_gate)
            {
                return _records.Count(record => !record.Read);
            }
        }
    }

    public void MarkRead(string notificationId)
    {
        lock (_gate)
        {
            foreach (var record in _records.Where(record => record.Id == notificationId))
            {
                record.Read = true;
            }
        }
        RaiseRecordsChanged();
    }

    public void MarkAllRead()
    {
        lock (_gate)
        {
            foreach (var record in _records)
            {
                record.Read = true;
            }
        }
        RaiseRecordsChanged();
    }

    private void OnWidgetStateChanged(WidgetStateChanged evt)
    {
        var state = evt.State;
        // Success/Info 静默（RFC §8：通知只管异常）；空 Summary 的占位不推
        if (state.Severity < Severity.Warning)
        {
            // 阈值回落 → 重新武装跨越语义
            lock (_gate)
            {
                _activeCrossings.RemoveWhere(key => key.WidgetId == state.WidgetId);
            }
            return;
        }

        var rules = _rulesProvider();
        foreach (var rule in rules)
        {
            if (!rule.Enabled || !Matches(rule, state))
            {
                continue;
            }
            if (ShouldDeliver(rule, state))
            {
                Deliver(rule, state);
            }
        }
    }

    private static bool Matches(NotificationRule rule, WidgetState state)
        => (rule.WidgetType is null || string.Equals(rule.WidgetType, state.WidgetType, StringComparison.OrdinalIgnoreCase))
           && (rule.WidgetId is null || rule.WidgetId == state.WidgetId)
           && state.Severity >= rule.SeverityAtLeast
           && (rule.SeverityAtMost is null || state.Severity <= rule.SeverityAtMost);

    private bool ShouldDeliver(NotificationRule rule, WidgetState state)
    {
        var key = (rule.Id, state.WidgetId);
        lock (_gate)
        {
            if (rule.Cooldown is { } cooldown)
            {
                return !_lastNotified.TryGetValue(key, out var last) || _clock.UtcNow - last >= cooldown;
            }
            // 阈值跨越：处于同一段告警期间不重复提醒
            return _activeCrossings.Add(key);
        }
    }

    private void Deliver(NotificationRule rule, WidgetState state)
    {
        var key = (rule.Id, state.WidgetId);
        lock (_gate)
        {
            _lastNotified[key] = _clock.UtcNow;
        }
        var record = new NotificationRecord
        {
            Id = $"{rule.Id}-{_clock.UtcNow:yyyyMMddHHmmssfff}-{Interlocked.Increment(ref _recordSequence)}",
            SourceWidgetId = state.WidgetId,
            WidgetType = state.WidgetType,
            Severity = state.Severity,
            Title = state.Summary,
            Message = state.IsStale ? $"Last update {state.FetchedAt:HH:mm:ss}" : null,
            DetailUrl = state.DetailUrl,
            Timestamp = _clock.UtcNow,
        };
        lock (_gate)
        {
            _records.Add(record);
            if (_records.Count > MaxRecords)
            {
                _records.RemoveRange(0, _records.Count - MaxRecords);
            }
        }
        try
        {
            _sink.Show(record, new NotificationDelivery(rule.Toast, rule.Sound));
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "通知出口投递失败（{NotificationId}）。", record.Id);
        }
        _bus.Publish(new NotificationRaised(record));
        RaiseRecordsChanged();
    }

    private void RaiseRecordsChanged() => RecordsChanged?.Invoke();

    public void Dispose() => _subscription.Dispose();
}
