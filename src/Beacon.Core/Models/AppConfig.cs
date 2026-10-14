namespace Beacon.Core.Models;

/// <summary>全局设置（config.json）。</summary>
public sealed class AppConfig
{
    /// <summary>全局热键，如 "Ctrl+Alt+B"，设置可改（RFC §38）。</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+B";
    public bool LaunchOnStartup { get; set; } = true;
    /// <summary>system / light / dark（RFC §42）。</summary>
    public string Theme { get; set; } = "system";
    public double UiOpacity { get; set; } = 1.0;
    /// <summary>L1 状态胶囊开关（胶囊本身即常驻 tile，可与 L0 分显示器，RFC §16 决策 6）。
    /// 2026-10-09 用户令：默认关闭（存量配置由 JsonConfigurationStore 一次性迁移拉平，设置里可再打开）。</summary>
    public bool ShowCapsule { get; set; } = false;
    /// <summary>L2 快捷面板常驻（2026-10-11 用户令「右键菜单前打勾，显示消息的面板就可以一直在」）：
    /// 开=托盘菜单 Keep Panel Open 打勾，面板失焦不再自动隐藏（Esc/再次点托盘仍可关，重启后保持勾选）；
    /// 关=Flyout 瞬态语义（RFC §6.4 默认，失焦自隐）。</summary>
    public bool KeepQuickPanelOpen { get; set; }
    /// <summary>配置结构版本：0=旧配置（缺字段），1=已执行状态胶囊默认关迁移。每次结构迁移 +1。</summary>
    public int ConfigVersion { get; set; }
    /// <summary>L0 悬浮形态：panel = 单宿主多 tile（默认）；floating = 每钉选组件一窗、可拖桌面任意位置（切换重启生效）。</summary>
    public string PinDisplayMode { get; set; } = "panel";
    /// <summary>数量悬浮窗（数值/额度类独立悬浮窗）总开关。默认开（2026-10-11 用户令翻转，
    /// 推翻 2026-10-08 的默认关拍板）：数值/额度类恢复悬浮窗；想桌面零残留可在设置里关。</summary>
    public bool NumericFloatingEnabled { get; set; } = true;
    /// <summary>全局检查频率（秒，0=按各组件刷新档策略表，默认）。设置「检查频率」落点：
    /// 组件级检测间隔优先，未设组件级的按此值走（2026-10-09 用户令：轮询节奏可配）。</summary>
    public int PollIntervalSeconds { get; set; } = 0;
    /// <summary>界面语言（2026-10-14 用户单②）："" = 跟随系统 UI 文化（zh 族 → 中文，其余英文）；
    /// "zh"/"en" 显式。切换即时生效面 = 看板 + 托盘菜单（设置页存量中文文案暂未整体迁移）。</summary>
    public string Language { get; set; } = "";
    /// <summary>贴边收起开关（2026-10-14 用户单③）：拖到屏幕边缘贴死才收成细条。默认开 = 保持
    /// 已发布行为（此前无开关时代的既定观感，翻转默认属行为变更须用户拍板）；关 = 拖边只吸附不收起，
    /// 且当前已收起的立刻弹回展开（设置切换即时生效）。</summary>
    public bool EdgeCollapseEnabled { get; set; } = true;
    /// <summary>设置窗口左栏宽度（DIP）。拖拽分隔条落点，启动恢复（2026-10-10 用户令「能拖」）；
    /// 运行时钳位 [200, 420]，0/非法=默认 264。</summary>
    public double SettingsSidebarWidth { get; set; } = 264;
    /// <summary>设置窗口尺寸记忆（DIP）。0=未记忆用默认 880×640；拖窗结束防抖落盘（2026-10-10 用户令）。</summary>
    public double SettingsWindowWidth { get; set; }
    public double SettingsWindowHeight { get; set; }
    /// <summary>已废弃（2026-10-11 用户令数量悬浮窗改默认开，一次性强制置关迁移随之撤除）：
    /// 字段保留只为旧配置 JSON 兼容，程序不再读不再写。</summary>
    public bool NumericFloatingResetDone { get; set; }
    /// <summary>用户通知规则；空列表时使用 DefaultNotificationRules（RFC §8）。</summary>
    public List<NotificationRule> NotificationRules { get; set; } = [];
    /// <summary>外观：级别色覆盖与动效（config.json appearance 段，RFC §4.1/§6.2.8/§9.1）。</summary>
    public AppearanceConfig Appearance { get; set; } = new();
}

/// <summary>外观配置：级别色按级别全局覆盖（Widget 级另存 widgets.json colorOverride）。</summary>
public sealed class AppearanceConfig
{
    /// <summary>级别色覆盖：info/success/warning/error/critical/offline → #RRGGBB（缺省键用默认色表）。</summary>
    public Dictionary<string, string> SeverityColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public MotionConfig Motion { get; set; } = new();
    /// <summary>tile 额度显示方式（2026-10-11 用户令翻转默认：恢复早期进度条表现）：
    /// "bar"=底部 4DIP 线性进度条 + NN% 静态读数（默认）；"pool"=圆池注水（旧特效，仅显式选择）。
    /// 组件级 widgets.json config.progressStyle 可覆盖；旧配置已显式写 "pool" 的保持不动（不迁移不覆盖）。</summary>
    public string ProgressStyle { get; set; } = "bar";
}

/// <summary>动效配置（B-707 承载行为；RFC §6.2.8：默认适度动效、可调可关）。</summary>
public sealed class MotionConfig
{
    /// <summary>full / reduced（默认）/ off。</summary>
    public string Mode { get; set; } = "reduced";
    /// <summary>0.5–2.0，缩放动效时长与幅度。</summary>
    public double Intensity { get; set; } = 1.0;
}

/// <summary>L0 tile 布局（pins.json）。</summary>
public sealed class PinTile
{
    public required string WidgetId { get; init; }
    public required PinLayout Layout { get; init; }
}

public sealed class PinsConfig
{
    public List<PinTile> Tiles { get; set; } = [];
}
