using Beacon.Core.Selectors;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// 额度摘要/重置解析边界收口（Batch 2）：BigModel Summarize 空态/重置段/其余窗列表、
/// Ark 耗尽响应窗口命名与重置时间防御解析（JSON 嵌套/ISO/毫秒秒 epoch/正则兜底）、
/// MiMo 控制台与本地计数非对象根/空条目兜底。均走 internal static 纯函数，无 IO。
/// </summary>
public sealed class QuotaSummarizeEdgeTests
{
    // ---------- BigModel Summarize ----------

    private static readonly BigModelUsageProvider.PlanUsage Empty =
        new(null, null, null, null);

    [Fact]
    public void BigModel_Summarize_AllNull_ReturnsEmpty()
        => Assert.Equal("", BigModelUsageProvider.Summarize(Empty, new QuotaWindowDecision(null, null, null, null)));

    [Fact]
    public void BigModel_Summarize_NoDisplay_NoPercent_WithReset_FallsBackToReset()
    {
        // 窗口存在但无百分比（percent 缺失不进摘要）→ 空态兜底只报重置时间
        var usage = Empty with { Rolling = new BigModelUsageProvider.UsageWindow(null, 1_730_000_000_000) };
        var summary = BigModelUsageProvider.Summarize(usage, new QuotaWindowDecision(null, null, null, null));

        Assert.StartsWith(" · ", summary);
        Assert.EndsWith(" 重置", summary);
    }

    [Fact]
    public void BigModel_Summarize_NoDisplay_NoPercent_NoReset_FallsBackToWindowData()
    {
        var usage = Empty with { Rolling = new BigModelUsageProvider.UsageWindow(null, null) };
        var summary = BigModelUsageProvider.Summarize(usage, new QuotaWindowDecision(null, null, null, null));

        Assert.Equal(" · 窗口数据", summary);
    }

    [Fact]
    public void BigModel_Summarize_DisplayWithReset_IncludesResetSegment()
    {
        var usage = Empty with { Rolling = new BigModelUsageProvider.UsageWindow(46.6, 1_730_000_000_000) };
        var decision = new QuotaWindowDecision(null, null, QuotaWindow.FiveHour, 46.6);
        var summary = BigModelUsageProvider.Summarize(usage, decision);

        Assert.Contains("5h 窗口已用 46.6%", summary);
        Assert.Contains("重置", summary);
    }

    [Fact]
    public void BigModel_Summarize_DisplayWithoutReset_OmitsResetSegment()
    {
        var usage = Empty with { Rolling = new BigModelUsageProvider.UsageWindow(46.6, null) };
        var decision = new QuotaWindowDecision(null, null, QuotaWindow.FiveHour, 46.6);
        var summary = BigModelUsageProvider.Summarize(usage, decision);

        Assert.Contains("5h 窗口已用 46.6%", summary);
        Assert.DoesNotContain("重置", summary);
    }

    [Fact]
    public void BigModel_Summarize_OtherWindows_ListedAfterDisplay()
    {
        var usage = Empty with
        {
            Rolling = new BigModelUsageProvider.UsageWindow(46.6, null),
            Weekly = new BigModelUsageProvider.UsageWindow(80, null),
            Monthly = new BigModelUsageProvider.UsageWindow(50, null),
        };
        var decision = new QuotaWindowDecision(null, null, QuotaWindow.FiveHour, 46.6);
        var summary = BigModelUsageProvider.Summarize(usage, decision);

        Assert.Contains("5h 窗口已用 46.6%", summary);
        Assert.Contains("周 80%", summary);
        Assert.Contains("月 50%", summary);
    }

    [Fact]
    public void BigModel_FormatReset_FormatsLocalDateTime()
    {
        var formatted = BigModelUsageProvider.FormatReset(1_730_000_000_000);
        Assert.Matches(@"^\d{2}-\d{2} \d{2}:\d{2}$", formatted);
    }

    // ---------- Ark TryParseResetSeconds ----------

    [Fact]
    public void Ark_ResetSeconds_JsonEpochSeconds()
        => Assert.Equal(1_730_000_000, ArkUsageProvider.TryParseResetSeconds("""{"reset_timestamp":1730000000}"""));

    [Fact]
    public void Ark_ResetSeconds_JsonEpochMillis_Normalized()
        => Assert.Equal(1_730_000_000, ArkUsageProvider.TryParseResetSeconds("""{"resetTime":1730000000000}"""));

    [Fact]
    public void Ark_ResetSeconds_NestedObject_DescendantScan()
        => Assert.Equal(1_730_000_000, ArkUsageProvider.TryParseResetSeconds("""{"error":{"reset_at":1730000000}}"""));

    [Fact]
    public void Ark_ResetSeconds_IsoString_Parsed()
    {
        var expected = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.Equal(expected, ArkUsageProvider.TryParseResetSeconds("""{"nextResetTime":"2026-12-01T00:00:00Z"}"""));
    }

    [Fact]
    public void Ark_ResetSeconds_JsonWithoutResetKey_ReturnsNull()
        => Assert.Null(ArkUsageProvider.TryParseResetSeconds("""{"foo":"bar"}"""));

    [Fact]
    public void Ark_ResetSeconds_NonJson_IsoRegex()
    {
        var expected = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.Equal(expected, ArkUsageProvider.TryParseResetSeconds("reset at 2026-12-01T00:00:00Z"));
    }

    [Fact]
    public void Ark_ResetSeconds_NonJson_MillisRegex()
        => Assert.Equal(1_730_000_000, ArkUsageProvider.TryParseResetSeconds("next reset 1730000000000"));

    [Fact]
    public void Ark_ResetSeconds_NonJson_SecondsRegex()
        => Assert.Equal(1_730_000_000, ArkUsageProvider.TryParseResetSeconds("reset 1730000000"));

    [Fact]
    public void Ark_ResetSeconds_NonJson_NoMatch_ReturnsNull()
        => Assert.Null(ArkUsageProvider.TryParseResetSeconds("hello world"));

    // ---------- Ark ParseExhaustedResponse ----------

    [Fact]
    public void Ark_Exhausted_EmptyBody_ReturnsNull()
        => Assert.Null(ArkUsageProvider.ParseExhaustedResponse(""));

    [Fact]
    public void Ark_Exhausted_NoQuotaCode_ReturnsNull()
        => Assert.Null(ArkUsageProvider.ParseExhaustedResponse("rate limited, retry later"));

    [Fact]
    public void Ark_Exhausted_UnknownWindow_GenericNote()
    {
        var usage = ArkUsageProvider.ParseExhaustedResponse("insufficient_quota");

        Assert.NotNull(usage);
        Assert.Equal("AccountQuotaExceeded", usage!.ExhaustedNote);
        Assert.Null(usage.Session);
        Assert.Null(usage.Weekly);
        Assert.Null(usage.Monthly);
    }

    [Fact]
    public void Ark_Exhausted_WeeklyWindow_MapsWeekly()
    {
        var usage = ArkUsageProvider.ParseExhaustedResponse("AccountQuotaExceeded weekly quota");

        Assert.NotNull(usage);
        Assert.Equal(100, usage!.Weekly!.Percent);
    }

    [Fact]
    public void Ark_Exhausted_MonthlyWindow_ChineseKeyword_MapsMonthly()
    {
        var usage = ArkUsageProvider.ParseExhaustedResponse("AccountQuotaExceeded 月度");

        Assert.NotNull(usage);
        Assert.Equal(100, usage!.Monthly!.Percent);
    }

    [Fact]
    public void Ark_Exhausted_SessionWindow_MapsSession()
    {
        var usage = ArkUsageProvider.ParseExhaustedResponse("insufficient_quota 5h");

        Assert.NotNull(usage);
        Assert.Equal(100, usage!.Session!.Percent);
    }

    [Fact]
    public void Ark_Exhausted_WithResetText_CarriesResetSeconds()
    {
        var usage = ArkUsageProvider.ParseExhaustedResponse("AccountQuotaExceeded weekly reset at 1730000000");

        Assert.NotNull(usage);
        Assert.Equal(1_730_000_000, usage!.Weekly!.ResetSeconds);
    }

    [Fact]
    public void Ark_FormatReset_FormatsLocalDateTime()
    {
        var formatted = ArkUsageProvider.FormatReset(1_730_000_000);
        Assert.Matches(@"^\d{2}-\d{2} \d{2}:\d{2}$", formatted);
    }

    // ---------- MiMo ----------

    [Fact]
    public void MiMo_ConsoleUsage_NonObjectRoot_ReturnsNull()
        => Assert.Null(MiMoUsageProvider.ParseConsoleUsage("[1,2]"));

    [Fact]
    public void MiMo_ConsoleUsage_EmptyItems_ReturnsNull()
        => Assert.Null(MiMoUsageProvider.ParseConsoleUsage("""{"monthUsage":{"items":[]}}"""));

    [Fact]
    public void MiMo_ConsoleUsage_NonObjectItem_Skipped()
    {
        var usage = MiMoUsageProvider.ParseConsoleUsage("""
            {"monthUsage":{"items":[{"name":"month_total_token","used":1,"limit":2},5]}}
            """);

        Assert.NotNull(usage);
        Assert.Equal(50, usage!.Percent, 5);
    }

    [Fact]
    public void MiMo_LocalCount_NonObjectRoot_ReturnsNull()
        => Assert.Null(MiMoUsageProvider.ParseLocalCount("[]"));

    [Fact]
    public void MiMo_LocalCount_NoCountKey_ReturnsNull()
        => Assert.Null(MiMoUsageProvider.ParseLocalCount("""{"foo":"bar"}"""));
}
