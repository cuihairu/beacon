using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Beacon.App.Components;

/// <summary>
/// 动作组件（Phase 2 交互件）：Action Button、Action Menu、Link。
/// 已采用：Action Button 收编 DetailWindow 动作区（内边距/触发链逐项等价）；
/// Action Menu 收编 QuickPanel 行右键 pin 菜单；Link 词汇件待示例页收编。
/// </summary>
internal static class ComponentAction
{
    /// <summary>动作按钮：触发走 fire-and-forget Task（详情窗动作区口径）。</summary>
    public static Button Button(string label, Func<Task> run)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(DesignTokens.Space12, DesignTokens.Space6, DesignTokens.Space12, DesignTokens.Space6),
        };
        button.Click += (_, _) => _ = run();
        return button;
    }

    /// <summary>菜单项：Click 直接同步回调（快捷面板行右键口径）。</summary>
    public static MenuFlyoutItem Item(string text, Action click)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => click();
        return item;
    }

    /// <summary>动作菜单：按给定菜单项组装（空项序列得空菜单，调用面自行决定挂载）。</summary>
    public static MenuFlyout Menu(params MenuFlyoutItem[] items)
    {
        var flyout = new MenuFlyout();
        foreach (var item in items)
        {
            flyout.Items.Add(item);
        }
        return flyout;
    }

    /// <summary>链接：标准 HyperlinkButton（目录词汇件，Phase 3 绑定/示例页收编）。</summary>
    public static HyperlinkButton Link(string text, Uri navigateUri) => new()
    {
        Content = text,
        NavigateUri = navigateUri,
    };
}
