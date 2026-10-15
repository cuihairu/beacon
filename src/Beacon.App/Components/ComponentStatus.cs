using Beacon.App.Services;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 状态指示组件（Phase 2 基础件 Status Indicator）：级别灯 + 离线空心变体。
/// 已采用：DetailWindow 头部（10 口径实心）；QuickPanel 行走 ComponentIcon.StatusDot（8 口径）；
/// 空心变体口径同胶囊离线横幅（透明底 + 三级描边 1.5）。
/// </summary>
internal static class ComponentStatus
{
    /// <summary>级别灯：实心圆点，色由 UiPalette 解析后传入。</summary>
    public static Ellipse Indicator(Brush fill, double size = 10) => new()
    {
        Width = size,
        Height = size,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = fill,
    };

    /// <summary>离线空心灯（灰空心，§4.1 Offline）：透明底 + 描边。</summary>
    public static Ellipse OfflineHollow(Brush stroke, double size = 8) => new()
    {
        Width = size,
        Height = size,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = new SolidColorBrush(SeverityPalette.Rgb(0, 0, 0, 0)),
        Stroke = stroke,
        StrokeThickness = 1.5,
    };
}
