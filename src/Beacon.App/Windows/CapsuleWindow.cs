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

    /// <summary>岛真实组件面单击（ISLAND-D4，2026-11-09 用户令）：携带真实 WidgetId（虚拟环/条面
    /// 不走此事件——保持整岛开板口径）。App 侧做看板定位/处置直达。</summary>
    public event Action<string>? IslandWidgetActivated;

    /// <summary>岛收起态展示计划（ISLAND-D1..D3）：App 按 IslandDisplayPlan.Compute 推送，本窗消费
    /// 渲染；null=未喂数（启动段/经典形态），RefreshBadge 回退既有环/条双面逻辑兜底。</summary>
    private IslandPlan? _islandPlan;

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

}
