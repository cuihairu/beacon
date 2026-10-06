namespace Beacon.Core.Models;

/// <summary>通知规则：IF（widget 类型/ID + severity 阈值）THEN（toast/声音），带冷却去重（RFC §8）。</summary>
public sealed class NotificationRule
{
    public required string Id { get; init; }
    /// <summary>null = 匹配所有 Widget 类型。</summary>
    public string? WidgetType { get; init; }
    public string? WidgetId { get; init; }
    public Severity SeverityAtLeast { get; init; } = Severity.Warning;
    public bool Toast { get; init; } = true;
    public bool Sound { get; init; }
    /// <summary>null = 同一阈值期间只提醒一次（跨过阈值时）；设置后冷却期外允许重复提醒。</summary>
    public TimeSpan? Cooldown { get; init; }
    public bool Enabled { get; init; } = true;
}

/// <summary>一次提醒的投递方式（Toast/声音/托盘着色由实现组合）。</summary>
public sealed record NotificationDelivery(bool Toast, bool Sound);

/// <summary>通知记录：滚动保留 + 已读/未读（RFC §8）。</summary>
public sealed class NotificationRecord
{
    public required string Id { get; init; }
    public required string SourceWidgetId { get; init; }
    public string? WidgetType { get; init; }
    public required Severity Severity { get; init; }
    public required string Title { get; init; }
    public string? Message { get; init; }
    public string? DetailUrl { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public bool Read { get; set; }
}
