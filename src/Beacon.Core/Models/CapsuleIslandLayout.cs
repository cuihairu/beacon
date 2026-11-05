namespace Beacon.Core.Models;

/// <summary>
/// 悬浮岛布局（display-modes-analysis §7 Phase 2）：顶部居中锚点的「显示器 + DIP 偏移」持久化
/// （shell-state.json islandLayout 段）。与 PinLayout（四角锚点）分开存——两种形态各有默认位
/// （岛=顶中、经典胶囊=右下），互不覆盖：切形态各回各的默认/记忆位。
/// </summary>
public sealed class CapsuleIslandLayout
{
    /// <summary>显示器标识（设备名+分辨率），显示器热插拔后按名恢复（PinLayout.Monitor 同口径）。</summary>
    public required string Monitor { get; set; }

    /// <summary>相对「工作区顶缘中点」的偏移（DIP，随 DPI 缩放）：X 正值=窗口中心在屏幕中心左侧。</summary>
    public required PinOffset OffsetDips { get; set; }
}
