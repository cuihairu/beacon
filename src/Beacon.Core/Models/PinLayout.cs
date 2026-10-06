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
    /// <summary>显示器标识（设备名+分辨率），显示器热插拔后按锚点恢复。</summary>
    public required string Monitor { get; init; }
    public required PinAnchor Anchor { get; init; }
    /// <summary>相对锚点角的偏移（DIP，随 DPI 缩放）。</summary>
    public required PinOffset OffsetDips { get; init; }
    /// <summary>吸边收起态：细条/圆点，悬停展开（RFC §6.2.2）。</summary>
    public bool Collapsed { get; set; }
}
