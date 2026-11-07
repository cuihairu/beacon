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

/// <summary>Recent Events 列表行（状态变化流，与通知同源，RFC §6.4）。</summary>
internal sealed record RecentEvent(DateTimeOffset At, WidgetState State);

/// <summary>
/// L2 看板（RFC §6.4 Flyout 语义保留）：失焦自动隐藏、ESC 关闭、Tab 可达、不进任务栏。
/// PANEL-UPGRADE-1 看板化：默认 720x560 大板面、可拖拽调尺寸（无边框厚框命中区 + 头部把手拖动），
/// 尺寸/位置记忆走 shell-state（ShellStateStore.QuickPanelLayout，PinLayoutMath 锚点口径）；
/// 事件动态（时间线 时刻/级别/来源/摘要）为左主区。
/// 重设计方案 B（2026-11-11 用户拍板，主从反转；原型 board-redesign.html 方案 B）：
/// 头带（点阵把手 affordance + 品牌标 #d4234a）→ 左「现在」296px 固定列（聚合状态灯 + OverallSummary +
/// 「需要关注」告警项列表占最大视觉权重 + 概览来源卡收底）与右时间线主区（rail 时间轴 + 跨日日期）+ 通知段
/// （MaxHeight 硬顶全消、段内滚动）→ 状态脚条（运行中/正常计数 + 交互提示）；空态文案三处。
/// 常驻档（config.keepQuickPanelOpen，托盘菜单 Keep Panel Open 打勾）：失焦不自隐，面板一直在。
/// 打开零网络等待（B-601/B-602）：内容常驻订阅事件流 + 启动时从缓存水合，打开 &lt;100ms。
/// Overview 按来源计数，卡片可点击过滤（默认全部 + 再点取消，2026-10-14 用户单①）；
/// 时间线行单击进 L3（DetailRequested），右键 Pin to desktop 落 widgets.json。
/// 文案经 Core UiStrings 取词（用户单②），语言切换即时重画。
/// </summary>
public sealed partial class QuickPanelWindow : Window
{
    private const int PanelWidth = 720;  // PANEL-UPGRADE-1 看板默认尺寸（master-plan 面板放大拍板 ~720x560，可拖拽调）
    private const int PanelHeight = 560;
    private const int MinPanelWidth = 520;  // 拖小下限：左「现在」列固定 296（方案 B）+ 时间线摘要区仍可读
    private const int MinPanelHeight = 320;
    private const int MaxRecentEvents = 50; // 内存事件壳上限（与 Core TimelineEntries.BoardMaxEntries 同口径，滚动承载）

    private const double BoardRowFontSize = 13;    // 看板行正文（用户「面板太小看不清」反馈：比全局 FontBody 12 升半档）
    private const double BoardRowCaptionSize = 12; // 看板行时刻/来源位（原 Caption 11 升一档）

    private const int GWL_STYLE = -16;          // 看板可调尺寸：无边框 presenter 下补回 WS_THICKFRAME 的样式槽
    private const long WS_THICKFRAME = 0x00040000L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;

    private readonly NativeMethods.RECT _fallbackWorkArea = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly IUiDispatcher _dispatcher;
    private readonly ShellStateStore _shellState;
    private readonly Dictionary<string, WidgetState> _states = [];
    private readonly List<RecentEvent> _events = [];
    private string? _activeSource; // OVERVIEW 卡片点击过滤：null=全部（默认），点击同类再点取消（用户单①）

    private QuickPanelLayout? _boardLayout; // shell-state 记忆位（null=没拖过，回落默认右下 720x560）
    private DragSession? _drag; // 头部把手拖动会话（PinnedHostWindow 同款记录：光标物理像素锚定）
    private bool _dragMoved; // 死区判定：区分把手点击与拖动（点击不落库）

    private IntPtr _hwnd;
    private AppWindow _appWindow = null!;
    private bool _visible;

    /// <summary>请求打开某 Widget 的 L3 详情窗（B-603 接线）。</summary>
    public event Action<WidgetState>? DetailRequested;

    /// <summary>Pin/Unpin 落库后通知（B-702：L0 宿主重建 tile 集）。</summary>
    public event Action? PinsChanged;

    public QuickPanelWindow(BeaconRuntime runtime, IUiDispatcher dispatcher, ShellStateStore shellState)
    {
        InitializeComponent();
        Title = "Beacon 快速面板"; // 窗口标题仅作取证/自动化锚点（边框已隐，UI 不可见；同 Beacon Tile 口径）
        // RFC §6.4：Flyout 语义——WinUI 栈规范「Acrylic 用于 flyout/瞬态面」，系统亚克力背板
        SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config); // B-706：渲染时实时读 appearance.SeverityColors
        _dispatcher = dispatcher;
        _shellState = shellState;

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
        ((FrameworkElement)Content).ActualThemeChanged += OnBoardThemeChanged; // 方案 A：告警条/把手配色随主题重挂
        ((FrameworkElement)Content).SizeChanged += OnBoardSizeChanged; // 交互式拖小下限钳回
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = true; // PANEL-UPGRADE-1：看板可拖拽调尺寸（下限 MinPanel* 由记忆/默认口径约束）
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
        _appWindow.IsShownInSwitchers = false;
        NativeMethods.AddWindowExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW);

        // PANEL-UPGRADE-1 可调尺寸：SetBorderAndTitleBar(false,false) 连厚框命中区一并摘除——
        // 补回 WS_THICKFRAME 让四边出系统 resize 光标与拖拽循环，WS_CAPTION 不回（视觉仍无边框 flyout）
        var style = NativeMethods.GetWindowLongPtr(_hwnd, GWL_STYLE).ToInt64();
        NativeMethods.SetWindowLongPtr(_hwnd, GWL_STYLE, new IntPtr(style | WS_THICKFRAME));
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpFrameChanged);

        // 头部把手拖动整窗（PinnedHostWindow 同款会话口径）：按住头部空白区拖动、松手落位记忆
        HeaderDragArea.PointerPressed += OnHeaderPointerPressed;
        HeaderDragArea.PointerMoved += OnHeaderPointerMoved;
        HeaderDragArea.PointerReleased += OnHeaderPointerReleased;
        HeaderDragArea.PointerCanceled += OnHeaderPointerReleased;
        // 方案 A⑥ 把手 affordance：悬停点阵转品牌色（默认弱灰点阵，头部可拖不再是隐形线索）
        HeaderDragArea.PointerEntered += (_, _) => UpdateGrabAffordance(hover: true);
        HeaderDragArea.PointerExited += (_, _) => UpdateGrabAffordance(hover: false);

        ApplyBoardChrome(); // 品牌标/告警条底色/把手点阵首挂（主题相关色走代码侧，主题切换 ActualThemeChanged 重挂）

        // 设计值是 DIP，AppWindow.Resize 收物理像素——只在 Win32 边界经 DpiLayoutMath 换算一次，
        // 否则高缩放下内容被裁（DPI 修复单 2026-10-10）。Resize 收的是窗口外框，客户区需求必须
        // 补回非客户区差值（无边框 presenter 仍保留 ~6px resize 边框），否则底部内容落进不可见区
        // （2026-10-14 进度条缺陷单同型根因：tile 底部 4 DIP 条带整条不可见）
        var dpi = GetDpi();
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
        _boardLayout = _shellState.LoadQuickPanelLayout(); // 看板尺寸记忆（shell-state 惯例；损坏/缺失回落默认）
        var width = Math.Clamp(_boardLayout?.Width ?? PanelWidth, MinPanelWidth, 1600);
        var height = Math.Clamp(_boardLayout?.Height ?? PanelHeight, MinPanelHeight, 1200);
        _appWindow.Resize(new SizeInt32(
            DpiLayoutMath.ToPx(width, dpi) + chromeWidth,
            DpiLayoutMath.ToPx(height, dpi) + chromeHeight));
        _appWindow.Hide();
        _visible = false;
        Closed += (_, _) => PersistLayout(); // 开着看板退应用也补记（Hide 路径另记）

        // B-601 验收：ESC 可完整关闭（global:: 限定：Beacon.App.Windows 会遮蔽 Windows 根命名空间）
        var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = global::Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, _) => Hide();
        ((FrameworkElement)Content).KeyboardAccelerators.Add(escape);

        ApplyStrings(); // 语言口径先行：XAML 缺省文案替换（含 OVERVIEW 头与 chip 标签）
        RebuildNotifications(); // notifications.json 水合记录随面板首开带出（跨重启）
        RebuildStatus(); // 「现在」列首绘（评审修复：纯缓存空启动无状态事件——告警空态文案/可见性须初始化）
        UpdateFooter(); // 脚条计数首绘（同上：无状态事件也要有 0 计数初值）
        RebuildRecent();
    }

    /// <summary>交互式拖小下限（评审修复）：SetBorderAndTitleBar(false,false)+WS_THICKFRAME 的系统 resize
    /// 无 presenter MinSize 可设，SizeChanged 松手钳回——左「现在」列固定 296，窗被拖到下限以下会吞掉右主区。
    /// 钳到位后再次触发 SizeChanged 不再满足条件，无回调环。</summary>
    private void OnBoardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_appWindow is null || _hwnd == IntPtr.Zero)
        {
            return;
        }
        var dpi = GetDpi();
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
        var size = _appWindow.Size;
        var minWidth = DpiLayoutMath.ToPx(MinPanelWidth, dpi) + chromeWidth;
        var minHeight = DpiLayoutMath.ToPx(MinPanelHeight, dpi) + chromeHeight;
        if (size.Width < minWidth || size.Height < minHeight)
        {
            _appWindow.Resize(new SizeInt32(
                Math.Max(size.Width, minWidth),
                Math.Max(size.Height, minHeight)));
        }
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
        if (_boardLayout is { } layout)
        {
            PositionAtSavedLayout(layout); // 记忆位（显示器 + 锚点角 + DIP 偏移，跨显示器热插拔回收）
        }
        else
        {
            PositionNearPrimaryCapsule(); // 默认位：主屏右下（胶囊上方）
        }
        _appWindow.Show();
        Activate(); // L2 需要键盘（ESC 关闭、Tab 导航）
        _visible = true;
    }

    public void Hide()
    {
        PersistLayout(); // 尺寸/位置随手记（看板 hide/save 惯例：拖过/调过即落 shell-state）
        _appWindow.Hide();
        _visible = false;
    }

    /// <summary>当前布局落 shell-state（PANEL-UPGRADE-1）：外框物理像素 → PinLayoutMath.Locate
    /// 锚点角 + DIP 偏移，尺寸记客户区 DIP（chrome 差值剔除，与 Restore 的加回口径互逆）。</summary>
    private void PersistLayout()
    {
        if (_appWindow is null || _hwnd == IntPtr.Zero)
        {
            return; // Loaded 前无可记
        }
        var dpi = GetDpi();
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
        var position = _appWindow.Position;
        var size = _appWindow.Size;
        var monitor = MonitorService.FromPixel(position.X + size.Width / 2, position.Y + size.Height / 2)
                      ?? MonitorService.Primary();
        if (monitor is null)
        {
            return;
        }
        var work = monitor.WorkPx;
        var rect = PinLayoutMath.ClampInto(
            ToPinRect(work), new PinRect(position.X, position.Y, size.Width, size.Height), margin: 0);
        var (anchor, offset) = PinLayoutMath.Locate(ToPinRect(work), rect, dpi);
        var saved = new QuickPanelLayout
        {
            Monitor = monitor.DeviceName,
            Anchor = anchor,
            OffsetDips = offset,
            Width = Math.Max(MinPanelWidth, (size.Width - chromeWidth) / dpi),
            Height = Math.Max(MinPanelHeight, (size.Height - chromeHeight) / dpi),
        };
        _shellState.SaveQuickPanelLayout(saved);
        _boardLayout = saved; // 记忆位同步内存：下次 Show 走落库锚点（不再回落默认右下）
    }

    /// <summary>记忆位恢复：显示器按名找回（热插拔回落主屏），PinLayoutMath.Place 锚点放置 + 越界回收；
    /// 尺寸用窗口现外框像素（Resize 已按记忆/默认客户区 DIP + chrome 落过）。</summary>
    private void PositionAtSavedLayout(QuickPanelLayout layout)
    {
        var monitor = MonitorService.FindByName(layout.Monitor) ?? MonitorService.Primary();
        var work = monitor?.WorkPx ?? _fallbackWorkArea;
        var dpi = GetDpi();
        var size = _appWindow.Size;
        var rect = PinLayoutMath.Place(
            ToPinRect(work), layout.Anchor, layout.OffsetDips, dpi, size.Width, size.Height);
        _appWindow.Move(new PointInt32(rect.X, rect.Y));
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
            RebuildStatus(); // 「现在」列标签/空态随语言重画
            UpdateFooter();
            RebuildRecent();
        });
    }

    /// <summary>静态文案统一入口（XAML 缺省英文 → 当前语言）；时间线主区头与 OVERVIEW 头随过滤态更新。</summary>
    private void ApplyStrings()
    {
        NotificationsHeader.Text = UiStrings.BoardNotifications;
        MarkAllReadButton.Content = UiStrings.BoardMarkAllRead;
        RecentHeader.Text = TimelineHeaderText(); // PANEL-UPGRADE-1：主区头「事件动态」双语 + 过滤后缀
        TimelineEmptyText.Text = UiStrings.BoardTimelineEmpty;
        TimelineFilterEmptyHint.Text = UiStrings.BoardFilterEmpty;
        ClearFilterButton.Content = UiStrings.BoardClearFilter;
        NotificationsEmpty.Text = UiStrings.BoardNotificationsEmpty;
        FooterHintActions.Text = UiStrings.BoardFooterHintActions;
        FooterHintWindow.Text = UiStrings.BoardFooterHintWindow;
        UpdateHeader(); // OverallSummary 文案（all clear / N warn · N error）
        UpdateOverview(); // OVERVIEW 头 + 卡片（含「全部」卡标签与高亮态）
        UpdateFooter(); // 脚条计数文案
        UpdateTimelineEmptyState(); // 空态文案随语言/过滤态
    }

    /// <summary>主题/品牌 chrome 色挂载（方案 B）：脚条色点走 UiPalette 实时读 config、把手点阵弱灰、
    /// 品牌标恒 #d4234a（ThemeColors.Brand，级别色两系不混用）。
    /// Loaded 与 ActualThemeChanged 双挂。</summary>
    private void ApplyBoardChrome()
    {
        FooterRunningDot.Fill = new SolidColorBrush(_palette.SeverityColor(Severity.Success, offline: true)); // 脚条「运行中」灰点（原型 --off 口径）
        FooterOkDot.Fill = new SolidColorBrush(_palette.SeverityColor(Severity.Success));
        BrandMark.Fill = new SolidColorBrush(ThemeColors.Brand());
        UpdateGrabAffordance(hover: false);
    }

    private void OnBoardThemeChanged(object sender, object e)
    {
        ApplyBoardChrome();
        RebuildRecent(); // 行/rail/卡面色随主题重挂（列表为现场全量重建口径，重画零残留）
        RebuildNotifications();
        RebuildStatus();
    }

    /// <summary>把手点阵 affordance（方案 A⑥）：默认弱灰点阵，悬停转品牌色——头部可拖有可见线索。</summary>
    private void UpdateGrabAffordance(bool hover)
    {
        var fill = hover
            ? new SolidColorBrush(ThemeColors.Brand())
            : new SolidColorBrush(ThemeColors.TextSecondary()) { Opacity = 0.45 };
        foreach (var dot in GrabHandle.Children.OfType<Ellipse>())
        {
            dot.Fill = fill;
        }
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

}
