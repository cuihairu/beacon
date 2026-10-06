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
    /// <summary>用户通知规则；空列表时使用 DefaultNotificationRules（RFC §8）。</summary>
    public List<NotificationRule> NotificationRules { get; set; } = [];
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
