using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

public sealed partial class QuickPanelWindow
{
    private void OnStateChanged(WidgetState state, bool record)
    {
        _states[state.WidgetId] = state;
        if (record)
        {
            _events.Insert(0, new RecentEvent(DateTimeOffset.Now, state));
            if (_events.Count > MaxRecentEvents)
            {
                _events.RemoveAt(_events.Count - 1);
            }
        }
        QueueSectionRebuild();
    }

    /// <summary>段重建合并节流（审核修复 2026-11-12 P1）：正常轮询多组件状态事件同拍到达，
    /// 此前每条事件都全量重建 Header/Overview/Status/Footer/Recent 五段（ItemsSource 整表替换），
    /// 大面板每拍 N×5 次重排。改为数据即时入表（_states/_events 始终同步）、UI 重建按调度拍合并——
    /// 重建已排队时后续事件只更新数据，同拍只重排一次。过滤/失效等交互路径仍走即时重建。</summary>
    private void QueueSectionRebuild()
    {
        if (_sectionRebuildQueued)
        {
            return;
        }
        _sectionRebuildQueued = true;
        _dispatcher.Post(() =>
        {
            _sectionRebuildQueued = false;
            UpdateHeader();
            UpdateOverview();
            RebuildStatus(); // 「现在」列随当前状态现场重建（现场重建口径保留——场景 S 实证）
            UpdateFooter(); // 脚条「运行中/正常」计数随状态现场刷新
            RebuildRecent();
        });
    }

    /// <summary>组件失效（模块停用/删除）：丢弃残留状态与事件，不留旧灯。</summary>
    private void OnWidgetsInvalidated(IReadOnlyList<string> widgetIds)
    {
        var removed = false;
        foreach (var widgetId in widgetIds)
        {
            removed |= _states.Remove(widgetId);
        }
        var ids = widgetIds.ToHashSet(StringComparer.Ordinal);
        removed |= _events.RemoveAll(e => ids.Contains(e.State.WidgetId)) > 0;
        if (!removed)
        {
            return;
        }
        UpdateHeader();
        UpdateOverview();
        RebuildStatus();
        UpdateFooter();
        RebuildRecent();
    }

    private void UpdateHeader()
    {
        var warn = _states.Values.Count(s => s.Severity == Severity.Warning);
        var error = _states.Values.Count(s => s.Severity >= Severity.Error);
        OverallSummary.Text = warn == 0 && error == 0
            ? UiStrings.BoardAllClear
            : string.Format(UiStrings.BoardWarnError, warn, error);
        var overall = _states.Values.Count == 0
            ? Severity.Success
            : _states.Values.Max(s => s.Severity);
        OverallLight.Fill = new SolidColorBrush(_palette.SeverityColor(overall));
    }

    /// <summary>OVERVIEW 区（用户单①；2026-10-29 UI 升级：chip 横排 → 卡片栅格）：
    /// 默认「全部」卡在首 + 来源卡可点击过滤；当前看哪一类必须界面可见——激活卡强调色填充
    /// + OVERVIEW 头带「· 来源」后缀双保险。</summary>
    private void UpdateOverview()
    {
        OverviewHeader.Text = _activeSource is null
            ? UiStrings.BoardOverview
            : $"{UiStrings.BoardOverview} · {_activeSource}";
        OverviewCount.Text = _states.Count.ToString(); // 方案 A：概览头旁常驻组件计数（「9 个组件」原型口径，数字保持语言中立）
        var states = _states.Values.ToList();
        var cards = new List<Border>
        {
            MakeSourceCard(
                UiStrings.BoardAll, states.Count,
                states.Count == 0 ? Severity.Success : states.Max(s => s.Severity),
                HealthFraction(states),
                sourceKey: null, active: _activeSource is null),
        };
        foreach (var group in states
            .GroupBy(state => QuickPanelFilter.SourceKey(state.WidgetType))
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var groupStates = group.ToList();
            // 组内首个可解析连接的类型 → 品牌图标（无映射 TryCreate 返 null，卡仍以环为视觉锚）
            var connectionType = groupStates
                .Select(s => _runtime.Config.FindWidget(s.WidgetId))
                .Where(w => w is not null)
                .Select(w => _runtime.Config.Connections.FirstOrDefault(c => c.Id == w!.ConnectionId)?.Type)
                .FirstOrDefault(t => t is not null);
            cards.Add(MakeSourceCard(
                group.Key, groupStates.Count,
                groupStates.Max(s => s.Severity),
                HealthFraction(groupStates),
                connectionType,
                sourceKey: group.Key, active: _activeSource == group.Key));
        }
        SourceCounts.ItemsSource = cards; // ItemsRepeater 无 Children 集合，走 ItemsSource 整表替换
    }

    /// <summary>健康份额（卡/环共用口径）：非关注态（&lt; Warning）占比，空集=1（全清）。</summary>
    private static double HealthFraction(IReadOnlyCollection<WidgetState> states)
        => states.Count == 0 ? 1.0 : (double)states.Count(s => s.Severity < Severity.Warning) / states.Count;

    /// <summary>来源过滤态（用户单①）——过滤既改视角也要有显式出口：时间线头带「· 来源」后缀，
    /// 空匹配给「无匹配，试试清除过滤」+ 清除入口（方案 A⑤）。</summary>
    private string TimelineHeaderText()
        => _activeSource is null
            ? UiStrings.BoardTimeline
            : $"{UiStrings.BoardTimeline} · {_activeSource}";

    /// <summary>chip 点击过滤（用户单①）：点某类只看该类，再点同类取消回全部；「全部」chip 恒回全部。
    /// 告警条 chip 与概览卡共用此入口（方案 A①：点击告警 chip 过滤到对应项）。</summary>
    private void ApplySourceFilter(string? sourceKey)
    {
        _activeSource = _activeSource == sourceKey && sourceKey is not null ? null : sourceKey;
        UpdateOverview(); // 头后缀 + chip 高亮态（可见过滤状态）
        RebuildStatus();
        UpdateFooter();
        UpdateTimelineEmptyState();
        RebuildRecent();
        RebuildNotifications();
    }

    /// <summary>「清除筛选」入口（方案 A⑤ 空态）：回全部视角，与再点同类取消同链。</summary>
    private void OnClearFilter(object sender, RoutedEventArgs e) => ApplySourceFilter(null);

    /// <summary>来源卡（2026-10-29 UI 升级，BrimDeck 对照）：品牌图标 + 迷你健康环 + 计数 + 来源名，整卡点击过滤。
    /// 激活卡=强调色填充白字；非激活=轻卡面（ThemeColors.SurfaceCard）+ 弱描边。色值全部走 ThemeColors 语义板。</summary>
    private Border MakeSourceCard(string label, int count, Severity worst, double healthFraction, string? connectionType = null, string? sourceKey = null, bool active = false)
    {
        var white = new SolidColorBrush(SeverityPalette.Rgb(255, 255, 255, 255));
        var topRow = ComponentLayout.Row(DesignTokens.Spacing(6, _runtime.Config.App.Appearance.Density));
        topRow.Children.Add(StatusRing.Build(14, 2, healthFraction, _palette.SeverityColor(worst)));
        topRow.Children.Add(new TextBlock
        {
            Text = $"{count}",
            FontSize = DesignTokens.FontBody,
            FontWeight = FontWeights.SemiBold,
            Foreground = active ? white : new SolidColorBrush(ThemeColors.TextPrimary()),
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (BrandIconFactory.TryCreate(connectionType, 14,
                active ? SeverityPalette.Rgb(255, 255, 255, 255) : ThemeColors.TextSecondary()) is { } brandIcon)
        {
            brandIcon.VerticalAlignment = VerticalAlignment.Center;
            topRow.Children.Add(brandIcon);
        }
        var cardBody = new StackPanel { Spacing = 2 };
        cardBody.Children.Add(topRow);
        // BUG-2（2026-11-05 用户截图：窄窗三列下来源名被截没）：标签换行优先、不用省略号——
        // 卡宽由 UniformGridLayout MinItemWidth 兜底，窄窗挤宽时折行保全名；整卡 Tooltip 挂全名兜底
        cardBody.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = DesignTokens.FontBody,
            Foreground = active ? white : new SolidColorBrush(ThemeColors.TextSecondary()),
            TextWrapping = TextWrapping.Wrap,
        });
        var accent = ThemeColors.AccentFill();
        var card = new Border
        {
            Background = new SolidColorBrush(active ? accent : ThemeColors.SurfaceCard()),
            BorderBrush = new SolidColorBrush(active ? accent : ThemeColors.TextSecondary()) { Opacity = 0.35 },
            BorderThickness = new Thickness(1),
            CornerRadius = DesignTokens.RadiusInner,
            Padding = new Thickness(8, 6, 8, 6),
            MinWidth = 96, // chip 最小宽：极短名不塌卡形，长名走上面的换行
            Child = cardBody,
        };
        Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(card, label); // 全名兜底：换行后仍极窄时悬停可见完整名
        // UIA 取证/CI 点击入口（口径沿 chip：Border 非 Control 无自带 peer，只设 AutomationId 不进 UIA 树——
        // CI 38533804493/38542642112 实证，Name 强制创建 FrameworkElementAutomationPeer；
        // Id/Name 格式不变=改版不破 W3 场景锚 chip-智谱 / 「label count」）。
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card, $"{label} {count}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(card, $"chip-{sourceKey ?? "all"}");
        card.Tapped += (_, _) => ApplySourceFilter(sourceKey); // sourceKey=null 的「全部」卡恒回全部
        return card;
    }

    /// <summary>
    /// 事件动态主区（PANEL-UPGRADE-1）：内存事件壳（新在前）翻 Core BoardTimelineEntry——
    /// 来源过滤（QuickPanelFilter 同口径）、时刻降序、BoardMaxEntries 条滚动承载（8 条上限已放宽）。
    /// 行面 rail 时间轴（方案 A②：竖线+节点点，MakeTimelineRow 行内 rail 格拼装）+ 时刻（跨日带日期，
    /// Core TimelineEntries 口径）/来源 chip/摘要。空列表给空态文案（方案 A⑤）。
    /// </summary>
    private void RebuildRecent()
    {
        var events = _events.Select(evt =>
        {
            var widget = _runtime.Config.FindWidget(evt.State.WidgetId);
            var source = widget is null ? "·" : QuickPanelFilter.SourceKey(widget.Type); // 组件已删：来源位占位不挡行
            return new BoardEvent(evt.At, evt.State, source);
        }).ToList();
        var entries = TimelineEntries.Board(events, _activeSource);
        RecentList.Items.Clear();
        for (var index = 0; index < entries.Count; index++)
        {
            RecentList.Items.Add(MakeTimelineRow(entries[index], isFirst: index == 0, isLast: index == entries.Count - 1));
        }
        UpdateTimelineEmptyState();
        RecentHeader.Text = TimelineHeaderText(); // 过滤后缀随当前态
    }

    /// <summary>时间线空态（方案 A⑤）：无事件「暂无事件」；过滤无匹配「无匹配，试试清除过滤」+ 清除入口。</summary>
    private void UpdateTimelineEmptyState()
    {
        var empty = RecentList.Items.Count == 0;
        var filtered = _activeSource is not null;
        TimelineEmpty.Visibility = ComponentLayout.When(empty);
        RecentList.Visibility = ComponentLayout.When(!empty);
        TimelineEmptyText.Text = filtered ? UiStrings.BoardFilterEmpty : UiStrings.BoardTimelineEmpty;
        ClearFilterButton.Visibility = ComponentLayout.When(empty && filtered);
        TimelineFilterEmptyHint.Visibility = Visibility.Collapsed; // 文案并入标题行，按钮作出口
    }

    /// <summary>时间线行（看板主区，方案 A② rail 时间轴）：时刻（右对齐，跨日带日期）+ rail 竖线节点
     /// （首行线自节点起、末行线止于节点）+ 来源 chip + 摘要；摘要换行不吃横向无限 offer（BUG-2 同根因）。
     /// 行字号 13/12 大板面重排。单击进 L3，右键 Pin（B-702 口径）。</summary>
    private ListViewItem MakeTimelineRow(BoardTimelineEntry entry, bool isFirst = false, bool isLast = false)
    {
        var severityColor = _palette.SeverityColor(entry.Severity,
            widgetOverride: _runtime.Config.FindWidget(entry.WidgetId)?.ColorOverride);
        var time = new TextBlock
        {
            Text = entry.Time,
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            FontSize = BoardRowCaptionSize,
            MinWidth = 64,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
        };
        // rail 格（方案 A②）：1px 竖线贯穿行高，节点点 7px 挂线上——行与行线段相接成整条时间轴；
        // 首行线从节点高度起、末行线止于节点（原型 .tl-rail 首尾裁口口径）
        var rail = new Grid { Width = 16 };
        var line = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            Fill = new SolidColorBrush(ThemeColors.TextSecondary()) { Opacity = 0.22 },
        };
        if (isFirst)
        {
            line.VerticalAlignment = VerticalAlignment.Stretch;
            line.Margin = new Thickness(0, 11, 0, 0);
        }
        else if (isLast)
        {
            line.VerticalAlignment = VerticalAlignment.Top;
            line.Height = 11;
        }
        else
        {
            line.VerticalAlignment = VerticalAlignment.Stretch;
        }
        var halo = new Ellipse
        {
            Width = 13,
            Height = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 0, 0),
            Fill = new SolidColorBrush(ThemeColors.Halo(severityColor, 45)),
        };
        var dot = new Ellipse
        {
            Width = 7,
            Height = 7,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 5, 0, 0),
            Fill = new SolidColorBrush(severityColor),
        };
        rail.Children.Add(line);
        rail.Children.Add(halo);
        rail.Children.Add(dot);
        // 来源 chip（原型 .src）：小圆角胶囊，卡面底 + 弱描边
        var sourceChip = new Border
        {
            Background = new SolidColorBrush(ThemeColors.SurfaceCard()),
            BorderBrush = new SolidColorBrush(ThemeColors.TextSecondary()) { Opacity = 0.20 },
            BorderThickness = new Thickness(1),
            CornerRadius = DesignTokens.RadiusPill,
            Padding = new Thickness(6, 0, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = entry.Source,
                Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
                FontSize = DesignTokens.FontFootnote,
            },
        };
        var summary = new TextBlock
        {
            Text = entry.Summary,
            Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
            FontSize = BoardRowFontSize,
            TextWrapping = TextWrapping.Wrap, // BUG-2 口径：换行优先不省略
        };
        var main = new StackPanel { Spacing = 2 };
        main.Children.Add(sourceChip);
        main.Children.Add(summary);
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(time, 0);
        Grid.SetColumn(rail, 1);
        Grid.SetColumn(main, 2);
        row.Children.Add(time);
        row.Children.Add(rail);
        row.Children.Add(main);

        var item = new InvokeableListItem { Content = row, Padding = new Thickness(0, 0, 2, 0), Margin = new Thickness(0, 0, 0, 0), Tag = entry.State };
        item.InvokeRequested += () => OpenDetailFromRow(item); // UIA Invoke 与 ItemClick 同链（行 46 解锁面）
        var pinned = _runtime.Config.FindWidget(entry.WidgetId)?.Pinned == true;
        item.ContextFlyout = ComponentAction.Menu(ComponentAction.Item(
            pinned ? "Unpin from desktop" : "Pin to desktop", () => PinWidget(entry.WidgetId)));
        return item;
    }

    /// <summary>
    /// 通栏「现在」列（方案 B 拍板 2026-11-11，主从反转）：左 296px 固定列承载「现在哪里坏了」最大视觉权重——
    /// 聚合状态灯 + OverallSummary 打头，「需要关注」告警项列表为主体（QuickPanelSections 纯函数分组，
    /// 失败/超时/离线/过期，级别降序），概览来源卡收底部。行语义与事件流一致（单击进 L3、右键 Pin），
    /// AutomationId「AttentionList」保持不变（场景 S/U 的 CI 锚零迁移）。空列表整段收起。
    /// </summary>
    private void RebuildStatus()
    {
        var attention = new List<WidgetState>();
        foreach (var state in _states.Values.Where(s => QuickPanelFilter.Matches(_activeSource, s.WidgetType)))
        {
            if (QuickPanelSections.Resolve(state) == QuickPanelSection.NeedsAttention)
            {
                attention.Add(state);
            }
        }
        attention.Sort((a, b) => b.Severity.CompareTo(a.Severity)); // Critical 最先
        AttentionList.Items.Clear();
        foreach (var state in attention)
        {
            AttentionList.Items.Add(MakeStateRow(state, state.FetchedAt.ToLocalTime().ToString("HH:mm:ss")));
        }
        var visible = ComponentLayout.When(attention.Count > 0);
        AttentionList.Visibility = visible;
        AttentionEmpty.Visibility = ComponentLayout.When(attention.Count == 0);
        AttentionEmptyText.Text = UiStrings.BoardAllClear; // 「一切正常 · 没有需要关注的组件」（原型方案 B 空态口径）
        AttentionEmptyHint.Text = UiStrings.BoardAttentionEmpty;
        AttentionHeader.Text = UiStrings.BoardNeedsAttention;
    }

    /// <summary>
    /// 「需要关注」行（方案 B 左列主体，原型 .st-row 口径）：卡面底 + 圆角，级别点 + 时间 + 摘要；
    /// 声明式行（P3-01）优先，传统行对照回退。单击进 L3，右键 Pin（B-702 口径）。
    /// UIA Invoke 与 ItemClick 同链（InvokeableListItem，场景 S4/U2 依赖）。
    /// </summary>
    private ListViewItem MakeStateRow(WidgetState state, string timeText, object? tag = null)
    {
        var widget = _runtime.Config.FindWidget(state.WidgetId);
        UIElement content = widget is not null
            && WidgetInstanceResolver.Resolve(widget, "row") is { } rowInstance
            ? RenderDeclarativeRow(rowInstance, state)
            : BuildTraditionalStateRow(state, timeText);

        var item = new InvokeableListItem
        {
            Content = content,
            Padding = new Thickness(7, 6, 7, 6),
            Margin = new Thickness(0, 0, 0, 6),
            CornerRadius = DesignTokens.RadiusInner,
            Background = new SolidColorBrush(ThemeColors.SurfaceCard()),
            BorderBrush = new SolidColorBrush(ThemeColors.TextSecondary()) { Opacity = 0.14 },
            BorderThickness = new Thickness(1),
            Tag = tag ?? state,
        };
        item.InvokeRequested += () => OpenDetailFromRow(item); // UIA Invoke 与 ItemClick 同链（行 46 解锁面）
        var pinned = _runtime.Config.FindWidget(state.WidgetId)?.Pinned == true;
        item.ContextFlyout = ComponentAction.Menu(ComponentAction.Item(
            pinned ? "Unpin from desktop" : "Pin to desktop", () => PinWidget(state.WidgetId)));
        return item;
    }

    /// <summary>声明式行渲染（P3-03 附加源）：自身状态用行携带的快照（RECENT 行保留事件时点），
    /// 附加源按面板状态表取最新快照；缺席源由渲染器出缺失态。行面随面板 OnStateChanged 现场
    /// 全量重建（面板既有口径），无行级增量路由。</summary>
    private UIElement RenderDeclarativeRow(WidgetInstance rowInstance, WidgetState state)
        => ComponentSurfaceRenderer.Render(
            rowInstance,
            WidgetInstanceResolver.ResolveSources(rowInstance, id =>
                string.Equals(id, state.WidgetId, StringComparison.Ordinal)
                    ? state
                    : _states.TryGetValue(id, out var source) ? source : null),
            _palette);

    /// <summary>传统状态行内容（级别点 + 时间 + 摘要）——原 MakeStateRow 内联段，声明式行的对照回退面。
    /// 看板行字号口径（PANEL-UPGRADE-1）：正文 13、时刻 12，与时间线主区行一致。</summary>
    private UIElement BuildTraditionalStateRow(WidgetState state, string timeText)
    {
        var dot = ComponentIcon.StatusDot(new SolidColorBrush(_palette.SeverityColor(state.Severity,
            widgetOverride: _runtime.Config.FindWidget(state.WidgetId)?.ColorOverride)));
        var time = new TextBlock
        {
            Text = timeText,
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            FontSize = BoardRowCaptionSize,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text = state.Summary,
            Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
            FontSize = BoardRowFontSize,
            TextWrapping = TextWrapping.Wrap, // BUG-2（2026-11-05 用户截图）：换行优先不省略
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = ComponentLayout.Row(DesignTokens.Spacing(7, _runtime.Config.App.Appearance.Density));
        row.Children.Add(dot);
        row.Children.Add(time);
        row.Children.Add(label);
        return row;
    }
}
