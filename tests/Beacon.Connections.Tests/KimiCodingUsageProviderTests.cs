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
        Assert.Null(usage.Plan); // 旧形状无 totalQuota → 无套餐窗
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
        Assert.Equal(59, usage.Plan!.RemainingPercent!.Value, 5); // 套餐窗 = totalQuota 换算
        Assert.Equal(59, usage.WorstRemainingPercent!.Value, 5); // 套餐剩 59% 最差（窗口全剩 100%）
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

    // ---- 社区口形状（2026-10-13 实抓：顶层 usages 对象 = 各窗 used_ratio，顶层 usage = 套餐）----

    [Fact]
    public void ParseUsages_CommunityShape_UsagesObjectWindowsAndPlan()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 1000, "used": 620, "remaining": 380, "resetTime": "2026-10-18T00:38:30Z" },
              "usages": {
                "limit_5h": { "used_ratio": 0.0, "reset_time": "2026-10-13T09:38:29Z" },
                "limit_7d": { "used_ratio": 0.62, "reset_time": "2026-10-18T00:38:30Z" }
              },
              "user": { "membership": { "level": "Pro" } }
            }
            """)!;

        Assert.Equal("Pro", usage.Level);
        Assert.Equal(100, usage.Rolling!.RemainingPercent!.Value, 5); // used_ratio 0 → 剩 100%
        Assert.Equal("2026-10-13T09:38:29Z", usage.Rolling.ResetTime);
        Assert.Equal(38, usage.Weekly!.RemainingPercent!.Value, 5); // used_ratio 0.62 → 剩 38%
        Assert.Equal(38, usage.Plan!.RemainingPercent!.Value, 5); // 顶层 usage = 套餐（不再当周窗）
        Assert.Equal(380, usage.Plan.Remaining);
        Assert.Equal("2026-10-18T00:38:30Z", usage.Plan.ResetTime); // resetTime 一并入套餐窗
        Assert.Equal(38, usage.WorstRemainingPercent!.Value, 5); // 周与套餐并列 38%，周优先
    }

    [Fact]
    public void ParseUsages_CommunityShape_StringDurationStillMaps5h()
    {
        // 根因回归：duration 为字符串 "300" 时 5h 窗不能被整条丢掉
        // （旧码 durationElement.TryGetInt32 只认 JSON number → 5h 窗消失 → 周窗静默夺最差位）
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "used": 85, "remaining": 15, "resetTime": "2026-10-18T00:38:30Z" },
              "limits": [
                { "window": { "duration": "300", "timeUnit": "MINUTE" },
                  "detail": { "limit": 100, "remaining": 0, "resetTime": "2026-10-13T09:38:30Z" } }
              ],
              "usages": {
                "limit_5h": { "used_ratio": 1.0, "reset_time": "2026-10-13T09:38:29Z" },
                "limit_7d": { "used_ratio": 0.8498, "reset_time": "2026-10-18T00:38:30Z" }
              }
            }
            """)!;

        Assert.Equal(0, usage.Rolling!.RemainingPercent!.Value, 5); // 5h 窗在
        Assert.Equal(100, usage.Rolling.Limit); // limits[] detail 补数值源
        Assert.Equal(0, usage.Rolling.Remaining);
        Assert.Equal(15.02, usage.Weekly!.RemainingPercent!.Value, 2); // used_ratio 0.8498 → 剩 15.02%
        Assert.Equal(15, usage.Plan!.RemainingPercent!.Value, 5);
    }

    [Fact]
    public void ParseUsages_CommunityShape_UsedRatioBeatsLimitsDetail()
    {
        // 权威口径：used_ratio 与 limits[] detail 冲突时取 used_ratio，detail 只补 limit/remaining
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "used": 50, "remaining": 50 },
              "limits": [
                { "window": { "duration": 300, "timeUnit": "TIME_UNIT_MINUTE" },
                  "detail": { "limit": 200, "remaining": 200, "resetTime": "2026-10-13T09:00:00Z" } }
              ],
              "usages": { "limit_5h": { "used_ratio": 1.0, "reset_time": "2026-10-13T09:38:29Z" } }
            }
            """)!;

        Assert.Equal(0, usage.Rolling!.RemainingPercent!.Value, 5); // used_ratio 1.0 赢过 detail 剩 100%
        Assert.Equal(200, usage.Rolling.Limit); // detail 补数值
        Assert.Equal("2026-10-13T09:38:29Z", usage.Rolling.ResetTime); // reset 取 ratio 源
        Assert.Null(usage.Weekly); // 无 limit_7d
        Assert.Equal(50, usage.Plan!.RemainingPercent!.Value, 5);
    }

    [Fact]
    public void ParseUsages_CommunityShape_UnrecognizedWindowKeyIgnored()
    {
        // 窗口名后缀非 5h/7d 的键不入摘要（不误标）
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "used": 40, "remaining": 60 },
              "usages": {
                "limit_1h": { "used_ratio": 0.9 },
                "limit_30d": { "used_ratio": 0.4 }
              }
            }
            """)!;

        Assert.Null(usage.Rolling);
        Assert.Null(usage.Weekly);
        Assert.Equal(60, usage.Plan!.RemainingPercent!.Value, 5);
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
        var usageBoth = new KimiCodingUsageProvider.KimiUsage(
            "pro",
            new KimiCodingUsageProvider.QuotaWindow(0, 100, 0, "2026-10-13"),
            new KimiCodingUsageProvider.QuotaWindow(0, 1000, 0, "2026-10-20"),
            null);
        var both = KimiCodingUsageProvider.Summarize(usageBoth);

        Assert.Contains("5h 窗口已用尽", both);
        Assert.Contains("周 窗口已用尽", both);

        var usageMixed = new KimiCodingUsageProvider.KimiUsage(
            "pro",
            new KimiCodingUsageProvider.QuotaWindow(74, 100, 74, null),
            new KimiCodingUsageProvider.QuotaWindow(0, 1000, 0, "2026-10-20"),
            null);
        var mixed = KimiCodingUsageProvider.Summarize(usageMixed);

        Assert.Contains("5h 窗口已用26%", mixed); // 有余量的窗按已用=100−剩余 反推
        Assert.Contains("周 窗口已用尽", mixed);
    }

    [Fact]
    public void Summarize_ThreeWindows_NaturalOrderWithWindowWording()
    {
        // 三窗齐显（2026-10-13 用户令）：窗序固定 5h → 周 → 套餐，窗口带「窗口」字样，套餐用「剩%」标注
        var usage = new KimiCodingUsageProvider.KimiUsage(
            "pro",
            new KimiCodingUsageProvider.QuotaWindow(100, 200, 200, "2026-10-13T09:38:29Z"),
            new KimiCodingUsageProvider.QuotaWindow(38, 1000, 380, "2026-10-18T00:38:30Z"),
            new KimiCodingUsageProvider.TotalQuota(1000, 620, 380, "2026-10-18T00:38:30Z"));

        Assert.Equal(" · 5h 窗口已用0% · 周 窗口已用62% · 套餐 剩38%",
            KimiCodingUsageProvider.Summarize(usage));
    }

    [Fact]
    public void Summarize_PlanExhausted_SaysExhausted()
    {
        var usage = new KimiCodingUsageProvider.KimiUsage(
            "pro",
            new KimiCodingUsageProvider.QuotaWindow(50, 200, 100, null),
            new KimiCodingUsageProvider.QuotaWindow(20, 1000, 200, null),
            new KimiCodingUsageProvider.TotalQuota(100, 100, 0, "2026-10-18T00:38:30Z"));

        Assert.Equal(" · 5h 窗口已用50% · 周 窗口已用80% · 套餐 已用尽",
            KimiCodingUsageProvider.Summarize(usage));
    }

    // ---- 端到端 ----

    [Fact]
    public async Task GetStateAsync_SummaryPayloadAndBearerAuth()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Warning, state!.Severity); // 级别按剩余：5h 剩 25% ≤ 30（口径分野：卡面/进度条=已用）
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal("Pro · 5h 窗口已用75% · 周 窗口已用45%", state.Summary);
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

    // ---- worst-window 口径（2026-10-13 用户令：进度条/主数值/摘要打头取已用最高窗=剩余最低窗并标窗名） ----

    [Fact]
    public async Task GetStateAsync_WeeklyRemainingLowest_ProgressAndSummaryFollowWeekly()
    {
        // 5h 剩 95%（已用 5%）、周 剩 5%（已用 95%）——旧码进度条跟 5h，修复后周窗驱动
        var body = """
            {
              "usage": { "limit": 1000, "used": 950, "remaining": 50, "resetTime": "2026-10-12T00:00:00Z" },
              "limits": [
                { "window": { "duration": 5, "timeUnit": "TIME_UNIT_HOUR" },
                  "detail": { "limit": 200, "used": 10, "remaining": 190, "resetTime": "2026-10-08T15:00:00Z" } }
              ],
              "user": { "membership": { "level": "Pro" } }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity); // 周剩 5% ≤ error 10（级别按剩余最差窗，不变）
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(0.95, state.Progress!.Value, 5); // 进度条=最差窗已用（周 95%）
        Assert.Equal("95", state.Payload["percent"]);
        Assert.Equal("weekly", state.Payload["worst_window"]);
        Assert.Equal("Pro · 5h 窗口已用5% · 周 窗口已用95%", state.Summary); // 三窗自然序（主数值/进度条另取最差窗）
    }

    [Fact]
    public async Task GetStateAsync_AllRemainingHigh_Success()
    {
        var body = """
            {
              "usage": { "limit": 1000, "used": 50, "remaining": 950, "resetTime": "2026-10-12T00:00:00Z" },
              "limits": [
                { "window": { "duration": 5, "timeUnit": "TIME_UNIT_HOUR" },
                  "detail": { "limit": 200, "used": 10, "remaining": 190, "resetTime": "2026-10-08T15:00:00Z" } }
              ],
              "user": { "membership": { "level": "Pro" } }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity); // 5h 剩 95%、周剩 95% → 全低
        Assert.Equal(0.05, state.Progress!.Value, 5); // 最差窗=5h（并列取先）已用 5%
        Assert.Equal("5", state.Payload["percent"]);
        Assert.Equal("rolling", state.Payload["worst_window"]);
    }

    [Fact]
    public async Task GetStateAsync_AllRemainingZero_ErrorFullBar()
    {
        // 双窗剩 0%（已用 100%）→ Error + 满格；并列取先（5h），「已用尽」措辞保留
        var body = """
            {
              "usage": { "limit": 1000, "used": 1000, "remaining": 0, "resetTime": "2026-10-12T00:00:00Z" },
              "limits": [
                { "window": { "duration": 5, "timeUnit": "TIME_UNIT_HOUR" },
                  "detail": { "limit": 200, "used": 200, "remaining": 0, "resetTime": "2026-10-08T15:00:00Z" } }
              ],
              "user": { "membership": { "level": "Pro" } }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity);
        Assert.Equal(1.0, state.Progress!.Value, 5);
        Assert.Equal("100", state.Payload["percent"]);
        Assert.Equal("rolling", state.Payload["worst_window"]);
        Assert.Equal("Pro · 5h 窗口已用尽 · 周 窗口已用尽", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_RealShape_SummaryPayloadAndTotalQuota()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, RealBody);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity); // 窗口全剩 100%（级别按剩余不变）
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("Kimi · 5h 窗口已用0% · 周 窗口已用0% · 套餐 剩59%", state.Summary); // 无 level → head 默认 Kimi
        Assert.Equal(0.41, state.Progress!.Value, 5); // 进度条=最差窗（套餐剩 59%）已用 41%
        Assert.Equal("41", state.Payload["percent"]);
        Assert.Equal("plan", state.Payload["worst_window"]);
        Assert.Equal("59", state.Payload["plan_remaining_percent"]);
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
        Assert.Equal("Kimi · 5h 窗口已用75% · 周 窗口已用45%", state.Summary);
    }

    // ---- 社区口三窗端到端（2026-10-13 缺陷单：5h 已用满必须赢过周窗）----

    [Fact]
    public async Task GetStateAsync_DefectFixture_5hFullUsed_WinsOverWeekly()
    {
        // 缺陷单夹具（2026-10-13 实抓）：5h used_ratio=1.0（已用满）、周 84.98%、套餐剩 15%
        // ——卡面必须显示 5h（已用 100%），不能显示周 85%
        var body = """
            {
              "usage": { "limit": 100, "used": 85, "remaining": 15, "resetTime": "2026-10-18T00:38:30Z" },
              "limits": [
                { "window": { "duration": 300, "timeUnit": "MINUTE" },
                  "detail": { "limit": 100, "remaining": 0, "resetTime": "2026-10-13T09:38:30Z" } }
              ],
              "usages": {
                "limit_5h": { "used_ratio": 1.0, "reset_time": "2026-10-13T09:38:29Z" },
                "limit_7d": { "used_ratio": 0.8498, "reset_time": "2026-10-18T00:38:30Z" }
              },
              "user": { "membership": { "level": "Pro" } }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity); // 5h 剩 0% ≤ error 10
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(1.0, state.Progress!.Value, 5); // 进度条 = 最差窗（5h）已用 100%
        Assert.Equal("100", state.Payload["percent"]); // 主数值 = 5h 已用 100%
        Assert.Equal("rolling", state.Payload["worst_window"]);
        Assert.Equal("Pro · 5h 窗口已用尽 · 周 窗口已用85% · 套餐 剩15%", state.Summary); // 三窗齐显
    }

    [Fact]
    public async Task GetStateAsync_ThreeWindowsUnequal_WorstDrivesMainValue()
    {
        // 三窗不等值（2026-10-13 用户令）：5h 已用 0%、周 已用 62%、套餐剩 38%
        // ——主数值/进度条取最差窗（周 62%），不能让用户看到 0% 以为额度归零
        var body = """
            {
              "usage": { "limit": 1000, "used": 620, "remaining": 380, "resetTime": "2026-10-18T00:38:30Z" },
              "usages": {
                "limit_5h": { "used_ratio": 0.0, "reset_time": "2026-10-13T09:38:29Z" },
                "limit_7d": { "used_ratio": 0.62, "reset_time": "2026-10-18T00:38:30Z" }
              },
              "user": { "membership": { "level": "Pro" } }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Success, state!.Severity); // 最差剩 38% > warn 30
        Assert.Equal(0.62, state.Progress!.Value, 5); // 进度条 = 周已用 62%
        Assert.Equal("62", state.Payload["percent"]);
        Assert.Equal("weekly", state.Payload["worst_window"]);
        Assert.Equal("Pro · 5h 窗口已用0% · 周 窗口已用62% · 套餐 剩38%", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_PlanStrictlyWorst_PlanDrivesMainValue()
    {
        var body = """
            {
              "usage": { "limit": 100, "used": 95, "remaining": 5, "resetTime": "2026-10-18T00:38:30Z" },
              "usages": {
                "limit_5h": { "used_ratio": 0.5, "reset_time": "2026-10-13T09:38:29Z" },
                "limit_7d": { "used_ratio": 0.8, "reset_time": "2026-10-18T00:38:30Z" }
              }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity); // 套餐剩 5% ≤ error 10
        Assert.Equal(0.95, state.Progress!.Value, 5);
        Assert.Equal("95", state.Payload["percent"]);
        Assert.Equal("plan", state.Payload["worst_window"]);
        Assert.Equal("Kimi · 5h 窗口已用50% · 周 窗口已用80% · 套餐 剩5%", state.Summary);
    }

    [Fact]
    public async Task GetStateAsync_PlanExhausted_SaysExhausted()
    {
        var body = """
            {
              "usage": { "limit": 100, "used": 100, "remaining": 0, "resetTime": "2026-10-18T00:38:30Z" },
              "usages": {
                "limit_5h": { "used_ratio": 0.1, "reset_time": "2026-10-13T09:38:29Z" },
                "limit_7d": { "used_ratio": 0.2, "reset_time": "2026-10-18T00:38:30Z" }
              }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Error, state!.Severity);
        Assert.Equal(1.0, state.Progress!.Value, 5);
        Assert.Equal("plan", state.Payload["worst_window"]);
        Assert.Equal("Kimi · 5h 窗口已用10% · 周 窗口已用20% · 套餐 已用尽", state.Summary);
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
