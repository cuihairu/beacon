using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 数值组件（Phase 2 基础件 Metric）：值位 + 单位小字横排。tile 值位与详情数值面
/// 在 Phase 3 绑定引擎接通后收编（当前 WidgetValueHint 产文本、各窗自拼）；口径：值位字号调用方定，
/// 单位 FontCaption 次要色。
/// </summary>
internal static class ComponentMetric
{
    public static StackPanel Value(string value, string? unit, Brush foreground, double valueFontSize = DesignTokens.FontHeader)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DesignTokens.Space2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = value,
                    FontSize = valueFontSize,
                    Foreground = foreground,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            },
        };
        if (unit is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = unit,
                FontSize = DesignTokens.FontCaption,
                Foreground = foreground,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Bottom,
            });
        }
        return panel;
    }
}
