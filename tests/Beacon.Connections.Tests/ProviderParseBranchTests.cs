using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Selectors;

namespace Beacon.Connections.Tests;

/// <summary>
/// Provider 解析/降级分支收口：Ark 重置时刻多形态与耗尽响应、Qwen 重置消息与百分比缺失、
/// SenseNova 空池条目/窗口哨兵、MiMo 非对象根/空条目/控制台 5xx、Kimi 窗口畸形、
/// BigModel 摘要兜底三态、ProviderDsl 畸形文档 fail-loud。
/// </summary>
public sealed class ProviderParseBranchTests
{
    // ---------- Ark ----------

    [Fact]
    public void ArkReset_JsonStringEpoch_AndNumberEpoch_AndIso()
    {
        Assert.Equal(1_780_000_000, ArkUsageProvider.TryParseResetSeconds("""{"reset_at":"1780000000"}"""));
        Assert.Equal(1_780_000_000, ArkUsageProvider.TryParseResetSeconds("""{"reset_at":1780000000}"""));
        Assert.NotNull(ArkUsageProvider.TryParseResetSeconds("""{"reset_at":"2026-11-08T00:00:00Z"}"""));
        Assert.Null(ArkUsageProvider.TryParseResetSeconds("""{"data":{"x":1}}""")); // 有效 JSON 无重置键 → null，不拿其他时间戳凑数
    }

    [Fact]
    public void ArkReset_DeepNestingBeyondLimit_YieldsNull()
        => Assert.Null(ArkUsageProvider.TryParseResetSeconds(
            """{"a":{"b":{"c":{"d":{"e":{"f":{"reset_at":1780000000}}}}}}}"""));

    [Fact]
    public void ArkReset_ArrayTraversal_FindsNestedReset()
        => Assert.Equal(1_780_000_000, ArkUsageProvider.TryParseResetSeconds(
            """{"items":[{"reset_at":"1780000000"},{"x":1}]}"""));

    [Theory]
    [InlineData("accountquotaexceeded weekly 1780000000000", 1_780_000_000L)] // 毫秒 epoch 正则兜底
    [InlineData("accountquotaexceeded weekly 1780000000", 1_780_000_000L)] // 秒 epoch 正则兜底
    [InlineData("accountquotaexceeded weekly nothing", null)] // 无 epoch → null 重置
    public void ArkExhausted_WeeklyWindow_WithResetFromRegex(string body, long? expectedReset)
    {
        var usage = ArkUsageProvider.ParseExhaustedResponse(body)!;
        Assert.NotNull(usage.Weekly);
        Assert.Equal(100, usage.Weekly!.Percent);
        Assert.Equal(expectedReset, usage.Weekly.ResetSeconds);
    }

    [Fact]
    public void ArkParseUsage_PercentAsArray_IsNotNumericWindow()
    {
        var usage = ArkUsageProvider.ParseUsage("""{"Result":{"QuotaUsage":[{"Level":"weekly","Percent":[]}]}}""");
        Assert.NotNull(usage);
        Assert.Null(usage!.Weekly); // 非数值 percent 不构成窗口（防御兜底）
    }

    // ---------- Qwen ----------

    [Fact]
    public void QwenReset_NullOrNoPattern_ReturnsNull()
    {
        Assert.Null(QwenUsageProvider.ParseQuotaResetUtc(null));
        Assert.Null(QwenUsageProvider.ParseQuotaResetUtc("no reset date in this message"));
    }

    [Fact]
    public void QwenReset_ImpossibleDate_ReturnsNull()
        => Assert.Null(QwenUsageProvider.ParseQuotaResetUtc("quota reset at 13-45 25:99 UTC"));

    [Fact]
    public void QwenReset_FutureDate_ParsesCandidate()
    {
        var future = DateTimeOffset.UtcNow.AddDays(10);
        var text = $"quota reset at {future:MM-dd HH:mm} UTC";

        var reset = QwenUsageProvider.ParseQuotaResetUtc(text);

        Assert.NotNull(reset);
        Assert.True(reset!.Value > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void QwenParseUsage_MissingPercent_ReturnsNull()
        => Assert.Null(QwenUsageProvider.ParseUsage("""{"data":{"reset":1771000000}}"""));

    // ---------- SenseNova ----------

    [Fact]
    public void SenseNova_EmptyPoolEntry_IsSkipped()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""{"plan":{"id":"p"},"pools":[{"id":"","name":""}]}""")!;
        Assert.Empty(usage.Pools);
    }

    [Fact]
    public void SenseNova_WindowAllNull_IsNullWindow()
    {
        var usage = SenseNovaUsageProvider.ParseUsage(
            """{"plan":{"id":"p"},"pools":[{"id":"pool-a","name":"A","window_5h":{},"window_7d":{}}]}""")!;
        Assert.Null(usage.Pools[0].Window5h);
        Assert.Null(usage.Pools[0].Window7d);
    }

    [Fact]
    public void SenseNova_WindowNonPositiveLimit_IsWindowWithoutPercent()
    {
        var usage = SenseNovaUsageProvider.ParseUsage(
            """{"plan":{"id":"p"},"pools":[{"id":"pool-a","name":"A","window_5h":{"limit":"0","used":"5"}}]}""")!;
        Assert.NotNull(usage.Pools[0].Window5h); // limit ≤ 0 仍建窗（仅百分比退 null）
        Assert.Null(usage.Pools[0].Window5h!.UsedPercent);
    }

    [Fact]
    public void SenseNova_NullAmountText_IsWindowWithoutPercent()
    {
        var usage = SenseNovaUsageProvider.ParseUsage(
            """{"plan":{"id":"p"},"pools":[{"id":"pool-a","name":"A","window_5h":{"limit":null,"used":"5"}}]}""")!;
        Assert.NotNull(usage.Pools[0].Window5h); // JSON null 金额当缺失，窗口仍在（百分比退 null）
        Assert.Null(usage.Pools[0].Window5h!.UsedPercent);
    }

    // ---------- MiMo ----------

    [Fact]
    public void MiMoParseLocalCount_NonObjectRoot_ReturnsNull()
        => Assert.Null(MiMoUsageProvider.ParseLocalCount("[]"));

    [Fact]
    public void MiMoParseConsoleUsage_NonObjectRoot_ReturnsNull()
        => Assert.Null(MiMoUsageProvider.ParseConsoleUsage("[]"));

    [Fact]
    public void MiMoParseConsoleUsage_EmptyMonthItems_ReturnsNull()
        => Assert.Null(MiMoUsageProvider.ParseConsoleUsage("""{"data":{"monthUsage":{"items":[]}}}"""));

    private static ConnectionConfig MiMoConnection() => new()
    {
        Id = "mimo-main",
        Type = "mimo",
        CredentialRef = "conn:mimo",
        Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static WidgetConfig MiMoWidget() => new()
    {
        Id = "w-mimo",
        Type = MiMoWidgetDescriptors.UsageType,
        ConnectionId = "mimo-main",
        Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext MiMoCtx(string consoleCookie)
    {
        var secrets = new SecretStoreStub();
        secrets.Secrets["mimo:console"] = consoleCookie;
        return new ConnectionContext { Secrets = secrets };
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, ConnectionHealthState.Offline)]
    [InlineData(HttpStatusCode.NotFound, ConnectionHealthState.Degraded)]
    public async Task MiMoConsole_Non2xx_MapsHealthByStatusCode(HttpStatusCode status, ConnectionHealthState expected)
    {
        var http = new FakeHttpMessageHandler { Responder = _ => new FakeHttpResponse(status, "error") };
        var provider = new MiMoUsageProvider(http);

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(MiMoWidget(), MiMoConnection(),
                MiMoCtx("ph-session-value"), CancellationToken.None));

        Assert.Equal(expected, exception.Health);
    }

    // ---------- Kimi ----------

    [Fact]
    public void KimiParse_EmptyTotalQuota_YieldsNullPlan()
    {
        var usage = KimiCodingUsageProvider.ParseUsages(
            """{"usages":[{"scope":"FEATURE_CODING","detail":{"limit":"10","remaining":"5"}}],"totalQuota":{}}""")!;
        Assert.NotNull(usage);
        Assert.Null(usage.Total); // 全空 totalQuota → 无套餐总额
    }

    [Fact]
    public void KimiParse_WindowWithoutDuration_IsDropped()
    {
        var body = """{"usages":[{"scope":"FEATURE_CODING","detail":{},"limits":[{"window":{},"detail":{}}]}],"totalQuota":{"limit":"1","used":"1","remaining":"1"}}""";
        var usage = KimiCodingUsageProvider.ParseUsages(body)!;
        Assert.NotNull(usage);
        Assert.Null(usage.Rolling);
    }

    [Fact]
    public void KimiParse_WindowNonNumericDuration_IsDropped()
    {
        var body = """{"usages":[{"scope":"FEATURE_CODING","detail":{},"limits":[{"window":{"duration":"abc","timeUnit":"HOUR"},"detail":{}}]}],"totalQuota":{"limit":"1","used":"1","remaining":"1"}}""";
        var usage = KimiCodingUsageProvider.ParseUsages(body)!;
        Assert.NotNull(usage);
        Assert.Null(usage.Rolling);
    }

    [Fact]
    public void KimiParse_UnknownTimeUnit_IsDropped()
    {
        var body = """{"usages":[{"scope":"FEATURE_CODING","detail":{},"limits":[{"window":{"duration":300,"timeUnit":"TIME_UNIT_FORTNIGHT"},"detail":{}}]}],"totalQuota":{"limit":"1","used":"1","remaining":"1"}}""";
        var usage = KimiCodingUsageProvider.ParseUsages(body)!;
        Assert.NotNull(usage);
        Assert.Null(usage.Rolling);
    }

    [Fact]
    public void KimiParse_MissingDetailLimit_YieldsWindowWithoutValues()
    {
        var body = """{"usages":[{"scope":"FEATURE_CODING","detail":{},"limits":[{"window":{"duration":300,"timeUnit":"TIME_UNIT_MINUTE"},"detail":{}}]}],"totalQuota":{"limit":"1","used":"1","remaining":"1"}}""";
        var usage = KimiCodingUsageProvider.ParseUsages(body)!;
        Assert.NotNull(usage);
        Assert.NotNull(usage.Rolling); // ParseWindow 恒返回窗口对象（全空字段占位）
        Assert.Null(usage.Rolling!.RemainingPercent);
    }

    [Fact]
    public void KimiParse_NonNumericDetailLimit_YieldsWindowWithoutValues()
    {
        var body = """{"usages":[{"scope":"FEATURE_CODING","detail":{},"limits":[{"window":{"duration":300,"timeUnit":"TIME_UNIT_MINUTE"},"detail":{"limit":"abc"}}]}],"totalQuota":{"limit":"1","used":"1","remaining":"1"}}""";
        var usage = KimiCodingUsageProvider.ParseUsages(body)!;
        Assert.NotNull(usage);
        Assert.NotNull(usage.Rolling);
        Assert.Null(usage.Rolling!.RemainingPercent); // 非数值 limit 不参与百分比
    }

    // ---------- BigModel 摘要兜底 ----------

    [Fact]
    public void BigModelSummarize_NoParts_PresentWindowWithReset_ShowsReset()
    {
        var usage = new BigModelUsageProvider.PlanUsage("Lite", new BigModelUsageProvider.UsageWindow(null, 1_771_000_000), null, null);
        var summary = BigModelUsageProvider.Summarize(usage, new QuotaWindowDecision(null, null, null, null));
        Assert.Contains("重置", summary);
    }

    [Fact]
    public void BigModelSummarize_NoParts_PresentWindowWithoutReset_ShowsWindowData()
    {
        var usage = new BigModelUsageProvider.PlanUsage("Lite", new BigModelUsageProvider.UsageWindow(null, null), null, null);
        var summary = BigModelUsageProvider.Summarize(usage, new QuotaWindowDecision(null, null, null, null));
        Assert.Contains("窗口数据", summary);
    }

    [Fact]
    public void BigModelSummarize_NoParts_NoWindows_ReturnsEmpty()
    {
        var usage = new BigModelUsageProvider.PlanUsage("Lite", null, null, null);
        Assert.Equal("", BigModelUsageProvider.Summarize(usage, new QuotaWindowDecision(null, null, null, null)));
    }

    // ---------- ProviderDsl 畸形文档 ----------

    [Fact]
    public void Dsl_TopLevelNotMapping_Fails()
        => Assert.Throws<YamlLiteException>(() => ProviderDsl.Load("just a scalar"));

    [Fact]
    public void Dsl_MappingValueNotScalar_Fails()
        => Assert.Throws<YamlLiteException>(() => ProviderDsl.Load("provider: x\nmapping:\n  status_path:\n    nested: y"));

    [Fact]
    public void Dsl_ActionItemNotMapping_Fails()
        => Assert.Throws<YamlLiteException>(() => ProviderDsl.Load("provider: x\nactions:\n  - juststring"));

    [Fact]
    public void Dsl_RetryNotMapping_Fails()
        => Assert.Throws<YamlLiteException>(() => ProviderDsl.Load(
            "provider: x\nactions:\n  - id: a\n    type: http\n    url: http://x\n    retry: true"));

    [Fact]
    public void Dsl_EmptyMappingValue_Fails()
        => Assert.Throws<YamlLiteException>(() => ProviderDsl.Load("provider: x\nmapping:\n  status_path:"));

    [Fact]
    public void Dsl_QuotedKeyWithColon_Parses()
    {
        var root = YamlLite.Parse("provider: x\nmapping:\n  \"a:b\": c");
        var mapping = root.Child("mapping")!;
        // YamlLite 不做键去引号：引号保留在键上，仅冒号切分时感知引号
        Assert.True(mapping.Map.TryGetValue("\"a:b\"", out var node));
        Assert.Equal("c", node.Scalar);
    }

    [Fact]
    public void Dsl_KeyContainingSpace_Fails()
        => Assert.Throws<YamlLiteException>(() => ProviderDsl.Load("provider: x\nmapping:\n  bad key: x"));
}
