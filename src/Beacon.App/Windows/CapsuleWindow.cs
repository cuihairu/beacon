using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
    private readonly UiPalette _palette;
    private readonly NativeMethods.RECT _defaultWorkAreaFallback = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

    private IntPtr _hwnd;
    private AppWindow _appWindow = null!;
    private (int Width, int Height) _sizePx; // 最近一次按内容实测的物理尺寸（定位与落盘共用，避免读旧值）
    private bool _positioned; // RestorePosition 已跑过（此后 ApplySize 变尺寸需重新贴锚点）
    private bool _startVisible = true; // config.showCapsule=false 时启动即隐藏（B-801）
    private bool _showAllowed = true; // 用户显隐意愿（TopmostGuard 据此放行）
    private bool _dragging;
    private global::Windows.Foundation.Point _dragStart;
    private double _dragDistance;
    private int _x;
    private int _y;
    private DateTimeOffset _lastFetchedAt;

    /// <summary>无拖动的单击 → 打开 L2 Quick Panel（B-504）。</summary>
    public event Action? OpenPanelRequested;

    public CapsuleWindow(ShellStateStore shellState, Beacon.Storage.JsonConfigurationStore config)
    {
        InitializeComponent();
        // RFC §6.2.2：亚克力/半透明背景（桌面悬浮物贴壁纸，Mica 会失去悬浮感，故用系统亚克力）
        SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        _shellState = shellState;
        _palette = new UiPalette(config); // B-706：聚合灯随 appearance.SeverityColors
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
        // 常驻悬浮物：不进任务栏/Alt-Tab、点击不抢焦点（RFC §6.2.7）
        NativeMethods.AddWindowExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        ApplySize(); // 按内容实测定尺寸（DIP×DPI 转物理像素，宽随内容=§6.2.2）
        RestorePosition();
        if (!_startVisible)
        {
            _appWindow.Hide(); // showCapsule=false：注册(hwnd/AppWindow 就绪)但不显示
        }
    }

    /// <summary>启动前声明「隐藏启动」（B-801 showCapsule=false；避免先显示后隐藏的闪烁）。</summary>
    public void StartHidden()
    {
        _startVisible = false;
        _showAllowed = false;
    }

    /// <summary>TopmostGuard 用：消息窗句柄与用户显隐意愿。</summary>
    public IntPtr Hwnd => _hwnd;

    public bool IsUserVisible => _showAllowed;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragging = true;
        _dragDistance = 0;
        _dragStart = e.GetCurrentPoint(Root).Position;
        Root.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        var position = e.GetCurrentPoint(Root).Position;
        _dragDistance += Math.Abs(position.X - _dragStart.X) + Math.Abs(position.Y - _dragStart.Y);
        var dpi = GetDpi();
        _x += (int)Math.Round((position.X - _dragStart.X) * dpi);
        _y += (int)Math.Round((position.Y - _dragStart.Y) * dpi);
        _dragStart = position;
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
        // 位移 < 6 dip 视为单击（不是拖动）→ 打开 L2（B-504）
        if (_dragDistance < 6)
        {
            OpenPanelRequested?.Invoke();
        }
        SavePosition();
    }

    private double GetDpi() => NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

    /// <summary>
    /// 宽随内容实测（RFC §6.2.2：常态 48–120 DIP，离线横幅放宽到 132），高最小 32 DIP；
    /// AppWindow.Resize/Size 一律物理像素，XAML 布局是 DIP——统一 ×DPI，不乘会在高缩放下被裁成一条。
    /// </summary>
    private (int Width, int Height) MeasureSizePx()
    {
        Root.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = Root.DesiredSize;
        var dpi = GetDpi();
        var widthDips = Math.Clamp(Math.Ceiling(desired.Width), 48, 132);
        var heightDips = Math.Max(32, Math.Ceiling(desired.Height));
        return ((int)Math.Round(widthDips * dpi), (int)Math.Round(heightDips * dpi));
    }

    private void ApplySize()
    {
        if (_appWindow is null)
        {
            return;
        }
        _sizePx = MeasureSizePx();
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
        var layout = _shellState.LoadCapsuleLayout();
        var monitor = (layout is null ? null : MonitorService.FindByName(layout.Monitor)) ?? MonitorService.Primary();
        var work = monitor?.WorkPx ?? _defaultWorkAreaFallback;
        var dpi = GetDpi();
        // 用实测尺寸而非 _appWindow.Size：Resize 刚发出时 Size 可能还是旧值，会把定位算偏
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : MeasureSizePx();

        if (layout is null)
        {
            // 默认：主屏工作区右下角（任务书 Level 1）
            _x = work.Right - widthPx - (int)(24 * dpi);
            _y = work.Bottom - heightPx - (int)(96 * dpi);
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

    private void SavePosition()
    {
        var dpi = GetDpi();
        var (widthPx, heightPx) = _sizePx.Width > 0 ? _sizePx : MeasureSizePx();
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
            _appWindow.Show();
        }
        else
        {
            _appWindow.Hide();
        }
    }

    /// <summary>config.uiOpacity（B-801 外观透明度）作用于胶囊整体。</summary>
    public void ApplyOpacity(double opacity)
        => ((FrameworkElement)Content).Opacity = Math.Clamp(opacity, 0.2, 1.0);

    /// <summary>接入真实聚合（B-504）：极简计数（零值组不显示，§6.2.2）+ 离线横幅（§6.2.6）。</summary>
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

        OverallLight.Fill = new SolidColorBrush(_palette.SeverityColor(status.Overall));

        var offline = status.OfflineConnections > 0;
        OfflineBanner.Visibility = offline ? Visibility.Visible : Visibility.Collapsed;
        if (offline)
        {
            var since = _lastFetchedAt == default ? status.Timestamp : _lastFetchedAt;
            OfflineText.Text = $"Last update {since:HH:mm}";
        }
        // 横幅/文案变化会改变内容尺寸（_appWindow 未就绪则 OnLoaded 的 ApplySize 兜底）
        ApplySize();
    }
}
