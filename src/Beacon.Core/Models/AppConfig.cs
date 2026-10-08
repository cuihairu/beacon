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
    /// <summary>L1 胶囊开关（胶囊本身即常驻 tile，可与 L0 分显示器，RFC §16 决策 6）。</summary>
    public bool ShowCapsule { get; set; } = true;
    /// <summary>L0 悬浮形态：panel = 单宿主多 tile（默认）；floating = 每钉选组件一窗、可拖桌面任意位置（切换重启生效）。</summary>
    public string PinDisplayMode { get; set; } = "panel";
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
