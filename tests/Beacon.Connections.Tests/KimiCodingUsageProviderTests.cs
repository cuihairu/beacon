using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>kimi.coding（Kimi For Coding 套餐余量，positioning P0 #5）：窗口归一化、剩余百分比阈值、Bearer 认证。</summary>
public sealed class KimiCodingUsageProviderTests
{
    private const string Body = """
        {
          "usage": { "limit": 1000, "used": 450, "remaining": 550, "resetTime": "2026-10-12T00:00:00Z" },
          "limits": [
            { "window": { "duration": 5, "timeUnit": "TIME_UNIT_HOUR" },
              "detail": { "limit": 200, "used": 150, "remaining": 50, "resetTime": "2026-10-08T15:00:00Z" } }
          ],
          "user": { "membership": { "level": "Pro" } }
        }
        """;

    private static ConnectionConfig Connection(string? endpoint = null) => new()
    {
        Id = "kimi",
        Type = "kimi",
        Endpoint = endpoint, // 可空：走默认 /coding/v1/usages
        CredentialRef = "conn:kimi",
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-kimi",
        Type = KimiWidgetDescriptors.CodingType,
        ConnectionId = "kimi",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx()
    {
        var secrets = new SecretStoreStub();
        secrets.Secrets["conn:kimi"] = "sk-kimi-demo";
        return new ConnectionContext { Secrets = secrets };
    }

    private static (KimiCodingUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new KimiCodingUsageProvider(http), http);
    }

    // ---- 响应解析 ----

    [Fact]
    public void ParseUsages_MapsRollingAndWeekly()
    {
        var usage = KimiCodingUsageProvider.ParseUsages(Body)!;

        Assert.Equal("Pro", usage.Level);
        Assert.Equal(25, usage.Rolling!.RemainingPercent!.Value, 5); // 50/200
        Assert.Equal(200, usage.Rolling.Limit);
        Assert.Equal(50, usage.Rolling.Remaining);
        Assert.Equal("2026-10-08T15:00:00Z", usage.Rolling.ResetTime);
        Assert.Equal(55, usage.Weekly!.RemainingPercent!.Value, 5); // 顶层 usage = 周限额兜底
        Assert.Equal("2026-10-12T00:00:00Z", usage.Weekly.ResetTime);
        Assert.Equal(25, usage.WorstRemainingPercent!.Value, 5);
    }

    [Fact]
    public void ParseUsages_LimitsWeeklyWinsOverTopLevel()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 1000, "remaining": 550 },
              "limits": [
                { "window": { "duration": 5, "timeUnit": "TIME_UNIT_HOUR" }, "detail": { "limit": 200, "remaining": 50 } },
                { "window": { "duration": 7, "timeUnit": "TIME_UNIT_DAY" }, "detail": { "limit": 1000, "remaining": 800 } }
              ]
            }
            """)!;

        Assert.Equal(25, usage.Rolling!.RemainingPercent!.Value, 5);
        Assert.Equal(80, usage.Weekly!.RemainingPercent!.Value, 5); // limits 里的 7 天窗优先于顶层
        Assert.Equal(25, usage.WorstRemainingPercent!.Value, 5);
    }

    [Fact]
    public void ParseUsages_SecondsWindowAndStringWindow()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "remaining": 90 },
              "limits": [
                { "window": { "duration": 18000, "timeUnit": "TIME_UNIT_SECOND" }, "detail": { "limit": 100, "remaining": 10 } },
                { "window": "weekly", "detail": { "limit": 100, "remaining": 95 } }
              ]
            }
            """)!;

        Assert.Equal(10, usage.Rolling!.RemainingPercent!.Value, 5); // 18000s → 5h
        Assert.Equal(95, usage.Weekly!.RemainingPercent!.Value, 5); // 字符串 window 兼容
    }

    [Fact]
    public void ParseUsages_UnknownWindowIgnored_FallsBackTopLevel()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "remaining": 90 },
              "limits": [
                { "window": { "duration": 3, "timeUnit": "TIME_UNIT_HOUR" }, "detail": { "limit": 100, "remaining": 5 } }
              ]
            }
            """)!;

        Assert.Null(usage.Rolling); // 3h 窗不在 5h/周 口径内，不误标
        Assert.Equal(90, usage.Weekly!.RemainingPercent!.Value, 5);
    }

    [Theory]
    [InlineData("{}")] // 无 usage
    [InlineData("[]")] // 非对象
    [InlineData("not json")]
    public void ParseUsages_InvalidPayloads_ReturnNull(string json)
    {
        Assert.Null(KimiCodingUsageProvider.ParseUsages(json));
    }

    // ---- 级别阈值（剩余口径：越低越差）----

    public static TheoryData<double?, double, double, Severity> SeverityCases => new()
    {
        { null, 30, 10, Severity.Warning }, // 无数据兜底：宁报勿漏
        { 100, 30, 10, Severity.Success },
        { 55, 30, 10, Severity.Success },
        { 30, 30, 10, Severity.Warning }, // 边界含阈值
        { 10.1, 30, 10, Severity.Warning },
        { 10, 30, 10, Severity.Error },
        { 9.9, 30, 10, Severity.Error },
        { 20, 20, 5, Severity.Warning }, // 自定义阈值
    };

    [Theory]
    [MemberData(nameof(SeverityCases))]
    public void MapSeverity_Thresholds(double? worstRemaining, double warn, double error, Severity expected)
    {
        Assert.Equal(expected, KimiCodingUsageProvider.MapSeverity(worstRemaining, warn, error));
    }

    // ---- 端到端 ----

    [Fact]
    public async Task GetStateAsync_SummaryPayloadAndBearerAuth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Warning, state!.Severity); // 5h 剩 25% ≤ 30
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal("Pro · 5h 剩25% · 周 剩55%", state.Summary);
        Assert.Equal("Pro", state.Payload["level"]);
        Assert.Equal("25", state.Payload["rolling_remaining_percent"]);
        Assert.Equal("55", state.Payload["weekly_remaining_percent"]);
        Assert.Equal("1000", state.Payload["weekly_limit"]);
        Assert.Equal("550", state.Payload["weekly_remaining"]);
        Assert.Equal("2026-10-12T00:00:00Z", state.Payload["weekly_reset"]);
        Assert.Equal("https://www.kimi.com/code", state.DetailUrl);

        var hasAuth = http.Requests[0].Headers.TryGetValues("Authorization", out var authorization);
        Assert.True(hasAuth);
        Assert.Equal("Bearer sk-kimi-demo", string.Join(",", authorization!));
        Assert.Equal(KimiCodingUsageProvider.DefaultEndpoint, http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetStateAsync_LabelOverridesHeadAndThresholdsApply()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);
        var widget = Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["label"] = "Kimi", ["warn_percent"] = "20" });

        var state = await provider.GetStateAsync(widget, Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity); // 25 > 20
        Assert.Equal("Kimi · 5h 剩25% · 周 剩55%", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_EmptyUsage_ThrowsDegraded()
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
        var provider = new KimiConnectionProvider(http);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.OK, Body);
        var healthy = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "{}");
        var unauthorized = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Healthy, healthy);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorized);
    }
}
