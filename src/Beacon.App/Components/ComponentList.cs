using Microsoft.UI.Xaml.Controls;

namespace Beacon.App.Components;

/// <summary>
/// 列表组件（Phase 2 基础件 List）：紧凑竖排容器（行距 3，详情表口径）。
/// 已采用：DetailWindow 详情表容器；目录词汇表见 redesign-plan §5.2。
/// </summary>
internal static class ComponentList
{
    /// <summary>竖排列表容器：调用面按行填充 Children（行内元素工厂各归其位）。</summary>
    public static StackPanel Vertical(double spacing = 3) => new()
    {
        Spacing = spacing,
    };
}
