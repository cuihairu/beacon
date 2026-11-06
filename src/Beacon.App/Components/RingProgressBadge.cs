using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 岛进度环徽标（ISLAND-RING-1，2026-11-07 用户令「人家都有圆圈展示进度的」）：底环 + 进度弧 +
/// 中心数字三层叠放。弧的几何学 BrimDeck UI.Ring 的口径（源码技法对照，实现是本仓自己的 WinUI 代码）：
/// ① 起笔 12 点方向 (center, center−r)；② ArcSegment 终点参数化 (center−r·sinθ, center−r·cosθ)，
/// θ=fraction·2π、IsLargeArc=fraction&gt;.5——超过半圆必须翻转 IsLargeArc，否则长弧被画成短弧；
/// ③ SweepDirection.Counterclockwise（其面板环同向）；④ 两端 PenLineCap.Round，弧起收读作圆头而非切口；
/// ⑤ fraction≥.999 直接整圆 Ellipse——0.1% 的缺口在像素上就是毛边，ArcSegment 画不出满圆。
/// 中心数字直接 Grid 居中：WPF 需 glyph-outline 居中是因其基线吸附整设备像素，WinUI 的 TextBlock
/// 在 Grid 中光学居中无此坑。每次状态更新整树重建（与 StatusRing「每渲染重建」同口径），
/// 弧段推进不做补间——状态变化低频。
/// </summary>
internal static class RingProgressBadge
{
    /// <summary>建环：fraction ∈ [0,1]（越界钳位）；arcColor=进度/级别色，trackColor=底环色，
    /// centerText=中心数字（空串=不落字），textColor=数字色。</summary>
    public static FrameworkElement Build(double size, double thickness, double fraction,
        global::Windows.UI.Color arcColor, global::Windows.UI.Color trackColor,
        string centerText, global::Windows.UI.Color textColor)
    {
        fraction = double.IsFinite(fraction) ? Math.Clamp(fraction, 0.0, 1.0) : 0.0;
        var host = new Grid { Width = size, Height = size };
        host.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(trackColor),
            StrokeThickness = thickness,
        });
        if (fraction >= 0.999)
        {
            host.Children.Add(new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = new SolidColorBrush(arcColor),
                StrokeThickness = thickness,
            });
        }
        else if (fraction > 0)
        {
            var radius = size / 2 - thickness / 2;
            var center = size / 2;
            var angle = fraction * Math.PI * 2;
            var figure = new PathFigure
            {
                StartPoint = new global::Windows.Foundation.Point(center, center - radius),
                IsClosed = false,
            };
            // WinUI 的 ArcSegment 没有 WPF 的 6 参构造（CS1729，CI 42281360170 实证）——属性初始化器逐项设
            figure.Segments.Add(new ArcSegment
            {
                Point = new global::Windows.Foundation.Point(center - radius * Math.Sin(angle), center - radius * Math.Cos(angle)),
                Size = new global::Windows.Foundation.Size(radius, radius),
                RotationAngle = 0,
                IsLargeArc = fraction > 0.5, // 大弧标志按过半翻转：ArcSegment 恒走劣弧除非显式要优弧
                SweepDirection = SweepDirection.Counterclockwise,
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            // System.IO.Path 与 XAML Shapes.Path 同名（CS0104）——本文件只用形状 Path，显式限定
            host.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = geometry,
                Stroke = new SolidColorBrush(arcColor),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            });
        }

        if (centerText.Length > 0)
        {
            host.Children.Add(new TextBlock
            {
                Text = centerText,
                FontSize = Math.Max(9, size * 0.45),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(textColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return host;
    }
}
