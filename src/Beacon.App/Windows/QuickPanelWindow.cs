using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>Recent Events 列表行（状态变化流，与通知同源，RFC §6.4）。</summary>
internal sealed record RecentEvent(DateTimeOffset At, WidgetState State);

/// <summary>
/// L2 Quick Panel（RFC §6.4）：Flyout 语义——失焦自动隐藏、ESC 关闭、Tab 可达、不进任务栏。
/// 常驻档（config.keepQuickPanelOpen，托盘菜单 Keep Panel Open 打勾）：失焦不自隐，面板一直在。
/// 打开零网络等待（B-601/B-602）：内容常驻订阅事件流 + 启动时从缓存水合，打开 &lt;100ms。
/// Overview 按来源计数，chip 可点击过滤（默认全部 + 再点取消，2026-10-14 用户单①）；
/// Recent Events 单击进 L3（DetailRequested），右键 Pin to desktop 落 widgets.json。
/// 文案经 Core UiStrings 取词（用户单②），语言切换即时重画。
/// </summary>
public sealed partial class QuickPanelWindow : Window
{
    private const int PanelWidth = 380; // master-plan §3.2 建议宽 360–420，取中（批3-补27 拍板④；原 320）
    private const int PanelHeight = 540; // 通知中心区加入后加高（未读徽标 + 记录列表 MaxHeight 118）；在建议带 420–560 内
    private const int MaxRecentEvents = 50;

    private readonly NativeMethods.RECT _fallbackWorkArea = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, WidgetState> _states = [];
    private readonly List<RecentEvent> _events = [];
    private string? _activeSource; // OVERVIEW chip 点击过滤：null=全部（默认），点击同类再点取消（用户单①）

    private IntPtr _hwnd;
    private AppWindow _appWindow = null!;
    private bool _visible;

    /// <summary>请求打开某 Widget 的 L3 详情窗（B-603 接线）。</summary>
    public event Action<WidgetState>? DetailRequested;

    /// <summary>Pin/Unpin 落库后通知（B-702：L0 宿主重建 tile 集）。</summary>
    public event Action? PinsChanged;

    public QuickPanelWindow(BeaconRuntime runtime, IUiDispatcher dispatcher)
    {
        InitializeComponent();
        Title = "Beacon 快速面板"; // 窗口标题仅作取证/自动化锚点（边框已隐，UI 不可见；同 Beacon Tile 口径）
        // RFC §6.4：Flyout 语义——WinUI 栈规范「Acrylic 用于 flyout/瞬态面」，系统亚克力背板
        SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config); // B-706：渲染时实时读 appearance.SeverityColors
        _dispatcher = dispatcher;

        runtime.Bus.Subscribe<WidgetStateChanged>(evt => dispatcher.Post(() => OnStateChanged(evt.State, record: true)));
        runtime.Bus.Subscribe<WidgetsInvalidated>(evt => dispatcher.Post(() => OnWidgetsInvalidated(evt.WidgetIds)));
        runtime.Bus.Subscribe<AggregateStatusChanged>(_ => dispatcher.Post(UpdateHeader));
        runtime.Bus.Subscribe<NotificationRaised>(_ => dispatcher.Post(RebuildNotifications)); // B-601 通知中心
        UiStrings.LanguageChanged += OnLanguageChanged; // 用户单②：设置切语言，看板文案即时重画（App 生命周期单例，无需退订）

        // 缓存优先（B-601 验收：打开无网络等待白屏）——启动即用上次落盘状态填充首屏
        foreach (var connection in runtime.Config.Connections)
        {
            foreach (var state in runtime.Cache.LoadStates(connection.Id).Values)
            {
                OnStateChanged(state, record: false);
            }
        }

        Activated += OnActivated;
        ((FrameworkElement)Content).Loaded += OnLoaded; // WinUI 3 的 Window 本身没有 Loaded 事件
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
        _appWindow.IsShownInSwitchers = false;
        NativeMethods.AddWindowExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW);

        // 设计值是 DIP，AppWindow.Resize 收物理像素——只在 Win32 边界经 DpiLayoutMath 换算一次，
        // 否则高缩放下内容被裁（DPI 修复单 2026-10-10）。Resize 收的是窗口外框，客户区需求必须
        // 补回非客户区差值（无边框 presenter 仍保留 ~6px resize 边框），否则底部内容落进不可见区
        // （2026-10-14 进度条缺陷单同型根因：tile 底部 4 DIP 条带整条不可见）
        var dpi = GetDpi();
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
        _appWindow.Resize(new SizeInt32(
            DpiLayoutMath.ToPx(PanelWidth, dpi) + chromeWidth,
            DpiLayoutMath.ToPx(PanelHeight, dpi) + chromeHeight));
        _appWindow.Hide();
        _visible = false;

        // B-601 验收：ESC 可完整关闭（global:: 限定：Beacon.App.Windows 会遮蔽 Windows 根命名空间）
        var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = global::Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, _) => Hide();
        ((FrameworkElement)Content).KeyboardAccelerators.Add(escape);

        ApplyStrings(); // 语言口径先行：XAML 缺省文案替换（含 OVERVIEW 头与 chip 标签）
        RebuildNotifications(); // notifications.json 水合记录随面板首开带出（跨重启）
        RebuildRecent();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // 失焦自动关闭（RFC §6.4：Flyout 语义）；常驻档（托盘菜单 Keep Panel Open 打勾）跳过——
        // 面板要一直在，失焦不能自隐（Esc/再次点托盘仍走显式隐藏）
        if (_visible
            && args.WindowActivationState == WindowActivationState.Deactivated
            && !_runtime.Config.App.KeepQuickPanelOpen)
        {
            Hide();
        }
    }

    /// <summary>热键/胶囊/托盘切换显隐。</summary>
    public void Toggle()
    {
        if (_visible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>确保显示（托盘打勾「面板常驻」即刻出）：已显示不动，不翻转。</summary>
    public void Show()
    {
        if (_visible || _appWindow is null)
        {
            return; // Loaded 前无 AppWindow，启动链路不抢跑
        }
        PositionNearPrimaryCapsule();
        _appWindow.Show();
        Activate(); // L2 需要键盘（ESC 关闭、Tab 导航）
        _visible = true;
    }

    public void Hide()
    {
        _appWindow.Hide();
        _visible = false;
    }

    /// <summary>
    /// 托盘「通知中心」入口（B-503 语义分化，2026-10-14 用户单④）：确保面板可见后把焦点与滚动落到
    /// 通知区——与「打开 Beacon」（Toggle 开/关面板）区分，不再是同义入口。已显示时不反向关闭
    /// （点「通知中心」不应 Toggle 收起面板），仅重新聚焦。
    /// </summary>
    public void OpenNotifications()
    {
        Show();
        Activate(); // Show 对已可见面板是空操作不再 Activate——补一次取焦点，聚焦通知区才有可见焦点框
        if (NotificationList.Items.Count > 0)
        {
            NotificationList.ScrollIntoView(NotificationList.Items[0]);
        }
        NotificationList.Focus(FocusState.Programmatic); // 焦点框落在通知列表 = 可见落在通知视图
    }

    /// <summary>config.uiOpacity（B-801 界面透明度）：挂面板内容层（批8 2026-10-10：与胶囊/宿主面板/独立悬浮框统一同一来源）。</summary>
    public void ApplyOpacity(double opacity)
        => ((FrameworkElement)Content).Opacity = Math.Clamp(opacity, 0.2, 1.0);

    /// <summary>
    /// 语言切换广播（用户单②）：文案 + 计数 + 列表全量重画——过滤态保持不变（切语言不换视角）。
    /// 设置页与面板同在 UI 线程，仍走 dispatcher 归口（与总线订阅同一派发口径，避免重入）。
    /// </summary>
    private void OnLanguageChanged()
    {
        _dispatcher.Post(() =>
        {
            ApplyStrings();
            RebuildNotifications();
            RebuildStatus();
            RebuildRecent();
        });
    }

    /// <summary>静态文案统一入口（XAML 缺省英文 → 当前语言）；OVERVIEW 头带过滤后缀，chip 标签随 chip 重建。</summary>
    private void ApplyStrings()
    {
        NotificationsHeader.Text = UiStrings.BoardNotifications;
        MarkAllReadButton.Content = UiStrings.BoardMarkAllRead;
        AttentionHeader.Text = UiStrings.BoardNeedsAttention;
        RunningHeader.Text = UiStrings.BoardRunning;
        RecentHeader.Text = UiStrings.BoardRecentEvents;
        UpdateHeader(); // OverallSummary 文案（all clear / N warn · N error）
        UpdateOverview(); // OVERVIEW 头 + chip（含「全部」chip 标签与高亮态）
    }

    private double GetDpi() => NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

    /// <summary>运行时主题——动态构建的行/胶囊配色随之切换（静态部分走 XAML ThemeResource）。</summary>
    private bool IsDarkTheme => ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark;

    private void PositionNearPrimaryCapsule()
    {
        var work = MonitorService.Primary()?.WorkPx ?? _fallbackWorkArea;
        var dpi = GetDpi(); // work 是物理像素，偏移设计值是 DIP——同单位换算
        // 锚点数学按外框口径（Resize 同口径补回非客户区差值，2026-10-14 缺陷单）：可见面板
        // 保持 24/140 DIP 边距，不被 resize 边框吃掉
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
        var x = work.Right - (DpiLayoutMath.ToPx(PanelWidth, dpi) + chromeWidth) - DpiLayoutMath.ToPx(24, dpi);
        var y = work.Bottom - (DpiLayoutMath.ToPx(PanelHeight, dpi) + chromeHeight) - DpiLayoutMath.ToPx(140, dpi); // 胶囊（右下角）上方
        _appWindow.Move(new PointInt32(x, y));
    }

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
        UpdateHeader();
        UpdateOverview();
        RebuildStatus(); // 状态区随当前状态现场重建（原仅 OnWidgetsInvalidated 触达——面板常驻期间「需要关注」恒为开面板前旧影，场景 S 实证）
        RebuildRecent();
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

    /// <summary>OVERVIEW 区（用户单①）：默认「全部」chip 在首 + 来源计数 chip 可点击过滤；
    /// 当前看哪一类必须界面可见——激活 chip 高亮填充 + OVERVIEW 头带「· 来源」后缀双保险。</summary>
    private void UpdateOverview()
    {
        OverviewHeader.Text = _activeSource is null
            ? UiStrings.BoardOverview
            : $"{UiStrings.BoardOverview} · {_activeSource}";
        SourceCounts.Children.Clear();
        SourceCounts.Children.Add(MakeChip(
            UiStrings.BoardAll, _states.Values.Count,
            _states.Values.Count == 0 ? Severity.Success : _states.Values.Max(s => s.Severity),
            sourceKey: null, active: _activeSource is null));
        foreach (var group in _states.Values
            .GroupBy(state => QuickPanelFilter.SourceKey(state.WidgetType))
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var worst = group.Max(state => state.Severity);
            SourceCounts.Children.Add(MakeChip(group.Key, group.Count(), worst,
                sourceKey: group.Key, active: _activeSource == group.Key));
        }
    }

    /// <summary>chip 点击过滤（用户单①）：点某类只看该类，再点同类取消回全部；「全部」chip 恒回全部。</summary>
    private void ApplySourceFilter(string? sourceKey)
    {
        _activeSource = _activeSource == sourceKey && sourceKey is not null ? null : sourceKey;
        UpdateOverview(); // 头后缀 + chip 高亮态（可见过滤状态）
        RebuildStatus();
        RebuildRecent();
        RebuildNotifications();
    }

    private Border MakeChip(string label, int count, Severity worst, string? sourceKey = null, bool active = false)
    {
        var dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(_palette.SeverityColor(worst)) };
        var text = new TextBlock
        {
            Text = $"{label} {count}",
            Foreground = new SolidColorBrush(active ? SeverityPalette.Rgb(255, 255, 255, 255) : IsDarkTheme ? SeverityPalette.Rgb(255, 241, 245, 249) : SeverityPalette.Rgb(255, 30, 41, 59)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        row.Children.Add(dot);
        row.Children.Add(text);
        // 激活态=当前在看：实色填充（两主题同色白字，对比可辨）；非激活维持原浅底
        var chip = new Border
        {
            Background = new SolidColorBrush(active ? SeverityPalette.Rgb(255, 37, 99, 235) : IsDarkTheme ? SeverityPalette.Rgb(28, 255, 255, 255) : SeverityPalette.Rgb(20, 15, 23, 42)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 8, 3),
            Child = row,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(chip, $"chip-{sourceKey ?? "all"}"); // UIA 取证/CI 点击入口
        chip.Tapped += (_, _) => ApplySourceFilter(sourceKey); // sourceKey=null 的「全部」chip 恒回全部
        return chip;
    }

    private void RebuildRecent()
    {
        RecentList.Items.Clear();
        foreach (var evt in _events.Where(evt => QuickPanelFilter.Matches(_activeSource, evt.State.WidgetType)))
        {
            RecentList.Items.Add(MakeEventRow(evt));
        }
    }

    /// <summary>
    /// 批3-补27 拍板④（master-plan §3.2 三分区）：当前状态按 Core 纯函数 QuickPanelSections 分组——
    /// 需要关注（失败/超时/离线/过期，级别降序）与正在运行；与 RECENT EVENTS（事件流）正交。
    /// 空区隐藏，两区全空整块收起（面板回落旧观感）。
    /// </summary>
    private void RebuildStatus()
    {
        var attention = new List<WidgetState>();
        var running = new List<WidgetState>();
        foreach (var state in _states.Values.Where(s => QuickPanelFilter.Matches(_activeSource, s.WidgetType)))
        {
            switch (QuickPanelSections.Resolve(state))
            {
                case QuickPanelSection.NeedsAttention:
                    attention.Add(state);
                    break;
                case QuickPanelSection.InProgress:
                    running.Add(state);
                    break;
            }
        }
        attention.Sort((a, b) => b.Severity.CompareTo(a.Severity)); // Critical 最先
        running.Sort((a, b) => b.Severity.CompareTo(a.Severity));
        FillStateList(AttentionList, AttentionHeader, attention);
        FillStateList(RunningList, RunningHeader, running);
        StatusPanel.Visibility = attention.Count == 0 && running.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void FillStateList(ListView list, FrameworkElement header, IReadOnlyList<WidgetState> states)
    {
        list.Items.Clear();
        foreach (var state in states)
        {
            list.Items.Add(MakeStateRow(state, state.FetchedAt.ToLocalTime().ToString("HH:mm:ss")));
        }
        var visible = states.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        list.Visibility = visible;
        header.Visibility = visible;
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
            Foreground = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(255, 148, 163, 184) : SeverityPalette.Rgb(255, 100, 116, 139)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var title = new TextBlock
        {
            Text = record.Title,
            Foreground = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(255, 226, 232, 240) : SeverityPalette.Rgb(255, 30, 41, 59)),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260, // 面板加宽 320→380 后同步放开（原 210）
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = record.Read ? FontWeights.Normal : FontWeights.SemiBold, // 未读加粗区分
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
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

    private ListViewItem MakeEventRow(RecentEvent evt)
        => MakeStateRow(evt.State, evt.At.ToString("HH:mm:ss"), tag: evt);

    /// <summary>状态行（当前状态区与 RECENT 共用）：级别点 + 时间 + 摘要；单击进 L3，右键 Pin（B-702 口径）。</summary>
    private ListViewItem MakeStateRow(WidgetState state, string timeText, object? tag = null)
    {
        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new SolidColorBrush(_palette.SeverityColor(state.Severity,
                widgetOverride: _runtime.Config.FindWidget(state.WidgetId)?.ColorOverride)),
        };
        var time = new TextBlock
        {
            Text = timeText,
            Foreground = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(255, 148, 163, 184) : SeverityPalette.Rgb(255, 100, 116, 139)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text = state.Summary,
            Foreground = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(255, 226, 232, 240) : SeverityPalette.Rgb(255, 30, 41, 59)),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260, // 面板加宽 320→380 后同步放开（原 210）
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        row.Children.Add(dot);
        row.Children.Add(time);
        row.Children.Add(label);

        var item = new InvokeableListItem { Content = row, Padding = new Thickness(2, 3, 2, 3), Tag = tag ?? state };
        item.InvokeRequested += () => OpenDetailFromRow(item); // UIA Invoke 与 ItemClick 同链（行 46 解锁面）
        var pinned = _runtime.Config.FindWidget(state.WidgetId)?.Pinned == true;
        var pin = new MenuFlyoutItem { Text = pinned ? "Unpin from desktop" : "Pin to desktop" };
        pin.Click += (_, _) => PinWidget(state.WidgetId);
        item.ContextFlyout = new MenuFlyout { Items = { pin } };
        return item;
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

    /// <summary>
    /// UIA 可达行（todo 行 46 解锁面）：ListViewItem 默认只暴露 SelectionItem/ScrollItem——S4 三 pattern
    /// 实测（38325074321/38330530278）全打不开 L3。加 Invoke pattern 后 CI/辅助技术与 ItemClick 同链开
    /// L3 详情，人手点击行为零变化（ItemClick 处理器未动，Invoke 走同一 OpenDetailFromRow）。
    /// </summary>
    private sealed class InvokeableListItem : ListViewItem
    {
        /// <summary>UIA Invoke 请求；宿主窗接到与 ItemClick 相同的处理链。</summary>
        public event Action? InvokeRequested;

        public void RaiseInvoke()
        {
            var queue = DispatcherQueue;
            if (queue is not null && !queue.HasThreadAccess)
            {
                queue.EnqueueGuarded(() => InvokeRequested?.Invoke()); // UIA 调用可能来自非 UI 线程，转回 UI 线程
            }
            else
            {
                InvokeRequested?.Invoke();
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new InvokeableListItemPeer(this);
    }

    /// <summary>Invoke pattern 只增不改：其余 pattern 走 ListViewItem 基线（SelectionItem 等保持默认语义）。</summary>
    private sealed class InvokeableListItemPeer : ListViewItemAutomationPeer, IInvokeProvider
    {
        public InvokeableListItemPeer(InvokeableListItem owner)
            : base(owner)
        {
        }

        protected override object? GetPatternCore(PatternInterface patternInterface)
            => patternInterface == PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);

        void IInvokeProvider.Invoke() => ((InvokeableListItem)Owner).RaiseInvoke();
    }
}

/// <summary>中性灰阶色（L1/L2/L3 文本与底色共用）；级别色已由 UiPalette 承载（B-706）。</summary>
internal static class SeverityPalette
{
    public static global::Windows.UI.Color Rgb(byte a, byte r, byte g, byte b) => new() { A = a, R = r, G = g, B = b };
}
