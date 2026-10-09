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
    /// <summary>配置结构版本：0=旧配置（缺字段），1=已执行状态胶囊默认关迁移。每次结构迁移 +1。</summary>
    public int ConfigVersion { get; set; }
    /// <summary>L0 悬浮形态：panel = 单宿主多 tile（默认）；floating = 每钉选组件一窗、可拖桌面任意位置（切换重启生效）。</summary>
    public string PinDisplayMode { get; set; } = "panel";
    /// <summary>数量悬浮窗（数值/额度类独立悬浮窗）总开关，默认关（拍板 2026-10-08：功能保留改可配置——关=桌面零残留，开=数值类恢复悬浮窗）。</summary>
    public bool NumericFloatingEnabled { get; set; } = false;
    /// <summary>全局检查频率（秒，0=按各组件刷新档策略表，默认）。设置「检查频率」落点：
    /// 组件级检测间隔优先，未设组件级的按此值走（2026-10-09 用户令：轮询节奏可配）。</summary>
    public int PollIntervalSeconds { get; set; } = 0;
    /// <summary>升级迁移标记：NumericFloatingEnabled 强制置关是一次性动作（老包开关 ON 的配置升级即回干净桌面），已执行不再动用户选择。</summary>
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
