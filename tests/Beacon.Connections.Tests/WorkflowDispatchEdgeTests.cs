using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// gh.workflow_* 动作边界收口（Batch 4）：dispatch 缺 workflow、cancel 缺 runId、
/// 基座 ConnectionException 转 Fail（客户端异常不炸动作链）。
/// </summary>
public sealed class WorkflowDispatchEdgeTests
{
    private static readonly ConnectionConfig Connection = new()
    {
        Id = "gh-edge",
        Type = "github",
        Endpoint = "https://api.github.test/",
    };

    private static ActionExecutionContext Context(Dictionary<string, string>? payload = null) => new()
    {
        Connection = Connection,
        ConnectionContext = new ConnectionContext { Secrets = new SecretStoreStub() },
        SourceState = new WidgetState
        {
            WidgetId = "w-ci",
            WidgetType = "github.actions.runs",
            ConnectionId = Connection.Id,
            Severity = Severity.Error,
            Summary = "ci failed",
            Payload = payload ?? new Dictionary<string, string> { ["repo"] = "owner/repo" },
        },
    };

    private static ActionConfig Action(string type, Dictionary<string, string>? parameters = null) => new()
    {
        Id = "a-1",
        Type = type,
        RequireConfirmation = false,
        Parameters = parameters ?? [],
    };

    private static async Task<ActionResult> RunAsync(
        IActionExecutor executor,
        FakeHttpMessageHandler? handler,
        ActionExecutionContext context,
        ActionConfig action,
        CancellationToken token = default)
    {
        if (handler is not null)
        {
            GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        }
        try
        {
            return await executor.ExecuteAsync(action, context, token);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task Dispatch_MissingWorkflow_FailsWithHint()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.NoContent) };
        var result = await RunAsync(new GitHubWorkflowDispatchExecutor(), handler,
            Context(payload: new Dictionary<string, string> { ["repo"] = "owner/repo" }),
            Action("gh.workflow_dispatch"));

        Assert.False(result.Success);
        Assert.Contains("workflow", result.Message);
        Assert.Empty(handler.Requests); // 缺参不发出请求
    }

    [Fact]
    public async Task Cancel_MissingRunId_FailsWithHint()
    {
        var handler = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.NoContent) };
        var result = await RunAsync(new GitHubWorkflowCancelExecutor(), handler,
            Context(payload: new Dictionary<string, string> { ["repo"] = "owner/repo" }),
            Action("gh.workflow_cancel"));

        Assert.False(result.Success);
        Assert.Contains("runId", result.Message);
    }

    [Fact]
    public async Task Executor_ClientException_ConvertsToFailureResult()
    {
        var handler = new FakeHttpMessageHandler
        {
            Responder = _ => throw new ConnectionException("boom", ConnectionHealthState.Offline),
        };
        var result = await RunAsync(new GitHubWorkflowDispatchExecutor(), handler,
            Context(payload: new Dictionary<string, string> { ["repo"] = "owner/repo", ["workflow"] = "ci.yml" }),
            Action("gh.workflow_dispatch"));

        Assert.False(result.Success);
        Assert.Contains("boom", result.Message);
    }
}
