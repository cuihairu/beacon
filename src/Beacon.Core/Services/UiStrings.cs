using System.Globalization;

namespace Beacon.Core.Services;

/// <summary>
/// 界面文案目录（2026-10-14 用户单②：看板缺中文 + 托盘菜单随语言切换）。
/// 本仓此前无 i18n 机制（src/ 与 docs/ 全量 grep Localization/LocalizedString/I18n 零命中）——
/// 这就是唯一一套：看板（QuickPanelWindow）、托盘菜单（TrayIconService）、设置页新增控件
/// 全部经此取词，不另起第二套。语言切换经 <see cref="LanguageChanged"/> 广播，订阅方重画文案。
/// 覆盖面 = 看板 + 托盘菜单 + 设置新增项；设置页存量中文文案未整体迁移（记录在案，后续单）。
/// </summary>
public static class UiStrings
{
    private const string En = "en";
    private const string Zh = "zh";

    private static readonly Dictionary<string, string> English = new()
    {
        ["board.notifications"] = "NOTIFICATIONS",
        ["board.markAllRead"] = "Mark all read",
        ["board.overview"] = "OVERVIEW",
        ["board.needsAttention"] = "NEEDS ATTENTION",
        ["board.running"] = "RUNNING",
        ["board.recentEvents"] = "RECENT EVENTS",
        ["board.timeline"] = "EVENT TIMELINE",
        ["board.allClear"] = "all clear",
        ["board.warnError"] = "{0} warn · {1} error",
        ["board.all"] = "All",
        ["board.timelineEmpty"] = "No events yet",
        ["board.notificationsEmpty"] = "You're all caught up",
        ["board.attentionEmpty"] = "No widgets need attention",
        ["board.filterEmpty"] = "No matches — try clearing the filter",
        ["board.clearFilter"] = "Clear filter",
        ["board.footerRunning"] = "Running {0}",
        ["board.footerOk"] = "Healthy {0}",
        ["board.footerHintActions"] = "Click for details · right-click to pin",
        ["board.footerHintWindow"] = "Drag header to move · edges resize · Esc closes",
        ["tray.openPanel"] = "Open Beacon",
        ["tray.notifications"] = "Notifications",
        ["tray.keepPanelOpen"] = "Keep Panel Open",
        ["tray.refreshAll"] = "Refresh All",
        ["tray.settings"] = "Settings",
        ["tray.components"] = "Component Center",
        ["tray.exit"] = "Exit",
        ["settings.language"] = "Language (board & tray apply immediately)",
        ["settings.languageAuto"] = "Auto (follow system)",
        ["settings.edgeCollapse"] = "Edge collapse (panel collapses to a strip at screen edge)",
        ["settings.capsuleIsland"] = "Floating island (capsule resident top-center)",
        ["feedback.offline"] = "Offline",
        ["feedback.error"] = "Error",
        ["feedback.empty"] = "No details",
    };

    private static readonly Dictionary<string, string> Chinese = new()
    {
        ["board.notifications"] = "通知",
        ["board.markAllRead"] = "全部已读",
        ["board.overview"] = "概览",
        ["board.needsAttention"] = "需要关注",
        ["board.running"] = "运行中",
        ["board.recentEvents"] = "最近事件",
        ["board.timeline"] = "事件动态",
        ["board.allClear"] = "一切正常",
        ["board.warnError"] = "{0} 警告 · {1} 错误",
        ["board.all"] = "全部",
        ["board.timelineEmpty"] = "暂无事件",
        ["board.notificationsEmpty"] = "通知已全部已读",
        ["board.attentionEmpty"] = "没有需要关注的组件",
        ["board.filterEmpty"] = "无匹配，试试清除过滤",
        ["board.clearFilter"] = "清除筛选",
        ["board.footerRunning"] = "运行中 {0}",
        ["board.footerOk"] = "正常 {0}",
        ["board.footerHintActions"] = "单击进详情 · 右键钉到桌面",
        ["board.footerHintWindow"] = "拖动顶部移动 · 边缘调整大小 · ESC 关闭",
        ["tray.openPanel"] = "打开 Beacon",
        ["tray.notifications"] = "通知中心",
        ["tray.keepPanelOpen"] = "面板常驻",
        ["tray.refreshAll"] = "全部刷新",
        ["tray.settings"] = "设置",
        ["tray.components"] = "组件中心",
        ["tray.exit"] = "退出",
        ["settings.language"] = "语言（看板与托盘即时生效）",
        ["settings.languageAuto"] = "自动（跟随系统）",
        ["settings.edgeCollapse"] = "贴边收起（拖到屏幕边缘收成细条）",
        ["settings.capsuleIsland"] = "悬浮岛形态（胶囊顶部居中常驻）",
        ["feedback.offline"] = "离线",
        ["feedback.error"] = "错误",
        ["feedback.empty"] = "暂无明细",
    };

    private static IReadOnlyDictionary<string, string> _current = English;

    /// <summary>当前语言（"en"/"zh"；空串入参解析后落其一）。</summary>
    public static string Language { get; private set; } = En;

    /// <summary>语言切换广播（设置页 UI 线程触发；订阅方按自身线程口径重画）。</summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// 显式 "en"/"zh"；空串/其他 = 跟随系统 UI 文化（zh 族 → zh，其余 en；CI 无 ICU 的 invariant
    /// 环境同样落 en）。同值幂等不广播。
    /// </summary>
    public static void SetLanguage(string language)
    {
        var resolved = Resolve(language);
        if (resolved == Language)
        {
            return;
        }
        _current = resolved == Zh ? Chinese : English;
        Language = resolved;
        LanguageChanged?.Invoke();
    }

    /// <summary>取词：当前语言缺键退英语，再缺退键名（不抛——文案缺失不阻断渲染）。</summary>
    public static string Get(string key)
        => _current.TryGetValue(key, out var value) ? value : English.GetValueOrDefault(key, key);

    /// <summary>某语言的全部键（双语键集一致性单测用）。</summary>
    public static IReadOnlyCollection<string> Keys(string language)
        => language == Zh ? Chinese.Keys : English.Keys;

    private static string Resolve(string language)
    {
        var normalized = (language ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is En or Zh)
        {
            return normalized;
        }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Zh ? Zh : En;
    }

    // —— 具名入口（调用点不散字符串键）——

    public static string BoardNotifications => Get("board.notifications");
    public static string BoardMarkAllRead => Get("board.markAllRead");
    public static string BoardOverview => Get("board.overview");
    public static string BoardNeedsAttention => Get("board.needsAttention");
    public static string BoardRunning => Get("board.running");
    public static string BoardRecentEvents => Get("board.recentEvents");
    public static string BoardTimeline => Get("board.timeline");
    public static string BoardAllClear => Get("board.allClear");
    public static string BoardWarnError => Get("board.warnError");
    public static string BoardAll => Get("board.all");
    public static string BoardTimelineEmpty => Get("board.timelineEmpty");
    public static string BoardNotificationsEmpty => Get("board.notificationsEmpty");
    public static string BoardAttentionEmpty => Get("board.attentionEmpty");
    public static string BoardFilterEmpty => Get("board.filterEmpty");
    public static string BoardClearFilter => Get("board.clearFilter");
    public static string BoardFooterRunning => Get("board.footerRunning");
    public static string BoardFooterOk => Get("board.footerOk");
    public static string BoardFooterHintActions => Get("board.footerHintActions");
    public static string BoardFooterHintWindow => Get("board.footerHintWindow");
    public static string TrayOpenPanel => Get("tray.openPanel");
    public static string TrayNotifications => Get("tray.notifications");
    public static string TrayKeepPanelOpen => Get("tray.keepPanelOpen");
    public static string TrayRefreshAll => Get("tray.refreshAll");
    public static string TraySettings => Get("tray.settings");
    public static string TrayComponents => Get("tray.components");
    public static string TrayExit => Get("tray.exit");
    public static string SettingsLanguage => Get("settings.language");
    public static string SettingsLanguageAuto => Get("settings.languageAuto");
    public static string SettingsEdgeCollapse => Get("settings.edgeCollapse");
    public static string SettingsCapsuleIsland => Get("settings.capsuleIsland");
    public static string FeedbackOffline => Get("feedback.offline");
    public static string FeedbackError => Get("feedback.error");
    public static string FeedbackEmpty => Get("feedback.empty");
}
