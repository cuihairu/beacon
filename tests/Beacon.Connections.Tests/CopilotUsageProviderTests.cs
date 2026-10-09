using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// copilot.usage（GitHub Copilot，AI Usage 第八家，档①官方口直连）：quota_snapshots 三槽解析、
/// plan 映射（Pro 月付带官方价）、级别阈值、缺 Token/401/404/5xx 分级、官方扩展两头必带、连接测试两态。
/// </summary>
public sealed class CopilotUsageProviderTests
{
    /// <summary>实测形状裁剪版（2026-10-09 本机 200）：Pro 月付、premium 1500 含额剩 1499、chat/completions 无限。</summary>
    private const string UsageBody = """
        {
          "login": "cuihairu",
          "copilot_plan": "individual",
          "access_type_sku": "monthly_subscriber_quota",
          "quota_reset_date": "2026-11-01",
          "quota_snapshots": {
            "chat": {"unlimited": true, "percent_remaining": 100.0, "entitlement": 0, "remaining": 0},
            "completions": {"unlimited": true, "percent_remaining": 100.0, "entitlement": 0, "remaining": 0},
            "premium_interactions": {"unlimited": false, "percent_remaining": 99.9, "entitlement": 1500, "remaining": 1499}
          }
        }
        """;

    private static ConnectionConfig Connection(string? endpoint = null, string? credentialRef = "conn:copilot") => new()
    {
        Id = "copilot-main",
        Type = "copilot",
        Endpoint = endpoint,
        CredentialRef = credentialRef,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-copilot",
        Type = CopilotWidgetDescriptors.UsageType,
        ConnectionId = "copilot-main",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? secret = null)
    {
        var secrets = new SecretStoreStub();
        if (secret is not null)
        {
            secrets.Secrets["conn:copilot"] = secret;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (CopilotUsageProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body) => Faked(_ => new FakeHttpResponse(status, body));

    private static (CopilotUsageProvider Provider, FakeHttpMessageHandler Http) Faked(Func<HttpRequestMessage, FakeHttpResponse> respond)
    {
        var http = new FakeHttpMessageHandler { Responder = respond };
        return (new CopilotUsageProvider(http), http);
    }

    // ---- 解析 ----

    [Fact]
    public void ParseUsage_MapsPlanSkuResetDateAndSlots()
    {
        var usage = CopilotUsageProvider.ParseUsage(UsageBody);

        Assert.Equal("individual", usage.PlanRaw);
        Assert.Equal("monthly_subscriber_quota", usage.AccessTypeSku);
        Assert.Equal("2026-11-01", usage.ResetDate);
        Assert.Equal(3, usage.Slots.Count);
        var premium = Assert.Single(usage.Slots, slot => slot.Id == "premium_interactions");
        Assert.False(premium.Unlimited);
        Assert.Equal(1500, premium.Entitlement);
        Assert.Equal(1499, premium.Remaining);
        Assert.All(usage.Slots.Where(slot => slot.Id != "premium_interactions"), slot => Assert.True(slot.Unlimited));
    }

    [Fact]
    public void ParseUsage_MissingQuotaSnapshots_ThrowsDegraded()
    {
        var exception = Assert.Throws<ConnectionException>(() => CopilotUsageProvider.ParseUsage("""{"copilot_plan":"individual"}"""));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // ---- 卡面（官方真数字） ----

    [Fact]
    public async Task GetStateAsync_Success_RendersProCardWithRealNumbers()
    {
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, UsageBody);
        });

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("ghp-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state!.Severity); // 已用 1/1500 ≈ 0.1%
        // Pro 套餐 + 官方定价（月付才标）+ 高级请求真数字 + 无限槽位 + API 重置日
        Assert.Contains("Pro $10/月", state.Summary);
        Assert.Contains("高级 1/1500", state.Summary);
        Assert.Contains("聊天 ∞", state.Summary);
        Assert.Contains("补全 ∞", state.Summary);
        Assert.Contains("11-01 重置", state.Summary);
        Assert.Equal("api", state.Payload["usage_source"]); // 档①官方口直连
        Assert.Equal("1500", state.Payload["premium_interactions_entitlement"]);
        Assert.Equal("1499", state.Payload["premium_interactions_remaining"]);
        Assert.Equal("2026-11-01", state.Payload["quota_reset_date"]);
        // 官方扩展同款两头必带（实测缺失端点不认）+ Bearer PAT
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("ghp-test", captured.Headers.Authorization?.Parameter);
        Assert.True(captured.Headers.TryGetValues("Editor-Version", out var editor));
        Assert.Equal(CopilotUsageProvider.EditorVersionHeader, Assert.Single(editor!));
        Assert.True(captured.Headers.TryGetValues("Copilot-Integration-Id", out var integration));
        Assert.Equal(CopilotUsageProvider.IntegrationIdHeader, Assert.Single(integration!));
    }

    [Fact]
    public async Task GetStateAsync_CustomEndpoint_UsesConnectionEndpoint()
    {
        HttpRequestMessage? captured = null;
        var (provider, _) = Faked(request =>
        {
            captured = request;
            return new FakeHttpResponse(HttpStatusCode.OK, UsageBody);
        });

        await provider.GetStateAsync(Widget(), Connection("https://gh.example.internal/copilot_internal/user"), Ctx("ghp-test"), CancellationToken.None);

        Assert.Equal("gh.example.internal", captured!.RequestUri!.Host);
    }

    [Theory]
    [InlineData(20.0, Severity.Warning)] // 已用 80% = 默认 warn 阈值
    [InlineData(5.0, Severity.Error)]    // 已用 95% = 默认 error 阈值
    public async Task GetStateAsync_PremiumUsage_CrossesThresholds(double percentRemaining, Severity expected)
    {
        var body = """
            {
              "copilot_plan": "individual",
              "access_type_sku": "monthly_subscriber_quota",
              "quota_reset_date": "2026-11-01",
              "quota_snapshots": {
                "chat": {"unlimited": true, "percent_remaining": 100.0},
                "completions": {"unlimited": true, "percent_remaining": 100.0},
                "premium_interactions": {"unlimited": false, "percent_remaining": %PERCENT%, "entitlement": 300, "remaining": %REMAIN%}
              }
            }
            """.Replace("%PERCENT%", percentRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture))
               .Replace("%REMAIN%", ((int)percentRemaining).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("ghp-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(expected, state!.Severity);
    }

    [Fact]
    public async Task GetStateAsync_AllSlotsUnlimited_SuccessBaseline()
    {
        var body = """
            {
              "copilot_plan": "free",
              "access_type_sku": "free_limited",
              "quota_reset_date": "2026-11-01",
              "quota_snapshots": {
                "chat": {"unlimited": true, "percent_remaining": 100.0},
                "completions": {"unlimited": true, "percent_remaining": 100.0}
              }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("ghp-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state!.Severity);
        Assert.Contains("Free", state.Summary);
        Assert.Contains("聊天 ∞", state.Summary);
        Assert.DoesNotContain("高级", state.Summary); // 无计量槽位不造虚数
    }

    // ---- 映射辅助 ----

    [Fact]
    public void PlanDisplay_KnownPlansMapped_UnknownPassthrough()
    {
        Assert.Equal("Pro", CopilotUsageProvider.PlanDisplay("individual", "yearly_subscriber_quota")); // 年付不标月价
        Assert.Equal("Pro+", CopilotUsageProvider.PlanDisplay("individual_next", "monthly_subscriber_quota"));
        Assert.Equal("Business", CopilotUsageProvider.PlanDisplay("business", "subscriber_quota"));
        Assert.Equal("Enterprise", CopilotUsageProvider.PlanDisplay("enterprise", "subscriber_quota"));
        Assert.Equal("Free", CopilotUsageProvider.PlanDisplay("free", "free_limited"));
        Assert.Equal("weird_plan", CopilotUsageProvider.PlanDisplay("weird_plan", "")); // 未知原样，不猜
        Assert.Equal("套餐未知", CopilotUsageProvider.PlanDisplay("", ""));
    }

    [Fact]
    public void ResetDisplay_IsoDateFormatted_FallbackRawAndUnknown()
    {
        Assert.Equal("11-01 重置", CopilotUsageProvider.ResetDisplay("2026-11-01"));
        Assert.Equal("soon 重置", CopilotUsageProvider.ResetDisplay("soon")); // 非日期原样展示，不编
        Assert.Equal("重置日未知", CopilotUsageProvider.ResetDisplay(null));
    }

    // ---- 凭据与错误分级 ----

    [Fact]
    public async Task GetStateAsync_MissingToken_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, UsageBody);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx(secret: null), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.Forbidden, ConnectionHealthState.Degraded)]
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Degraded)] // 端点变动/无 Copilot 权益 → 配置侧可查
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    public async Task GetStateAsync_HttpError_HealthFollowsStatusClass(HttpStatusCode status, ConnectionHealthState expected)
    {
        var (provider, _) = Faked(status, "upstream error");

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("ghp-test"), CancellationToken.None));

        Assert.Equal(expected, exception.Health);
    }

    // ---- 连接测试 ----

    [Fact]
    public async Task ConnectionTest_ValidToken_ReturnsHealthy()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.OK, UsageBody);
        var provider = new CopilotConnectionProvider(http);

        var health = (await provider.TestAsync(Connection(), Ctx("ghp-test"), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Healthy, health);
    }

    [Fact]
    public async Task ConnectionTest_InvalidToken_ReturnsDegraded()
    {
        var http = new FakeHttpMessageHandler();
        http.Enqueue(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""");
        var provider = new CopilotConnectionProvider(http);

        var health = (await provider.TestAsync(Connection(), Ctx("ghp-test"), CancellationToken.None)).Health;

        Assert.Equal(ConnectionHealthState.Degraded, health);
    }
}
