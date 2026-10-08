using System.Globalization;
using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>http.quota 通用额度数值卡：字段映射（used/remaining 二选一）、重置时间三格式、阈值严重度、端到端。</summary>
public sealed class HttpQuotaProviderTests
{
    private const string Body = """{"data":{"total":1000,"used":420,"reset_at":1760000000},"code":0}""";

    private static ConnectionConfig Connection(string? credentialRef = null) => new()
    {
        Id = "quota-local",
        Type = "http",
        Endpoint = "http://quota.local/api/quota",
        CredentialRef = credentialRef,
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w1",
        Type = HttpWidgetDescriptors.QuotaType,
        ConnectionId = "quota-local",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx(string? token = null)
    {
        var secrets = new SecretStoreStub();
        if (token is not null)
        {
            secrets.Secrets["conn:q"] = token;
        }
        return new ConnectionContext { Secrets = secrets };
    }

    private static (HttpQuotaProvider Provider, FakeHttpMessageHandler Http) Faked(HttpStatusCode status, string body)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, body) };
        return (new HttpQuotaProvider(http), http);
    }

    // ---- ParseQuota：口径与校验 ----

    [Fact]
    public void Parse_UsedAndTotal_ComputesPercent()
    {
        using var document = System.Text.Json.JsonDocument.Parse(Body);
        var reading = HttpQuotaProvider.ParseQuota(document.RootElement, new Dictionary<string, string>
        {
            ["total_path"] = "$.data.total",
            ["used_path"] = "$.data.used",
        });
        var quota = Assert.IsType<HttpQuotaProvider.QuotaReading>(reading);
        Assert.Equal(42.0, quota.Percent, 5);
        Assert.Equal(1000, quota.Total, 5);
        Assert.Equal(420, quota.Used!.Value, 5);
        Assert.Null(quota.Remaining);
    }

    [Fact]
    public void Parse_RemainingOnly_DerivesUsed()
    {
        const string body = """{"total":"500","remaining":"350"}"""; // 数值字符串同样可取
        using var document = System.Text.Json.JsonDocument.Parse(body);
        var reading = HttpQuotaProvider.ParseQuota(document.RootElement, new Dictionary<string, string>
        {
            ["total_path"] = "total",
            ["remaining_path"] = "remaining",
        });
        var quota = Assert.IsType<HttpQuotaProvider.QuotaReading>(reading);
        Assert.Equal(30.0, quota.Percent, 5); // (500-350)/500
        Assert.Equal(350, quota.Remaining!.Value, 5);
    }

    [Theory]
    [InlineData("""{"total":0,"used":1}""")]      // 总量为 0
    [InlineData("""{"total":100}""")]             // used/remaining 均未配
    [InlineData("""{}""")]                        // total_path 取不到
    public void Parse_InvalidInput_ReturnsNull(string body)
    {
        using var document = System.Text.Json.JsonDocument.Parse(body);
        var reading = HttpQuotaProvider.ParseQuota(document.RootElement, new Dictionary<string, string>
        {
            ["total_path"] = "total",
            ["used_path"] = "used",
        });
        Assert.Null(reading);
    }

    [Theory]
    [InlineData("1760000000", "2025-10-09T08:53:20Z")]  // epoch 秒
    [InlineData("1760000000000", "2025-10-09T08:53:20Z")] // epoch 毫秒
    [InlineData("\"2026-10-09T08:00:00Z\"", "2026-10-09T08:00:00Z")] // ISO 文本
    public void Parse_ResetFormats_EpochSecondsMillisAndIso(string raw, string expected)
    {
        var body = $$"""{"reset":{{raw}}}""";
        using var document = System.Text.Json.JsonDocument.Parse(body);
        var reset = HttpQuotaProvider.SelectReset(document.RootElement, "reset");
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), reset!.Value);
    }

    [Fact]
    public void Parse_Reset_MissingStaysNull()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""{"reset":"soon"}""");
        Assert.Null(HttpQuotaProvider.SelectReset(document.RootElement, "reset"));
    }

    // ---- 阈值严重度（与 bigmodel.usage 同口径） ----

    [Theory]
    [InlineData(59.9, Severity.Success)]
    [InlineData(60.0, Severity.Warning)]
    [InlineData(89.9, Severity.Warning)]
    [InlineData(90.0, Severity.Error)]
    public void MapSeverity_Thresholds(double percent, Severity expected)
        => Assert.Equal(expected, HttpQuotaProvider.MapSeverity(percent, warnPercent: 60, errorPercent: 90));

    // ---- 端到端：真实 HTTP 流程 ----

    [Fact]
    public async Task GetState_EndToEnd_MapsPayloadProgressAndSummary()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, Body);
        var state = await provider.GetStateAsync(Widget(new Dictionary<string, string>
        {
            ["label"] = "GLM 额度",
            ["total_path"] = "$.data.total",
            ["used_path"] = "$.data.used",
            ["reset_path"] = "$.data.reset_at",
        }), Connection(), Ctx(), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state.Severity);
        Assert.Equal(0.42, state.Progress!.Value, 5);
        Assert.Equal("42", state.Payload["percent"]); // payload 存裸数值，% 属显示层（tile ValueOf 拼接）
        Assert.Equal("420", state.Payload["used"]);
        Assert.Equal("1000", state.Payload["total"]);
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}", state.Payload["reset_iso"]); // 本地时区渲染，只验格式
        Assert.StartsWith("GLM 额度 · 已用 42%", state.Summary);
        Assert.Contains("重置", state.Summary);
        Assert.Equal("http://quota.local/api/quota", http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetState_OverThreshold_EscalatesAndClampsProgress()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, """{"total":100,"used":130}""");
        var state = await provider.GetStateAsync(Widget(new Dictionary<string, string>
        {
            ["total_path"] = "total",
            ["used_path"] = "used",
            ["warn_percent"] = "60",
            ["error_percent"] = "90",
        }), Connection(), Ctx(), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(1.0, state.Progress!.Value, 5); // 进度条封顶，摘要保留 130%
        Assert.Equal("130", state.Payload["percent"]);
    }

    [Fact]
    public async Task GetState_UnparsableJson_ThrowsDegraded()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, "not-json");
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            provider.GetStateAsync(Widget(new Dictionary<string, string> { ["total_path"] = "total", ["used_path"] = "used" }),
                Connection(), Ctx(), CancellationToken.None));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    [Fact]
    public async Task GetState_MissingPaths_ThrowsWithGuidance()
    {
        var (provider, _) = Faked(HttpStatusCode.OK, Body);
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));
        Assert.Contains("total_path", exception.Message);
    }

    [Fact]
    public async Task GetState_BearerAuth_InjectedFromCredentialRef()
    {
        var (provider, http) = Faked(HttpStatusCode.OK, """{"total":10,"remaining":9}""");
        await provider.GetStateAsync(Widget(new Dictionary<string, string> { ["total_path"] = "total", ["remaining_path"] = "remaining" }),
            Connection("conn:q"), Ctx("sk-test"), CancellationToken.None);
        Assert.Equal("Bearer sk-test", http.Requests[0].Headers!.GetValues("Authorization").Single());
    }
}
