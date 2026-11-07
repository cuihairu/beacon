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
    /// <summary>状态脚条（方案 A/B 同款）：运行中（InProgress）与正常（Normal）计数，含色点；
    /// 文案在 ApplyStrings/UpdateFooter 双挂（语言切换与状态刷新各自触达）。</summary>
    private void UpdateFooter()
    {
        var running = 0;
        var ok = 0;
        foreach (var state in _states.Values)
        {
            switch (QuickPanelSections.Resolve(state))
            {
                case QuickPanelSection.InProgress:
                    running++;
                    break;
                case QuickPanelSection.Normal:
                    ok++;
                    break;
            }
        }
        FooterRunning.Text = string.Format(UiStrings.BoardFooterRunning, running);
        FooterOk.Text = string.Format(UiStrings.BoardFooterOk, ok);
    }

    /// <summary>
    /// B-601 通知中心：渲染 NotificationEngine.Records（旧→新 → 界面新→旧）。
    /// 记录由 notifications.json 水合跨重启；单击单条标已读，「全部已读」一键清；
    /// 无跳转（记录的 Widget 可能已删，链接语义交给 L3 详情）。
    /// </summary>
    private void RebuildNotifications()
    {
        var records = _runtime.Notifications.Records;
        NotificationList.Items.Clear();
        foreach (var record in records.Reverse().Where(MatchesNotificationFilter))
        {
            NotificationList.Items.Add(MakeNotificationRow(record));
        }
        // 空态（方案 A⑤）：无记录=「通知已全部已读」；过滤无匹配同用一句（出口在时间线/概览卡）
        NotificationsEmpty.Visibility = ComponentLayout.When(NotificationList.Items.Count == 0);
        NotificationsEmpty.Text = _activeSource is null
            ? UiStrings.BoardNotificationsEmpty
            : UiStrings.BoardFilterEmpty;
        var unread = _runtime.Notifications.UnreadCount;
        UnreadBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        UnreadCount.Text = unread.ToString();
    }

    /// <summary>通知按来源过滤（用户单①）：记录只带 SourceWidgetId，经 config 反查组件类型取来源键；
    /// 组件已删的记录在过滤态不出现（无类可归），全览态照常。</summary>
    private bool MatchesNotificationFilter(NotificationRecord record)
        => _activeSource is null
           || (_runtime.Config.FindWidget(record.SourceWidgetId) is { } widget
               && QuickPanelFilter.SourceKey(widget.Type) == _activeSource);

    private ListViewItem MakeNotificationRow(NotificationRecord record)
    {
        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new SolidColorBrush(_palette.SeverityColor(record.Severity)),
        };
        var time = new TextBlock
        {
            Text = record.Timestamp.ToLocalTime().ToString("MM-dd HH:mm"),
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            FontSize = DesignTokens.FontCaption,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var title = new TextBlock
        {
            Text = record.Title,
            Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
            FontSize = DesignTokens.FontBody,
            // BUG-2（2026-11-05 用户截图）：换行优先不省略；MaxWidth 仅作换行边界
            // （横向 StackPanel 给子项无限宽 offer，无上界会顶出面板被窗缘裁切）
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis, // 仅换行后仍放不下的超长单词才现省略号
            MaxWidth = 260,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = record.Read ? FontWeights.Normal : FontWeights.SemiBold, // 未读加粗区分
        };
        var row = ComponentLayout.Row(DesignTokens.Spacing(7, _runtime.Config.App.Appearance.Density));
        row.Children.Add(dot);
        row.Children.Add(time);
        row.Children.Add(title);
        return new ListViewItem { Content = row, Padding = new Thickness(2, 2, 2, 2), Tag = record };
    }

    private void OnNotificationItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListViewItem { Tag: NotificationRecord { Read: false } record })
        {
            _runtime.Notifications.MarkRead(record.Id); // RecordsChanged 不进总线，直接重画
            RebuildNotifications();
        }
    }

    private void OnMarkAllRead(object sender, RoutedEventArgs e)
    {
        _runtime.Notifications.MarkAllRead();
        RebuildNotifications();
    }

    /// <summary>行激活统一入口：人手 ItemClick 与 UIA Invoke 同链（B-603）。Tag 两形态——状态区行带
    /// WidgetState、RECENT 行带 RecentEvent 事件壳——都解析到底层 state 后走 DetailRequested 唯一订阅。</summary>
    private void OpenDetailFromRow(ListViewItem? item)
    {
        if (item?.Tag switch
        {
            WidgetState state => state,
            RecentEvent evt => evt.State,
            _ => null,
        } is { } target)
        {
            DetailRequested?.Invoke(target);
        }
    }

    private void OnStateItemClick(object sender, ItemClickEventArgs e) => OpenDetailFromRow(e.ClickedItem as ListViewItem);

    private void OnRecentItemClick(object sender, ItemClickEventArgs e) => OpenDetailFromRow(e.ClickedItem as ListViewItem);

    /// <summary>右键 Pin/Unpin：仅 PinSupported 类型可钉（B-702 两入口一致性），落 widgets.json。</summary>
    private void PinWidget(string widgetId)
    {
        var widget = _runtime.Config.FindWidget(widgetId);
        if (widget is null || _runtime.Resolver.Resolve(widget.Type)?.Descriptor.PinSupported != true)
        {
            return; // 非准入类型不可钉
        }
        widget.Pinned = !widget.Pinned;
        _runtime.Config.UpsertWidget(widget);
        _runtime.ReloadWidgets(widget.Pinned ? [widgetId] : null); // 开钉=重建注册+立即拉数（与设置页钉开关同口径）
        PinsChanged?.Invoke();
    }
}
