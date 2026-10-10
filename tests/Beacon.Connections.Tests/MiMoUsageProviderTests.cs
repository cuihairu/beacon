using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// mimo.usage（小米 MiMo 开放平台，三档探测后如实降级）：模型目录解析、缺 Key/401/5xx 分级、
/// 无用量口时 Info 卡如实标注、自定义 usage_endpoint 原样透传。
/// </summary>
public sealed class MiMoUsageProviderTests
{
    private const string ModelsBody = """
        {"object":"list","data":[{"id":"mimo-v2.5","object":"model","owned_by":"xiaomi"},{"id":"mimo-v2.5-pro","object":"model","owned_by":"xiaomi"}]}
        """;

    private static ConnectionConfig Connection(Dictionary<string, string>? settings = null, string? credentialRef = "conn:mimo") => new()
    {
        Id = "mimo-main",
        Type = "mimo",
        CredentialRef = credentialRef,
        Settings = settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-mimo",
        Type = MiMoWidgetDescriptors.UsageType,
        ConnectionId = "mimo-main",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? secret = null, string? consoleCookie = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:mimo"] = secret;
        }
        if (consoleCookie is not null)
        {
            secrets.Secrets["mimo:console"] = consoleCookie; // 档① 控制台登录态（默认 ref）
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (MiMoUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body) => Faked(_ => new FakeHttpResponse(status, body));

    private static (MiMoUsageProvider Provider, FakeHttpMessageHandler Http) Faked(Func<HttpRequestMessage, FakeHttpResponse> respond)
    {
        var http = new FakeHttpMessageHandler { Responder = respond };
        return (new MiMoUsageProvider(http), http);
    }

    // ---- 模型目录（官方真数据） ----

    [Fact]
    public void ParseCatalog_MapsModelIds()
    {
        var catalog = MiMoUsageProvider.ParseCatalog(ModelsBody);

        Assert.Equal(2, catalog.Models);
        Assert.Equal(["mimo-v2.5", "mimo-v2.5-pro"], catalog.Ids);
    }

    [Fact]
    public void ParseCatalog_MissingDataArray_ThrowsDegraded()
    {
        var exception = Assert.Throws<ConnectionException>(() => MiMoUsageProvider.ParseCatalog("""{"object":"list"}"""));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task GetStateAsync_NoUsageEndpoint_ReportsCatalogWithHonestUsageLabel()
    {
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, ModelsBody);
        });

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("sk-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Info, state!.Severity);
        Assert.Contains("2 模型可用", state.Summary); // 模型数留次行信息
        Assert.Contains("plan-manage", state.Summary); // 批6 用户令：URL 页口径说明（套餐用量去控制台看）
        Assert.Equal("unavailable", state.Payload["usage_source"]); // 如实口径：不编数字
        Assert.Equal("无额度口", state.Payload["value_text"]); // 额度位显式文本，模型数不冒充额度
        Assert.Equal("https://platform.xiaomimimo.com/console/plan-manage", state.DetailUrl);
        // 连通性走官方 /models（Bearer 验证：MiMo key 实测 200）
        Assert.NotNull(captured);
        Assert.EndsWith("/models", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("sk-test", captured.Headers.Authorization?.Parameter);
    }

    // ---- 档① 控制台套餐用量（plan-manage 页口径，批6 用户令：已用/总量/百分比） ----

    [Fact]
    public async Task GetStateAsync_ConsoleCookiePresent_FetchesPlanUsageWithCookieHeader()
    {
        const string consoleBody = """{"code":200,"msg":"操作成功","data":{"used_tokens":124801805513,"total_tokens":132000000000}}""";
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, consoleBody);
        });

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("sk-test", consoleCookie: "ph-session-value"), CancellationToken.None);

        // 控制台 API：默认 plan-manage 接口 + api-platform_ph 头携带登录 cookie（真 Key 实测 Bearer 不认）
        Assert.NotNull(captured);
        Assert.Equal("/api/v1/tokenPlan/usage", captured!.RequestUri!.AbsolutePath);
        Assert.True(captured.Headers.TryGetValues("api-platform_ph", out var cookie));
        Assert.Equal("ph-session-value", string.Join(",", cookie));

        Assert.NotNull(state);
        Assert.Equal("console_api", state!.Payload["usage_source"]);
        Assert.Equal("94.5", state.Payload["percent"]); // 124801805513/132000000000 自算
        Assert.Equal("94.5%", state.Payload["value_text"]);
        Assert.Equal(0.945, state.Progress!.Value, 3);
        Assert.Contains("已用 1248亿 / 1320亿", state.Summary); // 中文数量级口径
        Assert.Contains("94.5%", state.Summary);
        Assert.Equal(Severity.Warning, state.Severity); // ≥90 快用完提醒，未超限不 Error
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("https://platform.xiaomimimo.com/console/plan-manage", state.DetailUrl);
    }

    [Fact]
    public async Task GetStateAsync_ConsolePercentOnly_KeepsAbsoluteFieldsEmpty()
    {
        // 响应只给百分比时不编绝对数（口径如实）
        var (provider, _) = Faked(_ => new FakeHttpResponse(HttpStatusCode.OK, """{"data":{"percentage":95}}"""));

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("sk-test", consoleCookie: "ph"), CancellationToken.None);

        Assert.Equal("95", state!.Payload["percent"]);
        Assert.Equal("", state.Payload["used"]);
        Assert.Equal("", state.Payload["total"]);
        Assert.DoesNotContain("已用", state.Summary); // 无绝对数就不摆「已用 X / Y」句式
        Assert.Contains("95%（控制台套餐用量）", state.Summary);
    }

    [Theory]
    [InlineData("""{"used_tokens":124801805513,"total_tokens":132000000000}""", 94.5)] // 裸字段自算（data 包裹剥开）
    [InlineData("""{"data":{"percentage":95,"used":"100","total":"400"}}""", 95)] // 百分比优先 + 字符串数字
    [InlineData("""{"data":{"usedPercent":88}}""", 88)] // 防御键名
    [InlineData("""{"code":200,"data":{"currentValue":30,"quota":100}}""", 30)] // 控制台 usage/currentValue 风格
    public void ParseConsoleUsage_DefensiveShapes(string body, double expectedPercent)
        => Assert.Equal(expectedPercent, MiMoUsageProvider.ParseConsoleUsage(body)!.Percent);

    [Theory]
    [InlineData("""{"code":200,"msg":"操作成功"}""")] // 无任何用量键
    [InlineData("""not json""")]
    public void ParseConsoleUsage_Unrecognized_ReturnsNull(string body)
        => Assert.Null(MiMoUsageProvider.ParseConsoleUsage(body));

    [Fact]
    public async Task GetStateAsync_ConsoleUnparseable_ThrowsDegradedWithBody()
    {
        var (provider, _) = Faked(_ => new FakeHttpResponse(HttpStatusCode.OK, """{"code":200,"msg":"操作成功"}"""));

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("sk-test", consoleCookie: "ph"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health); // 显式失败：键名需按真会话核对，不静默降档
    }

    [Fact]
    public async Task GetStateAsync_ConsoleUnauthorized_SuggestsCookieRefresh()
    {
        var (provider, _) = Faked(_ => new FakeHttpResponse(HttpStatusCode.Unauthorized, """{"code":401,"loginUrl":"https://account.xiaomi.com/pass/serviceLogin"}"""));

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("sk-test", consoleCookie: "expired"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("api-platform_ph", exception.Message); // 指引：回浏览器重取 cookie
    }

    [Fact]
    public async Task GetStateAsync_ConsoleCookieWinsOverLocalCounter()
    {
        // 档优先级：控制台真实套餐用量 > 本机计数（用户令：plan-manage 口径替代）
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, """{"data":{"percentage":95}}""");
        });
        var connection = Connection(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_endpoint"] = "https://count.local/mimo/usage",
        });

        var state = await provider.GetStateAsync(Widget(), connection, Ctx("sk-test", consoleCookie: "ph"), CancellationToken.None);

        Assert.Equal("/api/v1/tokenPlan/usage", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("console_api", state!.Payload["usage_source"]);
    }

    [Fact]
    public async Task GetStateAsync_ConsoleOverLimit_ErrorSeverity()
    {
        var (provider, _) = Faked(_ => new FakeHttpResponse(HttpStatusCode.OK, """{"data":{"percentage":100,"used":132000000000,"total":132000000000}}"""));

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("sk-test", consoleCookie: "ph"), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
    }

    // ---- 大数中文口径 ----

    [Theory]
    [InlineData(124801805513, "1248亿")]
    [InlineData(132000000000, "1320亿")]
    [InlineData(99999999, "99,999,999")] // 亿位以下千分位原样
    [InlineData(150000000, "1.5亿")]
    public void FormatTokens_ChineseMagnitude(double value, string expected)
        => Assert.Equal(expected, MiMoUsageProvider.FormatTokens(value));

    [Fact]
    public async Task GetStateAsync_CountShapedUsage_ShowsLocalCalls()
    {
        var usageUrl = "https://count.local/mimo/usage";
        var (provider, http) = Faked(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/usage")
                ? new FakeHttpResponse(HttpStatusCode.OK, """{"total_calls":128,"window_calls":6,"window_minutes":60}""")
                : new FakeHttpResponse(HttpStatusCode.OK, ModelsBody));

        var connection = Connection(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_endpoint"] = usageUrl,
        });

        var state = await provider.GetStateAsync(Widget(), connection, Ctx("sk-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Contains("本机累计 128 次", state!.Summary);
        Assert.Contains("近60分 6 次", state.Summary);
        Assert.Contains("官方无额度接口", state.Summary); // 数据源注明
        Assert.Equal("128", state.Payload["total_calls"]);
        Assert.Equal("local_count", state.Payload["usage_source"]);
        Assert.Equal(usageUrl, state.Payload["endpoint"]);
    }

    [Theory]
    [InlineData("""{"calls_total": 7}""", 7)] // 防御键名
    [InlineData("""{"data":{"count":3}}""", 3)] // 包裹层剥开
    [InlineData("""{"total_calls":"55"}""", 55)] // 字符串数字
    public void ParseLocalCount_DefensiveKeys(string body, long expected)
        => Assert.Equal(expected, MiMoUsageProvider.ParseLocalCount(body)!.TotalCalls);

    [Theory]
    [InlineData("""{"used": 5, "limit": 10}""")] // 非计数形状
    [InlineData("""not json""")]
    public void ParseLocalCount_Unrecognized_ReturnsNull(string body)
        => Assert.Null(MiMoUsageProvider.ParseLocalCount(body));

    [Fact]
    public async Task GetStateAsync_CustomUsageEndpoint_PassthroughBody()
    {
        var usageUrl = "https://token-plan-cn.xiaomimimo.com/v1/usage/custom";
        var (provider, http) = Faked(request =>
            new FakeHttpResponse(HttpStatusCode.OK, """{"used": 5, "limit": 10}"""));

        var state = await provider.GetStateAsync(
            Widget(),
            Connection(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["usage_endpoint"] = usageUrl }),
            Ctx("sk-test"),
            CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal("custom", state!.Payload["usage_source"]);
        Assert.Contains(usageUrl, state.Payload["endpoint"]);
        Assert.Contains("used", state.Payload["body"]); // 原样透传，不加工
    }

    // ---- 凭据与错误分级 ----

    [Fact]
    public async Task GetStateAsync_MissingKey_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, ModelsBody);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(secret: null), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.Forbidden, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Degraded)] // 推理域无 /usage 路由实测 404 → 配置侧可查
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    public async Task GetStateAsync_HttpError_HealthFollowsStatusClass(HttpStatusCode status, ConnectionHealthState expected)
    {
        var (provider, _) = Faked(status, "upstream error");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("sk-test"), CancellationToken.None));

        Assert.Equal(expected, exception.Health);
    }

    // ---- 连接测试 ----

    [Fact]
    public async Task ConnectionTest_ValidKey_ReturnsHealthy()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.OK, ModelsBody);
        var provider = new MiMoConnectionProvider(http);

        var health = (await provider.TestAsync(Connection(), Ctx("sk-test"), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Healthy, health);
    }

    [Fact]
    public async Task ConnectionTest_InvalidKey_ReturnsDegraded()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.Unauthorized, """{"error":{"message":"invalid key"}}""");
        var provider = new MiMoConnectionProvider(http);

        var health = (await provider.TestAsync(Connection(), Ctx("sk-test"), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Degraded, health);
    }
}
