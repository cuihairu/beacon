using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>moonshot.balance（开放平台余额）：响应解析（数字/字符串金额）、余额阈值、Bearer 认证、整串端点（无 base+path 拆分）。</summary>
public sealed class MoonshotBalanceProviderTests
{
    private const string Body = """
        {
          "code": 0,
          "data": { "available_balance": 110.50, "voucher_balance": 0, "total_balance": 110.50 }
        }
        """;

    private static ConnectionConfig Connection(string? endpoint = null) => new()
    {
        Id = "moonshot",
        Type = "moonshot",
        Endpoint = endpoint, // 可空：整串走默认 /v1/users/me/balance
        CredentialRef = "conn:moonshot",
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-moonshot",
        Type = MoonshotWidgetDescriptors.BalanceType,
        ConnectionId = "moonshot",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx()
    {
        var secrets = new SecretStoreStub();
        secrets.Secrets["conn:moonshot"] = "sk-moonshot-001";
        return new ConnectionContext { Secrets = secrets };
    }

    private static (MoonshotBalanceProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new MoonshotBalanceProvider(http), http);
    }

    // ---- 响应解析 ----

    [Fact]
    public void ParseBalance_ReadsNumericAmount()
    {
        Assert.Equal(110.50, MoonshotBalanceProvider.ParseBalance(Body));
    }

    [Fact]
    public void ParseBalance_ReadsStringAmount()
    {
        // 官方偶发字符串金额（开放平台历史行为），与 deepseek 同款兼容
        Assert.Equal(88.20, MoonshotBalanceProvider.ParseBalance(
            """{"code":"0","data":{"available_balance":"88.20"}}"""));
    }

    [Fact]
    public void ParseBalance_InvalidPayloads_ReturnNull()
    {
        Assert.Null(MoonshotBalanceProvider.ParseBalance("{}")); // 无 data
        Assert.Null(MoonshotBalanceProvider.ParseBalance("""{"code":0}"""));
        Assert.Null(MoonshotBalanceProvider.ParseBalance("""{"data":{}}""")); // 无 available_balance
        Assert.Null(MoonshotBalanceProvider.ParseBalance("""{"data":{"available_balance":"abc"}}""")); // 非数值
        Assert.Null(MoonshotBalanceProvider.ParseBalance("not json"));
    }

    // ---- 级别阈值 ----

    public static TheoryData<double, double, double, Severity> SeverityCases => new()
    {
        { 110, 20, 5, Severity.Success },
        { 20, 20, 5, Severity.Warning }, // 边界含阈值
        { 5.01, 20, 5, Severity.Warning },
        { 5, 20, 5, Severity.Error },
        { 4.9, 20, 5, Severity.Error },
        { 25, 30, 2, Severity.Warning }, // 自定义阈值
        { 1, 30, 2, Severity.Error },
    };

    [Theory]
    [MemberData(nameof(SeverityCases))]
    public void MapSeverity_Thresholds(double balance, double warnBelow, double errorBelow, Severity expected)
    {
        Assert.Equal(expected, MoonshotBalanceProvider.MapSeverity(balance, warnBelow, errorBelow));
    }

    // ---- 摘要 ----

    [Fact]
    public void Summarize_HeadAndYuanAmount()
    {
        Assert.Equal("Moonshot · ¥110.50", MoonshotBalanceProvider.Summarize("Moonshot", 110.5));
        Assert.Equal("自定义名 · ¥7.00", MoonshotBalanceProvider.Summarize("自定义名", 7));
    }

    // ---- 端到端 ----

    [Fact]
    public async Task GetStateAsync_SummaryPayloadAndBearerAuth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("Moonshot · ¥110.50", state.Summary);
        Assert.Equal("110.50", state.Payload["available_balance"]);
        Assert.Equal("api", state.Payload["usage_source"]); // 官方余额接口直连口径
        Assert.Equal("https://platform.moonshot.cn/console/account", state.DetailUrl);

        var hasAuth = http.Requests[0].Headers.TryGetValues("Authorization", out var authorization);
        Assert.True(hasAuth);
        Assert.Equal("Bearer sk-moonshot-001", string.Join(",", authorization!));
        Assert.Equal(MoonshotBalanceProvider.DefaultEndpoint, http.Requests[0].RequestUri!.ToString()); // 整串端点，无补路径
    }

    [Fact]
    public async Task GetStateAsync_ConfiguredLabelAndThresholds()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);
        var widget = Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = "Moonshot 平台",
            ["warn_below"] = "200", // 110.50 ≤ 200 → Warning
            ["error_below"] = "50",
        });

        var state = await provider.GetStateAsync(widget, Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Warning, state!.Severity);
        Assert.Equal(LifecycleState.Running, state.Lifecycle); // Warning 态 = 运行中而非失败
        Assert.Equal("Moonshot 平台 · ¥110.50", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_LowBalanceEscalatesToError()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{"code":0,"data":{"available_balance":3.20}}""");

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity); // 3.20 ≤ 默认 error_below 5
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
    }

    [Fact]
    public async Task GetStateAsync_EmptyBalance_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, "{}");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task GetStateAsync_CustomEndpoint_UsedAsIs()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);

        await provider.GetStateAsync(Widget(), Connection("https://proxy.example.com/v1/users/me/balance"), Ctx(), CancellationToken.None);

        Assert.Equal("https://proxy.example.com/v1/users/me/balance", http.Requests[0].RequestUri!.ToString());
    }

    // ---- 连接测试 ----

    [Fact]
    public async Task TestAsync_HealthyOn2xx_UnauthorizedOn401()
    {
        var http = new FakeHttpMessageHandler();
        var provider = new MoonshotConnectionProvider(http);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, Body);
        var healthy = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "{}");
        var unauthorized = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Healthy, healthy);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorized);
    }

    [Fact]
    public async Task TestAsync_2xxWithoutBalanceData_ReturnsDegraded()
    {
        // 代理回错误体（200 假阳性）不算连通：余额接口语义校验
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, """{"error":"not a balance response"}""") };
        var provider = new MoonshotConnectionProvider(http);

        var result = await provider.TestAsync(Connection("http://127.0.0.1:18081"), Ctx(), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, result.Health);
        Assert.Contains("不含余额数据", result.Detail);
    }
}
