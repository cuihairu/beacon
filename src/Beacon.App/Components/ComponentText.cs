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
    public static TextBlock Body(string text, Brush foreground, double? maxWidth = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = DesignTokens.FontBody,
            Foreground = foreground,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // WinUI 的 MaxWidth 不收 NaN（WPF 口径不可搬，NaN/负值 E_INVALIDARG——38976398309 场景 U 实证：
        // 详情窗表格行恒 NaN 即恒炸构造）；不限宽=不设值，Star 列截断由 TextTrimming 兜底。
        if (maxWidth is { } limit)
        {
            block.MaxWidth = limit;
        }
        return block;
    }
}
