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
}

public sealed record ActionResult(
    string ActionId,
    bool Success,
    string? Message,
    string? OutputPath,
    DateTimeOffset CompletedAt);
