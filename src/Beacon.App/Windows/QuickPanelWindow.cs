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

    /// <summary>头部把手拖动会话（左键按住起手）：光标屏幕物理像素锚定，与 AppWindow.Move 同坐标系。</summary>
    private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(HeaderDragArea).Properties.IsLeftButtonPressed || _appWindow is null)
        {
            return;
        }
        _dragMoved = false;
        _drag = new DragSession(HeaderDragArea, CursorPx(), _appWindow.Position.X, _appWindow.Position.Y);
        HeaderDragArea.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null || !ReferenceEquals(_drag.Element, sender) || _appWindow is null)
        {
            return;
        }
        var (cursorX, cursorY) = CursorPx();
        var dx = cursorX - _drag.StartCursor.X;
        var dy = cursorY - _drag.StartCursor.Y;
        if (!_dragMoved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
        {
            return; // 死区，区分点击与拖动
        }
        _dragMoved = true;
        var work = MonitorService.Primary()?.WorkPx ?? _fallbackWorkArea;
        var width = _appWindow.Size.Width;
        var height = _appWindow.Size.Height;
        // 夹回工作区：拖出去的看板收不回来（与 CapsuleWindow RestorePosition 同口径）
        var x = Math.Clamp(_drag.StartX + dx, work.Left, Math.Max(work.Left, work.Right - width));
        var y = Math.Clamp(_drag.StartY + dy, work.Top, Math.Max(work.Top, work.Bottom - height));
        if (x != _appWindow.Position.X || y != _appWindow.Position.Y)
        {
            _appWindow.Move(new PointInt32(x, y)); // 目标没变不重发 Move：减少 SetWindowPos 抖动
        }
        e.Handled = true;
    }

    private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null || !ReferenceEquals(_drag.Element, sender))
        {
            return;
        }
        _drag = null;
        HeaderDragArea.ReleasePointerCapture(e.Pointer);
        if (_dragMoved)
        {
            PersistLayout(); // 拖动落点记忆；死区内点击不落库
        }
        e.Handled = true;
    }

    /// <summary>光标屏幕物理像素（GetCursorPos；与 AppWindow.Position/Move 同一坐标系）。</summary>
    private (int X, int Y) CursorPx()
        => NativeMethods.GetCursorPos(out var point) ? (point.X, point.Y) : (_appWindow.Position.X, _appWindow.Position.Y);

    /// <summary>Win32 RECT（Left/Top/Right/Bottom 物理像素）→ Core 布局矩形（X/Y/Width/Height）：
    /// PinLayoutMath 是纯 Core 域（CapsuleWindow 同款换算），边界换算留在窗体侧。</summary>
    private static PinRect ToPinRect(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

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
        RebuildStatus(); // 「现在」列随当前状态现场重建（OnStateChanged 现场重建口径保留——场景 S 实证）
        UpdateFooter(); // 脚条「运行中/正常」计数随状态现场刷新
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
