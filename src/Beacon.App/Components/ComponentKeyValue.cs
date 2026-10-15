using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 键值对组件（Phase 2 基础件 Key-Value）：标签与值的紧凑展示（等宽字体单行）。
/// 已采用：DetailWindow 详情表（payload 逐行，值逐项等价）；目录词汇表见 redesign-plan §5.2。
/// </summary>
internal static class ComponentKeyValue
{
    /// <summary>键值行：等宽 Consolas + 单行截断（超宽走省略号，详情表口径）。</summary>
    public static TextBlock Row(string key, string value, Brush foreground) => new()
    {
        Text = $"{key}  {value}",
        FontSize = DesignTokens.FontBody,
        FontFamily = new FontFamily("Consolas"),
        Foreground = foreground,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
}
