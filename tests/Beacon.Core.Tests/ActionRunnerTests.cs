using Beacon.Core.Services;

using Beacon.Core.Models;
using Beacon.Core.Abstractions;
namespace Beacon.Core.Tests;

/// <summary>B-401 验收：路由/确认/超时/结果事件单测。</summary>
public sealed class ActionRunnerTests
{
    private sealed class StubExecutor(string type, Func<ActionConfig, CancellationToken, Task<ActionResult>>? body = null) : IActionExecutor
    {
        public int CallCount;

        public string ActionType => type;

        public Task<ActionResult> ExecuteAsync(ActionConfig action, ActionExecutionContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return body?.Invoke(action, cancellationToken)
                ?? Task.FromResult(Ok(action.Id));
        }
    }

    private static ActionResult Ok(string actionId) => new(actionId, true, "done", null, DateTimeOffset.UtcNow);

    private static ActionConfig Action(string type, bool requireConfirmation = false) => new()
    {
        Id = "a-1",
        Type = type,
        RequireConfirmation = requireConfirmation,
    };

    private static (ActionRunner Runner, List<Core.Events.ActionExecuted> Events, EventBus Bus) Build(params IActionExecutor[] executors)
    {
        var bus = new EventBus();
        var events = new List<Core.Events.ActionExecuted>();
        bus.Subscribe<Core.Events.ActionExecuted>(events.Add);
        var runner = new ActionRunner(executors, bus, new FakeClock());
        return (runner, events, bus);
    }

    [Fact]
    public async Task Routes_ToExecutorByType()
    {
        var open = new StubExecutor("open.url");
        var http = new StubExecutor("http");
        var (runner, events, _) = Build(open, http);

        var result = await runner.ExecuteAsync(Action("http"), new ActionExecutionContext());

        Assert.Equal(1, http.CallCount);
        Assert.Equal(0, open.CallCount);
        Assert.True(result.Success);
        Assert.Equal(ActionOutcome.Succeeded, result.Outcome);
        Assert.Single(events); // ActionExecuted 已发布
    }

    [Fact]
    public async Task UnknownType_FailsWithoutThrowing()
    {
        var (runner, events, _) = Build(new StubExecutor("open.url"));

        var result = await runner.ExecuteAsync(Action("nope"), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Contains("未知 Action 类型", result.Message);
        Assert.Equal(ActionOutcome.Unsupported, result.Outcome); // P5-01：未知动作不混作普通失败
        Assert.Single(events);
    }

    [Fact]
    public async Task Confirmation_Rejected_SkipsExecutor()
    {
        var executor = new StubExecutor("open.url");
        var (runner, _, _) = Build(executor);
        runner.ConfirmationHandler = _ => Task.FromResult(false);

        var result = await runner.ExecuteAsync(Action("open.url", requireConfirmation: true), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Equal("已取消", result.Message);
        Assert.Equal(ActionOutcome.Cancelled, result.Outcome);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public async Task Confirmation_Accepted_Executes()
    {
        var executor = new StubExecutor("open.url");
        var (runner, _, _) = Build(executor);
        runner.ConfirmationHandler = _ => Task.FromResult(true);

        var result = await runner.ExecuteAsync(Action("open.url", requireConfirmation: true), new ActionExecutionContext());

        Assert.True(result.Success);
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task RequireConfirmation_WithoutChannel_RefusesSafe()
    {
        var executor = new StubExecutor("open.url");
        var (runner, _, _) = Build(executor); // 不设 ConfirmationHandler

        var result = await runner.ExecuteAsync(Action("open.url", requireConfirmation: true), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Equal(0, executor.CallCount);
    }

    [Fact]
    public async Task NoConfirmationNeeded_ExecutesWithoutHandler()
    {
        var executor = new StubExecutor("open.url");
        var (runner, _, _) = Build(executor);

        var result = await runner.ExecuteAsync(Action("open.url"), new ActionExecutionContext());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Timeout_CancelsExecutor_ReturnsFailure()
    {
        var executor = new StubExecutor("slow", async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Ok("a-1");
        });
        var (runner, _, _) = Build(executor);
        runner.Timeout = TimeSpan.FromMilliseconds(50);

        var result = await runner.ExecuteAsync(Action("slow"), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Contains("超时", result.Message);
        Assert.Equal(ActionOutcome.TimedOut, result.Outcome); // P5-01：超时态显式携带
    }

    [Fact]
    public async Task ExecutorThrows_FailsGracefully()
    {
        var executor = new StubExecutor("boom", (_, _) => throw new InvalidOperationException("disk full"));
        var (runner, events, _) = Build(executor);

        var result = await runner.ExecuteAsync(Action("boom"), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Equal("disk full", result.Message);
        Assert.Equal(ActionOutcome.Failed, result.Outcome);
        Assert.Single(events); // 失败也发事件 → UI 可见反馈
    }

    [Fact]
    public async Task UserCancellation_ReturnsCancelled()
    {
        var executor = new StubExecutor("slow", async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Ok("a-1");
        });
        var (runner, _, _) = Build(executor);
        runner.Timeout = TimeSpan.FromSeconds(30);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        var result = await runner.ExecuteAsync(Action("slow"), new ActionExecutionContext(), cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal("已取消", result.Message);
        Assert.Equal(ActionOutcome.Cancelled, result.Outcome);
    }

    // ---- 重试（批3-补42：DSL actions.retry 落到 Parameters，默认 1 次 = 不重试） ----

    private static ActionConfig RetryAction(int attempts, int backoff) => new()
    {
        Id = "a-1",
        Type = "flaky",
        RequireConfirmation = false,
        Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["retry_max_attempts"] = attempts.ToString("0"),
            ["retry_backoff_seconds"] = backoff.ToString("0"),
        },
    };

    [Fact]
    public async Task Retry_SucceedsOnLaterAttempt()
    {
        var executor = new StubExecutor("flaky", (_, _) =>
            Task.FromResult(new ActionResult("a-1", false, "HTTP 503", null, DateTimeOffset.UtcNow)));
        var (runner, _, _) = Build(executor);

        var result = await runner.ExecuteAsync(RetryAction(3, 0), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Equal(3, executor.CallCount);
        Assert.Contains("已重试 2 次仍失败", result.Message);
        Assert.Contains("HTTP 503", result.Message);
    }

    [Fact]
    public async Task Retry_FailThenSuccess_ReturnsSuccessWithoutAnnotation()
    {
        var calls = 0;
        var executor = new StubExecutor("flaky", (_, _) =>
        {
            var attempt = Interlocked.Increment(ref calls);
            return Task.FromResult(attempt < 3
                ? new ActionResult("a-1", false, "HTTP 503", null, DateTimeOffset.UtcNow)
                : Ok("a-1"));
        });
        var (runner, _, _) = Build(executor);

        var result = await runner.ExecuteAsync(RetryAction(3, 0), new ActionExecutionContext());

        Assert.True(result.Success);
        Assert.Equal("done", result.Message);
        Assert.Equal(3, executor.CallCount);
    }

    [Fact]
    public async Task Retry_NoParams_SingleAttempt_OriginalMessage()
    {
        var executor = new StubExecutor("flaky", (_, _) =>
            Task.FromResult(new ActionResult("a-1", false, "HTTP 503", null, DateTimeOffset.UtcNow)));
        var (runner, _, _) = Build(executor);

        var result = await runner.ExecuteAsync(Action("flaky"), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Equal("HTTP 503", result.Message); // 单次路径消息逐字节不变
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task Retry_InvalidParams_FallBackOrClamp()
    {
        var executor = new StubExecutor("flaky", (_, _) =>
            Task.FromResult(new ActionResult("a-1", false, "boom", null, DateTimeOffset.UtcNow)));
        var (runner, _, _) = Build(executor);

        // 非数字 → 默认 1 次
        var bad = await runner.ExecuteAsync(new ActionConfig
        {
            Id = "a-1",
            Type = "flaky",
            RequireConfirmation = false,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["retry_max_attempts"] = "abc" },
        }, new ActionExecutionContext());
        Assert.Equal(1, executor.CallCount);
        Assert.Equal("boom", bad.Message);

        // 超上限 → 钳位到 10
        var clampExecutor = new StubExecutor("flaky", (_, _) =>
            Task.FromResult(new ActionResult("a-1", false, "boom", null, DateTimeOffset.UtcNow)));
        var (clampRunner, _, _) = Build(clampExecutor);
        var clamped = await clampRunner.ExecuteAsync(new ActionConfig
        {
            Id = "a-1",
            Type = "flaky",
            RequireConfirmation = false,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["retry_max_attempts"] = "99",
                ["retry_backoff_seconds"] = "0",
            },
        }, new ActionExecutionContext());
        Assert.Equal(10, clampExecutor.CallCount);
        Assert.Contains("已重试 9 次仍失败", clamped.Message);
    }

    [Fact]
    public async Task Retry_UserCancelDuringBackoff_ReturnsCancelled()
    {
        var executor = new StubExecutor("flaky", (_, _) =>
            Task.FromResult(new ActionResult("a-1", false, "HTTP 503", null, DateTimeOffset.UtcNow)));
        var (runner, _, _) = Build(executor);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(80));

        var result = await runner.ExecuteAsync(RetryAction(3, 30), new ActionExecutionContext(), cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal("已取消", result.Message); // 退避中取消：不重试、不标注
        Assert.Equal(1, executor.CallCount);
    }

    [Fact]
    public async Task Retry_TimeoutDuringBackoff_ReturnsTimeoutFail()
    {
        var executor = new StubExecutor("flaky", (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new ActionResult("a-1", false, "HTTP 503", null, DateTimeOffset.UtcNow));
        });
        var (runner, _, _) = Build(executor);
        runner.Timeout = TimeSpan.FromMilliseconds(150);

        var result = await runner.ExecuteAsync(RetryAction(3, 30), new ActionExecutionContext());

        Assert.False(result.Success);
        Assert.Contains("执行超时", result.Message); // 重试共享总超时预算：预算耗尽打断退避、按超时收口
        Assert.Equal(1, executor.CallCount); // 退避被打断即收口，不再发起下一次尝试
    }
}
