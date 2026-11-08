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
    /// <summary>未读徽标喂数（App 订阅 NotificationRaised 转发 runtime.Notifications.UnreadCount）：
    /// 岛收起态徽标=未读优先、异常次之，都为零隐藏。经典形态只存值不渲染。</summary>
    public void UpdateBadge(int unread)
    {
        _unreadCount = Math.Max(0, unread);
        RefreshBadge();
    }

    /// <summary>岛收起态重画（ISLAND-D1..D3，2026-11-09 用户令）：App 已推展示计划时计划驱动——
    /// 展示方式/上岛白名单/真实数据面全部 Core 决策，本窗按计划渲染；计划未喂数（启动段首帧/经典
    /// 形态）回退既有环/条双面逻辑兜底（App 首推即覆盖，双路径视觉同形）。环/条口径不变：
    /// 环=底环+进度弧+中心数字（数字=未读优先、异常次之，全零空中心）；条=轨道+左锚填充。
    /// 经典形态恒隐藏。每次重画整树重建（同 StatusRing 口径）。</summary>
    private void RefreshBadge()
    {
        if (!_island)
        {
            IslandProgress.Visibility = Visibility.Collapsed;
            IslandProgress.Content = null;
            return;
        }
        var content = _islandPlan is not null ? BuildIslandContent() : LegacyBadgeContent();
        IslandProgress.Content = content;
        IslandProgress.Visibility = content is null ? Visibility.Collapsed : Visibility.Visible;
        ApplySize(); // 勾选/计划/计数变化改变收起行宽（238 下限内通常不动，低于下限的窄岛需要重排）
    }

    /// <summary>未喂数兜底：环/条二选勾（ISLAND-RING-1 原逻辑，_islandPlan 推送前的首帧口径）。</summary>
    private FrameworkElement? LegacyBadgeContent()
    {
        var ring = Beacon.Core.Services.ComponentCenterCatalog.IslandRingVisible(_config.App);
        var bar = Beacon.Core.Services.ComponentCenterCatalog.IslandBarVisible(_config.App);
        var arcColor = _ringColor;
        var trackColor = ThemeColors.Halo(arcColor, 80); // 底环=进度色低透明（StatusRing 35% 同族，暗底更含蓄）
        var countText = _unreadCount > 0 ? _unreadCount.ToString()
            : _abnormalCount > 0 ? _abnormalCount.ToString()
            : string.Empty; // 全零：中心不落字，环/条自己讲进度
        return (ring, bar) switch
        {
            (true, true) => BothProgressFaces(_ringRatio, arcColor, trackColor, countText),
            (true, false) => Components.RingProgressBadge.Build(IslandRingSize, IslandRingThickness, _ringRatio,
                arcColor, trackColor, countText, ThemeColors.IslandText()),
            (false, true) => BarOnlyFace(_ringRatio, arcColor, trackColor, countText),
            (false, false) => null, // 都不勾：岛收起态只剩品牌标（组件中心说了算，无兜底无默认）
        };
    }

    /// <summary>环+条都勾：横向并置（布局由岛的空间管理决定——PillRow 居中，宽超 238 下限时岛自动加宽）。</summary>
    private static FrameworkElement BothProgressFaces(double fraction, global::Windows.UI.Color arcColor, global::Windows.UI.Color trackColor, string countText)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Components.RingProgressBadge.Build(IslandRingSize, IslandRingThickness, fraction,
            arcColor, trackColor, countText, ThemeColors.IslandText()));
        row.Children.Add(Components.BarProgressBadge.Build(IslandBarWidth, IslandBarThickness, fraction, arcColor, trackColor));
        return row;
    }

    /// <summary>仅条在场：条 + 计数文本（环不在场，数字跟条走）。</summary>
    private static FrameworkElement BarOnlyFace(double fraction, global::Windows.UI.Color arcColor, global::Windows.UI.Color trackColor, string countText)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Components.BarProgressBadge.Build(IslandBarWidth, IslandBarThickness, fraction, arcColor, trackColor));
        if (countText.Length > 0)
        {
            row.Children.Add(new TextBlock
            {
                Text = countText,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ThemeColors.IslandText()),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return row;
    }

    /// <summary>岛进度组件勾选变化即时生效（设置页组件中心 → PinsChanged → App 转发）：按最新勾选重建进度面。</summary>
    public void RefreshComponents() => RefreshBadge();

    /// <summary>接入真实聚合（B-504）：极简计数（零值组不显示，§6.2.2）+ 健康环/光晕灯（2026-10-29 UI 升级）
    /// + 离线横幅（§6.2.6）。计数文案格式是 Q 场景 UIA 断言锚（「2 ok」「1 ok · 1 err」），不动。</summary>
    public void UpdateStatus(AggregateStatusChanged status)
    {
        var ok = status.Counts.GetValueOrDefault(Severity.Success) + status.Counts.GetValueOrDefault(Severity.Info);
        var warn = status.Counts.GetValueOrDefault(Severity.Warning);
        var error = status.Counts.GetValueOrDefault(Severity.Error) + status.Counts.GetValueOrDefault(Severity.Critical);
        var parts = new List<string>();
        if (ok > 0)
        {
            parts.Add($"{ok} ok");
        }
        if (warn > 0)
        {
            parts.Add($"{warn} warn");
        }
        if (error > 0)
        {
            parts.Add($"{error} err");
        }
        CountText.Text = parts.Count == 0 ? "Beacon" : string.Join(" · ", parts);

        // 灯/晕/环三面同色（UiPalette 实时读 config——G4/G5 改色即变色链不动）；换色走 MotionEngine
        // 过渡（off 档瞬时直设），级别抬升再叠一次 Flash（full 档）
        var lightColor = _palette.SeverityColor(status.Overall);
        _motion.TransitionFill(OverallLight, lightColor);
        OverallHalo.Fill = new SolidColorBrush(ThemeColors.Halo(lightColor, 64));
        var total = ok + warn + error;
        _ringRatio = total == 0 ? 1.0 : (double)ok / total; // 缓存恒更新：岛期间状态变化，切回经典环即最新份额
        _ringColor = lightColor;
        if (!_island)
        {
            // 经典形态健康环照旧（岛收起态由进度环/条组件扛状态，ISLAND-RING-1）
            HealthRing.Content = StatusRing.Build(16, 2.5, _ringRatio, lightColor);
        }
        if (status.Overall > _lastOverall)
        {
            _motion.Flash(OverallLight);
        }
        _lastOverall = status.Overall;
        _abnormalCount = warn + error; // 岛徽标异常计数（未读优先级更高，RefreshBadge 裁决）
        RefreshBadge(); // 岛进度面重建（环色随级别、份额随 ok 占比、计数=未读/异常）
        IslandTrace($"status ok={ok} warn={warn} err={error} ratio={_ringRatio:F2} unread={_unreadCount} " +
                    $"ring=#{_ringColor.R:X2}{_ringColor.G:X2}{_ringColor.B:X2} faces={IslandProgress.Visibility}/{IslandProgress.ActualWidth}");

        var offline = status.OfflineConnections > 0;
        OfflineBanner.Visibility = offline ? Visibility.Visible : Visibility.Collapsed;
        if (offline)
        {
            var since = _lastFetchedAt == default ? status.Timestamp : _lastFetchedAt;
            OfflineText.Text = WidgetFeedback.LastUpdate(since);
        }
        // 横幅/文案变化会改变内容尺寸（_appWindow 未就绪则 OnLoaded 的 ApplySize 兜底）
        ApplySize();
    }

    /// <summary>悬停预览喂数（App.xaml.cs 按 WidgetStateChanged 聚合后推）：仅悬停中即时重画，
    /// 非悬停只存行集——下次展开用最新（悬停展开时读的就是已存最新行）。</summary>
    public void UpdateSourcePreview(IReadOnlyList<SourcePreviewRow> rows)
    {
        _previewRows = rows;
        if (_hovered && !_dragging && _previewRows.Count > 0)
        {
            RebuildPreview();
            ApplySize(); // 行数变化窗高随之（贴锚点）
        }

        if (IslandPreviewProbe && !_previewProbeShown && _island && _previewRows.Count > 0 && !_dragging)
        {
            _previewProbeShown = true;
            ShowPreview(); // 取证钩子：锁屏会话注入不了指针，展开态只能程序触发（见字段注释）
        }
    }
}
