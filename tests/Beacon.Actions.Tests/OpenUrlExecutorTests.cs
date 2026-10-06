using Beacon.Core.Models;

namespace Beacon.Actions.Tests;

/// <summary>B-402 验收：模板渲染单测（浏览器打开为手动验证项）。</summary>
public sealed class OpenUrlExecutorTests
{
    private static ActionExecutionContext Context(Dictionary<string, string>? payload = null) => new()
    {
        Vars = new Dictionary<string, string> { ["owner"] = "cuihairu" },
        SourceState = new WidgetState
        {
            WidgetId = "w-1",
            WidgetType = "github.actions.runs",
            ConnectionId = "gh-main",
            Severity = Severity.Error,
            Summary = "ci failed",
            Payload = payload ?? new Dictionary<string, string> { ["repo"] = "beacon", ["run_id"] = "987" },
        },
    };

    private static ActionConfig Action(string url) => new()
    {
        Id = "open-ci",
        Type = "open.url",
        RequireConfirmation = false,
        Parameters = new Dictionary<string, string> { ["url"] = url },
    };

    [Fact]
    public async Task Renders_Template_FromVarsAndPayload()
    {
        var opened = new List<string>();
        var executor = new OpenUrlExecutor(url => { opened.Add(url); return Task.CompletedTask; });

        var result = await executor.ExecuteAsync(
            Action("https://github.com/{owner}/{repo}/actions/runs/{run_id}"),
            Context(),
            CancellationToken.None);

        Assert.True(result.Success);
        var target = Assert.Single(opened);
        Assert.Equal("https://github.com/cuihairu/beacon/actions/runs/987", target);
        Assert.Equal(target, result.Message); // UI 反馈目标链接
    }

    [Fact]
    public async Task Payload_OverridesVars_OnConflict()
    {
        var opened = new List<string>();
        var executor = new OpenUrlExecutor(url => { opened.Add(url); return Task.CompletedTask; });

        await executor.ExecuteAsync(Action("https://x.test/{repo}"), Context(payload: new Dictionary<string, string> { ["repo"] = "from-payload" }), CancellationToken.None);

        Assert.Equal("https://x.test/from-payload", Assert.Single(opened));
    }

    [Fact]
    public async Task MissingPlaceholder_FailsClosed()
    {
        var opened = new List<string>();
        var executor = new OpenUrlExecutor(url => { opened.Add(url); return Task.CompletedTask; });

        var result = await executor.ExecuteAsync(Action("https://x.test/{missing_key}"), Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("missing_key", result.Message);
        Assert.Empty(opened); // 未执行打开
    }

    [Fact]
    public async Task MissingUrlParameter_Fails()
    {
        var executor = new OpenUrlExecutor(_ => Task.CompletedTask);

        var result = await executor.ExecuteAsync(
            new ActionConfig { Id = "a", Type = "open.url", RequireConfirmation = false },
            Context(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("url", result.Message);
    }
}
