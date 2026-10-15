using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 图标组件（Phase 2 基础件 Icon）：状态点为第一形态（8×8 级别灯，色由 UiPalette 解析后传入）。
/// 已采用：QuickPanel 状态行/事件行（MakeStateRow）。
/// </summary>
internal static class ComponentIcon
{
    /// <summary>状态点：size 默认 8（现值），垂直居中。</summary>
    public static Ellipse StatusDot(Brush fill, double size = 8) => new()
    {
        Width = size,
        Height = size,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = fill,
    };
}
