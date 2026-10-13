using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>kimi.coding（Kimi For Coding 套餐余量，positioning P0 #5）：真实响应形状（usages[]+
/// totalQuota+字符串数值，2026-10-11 实抓）、旧形状兼容、窗口归一化、剩余百分比阈值、Bearer/Cookie 认证。</summary>
public sealed class KimiCodingUsageProviderTests
{
    // 真实响应形状（2026-10-11 用户实抓 POST www.kimi.com/...GetUsages；数值是字符串）
    private const string RealBody = """
        {
          "usages": [
            {
              "scope": "FEATURE_CODING",
              "detail": { "limit": "100", "remaining": "100", "resetTime": "2026-10-18T00:38:30.819935Z" },
              "limits": [
                { "window": { "duration": 300, "timeUnit": "TIME_UNIT_MINUTE" },
                  "detail": { "limit": "100", "remaining": "100", "resetTime": "2026-10-11T07:38:30.819935Z" } }
              ]
            }
          ],
          "totalQuota": { "limit": "100", "used": "41", "remaining": "59" }
        }
        """;

    // 旧形状（社区端点兜底口径：顶层 usage 对象 + 数字值）
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

    // ---- 真实形状（2026-10-11 实抓）----

    [Fact]
    public void ParseUsages_RealShape_StringNumbersWindowsAndTotalQuota()
    {
        var usage = KimiCodingUsageProvider.ParseUsages(RealBody)!;

        Assert.Null(usage.Level); // 真实响应无 user.membership，head 落「Kimi」默认
        Assert.Equal(100, usage.Rolling!.RemainingPercent!.Value, 5); // 300min → 5h 窗
        Assert.Equal(100, usage.Rolling.Limit); // 字符串数值容忍
        Assert.Equal("2026-10-11T07:38:30.819935Z", usage.Rolling.ResetTime);
        Assert.Equal(100, usage.Weekly!.RemainingPercent!.Value, 5); // 条目 detail = 周期窗
        Assert.Equal("2026-10-18T00:38:30.819935Z", usage.Weekly.ResetTime);
        Assert.Equal(100, usage.Total!.Limit);
        Assert.Equal(41, usage.Total.Used);
        Assert.Equal(59, usage.Total.Remaining);
        Assert.Equal(100, usage.WorstRemainingPercent!.Value, 5);
    }

    [Fact]
    public void ParseUsages_RealShape_SelectsCodingScope_NotFirstEntry()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usages": [
                { "scope": "FEATURE_CHAT", "detail": { "limit": "999", "remaining": "1" } },
                { "scope": "FEATURE_CODING", "detail": { "limit": "100", "remaining": "50" } }
              ]
            }
            """)!;

        Assert.Equal(50, usage.Weekly!.RemainingPercent!.Value, 5); // 取 FEATURE_CODING 条目而非首个
        Assert.Null(usage.Rolling); // 该条目无 limits
    }

    [Fact]
    public void ParseUsages_RealShape_EmptyUsages_ReturnsNull()
    {
        Assert.Null(KimiCodingUsageProvider.ParseUsages("""{ "usages": [] }"""));
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

    // ---- 措辞对齐（bug 批9：余 0% 显示「已用尽」；2026-10-13 用户令卡面已用口径=剩余反推） ----

    [Fact]
    public void Summarize_ZeroRemaining_SaysExhausted()
    {
        var both = KimiCodingUsageProvider.Summarize(new KimiCodingUsageProvider.KimiUsage(
            "pro",
            new KimiCodingUsageProvider.QuotaWindow(0, 100, 0, "2026-10-13"),
            new KimiCodingUsageProvider.QuotaWindow(0, 1000, 0, "2026-10-20"),
            null));

        Assert.Contains("5h 已用尽", both);
        Assert.Contains("周 已用尽", both);

        var mixed = KimiCodingUsageProvider.Summarize(new KimiCodingUsageProvider.KimiUsage(
            "pro",
            new KimiCodingUsageProvider.QuotaWindow(74, 100, 74, null),
            new KimiCodingUsageProvider.QuotaWindow(0, 1000, 0, "2026-10-20"),
            null));

        Assert.Contains("5h 已用26%", mixed); // 有余量的窗按已用=100−剩余 反推
        Assert.Contains("周 已用尽", mixed);
    }

    // ---- 端到端 ----

    [Fact]
    public async Task GetStateAsync_SummaryPayloadAndBearerAuth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Warning, state!.Severity); // 级别按剩余：5h 剩 25% ≤ 30（口径分野：卡面/进度条=已用）
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal("Pro · 5h 已用75% · 周 已用45%", state.Summary);
        Assert.Equal(0.75, state.Progress!.Value, 5); // 进度条=已用占比（主窗 5h），与各家 provider 同语义
        Assert.Equal("Pro", state.Payload["level"]);
        Assert.Equal("75", state.Payload["rolling_percent"]); // 已用=剩余反推（payload 原始剩余键并存）
        Assert.Equal("45", state.Payload["weekly_percent"]);
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
    public async Task GetStateAsync_RealShape_SummaryPayloadAndTotalQuota()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, RealBody);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity); // 窗口全剩 100%（级别按剩余不变）
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("Kimi · 5h 已用0% · 周 已用0%", state.Summary); // 无 level → head 默认 Kimi
        Assert.Equal(0.0, state.Progress!.Value, 5); // 进度条=已用占比：全剩 → 0
        Assert.Equal("100", state.Payload["total_limit"]);
        Assert.Equal("41", state.Payload["total_used"]);
        Assert.Equal("59", state.Payload["total_remaining"]);
        Assert.Equal("100", state.Payload["weekly_limit"]); // 字符串数值归一
        Assert.Equal("2026-10-18T00:38:30.819935Z", state.Payload["weekly_reset"]);
        Assert.Equal(KimiCodingUsageProvider.DefaultEndpoint, http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetStateAsync_HttpMethodBodyAndCookieHeader_Apply()
    {
        var http = new FakeHttpMessageHandler();
        string? sentBody = null; // 请求发出后 Content 即被释放，发送时同步截留
        http.Responder = request =>
        {
            sentBody = request.Content is null ? null : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new FakeHttpResponse(HttpStatusCode.OK, RealBody);
        };
        var provider = new KimiCodingUsageProvider(http);
        var connection = Connection("https://www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages");
        connection.Settings["http_method"] = "POST";
        connection.Settings["http_body"] = "{}";
        connection.Settings["auth_header"] = "Cookie"; // 控制台会话凭据走 Cookie，无 Bearer 前缀
        connection.Settings["auth_prefix"] = "";

        var state = await provider.GetStateAsync(Widget(), connection, Ctx(), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal("POST", http.Requests[0].Method.Method);
        Assert.Equal("https://www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages",
            http.Requests[0].RequestUri!.ToString());
        Assert.Equal("{}", sentBody);
        var hasCookie = http.Requests[0].Headers.TryGetValues("Cookie", out var cookie);
        Assert.True(hasCookie);
        Assert.Equal("sk-kimi-demo", string.Join(",", cookie!)); // auth_prefix 置空 → 裸凭据
    }

    [Fact]
    public async Task GetStateAsync_LabelOverridesHeadAndThresholdsApply()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);
        var widget = Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["label"] = "Kimi", ["warn_percent"] = "20" });

        var state = await provider.GetStateAsync(widget, Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity); // 25 > 20（级别按剩余判定不变）
        Assert.Equal("Kimi · 5h 已用75% · 周 已用45%", state.Summary);
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
        var healthy = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        http.Responder = _ => new FakeHttpResponse(HttpStatusCode.Unauthorized, "{}");
        var unauthorized = (await provider.TestAsync(Connection(), Ctx(), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Healthy, healthy);
        Assert.Equal(ConnectionHealthState.Unauthorized, unauthorized);
    }
}
