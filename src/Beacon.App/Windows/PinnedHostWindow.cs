using System.Runtime.InteropServices;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>
/// L0 悬浮 tile（B-702，RFC §6.2.1）：StatusLight + 标签 + 数字/摘要，32 DIP 高。
/// 只渲染不交互（点击落宿主的 HTCLIENT），状态变化只更新自身视觉（B-701 独立更新）。
/// </summary>
internal sealed class PinTile : Border
{
    private const int TileHeight = 32;

    private readonly Ellipse _light = new()
    {
        Width = 8,
        Height = 8,
        Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)),
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _label = new()
    {
        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 226, 232, 240)),
        FontSize = 12,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterTrim,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _value = new()
    {
        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
        FontSize = 11,
        TextTrimming = TextTrimming.CharacterTrim,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right,
    };

    public string WidgetId { get; }

    public PinTile(WidgetConfig widget)
    {
        WidgetId = widget.Id;
        Height = TileHeight;
        Padding = new Thickness(10, 0, 10, 0);
        Background = new SolidColorBrush(SeverityPalette.Rgb(31, 17, 24, 32));
        _label.Text = LabelOf(widget);

        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_light, 0);
        Grid.SetColumn(_label, 1);
        Grid.SetColumn(_value, 2);
        grid.Children.Add(_light);
        grid.Children.Add(_label);
        grid.Children.Add(_value);
        Child = grid;
    }

    /// <summary>状态变化只更新本 tile（B-701：两 tile 独立更新）。</summary>
    public void Update(WidgetState state)
    {
        _light.Fill = new SolidColorBrush(SeverityPalette.Color(state.Severity));
        _value.Text = ValueOf(state);
        ToolTipService.SetToolTip(this, state.IsStale ? $"Last update {state.FetchedAt.ToLocalTime():HH:mm:ss}" : null);
    }

    private static string LabelOf(WidgetConfig widget)
        => widget.Config.TryGetValue("repo", out var repo) ? repo : widget.Type.Split('.')[^1];

    private static string ValueOf(WidgetState state)
    {
        if (state.Payload.TryGetValue("open_count", out var open))
        {
            return $"{open} open";
        }
        if (state.IsStale)
        {
            return "stale";
        }
        return state.Summary.Length > 22 ? state.Summary[..22] : state.Summary;
    }
}

/// <summary>
/// L0 单窗口多 tile 宿主（B-701，RFC §6.2.4）：每显示器一个紧凑面板（多显示器分配随 B-703 位置持久化）。
/// 置顶 + WS_EX_NOACTIVATE（点击不打断当前焦点）+ WS_EX_TOOLWINDOW（不进任务栏/Alt-Tab）；
/// 窗口子类化 WM_NCHITTEST：tile 行内 HTCLIENT，其余（边角缝隙）HTTRANSPARENT——空白点击落到桌面。
/// 状态变化经事件路由到对应 PinTile，只更新该 tile 视觉。
/// </summary>
internal sealed class PinnedHostWindow
{
    private const int PanelWidthDips = 168;

    private readonly BeaconRuntime _runtime;
    private readonly MonitorInfo _monitor;
    private readonly Dictionary<string, PinTile> _tiles = [];
    private readonly Dictionary<string, WidgetState> _latest = [];
    private readonly List<IDisposable> _subscriptions = [];

    private Window _window = null!;
    private StackPanel _tilePanel = null!;
    private IntPtr _hwnd;
    private NativeMethods.WndProcDelegate? _wndProc; // 字段持有防 GC 回收子类化回调
    private IntPtr _prevWndProc;

    public PinnedHostWindow(BeaconRuntime runtime, MonitorInfo monitor)
    {
        _runtime = runtime;
        _monitor = monitor;
    }

    public void Initialize()
    {
        _window = new Window { Title = "Beacon Pinned" };
        _tilePanel = new StackPanel { Orientation = Orientation.Vertical };
        _window.Content = _tilePanel;
        ((FrameworkElement)_window.Content).Loaded += OnLoaded;
        _window.Activate();

        _subscriptions.Add(_runtime.Bus.Subscribe<WidgetStateChanged>(evt =>
        {
            _latest[evt.State.WidgetId] = evt.State;
            UpdateTile(evt.State);
        }));

        ReloadTiles();
    }

    /// <summary>按 widgets.json 的 Pinned + PinSupported 重建 tile 集（B-702 入口一致性：右键/设置开关后调用）。</summary>
    public void ReloadTiles()
    {
        _tilePanel.Children.Clear();
        _tiles.Clear();
        foreach (var widget in _runtime.Config.Widgets)
        {
            if (!widget.Pinned || !IsPinSupported(widget.Type))
            {
                continue;
            }
            var tile = new PinTile(widget);
            _tiles[widget.Id] = tile;
            _tilePanel.Children.Add(tile);
        }
        // 重放最近状态，tile 不空等下一轮刷新
        foreach (var tile in _tiles.Values)
        {
            if (_latest.TryGetValue(tile.WidgetId, out var state))
            {
                tile.Update(state);
            }
        }
        ResizeToContent();
    }

    private bool IsPinSupported(string widgetType)
        => _runtime.Resolver.Resolve(widgetType)?.Descriptor.PinSupported == true;

    private void UpdateTile(WidgetState state)
    {
        if (_tiles.TryGetValue(state.WidgetId, out var tile))
        {
            tile.Update(state);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
        appWindow.IsShownInSwitchers = false;
        NativeMethods.AddWindowExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        // 宿主只占 tile 面板大小，钉在显示器工作区右上角（拖动/吸边在 B-703）
        ResizeToContent();
        var x = _monitor.WorkPx.Right - appWindow.Size.Width - (int)(24 * GetDpi());
        var y = _monitor.WorkPx.Top + (int)(96 * GetDpi());
        appWindow.Move(new PointInt32(x, y));

        SubclassForHitTest();
    }

    private void SubclassForHitTest()
    {
        _wndProc = WndProc;
        _prevWndProc = NativeMethods.SetWindowLongPtr(
            _hwnd, NativeMethods.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_NCHITTEST)
        {
            // tile 行内可交互；面板其余空白穿透到桌面（B-701 验收）
            var point = new NativeMethods.POINT { X = unchecked((short)(long)lParam), Y = unchecked((short)((long)lParam >> 16)) };
            if (NativeMethods.ScreenToClient(hWnd, ref point))
            {
                var dpi = GetDpi();
                var insideTile = point.Y >= 0 && point.Y < _tiles.Count * (int)(32 * dpi)
                    && point.X >= 0 && point.X < (int)(PanelWidthDips * dpi);
                return insideTile ? new IntPtr(NativeMethods.HTCLIENT) : new IntPtr(NativeMethods.HTTRANSPARENT);
            }
        }
        return _prevWndProc == IntPtr.Zero
            ? NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam)
            : NativeMethods.CallWindowProcW(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    private void ResizeToContent()
    {
        var appWindow = GetAppWindow();
        if (appWindow is null)
        {
            return;
        }
        var dpi = GetDpi();
        var width = (int)(PanelWidthDips * dpi);
        var height = Math.Max(1, _tiles.Count) * (int)(32 * dpi);
        appWindow.Resize(new SizeInt32(width, height));
    }

    private AppWindow? GetAppWindow()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return null;
        }
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        return windowId == default ? null : AppWindow.GetFromWindowId(windowId);
    }

    private double GetDpi() => _hwnd == IntPtr.Zero ? 1.0 : NativeMethods.GetDpiForWindow(_hwnd) / 96.0;
}
