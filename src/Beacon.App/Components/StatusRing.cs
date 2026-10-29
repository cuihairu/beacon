using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 状态圆环（2026-10-29 UI 升级）：胶囊健康环与面板来源卡共用。双 Ellipse 同心——
/// track=值色 35% 透明、fill=状态色 StrokeDashArray 弧段（中线周长 π×(size−thickness)，
/// DashOffset 四分之一周长从 12 点方向起笔）——口径与 ComponentSurfaceRenderer.Ring（2026-10-18
/// 评审 P1「圆环」模板，CI 实证）一致，抽公共件收编两处。每次状态更新整树重建（与行/chip
/// 同「每渲染重建」口径），弧段推进不做补间——状态变化本就低频。
/// </summary>
internal static class StatusRing
{
    /// <summary>建环：fraction ∈ [0,1]（越界钳位），color=状态色（环色即级别语义）。</summary>
    public static FrameworkElement Build(double size, double thickness, double fraction, global::Windows.UI.Color color)
    {
        fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0.0, 1.0) : 0.0;
        var circumference = Math.PI * (size - thickness);
        var host = new Grid { Width = size, Height = size };
        host.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(color) { Opacity = 0.35 },
            StrokeThickness = thickness,
        });
        host.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeDashArray = new DoubleCollection { Math.Max(fraction * circumference, 0), circumference },
            StrokeDashOffset = circumference / 4,
        });
        return host;
    }
}
