using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation; // WinUI 3 的 MeasureOverride/ArrangeOverride 用 Windows.Foundation.Size（无 Microsoft.UI.Xaml.Size）

namespace Beacon.App.Windows;

/// <summary>
/// 悬浮岛曲面 Path（USER-RPT-4）：几何由代码按宿主实际尺寸喂（<c>IslandSurfaceOutline</c> → PathGeometry），
/// 自身**不参与测量**——否则 Data 边界 + 描边半宽会把父 Grid 撑大，SizeChanged 再喂更大的几何，
/// 形成每帧 +0.5 DIP 的测量反馈环（2026-11-07 imac 真机实测：1907×32 在 81 帧内长到 1907×72，
/// 布局失控后 XAML 抛 InvalidCastException「不支持此接口」，OnLoaded 根本没跑）。
/// Measure 返回 0：曲面只画不占位；Arrange 槽位 = 宿主实际尺寸，Stretch=None 时按 Data 自然尺寸从原点绘制。
/// </summary>
public sealed class IslandSurfacePath : Microsoft.UI.Xaml.Shapes.Path // 隐式 using System.IO 也有 Path → 显式限定
{
    protected override Size MeasureOverride(Size availableSize) => default;

    protected override Size ArrangeOverride(Size finalSize) => base.ArrangeOverride(finalSize);
}
