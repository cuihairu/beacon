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

/// <summary>
/// L1 Status Capsule（RFC §6.3）：常驻聚合状态 tile。
/// 置顶、不进任务栏/Alt-Tab、不抢焦点、可拖动、位置按「显示器+锚点角+DIP 偏移」持久化。
/// 也是 PinnedHost 的第一个成员（RFC §6.2.4 单窗口共享渲染的雏形）。
/// </summary>
public sealed partial class CapsuleWindow : Window
{
    private readonly ShellStateStore _shellState;
    private readonly Beacon.Storage.JsonConfigurationStore _config; // 岛进度组件勾选态实时读（组件中心单源）
    private readonly UiPalette _palette;
    private readonly MotionEngine _motion;
    private readonly NativeMethods.RECT _defaultWorkAreaFallback = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

    private IntPtr _hwnd;
    private AppWindow _appWindow = null!;
    private (int Width, int Height) _sizePx; // 最近一次应用的外框物理尺寸（客户区需求+非客户区差值，定位与落盘共用，避免读旧值）
    private bool _positioned; // RestorePosition 已跑过（此后 ApplySize 变尺寸需重新贴锚点）
    private bool _showAllowed = true; // 用户显隐意愿（TopmostGuard 据此放行；懒建后窗存在即用户放行）
    private bool _dragging;
    private bool _dpiChangedDuringDrag; // 拖动中跨了 DPI：落点后补排尺寸（拖动中跳过是为不与 Move 互抢）
    private (int X, int Y) _dragStartCursor; // 按下时光标屏幕物理像素（GetCursorPos，与 AppWindow.Position 同单位）
    private (int X, int Y) _dragOrigin; // 按下时的窗口位置：拖动目标 = 起点 + 光标位移
    private NativeMethods.WndProcDelegate? _wndProc; // 字段持有防 GC 回收子类化回调
    private IntPtr _prevWndProc;
    private int _x;
    private int _y;
    private DateTimeOffset _lastFetchedAt;
    private Severity _lastOverall = Severity.Success; // 级别抬升判定基线（Flash 触发面）
    private bool _hovered; // 指针在窗内（悬停预览展开条件之一）
    private IReadOnlyList<SourcePreviewRow> _previewRows = Array.Empty<SourcePreviewRow>();
    private bool _island; // 悬浮岛形态（§7 Phase 2，config.capsuleIsland）：顶中锚定 + 品牌标/徽标收起态
    private bool _chromeCaptured; // 经典形态的 XAML 视觉快照（首次切岛前抓，切回原样还原）
    private Brush _classicPillBackground = null!;
    private Brush _classicPillBorder = null!;
    private Thickness _classicPadding;
    private double _classicRowSpacing;
    private Brush _classicBrandForeground = null!;
    private Brush _classicPreviewBackground = null!;
    private Brush _classicPreviewBorder = null!;
    private Thickness _classicBorderThickness; // RootBorder 描边粗细（岛归 0：描边交给曲面 Path）
    private CornerRadius _classicCornerRadius; // RootBorder 药丸圆角（岛归 0：曲面自绘顶满宽+底角12）
    private Thickness _classicPreviewThickness; // 预览描边粗细（岛归 0：预览在曲面内无缝）
    private double _ringRatio = 1.0; // 最近一次健康环份额（岛↔经典切换后重画环用）
    private global::Windows.UI.Color _ringColor = ThemeColors.StatusOk(); // 最近一次环色（同上）
    private int _unreadCount; // 未读通知数（App 订阅 NotificationRaised 喂数；岛徽标第一优先）
    private int _abnormalCount; // 异常计数（warn+err，UpdateStatus 随聚合刷新；岛徽标第二优先）
    private Thickness _classicStackMargin; // SurfaceStack 经典外边距（岛=两侧 NotchInset 30 内缩）
    private HorizontalAlignment _classicPillAlign; // PillRow 经典对齐（岛=居中，刘海内容居中读）
    private VerticalAlignment _classicStackAlign; // SurfaceStack 经典垂直对齐（岛=Top：窗口含 8 DIP 阴影带，内容行钉顶贴曲面）
    private bool _ruleHidden; // 全屏避让：前台无边框全屏窗盖住岛所在屏 → 岛暂时退场（BrimDeck Borderless/Exclusive→Hide 默认口径）
    private NativeMethods.WinEventProc? _foregroundCallback; // 字段持有防 GC 回收钩子回调（同 _wndProc）
    private IntPtr _foregroundHook;
    private double _islandWidthDips; // 岛曲面宽（DIP）：MeasureSizePx 记账 → 预览 MaxWidth/曲面宽共用
    private int _morphGeneration; // 收起动效代际：重新悬停/拖动即作废未落定的收面回调（防悬停打断后仍被收掉）
    /// <summary>取证钩子（BEACON_DEBUG_ISLAND_PREVIEW=1，与 App 的 BEACON_DEBUG_CRASH 同族）：
    /// 锁屏/无头装机演练注入不了真实指针，置此变量后首次拿到预览行即自动展开一次，
    /// 供三态截图取证；不设变量时行为逐字节不动，不进设置 UI。</summary>
    private static readonly bool IslandPreviewProbe =
        string.Equals(Environment.GetEnvironmentVariable("BEACON_DEBUG_ISLAND_PREVIEW"), "1", StringComparison.Ordinal);

    private bool _previewProbeShown; // 钩子只展开一次（后续行变化不再反复弹）

    /// <summary>无拖动的单击 → 打开 L2 Quick Panel（B-504）。</summary>
    public event Action? OpenPanelRequested;

    /// <summary>岛进度组件规格（ISLAND-RING-1）：环 Ø22/弦 2.5（32 高行内的 BrimDeck 徽标环口径）、
    /// 条宽 56×5（环+条并置时行内不拥挤，238 下限宽装得下）。</summary>
    private const double IslandRingSize = 22;
    private const double IslandRingThickness = 2.5;
    private const double IslandBarWidth = 56;
    private const double IslandBarThickness = 5;

    /// <summary>右键 → 直达设置（master-plan §3.1「右键打开…常用设置」；批3-补28）。</summary>
    public event Action? SettingsRequested;

    /// <summary>悬停预览行（2026-10-29 UI 升级）：来源聚合一行——级别点 + 来源名 + 最差态摘要。</summary>
    public sealed record SourcePreviewRow(string Source, Severity Worst, string Summary);

    public CapsuleWindow(ShellStateStore shellState, Beacon.Storage.JsonConfigurationStore config)
    {
        InitializeComponent();
        // RFC §6.2.2：亚克力/半透明背景（桌面悬浮物贴壁纸，Mica 会失去悬浮感，故用系统亚克力）
        SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        _shellState = shellState;
        _config = config; // 岛进度组件勾选态实时读（ISLAND-RING-1：组件中心勾选 → 岛渲染面，Core 单源）
        _palette = new UiPalette(config); // B-706：聚合灯随 appearance.SeverityColors
        _motion = new MotionEngine(config); // 入场/抬升动效（档位实时读，off 档全瞬时）
        // 初始灯/晕色（XAML 无 x:Static 不落 hex，语义色板单源 ThemeColors）
        var initial = ThemeColors.StatusOk();
        OverallLight.Fill = new SolidColorBrush(initial);
        OverallHalo.Fill = new SolidColorBrush(ThemeColors.Halo(initial, 64));
        ((FrameworkElement)Content).Loaded += OnLoaded; // WinUI 3 的 Window 本身没有 Loaded 事件
        // 启动期抢先把 AppWindow 句柄取到（WinUI 3 Window 构造即建原生窗）：EnsureCapsule 在 Activate
        // 前调用 SetIslandMode 时，其既有 ApplySize/RestorePosition 路径就能按岛/胶囊口径预排尺寸，
        // 避免窗口先以系统默认尺寸（近全屏）闪一帧再被 OnLoaded 收窄——真机 trace outline 1907x32/
        // root=1907x990 实证。HWND 未就绪（平台时序差异）则交给 OnLoaded 的既有初始化，绝不因此崩溃。
        try
        {
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd));
        }
        catch
        {
            // 取不到不改状态：OnLoadedCore 兜底（构造期拿句柄失败的窗口照常由既有路径初始化）
        }
        // 曲面轮廓按实际尺寸重绘（岛展开/切形态/换 DPI 都会改 RootBorder 尺寸；经典形态内部早退）
        RootBorder.SizeChanged += (_, _) => UpdateSurfaceOutline();
        Closed += (_, _) =>
        {
            if (_foregroundHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_foregroundHook); // 防原生钩子悬挂在已销毁窗上
                _foregroundHook = IntPtr.Zero;
            }
        };
    }

    /// <summary>启动期定位痕迹（USER-RPT-4 取证用）：把关键步骤与异常栈写进
    /// %APPDATA%\Beacon\logs\island-trace.txt——WinUI UnhandledException 拿到的
    /// InvalidCastException 常无栈，在抛出点就地捕获才能留下托管栈。</summary>
    private static void IslandTrace(string message)
    {
        try
        {
            // System.IO.Path 与 Microsoft.UI.Xaml.Shapes.Path 同名（本文件 using Shapes）→ 显式限定
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon", "logs");
            Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, "island-trace.txt");
            // 轮转：悬停开合每次都落一行，超 64KB 从头截断，痕迹文件永不撑盘
            if (File.Exists(file) && new FileInfo(file).Length > 64 * 1024)
            {
                File.Delete(file);
            }

            File.AppendAllText(file, $"{DateTimeOffset.Now:HH:mm:ss.fff} {message}\n");
        }
        catch
        {
            // 痕迹写失败绝不反噬常驻应用
        }
    }

    /// <summary>取证通道（internal）：App 聚合订阅侧复用同一痕迹文件，岛数据链跨层可对账
    /// （ISLAND-RING-1 取证：聚合→过滤→岛，err 态在哪层丢的一眼定位）。</summary>
    internal static void IslandTraceShared(string message) => IslandTrace(message);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            OnLoadedCore(e);
            IslandTrace("OnLoaded OK");
        }
        catch (Exception ex)
        {
            IslandTrace($"OnLoaded FAILED\n{ex}");
            throw;
        }
    }

    private void OnLoadedCore(RoutedEventArgs e)
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
        // 常驻悬浮物：不进任务栏/Alt-Tab、点击不抢焦点（RFC §6.2.7）
        NativeMethods.AddWindowExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        SubclassForDpiChange();
        ApplySize(); // 按内容实测定尺寸（DIP×DPI 转物理像素，宽随内容=§6.2.2）
        RestorePosition();
        _motion.SlideIn(RootBorder); // 入场滑入（full 档专属，其余档瞬时；2026-10-29 UI 升级）
        HookFullscreenAvoidance(); // 前台事件钩子：无边框全屏窗盖住岛所在屏时岛退场（USER-RPT-4 避让面）
        EvaluateFullscreenAvoidance(); // 启动即评估（进程可能在全屏应用已前台时才起）
    }

    /// <summary>子类化挂 WM_DPICHANGED（同单宿主 PinnedHostWindow 口径）：跨缩放屏后立即按新 DPI 重排，
    /// 不再等下一次数据刷新才自愈。</summary>
    private void SubclassForDpiChange()
    {
        _wndProc = WndProc;
        _prevWndProc = NativeMethods.SetWindowLongPtr(
            _hwnd, NativeMethods.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_DPICHANGED)
        {
            // 拖动中跳过——重排的 Move 会和拖拽 Move 互抢窗口位置（跨屏闪烁），落点后在 released 补排
            if (_dragging)
            {
                _dpiChangedDuringDrag = true;
            }
            else
            {
                ApplySize(); // GetDpiForWindow 此时已返回新 DPI，MeasureSizePx 实测内容宽随新缩放
            }
        }
        return _prevWndProc == IntPtr.Zero
            ? NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam)
            : NativeMethods.CallWindowProcW(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>TopmostGuard 用：消息窗句柄与用户显隐意愿。</summary>
    public IntPtr Hwnd => _hwnd;

    public bool IsUserVisible => _showAllowed;

    /// <summary>哨兵放行面 = 用户显隐意愿 且 非全屏避让中。两者同真才允许 Show/钉顶——
    /// 避让钩子与哨兵都随同一前台事件触发，无论先后顺序，最终态都是「全屏前台=退场」。</summary>
    public bool IsGuardVisible => _showAllowed && !_ruleHidden;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed)
        {
            return;
        }
        HidePreview(); // 拖动起手先收预览：拖动中 ApplySize 的 Move 会与拖拽 Move 互抢（WM_DPICHANGED 同型）
        _dragging = true;
        _dragStartCursor = CursorPx();
        _dragOrigin = (_x, _y);
        Root.CapturePointer(e.Pointer);
    }

    /// <summary>悬停展开预览（BrimDeck 对照，2026-10-29 UI 升级）：入窗即展、出窗即收；
    /// 按住进入/拖动中/无数据不展——展开=窗变高，与拖动互抢必须挡。</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hovered = true;
        if (_dragging || e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed || _previewRows.Count == 0)
        {
            return;
        }
        ShowPreview();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hovered = false;
        HidePreview();
    }

    private void ShowPreview()
    {
        _morphGeneration++; // 作废未落定的收面回调（收起动效进行中又悬停回来）
        if (SourcePreview.Visibility == Visibility.Visible)
        {
            _motion.StopLoops(SourcePreview); // 淡出还没落定：复位后重新展开，否则从半透明继续
            if (_island)
            {
                _motion.MorphIn(SourcePreview);
            }

            return;
        }

        RebuildPreview();
        SourcePreview.Visibility = Visibility.Visible;
        if (_island)
        {
            IslandShadowStack.Visibility = Visibility.Collapsed; // 展开=岛变高，阴影随其口径退场（不跟窗底走）
        }
        ApplySize(); // 高随预览行数（岛=顶缘贴屏顶不动、向下长；经典=锚点角贴位）
        if (_island)
        {
            _motion.MorphIn(SourcePreview); // BrimDeck 口径展开动效（IslandMotion：360ms Bounce，off 档瞬时）；经典形态行为不动
        }
    }

    private void HidePreview()
    {
        if (SourcePreview.Visibility == Visibility.Collapsed)
        {
            return;
        }

        if (!_island || _dragging || _motion.Mode == MotionMode.Off)
        {
            CollapsePreview(); // 经典形态/拖动起手/动效关闭：立即收（拖动中收面尺寸与拖拽 Move 互抢必须瞬时）
            return;
        }

        var generation = ++_morphGeneration;
        _motion.MorphOut(SourcePreview, () =>
        {
            if (generation == _morphGeneration)
            {
                CollapsePreview(); // 内容淡出走完才收面（BrimDeck：内容先撤、曲面随后）
            }
        });
    }

    private void CollapsePreview()
    {
        if (SourcePreview.Visibility == Visibility.Collapsed)
        {
            return;
        }

        SourcePreview.Visibility = Visibility.Collapsed;
        _motion.StopLoops(SourcePreview); // 停掉残留淡出（Opacity/Scale 回基值，下次展开从 0 起）
        // 阴影层恒退场（2026-11-08 实机诊断：刘海贴顶平齐无落影带，见 ApplyIslandChrome 同口径）——
        // 不再随收回重新挂上（旧逻辑曾在此回场 Visible，与 ApplyIslandChrome 的挂载口径一致）。
        ApplySize();
    }

    /// <summary>预览行重建：级别点 + 来源名 + 最差态摘要（裁尾），上限 5 行。
    /// 行用 Grid 三列（点/来源 Auto、摘要 Star）：岛形态预览宽被 MaxWidth 钉在曲面内容宽内，
    /// 摘要列吃剩余宽度并裁尾——文本只会在岛内裁，不越出曲面缘（横向 StackPanel 会量到无限宽而溢出）。
    /// 经典形态预览宽=内容宽（星列退化为 Auto），摘要仍受 92 上限，观感同旧。</summary>
    private void RebuildPreview()
    {
        var panel = new Grid();
        var rows = _previewRows.Take(5).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var line = new Grid { Margin = i == rows.Count - 1 ? new Thickness(0) : new Thickness(0, 0, 0, 5) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // WinUI 3 无 GridLength.Star 静态成员（WPF 才有）：星列显式构造
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(_palette.SeverityColor(row.Worst)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            };
            var source = new TextBlock
            {
                Text = row.Source,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var summary = new TextBlock
            {
                Text = row.Summary,
                FontSize = 11,
                Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = _island ? double.PositiveInfinity : 92, // 岛=由星列按曲面内容宽裁尾；经典=旧 92 上限
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(dot, 0);
            Grid.SetColumn(source, 1);
            Grid.SetColumn(summary, 2);
            line.Children.Add(dot);
            line.Children.Add(source);
            line.Children.Add(summary);
            Grid.SetRow(line, i);
            panel.Children.Add(line);
        }

        SourcePreview.Child = panel; // Border 装饰件只有 Child（Content 是 ContentControl 面）
    }

    /// <summary>右键直达设置（左键拖动已在 Pressed 过滤非左键，互不干扰）。</summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        // 光标屏幕物理像素锚定：目标 = 按下窗口位 + 光标位移，与 AppWindow.Position 同一坐标系——
        // 跨缩放屏拖动不换算不混算（旧实现每帧重读 GetDpi 与按下时 DIP 空间相减，跨屏比例错乱）；
        // 高频事件下目标没变不重发 Move（SetWindowPos 抖动是跨屏闪烁放大器）
        var (cursorX, cursorY) = CursorPx();
        var x = _dragOrigin.X + cursorX - _dragStartCursor.X;
        // 岛=刘海贴顶（BrimDeck Notch 口径）：垂直分量锁死在按下时的屏顶位，只许水平拖（落点 X 由 Locate 只记水平分量）
        var y = _island ? _dragOrigin.Y : _dragOrigin.Y + cursorY - _dragStartCursor.Y;
        if (x == _x && y == _y)
        {
            return;
        }
        _x = x;
        _y = y;
        _appWindow.Move(new PointInt32(_x, _y));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        Root.ReleasePointerCapture(e.Pointer);
        // 净位移 < 6 dip 视为单击（不是拖动）→ 打开 L2（B-504）；按住抖动由净位移抵消
        var dragDips = (Math.Abs(_x - _dragOrigin.X) + Math.Abs(_y - _dragOrigin.Y)) / GetDpi();
        if (dragDips < 6)
        {
            OpenPanelRequested?.Invoke();
        }
        SavePosition();
        if (_island)
        {
            RestorePosition(); // 岛落点即贴回屏顶并夹回屏幕内（Y 恒 0、X 夹取都在 CapsuleIslandMath.Place）
        }

        if (_dpiChangedDuringDrag)
        {
            _dpiChangedDuringDrag = false;
            ApplySize(); // 拖动中跨了 DPI 的消息被跳过：落点后按新 DPI 补排尺寸
        }
    }

    private double GetDpi() => NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

    /// <summary>光标屏幕物理像素（GetCursorPos；与 AppWindow.Position/Move 同一坐标系）。</summary>
    private static (int X, int Y) CursorPx()
    {
        NativeMethods.GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    /// <summary>
    /// 宽随内容实测（RFC §6.2.2：常态 48–120 DIP；2026-10-29 UI 升级环+灯并置后极值文案
    /// 「1 ok · 1 err」实测放宽到 156，悬停预览/离线横幅同上限），高最小 32 DIP；
    /// AppWindow.Resize/Size 一律物理像素，XAML 布局是 DIP——统一 ×DPI，不乘会在高缩放下被裁成一条。
    /// </summary>
    private (int Width, int Height) MeasureSizePx()
    {
        Root.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = Root.DesiredSize;
        var dpi = GetDpi();
        if (_island)
        {
            // 岛（BrimDeck Notch 口径）：宽 = max(NotchWidthDefault 238, 收起行 + 2×NotchInset 30)，
            // 下限 NotchWidthMin 72、上限屏宽 - 16 DIP；展开不改宽（BrimDeck 面板宽随岛宽，只向下长），
            // 高 = max(BaseHeight 32, 内容实测)——收起恒 32，悬停预览把岛向下拉长。
            var rowDips = PillArea.DesiredSize.Width + CapsuleIslandMath.InsetDips * 2;
            var widthDips = Math.Clamp(Math.Ceiling(Math.Max(CapsuleIslandMath.DefaultWidthDips, rowDips)),
                CapsuleIslandMath.MinWidthDips, IslandMaxWidthDips(dpi));
            _islandWidthDips = widthDips;
            var heightDips = Math.Max(CapsuleIslandMath.HeightDips, Math.Ceiling(desired.Height));
            if (SourcePreview.Visibility != Visibility.Visible && OfflineBanner.Visibility != Visibility.Visible)
            {
                // 收起态：岛=贴顶平齐刘海（Dynamic Island 口径），窗高=曲面高 32 恒——无底部阴影带。
                // ① 刘海是贴顶的平齐黑药丸，不是带落影的浮块：底部阴影带把窗口读成矩形板
                //    （真机截图 slab：面下 8 DIP 全宽暗带 + 桌面剪影随高度变宽的实证）。
                // ② 确定性高度还消除「展开→收起后窗高卡在 69 而非 40」的实测残留（真机 trace
                //    root=238x69：刚收面的那一帧 Root.DesiredSize 仍是展开态残留，实测值不可信）。
                // 收起内容恒 ≤ 曲面高（PillArea.MinHeight=32 行容器内环/条/徽标都 ≤32），不会裁切；
                // 离线横幅在面下独立成块时随内容（横幅可见走上方实测分支）。
                heightDips = CapsuleIslandMath.HeightDips;
            }
            return (DpiLayoutMath.ToPx(widthDips, dpi), DpiLayoutMath.ToPx(heightDips, dpi));
        }

        // 经典胶囊：观感口径不动（48–156 DIP 宽随内容、最小 32 高）
        var classicWidth = Math.Clamp(Math.Ceiling(desired.Width), 48, 156);
        var classicHeight = Math.Max(32, Math.Ceiling(desired.Height));
        return (DpiLayoutMath.ToPx(classicWidth, dpi), DpiLayoutMath.ToPx(classicHeight, dpi));
    }

    /// <summary>岛宽上限 = 所在屏物理宽 - 16 DIP 边距（BrimDeck WidthRange 用屏宽收窄的同口径）；
    /// 取不到显示器时回退 1024 DIP（Place 的夹取仍会把窗放回屏内）。</summary>
    private double IslandMaxWidthDips(double dpi)
    {
        var monitor = _positioned
            ? MonitorService.FromPixel(_x + DpiLayoutMath.ToPx((int)_islandWidthDips, dpi) / 2, _y + 1)
            : MonitorService.Primary();
        var screen = monitor?.MonitorPx;
        if (screen is not { } rect || rect.Right - rect.Left <= 0)
        {
            return 1024;
        }

        var marginPx = DpiLayoutMath.ToPx(16, dpi);
        return Math.Max(CapsuleIslandMath.MinWidthDips, (rect.Right - rect.Left - marginPx) / dpi);
    }

    /// <summary>外框物理尺寸 = 客户区需求（内容实测）+ 非客户区差值。AppWindow.Resize 收的是外框，
    /// 直接喂客户区需求 → 底部 ~6px 不可见（无边框 presenter 仍保留 resize 边框），内容裁切
    /// （2026-10-14 进度条缺陷单同型根因）。_sizePx 与 RestorePosition/SavePosition 的锚点数学
    /// 同为外框口径，save/restore 往返不漂移。</summary>
    private (int Width, int Height) FrameSizePx()
    {
        var client = MeasureSizePx();
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
        return (client.Width + chromeWidth, client.Height + chromeHeight);
    }

    private void ApplySize()
    {
        if (_appWindow is null)
        {
            return;
        }
        _sizePx = FrameSizePx();
        // 岛：预览区钉死在曲面内容宽内（SurfaceStack 已两侧各内缩 30），行文本只在岛内裁尾不越缘；
        // 经典：恢复无上限（旧口径）。MaxWidth 只影响预览行布局，不影响岛宽（宽只量收起行）。
        SourcePreview.MaxWidth = _island
            ? Math.Max(0, _islandWidthDips - CapsuleIslandMath.InsetDips * 2)
            : double.PositiveInfinity;
        if (_sizePx.Width == _appWindow.Size.Width && _sizePx.Height == _appWindow.Size.Height)
        {
            return;
        }
        _appWindow.Resize(new SizeInt32(_sizePx.Width, _sizePx.Height));
        if (_positioned)
        {
            RestorePosition(); // 宽随内容变化后按锚点角重新贴位
        }
    }

    private void RestorePosition()
    {
        if (_island)
        {
            RestoreIslandPosition(); // 岛=顶中锚点独立持久化（与经典四角布局互不覆盖）
            return;
        }
        var layout = _shellState.LoadCapsuleLayout();
        var monitor = (layout is null ? null : MonitorService.FindByName(layout.Monitor)) ?? MonitorService.Primary();
        var work = monitor?.WorkPx ?? _defaultWorkAreaFallback;
        var dpi = GetDpi();
        // 用实测尺寸而非 _appWindow.Size：Resize 刚发出时 Size 可能还是旧值，会把定位算偏
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();

        if (layout is null)
        {
            // 默认：主屏工作区右下角（任务书 Level 1）
            _x = work.Right - widthPx - DpiLayoutMath.ToPx(24, dpi);
            _y = work.Bottom - heightPx - DpiLayoutMath.ToPx(96, dpi);
        }
        else
        {
            var offsetX = (int)Math.Round(layout.OffsetDips.X * dpi);
            var offsetY = (int)Math.Round(layout.OffsetDips.Y * dpi);
            _x = layout.Anchor is PinAnchor.TopLeft or PinAnchor.BottomLeft
                ? work.Left + offsetX
                : work.Right - widthPx - offsetX;
            _y = layout.Anchor is PinAnchor.TopLeft or PinAnchor.TopRight
                ? work.Top + offsetY
                : work.Bottom - heightPx - offsetY;
        }

        // 夹回工作区：锚点+偏移在换屏/改缩放后可能把窗口推出屏幕（与 PinnedHost 的 ClampInto 同理）
        _x = Math.Clamp(_x, work.Left, Math.Max(work.Left, work.Right - widthPx));
        _y = Math.Clamp(_y, work.Top, Math.Max(work.Top, work.Bottom - heightPx));

        _appWindow.Move(new PointInt32(_x, _y));
        _positioned = true;
    }

    /// <summary>岛形态恢复：显示器 + 顶中锚偏移（CapsuleIslandMath 纯函数，Core.Tests 锁口径）。
    /// 入参是**屏幕矩形**（非工作区——刘海越过任务栏贴屏幕顶，BrimDeck Notch surface.Y=0 同口径）；
    /// 布局缺失用主屏顶中默认位（DefaultOffset = 居中、Y 恒 0）。
    /// 外框顶 ≠ 可见曲面顶：无边框窗仍可能带隐藏非客户区，按实测「客户区顶-外框顶」补偿，
    /// 保证肉眼看到的曲面顶真的压在屏幕顶（真机三态证据里量的就是这条）。</summary>
    private void RestoreIslandPosition()
    {
        var layout = _shellState.LoadIslandLayout();
        var monitor = (layout is null ? null : MonitorService.FindByName(layout.Monitor)) ?? MonitorService.Primary();
        var screen = monitor is { } found ? found.MonitorPx : _defaultWorkAreaFallback;
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();
        var rect = CapsuleIslandMath.Place(
            ToPinRect(screen), layout?.OffsetDips ?? CapsuleIslandMath.DefaultOffset, GetDpi(), widthPx, heightPx);
        _x = rect.X;
        _y = rect.Y - ClientTopOffsetPx();
        _appWindow.Move(new PointInt32(_x, _y));
        _positioned = true;
    }

    /// <summary>可见客户区顶相对窗口外框顶的物理偏移（≥0）：曲面顶贴屏顶时要把窗口上提这一截。</summary>
    private int ClientTopOffsetPx()
    {
        if (!NativeMethods.GetWindowRect(_hwnd, out var window))
        {
            return 0;
        }

        var origin = new NativeMethods.POINT { X = 0, Y = 0 };
        return NativeMethods.ClientToScreen(_hwnd, out origin) ? Math.Max(0, origin.Y - window.Top) : 0;
    }

    /// <summary>Win32 RECT（Left/Top/Right/Bottom 物理像素）→ Core 布局矩形（X/Y/Width/Height）：
    /// CapsuleIslandMath 是纯 Core 域（PinLayoutMath 同口径不依赖 Win32），边界换算留在窗体侧。</summary>
    private static PinRect ToPinRect(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private void SavePosition()
    {
        if (_island)
        {
            SaveIslandPosition();
            return;
        }
        var dpi = GetDpi();
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();
        var monitor = MonitorService.FromPixel(_x + widthPx / 2, _y + heightPx / 2)
                      ?? MonitorService.Primary();
        if (monitor is null)
        {
            return;
        }
        var work = monitor.WorkPx;

        var anchorLeft = _x + widthPx / 2 < (work.Left + work.Right) / 2;
        var anchorTop = _y + heightPx / 2 < (work.Top + work.Bottom) / 2;
        var anchor = anchorLeft
            ? (anchorTop ? PinAnchor.TopLeft : PinAnchor.BottomLeft)
            : (anchorTop ? PinAnchor.TopRight : PinAnchor.BottomRight);

        var offsetX = Math.Max(0, (anchorLeft ? _x - work.Left : work.Right - _x - widthPx) / dpi);
        var offsetY = Math.Max(0, (anchorTop ? _y - work.Top : work.Bottom - _y - heightPx) / dpi);

        _shellState.SaveCapsuleLayout(new PinLayout
        {
            Monitor = monitor.DeviceName,
            Anchor = anchor,
            OffsetDips = new PinOffset(offsetX, offsetY),
            Collapsed = false,
        });
    }

    /// <summary>岛形态拖动落点持久化：显示器 + 顶中锚偏移（Locate 互逆换算），与经典胶囊布局分存互不覆盖。</summary>
    private void SaveIslandPosition()
    {
        var dpi = GetDpi();
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : FrameSizePx();
        var monitor = MonitorService.FromPixel(_x + widthPx / 2, _y + heightPx / 2)
                      ?? MonitorService.Primary();
        if (monitor is null)
        {
            return;
        }
        _shellState.SaveIslandLayout(new CapsuleIslandLayout
        {
            Monitor = monitor.DeviceName,
            // 屏幕矩形（非工作区）：贴顶岛的锚在屏顶，工作区会因任务栏把 X 居中算偏
            OffsetDips = CapsuleIslandMath.Locate(
                ToPinRect(monitor.MonitorPx), new PinRect(_x, _y, widthPx, heightPx), dpi),
        });
    }

    /// <summary>记录最近一次数据拉取时间（Offline 横幅的 Last update 来源，B-504）。</summary>
    public void NoteFetch(DateTimeOffset fetchedAt)
    {
        if (fetchedAt > _lastFetchedAt)
        {
            _lastFetchedAt = fetchedAt;
        }
    }

    /// <summary>config.showCapsule 开关（B-801）：启动门控 + 设置页实时显隐。</summary>
    public void SetVisible(bool visible)
    {
        _showAllowed = visible; // 先记意愿再动窗口（AppWindow 未就绪时哨兵也能拿到正确门控）
        if (_appWindow is null)
        {
            return; // Loaded 前无 AppWindow，启动门控走 App.xaml.cs 的 Activate 分支
        }
        if (visible)
        {
            if (!_ruleHidden)
            {
                _appWindow.Show(); // 全屏避让中：保持退场，等前台退出全屏由钩子回场
            }
        }
        else
        {
            _appWindow.Hide();
        }
    }

    /// <summary>config.uiOpacity（B-801 外观透明度）作用于胶囊整体。</summary>
    public void ApplyOpacity(double opacity)
        => ((FrameworkElement)Content).Opacity = Math.Clamp(opacity, 0.2, 1.0);

    /// <summary>悬浮岛形态开关（display-modes-analysis §7 Phase 2，config.capsuleIsland 默认关=现状）：
    /// 开=顶部居中锚定，收起态计数文本让位给「品牌标 + 未读/异常徽标」；关=经典胶囊逐字节不动。
    /// §1 只读契约不变：点击仍走 OpenPanelRequested（App.xaml.cs 接 quickPanel.Toggle，不新增岛内操作）。
    /// Activate 前调用时 _appWindow 未就绪——只翻标志与元素可见性，位置由 OnLoaded 的 RestorePosition
    /// 按 _island 分支落；运行中切换则即时重排。</summary>
    public void SetIslandMode(bool island)
    {
        if (_island == island)
        {
            return;
        }
        _island = island;
        if (!island && _ruleHidden)
        {
            _ruleHidden = false; // 避让只对岛形态生效：切回经典胶囊立即回场
            if (_showAllowed && _appWindow is not null)
            {
                _appWindow.Show();
            }
        }

        ApplyIslandChrome();
        CountText.Visibility = island ? Visibility.Collapsed : Visibility.Visible;
        BrandMark.Visibility = island ? Visibility.Visible : Visibility.Collapsed;
        RefreshBadge();
        if (_appWindow is not null)
        {
            ApplySize(); // 收起态内容宽变了（计数文本 ↔ 徽标+品牌标），重排后按锚点重贴
            RestorePosition();
        }
    }

    /// <summary>岛形态视觉重做（Dynamic Island 口径，2026-11-06 实机截图诊断驱动）：近实色黑药丸
    /// （DesktopAcrylic 在亮壁纸上把灰底稀释成「看不清」，恒黑底对比恒定）、发丝白描边只做形态分层、
    /// 健康环退场（整环读作 spinner 且与灯点双绿冗余——单灯点+晕扛状态）、字恒近白、间距放宽。
    /// 经典胶囊逐字节不动：首切岛前快照 XAML 原值，切回即还原。</summary>
    private void ApplyIslandChrome()
    {
        if (!_chromeCaptured)
        {
            _classicPillBackground = RootBorder.Background;
            _classicPillBorder = RootBorder.BorderBrush;
            _classicPadding = RootBorder.Padding;
            _classicRowSpacing = PillRow.Spacing;
            _classicBrandForeground = BrandMark.Foreground;
            _classicPreviewBackground = SourcePreview.Background;
            _classicPreviewBorder = SourcePreview.BorderBrush;
            _classicBorderThickness = RootBorder.BorderThickness;
            _classicCornerRadius = RootBorder.CornerRadius;
            _classicPreviewThickness = SourcePreview.BorderThickness;
            _classicStackMargin = SurfaceStack.Margin;      // 经典无内缩（岛 NotchInset 30 才有）
            _classicPillAlign = PillRow.HorizontalAlignment; // 经典 Stretch（岛才居中）——default() 会落 Left，必须快照
            _classicStackAlign = SurfaceStack.VerticalAlignment;
            _chromeCaptured = true;
        }
        if (_island)
        {
            // 底色交给曲面 Path（否则 Border 的矩形底会把肩部/底角的轮廓填平，岛退化成方块——
            // 真机实测：矩形底 + 同色曲面叠加后四边全直，肩部完全看不见）
            RootBorder.Background = null; // 无底色：曲面填充只由 IslandSurface 画（Background 可空，切回经典由快照还原）
            RootBorder.BorderBrush = new SolidColorBrush(ThemeColors.IslandHairline());
            // 药丸画法退场：曲面由 IslandSurface 单路径自绘（顶缘贴屏满宽 + 肩部 + 底角12），
            // 描边/圆角/内边距全从 Border 移走——Path 是 Border 的子元素，Border 若留圆角会把贴顶两角裁掉，
            // 若留内边距 Path 就铺不满曲面（肩部必须伸到窗口两角才读得出「刘海」）。
            RootBorder.BorderThickness = new Thickness(0);
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.Padding = new Thickness(0);
            SurfaceStack.Margin = new Thickness(CapsuleIslandMath.InsetDips, 0, CapsuleIslandMath.InsetDips, 0); // NotchInset 30
            PillArea.MinHeight = CapsuleIslandMath.HeightDips; // 收起行=曲面高 32，行内容垂直居中（BrimDeck 收起态行高口径）
            SurfaceStack.VerticalAlignment = VerticalAlignment.Top; // 窗口含底部 8 DIP 阴影带：内容行钉顶，与 y=0 起的曲面对齐
            PillRow.HorizontalAlignment = HorizontalAlignment.Center; // 刘海默认宽 238：内容居中才左右留白对称
            PillRow.Spacing = 8;
            HealthRing.Visibility = Visibility.Collapsed;
            OverallLightHost.Visibility = Visibility.Collapsed; // 岛收起态：环/条组件扛级别色，灯点双显冗余（ISLAND-RING-1）
            BrandMark.Foreground = new SolidColorBrush(ThemeColors.IslandText());
            IslandSurface.Visibility = Visibility.Visible;
            IslandSurface.Fill = new SolidColorBrush(ThemeColors.IslandPill());
            // 发丝内缘（BrimDeck 技法）：2px 描边 + UpdateSurfaceOutlineCore 里按曲面矩形裁掉外侧一半
            IslandSurface.Stroke = new SolidColorBrush(ThemeColors.IslandHairline());
            IslandSurface.StrokeThickness = 2;
            // 软阴影层退场（2026-11-08 实机诊断）：刘海=贴顶平齐（Dynamic Island 口径），窗高恒曲面高，
            // 无底部阴影带——旧的两层不透明剪影（Fill=IslandPill + 粗描边 6/14 + 下沉 2/3）在面下叠出
            // 全宽暗带，把岛读成矩形板（真机截图 slab + 桌面剪影随高度变宽实证）。剪影层随窗口一并裁掉，
            // 保留 XAML 节点与 ApplyShadowGeometry 骨架（经典形态切回仍走既有 Collapsed 早退口径）。
            SourcePreview.Background = null; // 预览在曲面内无缝长出（同底色/同描边都多余——展开读作岛变高）
            SourcePreview.BorderThickness = new Thickness(0);
            UpdateSurfaceOutline();
        }
        else
        {
            RootBorder.Background = _classicPillBackground;
            RootBorder.BorderBrush = _classicPillBorder;
            RootBorder.Padding = _classicPadding;
            RootBorder.BorderThickness = _classicBorderThickness;
            RootBorder.CornerRadius = _classicCornerRadius;
            SurfaceStack.Margin = _classicStackMargin;
            PillArea.MinHeight = 0;
            PillRow.HorizontalAlignment = _classicPillAlign;
            SurfaceStack.VerticalAlignment = _classicStackAlign;
            PillRow.Spacing = _classicRowSpacing;
            HealthRing.Visibility = Visibility.Visible;
            OverallLightHost.Visibility = Visibility.Visible; // 经典胶囊灯点照旧（G4/G5 像素锚不动）
            HealthRing.Content = StatusRing.Build(16, 2.5, _ringRatio, _ringColor); // 岛期间环停画，切回补最近份额
            RefreshBadge(); // 岛进度面随形态退场（经典无环/条徽标，Content 清空）
            BrandMark.Foreground = _classicBrandForeground;
            IslandSurface.Visibility = Visibility.Collapsed;
            IslandSurface.Data = null;
            IslandShadowStack.Visibility = Visibility.Collapsed; // 经典无阴影带，剪影层一并退场
            IslandShadowNear.Data = IslandShadowFar.Data = null;
            SourcePreview.Background = _classicPreviewBackground;
            SourcePreview.BorderBrush = _classicPreviewBorder;
            SourcePreview.BorderThickness = _classicPreviewThickness;
        }
    }

    /// <summary>按曲面实际尺寸重建岛轮廓（IslandSurfaceOutline → PathGeometry，BrimDeck BuildOutline 同式控制点）。
    /// RootBorder.SizeChanged 每次触发（展开/切形态/换 DPI），经典形态与未就绪早退。</summary>
    private void UpdateSurfaceOutline()
    {
        try
        {
            UpdateSurfaceOutlineCore();
        }
        catch (Exception ex)
        {
            IslandTrace($"UpdateSurfaceOutline FAILED\n{ex}");
            throw;
        }
    }

    private void UpdateSurfaceOutlineCore()
    {
        if (!_island || IslandSurface.Visibility != Visibility.Visible)
        {
            return;
        }

        var width = RootBorder.ActualWidth;
        // 曲面高：收起态恒 32（窗口比曲面多出的 8 DIP 是阴影带，不进轮廓）；展开态铺满整窗（岛变高）
        var height = SourcePreview.Visibility == Visibility.Visible
            ? RootBorder.ActualHeight
            : CapsuleIslandMath.HeightDips;
        if (!(width > 0) || !(height > 0))
        {
            return;
        }

        PathGeometry BuildOutlineGeometry()
        {
            // WinUI 3 的同一 Geometry 实例不能挂多个 Path.Data（抛「值不在预期范围」）——
            // 曲面与两层阴影剪影各自新建一份（控制点序列相同、实例独立）
            var figure = new PathFigure
            {
                StartPoint = new global::Windows.Foundation.Point(0, 0),
                IsClosed = true,
                IsFilled = true,
            };
            foreach (var seg in IslandSurfaceOutline.Build(width, height, CapsuleIslandMath.ShoulderDips, CapsuleIslandMath.BottomRadiusDips))
            {
                switch (seg)
                {
                    case IslandSurfaceOutline.Move:
                        break; // 首点已作 StartPoint
                    case IslandSurfaceOutline.Line line:
                        figure.Segments.Add(new LineSegment { Point = ToPoint(line.To) });
                        break;
                    case IslandSurfaceOutline.Cubic cubic:
                        figure.Segments.Add(new BezierSegment
                        {
                            Point1 = ToPoint(cubic.C1),
                            Point2 = ToPoint(cubic.C2),
                            Point3 = ToPoint(cubic.To),
                        });
                        break;
                }
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        var outlineGeometry = BuildOutlineGeometry();
        IslandSurface.Data = outlineGeometry;
        // 发丝内缘收口：按曲面矩形裁掉 2px 描边的外侧一半（BrimDeck「画大一半、裁半」技法）。
        // 展开态同样裁——预览长出后轮廓随窗高，描边仍只留内侧
        IslandSurface.Clip = new RectangleGeometry { Rect = new global::Windows.Foundation.Rect(0, 0, width, height) };
        ApplyShadowGeometry(BuildOutlineGeometry(), BuildOutlineGeometry(), width, height);
        IslandTrace($"outline {width}x{height} segs={outlineGeometry.Figures[0].Segments.Count} " +
            $"row={PillRow.ActualWidth}x{PillRow.ActualHeight} " +
            $"pill={PillArea.ActualWidth}x{PillArea.ActualHeight} " +
            $"stack={SurfaceStack.ActualWidth} margin={SurfaceStack.Margin} " +
            $"brand={BrandMark.Visibility}/{BrandMark.ActualWidth} " +
            $"count={CountText.Visibility} progress={IslandProgress.Visibility}/{IslandProgress.ActualWidth} " +
            $"align={PillRow.HorizontalAlignment} root={Root.ActualWidth}x{Root.ActualHeight}");

        static global::Windows.Foundation.Point ToPoint(IslandSurfaceOutline.Point p) => new(p.X, p.Y);
    }

    /// <summary>岛软阴影（BrimDeck 技法的原生实现）：两层同轮廓剪影，逐层加粗描边模拟模糊半径、
    /// 下沉模拟投影深度。2026-11-08 实机诊断后退休：不透明剪影 + 下沉在面下叠出全宽暗带（slab），
    /// 且收起态窗高改恒 32（无落影带）后剪影层被窗口裁掉——整层恒 Collapsed，此处早退不画。
    /// 保留骨架供经典形态/未来换用真模糊时挂回，现态不参与渲染。</summary>
    private void ApplyShadowGeometry(PathGeometry nearGeometry, PathGeometry farGeometry, double width, double height)
    {
        if (IslandShadowStack.Visibility != Visibility.Visible)
        {
            return;
        }
        IslandShadowNear.Data = nearGeometry;
        IslandShadowFar.Data = farGeometry;
        IslandShadowNear.StrokeThickness = 6;
        IslandShadowFar.StrokeThickness = 14;
        IslandShadowNear.RenderTransform = new TranslateTransform { Y = 2 };
        IslandShadowFar.RenderTransform = new TranslateTransform { Y = 3 };
    }

    // —— 全屏避让（USER-RPT-4，BrimDeck ScreenContext 全屏规则的最小实现面）——

    /// <summary>挂前台事件钩子（out-of-context，随本线程消息泵派发，同 TopmostGuard 口径）：
    /// 避让与哨兵必须同源触发，两边都读 IsGuardVisible 才不会出现「哨兵先 Show、避让后 Hide」的闪现残留。</summary>
    private void HookFullscreenAvoidance()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            return;
        }

        _foregroundCallback = OnForegroundChanged;
        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _foregroundCallback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void OnForegroundChanged(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0)
        {
            return; // 只要 OBJID_WINDOW（0）的窗级事件
        }

        EvaluateFullscreenAvoidance();
    }

    /// <summary>评估全屏避让：前台出现「无标题栏且外框盖满岛所在屏」的窗 → 岛退场；退出全屏 → 回场。
    /// BrimDeck 默认 Borderless/Exclusive → Hide，Maximized → 降级 Line（我方无 Line 形态，差异表记为已知差距，
    /// 本实现不处理最大化窗）。只对岛形态生效（经典胶囊是历史口径，行为不动）。</summary>
    private void EvaluateFullscreenAvoidance()
    {
        if (_appWindow is null)
        {
            return;
        }

        var hide = _island && _showAllowed && ForegroundCoversIslandMonitor();
        if (hide == _ruleHidden)
        {
            return;
        }

        _ruleHidden = hide;
        if (!_showAllowed)
        {
            return; // 用户本就关着岛：只记状态，等 SetVisible(true) 时按 _ruleHidden 决定是否 Show
        }

        if (hide)
        {
            _appWindow.Hide();
        }
        else
        {
            _appWindow.Show();
        }
    }

    /// <summary>前台窗是否=盖满岛所在屏的无边框全屏窗（外框含任务栏，2px 容差；
    /// 有 WS_CAPTION 的是窗口化/最大化窗，不在本避让面）。</summary>
    private bool ForegroundCoversIslandMonitor()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == _hwnd || !NativeMethods.IsWindowVisible(foreground))
        {
            return false;
        }

        var monitor = _positioned
            ? MonitorService.FromPixel(_x + _sizePx.Width / 2, _y + _sizePx.Height / 2)
            : MonitorService.Primary();
        monitor ??= MonitorService.Primary();
        if (monitor is null || !NativeMethods.GetWindowRect(foreground, out var rect))
        {
            return false;
        }

        const int tolerance = 2;
        var screen = monitor.MonitorPx;
        var covers = rect.Left <= screen.Left + tolerance && rect.Top <= screen.Top + tolerance
                     && rect.Right >= screen.Right - tolerance && rect.Bottom >= screen.Bottom - tolerance;
        if (!covers)
        {
            return false;
        }

        var style = NativeMethods.GetWindowLongPtr(foreground, NativeMethods.GWL_STYLE).ToInt64();
        return (style & NativeMethods.WS_CAPTION) == 0;
    }

    /// <summary>未读徽标喂数（App 订阅 NotificationRaised 转发 runtime.Notifications.UnreadCount）：
    /// 岛收起态徽标=未读优先、异常次之，都为零隐藏。经典形态只存值不渲染。</summary>
    public void UpdateBadge(int unread)
    {
        _unreadCount = Math.Max(0, unread);
        RefreshBadge();
    }

    /// <summary>岛进度徽标重画（ISLAND-RING-1，2026-11-07 用户令）：进度环/进度条是组件中心里的两个
    /// 组件，岛显示什么完全由勾选状态决定——勾环显环、勾条显条、都勾都显、都不勾不显（无「默认样式」
    /// 概念）。环=底环+进度弧+中心数字（数字=未读优先、异常次之，全零空中心——环本身扛进度/级别色）；
    /// 条=轨道+左锚填充（仅条在场时计数跟条走）。经典形态恒隐藏。每次重画整树重建（同 StatusRing 口径）。</summary>
    private void RefreshBadge()
    {
        if (!_island)
        {
            IslandProgress.Visibility = Visibility.Collapsed;
            IslandProgress.Content = null;
            return;
        }
        var ring = Beacon.Core.Services.ComponentCenterCatalog.IslandRingVisible(_config.App);
        var bar = Beacon.Core.Services.ComponentCenterCatalog.IslandBarVisible(_config.App);
        var arcColor = _ringColor;
        var trackColor = ThemeColors.Halo(arcColor, 80); // 底环=进度色低透明（StatusRing 35% 同族，暗底更含蓄）
        var countText = _unreadCount > 0 ? _unreadCount.ToString()
            : _abnormalCount > 0 ? _abnormalCount.ToString()
            : string.Empty; // 全零：中心不落字，环/条自己讲进度
        FrameworkElement? content = (ring, bar) switch
        {
            (true, true) => BothProgressFaces(_ringRatio, arcColor, trackColor, countText),
            (true, false) => Components.RingProgressBadge.Build(IslandRingSize, IslandRingThickness, _ringRatio,
                arcColor, trackColor, countText, ThemeColors.IslandText()),
            (false, true) => BarOnlyFace(_ringRatio, arcColor, trackColor, countText),
            (false, false) => null, // 都不勾：岛收起态只剩品牌标（组件中心说了算，无兜底无默认）
        };
        IslandProgress.Content = content;
        IslandProgress.Visibility = content is null ? Visibility.Collapsed : Visibility.Visible;
        ApplySize(); // 勾选/计数变化改变收起行宽（238 下限内通常不动，低于下限的窄岛需要重排）
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
