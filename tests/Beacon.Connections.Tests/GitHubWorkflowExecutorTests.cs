using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-403 验收：dispatch/rerun/cancel API 封装（假 Handler）。</summary>
public sealed class GitHubWorkflowExecutorTests
{
    private static readonly ConnectionConfig Connection = new()
    {
        Id = "gh-main",
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
            Payload = payload ?? new Dictionary<string, string> { ["repo"] = "owner/repo", ["run_id"] = "987" },
        },
    };

    private static ActionConfig Action(string type, Dictionary<string, string>? parameters = null) => new()
    {
        Id = "a-1",
        Type = type,
        RequireConfirmation = false,
        Parameters = parameters ?? [],
    };

    /// <summary>注入假客户端后执行，返回结果与捕获的请求。</summary>
    private static async Task<(ActionResult Result, IReadOnlyList<HttpRequestMessage> Requests)> RunAsync(
        IActionExecutor executor,
        FakeHttpMessageHandler handler,
        ActionExecutionContext context,
        ActionConfig action)
    {
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var result = await executor.ExecuteAsync(action, context, CancellationToken.None);
            return (result, handler.Requests);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task Dispatch_PostsRefToWorkflowEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.NoContent);

        var (result, requests) = await RunAsync(
            new GitHubWorkflowDispatchExecutor(),
            handler,
            Context(),
            Action(GitHubWorkflowDispatchExecutor.DefaultType, new Dictionary<string, string>
            {
                ["workflow"] = "ci.yml",
                ["branch"] = "feature-x",
            }));

        Assert.True(result.Success);
        Assert.Contains("已触发 owner/repo/ci.yml@feature-x", result.Message);
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("/actions/workflows/ci.yml/dispatches", request.RequestUri!.ToString());
        Assert.Contains("\"ref\":\"feature-x\"", await request.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rerun_UsesRunIdFromPayload()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Accepted);

        var (result, requests) = await RunAsync(
            new GitHubWorkflowRerunExecutor(),
            handler,
            Context(),
            Action(GitHubWorkflowRerunExecutor.DefaultType));

        Assert.True(result.Success);
        Assert.Contains("/actions/runs/987/rerun-failed-jobs", Assert.Single(requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task Cancel_PostsCancelEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Accepted);

        var (result, requests) = await RunAsync(
            new GitHubWorkflowCancelExecutor(),
            handler,
            Context(),
            Action(GitHubWorkflowCancelExecutor.DefaultType));

        Assert.True(result.Success);
        Assert.Contains("已请求取消 run 987", result.Message);
        Assert.Contains("/actions/runs/987/cancel", Assert.Single(requests).RequestUri!.ToString());
    }

    [Fact]
    public async Task MissingConnectionContext_FailsSafe()
    {
        var executor = new GitHubWorkflowDispatchExecutor();
        var bare = new ActionExecutionContext { SourceState = Context().SourceState }; // 未注入连接

        var result = await executor.ExecuteAsync(Action("gh.workflow_dispatch"), bare, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("缺少连接上下文", result.Message);
    }

    [Fact]
    public async Task ApiError_ConvertsToFailureResult()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.Forbidden, "{}", headers: [("X-RateLimit-Remaining", "7")]); // 权限不足

        var (result, _) = await RunAsync(
            new GitHubWorkflowCancelExecutor(),
            handler,
            Context(),
            Action(GitHubWorkflowCancelExecutor.DefaultType));

        Assert.False(result.Success);
        Assert.Contains("403", result.Message);
    }

    [Fact]
    public async Task MissingRunId_FailsWithHint()
    {
        var handler = new FakeHttpMessageHandler();
        var executor = new GitHubWorkflowCancelExecutor();
        var context = Context(payload: new Dictionary<string, string> { ["repo"] = "owner/repo" });

        var (result, requests) = await RunAsync(executor, handler, context, Action("gh.workflow_cancel"));

        Assert.False(result.Success);
        Assert.Contains("runId", result.Message);
        Assert.Empty(requests);
    }
}
