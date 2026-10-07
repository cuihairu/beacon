namespace Beacon.Core.Models;

/// <summary>Widget 实例配置（widgets.json 单条）。</summary>
public sealed class WidgetConfig
{
    public required string Id { get; init; }
    /// <summary>Widget 类型标识，如 "github.actions.runs"。</summary>
    public required string Type { get; init; }
    public string ConnectionId { get; init; } = "";
    public Dictionary<string, string> Config { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string RefreshTier { get; init; } = RefreshTiers.Default;
    /// <summary>是否钉到桌面（L0 悬浮组件，RFC §6.2.1）。</summary>
    public bool Pinned { get; set; }
    /// <summary>状态灯颜色覆盖 #RRGGBB（B-706：Widget > 级别 > 默认，非法值回退默认）。</summary>
    public string? ColorOverride { get; set; }
    /// <summary>L0 布局由 pins.json 承载，此处仅为便利引用（与 PinsConfig 保持同步）。</summary>
    public PinLayout? PinLayout { get; set; }
}
