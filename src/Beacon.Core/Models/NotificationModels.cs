namespace Beacon.Core.Models;

/// <summary>通知规则：IF（widget 类型/ID + severity 阈值）THEN（toast/声音），带冷却去重（RFC §8）。</summary>
public sealed class NotificationRule
{
    public required string Id { get; init; }
    /// <summary>null = 匹配所有 Widget 类型。</summary>
    public string? WidgetType { get; init; }
    public string? WidgetId { get; init; }
    public Severity SeverityAtLeast { get; init; } = Severity.Warning;
    /// <summary>null = 无上限（与 SeverityAtLeast 构成 [AtLeast, AtMost] 频带，如「仅 Warning」）。</summary>
    public Severity? SeverityAtMost { get; init; }
    public bool Toast { get; init; } = true;
    public bool Sound { get; init; }
    /// <summary>null = 同一阈值期间只提醒一次（跨过阈值时）；设置后冷却期外允许重复提醒。</summary>
    public TimeSpan? Cooldown { get; init; }
    public bool Enabled { get; init; } = true;
}

/// <summary>内置默认规则（用户规则列表为空时生效，RFC §8）：CI Failed→Toast、运行>15min→Warning、PR 待 review、连接降级兜底。</summary>
public static class DefaultNotificationRules
{
    public static IReadOnlyList<NotificationRule> All { get; } =
    [
        new NotificationRule
        {
            Id = "builtin.ci-failed",
            WidgetType = "github.actions.runs",
            SeverityAtLeast = Severity.Error,
            Toast = true,
        },
        new NotificationRule
        {
            Id = "builtin.ci-stuck",
            WidgetType = "github.actions.runs",
            SeverityAtLeast = Severity.Warning,
            SeverityAtMost = Severity.Warning, // 仅 Warning 频带：卡住的运行，不与 Failed 重叠
            Toast = true,
            Cooldown = TimeSpan.FromMinutes(30),
        },
        new NotificationRule
        {
            Id = "builtin.pr-review",
            WidgetType = "github.pull_requests",
            SeverityAtLeast = Severity.Warning,
            Toast = true,
            Cooldown = TimeSpan.FromMinutes(30),
        },
        new NotificationRule
        {
            Id = "builtin.connection-degraded",
            WidgetType = null, // 任意 Widget 陈旧（Unable to refresh）
            SeverityAtLeast = Severity.Warning,
            Toast = true,
            Cooldown = TimeSpan.FromMinutes(15),
        },
    ];
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
