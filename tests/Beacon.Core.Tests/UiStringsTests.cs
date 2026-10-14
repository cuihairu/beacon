using System.Globalization;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>2026-10-14 用户单②：界面文案目录（唯一 i18n 机制）——双语键集一致、切换即时生效、缺键回退。</summary>
public class UiStringsTests
{
    public UiStringsTests()
    {
        UiStrings.SetLanguage("en"); // 每例归位：静态状态不跨例漂移
    }

    [Fact]
    public void SetLanguage_zh_switches_board_strings_immediately()
    {
        UiStrings.SetLanguage("zh");
        Assert.Equal("通知", UiStrings.BoardNotifications);
        Assert.Equal("全部已读", UiStrings.BoardMarkAllRead);
        Assert.Equal("需要关注", UiStrings.BoardNeedsAttention);
        Assert.Equal("概览", UiStrings.BoardOverview);
        Assert.Equal("运行中", UiStrings.BoardRunning);
        Assert.Equal("最近事件", UiStrings.BoardRecentEvents);
        Assert.Equal("打开 Beacon", UiStrings.TrayOpenPanel);
        Assert.Equal("通知中心", UiStrings.TrayNotifications);
    }

    [Fact]
    public void SetLanguage_en_roundtrip_restores_english()
    {
        UiStrings.SetLanguage("zh");
        UiStrings.SetLanguage("en");
        Assert.Equal("NOTIFICATIONS", UiStrings.BoardNotifications);
        Assert.Equal("Open Beacon", UiStrings.TrayOpenPanel);
        Assert.Equal("Notifications", UiStrings.TrayNotifications);
    }

    [Fact]
    public void English_and_chinese_key_sets_match()
    {
        var en = UiStrings.Keys("en").OrderBy(k => k, StringComparer.Ordinal).ToList();
        var zh = UiStrings.Keys("zh").OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(en, zh);
    }

    [Fact]
    public void WarnError_format_has_two_slots_in_both_languages()
    {
        Assert.Contains("{0}", UiStrings.Get("board.warnError"));
        Assert.Contains("{1}", UiStrings.Get("board.warnError"));
        UiStrings.SetLanguage("zh");
        Assert.Contains("{0}", UiStrings.Get("board.warnError"));
        Assert.Contains("{1}", UiStrings.Get("board.warnError"));
    }

    [Fact]
    public void Get_unknown_key_falls_back_to_key_name()
    {
        Assert.Equal("board.nonexistent", UiStrings.Get("board.nonexistent"));
    }

    [Fact]
    public void SetLanguage_invalid_value_follows_system_culture()
    {
        // 非法值 = 跟随系统（测试环境非 zh UI 文化 → en；invariant 环境同样落 en）
        var expected = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh" : "en";
        UiStrings.SetLanguage("klingon");
        Assert.Equal(expected, UiStrings.Language);
    }

    [Fact]
    public void LanguageChanged_fires_only_on_actual_change()
    {
        var fired = 0;
        UiStrings.LanguageChanged += Count;
        try
        {
            UiStrings.SetLanguage("zh");
            UiStrings.SetLanguage("zh"); // 同值幂等不广播
            Assert.Equal(1, fired);
            UiStrings.SetLanguage("en");
            Assert.Equal(2, fired);
        }
        finally
        {
            UiStrings.LanguageChanged -= Count;
        }
        void Count() => fired++;
    }

    [Fact]
    public void Language_reports_current_language_id()
    {
        UiStrings.SetLanguage("zh");
        Assert.Equal("zh", UiStrings.Language);
        UiStrings.SetLanguage("en");
        Assert.Equal("en", UiStrings.Language);
    }
}
