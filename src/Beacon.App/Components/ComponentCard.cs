using Beacon.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 状态卡组件（Phase 2 基础件 Status Card）：单个任务或系统的状态摘要——
/// 卡面（Surface.Card）+ 竖排头行/摘要行。目录词汇件，待 Phase 3 绑定与视觉示例页收编（当前无调用面）。
/// </summary>
internal static class ComponentCard
{
    /// <summary>状态卡：header（指示灯 + 标题行）必给，body（摘要行）可空；底色由调用面给。</summary>
    public static Border Status(UIElement header, UIElement? body, Brush background, double padding = DesignTokens.Space10)
    {
        var stack = new StackPanel { Spacing = DesignTokens.Space6 };
        stack.Children.Add(header);
        if (body is not null)
        {
            stack.Children.Add(body);
        }
        return ComponentSurface.Card(stack, background, padding);
    }
}
