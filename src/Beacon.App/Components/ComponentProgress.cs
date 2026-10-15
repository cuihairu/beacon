using System;
using Beacon.App.Windows;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 进度组件（Phase 2 基础件 Progress）：静态线性条（底部 4 DIP、圆角 2、track=Value 色 35% 透明、
/// fill 左对齐档位色）——口径与 PinnedHost tile 条一致（2026-10-11 重设计），档位判定单一来源
/// Core ProgressTiers（≥80% warning、≥98% error）。fill 宽 ≥2 DIP 保底可辨；推进动画
/// （Storyboard 动 Width）是 tile 面的布局动画逻辑，不在此静态件内。
/// </summary>
internal static class ComponentProgress
{
    private const int BarHeightDips = 4;
    private const double BarRadiusDips = 2;

    /// <summary>fill 档位色：ProgressTiers 判档，severityColor 解析（UiPalette），无档位用 normalColor。</summary>
    public static global::Windows.UI.Color FillColor(
        double fraction,
        global::Windows.UI.Color normalColor,
        Func<Severity, global::Windows.UI.Color> severityColor)
        => ProgressTiers.Level(fraction) is { } tier ? severityColor(tier) : normalColor;

    /// <summary>静态线性条：返回 (track, fill)；fill 宽 = width × clamp(fraction)，最少 2 DIP。</summary>
    public static (Rectangle Track, Rectangle Fill) Bar(double fraction, double width)
    {
        var clamped = Math.Clamp(fraction, 0, 1);
        Rectangle track = new()
        {
            Height = BarHeightDips,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = new SolidColorBrush(ThemeColors.Value()) { Opacity = 0.35 },
            RadiusX = BarRadiusDips,
            RadiusY = BarRadiusDips,
        };
        Rectangle fill = new()
        {
            Height = BarHeightDips,
            Width = Math.Max(2, width * clamped),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Left,
            RadiusX = BarRadiusDips,
            RadiusY = BarRadiusDips,
        };
        return (track, fill);
    }
}
