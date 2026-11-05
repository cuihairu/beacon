namespace Beacon.Core.Models;

/// <summary>
/// 快捷面板（看板）窗口布局（PANEL-UPGRADE-1，shell-state.json quickPanelLayout 段）：
/// 看板可拖拽调尺寸，「显示器 + 锚点角 + DIP 偏移 + 窗口 DIP 尺寸」持久化——
/// 锚点口径与 PinLayout（四角）同构（PinLayoutMath 互算），尺寸记 DIP 随 DPI 缩放还原。
/// 缺失段=看板默认 720x560 右下（胶囊上方），不覆盖其他 shell-state 段。
/// </summary>
public sealed class QuickPanelLayout
{
    /// <summary>显示器标识（设备名+分辨率），显示器热插拔后按名恢复（PinLayout.Monitor 同口径）。</summary>
    public required string Monitor { get; set; }

    public required PinAnchor Anchor { get; set; }

    /// <summary>相对锚点角的偏移（DIP，随 DPI 缩放）。</summary>
    public required PinOffset OffsetDips { get; set; }

    /// <summary>窗口客户区宽（DIP，随 DPI 缩放）。</summary>
    public required double Width { get; set; }

    /// <summary>窗口客户区高（DIP，随 DPI 缩放）。</summary>
    public required double Height { get; set; }
}
