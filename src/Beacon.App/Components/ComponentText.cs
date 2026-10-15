using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 文本组件（Phase 2 基础件 Text，redesign-plan §6）：字号取 DesignTokens 刻度，
/// 前景与截断由调用面定制（主题分支不上收——组件管排印，不管配色）。
/// 已采用：QuickPanel 状态行/事件行（MakeStateRow）。
/// </summary>
internal static class ComponentText
{
    /// <summary>次要文案（时间戳/子标签）：FontCaption，垂直居中。</summary>
    public static TextBlock Caption(string text, Brush foreground) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontCaption,
        Foreground = foreground,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>正文（标签/值位）：FontBody + 字符省略截断；maxWidth 限宽（null = 不限）。</summary>
    public static TextBlock Body(string text, Brush foreground, double? maxWidth = null) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontBody,
        Foreground = foreground,
        TextTrimming = TextTrimming.CharacterEllipsis,
        MaxWidth = maxWidth ?? double.NaN,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
