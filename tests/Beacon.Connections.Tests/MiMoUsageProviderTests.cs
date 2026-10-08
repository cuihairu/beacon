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

    private static ConnectionContext Ctx(string? secret = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:mimo"] = secret;
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
        Assert.Contains("2 模型可用", state.Summary);
        Assert.Contains("官方未开放", state.Summary);
        Assert.Equal("unavailable", state.Payload["usage_source"]); // 如实口径：不编数字
        Assert.Equal("2", state.Payload["models"]);
        // 连通性走官方 /models（Bearer 验证：MiMo key 实测 200）
        Assert.NotNull(captured);
        Assert.EndsWith("/models", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("sk-test", captured.Headers.Authorization?.Parameter);
    }

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

        var health = await provider.TestAsync(Connection(), Ctx("sk-test"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Healthy, health);
    }

    [Fact]
    public async Task ConnectionTest_InvalidKey_ReturnsDegraded()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.Unauthorized, """{"error":{"message":"invalid key"}}""");
        var provider = new MiMoConnectionProvider(http);

        var health = await provider.TestAsync(Connection(), Ctx("sk-test"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, health);
    }
}
