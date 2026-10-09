using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>deepseek.balance（开放平台余额，positioning P0 #5）：响应解析（字符串金额）、余额阈值、Bearer 认证。</summary>
public sealed class DeepSeekBalanceProviderTests
{
    private const string Body = """
        {
          "is_available": true,
          "balance_infos": [
            { "currency": "CNY", "total_balance": "110.00", "granted_balance": "10.00", "topped_up_balance": "100.00" }
          ]
        }
        """;

    private static ConnectionConfig Connection(string? endpoint = null) => new()
    {
        Id = "deepseek",
        Type = "deepseek",
        Endpoint = endpoint, // 可空：走默认 /user/balance
        CredentialRef = "conn:deepseek",
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-deepseek",
        Type = DeepSeekWidgetDescriptors.BalanceType,
        ConnectionId = "deepseek",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx()
    {
        var secrets = new SecretStoreStub();
        secrets.Secrets["conn:deepseek"] = "sk-deepseek-001";
        return new ConnectionContext { Secrets = secrets };
    }

    private static (DeepSeekBalanceProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new DeepSeekBalanceProvider(http), http);
    }

    // ---- 响应解析 ----

    [Fact]
    public void ParseBalance_ReadsStringAmounts()
    {
        var balance = DeepSeekBalanceProvider.ParseBalance(Body)!;

        Assert.True(balance.IsAvailable);
        Assert.Equal("CNY", balance.Currency);
        Assert.Equal(110.00, balance.Total);
        Assert.Equal(10.00, balance.Granted);
        Assert.Equal(100.00, balance.ToppedUp);
    }

    [Fact]
    public void ParseBalance_NumberAmountsAndAbsentFlag()
    {
        var balance = DeepSeekBalanceProvider.ParseBalance(
            """{"balance_infos":[{"currency":"USD","total_balance":4.5}]}""")!;

        Assert.True(balance.IsAvailable); // is_available 缺省按可用
        Assert.Equal("USD", balance.Currency);
        Assert.Equal(4.5, balance.Total);
        Assert.Null(balance.Granted);
    }

    [Theory]
    [InlineData("{}")] // 无 balance_infos
    [InlineData("""{"balance_infos":[]}""")] // 空数组
    [InlineData("""{"balance_infos":[{"currency":"CNY"}]}""")] // 条目无 total_balance
    [InlineData("not json")]
    public void ParseBalance_InvalidPayloads_ReturnNull(string json)
    {
        Assert.Null(DeepSeekBalanceProvider.ParseBalance(json));
    }

    // ---- 级别阈值 ----

    public static TheoryData<bool, double, double, double, Severity> SeverityCases => new()
    {
        { true, 110, 20, 5, Severity.Success },
        { true, 20, 20, 5, Severity.Warning }, // 边界含阈值
        { true, 5.01, 20, 5, Severity.Warning },
        { true, 5, 20, 5, Severity.Error },
        { true, 4.9, 20, 5, Severity.Error },
        { false, 110, 20, 5, Severity.Error }, // is_available=false 直接报错
        { true, 5, 10, 2, Severity.Warning }, // 自定义阈值
    };

    [Theory]
    [MemberData(nameof(SeverityCases))]
    public void MapSeverity_Thresholds(bool isAvailable, double total, double warnBelow, double errorBelow, Severity expected)
    {
        Assert.Equal(expected, DeepSeekBalanceProvider.MapSeverity(isAvailable, total, warnBelow, errorBelow));
    }

    // ---- 摘要 ----

    [Fact]
    public void Summarize_CurrencySymbolsAndGrantedNote()
    {
        var cny = DeepSeekBalanceProvider.ParseBalance(Body)!;
        Assert.Equal("DeepSeek · ¥110.00（含赠 ¥10.00）", DeepSeekBalanceProvider.Summarize("DeepSeek", cny));

        var usd = DeepSeekBalanceProvider.ParseBalance("""{"balance_infos":[{"currency":"USD","total_balance":"4.50"}]}""")!;
        Assert.Equal("DeepSeek · $4.50", DeepSeekBalanceProvider.Summarize("DeepSeek", usd));

        var eur = DeepSeekBalanceProvider.ParseBalance("""{"balance_infos":[{"currency":"EUR","total_balance":"7"}]}""")!;
        Assert.Equal("DeepSeek · 7.00 EUR", DeepSeekBalanceProvider.Summarize("DeepSeek", eur));
    }

    // ---- 端到端 ----

    [Fact]
    public async Task GetStateAsync_SummaryPayloadAndBearerAuth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("DeepSeek · ¥110.00（含赠 ¥10.00）", state.Summary);
        Assert.Equal("CNY", state.Payload["currency"]);
        Assert.Equal("110.00", state.Payload["total"]);
        Assert.Equal("api", state.Payload["usage_source"]); // 官方余额接口直连口径
        Assert.Equal("https://platform.deepseek.com/usage", state.DetailUrl);

        var hasAuth = http.Requests[0].Headers.TryGetValues("Authorization", out var authorization);
        Assert.True(hasAuth);
        Assert.Equal("Bearer sk-deepseek-001", string.Join(",", authorization!));
        Assert.Equal(DeepSeekBalanceProvider.DefaultEndpoint + "/user/balance", http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetStateAsync_LowBalanceEscalates()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"3.20"}]}""");

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity); // 3.20 ≤ 5
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
    }

    [Fact]
    public async Task GetStateAsync_InsufficientBalanceFlag_IsError()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{"is_available":false,"balance_infos":[{"currency":"CNY","total_balance":"999"}]}""");

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity);
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
    public async Task TestAsync_HealthyOn2xx_UnauthorizedOn401()
    {
        var http = new FakeHttpMessageHandler();
        var provider = new DeepSeekConnectionProvider(http);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, Body);
        var healthy = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "{}");
        var unauthorized = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Healthy, healthy);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorized);
    }
}
