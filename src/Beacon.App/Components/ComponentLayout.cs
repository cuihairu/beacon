using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 布局组件（Phase 2 布局件）：Stack（Row/Column）、Grid 列、Section 标签、Conditional 可见性。
/// 已采用：Row 收编 QuickPanel 三横行与 DetailWindow 头行/动作行、Columns 收编 PinnedHost 主行六列、
/// SectionLabel 收编 DetailWindow 区段标签、When 收编 QuickPanel 空区收起。
/// </summary>
internal static class ComponentLayout
{
    /// <summary>横排容器（行）：间距由调用面给（QuickPanel/Detail 行口径）。</summary>
    public static StackPanel Row(double spacing) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = spacing,
    };

    /// <summary>竖排容器（列）：List 之外的通用竖排（行距 3 的紧凑列表走 ComponentList.Vertical）。</summary>
    public static StackPanel Column(double spacing) => new()
    {
        Orientation = Orientation.Vertical,
        Spacing = spacing,
    };

    /// <summary>列网格：按给定列宽建列（PinnedHost 主行口径，GridLength.Auto/Star 直传）。</summary>
    public static Grid Columns(double? columnSpacing = null, params GridLength[] widths)
    {
        var grid = new Grid();
        if (columnSpacing is { } spacing)
        {
            grid.ColumnSpacing = spacing;
        }
        foreach (var width in widths)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        }
        return grid;
    }

    /// <summary>区段标签：小号标题 + 顶距（DetailWindow DETAILS/ACTIONS 口径，色由调用面给）。</summary>
    public static TextBlock SectionLabel(string text, Brush foreground) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontCaption,
        Foreground = foreground,
        Margin = new Thickness(0, DesignTokens.Space6, 0, 0),
    };

    /// <summary>条件可见：真可见 / 假收起（空区隐藏口径）。</summary>
    public static Visibility When(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
