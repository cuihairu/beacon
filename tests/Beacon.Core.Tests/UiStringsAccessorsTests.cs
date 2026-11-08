using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// UiStrings 具名入口全覆盖（Batch 5）：每个具名访问器都读一次并断言非空，
/// 不依赖当前语言（静态全局量，CI 默认 en，断言非空即可，不改语言不串扰并行类）。
/// 覆盖 Board*/Tray*/Settings*/Feedback* 表达式体属性。
/// </summary>
public sealed class UiStringsAccessorsTests
{
    private static void AssertNonEmpty(string value) => Assert.False(string.IsNullOrWhiteSpace(value));

    [Fact]
    public void BoardAccessors_AllNonEmpty()
    {
        AssertNonEmpty(UiStrings.BoardNotifications);
        AssertNonEmpty(UiStrings.BoardMarkAllRead);
        AssertNonEmpty(UiStrings.BoardOverview);
        AssertNonEmpty(UiStrings.BoardNeedsAttention);
        AssertNonEmpty(UiStrings.BoardRunning);
        AssertNonEmpty(UiStrings.BoardRecentEvents);
        AssertNonEmpty(UiStrings.BoardTimeline);
        AssertNonEmpty(UiStrings.BoardAllClear);
        AssertNonEmpty(UiStrings.BoardWarnError);
        AssertNonEmpty(UiStrings.BoardAll);
        AssertNonEmpty(UiStrings.BoardTimelineEmpty);
        AssertNonEmpty(UiStrings.BoardNotificationsEmpty);
        AssertNonEmpty(UiStrings.BoardAttentionEmpty);
        AssertNonEmpty(UiStrings.BoardFilterEmpty);
        AssertNonEmpty(UiStrings.BoardClearFilter);
        AssertNonEmpty(UiStrings.BoardFooterRunning);
        AssertNonEmpty(UiStrings.BoardFooterOk);
        AssertNonEmpty(UiStrings.BoardFooterHintActions);
        AssertNonEmpty(UiStrings.BoardFooterHintWindow);
    }

    [Fact]
    public void TrayAccessors_AllNonEmpty()
    {
        AssertNonEmpty(UiStrings.TrayOpenPanel);
        AssertNonEmpty(UiStrings.TrayNotifications);
        AssertNonEmpty(UiStrings.TrayKeepPanelOpen);
        AssertNonEmpty(UiStrings.TrayRefreshAll);
        AssertNonEmpty(UiStrings.TraySettings);
        AssertNonEmpty(UiStrings.TrayComponents);
        AssertNonEmpty(UiStrings.TrayExit);
    }

    [Fact]
    public void SettingsAccessors_AllNonEmpty()
    {
        AssertNonEmpty(UiStrings.SettingsLanguage);
        AssertNonEmpty(UiStrings.SettingsLanguageAuto);
        AssertNonEmpty(UiStrings.SettingsEdgeCollapse);
        AssertNonEmpty(UiStrings.SettingsCapsuleIsland);
    }

    [Fact]
    public void FeedbackAccessors_AllNonEmpty()
    {
        AssertNonEmpty(UiStrings.FeedbackOffline);
        AssertNonEmpty(UiStrings.FeedbackError);
        AssertNonEmpty(UiStrings.FeedbackEmpty);
    }
}
