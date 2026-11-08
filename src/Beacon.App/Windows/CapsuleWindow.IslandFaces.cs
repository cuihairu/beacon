using System.Runtime.InteropServices;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;


public sealed partial class CapsuleWindow
{
    /// <summary>岛收起态展示计划推送（ISLAND-D1..D3，2026-11-09 用户令）：App 按
    /// IslandDisplayPlan.Compute 产出（展示方式/上岛白名单/真实状态全部 Core 决策），本窗只做渲染。
    /// 同一 UI 线程派发口径（App 侧 Dispatcher.EnqueueGuarded），存值即按最新计划重画。</summary>
    public void UpdateIslandPlan(IslandPlan plan)
    {
        _islandPlan = plan;
        if (_island)
        {
            RefreshBadge();
        }
    }

    /// <summary>岛收起态内容重建（ISLAND-D1..D3）：计划驱动（App 推送后唯一渲染路径）——
    /// 面上顺序=计划面序（虚拟环/条在前、上岛白名单续后），宽度自适应仍由 ApplySize 实测收口；
    /// 轮播徽标（未读/异常计数）前置；空计划=岛只剩品牌标。计划未喂数时回退既有环/条双面逻辑。</summary>
    private FrameworkElement? BuildIslandContent()
    {
        if (_islandPlan is not { } plan)
        {
            return null; // RefreshBadge 回退旧逻辑（启动段首帧兜底，App 首推即覆盖）
        }
        if (plan.Faces.Count == 0 && !plan.BadgeVisible)
        {
            return null; // 空计划：组件中心说了算，无兜底无默认（ISLAND-RING-1 口径）
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (plan.BadgeVisible)
        {
            // 轮播徽标：未读优先、异常次之（计划已裁决），计数文本与现环心数字同口径
            row.Children.Add(new TextBlock
            {
                Text = plan.BadgeCount.ToString(),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ThemeColors.IslandText()),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        foreach (var face in plan.Faces)
        {
            row.Children.Add(FaceHost(face, IslandFaceView(face)));
        }
        return row;
    }

    /// <summary>单面视图：环/条/灯点chip 三形（计划裁决，本方法只按形画）。环/条尺寸沿用
    /// ISLAND-RING-1 口径（环 Ø22/弦2.5、条 56×5），chip=级别灯点+组件标签（摘要走 hover 预览）。</summary>
    private FrameworkElement IslandFaceView(IslandFace face)
    {
        var color = _palette.SeverityColor(face.Severity);
        var track = ThemeColors.Halo(color, 80);
        return face.Kind switch
        {
            "ring" => Components.RingProgressBadge.Build(IslandRingSize, IslandRingThickness,
                face.Progress ?? 0, color, track, face.Text, ThemeColors.IslandText()),
            "bar" => Components.BarProgressBadge.Build(IslandBarWidth, IslandBarThickness,
                face.Progress ?? 0, color, track),
            _ => ChipFace(face, color),
        };
    }

    /// <summary>灯点 chip 面：级别灯点（占位=空心描边+文字降透明度，不渲染真实数值）+ 组件标签。
    /// 摘要进 tooltip（真实数据可见，占位面不造摘要）。</summary>
    private FrameworkElement ChipFace(IslandFace face, global::Windows.UI.Color color)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(face.Placeholder
            ? new Ellipse { Width = 8, Height = 8, Stroke = new SolidColorBrush(color), StrokeThickness = 1.5 }
            : new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(color) });
        row.Children.Add(new TextBlock
        {
            Text = face.Text,
            FontSize = 11,
            Foreground = face.Placeholder
                ? new SolidColorBrush(ThemeColors.IslandText()) { Opacity = 0.55 }
                : new SolidColorBrush(ThemeColors.IslandText()),
            MaxWidth = 96, // 收起行上限内的标签截断（超长省略，不撑宽岛）
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (!string.IsNullOrEmpty(face.Summary))
        {
            ToolTipService.SetToolTip(row, face.Summary);
        }
        return row;
    }

    /// <summary>面宿主（点击路由锚）：Tag 挂 WidgetId（虚拟环/条 id 或真实 id），释放时命中测试
    /// 按 Tag 路由——整岛单击开板口径不变，真实组件面单击走 IslandWidgetActivated（ISLAND-D4）。
    /// UIA：AutomationId 只挂在 Grid 上不进 UIA 树（记忆坑），补 SetName 造 peer 供取证锚定。</summary>
    private static FrameworkElement FaceHost(IslandFace face, FrameworkElement view)
    {
        var host = new Grid
        {
            Tag = face.WidgetId,
            VerticalAlignment = VerticalAlignment.Center,
        };
        host.Children.Add(view);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(host, $"island-face-{face.WidgetId}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(host,
            face.Text.Length > 0 ? $"{face.WidgetId} {face.Text}" : face.WidgetId);
        return host;
    }

    /// <summary>释放点命中的岛面 WidgetId（Drag.OnPointerReleased 单击分支调用）：命中面宿主
    /// （Tag 为字符串）返回其 id，未命中/未就绪返回 null。虚拟环/条 id 由调用方归口开板。</summary>
    private string? FaceIdAt(PointerRoutedEventArgs e)
    {
        if (!_island || IslandProgress.Visibility != Visibility.Visible || IslandProgress.Content is null)
        {
            return null;
        }
        var point = e.GetCurrentPoint(Root).Position;
        foreach (var element in VisualTreeHelper.FindElementsInHostCoordinates(point, Root))
        {
            if (element is FrameworkElement { Tag: string id } && id.Length > 0)
            {
                return id;
            }
        }
        return null;
    }

    /// <summary>释放单击路由（ISLAND-D4）：命中真实组件面 → IslandWidgetActivated（App 做看板定位/
    /// 处置直达）；命中虚拟环/条面或未命中 → 整岛开板（OpenPanelRequested 口径不变）。</summary>
    private void RouteIslandClick(PointerRoutedEventArgs e)
    {
        var faceId = FaceIdAt(e);
        if (faceId is not null
            && faceId != Beacon.Core.Services.ComponentCenterCatalog.IslandRingId
            && faceId != Beacon.Core.Services.ComponentCenterCatalog.IslandBarId)
        {
            IslandWidgetActivated?.Invoke(faceId);
            return;
        }
        OpenPanelRequested?.Invoke();
    }
}
