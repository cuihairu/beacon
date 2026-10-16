using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 时间线组件（Phase 5「时间线、表格、图表与趋势组件」，三诊口径「时间线走事件历史」）：
/// 等级点 + 时刻（Caption 灰）+ 摘要正文的单行条目，新在前由调用面按 Core
/// TimelineEntries（引擎记录旧→新 → 展示序新在前）排好传入。已读/未读等样式差异由
/// 调用面选色，本件不做状态判定。
/// </summary>
internal static class ComponentTimeline
{
    /// <summary>时间线条目：等级点 + 时刻 + 摘要。</summary>
    public static UIElement Row(string time, string text, Brush dot, Brush timeForeground, Brush textForeground)
    {
        var row = ComponentLayout.Row(6);
        row.Children.Add(ComponentIcon.StatusDot(dot, 8));
        row.Children.Add(ComponentText.Caption(time, timeForeground));
        row.Children.Add(ComponentText.Body(text, textForeground));
        return row;
    }
}
