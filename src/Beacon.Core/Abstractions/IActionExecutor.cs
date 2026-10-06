using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>Action 执行器（"open.url" | "http" | "gh.workflow_dispatch" | "local.command" | "webhook"）。</summary>
public interface IActionExecutor
{
    string ActionType { get; }

    Task<ActionResult> ExecuteAsync(ActionConfig action, ActionExecutionContext context, CancellationToken cancellationToken);
}
