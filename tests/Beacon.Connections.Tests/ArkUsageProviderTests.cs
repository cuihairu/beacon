using System.Net;
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

    private static ConnectionConfig Connection(Dictionary<string, string>? settings = null, string? credentialRef = "conn:ark") => new()
    {
        Id = "ark-main",
        Type = "ark",
        CredentialRef = credentialRef,
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

        Assert.Equal(Severity.Error, state.Severity); // 最差窗口 92 ≥ error 90
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(0.92, state.Progress);
        Assert.Equal("方舟 · 5h 42.5% · 周 8% · 月 92%", state.Summary);
        Assert.Equal("92", state.Payload["percent"]);
        Assert.Equal("42.5", state.Payload["rolling_percent"]);
        Assert.Equal("8", state.Payload["weekly_percent"]);
        Assert.Equal("92", state.Payload["monthly_percent"]);
        Assert.Equal(ResetDisplay, state.Payload["reset_iso"]); // 1771000000 → 最近重置（三窗口中仅 session/weekly 有）
    }

    [Fact]
    public void ToState_CustomThresholdsApply()
    {
        var usage = ArkUsageProvider.ParseUsage(Body)!; // WorstPercent = 92

        var warnState = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["error_percent"] = "95" }), Connection(), usage);
        var okState = ArkUsageProvider.ToState(Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["warn_percent"] = "95", ["error_percent"] = "99" }), Connection(), usage);

        Assert.Equal(Severity.Warning, warnState.Severity);
        Assert.Equal(Severity.Success, okState.Severity);
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

    private static string ResetDisplay
        => DateTimeOffset.FromUnixTimeSeconds(1771000000).LocalDateTime.ToString("MM-dd HH:mm");

    // —— 测试内独立 HMAC/SHA 工具（不引用被测代码的 helper） ——

    private static byte[] Hmac(byte[] key, byte[] value) => new HMACSHA256(key).ComputeHash(value);

    private static string Sha256Hex(string value) => Hex(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Hex(byte[] data) => Convert.ToHexString(data).ToLowerInvariant();
}
