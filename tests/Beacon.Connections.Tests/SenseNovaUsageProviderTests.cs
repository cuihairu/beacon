using System.Globalization;
using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// sensenova.tokenplan（商汤控制台 Token Plan 积分池额度，2026-10-13 用户令）：真实响应夹具逐字段解析
/// （字符串小数/千分位/epoch 哨兵）、多池最差窗三态（5h 胜 / 7d 胜 / 跨池胜）、卡面主数值与
/// 摘要多池齐显、401 → 显式 Degraded「控制台 token 已过期」、赠额到期预警（≤10% 或 7 天内）。
/// </summary>
public sealed class SenseNovaUsageProviderTests
{
    /// <summary>用户 2026-10-13 实抓响应（plan + 两池：通用积分池 / Flash-Lite积分池）。</summary>
    private const string PoolUsageBody = """
        {
          "plan": { "id": "free", "name": "Free Plan", "type": "TOKEN_PLAN_PLAN_TYPE_FREE" },
          "pools": [
            {
              "id": "pool_f2f9196e", "name": "通用积分池",
              "model_ids": ["deepseek-flash", "glm-5.2", "kimi-k3", "sensenova-6.8-flash-lite"],
              "window_5h": { "limit": "60000", "used": "27997.056", "remaining": "32002.944", "reset_at": "1791899156" },
              "window_7d": { "limit": "600000", "used": "27997.056", "remaining": "572002.944", "reset_at": "1792485956" },
              "grant_balance": "17610.794", "nearest_grant_expiry": "1794474000",
              "nearest_grant_expiring_balance": "17610.794", "pool_type": "default"
            },
            {
              "id": "pool_2afaf48e", "name": "Flash-Lite积分池",
              "model_ids": ["sensenova-6.7-flash-lite", "sensenova-6.8-flash-lite"],
              "window_5h": { "limit": "60000", "used": "60000", "remaining": "0", "reset_at": "1791899156" },
              "window_7d": { "limit": "600000", "used": "60000", "remaining": "540000", "reset_at": "1792485956" },
              "grant_balance": "0", "nearest_grant_expiry": "0",
              "nearest_grant_expiring_balance": "0", "pool_type": "dedicated"
            }
          ]
        }
        """;

    private static ConnectionConfig Connection(string? credentialRef = "conn:sensenova", string? endpoint = null) => new()
    {
        Id = "sensenova-main",
        Type = "sensenova",
        CredentialRef = credentialRef,
        Endpoint = endpoint,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-sensenova",
        Type = SenseNovaWidgetDescriptors.UsageType,
        ConnectionId = "sensenova-main",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? secret = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:sensenova"] = secret;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (SenseNovaUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new SenseNovaUsageProvider(http), http);
    }

    // ---- 解析：真实夹具逐字段 ----

    [Fact]
    public void ParseUsage_Fixture_PoolsWindowsAndGrants()
    {
        var usage = SenseNovaUsageProvider.ParseUsage(PoolUsageBody)!;
        Assert.NotNull(usage);
        Assert.Equal("Free Plan", usage.PlanName);
        Assert.Equal(2, usage.Pools.Count);

        var general = usage.Pools[0];
        Assert.Equal("通用积分池", general.Name);
        Assert.Equal("通用池", general.ShortName);
        Assert.Equal(46.66, general.Window5h!.UsedPercent!.Value, 2);
        Assert.Equal(4.67, general.Window7d!.UsedPercent!.Value, 2);
        Assert.Equal(1791899156, general.Window5h.ResetSeconds);
        Assert.Equal(17610.794, general.GrantBalance!.Value, 3);
        Assert.Equal(1794474000, general.NearestGrantExpirySeconds);
        Assert.Equal("default", general.PoolType);

        var flash = usage.Pools[1];
        Assert.Equal("Flash-Lite池", flash.ShortName);
        Assert.Equal(100, flash.Window5h!.UsedPercent!.Value, 2);
        Assert.Equal(10, flash.Window7d!.UsedPercent!.Value, 2);
        Assert.Equal(0, flash.GrantBalance); // "0" = 值 0（赠额为空），非「未给」
        Assert.Null(flash.NearestGrantExpirySeconds); // "0" 哨兵 → null（无到期）
    }

    [Fact]
    public void ParseUsage_StringDecimalsWithThousandsSeparator()
    {
        const string body = """
            { "pools": [ { "id": "p1", "name": "池",
                "window_5h": { "limit": "60,000", "used": "27,997.056", "remaining": "32,002.944", "reset_at": "1791899156" } } ] }
            """;
        var usage = SenseNovaUsageProvider.ParseUsage(body)!;
        Assert.Equal(46.66, usage.Pools[0].Window5h!.UsedPercent!.Value, 2);
    }

    [Fact]
    public void ParseUsage_UsedMissing_FallsBackToLimitMinusRemaining()
    {
        const string body = """
            { "pools": [ { "id": "p1", "name": "池",
                "window_5h": { "limit": "100", "remaining": "25" } } ] }
            """;
        var usage = SenseNovaUsageProvider.ParseUsage(body)!;
        Assert.Equal(75, usage.Pools[0].Window5h!.UsedPercent!.Value, 2);
    }

    [Fact]
    public void ParseUsage_NumericAndEpochMillis_Normalized()
    {
        const string body = """
            { "pools": [ { "id": "p1", "name": "池",
                "window_5h": { "limit": 100, "used": 50, "reset_at": 1791899156000 } } ] }
            """;
        var usage = SenseNovaUsageProvider.ParseUsage(body)!;
        Assert.Equal(50, usage.Pools[0].Window5h!.UsedPercent!.Value, 2);
        Assert.Equal(1791899156, usage.Pools[0].Window5h!.ResetSeconds); // 毫秒折秒
    }

    [Fact]
    public void ParseUsage_MissingPools_ReturnsNull()
    {
        Assert.Null(SenseNovaUsageProvider.ParseUsage("""{ "plan": { "name": "Free Plan" } }"""));
        Assert.Null(SenseNovaUsageProvider.ParseUsage("""{ "pools": "nope" }"""));
    }

    [Fact]
    public void ParseUsage_EmptyPoolsArray_ReturnsEmptyPlan()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""{ "pools": [] }""")!;
        Assert.NotNull(usage);
        Assert.Empty(usage.Pools);
    }

    // ---- 最差窗三态 ----

    [Fact]
    public void WorstWindow_5hHigher_Picks5h()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""
            { "pools": [ { "id": "p1", "name": "池",
                "window_5h": { "limit": "100", "used": "90" }, "window_7d": { "limit": "100", "used": "10" } } ] }
            """)!;
        var (pool, key, label, window) = usage.Worst()!.Value;
        Assert.Equal("p1", pool.Id);
        Assert.Equal("5h", key);
        Assert.Equal("5h", label);
        Assert.Equal(90, window.UsedPercent!.Value, 2);
    }

    [Fact]
    public void WorstWindow_7dHigher_Picks7d()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""
            { "pools": [ { "id": "p1", "name": "池",
                "window_5h": { "limit": "100", "used": "10" }, "window_7d": { "limit": "100", "used": "90" } } ] }
            """)!;
        var (_, key, _, window) = usage.Worst()!.Value;
        Assert.Equal("7d", key);
        Assert.Equal(90, window.UsedPercent!.Value, 2);
    }

    [Fact]
    public void WorstWindow_SecondPoolHigher_PicksThatPool()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""
            { "pools": [
              { "id": "p1", "name": "通用积分池", "window_5h": { "limit": "100", "used": "10" } },
              { "id": "p2", "name": "Flash-Lite积分池", "window_5h": { "limit": "100", "used": "95" } } ] }
            """)!;
        var (pool, _, _, window) = usage.Worst()!.Value;
        Assert.Equal("p2", pool.Id);
        Assert.Equal("Flash-Lite池", pool.ShortName);
        Assert.Equal(95, window.UsedPercent!.Value, 2);
    }

    [Fact]
    public void WorstWindow_Tie_Prefers5hThenFirstPool()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""
            { "pools": [
              { "id": "p1", "name": "池", "window_5h": { "limit": "100", "used": "50" }, "window_7d": { "limit": "100", "used": "50" } },
              { "id": "p2", "name": "池2", "window_5h": { "limit": "100", "used": "50" } } ] }
            """)!;
        var (pool, key, _, _) = usage.Worst()!.Value;
        Assert.Equal("p1", pool.Id);
        Assert.Equal("5h", key);
    }

    [Fact]
    public void WorstWindow_NoWindowsAtAll_ReturnsNull()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""{ "pools": [ { "id": "p1", "name": "池" } ] }""")!;
        Assert.Null(usage.Worst());
    }

    // ---- 卡面：主数值 / 进度条 / 级别 / 摘要 ----

    [Fact]
    public async Task GetStateAsync_Fixture_WorstPoolWindowDrivesCard()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, PoolUsageBody);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("console-jwt"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(1.0, state.Progress);
        Assert.Equal("Flash-Lite池 5h 已用100%", state.Payload["value_text"]);
        Assert.Equal("Flash-Lite池", state.Payload["worst_pool"]);
        Assert.Equal("5h", state.Payload["worst_window"]);
        Assert.Equal("100", state.Payload["percent"]);
        Assert.Equal("2", state.Payload["pool_count"]);
        Assert.Equal("通用池 5h 47% · 7d 5% / Flash-Lite池 5h 100% · 7d 10%", state.Payload["pools"]);
        Assert.Equal("商汤 · 通用池 5h 47% · 7d 5% / Flash-Lite池 5h 100% · 7d 10% · 剩余 17610.794 · 11-12 到期", state.Summary);
        Assert.Equal("17610.794", state.Payload["grant_balance"]);
        Assert.Equal("11-12", state.Payload["grant_expiry"]);
        Assert.Equal("https://platform.sensenova.cn", state.DetailUrl);
    }

    [Fact]
    public async Task GetStateAsync_5hWorst_CardShows5h()
    {
        const string body = """
            { "pools": [ { "id": "p1", "name": "通用积分池",
                "window_5h": { "limit": "100", "used": "88" }, "window_7d": { "limit": "100", "used": "12" } } ] }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal("通用池 5h 已用88%", state.Payload["value_text"]);
        Assert.Equal("5h", state.Payload["worst_window"]);
        Assert.Equal(0.88, state.Progress);
        Assert.Equal(Severity.Warning, state.Severity); // 88 ≥ warn 80
    }

    [Fact]
    public async Task GetStateAsync_7dWorst_CardShows7d()
    {
        const string body = """
            { "pools": [ { "id": "p1", "name": "通用积分池",
                "window_5h": { "limit": "100", "used": "12" }, "window_7d": { "limit": "100", "used": "96" } } ] }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal("通用池 7d 已用96%", state.Payload["value_text"]);
        Assert.Equal("7d", state.Payload["worst_window"]);
        Assert.Equal(Severity.Error, state.Severity); // 96 ≥ error 95
    }

    [Fact]
    public async Task GetStateAsync_LabelOverridesHead()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, PoolUsageBody);
        var state = await provider.GetStateAsync(
            Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["label"] = "商汤主号" }),
            Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.StartsWith("商汤主号 · ", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_ThresholdsConfigurable()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, PoolUsageBody);
        var state = await provider.GetStateAsync(
            Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["error_percent"] = "101" }),
            Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal(Severity.Warning, state.Severity); // 100 < 101 → 不再 Error
    }

    // ---- 赠额到期预警 ----

    [Fact]
    public async Task GetStateAsync_GrantExpiringWithin7Days_Warns()
    {
        var soon = DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var (provider, _) = Faked(HttpStatusCode.OK, $$"""
            { "pools": [ { "id": "p1", "name": "通用积分池",
                "window_5h": { "limit": "100", "used": "10" },
                "grant_balance": "1000", "nearest_grant_expiry": "{{soon}}", "nearest_grant_expiring_balance": "900" } ] }
            """);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("1", state.Payload["grant_expiring_soon"]);
    }

    [Fact]
    public async Task GetStateAsync_ExpiringBalanceUnder10Percent_Warns()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """
            { "pools": [ { "id": "p1", "name": "通用积分池",
                "window_5h": { "limit": "100", "used": "10" },
                "grant_balance": "1000", "nearest_grant_expiry": "1894474000", "nearest_grant_expiring_balance": "50" } ] }
            """);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("1", state.Payload["grant_expiring_soon"]);
    }

    [Fact]
    public async Task GetStateAsync_GrantHealthy_NoWarning()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, PoolUsageBody);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal("", state.Payload["grant_expiring_soon"]);
        Assert.Equal(Severity.Error, state.Severity); // 窗口 100% 主导，不被赠额路径改写
    }

    // ---- 边界态 ----

    [Fact]
    public async Task GetStateAsync_EmptyPools_InfoCard()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{ "pools": [] }""");
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal(LifecycleState.Unknown, state.Lifecycle);
        Assert.Null(state.Progress);
        Assert.Contains("无积分池", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_PoolsWithoutWindows_InfoCard()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{ "pools": [ { "id": "p1", "name": "池" } ] }""");
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal(Severity.Info, state.Severity);
        Assert.Null(state.Progress);
        Assert.Contains("无窗口额度数据", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_MalformedJson_Degraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, "not json");
        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task GetStateAsync_MissingPoolsKey_Degraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{ "plan": { "name": "Free Plan" } }""");
        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("jwt"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("不含积分池数据", exception.Message);
    }

    // ---- 401：控制台 JWT 过期 ----

    [Fact]
    public async Task GetStateAsync_401_ExplicitTokenExpiredDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.Unauthorized, """{ "error": "Invalid access token" }""");
        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("expired-jwt"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("控制台 token 已过期", exception.Message);
        Assert.Contains("重新获取", exception.Message);
    }

    [Fact]
    public async Task ConnectionProvider_401_ReportsTokenExpired()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "{}") };
        var result = await new SenseNovaConnectionProvider(http).TestAsync(Connection(), Ctx("expired-jwt"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, result.Health);
        Assert.Contains("控制台 token 已过期", result.Detail);
    }

    [Fact]
    public async Task ConnectionProvider_Ok()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, PoolUsageBody) };
        var result = await new SenseNovaConnectionProvider(http).TestAsync(Connection(), Ctx("jwt"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Healthy, result.Health);
        Assert.Null(result.Detail);
    }

    [Fact]
    public async Task ConnectionProvider_UsesBearerConsoleJwt()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, PoolUsageBody) };
        await new SenseNovaConnectionProvider(http).TestAsync(Connection(), Ctx("console-jwt"), CancellationToken.None);

        var request = Assert.Single(http.Requests);
        Assert.Equal("Bearer console-jwt", request.Headers.Authorization?.ToString());
        Assert.Equal(SenseNovaUsageProvider.DefaultEndpoint, request.RequestUri?.ToString());
    }

    [Fact]
    public async Task GetStateAsync_EndpointOverride_Honored()
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, PoolUsageBody) };
        var provider = new SenseNovaUsageProvider(http);
        await provider.GetStateAsync(Widget(), Connection(endpoint: "https://proxy.example.com/pool"), Ctx("jwt"), CancellationToken.None);

        Assert.Equal("https://proxy.example.com/pool", Assert.Single(http.Requests).RequestUri?.ToString());
    }
}
