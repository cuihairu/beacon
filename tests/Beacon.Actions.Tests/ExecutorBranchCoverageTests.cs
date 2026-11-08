using System.Net;
using Beacon.Core.Models;

namespace Beacon.Actions.Tests;

/// <summary>
/// 执行器剩余分支收口：类型契约（ActionType/DefaultType）、外部取消与超时两分、
/// webhook 头部透传与负超时 fail-loud、local.command 未知 shell 与日志写失败吞错、
/// open.url 真实 shell 启动。
/// </summary>
public sealed class ExecutorBranchCoverageTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(Response);
        }
    }

    private static ActionExecutionContext Context() => new()
    {
        SourceState = new WidgetState
        {
            WidgetId = "w-1",
            WidgetType = "t",
            ConnectionId = "c-main",
            Severity = Severity.Info,
            Summary = "ok",
            Payload = new Dictionary<string, string>(),
        },
    };

    private static ActionConfig Action(string type, Dictionary<string, string> parameters) => new()
    {
        Id = "a-1",
        Type = type,
        RequireConfirmation = false,
        Parameters = parameters,
    };

    // ---------- HttpExecutor ----------

    [Fact]
    public void ExecutorBranch_Http_TypeContractIsPinned()
    {
        Assert.Equal("http", HttpExecutor.DefaultType);
        Assert.Equal("http", new HttpExecutor().ActionType);
    }

    [Fact]
    public async Task ExecutorBranch_Http_ExternallyCancelled_ReturnsCancelled()
    {
        var executor = new HttpExecutor(new CaptureHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await executor.ExecuteAsync(
            Action("http", new() { ["url"] = "https://x.test/" }), Context(), cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ActionOutcome.Cancelled, result.Outcome);
        Assert.Equal("已取消", result.Message);
    }

    // ---------- WebhookExecutor ----------

    [Fact]
    public void ExecutorBranch_Webhook_TypeContractIsPinned()
    {
        Assert.Equal("webhook", WebhookExecutor.DefaultType);
        Assert.Equal("X-Beacon-Signature", WebhookExecutor.SignatureHeader);
        Assert.Equal("webhook", new WebhookExecutor().ActionType);
    }

    [Fact]
    public async Task ExecutorBranch_Webhook_HeaderPassthrough_ReachesRequest()
    {
        var handler = new CaptureHandler();
        var executor = new WebhookExecutor(handler);

        var result = await executor.ExecuteAsync(
            Action("webhook", new()
            {
                ["url"] = "https://hook.test/",
                ["sign"] = "false",
                ["header.X-Trace"] = "abc-123",
            }), Context(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(handler.Last);
        Assert.True(handler.Last!.Headers.TryGetValues("X-Trace", out var values));
        Assert.Equal("abc-123", values!.Single());
    }

    [Fact]
    public async Task ExecutorBranch_Webhook_NegativeTimeout_FailsLoud()
    {
        var executor = new WebhookExecutor(new CaptureHandler());

        var result = await executor.ExecuteAsync(
            Action("webhook", new() { ["url"] = "https://hook.test/", ["timeoutSeconds"] = "-5" }),
            Context(), CancellationToken.None);

        Assert.False(result.Success); // CancelAfter 负延时抛 ArgumentException → fail-loud
    }

    [Fact]
    public async Task ExecutorBranch_Webhook_ExternallyCancelled_ReturnsCancelled()
    {
        var executor = new WebhookExecutor(new CaptureHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await executor.ExecuteAsync(
            Action("webhook", new() { ["url"] = "https://hook.test/", ["sign"] = "false" }),
            Context(), cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ActionOutcome.Cancelled, result.Outcome);
        Assert.Equal("已取消", result.Message);
    }

    // ---------- LocalCommandExecutor ----------

    [Fact]
    public void ExecutorBranch_LocalCommand_TypeContractIsPinned()
    {
        Assert.Equal("local.command", LocalCommandExecutor.DefaultType);
        Assert.Equal("local.command", new LocalCommandExecutor().ActionType);
    }

    [Fact]
    public async Task ExecutorBranch_LocalCommand_UnknownShell_FailsLoud()
    {
        var executor = new LocalCommandExecutor(Path.GetTempPath());

        var result = await executor.ExecuteAsync(
            Action("local.command", new()
            {
                ["shell"] = "fish",
                ["command"] = "echo hi",
            }), Context(), CancellationToken.None);

        Assert.False(result.Success); // BuildStartInfo 抛 ArgumentException → 启动失败分支
        Assert.Contains("未知 shell", result.Message);
    }

    [Fact]
    public async Task ExecutorBranch_LocalCommand_LogWriteFailure_StillReturnsResult()
    {
        if (!File.Exists("/bin/true")) return; // CI/本机 Linux 兜底
        var blocker = Path.Combine(Path.GetTempPath(), $"beacon-log-blocker-{Guid.NewGuid():N}");
        File.WriteAllText(blocker, "x");
        try
        {
            var executor = new LocalCommandExecutor(Path.Combine(blocker, "sub")); // 日志目录被文件占位 → IOException

            var result = await executor.ExecuteAsync(
                Action("local.command", new()
                {
                    ["shell"] = "exe",
                    ["command"] = "/bin/true",
                }), Context(), CancellationToken.None);

            Assert.True(result.Success); // 日志写失败被吞，执行结果照常返回
            Assert.Contains("退出码 0", result.Message);
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    // ---------- OpenUrlExecutor ----------

    [Fact]
    public void ExecutorBranch_OpenUrl_TypeContractIsPinned()
    {
        Assert.Equal("open.url", OpenUrlExecutor.DefaultType);
        Assert.Equal("open.url", new OpenUrlExecutor().ActionType);
    }

    [Fact]
    public async Task ExecutorBranch_OpenUrl_RealShellExecute_LaunchesTarget()
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/bin/true")) return;
        var executor = new OpenUrlExecutor();

        var result = await executor.ExecuteAsync(
            Action("open.url", new() { ["url"] = "/bin/true" }), Context(), CancellationToken.None);

        Assert.True(result.Success); // 无 opener → 真实 shell 启动可执行文件
        Assert.Equal("/bin/true", result.Message);
    }
}
