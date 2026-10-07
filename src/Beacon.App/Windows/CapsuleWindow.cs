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
    private readonly NativeMethods.RECT _defaultWorkAreaFallback = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

    private IntPtr _hwnd;
    private AppWindow _appWindow = null!;
    private bool _dragging;
    private global::Windows.Foundation.Point _dragStart;
    private double _dragDistance;
    private int _x;
    private int _y;
    private DateTimeOffset _lastFetchedAt;

    /// <summary>无拖动的单击 → 打开 L2 Quick Panel（B-504）。</summary>
    public event Action? OpenPanelRequested;

    public CapsuleWindow(ShellStateStore shellState)
    {
        InitializeComponent();
        _shellState = shellState;
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

        _appWindow.Resize(new SizeInt32(180, 46));
        RestorePosition();
    }

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

    private void RestorePosition()
    {
        var layout = _shellState.LoadCapsuleLayout();
        var monitor = (layout is null ? null : MonitorService.FindByName(layout.Monitor)) ?? MonitorService.Primary();
        var work = monitor?.WorkPx ?? _defaultWorkAreaFallback;
        var dpi = GetDpi();
        var widthPx = Math.Max(1, _appWindow.Size.Width);
        var heightPx = Math.Max(1, _appWindow.Size.Height);

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

        _appWindow.Move(new PointInt32(_x, _y));
    }

    private void SavePosition()
    {
        var dpi = GetDpi();
        var monitor = MonitorService.FromPixel(_x + _appWindow.Size.Width / 2, _y + _appWindow.Size.Height / 2)
                      ?? MonitorService.Primary();
        if (monitor is null)
        {
            return;
        }
        var work = monitor.WorkPx;
        var widthPx = Math.Max(1, _appWindow.Size.Width);
        var heightPx = Math.Max(1, _appWindow.Size.Height);

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

    /// <summary>接入真实聚合（B-504）：分级计数 + Offline 横幅。</summary>
    public void UpdateStatus(AggregateStatusChanged status)
    {
        var ok = status.Counts.GetValueOrDefault(Severity.Success) + status.Counts.GetValueOrDefault(Severity.Info);
        var warn = status.Counts.GetValueOrDefault(Severity.Warning);
        var error = status.Counts.GetValueOrDefault(Severity.Error) + status.Counts.GetValueOrDefault(Severity.Critical);
        CountText.Text = $"{ok} · {warn} · {error}";

        OverallLight.Fill = status.Overall switch
        {
            Severity.Success => new SolidColorBrush(Microsoft.UI.Colors.LimeGreen),
            Severity.Info => new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            Severity.Warning => new SolidColorBrush(Microsoft.UI.Colors.Gold),
            Severity.Error => new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
            Severity.Critical => new SolidColorBrush(Microsoft.UI.Colors.Red),
            _ => new SolidColorBrush(Microsoft.UI.Colors.Gray),
        };

        var offline = status.OfflineConnections > 0;
        OfflineBanner.Visibility = offline ? Visibility.Visible : Visibility.Collapsed;
        if (offline)
        {
            var since = _lastFetchedAt == default ? status.Timestamp : _lastFetchedAt;
            OfflineText.Text = $"Offline · Last update {since:HH:mm:ss}";
        }
        // 横幅显隐改变内容高度（初始渲染时 _appWindow 尚未就绪则跳过）
        if (_appWindow is not null)
        {
            _appWindow.Resize(new SizeInt32(180, offline ? 70 : 46));
        }
    }
}
