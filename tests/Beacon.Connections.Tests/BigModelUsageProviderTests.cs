using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>bigmodel.usage（智谱 GLM Coding Plan 额度，positioning P0 #5）：响应解析、级别阈值、裸 Key 认证。</summary>
public sealed class BigModelUsageProviderTests
{
    private const string Body = """
        {
          "data": {
            "level": "GLM Coding Pro",
            "limits": [
              { "type": "TOKENS_LIMIT", "unit": 3, "number": 5, "percentage": 42.5, "nextResetTime": 1770000000000 },
              { "type": "TOKENS_LIMIT", "unit": 6, "number": 1, "percentage": 8, "nextResetTime": 1770100000000 },
              { "type": "TIME_LIMIT", "percentage": 3, "nextResetTime": 1771000000000 }
            ]
          }
        }
        """;

    private static ConnectionConfig Connection(string? endpoint = null) => new()
    {
        Id = "zhipu",
        Type = "bigmodel",
        Endpoint = endpoint, // 可空：走默认监控端点
        CredentialRef = "conn:zhipu",
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-glm",
        Type = BigModelWidgetDescriptors.UsageType,
        ConnectionId = "zhipu",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx()
    {
        var secrets = new SecretStoreStub();
        secrets.Secrets["conn:zhipu"] = "abc123.xyz456";
        return new ConnectionContext { Secrets = secrets };
    }

    private static (BigModelUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body) => Faked(_ => new FakeHttpResponse(status, body));

    private static (BigModelUsageProvider Provider, FakeHttpMessageHandler Http) Faked(Func<HttpRequestMessage, FakeHttpResponse> respond)
    {
        var http = new FakeHttpMessageHandler { Responder = respond };
        return (new BigModelUsageProvider(http), http);
    }

    // ---- 响应解析 ----

    [Fact]
    public void ParseUsage_MapsThreeWindows()
    {
        var usage = BigModelUsageProvider.ParseUsage(Body)!;

        Assert.Equal("GLM Coding Pro", usage.Level);
        Assert.Equal(42.5, usage.Rolling!.Percent);
        Assert.Equal(1770000000000, usage.Rolling.ResetMs);
        Assert.Equal(8, usage.Weekly!.Percent);
        Assert.Equal(3, usage.Monthly!.Percent);
        Assert.Equal(42.5, usage.WorstPercent);
    }

    [Fact]
    public void ParseUsage_AcceptsUnwrappedRoot()
    {
        var usage = BigModelUsageProvider.ParseUsage("""{"level":"Lite","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":50}]}""")!;

        Assert.Equal("Lite", usage.Level);
        Assert.Equal(50, usage.Rolling!.Percent);
        Assert.Null(usage.Weekly);
        Assert.Null(usage.Monthly); // TIME_LIMIT 未出现
        Assert.Null(usage.Rolling.ResetMs);
    }

    [Theory]
    [InlineData("{}")] // 无 limits
    [InlineData("[]")] // 非对象
    [InlineData("not json")]
    public void ParseUsage_InvalidPayloads_ReturnNull(string json)
    {
        Assert.Null(BigModelUsageProvider.ParseUsage(json));
    }

    // ---- 级别阈值 ----

    public static TheoryData<double?, double, double, Severity> SeverityCases => new()
    {
        { null, 60, 90, Severity.Warning }, // 无数据兜底：宁报勿漏
        { 42.5, 60, 90, Severity.Success },
        { 60, 60, 90, Severity.Warning }, // 边界含阈值
        { 89.9, 60, 90, Severity.Warning },
        { 90, 60, 90, Severity.Error },
        { 30, 30, 80, Severity.Warning },
    };

    [Theory]
    [MemberData(nameof(SeverityCases))]
    public void MapSeverity_Thresholds(double? worst, double warn, double error, Severity expected)
    {
        Assert.Equal(expected, BigModelUsageProvider.MapSeverity(worst, warn, error));
    }

    // ---- 端到端 ----

    [Fact]
    public async Task GetStateAsync_SummaryPayloadAndRawKeyAuth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("GLM Coding Pro · 5h 42.5% · 周 8% · 月 3%", state.Summary);
        Assert.Equal("GLM Coding Pro", state.Payload["level"]);
        Assert.Equal("42.5", state.Payload["rolling_percent"]);
        Assert.Equal("https://open.bigmodel.cn/usage", state.DetailUrl);

        // 裸 Key：Authorization 头不带 Bearer 前缀（监控接口口径）
        var hasAuth = http.Requests[0].Headers.TryGetValues("Authorization", out var authorization);
        Assert.True(hasAuth);
        Assert.Equal("abc123.xyz456", string.Join(",", authorization!));
        Assert.Equal(BigModelUsageProvider.DefaultEndpoint, http.Requests[0].RequestUri!.ToString()); // Endpoint 缺省自动补
    }

    [Fact]
    public async Task GetStateAsync_CustomEndpointAndWarnThreshold()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);
        var widget = Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["warn_percent"] = "30" });

        var state = await provider.GetStateAsync(widget, Connection(BigModelUsageProvider.ZaiEndpoint), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Warning, state!.Severity); // 42.5 ≥ 30
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal("42.5", state.Payload["rolling_percent"]);
        Assert.Equal(BigModelUsageProvider.ZaiEndpoint, http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetStateAsync_EmptyLimits_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, "{}");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task TestAsync_HealthyOn2xx_UnauthorizedOn401()
    {
        var http = new FakeHttpMessageHandler();
        var provider = new BigModelConnectionProvider(http);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, Body);
        var healthy = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "{}");
        var unauthorized = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Healthy, healthy);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorized);
    }
}
