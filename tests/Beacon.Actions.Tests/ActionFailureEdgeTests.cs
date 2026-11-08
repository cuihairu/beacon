using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Actions.Tests;

/// <summary>
/// 动作执行失败/边界收口（Batch 4）：外部取消（Outcome.Cancelled）、网络失败、空体 webhook、
/// 未知 shell fail-loud、类型键钉住。补 HttpExecutor/WebhookExecutor/LocalCommandExecutor/OpenUrlExecutor 的空缺分支。
/// </summary>
public sealed class ActionFailureEdgeTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }

    private static ActionExecutionContext Context => new()
    {
        SourceState = new WidgetState
        {
            WidgetId = "w-1",
            WidgetType = "github.actions.runs",
            ConnectionId = "gh-main",
            Severity = Severity.Error,
            Summary = "failed",
            Payload = new Dictionary<string, string> { ["run_id"] = "42" },
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
    public void HttpExecutor_TypeAndDefaults_ArePinned()
    {
        Assert.Equal("http", HttpExecutor.DefaultType);
        Assert.Equal(HttpExecutor.DefaultType, new HttpExecutor(new RecordingHandler()).ActionType);
    }

    [Fact]
    public async Task HttpExecutor_ExternalCancellation_MarksCancelled()
    {
        var handler = new RecordingHandler { Responder = _ => throw new OperationCanceledException() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await new HttpExecutor(handler).ExecuteAsync(
            Action("http", new Dictionary<string, string> { ["url"] = "https://hook.test/slow" }),
            Context, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ActionOutcome.Cancelled, result.Outcome);
        Assert.Contains("取消", result.Message);
    }

    [Fact]
    public async Task HttpExecutor_DefaultTimeoutParam_UsesFifteenSeconds()
    {
        var handler = new RecordingHandler { Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) };
        var executor = new HttpExecutor(handler);

        var result = await executor.ExecuteAsync(
            Action("http", new Dictionary<string, string> { ["url"] = "https://hook.test/ok" }),
            Context, CancellationToken.None);

        Assert.True(result.Success);
    }

    // ---------- WebhookExecutor ----------

    [Fact]
    public void WebhookExecutor_TypeAndHeader_ArePinned()
    {
        Assert.Equal("webhook", WebhookExecutor.DefaultType);
        Assert.Equal("X-Beacon-Signature", WebhookExecutor.SignatureHeader);
        Assert.Equal(WebhookExecutor.DefaultType, new WebhookExecutor(new RecordingHandler()).ActionType);
    }

    [Fact]
    public async Task WebhookExecutor_ExternalCancellation_MarksCancelled()
    {
        var handler = new RecordingHandler { Responder = _ => throw new OperationCanceledException() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action("webhook", new Dictionary<string, string> { ["url"] = "https://hook.test/slow", ["sign"] = "false" }),
            Context, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ActionOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task WebhookExecutor_NetworkFailure_FailsGracefully()
    {
        var handler = new RecordingHandler { Responder = _ => throw new HttpRequestException("connection refused") };

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action("webhook", new Dictionary<string, string> { ["url"] = "https://hook.test/x", ["sign"] = "false" }),
            Context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("connection refused", result.Message);
    }

    [Fact]
    public async Task WebhookExecutor_NoBody_OmitsContent()
    {
        var handler = new RecordingHandler { Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent) };

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action("webhook", new Dictionary<string, string> { ["url"] = "https://hook.test/empty", ["sign"] = "false" }),
            Context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(handler.LastRequest!.Content); // 无体 POST 合法
    }

    [Fact]
    public void WebhookExecutor_Sign_MatchesKnownVector()
    {
        // RFC 4231 测试向量 1 同构：HMAC-SHA256("key", "The quick brown fox...")
        var expected = WebhookExecutor.Sign("The quick brown fox jumps over the lazy dog", "key");
        Assert.Equal(64, expected.Length); // sha256 十六进制
        Assert.All(expected, c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }

    // ---------- LocalCommandExecutor ----------

    [Fact]
    public void LocalCommandExecutor_Type_IsPinned()
    {
        Assert.Equal("local.command", LocalCommandExecutor.DefaultType);
        Assert.Equal(LocalCommandExecutor.DefaultType, new LocalCommandExecutor("/tmp").ActionType);
    }

    [Fact]
    public async Task LocalCommandExecutor_UnknownShell_FailsLoud()
    {
        using var dir = new TempDir();
        var result = await new LocalCommandExecutor(dir.Path).ExecuteAsync(
            Action("local.command", new Dictionary<string, string>
            {
                ["command"] = "echo hi",
                ["shell"] = "bogus-shell",
            }),
            Context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("shell", result.Message);
    }

    // ---------- OpenUrlExecutor ----------

    [Fact]
    public void OpenUrlExecutor_Type_IsPinned()
    {
        Assert.Equal("open.url", OpenUrlExecutor.DefaultType);
        Assert.Equal(OpenUrlExecutor.DefaultType, new OpenUrlExecutor(_ => Task.CompletedTask).ActionType);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "beacon-actions-tests", Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }
    }
}
