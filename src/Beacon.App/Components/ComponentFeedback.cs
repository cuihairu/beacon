using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 通用状态组件（Phase 2）：Empty、Loading、Error、Offline 四态占位/提示。
/// 当前仓内 UX 口径=空区隐藏（QuickPanel 分区）+ 胶囊离线横幅（CapsuleWindow.xaml），故为目录词汇件
/// （redesign-plan §5.2-B），供视觉示例页（Phase 2 末行）与 Phase 3 绑定收编。
/// </summary>
internal static class ComponentFeedback
{
    /// <summary>空态：居中说明（无数据/无事件/尚未配置）。</summary>
    public static TextBlock Empty(string text, Brush foreground) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontBody,
        Foreground = foreground,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>加载中：说明文字（tile 首态「加载中…」口径）。</summary>
    public static TextBlock Loading(string text, Brush foreground) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontBody,
        Foreground = foreground,
    };

    /// <summary>错误摘要：换行正文（失败原因/错误摘要口径）。</summary>
    public static TextBlock Error(string message, Brush foreground) => new()
    {
        Text = message,
        FontSize = DesignTokens.FontBody,
        Foreground = foreground,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>离线态：空心灯 + 说明行（胶囊离线横幅口径，§4.1 Offline）。</summary>
    public static StackPanel Offline(string text, Brush stroke, Brush captionForeground)
    {
        var row = ComponentLayout.Row(6);
        row.Children.Add(ComponentStatus.OfflineHollow(stroke));
        row.Children.Add(ComponentText.Caption(text, captionForeground));
        return row;
    }
}
