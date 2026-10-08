namespace Beacon.Core.Models;

public enum PinAnchor
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public sealed record PinOffset(double X, double Y);

/// <summary>L0 悬浮组件布局：按「显示器 + 锚点角 + DIP 偏移」持久化，支持吸边收起（RFC §6.2.3）。</summary>
public sealed class PinLayout
{
    /// <summary>显示器标识（设备名+分辨率），显示器热插拔后按锚点恢复；拖动/拓扑回收会原位改写。</summary>
    public required string Monitor { get; set; }
    public required PinAnchor Anchor { get; set; }
    /// <summary>相对锚点角的偏移（DIP，随 DPI 缩放）。</summary>
    public required PinOffset OffsetDips { get; set; }
    /// <summary>吸边收起态：细条/圆点，悬停展开（RFC §6.2.2）。</summary>
    public bool Collapsed { get; set; }
    /// <summary>独立悬浮框模式的窗口位置（物理像素，按组件各记；null = 未拖过，按序级联）。panel 模式不写。</summary>
    public int? FloatingX { get; set; }
    public int? FloatingY { get; set; }
}
