using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 岛进度条组件（ISLAND-RING-1，2026-11-07 用户令：进度条与进度环同为组件中心里的组件）：
/// 圆角轨道 + 左锚定填充，份额=fill.Width/轨宽。与环同数据源（ok 份额+级别色），只是载体不同。
/// 每次状态更新整树重建（同 StatusRing「每渲染重建」口径），填充推进不做补间。
/// </summary>
internal static class BarProgressBadge
{
    /// <summary>建条：fraction ∈ [0,1]（越界钳位）；fillColor=进度/级别色，trackColor=轨道色。</summary>
    public static FrameworkElement Build(double width, double height, double fraction,
        global::Windows.UI.Color fillColor, global::Windows.UI.Color trackColor)
    {
        fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0.0, 1.0) : 0.0;
        var host = new Grid { Width = width, Height = height, VerticalAlignment = VerticalAlignment.Center };
        host.Children.Add(new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = new SolidColorBrush(trackColor),
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        if (fraction > 0)
        {
            host.Children.Add(new Border
            {
                Width = Math.Max(height, width * fraction), // 零份额不落块；最小宽=高（圆头不缩成竖线）
                Height = height,
                CornerRadius = new CornerRadius(height / 2),
                Background = new SolidColorBrush(fillColor),
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }

        return host;
    }
}
