using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 表格组件（Phase 5「时间线、表格、图表与趋势组件」，三诊口径「表格走 payload 多行」）：
/// 两列网格（Columns 词汇件，标签 Auto + 值 Star，同 PinnedHost 主行口径）逐行铺开——
/// 与 ComponentKeyValue 单行等宽键值互补：KeyValue 用于单行紧凑对，本件用于多行对照表。
/// 表体空（无行）返回空竖排容器，空态说明由调用面 ComponentFeedback.Empty 承担。
/// </summary>
internal static class ComponentTable
{
    private const double GridColumnSpacing = 6;

    /// <summary>表头行：标签列 + 值列，Caption 灰。</summary>
    public static Grid Header(string label, string value, Brush caption)
    {
        var grid = ComponentLayout.Columns(GridColumnSpacing, GridLength.Auto, new GridLength(1, GridUnitType.Star));
        grid.Children.Add(ComponentText.Caption(label, caption));
        grid.Children.Add(ComponentText.Caption(value, caption));
        return grid;
    }

    /// <summary>表体：逐行网格（键 Caption 灰 + 值 Body）；无行返回空竖排容器。</summary>
    public static StackPanel Body(IReadOnlyList<(string Key, string Value)> rows, Brush label, Brush value)
    {
        var column = ComponentLayout.Column(2);
        foreach (var (key, text) in rows)
        {
            var grid = ComponentLayout.Columns(GridColumnSpacing, GridLength.Auto, new GridLength(1, GridUnitType.Star));
            grid.Children.Add(ComponentText.Caption(key, label));
            grid.Children.Add(ComponentText.Body(text, value));
            column.Children.Add(grid);
        }
        return column;
    }
}
