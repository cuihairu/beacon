using System.Net;
using System.Net.Sockets;
using System.Text;
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

    // ---- 分支补齐：共享客户端 / 缺省标量解析 / 阈值容错 / 计量槽缺字段 / 缺省凭据 / 长文截断 ----

    [Fact]
    public async Task FetchUsageAsync_ParameterlessProvider_FetchesViaSharedClient()
    {
        // 参数less 构造（无注入 handler）→ L86 `_client ?? HttpEndpoint.Shared` 走共享客户端；
        // 回环 HTTP 桩做一次真实交换，验证共享客户端上整链路（Bearer + 官方扩展两头）仍成立。
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var provider = new CopilotUsageProvider();

        var fetchTask = provider.FetchUsageAsync(Connection($"http://127.0.0.1:{port}"), Ctx("ghp-test"), CancellationToken.None);
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
        Assert.Contains("Bearer ghp-test", requestText, StringComparison.Ordinal); // 共享客户端上 Bearer 照发
        Assert.Contains("Editor-Version", requestText, StringComparison.Ordinal);  // 官方扩展两头必带

        var responseBytes = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(UsageBody)}\r\nConnection: close\r\n\r\n{UsageBody}");
        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

        var usage = await fetchTask;
        Assert.Equal("individual", usage.PlanRaw); // 共享客户端完成取数并解析
        listener.Stop();
    }

    [Fact]
    public void ParseUsage_OptionalScalarsMissing_FallBackToDefaults()
    {
        // copilot_plan / access_type_sku / quota_reset_date 全缺：不炸，落空串/空重置（L114/L117/L120 缺属性臂）
        var usage = CopilotUsageProvider.ParseUsage("""{"quota_snapshots":{"chat":{"unlimited":true}}}""");

        Assert.Equal("", usage.PlanRaw);
        Assert.Equal("", usage.AccessTypeSku);
        Assert.Null(usage.ResetDate);
    }

    [Fact]
    public void ParseUsage_NullOrNonStringScalars_TreatedAsAbsent()
    {
        // 类型不对（copilot_plan=null、access_type_sku=数字、quota_reset_date=数字）：不炸不编造，落缺省
        var usage = CopilotUsageProvider.ParseUsage("""
            {
              "copilot_plan": null,
              "access_type_sku": 42,
              "quota_reset_date": 20261101,
              "quota_snapshots": {"chat": {"unlimited": true}}
            }
            """);

        Assert.Equal("", usage.PlanRaw);
        Assert.Equal("", usage.AccessTypeSku);
        Assert.Null(usage.ResetDate);
    }

    [Fact]
    public async Task GetStateAsync_ThresholdParseFailures_FallBackToDefaults()
    {
        // warn/error 填了非数字：不炸也不按 0 判级——回退默认 80/95（已用 75% < 80 → Success；若按 0 判必 Error）
        var body = """
            {
              "copilot_plan": "individual",
              "quota_snapshots": {
                "premium_interactions": {"unlimited": false, "percent_remaining": 25.0, "entitlement": 300, "remaining": 225}
              }
            }
            """;
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["warn_percent"] = "abc",
            ["error_percent"] = "",
        };
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(config), Connection(), Ctx("ghp-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state!.Severity);
    }

    [Fact]
    public async Task GetStateAsync_ConfiguredThresholds_OverrideDefaults()
    {
        // 已用 55%（percent_remaining 45）：默认 80/95 会显 Success；显式 warn=50 → Warning 证明阈值被读取
        var body = """
            {
              "copilot_plan": "individual",
              "quota_snapshots": {
                "premium_interactions": {"unlimited": false, "percent_remaining": 45.0, "entitlement": 300, "remaining": 135}
              }
            }
            """;
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["warn_percent"] = "50",
            ["error_percent"] = "60",
        };
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(config), Connection(), Ctx("ghp-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Warning, state!.Severity); // 55 ≥ warn 50、< error 60
    }

    [Fact]
    public async Task GetStateAsync_MeteredSlotWithoutEntitlement_ShowsRemainingPercent()
    {
        // 计量槽缺 entitlement/remaining：不造 0/x 数字，改显「剩 X%」；无 unlimited 键的槽同样按计量槽处理（L138/L176 缺省臂）
        var body = """
            {
              "copilot_plan": "individual",
              "quota_snapshots": {
                "chat": {"percent_remaining": 100.0},
                "premium_interactions": {"unlimited": false, "percent_remaining": 42.0}
              }
            }
            """;
        var (provider, _) = Faked(HttpStatusCode.OK, body);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx("ghp-test"), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Contains("高级 剩 42%", state!.Summary);
        Assert.Contains("聊天 剩 100%", state.Summary); // chat 无 unlimited 键 → 按非无限槽渲染
        Assert.Equal(Severity.Success, state.Severity); // 最差已用 58% < 80
    }

    [Fact]
    public async Task FetchUsageAsync_NullCredentialRef_FallsBackToDefaultSecretKey()
    {
        // CredentialRef 缺省 → 回退默认键 "copilot:default"（L271 右臂）；从该键取到 PAT 才能成功
        var (provider, _) = Faked(HttpStatusCode.OK, UsageBody);
        var secrets = new SecretStoreStub();
        secrets.Secrets["copilot:default"] = "ghp-from-default-slot";

        var usage = await provider.FetchUsageAsync(
            Connection(credentialRef: null), new ConnectionContext { Secrets = secrets }, CancellationToken.None);

        Assert.Equal("individual", usage.PlanRaw);
    }

    [Fact]
    public async Task GetStateAsync_LongErrorBody_TruncatesTo120Chars()
    {
        // 超长错误体截到 120（L281 右臂），错误卡/详情不被上游长文淹没
        var tail = "TAIL-BEYOND-120";
        var body = new string('a', 120) + tail;
        var (provider, _) = Faked(HttpStatusCode.Unauthorized, body);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(), Ctx("ghp-test"), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("401", exception.Message);
        Assert.Contains(new string('a', 120), exception.Message);
        Assert.DoesNotContain(tail, exception.Message);
    }
}
