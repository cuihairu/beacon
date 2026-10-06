using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>GitHub Workflow Action 参数解析共用件（B-403）：Parameters 优先，回落 SourceState.Payload。</summary>
internal static class GitHubWorkflowArgs
{
    public static string? Resolve(ActionConfig action, ActionExecutionContext context, string key, string payloadKey)
    {
        if (action.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }
        if (context.SourceState is { } state && state.Payload.TryGetValue(payloadKey, out var fromPayload) && !string.IsNullOrWhiteSpace(fromPayload))
        {
            return fromPayload.Trim();
        }
        return null;
    }
}

/// <summary>三个 Workflow Action（B-403）的公共路由：取连接、取客户端、异常转失败结果。</summary>
public abstract class GitHubWorkflowExecutorBase : IActionExecutor
{
    public abstract string ActionType { get; }

    protected abstract Task<ActionResult> ExecuteCoreAsync(
        GitHubApiClient client,
        ActionConfig action,
        ActionExecutionContext context,
        string repo,
        CancellationToken cancellationToken);

    public async Task<ActionResult> ExecuteAsync(
        ActionConfig action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Connection is not { } connection || context.ConnectionContext is not { } connectionContext)
        {
            return Fail(action.Id, "缺少连接上下文（SourceState 未关联连接或未注入密钥库）。");
        }
        var repo = GitHubWorkflowArgs.Resolve(action, context, "repo", "repo");
        if (repo is null)
        {
            return Fail(action.Id, "缺少 repo（可由 Widget 状态携带）。");
        }

        try
        {
            var client = GitHubClientCache.Get(connection, connectionContext);
            return await ExecuteCoreAsync(client, action, context, repo, cancellationToken).ConfigureAwait(false);
        }
        catch (ConnectionException exception)
        {
            return Fail(action.Id, exception.Message);
        }
    }

    protected static ActionResult Ok(string actionId, string message)
        => new(actionId, Success: true, Message: message, OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);

    protected static ActionResult Fail(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);
}

/// <summary>gh.workflow_dispatch：触发运行。触发后由 workflow 级（10s）常规轮询跟踪至终态（payload run_id 已随 WidgetState 流转）。</summary>
public sealed class GitHubWorkflowDispatchExecutor : GitHubWorkflowExecutorBase
{
    public static readonly string DefaultType = "gh.workflow_dispatch";

    public override string ActionType => DefaultType;

    protected override async Task<ActionResult> ExecuteCoreAsync(
        GitHubApiClient client,
        ActionConfig action,
        ActionExecutionContext context,
        string repo,
        CancellationToken cancellationToken)
    {
        var workflow = GitHubWorkflowArgs.Resolve(action, context, "workflow", "workflow");
        var branch = GitHubWorkflowArgs.Resolve(action, context, "branch", "branch") ?? "main";
        if (workflow is null)
        {
            return Fail(action.Id, "缺少 workflow（可由 Widget 状态携带）。");
        }
        var body = JsonSerializer.Serialize(new { @ref = branch });
        var ok = await client
            .PostAsync($"/repos/{repo}/actions/workflows/{Uri.EscapeDataString(workflow)}/dispatches", body, cancellationToken)
            .ConfigureAwait(false);
        return Ok(action.Id, $"已触发 {repo}/{workflow}@{branch}");
    }
}

/// <summary>gh.workflow_rerun：rerun failed jobs（Retry）。</summary>
public sealed class GitHubWorkflowRerunExecutor : GitHubWorkflowExecutorBase
{
    public static readonly string DefaultType = "gh.workflow_rerun";

    public override string ActionType => DefaultType;

    protected override async Task<ActionResult> ExecuteCoreAsync(
        GitHubApiClient client,
        ActionConfig action,
        ActionExecutionContext context,
        string repo,
        CancellationToken cancellationToken)
    {
        var runId = GitHubWorkflowArgs.Resolve(action, context, "runId", "run_id")
            ?? throw new ConnectionException("缺少 runId（可由 Widget 状态携带）。", ConnectionHealthState.Degraded);
        var ok = await client
            .PostAsync($"/repos/{repo}/actions/runs/{Uri.EscapeDataString(runId)}/rerun-failed-jobs", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Ok(action.Id, $"已重跑 run {runId} 的失败 job");
    }
}

/// <summary>gh.workflow_cancel：取消运行中的 run。</summary>
public sealed class GitHubWorkflowCancelExecutor : GitHubWorkflowExecutorBase
{
    public static readonly string DefaultType = "gh.workflow_cancel";

    public override string ActionType => DefaultType;

    protected override async Task<ActionResult> ExecuteCoreAsync(
        GitHubApiClient client,
        ActionConfig action,
        ActionExecutionContext context,
        string repo,
        CancellationToken cancellationToken)
    {
        var runId = GitHubWorkflowArgs.Resolve(action, context, "runId", "run_id")
            ?? throw new ConnectionException("缺少 runId（可由 Widget 状态携带）。", ConnectionHealthState.Degraded);
        var ok = await client
            .PostAsync($"/repos/{repo}/actions/runs/{Uri.EscapeDataString(runId)}/cancel", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Ok(action.Id, $"已请求取消 run {runId}");
    }
}
