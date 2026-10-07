using Beacon.Core.Models;

namespace Beacon.Core.Events;

/// <summary>Event Bus 事件契约（RFC §3 数据流：Provider → WidgetState → Event Bus → 聚合/通知/UI）。</summary>
public sealed record WidgetStateChanged(WidgetState State);

public sealed record ConnectionHealthChanged(string ConnectionId, ConnectionHealthState State, DateTimeOffset Timestamp);

/// <summary>组件失效（模块停用/删除）：UI 与聚合器丢弃残留状态——停用的模块不留旧灯。</summary>
public sealed record WidgetsInvalidated(IReadOnlyList<string> WidgetIds);

/// <summary>总体状态变化：胶囊/托盘/L0 状态灯消费（overall = max severity）。</summary>
public sealed record AggregateStatusChanged(
    Severity Overall,
    IReadOnlyDictionary<Severity, int> Counts,
    int OfflineConnections,
    DateTimeOffset Timestamp);

public sealed record NotificationRaised(NotificationRecord Notification);

public sealed record ActionExecuted(ActionResult Result);
