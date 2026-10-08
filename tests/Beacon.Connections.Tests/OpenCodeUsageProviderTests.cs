using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// opencode.usage（OpenCode Go，AI Usage 第九家，档①官方 Console Budgets API 直连）：micro-cents 解析与
/// 美元换算、无上限/超限/阈值分级、多成员聚合、缺 Key/401/403/404/5xx 分级、连接测试两态。
/// </summary>
public sealed class OpenCodeUsageProviderTests
{
    /// <summary>实测形状（2026-10-09 本机 200）：个人工作区单成员、无上限、本月零消费、11-01 重置。</summary>
    private const string BudgetsBody = """
        [{"user_id":"user_01M3VJV5VMQ8Y43AGPEW2K0CN0","email":"chuihairu@gmail.com","limit_micro_cents":null,"spent_micro_cents":"0","exceeded":false,"resets_at":"2026-11-01T00:00:00.000Z","source":null,"updated_at":null}]
        """;

    /// <summary>$100.00 上限（官方口径 1 美元 = 1 亿 micro-cents）。</summary>
    private const long HundredDollars = 10_000_000_000;

    private static ConnectionConfig Connection(string? endpoint = null, string? credentialRef = "conn:opencode") => new()
    {
        Id = "opencode-main",
        Type = "opencode",
        Endpoint = endpoint,
        CredentialRef = credentialRef,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-opencode",
        Type = OpenCodeWidgetDescriptors.UsageType,
        ConnectionId = "opencode-main",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? secret = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:opencode"] = secret;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (OpenCodeUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body) => Faked(_ => new FakeHttpResponse(status, body));

    private static (OpenCodeUsageProvider Provider, FakeHttpMessageHandler Http) Faked(Func<HttpRequestMessage, FakeHttpResponse> respond)
    {
        var http = new FakeHttpMessageHandler { Responder = respond };
        return (new OpenCodeUsageProvider(http), http);
    }

    private static string MemberBody(long? limitMicroCents, long spentMicroCents, bool exceeded = false, string resetsAt = "2026-11-01T00:00:00.000Z")
        => $$"""
            [{"user_id":"user_1","email":"dev@example.com","limit_micro_cents":{{(limitMicroCents is null ? "null" : $"\"{limitMicroCents}\"")}},"spent_micro_cents":"{{spentMicroCents}}","exceeded":{{(exceeded ? "true" : "false")}},"resets_at":"{{resetsAt}}","source":null,"updated_at":null}]
            """;

    // ---- 解析 ----

    [Fact]
    public void ParseBudgets_MapsMicrocentsStringsAndNulls()
    {
        var budgets = OpenCodeUsageProvider.ParseBudgets(BudgetsBody);

        var budget = Assert.Single(budgets);
        Assert.Null(budget.LimitMicroCents); // null=无上限
        Assert.Equal(0, budget.SpentMicroCents);
        Assert.False(budget.Exceeded);
        Assert.Equal("2026-11-01T00:00:00.000Z", budget.ResetsAt);
    }

    [Fact]
    public void ParseBudgets_NonArrayOrEmpty_ThrowsDegraded()
    {
        Assert.Equal(ConnectionHealthState.Degraded,
            Assert.Throws<ConnectionException>(() => OpenCodeUsageProvider.ParseBudgets("""{"error":"nope"}""")).Health);
        Assert.Equal(ConnectionHealthState.Degraded,
            Assert.Throws<ConnectionException>(() => OpenCodeUsageProvider.ParseBudgets("[]")).Health);
    }

    // ---- 卡面（官方真数字） ----

    [Fact]
    public async Task GetStateAsync_UnlimitedBaseline_ShowsSpentWithoutCap()
    {
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, BudgetsBody);
        });

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state!.Severity); // 无上限=Success 基线，不造虚数
        Assert.Contains("OpenCode Go $10/月", state.Summary); // 套餐为展示层标注（默认名）
        Assert.Contains("$0.00 · 无上限", state.Summary);
        Assert.Contains("11-01 重置", state.Summary);
        Assert.Equal("api", state.Payload["usage_source"]); // 档①官方 Console API 直连
        Assert.Equal("0.00", state.Payload["spent_usd"]);
        Assert.Equal("", state.Payload["limit_micro_cents"]);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("oc_sk-test", captured.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetStateAsync_LimitedBudget_ShowsSpentOverCap()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, MemberBody(HundredDollars, spentMicroCents: 2_500_000_000)); // $25 / $100

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Contains("$25.00 / $100.00", state.Summary);
        Assert.Equal("10000000000", state.Payload["limit_micro_cents"]);
    }

    [Theory]
    [InlineData(8_000_000_000, Severity.Warning)] // $80 / $100 = 80% = 默认 warn 阈值
    [InlineData(9_500_000_000, Severity.Error)]    // $95 / $100 = 95% = 默认 error 阈值
    public async Task GetStateAsync_LimitedBudget_CrossesThresholds(long spentMicroCents, Severity expected)
    {
        var (provider, _) = Faked(HttpStatusCode.OK, MemberBody(HundredDollars, spentMicroCents));

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(expected, state!.Severity);
    }

    [Fact]
    public async Task GetStateAsync_ExceededFlag_IsErrorEvenUnderThreshold()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, MemberBody(HundredDollars, spentMicroCents: 1_000_000_000, exceeded: true)); // $10/100 但官方已标超

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Error, state!.Severity); // 官方 exceeded 一票 Error（以官方口径为准）
        Assert.Equal("true", state.Payload["exceeded"]);
    }

    [Fact]
    public async Task GetStateAsync_MultipleMembers_AggregatesSpentAndAnyNullLimitWins()
    {
        var body = $$"""
            [
              {"user_id":"user_1","email":"a@example.com","limit_micro_cents":"5000000000","spent_micro_cents":"1000000000","exceeded":false,"resets_at":"2026-11-01T00:00:00.000Z","source":null,"updated_at":null},
              {"user_id":"user_2","email":"b@example.com","limit_micro_cents":null,"spent_micro_cents":"2000000000","exceeded":false,"resets_at":"2026-11-01T00:00:00.000Z","source":null,"updated_at":null}
            ]
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Contains("$30.00 · 无上限", state.Summary); // 聚合消费 $10+$20；任一 null 即无上限
        Assert.Equal("3000000000", state.Payload["spent_micro_cents"]);
        Assert.Equal("2", state.Payload["members"]);
    }

    [Fact]
    public async Task GetStateAsync_CustomEndpoint_UsesConnectionEndpoint()
    {
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, BudgetsBody);
        });

        await provider.GetStateAsync(Widget(), Connection("https://opencode.internal/console/api/v1/budgets/members"), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.Equal("opencode.internal", captured!.RequestUri!.Host);
    }

    [Fact]
    public void ResetDisplay_IsoTimestampUtcFormatted_FallbackRawAndAbsent()
    {
        Assert.Equal(" · 11-01 重置", OpenCodeUsageProvider.ResetDisplay("2026-11-01T00:00:00.000Z"));
        Assert.Equal(" · 03-01 重置", OpenCodeUsageProvider.ResetDisplay("2027-03-01T00:00:00.000Z"));
        Assert.Equal(" · soon 重置", OpenCodeUsageProvider.ResetDisplay("soon")); // 非日期原样，不编
        Assert.Equal("", OpenCodeUsageProvider.ResetDisplay(null)); // 没有就不显示该段
    }

    // ---- 凭据与错误分级 ----

    [Fact]
    public async Task GetStateAsync_MissingKey_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, BudgetsBody);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(secret: null), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.Forbidden, ConnectionHealthState.Degraded)] // inference-only Key 读不了 Budgets（需 All 权限）
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    public async Task GetStateAsync_HttpError_HealthFollowsStatusClass(HttpStatusCode status, ConnectionHealthState expected)
    {
        var (provider, _) = Faked(status, "upstream error");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("oc_sk-test"), CancellationToken.None));

        Assert.Equal(expected, exception.Health);
    }

    // ---- 连接测试 ----

    [Fact]
    public async Task ConnectionTest_ValidKey_ReturnsHealthy()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.OK, BudgetsBody);
        var provider = new OpenCodeConnectionProvider(http);

        var health = await provider.TestAsync(Connection(), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Healthy, health);
    }

    [Fact]
    public async Task ConnectionTest_InvalidKey_ReturnsDegraded()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.Unauthorized, """{"error":"invalid key"}""");
        var provider = new OpenCodeConnectionProvider(http);

        var health = await provider.TestAsync(Connection(), Ctx("oc_sk-test"), CancellationToken.None);

        Assert.Equal(ConnectionHealthState.Degraded, health);
    }
}
