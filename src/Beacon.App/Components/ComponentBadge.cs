using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 徽标组件（Phase 2 基础件 Badge）：计数 pill（RadiusInner 圆角 + 水平窄内边距）。
/// 供通知未读数/告警计数等面收编（QuickPanel XAML 版 UnreadBadge 迁移到 C# 构造时对齐）；
/// 当前无调用面。
/// </summary>
internal static class ComponentBadge
{
    /// <summary>计数徽标：内边距水平 Space6、垂直 1（组件默认归入间距刻度，采用时以现值核对）。</summary>
    public static Border Count(TextBlock label, Brush background) => new()
    {
        Child = label,
        CornerRadius = DesignTokens.RadiusInner,
        Padding = new Thickness(DesignTokens.Space6, 1, DesignTokens.Space6, 1),
        VerticalAlignment = VerticalAlignment.Center,
        Background = background,
    };
}
