using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// ark.usage（火山方舟 Coding Plan 额度，positioning P0 #6）：V4 签名 golden vector（测试内独立
/// HMAC 链对照）、响应解析（三窗口/字符串 Percent/ResetTimestamp 哨兵/空数组=无套餐）、级别阈值、
/// 凭据拆分与错误分级（4xx=Degraded 可修配置，5xx=Offline）。
/// </summary>
public sealed class ArkUsageProviderTests
{
    private const string AK = "AKTESTKEY";
    private const string SK = "SKTESTSECRET";

    private const string Body = """
        {
          "ResponseMetadata": { "RequestId": "req-1", "Action": "GetCodingPlanUsage" },
          "Result": {
            "Status": "Running",
            "QuotaUsage": [
              { "Level": "session", "Percent": 42.5, "ResetTimestamp": 1771000000 },
              { "Level": "weekly", "Percent": 8, "ResetTimestamp": 1772000000 },
              { "Level": "monthly", "Percent": "92", "ResetTimestamp": 0 }
            ]
          }
        }
        """;

    private static ConnectionConfig Connection(Dictionary<string, string>? settings = null, string? credentialRef = "conn:ark", string? endpoint = null) => new()
    {
        Id = "ark-main",
        Type = "ark",
        CredentialRef = credentialRef,
        Endpoint = endpoint,
        Settings = settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-ark",
        Type = ArkWidgetDescriptors.UsageType,
        ConnectionId = "ark-main",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? secret = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:ark"] = secret;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (ArkUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body) => Faked(_ => new FakeHttpResponse(status, body));

    private static (ArkUsageProvider Provider, FakeHttpMessageHandler Http) Faked(Func<HttpRequestMessage, FakeHttpResponse> respond)
    {
        var http = new FakeHttpMessageHandler { Responder = respond };
        return (new ArkUsageProvider(http), http);
    }

    // ---- V4 签名：golden vector（测试内按火山签名规范独立推导，对照 BuildAuthorization 输出） ----

    [Fact]
    public void BuildAuthorization_MatchesIndependentHmacChain()
    {
        var query = $"Action=GetCodingPlanUsage&Region=cn-beijing&Version=2024-01-01";
        var utcNow = new DateTimeOffset(2026, 10, 8, 2, 30, 0, TimeSpan.Zero);

        var authorization = ArkUsageProvider.BuildAuthorization(
            AK, SK, "open.volcengineapi.com", "cn-beijing", query, utcNow, out var xDate, out var payloadHash);

        // xDate：UTC 基本格式 yyyyMMddTHHmmssZ；payloadHash：空 body 的 SHA-256
        Assert.Equal("20261008T023000Z", xDate);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", payloadHash);

        // 独立推导（签名规范 docs.volcengine.com/docs/6369/67269：算法串无 AWS4 前缀、scope 终止符 request）
        var contentType = "application/json; charset=utf-8";
        var canonicalRequest =
            "POST\n/\n" + query + "\n" +
            $"content-type:{contentType}\n" +
            "host:open.volcengineapi.com\n" +
            $"x-content-sha256:{payloadHash}\n" +
            $"x-date:{xDate}\n" +
            "\n" +
            "content-type;host;x-content-sha256;x-date\n" +
            payloadHash;
        var stringToSign =
            $"HMAC-SHA256\n{xDate}\n20261008/cn-beijing/ark/request\n" + Sha256Hex(canonicalRequest);

        var kDate = Hmac(Encoding.UTF8.GetBytes(SK), Encoding.UTF8.GetBytes("20261008"));
        var kRegion = Hmac(kDate, Encoding.UTF8.GetBytes("cn-beijing"));
        var kService = Hmac(kRegion, Encoding.UTF8.GetBytes("ark"));
        var kSigning = Hmac(kService, Encoding.UTF8.GetBytes("request"));
        var expectedSignature = Hex(Hmac(kSigning, Encoding.UTF8.GetBytes(stringToSign)));
        var expected = $"HMAC-SHA256 Credential={AK}/20261008/cn-beijing/ark/request, " +
            "SignedHeaders=content-type;host;x-content-sha256;x-date, " +
            $"Signature={expectedSignature}";

        Assert.Equal(expected, authorization);
    }

    [Fact]
    public async Task FetchUsageAsync_SendsSignedRequestWithMatchingHeaders()
    {
        HttpRequestMessage? captured = null;
        var (provider, http) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, Body);
        });
        var connection = Connection();

        await provider.FetchUsageAsync(connection, Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        var request = captured!;
        Assert.Equal("https://open.volcengineapi.com/?Action=GetCodingPlanUsage&Region=cn-beijing&Version=2024-01-01",
            request.RequestUri!.ToString());
        var headers = request.Headers;
        Assert.True(headers.TryGetValues("X-Content-Sha256", out var shaValues));
        Assert.Equal(ArkUsageProvider.EmptyBodySha256, Assert.Single(shaValues));
        Assert.True(headers.TryGetValues("X-Date", out var dateValues));
        var xDate = Assert.Single(dateValues);
        Assert.Matches(@"^\d{8}T\d{6}Z$", xDate);
        // Authorization 引用同一 xDate/短日期，签名段 64 位 hex
        Assert.True(headers.TryGetValues("Authorization", out var authValues));
        var authorization = Assert.Single(authValues);
        Assert.StartsWith($"HMAC-SHA256 Credential={AK}/{xDate[..8]}/cn-beijing/ark/request", authorization);
        Assert.Contains("SignedHeaders=content-type;host;x-content-sha256;x-date, Signature=", authorization);
        Assert.Matches("^[0-9a-f]{64}$", authorization[(authorization.Length - 64)..]);
    }

    [Fact]
    public async Task FetchUsageAsync_RegionOverride_ComesFromConnectionSettings()
    {
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, Body);
        });
        var connection = Connection(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["region"] = "cn-shanghai" });

        await provider.FetchUsageAsync(connection, Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Contains("Region=cn-shanghai", captured!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FetchUsageAsync_EndpointOverride_ComesFromConnection()
    {
        // 端点可覆盖（区域/代理/验收 mock）——与其余 Provider 的 HttpEndpoint 口径一致
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, Body);
        });
        var connection = Connection(endpoint: "http://127.0.0.1:18081");

        await provider.FetchUsageAsync(connection, Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("http://127.0.0.1:18081/?Action=GetCodingPlanUsage&Region=cn-beijing&Version=2024-01-01",
            captured!.RequestUri!.ToString());
    }

    // ---- 凭据拆分与错误分级 ----

    [Fact]
    public async Task FetchUsageAsync_MissingCredential_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchUsageAsync(Connection(), Ctx(secret: null), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Theory]
    [InlineData("onlykey")] // 无冒号
    [InlineData(":nosecret")] // 空 AK
    [InlineData("AK:")] // 空 SK
    public async Task FetchUsageAsync_MalformedCredential_ThrowsDegraded(string secret)
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchUsageAsync(Connection(), Ctx(secret), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.Forbidden, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.BadRequest, ConnectionHealthState.Degraded)] // 火山鉴权失败用 400 InvalidAuthorization 回（2026-10-08 实测）
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.BadGateway, ConnectionHealthState.Offline)]
    public async Task FetchUsageAsync_HttpError_HealthFollowsStatusClass(HttpStatusCode status, ConnectionHealthState expected)
    {
        var (provider, _) = Faked(status, """{"ResponseMetadata":{"Error":{"Code":"InvalidAuthorization","Message":"denied"}}}""");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None));

        Assert.Equal(expected, exception.Health);
    }

    [Fact]
    public async Task FetchUsageAsync_HappyPath_ParsesQuotaWindows()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.True(usage.HasPlan);
        Assert.Equal(42.5, usage.Session!.Percent);
        Assert.Equal(8, usage.Weekly!.Percent);
        Assert.Equal(92, usage.Monthly!.Percent);
        Assert.Equal(92, usage.WorstPercent);
    }

    [Fact]
    public async Task FetchUsageAsync_StructureMissing_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{"Result":{"Other":1}}""");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // ---- 连接测试（设置页「测试连接」） ----

    [Fact]
    public async Task ConnectionTest_HealthyPlan_ReturnsHealthy()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.OK, Body);
        var provider = new ArkConnectionProvider(http);

        var health = (await provider.TestAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Healthy, health);
    }

    [Fact]
    public async Task ConnectionTest_AuthFailure_ReturnsDegraded()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.Unauthorized, """{"ResponseMetadata":{"Error":{"Code":"InvalidCredential"}}}""");
        var provider = new ArkConnectionProvider(http);

        var health = (await provider.TestAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Degraded, health);
    }

    [Fact]
    public async Task ConnectionTest_DegradedCarriesRealReason()
    {
        // 用户实测「明明连上了却报降级」：通用文案「降级（限流等）」吞掉真实原因——
        // Detail 必须带出 4xx 响应体/凭据格式/响应结构，设置页才能显示可行动的原因
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.BadRequest, """{"ResponseMetadata":{"Error":{"Code":"InvalidAuthorization"}}}""");
        var provider = new ArkConnectionProvider(http);

        var result = await provider.TestAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, result.Health);
        Assert.Contains("400", result.Detail);
        Assert.Contains("InvalidAuthorization", result.Detail);
    }

    [Fact]
    public async Task ConnectionTest_MissingCredentialCarriesRealReason()
    {
        var provider = new ArkConnectionProvider(new FakeHttpMessageHandler());

        var result = await provider.TestAsync(Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, result.Health);
        Assert.Contains("AK/SK", result.Detail);
    }

    [Fact]
    public async Task GetStateAsync_HappyPath_ReturnsRenderedState()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.Equal(ArkWidgetDescriptors.UsageType, state!.WidgetType);
        Assert.Equal("ark-main", state.ConnectionId);
        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal("monthly", state.Payload["worst_window"]);
    }

    // ---- 响应解析 ----

    [Fact]
    public void ParseUsage_MapsThreeWindows()
    {
        var usage = ArkUsageProvider.ParseUsage(Body)!;

        Assert.Equal("Running", usage.Status);
        Assert.Equal(42.5, usage.Session!.Percent);
        Assert.Equal(1771000000, usage.Session.ResetSeconds);
        Assert.Equal(8, usage.Weekly!.Percent);
        Assert.Equal(1772000000, usage.Weekly.ResetSeconds);
        Assert.Equal(92, usage.Monthly!.Percent);
        Assert.Null(usage.Monthly.ResetSeconds); // ResetTimestamp=0 哨兵：暂无重置
    }

    [Fact]
    public void ParseUsage_LevelCaseInsensitive()
    {
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"SESSION","Percent":1},{"Level":"Weekly","Percent":2},{"Level":"MONTHLY","Percent":3}]}}
            """)!;

        Assert.Equal(1, usage.Session!.Percent);
        Assert.Equal(2, usage.Weekly!.Percent);
        Assert.Equal(3, usage.Monthly!.Percent);
    }

    [Fact]
    public void ParseUsage_EmptyQuotaArray_NoPlan()
    {
        // 未订阅/已回收（Status=Reclaimed 等价形态）：空数组 → HasPlan=false，渲染「无套餐」Info 卡而非错误
        var usage = ArkUsageProvider.ParseUsage("""{"Result":{"Status":"Reclaimed","QuotaUsage":[]}}""")!;

        Assert.False(usage.HasPlan);
        Assert.Equal("Reclaimed", usage.Status);
    }

    [Fact]
    public void ParseUsage_MissingResult_ReturnsNull()
    {
        Assert.Null(ArkUsageProvider.ParseUsage("""{"ResponseMetadata":{"Error":{"Code":"X"}}}"""));
        Assert.Null(ArkUsageProvider.ParseUsage("""{"Result":{"Other":1}}""")); // 数组字段缺失
        Assert.Null(ArkUsageProvider.ParseUsage("""{"Result":{"QuotaUsage":{}}}""")); // 非数组
    }

    [Fact]
    public void ParseUsage_AcceptsDefensiveArrayNames()
    {
        // QuotaUsage 是实测真实字段；Usages/Details 为社区实现交叉验证的防御口径
        var viaUsages = ArkUsageProvider.ParseUsage("""{"Result":{"Usages":[{"Level":"session","Percent":10}]}}""")!;
        var viaDetails = ArkUsageProvider.ParseUsage("""{"Result":{"Details":[{"Level":"session","Percent":10}]}}""")!;

        Assert.Equal(10, viaUsages.Session!.Percent);
        Assert.Equal(10, viaDetails.Session!.Percent);
    }

    [Fact]
    public void ParseUsage_ItemWithoutPercentOrLevel_Skips()
    {
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Percent":5},{"Level":"weekly"},{"Level":"monthly","Percent":"33"}]}}
            """)!;

        Assert.Null(usage.Session);
        Assert.Null(usage.Weekly);
        Assert.Equal(33, usage.Monthly!.Percent);
    }

    // ---- 状态渲染 ----

    [Fact]
    public void ToState_NoPlan_RendersInfoCard()
    {
        var usage = new ArkUsageProvider.ArkPlanUsage("Reclaimed", null, null, null);

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(Severity.Info, state.Severity);
        Assert.Contains("无套餐", state.Summary);
        Assert.Null(state.Progress); // Info 卡不渲染进度条
    }

    [Fact]
    public void ToState_WorstWindowDrivesSeverityProgressAndPayload()
    {
        var usage = ArkUsageProvider.ParseUsage(Body)!;

        var state = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = "方舟",
            ["warn_percent"] = "60",
            ["error_percent"] = "90",
        }), Connection(), usage);

        // worst-window 规则（2026-10-13 用户令）：月 92 已用最高 → 级别/进度条/主数值全跟月窗
        Assert.Equal(Severity.Error, state.Severity); // 月 92 ≥ error 90
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(0.92, state.Progress!.Value, 5);
        Assert.Equal("方舟 · 月 92% · 5h 42.5% · 周 8% · 待重置", state.Summary); // 三窗明细保留，重置取最差窗（月无重置）
        Assert.Equal("92", state.Payload["percent"]);
        Assert.Equal("monthly", state.Payload["worst_window"]);
        Assert.Equal("42.5", state.Payload["rolling_percent"]);
        Assert.Equal("8", state.Payload["weekly_percent"]);
        Assert.Equal("92", state.Payload["monthly_percent"]);
        Assert.Equal("", state.Payload["reset_iso"]); // 月窗 ResetTimestamp=0 → 无重置
    }

    [Fact]
    public void ToState_NoSession_FallsBackToWorstWindow()
    {
        var usage = ArkUsageProvider.ParseUsage(
            """{"Result":{"QuotaUsage":[{"Level":"weekly","Percent":30,"ResetTimestamp":1772000000}]}}""")!;

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal("30", state.Payload["percent"]); // 无 session 时退回最差窗口（唯一窗），不空转
        Assert.Equal("weekly", state.Payload["worst_window"]);
        Assert.Contains("周 30%", state.Summary);
    }

    [Fact]
    public void ToState_CustomThresholdsApply()
    {
        // 单窗（session 42.5）下阈值作用在最差窗已用 % 上，语义不变
        var usage = ArkUsageProvider.ParseUsage(
            """{"Result":{"QuotaUsage":[{"Level":"session","Percent":42.5,"ResetTimestamp":1771000000}]}}""")!;

        var warnState = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["warn_percent"] = "40" }), Connection(), usage);
        var okState = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["warn_percent"] = "95", ["error_percent"] = "99" }), Connection(), usage);
        var errorState = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["warn_percent"] = "40", ["error_percent"] = "42" }), Connection(), usage);

        Assert.Equal(Severity.Warning, warnState.Severity);
        Assert.Equal(Severity.Success, okState.Severity);
        Assert.Equal(Severity.Error, errorState.Severity);
    }

    [Theory]
    [InlineData(0, 60, 90, Severity.Success)]
    [InlineData(59.9, 60, 90, Severity.Success)]
    [InlineData(60, 60, 90, Severity.Warning)]
    [InlineData(89.9, 60, 90, Severity.Warning)]
    [InlineData(90, 60, 90, Severity.Error)]
    [InlineData(100, 60, 90, Severity.Error)]
    public void MapSeverity_ThresholdBoundaries(double worst, double warn, double error, Severity expected)
        => Assert.Equal(expected, ArkUsageProvider.MapSeverity(worst, warn, error));

    // ---- 额度用尽口径（bug 批9 2026-10-10：周/月尽不得被新 5h 窗的低用量掩盖） ----

    [Fact]
    public void ToState_WeeklyExhaustedWhileSessionFresh_ShowsExhaustedNotHealthy()
    {
        // 用户实测形态：周额度尽（100%），新 5h 窗刚重置（3%）——旧码显示健康，修复后必须显示已尽
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"session","Percent":3,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":100,"ResetTimestamp":1772000000}]}}
            """)!;

        var state = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = "方舟",
        }), Connection(), usage);

        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(1.0, state.Progress!.Value, 5); // 已尽=满格，不显示新窗的低进度
        Assert.Equal($"方舟 · 额度用尽（周，{WeeklyResetDisplay} 重置）", state.Summary);
        Assert.Equal("100", state.Payload["percent"]);
        Assert.Equal("周", state.Payload["exhausted_window"]);
        Assert.Equal("周", state.Payload["exhausted_windows"]);
        Assert.Equal("周额度已尽", state.Payload["value_text"]); // 数值位报耗尽层，不被 5h 3% 读成「有余额」
        Assert.Contains("周度", state.Payload["exhausted_reason"]);
        Assert.Equal("3", state.Payload["rolling_percent"]); // 各窗余量仍留 payload 供 L3 详情
    }

    [Fact]
    public void ToState_MonthlyExhausted_NamesMonthlyWindow()
    {
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"session","Percent":10},{"Level":"monthly","Percent":100}]}}
            """)!;

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal("方舟 · 额度用尽（月，待重置）", state.Summary); // 无重置时间不硬造
        Assert.Equal("月", state.Payload["exhausted_window"]);
        Assert.Equal("", state.Payload["reset_iso"]);
    }

    [Fact]
    public void ToState_TwoWindowsExhausted_NamesOutermost()
    {
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"weekly","Percent":100,"ResetTimestamp":1772000000},{"Level":"monthly","Percent":100,"ResetTimestamp":1771500000}]}}
            """)!;

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal("月", state.Payload["exhausted_window"]); // 嵌套外层优先命名主摘要
        Assert.Equal("月,周", state.Payload["exhausted_windows"]);
    }

    [Fact]
    public void ToState_WeeklyResetsEarlierThanMonthly_StillNamesMonthly()
    {
        // 外层优先不看重置序：月尽（重置更晚）⇒ 周必随之耗尽，不得按重置时间偏到周（旧口径缺陷形态）
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"weekly","Percent":100,"ResetTimestamp":1771000000},{"Level":"monthly","Percent":100,"ResetTimestamp":1772000000}]}}
            """)!;

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal("月", state.Payload["exhausted_window"]);
        Assert.Equal("月,周", state.Payload["exhausted_windows"]);
    }

    [Theory]
    [InlineData("99.9", "99.9")] // 未到 100：正常路径（最差窗=周 99.9，主数值跟它）
    [InlineData("100", "100")] // 恰 100：判尽
    public void ToState_ExhaustionBoundaryAt100(string weeklyPercentText, string expectedPercent)
    {
        var json = """{"Result":{"QuotaUsage":[{"Level":"session","Percent":10,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":"PERCENT"}]}}"""
            .Replace("PERCENT", weeklyPercentText);
        var usage = ArkUsageProvider.ParseUsage(json)!;

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(expectedPercent, state.Payload["percent"]);
    }

    [Fact]
    public void ToState_WeeklyAndMonthlyAt100_SessionFresh_ExhaustedError()
    {
        // 用户令测试形态：5h 5%、周 100%、月 100%——任一窗 ≥100 走已尽分支（恒定 Error + 满格，周/月皆尽取最外层）
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"session","Percent":5,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":100,"ResetTimestamp":1772000000},{"Level":"monthly","Percent":100,"ResetTimestamp":1771500000}]}}
            """);

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal(1.0, state.Progress!.Value, 5);
        Assert.Equal("100", state.Payload["percent"]); // 已尽窗已用 %
        Assert.Equal("月", state.Payload["exhausted_window"]); // 嵌套外层优先（与重置序无关）
        Assert.Equal("月,周", state.Payload["exhausted_windows"]);
    }

    [Fact]
    public void ToState_WeeklyNearFullWhileSessionFresh_ErrorNotGreen()
    {
        // 用户实测形态：5h 5%、周 95%、月 80%——旧码按 5h 判级显绿，修复后周窗驱动 Error
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"session","Percent":5,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":95,"ResetTimestamp":1772000000},{"Level":"monthly","Percent":80,"ResetTimestamp":0}]}}
            """);

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(Severity.Error, state.Severity); // 周 95 ≥ error 90
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(0.95, state.Progress!.Value, 5);
        Assert.Equal("95", state.Payload["percent"]);
        Assert.Equal("weekly", state.Payload["worst_window"]);
        Assert.Equal($"方舟 · 周 95% · 5h 5% · 月 80% · 重置 {WeeklyResetDisplay}", state.Summary);
    }

    [Fact]
    public void ToState_AllWindowsLow_Success()
    {
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"session","Percent":10,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":20,"ResetTimestamp":1772000000},{"Level":"monthly","Percent":30,"ResetTimestamp":0}]}}
            """);

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(Severity.Success, state.Severity);
        Assert.Equal(0.3, state.Progress!.Value, 5); // 最差窗=月 30%
        Assert.Equal("30", state.Payload["percent"]);
        Assert.Equal("monthly", state.Payload["worst_window"]);
    }

    [Fact]
    public void ToState_OnlyMonthlyOver_Error()
    {
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[{"Level":"session","Percent":10,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":20,"ResetTimestamp":1772000000},{"Level":"monthly","Percent":95,"ResetTimestamp":0}]}}
            """);

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal(0.95, state.Progress!.Value, 5);
        Assert.Equal("95", state.Payload["percent"]);
        Assert.Equal("monthly", state.Payload["worst_window"]);
    }

    [Fact]
    public void ToState_ExhaustedNoteWithoutWindows_RendersGenericExhausted()
    {
        var usage = new ArkUsageProvider.ArkPlanUsage("AccountQuotaExceeded", null, null, null, ExhaustedNote: "AccountQuotaExceeded");

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Null(state.Progress); // 无窗无重置，没有可显示的进度数字
        Assert.Contains("额度用尽", state.Summary);
        Assert.Contains("AccountQuotaExceeded", state.Summary);
        Assert.Equal("1", state.Payload["exhausted"]);
    }

    [Fact]
    public async Task FetchUsageAsync_429QuotaExceededWeekly_ReturnsExhaustedWeeklyWithReset()
    {
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"ResponseMetadata":{"RequestId":"req-9","Error":{"Code":"AccountQuotaExceeded","Message":"Weekly quota of the plan is exhausted","ResetTimestamp":1771900000}}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.True(usage.HasPlan); // 已尽是有效套餐状态，不落 Degraded
        Assert.Equal(100, usage.Weekly!.Percent);
        Assert.Equal(1771900000, usage.Weekly.ResetSeconds);
        Assert.Null(usage.Session);

        // 全链路：额度卡显示「已尽 + 重置时间」，绝不显示剩余可用
        var state = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = "方舟",
        }), Connection(), usage);
        Assert.Equal($"方舟 · 额度用尽（周，{ExhaustedResetDisplay} 重置）", state.Summary);
        Assert.Equal(Severity.Error, state.Severity);
    }

    [Fact]
    public async Task FetchUsageAsync_429QuotaExceededMillisecondsReset_NormalizesToSeconds()
    {
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"ResponseMetadata":{"Error":{"Code":"QuotaExceeded","Message":"monthly quota exhausted","resetTime":1771900000000}}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.Equal(100, usage.Monthly!.Percent);
        Assert.Equal(1771900000, usage.Monthly.ResetSeconds);
    }

    [Fact]
    public async Task FetchUsageAsync_429QuotaExceededIsoResetString_ParsesIso()
    {
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"Error":{"Code":"AccountQuotaExceeded","Message":"weekly exhausted","reset_at":"2026-10-13T08:30:00Z"}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.Equal(100, usage.Weekly!.Percent);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 8, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds(), usage.Weekly.ResetSeconds);
    }

    [Fact]
    public async Task FetchUsageAsync_429QuotaExceededPlainText_ResetViaRegex()
    {
        // 非 JSON 正文（网关/代理透传的纯文本错误）也要认出已尽 + ISO 时间
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            "AccountQuotaExceeded: weekly quota exhausted, resets at 2026-10-13 08:30 UTC");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.Equal(100, usage.Weekly!.Percent);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 8, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds(), usage.Weekly.ResetSeconds);
    }

    [Fact]
    public async Task FetchUsageAsync_429QuotaExceededUnknownWindow_FallsBackToExhaustedNote()
    {
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"ResponseMetadata":{"Error":{"Code":"AccountQuotaExceeded","Message":"plan quota exhausted"}}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.True(usage.HasPlan);
        Assert.Null(usage.Session);
        Assert.Null(usage.Weekly);
        Assert.Null(usage.Monthly);
        Assert.Equal("AccountQuotaExceeded", usage.ExhaustedNote);

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);
        Assert.Contains("额度用尽", state.Summary);
        Assert.Equal(Severity.Error, state.Severity);
    }

    [Fact]
    public async Task FetchUsageAsync_429RateLimitOnly_ThrowsDegraded()
    {
        // 纯限流（无配额耗尽码）≠用尽——保持既有 4xx=Degraded 口径（同千问探针「瞬时限速不翻转」先例）
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"ResponseMetadata":{"Error":{"Code":"FlowLimitExceeded","Message":"Too many requests"}}}""");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task FetchUsageAsync_429SessionQuotaExceeded_ReturnsExhaustedSession()
    {
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"Error":{"Code":"QuotaExceeded","Message":"session quota exhausted","ResetTimestamp":1771900000}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.Equal(100, usage.Session!.Percent);
        Assert.Equal(1771900000, usage.Session.ResetSeconds);
    }

    [Fact]
    public async Task FetchUsageAsync_429EmptyBody_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests, "");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public void ParseUsage_StringPercentInvalid_SkipsWindow()
    {
        var usage = ArkUsageProvider.ParseUsage(
            """{"Result":{"QuotaUsage":[{"Level":"session","Percent":"not-a-percent"}]}}""");

        Assert.NotNull(usage);
        Assert.Null(usage.Session);
        Assert.Null(usage.Weekly);
        Assert.Null(usage.Monthly);
    }

    [Fact]
    public void NearestReset_PrefersEarliestWindow()
    {
        var usage = ArkUsageProvider.ParseUsage("""
            {"Result":{"QuotaUsage":[
              {"Level":"monthly","Percent":3,"ResetTimestamp":1773000000},
              {"Level":"session","Percent":1,"ResetTimestamp":1771000000},
              {"Level":"weekly","Percent":2,"ResetTimestamp":1772000000}
            ]}}
            """)!;

        Assert.Equal(1771000000, ArkUsageProvider.NearestReset(usage));
    }

    [Fact]
    public void NearestReset_NoTimestamps_ReturnsNull()
    {
        var usage = ArkUsageProvider.ParseUsage(
            """{"Result":{"QuotaUsage":[{"Level":"weekly","Percent":2}]}}""");

        Assert.Null(ArkUsageProvider.NearestReset(usage!));
    }

    // ---- 分支补齐：共享客户端 / 缺省凭据 / 429 数组与深度封顶 / 哨兵时间戳 / 5h 窗已尽 / 长文截断 ----

    [Fact]
    public async Task FetchUsageAsync_ParameterlessProvider_FetchesViaSharedClient()
    {
        // 参数less 构造（无注入 handler）→ L100 `_client ?? HttpEndpoint.Shared` 走共享客户端；
        // 回环 HTTP 桩做一次真实交换，验证共享客户端上整条签名请求链仍成立。
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var provider = new ArkUsageProvider();

        var fetchTask = provider.FetchUsageAsync(Connection(endpoint: $"http://127.0.0.1:{port}"), Ctx($"{AK}:{SK}"), CancellationToken.None);
        using var tcpClient = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10));
        using var stream = tcpClient.GetStream();
        var buffer = new byte[4096];
        var head = new StringBuilder();
        while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }
            head.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
        var requestText = head.ToString();
        Assert.Contains("POST", requestText, StringComparison.Ordinal); // 管控面固定 POST
        Assert.Contains("x-date:", requestText, StringComparison.OrdinalIgnoreCase); // 签名头照带

        var responseBytes = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(Body)}\r\nConnection: close\r\n\r\n{Body}");
        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

        var usage = await fetchTask;
        Assert.Equal(42.5, usage.Session!.Percent); // 共享客户端完成签名取数并解析
        listener.Stop();
    }

    [Fact]
    public async Task FetchUsageAsync_NullCredentialRef_FallsBackToDefaultSecretKey()
    {
        // CredentialRef 缺省 → 回退默认键 "ark:default"（L277 右臂）；从该键取到 AK:SK 才能成功
        var (provider, _) = Faked(HttpStatusCode.OK, Body);
        var secrets = new SecretStoreStub();
        secrets.Secrets["ark:default"] = $"{AK}:{SK}";

        var usage = await provider.FetchUsageAsync(
            Connection(credentialRef: null), new ConnectionContext { Secrets = secrets }, CancellationToken.None);

        Assert.True(usage.HasPlan);
        Assert.Equal(42.5, usage.Session!.Percent);
    }

    [Fact]
    public async Task FetchUsageAsync_429ResetInsideArrayItem_IsFound()
    {
        // 429 耗尽体里重置键在数组元素内（非顶层对象）：数组仍被递归扫描并命中（L530/L532 非空臂）
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"Error":{"Code":"QuotaExceeded","Message":"session quota exhausted","Attempts":[{"ResetTimestamp":1771900000}]}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.NotNull(usage.Session);
        Assert.Equal(100, usage.Session!.Percent);
        Assert.Equal(1771900000, usage.Session.ResetSeconds);
    }

    [Fact]
    public async Task FetchUsageAsync_429ResetBeyondDepthCap_IsIgnored()
    {
        // 重置键藏在深度 4 封顶之外：不取深层数字凑数（L532 空迭代臂）——窗口仍按 100% 判尽但重置留空
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"Error":{"Code":"QuotaExceeded","Message":"weekly quota exhausted","Trace":{"Log":{"Attempts":[{"ResetTimestamp":1771900000}]}}}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.NotNull(usage.Weekly);
        Assert.Equal(100, usage.Weekly!.Percent);
        Assert.Null(usage.Weekly.ResetSeconds);
    }

    [Fact]
    public async Task FetchUsageAsync_429EmptyArrayField_StillDetectsExhaustion()
    {
        // 429 体里空数组字段：不炸、不误判，耗尽识别照常（L530 空集合臂）
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"Error":{"Code":"AccountQuotaExceeded","Message":"plan quota exhausted","Tags":[]}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.Equal("AccountQuotaExceeded", usage.ExhaustedNote);
        Assert.Null(usage.Session);
        Assert.Null(usage.Weekly);
        Assert.Null(usage.Monthly);
    }

    [Fact]
    public async Task FetchUsageAsync_429ResetTimestampSentinel_NotTreatedAsEpoch()
    {
        // ResetTimestamp=0（哨兵/非时间戳数字）：NormalizeEpoch 返回 null（L542 null 臂），不拿来当重置时刻
        var (provider, _) = Faked(HttpStatusCode.TooManyRequests,
            """{"Error":{"Code":"QuotaExceeded","Message":"session quota exhausted","ResetTimestamp":0}}""");

        var usage = await provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None);

        Assert.NotNull(usage.Session);
        Assert.Equal(100, usage.Session!.Percent);
        Assert.Null(usage.Session.ResetSeconds);
    }

    [Fact]
    public void ToState_SessionExhausted_NamesSessionWindow()
    {
        // 5h 窗自身尽（月/周均缺）：ExhaustedWindows 的 session 非空且已尽臂（L585）→ 标「5h 窗」并满格
        var usage = ArkUsageProvider.ParseUsage(
            """{"Result":{"QuotaUsage":[{"Level":"session","Percent":100,"ResetTimestamp":1771000000}]}}""")!;

        var state = ArkUsageProvider.ToState(Widget(), Connection(), usage);

        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(1.0, state.Progress!.Value, 5);
        Assert.Equal("5h 窗", state.Payload["exhausted_window"]);
        Assert.Equal("100", state.Payload["percent"]);
        Assert.Contains("5 小时", state.Payload["exhausted_reason"]);
        Assert.Equal($"方舟 · 额度用尽（5h 窗，{ResetDisplay} 重置）", state.Summary);
    }

    [Fact]
    public async Task FetchUsageAsync_LongErrorBody_TruncatesTo180Chars()
    {
        // 超长错误体截到 180（L612 右臂），连接错误消息不被上游长文淹没
        var tail = "TAIL-BEYOND-180";
        var body = new string('a', 180) + tail;
        var (provider, _) = Faked(HttpStatusCode.BadRequest, body);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.FetchUsageAsync(Connection(), Ctx($"{AK}:{SK}"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("400", exception.Message);
        Assert.Contains(new string('a', 180), exception.Message);
        Assert.DoesNotContain(tail, exception.Message);
    }

    private static string ResetDisplay
        => DateTimeOffset.FromUnixTimeSeconds(1771000000).LocalDateTime.ToString("MM-dd HH:mm");

    private static string WeeklyResetDisplay
        => DateTimeOffset.FromUnixTimeSeconds(1772000000).LocalDateTime.ToString("MM-dd HH:mm");

    private static string ExhaustedResetDisplay
        => DateTimeOffset.FromUnixTimeSeconds(1771900000).LocalDateTime.ToString("MM-dd HH:mm");

    // —— 测试内独立 HMAC/SHA 工具（不引用被测代码的 helper） ——

    private static byte[] Hmac(byte[] key, byte[] value) => new HMACSHA256(key).ComputeHash(value);

    private static string Sha256Hex(string value) => Hex(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Hex(byte[] data) => Convert.ToHexString(data).ToLowerInvariant();
}
