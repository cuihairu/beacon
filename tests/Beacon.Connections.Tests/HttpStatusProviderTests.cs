using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>Generic HTTP Provider（positioning P0 #3）：点路径提取、状态词映射、认证头注入、健康语义。</summary>
public sealed class HttpStatusProviderTests
{
    private const string Body = """{"status":"building","message":"Game-A #123","html_url":"https://ci.local/builds/1","nested":{"list":[{"state":"failed"}]}}""";

    private static ConnectionConfig Connection(string? credentialRef = null, Dictionary<string, string>? settings = null) => new()
    {
        Id = "ci-local",
        Type = "http",
        Endpoint = "http://ci.local/api/status",
        CredentialRef = credentialRef,
        Settings = settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w1",
        Type = HttpWidgetDescriptors.StatusType,
        ConnectionId = "ci-local",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? token = null)
    {
        var secrets = new SecretStoreStub();
        if (token is not null)
        {
            secrets.Secrets["conn:ci"] = token;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (HttpStatusProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body) => Faked(_ => new FakeHttpResponse(status, body));

    private static (HttpStatusProvider Provider, FakeHttpMessageHandler Http) Faked(Func<HttpRequestMessage, FakeHttpResponse> respond)
    {
        var http = new FakeHttpMessageHandler { Responder = respond };
        return (new HttpStatusProvider(http), http);
    }

    // ---- HttpJsonPath 点路径提取 ----

    [Theory]
    [InlineData("$.status", "building")]
    [InlineData("$.message", "Game-A #123")]
    [InlineData("$.nested.list[0].state", "failed")]
    [InlineData("nested.list[0].state", "failed")] // 允许省略 $
    [InlineData("$.missing", null)]
    [InlineData("$.nested.list[5].state", null)] // 越界
    [InlineData("$.status.deep", null)] // 标量继续下钻 → null
    [InlineData("$.html_url", "https://ci.local/builds/1")]
    public void Select_DotPath(string path, string? expected)
    {
        using var document = System.Text.Json.JsonDocument.Parse(Body);

        Assert.Equal(expected, HttpJsonPath.Select(document.RootElement, path));
    }

    [Fact]
    public void Select_NullOrEmptyPath_ReturnsNull()
    {
        using var document = System.Text.Json.JsonDocument.Parse(Body);

        Assert.Null(HttpJsonPath.Select(document.RootElement, null));
        Assert.Null(HttpJsonPath.Select(document.RootElement, "  "));
    }

    // ---- 状态词→级别映射（默认词表，positioning.md §3 口径） ----

    public static TheoryData<string?, LifecycleState, Severity> StatusMappingCases => new()
    {
        { "running", LifecycleState.Running, Severity.Warning },
        { "BUILDING", LifecycleState.Running, Severity.Warning }, // 大小写不敏感
        { " queued ", LifecycleState.Running, Severity.Warning }, // 容忍首尾空白
        { "success", LifecycleState.Success, Severity.Success },
        { "ok", LifecycleState.Success, Severity.Success },
        { "healthy", LifecycleState.Success, Severity.Success },
        { "failed", LifecycleState.Failed, Severity.Error },
        { "critical", LifecycleState.Failed, Severity.Error },
        { null, LifecycleState.Unknown, Severity.Warning }, // 空兜底：宁报勿漏
        { "whatever", LifecycleState.Unknown, Severity.Warning }, // 未知词兜底
    };

    [Theory]
    [MemberData(nameof(StatusMappingCases))]
    public void MapStatus_DefaultBuckets(string? status, LifecycleState expectedLifecycle, Severity expectedSeverity)
    {
        var widget = Widget();

        var (lifecycle, severity) = HttpStatusProvider.MapStatus(
            status,
            HttpStatusProvider.SuccessValues(widget),
            HttpStatusProvider.WarningValues(widget),
            HttpStatusProvider.ErrorValues(widget));

        Assert.Equal(expectedLifecycle, lifecycle);
        Assert.Equal(expectedSeverity, severity);
    }

    [Fact]
    public void MapStatus_CustomValues_OverrideDefaults()
    {
        var widget = Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["success_values"] = "green, idle",
            ["warning_values"] = "warmup",
            ["error_values"] = "dead",
        });
        HashSet<string> Success() => HttpStatusProvider.SuccessValues(widget);
        HashSet<string> Warning() => HttpStatusProvider.WarningValues(widget);
        HashSet<string> Error() => HttpStatusProvider.ErrorValues(widget);

        var (idle, _) = HttpStatusProvider.MapStatus("idle", Success(), Warning(), Error());
        var (warmup, _) = HttpStatusProvider.MapStatus("warmup", Success(), Warning(), Error());
        var (dead, severity) = HttpStatusProvider.MapStatus("dead", Success(), Warning(), Error());
        // 错误词优先判定（fail-loud）：同词挂两桶按失败处理
        var (dup, dupSeverity) = HttpStatusProvider.MapStatus("dead", Success(), Warning(), new HashSet<string> { "dead", "idle" });

        Assert.Equal(LifecycleState.Success, idle);
        Assert.Equal(LifecycleState.Running, warmup);
        Assert.Equal(LifecycleState.Failed, dead);
        Assert.Equal(Severity.Error, severity);
        Assert.Equal(LifecycleState.Failed, dup);
        Assert.Equal(Severity.Error, dupSeverity);
    }

    // ---- 端到端：夹具接口 → WidgetState ----

    [Fact]
    public async Task GetStateAsync_MapsStatusAndSummary()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);
        var widget = Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = "打包机A",
            ["summary_path"] = "$.message",
            ["url_path"] = "$.html_url",
        });

        var state = await provider.GetStateAsync(widget, Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(LifecycleState.Running, state!.Lifecycle);
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("打包机A · Game-A #123", state.Summary);
        Assert.Equal("https://ci.local/builds/1", state.DetailUrl);
        Assert.Equal("building", state.Payload["status"]);
        Assert.Equal("http://ci.local/api/status", http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetStateAsync_DefaultSummaryUsesLabelAndStatus()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{"status":"success"}""");

        var state = await provider.GetStateAsync(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["label"] = "打包机A" }), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("打包机A · success", state!.Summary);
        Assert.Equal(Severity.Success, state.Severity);
    }

    [Fact]
    public async Task GetStateAsync_InjectsAuthHeader()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);
        var credential = Connection("conn:ci");

        // 默认 Authorization: Bearer <token>
        _ = await provider.GetStateAsync(Widget(), credential, Ctx("s3cret"), CancellationToken.None);
        var hasAuth = http.Requests[0].Headers.TryGetValues("Authorization", out var authorization);

        Assert.True(hasAuth);
        Assert.Equal("Bearer s3cret", string.Join(",", authorization!));

        // 自定义认证头与空前缀（如 X-Token: <token>）
        var (provider2, http2) = Faked(HttpStatusCode.OK, Body);
        var connection = Connection("conn:ci", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auth_header"] = "X-Token",
            ["auth_prefix"] = "",
        });
        _ = await provider2.GetStateAsync(Widget(), connection, Ctx("s3cret"), CancellationToken.None);
        var hasCustom = http2.Requests[0].Headers.TryGetValues("X-Token", out var custom);

        Assert.True(hasCustom);
        Assert.Equal("s3cret", string.Join(",", custom!));
    }

    // ---- 健康语义 ----

    [Fact]
    public async Task GetStateAsync_Unauthorized_ThrowsUnauthorized()
    {
        var (provider, _) = Faked(HttpStatusCode.Unauthorized, "{}");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection("conn:ci"), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Unauthorized, exception.Health);
    }

    [Fact]
    public async Task GetStateAsync_ServerError_ThrowsOffline()
    {
        var (provider, _) = Faked(HttpStatusCode.InternalServerError, "{}");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Offline, exception.Health);
    }

    [Fact]
    public async Task GetStateAsync_NonJson_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, "<html>not json</html>");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task GetStateAsync_MissingEndpoint_ThrowsDegraded()
    {
        var provider = new HttpStatusProvider();
        var connection = new ConnectionConfig { Id = "ci-local", Type = "http", Endpoint = null };

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), connection, Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task TestAsync_HealthyOn2xx_UnauthorizedOn401()
    {
        var http = new FakeHttpMessageHandler();
        var provider = new HttpConnectionProvider(http);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, "{}");
        var healthy = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "{}");
        var unauthorized = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Healthy, healthy);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorized);
    }

    [Fact]
    public void Descriptors_IconField_CarriesPickerChoicesAllResolvable()
    {
        // 自选图标（用户令 2026-10-10：图标要自带可选）——icon 字段 Choices 全部能渲染
        foreach (var descriptor in HttpWidgetDescriptors.All)
        {
            var icon = Assert.Single(descriptor.Fields, f => f.Key == "icon");
            Assert.NotNull(icon.Choices);
            Assert.True(icon.Choices!.Count >= 16);
            foreach (var choice in icon.Choices)
            {
                Assert.True(Beacon.Core.Services.BrandIcons.TryGet(choice, out var path), $"{descriptor.Type} 选择项 {choice} 无 path");
                Assert.False(string.IsNullOrWhiteSpace(path));
            }
        }
    }
}
