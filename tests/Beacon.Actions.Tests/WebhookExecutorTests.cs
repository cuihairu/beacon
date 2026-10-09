using System.Net;
using System.Security.Cryptography;
using System.Text;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Actions.Tests;

/// <summary>webhook 执行器（审计 F-07 收口）：POST + HMAC 签名 / fail closed / 超时失败路径。</summary>
public sealed class WebhookExecutorTests
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

    private sealed class SecretStoreStub : ISecretStore
    {
        public Dictionary<string, string> Secrets { get; } = new();

        public Task<string?> GetAsync(string credentialRef) => Task.FromResult(Secrets.GetValueOrDefault(credentialRef));
        public Task SetAsync(string credentialRef, string secret) { Secrets[credentialRef] = secret; return Task.CompletedTask; }
        public Task DeleteAsync(string credentialRef) { Secrets.Remove(credentialRef); return Task.CompletedTask; }
    }

    private static WidgetState SourceState(string credentialRef) => new()
    {
        WidgetId = "w-1",
        WidgetType = "github.actions.runs",
        ConnectionId = "gh-main",
        Severity = Severity.Error,
        Summary = "failed",
        Payload = new Dictionary<string, string> { ["run_id"] = "42" },
        // credentialRef 挂在连接模型上，WidgetState 只带 ConnectionId——这里经 Connection 注入
    };

    private static ActionExecutionContext Context(string? credentialRef = "conn:gh-main", bool withSecrets = true)
    {
        var secrets = new SecretStoreStub();
        secrets.Secrets["conn:gh-main"] = "whsec-001";
        return new ActionExecutionContext
        {
            SourceState = SourceState(credentialRef!),
            Connection = new ConnectionConfig
            {
                Id = "gh-main",
                Type = "github",
                CredentialRef = credentialRef,
            },
            ConnectionContext = withSecrets ? new ConnectionContext { Secrets = secrets } : null,
        };
    }

    private static ActionConfig Action(Dictionary<string, string> parameters) => new()
    {
        Id = "hook-1",
        Type = "webhook",
        RequireConfirmation = false,
        Parameters = parameters,
    };

    [Fact]
    public async Task Post_WithTemplateBody_SendsSignatureOverRenderedBody()
    {
        var capturedBody = (string?)null;
        var handler = new RecordingHandler
        {
            // request 在 executor 内被释放，body 须在响应回调里同步捕获
            Responder = request =>
            {
                capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
        };

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["url"] = "https://hook.test/notify",
                ["body"] = """{"run":"{run_id}"}""",
            }),
            Context(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("HTTP 200", result.Message);
        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("""{"run":"42"}""", capturedBody);

        var expected = $"sha256={WebhookExecutor.Sign("""{"run":"42"}""", "whsec-001")}";
        var signature = Assert.Single(request.Headers.GetValues(WebhookExecutor.SignatureHeader));
        Assert.Equal(expected, signature);
    }

    [Fact]
    public void Sign_MatchesHmacSha256HexLowercase()
    {
        // 独立用 System.Security.Cryptography 现算，锁定签名口径（GitHub X-Hub-Signature-256 同构）
        using var hmac = new HMACSHA256("whsec-001"u8.ToArray());
        var expected = Convert.ToHexString(hmac.ComputeHash("payload"u8.ToArray())).ToLowerInvariant();
        Assert.Equal(expected, WebhookExecutor.Sign("payload", "whsec-001"));
    }

    [Fact]
    public async Task SignFalse_OmitsSignatureHeader()
    {
        var handler = new RecordingHandler();

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/x", ["sign"] = "false" }),
            Context(withSecrets: false), // 连密钥仓都没有也能发——显式关签名
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(handler.LastRequest!.Headers.Contains(WebhookExecutor.SignatureHeader));
    }

    [Fact]
    public async Task SigningRequested_MissingSecret_FailsClosed()
    {
        var handler = new RecordingHandler();

        // 连接没配 credentialRef
        var noRef = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/x" }),
            Context(credentialRef: null),
            CancellationToken.None);
        Assert.False(noRef.Success);
        Assert.Contains("credentialRef", noRef.Message);

        // credentialRef 指向的密钥不存在
        var unknownRef = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/x", ["credentialRef"] = "conn:gone" }),
            Context(),
            CancellationToken.None);
        Assert.False(unknownRef.Success);
        Assert.Contains("conn:gone", unknownRef.Message);

        // 密钥仓整体缺席
        var noSecrets = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/x" }),
            Context(withSecrets: false),
            CancellationToken.None);
        Assert.False(noSecrets.Success);
    }

    [Fact]
    public async Task CredentialRefParameter_OverridesConnectionRef()
    {
        var handler = new RecordingHandler();

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["url"] = "https://hook.test/x",
                ["credentialRef"] = "conn:custom",
            }),
            Context(),
            CancellationToken.None);

        // conn:custom 不在密钥仓 → fail closed 且点名缺失 ref（证明参数覆盖生效）
        Assert.False(result.Success);
        Assert.Contains("conn:custom", result.Message);
    }

    [Fact]
    public async Task Non2xx_FailsWithStatusCode()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.BadGateway),
        };

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/fail" }),
            Context(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("502", result.Message);
    }

    [Fact]
    public async Task Timeout_FailsWithMessage()
    {
        var handler = new RecordingHandler
        {
            Responder = request => throw new OperationCanceledException(),
        };

        var result = await new WebhookExecutor(handler).ExecuteAsync(
            Action(new Dictionary<string, string> { ["url"] = "https://hook.test/slow" }),
            Context(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("超时", result.Message);
    }

    [Fact]
    public async Task MissingUrl_Fails()
    {
        var result = await new WebhookExecutor(new RecordingHandler()).ExecuteAsync(
            Action(new Dictionary<string, string>()), Context(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("url", result.Message);
    }
}
