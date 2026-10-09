using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// qwen.usage（阿里千问 百炼 Token Plan，AI Usage 第十家）：模型目录解析（官方真数据）、
/// 自定义用量端点防御键名解析（已用%/剩余%/credits/重置 ISO 与 epoch）、缺 Key/401/5xx 分级、
/// 无用量口 Info 卡如实标注、额度卡级别阈值与 Summary。
/// </summary>
public sealed class QwenUsageProviderTests
{
    private const string ModelsBody = """
        {"object":"list","data":[{"id":"qwen3-coder-plus","object":"model"},{"id":"qwen3-max","object":"model"}]}
        """;

    private const string UsageBody = """
        {"percent":42.5,"remaining_credits":1150,"total_credits":2000,"reset_at":"2026-10-19T00:00:00+08:00"}
        """;

    private static ConnectionConfig Connection(Dictionary<string, string>? settings = null, string? credentialRef = "conn:qwen", string? endpoint = null) => new()
    {
        Id = "qwen-main",
        Type = "qwen",
        CredentialRef = credentialRef,
        Endpoint = endpoint,
        Settings = settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-qwen",
        Type = QwenWidgetDescriptors.UsageType,
        ConnectionId = "qwen-main",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? secret = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:qwen"] = secret;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (QwenUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body) => Faked(_ => new FakeHttpResponse(status, body));

    private static (QwenUsageProvider Provider, FakeHttpMessageHandler Http) Faked(Func<HttpRequestMessage, FakeHttpResponse> respond)
    {
        var http = new FakeHttpMessageHandler { Responder = respond };
        return (new QwenUsageProvider(http), http);
    }

    // ---- 模型目录（官方真数据兜底） ----

    [Fact]
    public void ParseCatalog_MapsModelIds()
    {
        var catalog = QwenUsageProvider.ParseCatalog(ModelsBody);

        Assert.Equal(2, catalog.Models);
        Assert.Equal(["qwen3-coder-plus", "qwen3-max"], catalog.Ids);
    }

    [Fact]
    public void ParseCatalog_MissingData_ThrowsDegraded()
    {
        var exception = Assert.Throws<ConnectionException>(() => QwenUsageProvider.ParseCatalog("""{"other":1}"""));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task FetchModelsAsync_DefaultEndpoint_AndBearerKey()
    {
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, ModelsBody);
        });

        await provider.FetchModelsAsync(Connection(), Ctx("sk-sp-test"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1/models",
            captured!.RequestUri!.ToString());
        Assert.Equal("Bearer sk-sp-test", captured.Headers.Authorization!.ToString());
    }

    // ---- 用量解析（防御键名） ----

    [Fact]
    public void ParseUsage_FlatKeys_PercentCreditsReset()
    {
        var usage = QwenUsageProvider.ParseUsage(UsageBody)!;

        Assert.Equal(42.5, usage.PercentUsed);
        Assert.Equal("2026-10-19T00:00:00+08:00", usage.Reset);
        Assert.Equal(1150, usage.RemainingCredits);
        Assert.Equal(2000, usage.TotalCredits);
    }

    [Fact]
    public void ParseUsage_RemainingPercent_ComputesUsed()
    {
        var usage = QwenUsageProvider.ParseUsage("""{"remaining_percent":30}""")!;

        Assert.Equal(70, usage.PercentUsed);
    }

    [Fact]
    public void ParseUsage_DataEnvelope_Unwraps()
    {
        var usage = QwenUsageProvider.ParseUsage("""{"data":{"used_percent":55,"reset":1771000000}}""")!;

        Assert.Equal(55, usage.PercentUsed);
        Assert.Equal("1771000000", usage.Reset);
    }

    [Theory]
    [InlineData("""{"credits":100}""")]
    [InlineData("""{"message":"no quota fields"}""")]
    public void ParseUsage_UnrecognizedShape_ReturnsNull(string body)
        => Assert.Null(QwenUsageProvider.ParseUsage(body));

    // ---- 自定义用量端点拉取与错误分级 ----

    [Fact]
    public async Task FetchCustomUsageAsync_HappyPath_Parses()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, UsageBody);
        var connection = Connection(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_endpoint"] = "http://127.0.0.1:18081/qwen/usage",
        });

        var usage = await provider.FetchCustomUsageAsync(connection, Ctx("sk-sp-test"), "http://127.0.0.1:18081/qwen/usage", CancellationToken.None);

        Assert.Equal(42.5, usage.PercentUsed);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.Forbidden, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    public async Task FetchCustomUsageAsync_HttpError_HealthFollowsStatusClass(HttpStatusCode status, ConnectionHealthState expected)
    {
        var (provider, _) = Faked(status, """{"error":"denied"}""");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchCustomUsageAsync(Connection(), Ctx("sk-sp-test"), "http://x/usage", CancellationToken.None));

        Assert.Equal(expected, exception.Health);
    }

    [Fact]
    public async Task FetchCustomUsageAsync_UnrecognizedShape_ThrowsDegradedWithReason()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{"unexpected":1}""");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchCustomUsageAsync(Connection(), Ctx("sk-sp-test"), "http://x/usage", CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("无可识别字段", exception.Message);
    }

    [Fact]
    public async Task FetchModelsAsync_MissingCredential_ThrowsDegradedWithGuidance()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, ModelsBody);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchModelsAsync(Connection(), Ctx(secret: null), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("sk-sp-", exception.Message);
    }

    // ---- 状态渲染 ----

    [Fact]
    public void ToUsageState_SeverityProgressSummaryPayload()
    {
        var usage = QwenUsageProvider.ParseUsage(UsageBody)!;

        var state = QwenUsageProvider.ToUsageState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = "千问",
            ["warn_percent"] = "40",
        }), Connection(), usage);

        Assert.Equal(Severity.Warning, state.Severity); // 42.5 ≥ warn 40（默认 60 不触发）
        Assert.Equal(0.425, state.Progress!.Value, 5);
        Assert.Contains("本期已用 42.5%", state.Summary);
        Assert.Contains("重置 ", state.Summary);
        Assert.Equal("42.5", state.Payload["percent"]);
        Assert.Equal("1150", state.Payload["remaining_credits"]);
        Assert.Equal("2000", state.Payload["total_credits"]);
        Assert.Equal("custom", state.Payload["usage_source"]);
    }

    [Fact]
    public void ToUsageState_ThresholdBoundaries()
    {
        var warn = QwenUsageProvider.ToUsageState(Widget(), Connection(), new QwenUsageProvider.QwenPlanUsage(60, null, null, null));
        var ok = QwenUsageProvider.ToUsageState(Widget(), Connection(), new QwenUsageProvider.QwenPlanUsage(59.9, null, null, null));
        var error = QwenUsageProvider.ToUsageState(Widget(), Connection(), new QwenUsageProvider.QwenPlanUsage(90, null, null, null));

        Assert.Equal(Severity.Warning, warn.Severity);
        Assert.Equal(Severity.Success, ok.Severity);
        Assert.Equal(Severity.Error, error.Severity);
    }

    [Fact]
    public void ToCatalogState_RendersInfoCardWithHonestLabel()
    {
        var catalog = QwenUsageProvider.ParseCatalog(ModelsBody);

        var state = QwenUsageProvider.ToCatalogState(Widget(), Connection(), catalog);

        Assert.Equal(Severity.Info, state.Severity);
        Assert.Null(state.Progress); // Info 卡不渲染进度条
        Assert.Contains("官方未开放", state.Summary);
        Assert.Equal("unavailable", state.Payload["usage_source"]);
    }

    // ---- 重置时间格式化 ----

    [Theory]
    [InlineData(null, "—")]
    [InlineData("2026-10-19T00:00:00+08:00", "MM-dd")] // ISO → 本地 MM-dd HH:mm（时区相关，只断言形）
    [InlineData("1771000000", "MM-dd")]
    [InlineData("每月19日", "每月19日")] // 非时间形状原样透传
    public void FormatReset_NormalizesOrPassesThrough(string? input, string expectedShape)
    {
        var formatted = QwenUsageProvider.FormatReset(input);

        if (expectedShape == "MM-dd")
        {
            Assert.Matches(@"^\d{2}-\d{2} \d{2}:\d{2}$", formatted);
        }
        else
        {
            Assert.Equal(expectedShape, formatted);
        }
    }

    // ---- 连接测试（设置页「测试连接」） ----

    [Fact]
    public async Task ConnectionTest_HealthyModels_ReturnsHealthy()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.OK, ModelsBody);
        var provider = new QwenConnectionProvider(http);

        var result = await provider.TestAsync(Connection(), Ctx("sk-sp-test"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Healthy, result.Health);
    }

    [Fact]
    public async Task ConnectionTest_AuthFailure_ReturnsDegradedWithRealReason()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.Unauthorized, """{"error":{"message":"invalid api key"}}""");
        var provider = new QwenConnectionProvider(http);

        var result = await provider.TestAsync(Connection(), Ctx("sk-sp-bad"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, result.Health);
        Assert.Contains("401", result.Detail);
    }
}
