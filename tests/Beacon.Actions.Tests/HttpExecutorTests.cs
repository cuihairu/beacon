using System.Net;
using Beacon.Core.Models;

namespace Beacon.Actions.Tests;

/// <summary>B-405 验收：成功/失败/超时路径（假 Handler 等价本地 stub）。</summary>
public sealed class HttpExecutorTests
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

    private static readonly ActionExecutionContext Context = new()
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

    private static ActionConfig Action(Dictionary<string, string> parameters) => new()
    {
        Id = "webhook-1",
        Type = "http",
        RequireConfirmation = false,
        Parameters = parameters,
    };

    [Fact]
    public async Task Get_2xx_Succeeds_WithRenderedUrl()
    {
        var handler = new RecordingHandler();
        var executor = new HttpExecutor(handler);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/runs/{run_id}/ack" }),
            Context,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("HTTP 200", result.Message);
        Assert.Equal("https://hook.test/runs/42/ack", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Post_WithHeadersAndBody_SendsAll()
    {
        var capturedBody = (string?)null;
        var handler = new RecordingHandler
        {
            // request 在 executor 内被释放，body 须在响应回调里同步捕获
            Responder = request =>
            {
                capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.Created);
            },
        };
        var executor = new HttpExecutor(handler);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["url"] = "https://hook.test/api",
                ["method"] = "POST",
                ["body"] = """{"run":"{run_id}"}""",
                ["header.X-Token"] = "secret-token",
            }),
            Context,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("HTTP 201", result.Message);
        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("secret-token", request.Headers.GetValues("X-Token").Single());
        Assert.Contains("\"run\":\"42\"", capturedBody);
    }

    [Fact]
    public async Task Non2xx_FailsWithStatusCode()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
        };
        var executor = new HttpExecutor(handler);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/fail" }),
            Context,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("500", result.Message);
    }

    [Fact]
    public async Task Timeout_FailsWithMessage()
    {
        var handler = new RecordingHandler
        {
            Responder = request => throw new OperationCanceledException(),
        };
        var executor = new HttpExecutor(handler);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/slow" }),
            Context,
            CancellationToken.None);

        // Responder 抛 OCE：请求 token 未取消 → 归类为超时
        Assert.False(result.Success);
        Assert.Contains("超时", result.Message);
    }

    [Fact]
    public async Task NetworkFailure_FailsGracefully()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => throw new HttpRequestException("connection refused"),
        };
        var executor = new HttpExecutor(handler);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/x" }),
            Context,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("connection refused", result.Message);
    }

    [Fact]
    public async Task MissingPlaceholder_FailsClosed()
    {
        var executor = new HttpExecutor(new RecordingHandler());

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/{nope}" }),
            Context,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("nope", result.Message);
    }

    [Fact]
    public async Task MissingUrl_Fails()
    {
        var executor = new HttpExecutor(new RecordingHandler());

        var result = await executor.ExecuteAsync(Action(new Dictionary<string, string>()), Context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("url", result.Message);
    }
}
