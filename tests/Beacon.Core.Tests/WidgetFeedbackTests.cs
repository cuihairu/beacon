using System.Text.RegularExpressions;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 5「统一的空数据、离线与错误状态」：附加提示面四态判定（错误 &gt; 离线 &gt; 空数据 &gt; 无）。
/// 与 UiStringsTests 同集合串行：本班断言本地化标题时语言不得被并行班改掉（见 UiStringsTests 注释）。
/// </summary>
[Collection("UiStrings")]
public sealed class WidgetFeedbackTests
{
    private static readonly Regex MinuteStamp = new("^Last update \\d{2}:\\d{2}$", RegexOptions.Compiled);
    private static readonly Regex SecondStamp = new("^Last update \\d{2}:\\d{2}:\\d{2}$", RegexOptions.Compiled);

    public WidgetFeedbackTests()
    {
        UiStrings.SetLanguage("en"); // 每例归位：静态语言不跨例漂移
    }

    // —— 错误态 ——

    [Fact]
    public void Resolve_ErrorSeverityWithErrorKey_ReturnsErrorWithReason()
    {
        var state = TestData.State(severity: Severity.Error, summary: "拉取失败")
            with
            {
                Payload = new Dictionary<string, string> { [WidgetFeedback.ErrorKey] = "401 unauthorized" },
            };

        var feedback = WidgetFeedback.Resolve(state);

        Assert.Equal(FeedbackKind.Error, feedback.Kind);
        Assert.Equal("Error", feedback.Headline);
        Assert.Equal("401 unauthorized", feedback.Caption);
    }

    [Fact]
    public void Resolve_ErrorSeverityWithErrorSummaryKey_ReturnsError()
    {
        var state = TestData.State(severity: Severity.Critical)
            with
            {
                Payload = new Dictionary<string, string> { [WidgetFeedback.ErrorSummaryKey] = "gateway 502" },
            };

        var feedback = WidgetFeedback.Resolve(state);

        Assert.Equal(FeedbackKind.Error, feedback.Kind);
        Assert.Equal("gateway 502", feedback.Caption);
    }

    [Theory]
    [InlineData(Severity.Success)]
    [InlineData(Severity.Info)]
    [InlineData(Severity.Warning)]
    public void Resolve_BelowErrorSeverity_DoesNotReturnError(Severity severity)
    {
        var state = TestData.State(severity: severity)
            with
            {
                Payload = new Dictionary<string, string> { [WidgetFeedback.ErrorKey] = "reason is ignored below error" },
            };

        var feedback = WidgetFeedback.Resolve(state);

        Assert.NotEqual(FeedbackKind.Error, feedback.Kind);
    }

    [Fact]
    public void Resolve_ErrorSeverityWithoutReasonKey_DoesNotReturnError()
    {
        var feedback = WidgetFeedback.Resolve(TestData.State(severity: Severity.Error));

        Assert.NotEqual(FeedbackKind.Error, feedback.Kind); // Summary 已承载原因，无 payload 键时不另起错误面
    }

    // —— 离线态 ——

    [Fact]
    public void Resolve_Stale_ReturnsOfflineWithMinuteStamp()
    {
        var feedback = WidgetFeedback.Resolve(TestData.State() with { IsStale = true });

        Assert.Equal(FeedbackKind.Offline, feedback.Kind);
        Assert.Equal("Offline", feedback.Headline);
        Assert.Matches(MinuteStamp, feedback.Caption);
    }

    [Fact]
    public void Resolve_UnhealthyConnection_ReturnsOffline()
        => Assert.Equal(FeedbackKind.Offline,
            WidgetFeedback.Resolve(TestData.State() with { ConnectionHealthy = false }).Kind);

    [Fact]
    public void Resolve_ErrorBeatsOffline()
    {
        var state = TestData.State(severity: Severity.Error)
            with
            {
                IsStale = true,
                ConnectionHealthy = false,
                Payload = new Dictionary<string, string> { [WidgetFeedback.ErrorKey] = "reason" },
            };

        Assert.Equal(FeedbackKind.Error, WidgetFeedback.Resolve(state).Kind);
    }

    [Fact]
    public void Resolve_OfflineBeatsEmpty()
        => Assert.Equal(FeedbackKind.Offline,
            WidgetFeedback.Resolve(TestData.State() with { IsStale = true }).Kind); // 空 payload 同例：离线说明比「暂无明细」更有信息量

    // —— 空数据态 ——

    [Fact]
    public void Resolve_EmptyPayload_ReturnsEmpty()
    {
        var feedback = WidgetFeedback.Resolve(TestData.State());

        Assert.Equal(FeedbackKind.Empty, feedback.Kind);
        Assert.Equal("No details", feedback.Headline);
        Assert.Null(feedback.Caption);
    }

    [Fact]
    public void Resolve_ValueHintOnlyPayload_ReturnsEmpty()
    {
        var state = TestData.State()
            with
            {
                Payload = new Dictionary<string, string> { [WidgetFeedback.ValueHintKey] = "38%" },
            };

        Assert.Equal(FeedbackKind.Empty, WidgetFeedback.Resolve(state).Kind); // 数值位提示键不算详情行
    }

    [Fact]
    public void Resolve_HealthyWithDetailRows_ReturnsNone()
    {
        var state = TestData.State()
            with
            {
                Payload = new Dictionary<string, string> { ["repo"] = "owner/repo", ["value_text"] = "38%" },
            };

        var feedback = WidgetFeedback.Resolve(state);

        Assert.Equal(FeedbackKind.None, feedback.Kind);
        Assert.Equal(FeedbackPresentation.None, feedback); // 单例：无提示面不产生新对象
    }

    // —— HasDetailRows ——

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HasDetailRows_WhitespaceValueOnly_ReturnsFalse(bool includeHintKey)
    {
        var payload = new Dictionary<string, string>
        {
            [includeHintKey ? WidgetFeedback.ValueHintKey : "blank"] = "   ",
        };

        Assert.False(WidgetFeedback.HasDetailRows(payload));
    }

    [Fact]
    public void HasDetailRows_AnyNonBlankRow_ReturnsTrue()
        => Assert.True(WidgetFeedback.HasDetailRows(new Dictionary<string, string> { ["repo"] = "owner/repo" }));

    // —— LastUpdate 时戳 ——

    [Fact]
    public void LastUpdate_DefaultIsMinuteFormLocalTime()
    {
        var fetched = TestData.State().FetchedAt;

        Assert.Equal("Last update " + fetched.ToLocalTime().ToString("HH:mm"),
            WidgetFeedback.LastUpdate(fetched));
    }

    [Fact]
    public void LastUpdate_WithSecondsIsSecondFormLocalTime()
    {
        var fetched = TestData.State().FetchedAt;

        Assert.Matches(SecondStamp, WidgetFeedback.LastUpdate(fetched, withSeconds: true));
    }

    // —— 文案本地化 ——

    [Fact]
    public void Resolve_HeadlinesFollowUiStringsLanguage()
    {
        UiStrings.SetLanguage("zh");
        Assert.Equal("离线", WidgetFeedback.Resolve(TestData.State() with { IsStale = true }).Headline);
        Assert.Equal("暂无明细", WidgetFeedback.Resolve(TestData.State()).Headline);

        UiStrings.SetLanguage("en");
        Assert.Equal("Offline", WidgetFeedback.Resolve(TestData.State() with { IsStale = true }).Headline);
    }
}
