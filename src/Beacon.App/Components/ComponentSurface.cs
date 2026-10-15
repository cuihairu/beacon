using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 面组件（Phase 2 基础件 Surface）：圆角取 DesignTokens，底色/内边距由调用面给。
/// 供 Phase 2 后续（Metric/StatusCard 等组合件）与设置卡片逐面收编；当前无调用面。
/// </summary>
internal static class ComponentSurface
{
    /// <summary>卡片面：RadiusCard + 均匀内边距（默认 Space10，设置卡片口径）。</summary>
    public static Border Card(UIElement child, Brush background, double padding = DesignTokens.Space10) => new()
    {
        Child = child,
        CornerRadius = DesignTokens.RadiusCard,
        Background = background,
        Padding = new Thickness(padding),
    };
}
