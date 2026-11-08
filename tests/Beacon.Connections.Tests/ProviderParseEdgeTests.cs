using System.Text.Json;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// 纯函数解析边界收口（Batch 2）：HttpJsonPath 路径/索引/标量退化、KimiCoding 窗口归一化与摘要兜底、
/// Qwen 重置时刻解析（顺延/非法）、SenseNova 池条目与数值/epoch 退化、Copilot 套餐名与未知槽位。
/// 这些面多为「配错不炸刷新」的离线降级口径，逐一钉住防回归。
/// </summary>
public sealed class ProviderParseEdgeTests
{
    // ---------- HttpJsonPath ----------

    [Fact]
    public void HttpJsonPath_NullOrEmptyPath_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");
        Assert.Null(HttpJsonPath.Select(doc.RootElement, null));
        Assert.Null(HttpJsonPath.Select(doc.RootElement, ""));
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "  "));
    }

    [Fact]
    public void HttpJsonPath_MissingProperty_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.b"));
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.a.c"));
    }

    [Fact]
    public void HttpJsonPath_IndexOutOfRange_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"arr":[1,2,3]}""");
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.arr[9]"));
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.arr[-1]"));
    }

    [Fact]
    public void HttpJsonPath_NonNumericIndex_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"arr":[1,2,3]}""");
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.arr[abc]"));
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.arr[ ]"));
    }

    [Fact]
    public void HttpJsonPath_EmptySegment_IsSkipped()
    {
        using var doc = JsonDocument.Parse("""{"a":{"b":"x"}}""");
        Assert.Equal("x", HttpJsonPath.Select(doc.RootElement, "a. .b"));
    }

    [Fact]
    public void HttpJsonPath_ScalarVariants()
    {
        using var doc = JsonDocument.Parse("""{"s":"str","n":42,"t":true,"f":false,"nil":null,"obj":{"z":1},"list":[1]}""");
        var root = doc.RootElement;
        Assert.Equal("str", HttpJsonPath.Select(root, "$.s"));
        Assert.Equal("42", HttpJsonPath.Select(root, "$.n"));
        Assert.Equal("true", HttpJsonPath.Select(root, "$.t"));
        Assert.Equal("false", HttpJsonPath.Select(root, "$.f"));
        Assert.Null(HttpJsonPath.Select(root, "$.nil"));
        Assert.Null(HttpJsonPath.Select(root, "$.obj"));
        Assert.Null(HttpJsonPath.Select(root, "$.list"));
    }

    [Fact]
    public void HttpJsonPath_IndexOnScalar_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"a":{"b":"x"},"n":5}""");
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.n[0]")); // 标量非数组
        Assert.Null(HttpJsonPath.Select(doc.RootElement, "$.a.b[0]")); // 字符串非数组
    }

    // ---------- KimiCoding ----------

    [Fact]
    public void KimiCoding_NonObjectRoot_ReturnsNull()
    {
        Assert.Null(KimiCodingUsageProvider.ParseUsages("[]"));
        Assert.Null(KimiCodingUsageProvider.ParseUsages("42"));
    }

    [Fact]
    public void KimiCoding_UnknownStringWindow_Ignored_FallsBackTopLevel()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "remaining": 90 },
              "limits": [
                { "window": "daily", "detail": { "limit": 100, "remaining": 5 } }
              ]
            }
            """)!;

        Assert.Null(usage.Rolling); // "daily" 未识别 → 该窗整条丢弃
        Assert.Equal(90, usage.Weekly!.RemainingPercent!.Value, 5);
    }

    [Fact]
    public void KimiCoding_UnknownTimeUnit_Ignored_FallsBackTopLevel()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "remaining": 90 },
              "limits": [
                { "window": { "duration": 3, "timeUnit": "TIME_UNIT_BOGUS" }, "detail": { "limit": 100, "remaining": 5 } }
              ]
            }
            """)!;

        Assert.Null(usage.Rolling);
        Assert.Equal(90, usage.Weekly!.RemainingPercent!.Value, 5);
    }

    [Fact]
    public void KimiCoding_NonNumericDuration_Ignored()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "remaining": 90 },
              "limits": [
                { "window": { "duration": "abc", "timeUnit": "TIME_UNIT_HOUR" }, "detail": { "limit": 100, "remaining": 5 } }
              ]
            }
            """)!;

        Assert.Null(usage.Rolling);
        Assert.Equal(90, usage.Weekly!.RemainingPercent!.Value, 5);
    }

    [Fact]
    public void KimiCoding_ZeroLimit_YieldsNullPercentWindow()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "remaining": 90 },
              "limits": [
                { "window": { "duration": 5, "timeUnit": "TIME_UNIT_HOUR" }, "detail": { "limit": 0, "remaining": 0 } }
              ]
            }
            """)!;

        Assert.True(usage.Rolling is null || usage.Rolling.RemainingPercent is null);
        Assert.Equal(90, usage.Weekly!.RemainingPercent!.Value, 5);
    }

    [Fact]
    public void KimiCoding_NonNumericNumbers_YieldNullPercentWindow()
    {
        var usage = KimiCodingUsageProvider.ParseUsages("""
            {
              "usage": { "limit": 100, "remaining": 90 },
              "limits": [
                { "window": { "duration": 5, "timeUnit": "TIME_UNIT_HOUR" }, "detail": { "limit": "abc", "remaining": "xyz" } }
              ]
            }
            """)!;

        Assert.True(usage.Rolling is null || usage.Rolling.RemainingPercent is null);
    }

    [Fact]
    public void KimiCoding_Summarize_AllNull_Empty()
    {
        var usage = new KimiCodingUsageProvider.KimiUsage(null, null, null, null);

        Assert.Equal("", KimiCodingUsageProvider.Summarize(usage));
    }

    // ---------- Qwen ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no reset here")]
    public void Qwen_ResetUtc_NoMatch_ReturnsNull(string? message)
        => Assert.Null(QwenUsageProvider.ParseQuotaResetUtc(message));

    [Fact]
    public void Qwen_ResetUtc_InvalidFields_ReturnsNull()
    {
        Assert.Null(QwenUsageProvider.ParseQuotaResetUtc("reset at 1x-2y 30:99 UTC"));
        Assert.Null(QwenUsageProvider.ParseQuotaResetUtc("reset at 13-40 25:00:00 UTC")); // 月份越界 → ArgumentOutOfRange
    }

    [Fact]
    public void Qwen_ResetUtc_PastDate_RollsToNextYear()
    {
        var reset = QwenUsageProvider.ParseQuotaResetUtc("quota exhausted, reset at 01-01 00:00:00 UTC");
        Assert.NotNull(reset);
        Assert.Equal(DateTimeOffset.UtcNow.Year + 1, reset!.Value.Year);
        Assert.Equal(1, reset.Value.Month);
    }

    [Fact]
    public void Qwen_ResetUtc_FutureDate_StaysCurrentYear()
    {
        var reset = QwenUsageProvider.ParseQuotaResetUtc("reset at 12-31 23:59:00 UTC");
        Assert.NotNull(reset);
        Assert.Equal(DateTimeOffset.UtcNow.Year, reset!.Value.Year);
    }

    [Fact]
    public void Qwen_QuotaExhausted_InvalidJson_ReturnsNull()
        => Assert.Null(QwenUsageProvider.ParseQuotaExhausted("not json"));

    [Fact]
    public void Qwen_QuotaExhausted_NoQuotaCode_ReturnsNull()
        => Assert.Null(QwenUsageProvider.ParseQuotaExhausted("""{"error":{"code":"RateLimit","message":"slow down"}}"""));

    [Fact]
    public void Qwen_QuotaExhausted_AllocationQuota_ReturnsExhausted()
    {
        var result = QwenUsageProvider.ParseQuotaExhausted(
            """{"error":{"code":"AllocationQuota","message":"exhausted, reset at 10-19 16:00:00 UTC"}}""");

        Assert.NotNull(result);
        Assert.NotNull(result!.ResetUtc);
    }

    // ---------- SenseNova ----------

    [Fact]
    public void SenseNova_EmptyPoolEntry_IsSkipped()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""{"plan":{"name":"p"},"pools":[{},{"id":"ok","name":"n"}]}""")!;

        Assert.Single(usage.Pools);
        Assert.Equal("ok", usage.Pools[0].Id);
    }

    [Fact]
    public void SenseNova_NonNumericNumbers_YieldNullPercentWindow()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""
            {"plan":{"name":"p"},"pools":[{"id":"p1","name":"主池","window_5h":{"limit":"abc","used":"10"}}]}
            """)!;

        var window = usage.Pools[0].Window5h;
        Assert.NotNull(window);
        Assert.Null(window!.UsedPercent); // limit 解析失败 → 不硬造百分比
        Assert.Equal(10, window.Used);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void SenseNova_NonPositiveEpoch_YieldsNullReset(string resetAt)
    {
        var body = "{\"plan\":{\"name\":\"p\"},\"pools\":[{\"id\":\"p1\",\"window_5h\":{\"limit\":\"100\",\"used\":\"50\",\"reset_at\":\"" + resetAt + "\"}}]}";
        var usage = SenseNovaUsageProvider.ParseUsage(body)!;

        Assert.Null(usage.Pools[0].Window5h!.ResetSeconds);
    }

    [Fact]
    public void SenseNova_StringEpoch_NormalizedToSeconds()
    {
        var usage = SenseNovaUsageProvider.ParseUsage("""
            {"plan":{"name":"p"},"pools":[{"id":"p1","window_5h":{"limit":"100","used":"50","reset_at":"1730000000"}}]}
            """)!;

        Assert.Equal(1_730_000_000, usage.Pools[0].Window5h!.ResetSeconds);
    }

    [Fact]
    public void SenseNova_FormatDate_LocalShortDate()
    {
        var formatted = SenseNovaUsageProvider.FormatDate(1_730_000_000);
        Assert.Matches(@"^\d{2}-\d{2}$", formatted);
    }

    // ---------- Copilot ----------

    [Theory]
    [InlineData("team_plan", "whatever", "team_plan")]          // 未知套餐原样显示
    [InlineData("individual", "annual_subscriber_quota", "Pro")] // 非月付不标价
    [InlineData("individual", "monthly_subscriber_quota", "Pro $10/月")]
    [InlineData("individual_next", "monthly_subscriber_quota", "Pro+")]
    [InlineData("business", "monthly_subscriber_quota", "Business")]
    [InlineData("enterprise", "monthly_subscriber_quota", "Enterprise")]
    [InlineData("free", "monthly_subscriber_quota", "Free")]
    [InlineData("", "monthly_subscriber_quota", "套餐未知")]
    public void Copilot_PlanDisplay_Table(string plan, string sku, string expected)
        => Assert.Equal(expected, CopilotUsageProvider.PlanDisplay(plan, sku));

    [Fact]
    public void Copilot_ToState_UnknownSlot_ShowsRawId()
    {
        var usage = new CopilotUsage("individual", "monthly_subscriber_quota", "2026-12-01",
        [
            new CopilotQuotaSlot("future_slot", false, 50, 1000, 500),
        ]);
        var state = CopilotUsageProvider.ToState(
            new WidgetConfig { Id = "w", Type = CopilotWidgetDescriptors.UsageType, ConnectionId = "c" },
            new ConnectionConfig { Id = "c", Type = "copilot" },
            usage);

        Assert.Contains("future_slot", state.Summary);
        Assert.Equal("false", state.Payload["future_slot_unlimited"]);
        Assert.Equal("1000", state.Payload["future_slot_entitlement"]);
        Assert.Equal("500", state.Payload["future_slot_remaining"]);
        Assert.True(state.Payload.ContainsKey("future_slot_percent_remaining"));
    }
}
