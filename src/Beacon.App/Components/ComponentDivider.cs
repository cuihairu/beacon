using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 分隔组件（Phase 2 基础件 Divider）：水平 1px 线，色由调用面给（主题描边）。
/// 供分组/节间距面收编；当前无调用面。
/// </summary>
internal static class ComponentDivider
{
    public static Rectangle Horizontal(Brush stroke) => new()
    {
        Height = 1,
        Fill = stroke,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
}
