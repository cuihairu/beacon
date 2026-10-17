using Beacon.Core.Abstractions;

namespace Beacon.Core.Models;

/// <summary>Action 配置（来自 Widget 绑定或 Workflow，MVP 仅简单 Action）。</summary>
public sealed class ActionConfig
{
    public required string Id { get; init; }
    /// <summary>"open.url" | "http" | "gh.workflow_dispatch" | "local.command" | "webhook"。</summary>
    public required string Type { get; init; }
    public string? Name { get; init; }
    /// <summary>危险操作执行前需要确认，可按 Action 关闭（RFC §13）。</summary>
    public bool RequireConfirmation { get; init; } = true;
    public Dictionary<string, string> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ActionExecutionContext
{
    /// <summary>模板变量；执行时由 ActionRunner 合并 SourceState 派生变量。</summary>
    public IReadOnlyDictionary<string, string> Vars { get; init; } = new Dictionary<string, string>();
    public WidgetState? SourceState { get; init; }
    /// <summary>SourceState 对应的连接（ActionRunner 注入），远程 Action 执行器消费。</summary>
    public ConnectionConfig? Connection { get; init; }
    /// <summary>连接执行上下文（密钥等，ActionRunner 注入）。</summary>
    public ConnectionContext? ConnectionContext { get; init; }
}

/// <summary>单次 Action 执行终态（P5-01 五态）：取消/超时/未知动作不再靠解析提示文字判定。</summary>
public enum ActionOutcome
{
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
    Unsupported,
}

public sealed record ActionResult(
    string ActionId,
    bool Success,
    string? Message,
    string? OutputPath,
    DateTimeOffset CompletedAt)
{
    /// <summary>执行终态：显式携带五态；未显式指定时按 Success 兼容派生（旧构造/旧落盘数据零迁移），
    /// 兼容映射不变式 Success == (Outcome == Succeeded) 由 Runner 落枚举处保证。</summary>
    public ActionOutcome Outcome { get; init; } = Success ? ActionOutcome.Succeeded : ActionOutcome.Failed;

    /// <summary>动作来源 Widget（P5-02 动作后刷源）：Runner 按 SourceState 盖章；null=无来源，不触发刷新。
    /// 关联的是配置 ID 而非 run——远端任务关联仍看各执行器的消息与 Widget 状态流转。</summary>
    public string? SourceWidgetId { get; init; }

    /// <summary>单次执行关联标识（P5-01）：区分 Action 配置 ID 与每一次执行（重试共用一次结算不另发 id）。
    /// 构造即派生（旧构造/旧落盘零迁移）；序列化保留已存值，ActionId 与 ExecutionId 不混用。</summary>
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
}
