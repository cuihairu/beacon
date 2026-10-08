using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>
/// 独立悬浮框形态（app.pinDisplayMode = "floating"）：每个钉选组件一窗——
/// 置顶 + WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW，整窗即 tile（32 DIP 高），可拖到桌面任意位置。
/// 位置按组件各记（PinLayout.FloatingX/Y 物理像素，pins.json 权威承载；未拖过按序级联落位）；
/// 点击下钻 L2，拖动与单宿主同款死区/换算口径。与单宿主形态互斥，设置「悬浮形态」切换（重启生效）。
/// </summary>
internal sealed class FloatingTileHost
{
    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly MotionEngine _motion;
    private readonly MonitorInfo _fallbackMonitor;
    private readonly INotificationSink? _sink;
    private readonly Dictionary<string, FloatingTileWindow> _windows = [];
    private readonly Dictionary<string, WidgetState> _latest = [];
    private readonly List<IDisposable> _subscriptions = [];
    private bool _migrationNoticeShown;

    /// <summary>tile 点击：下钻 L2（RFC §6.2.7，与单宿主同义）。</summary>
    public event Action? TileActivated;

    public FloatingTileHost(BeaconRuntime runtime, MonitorInfo fallbackMonitor, INotificationSink? sink = null)
    {
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config);
        _motion = new MotionEngine(runtime.Config);
        _fallbackMonitor = fallbackMonitor;
        _sink = sink;
    }

    /// <summary>悬浮形态准入（产品拍板）：钉选准入之外还须信息密度够（FloatingSupported）——数值/额度类只在宿主面板。</summary>
    /// <summary>悬浮形态准入：信息密集组件（FloatingSupported，趋势图/灯组）恒可悬浮；
    /// 数值/额度类（FloatingOptIn）由设置「数量悬浮窗」开关放行，默认关=桌面零残留（拍板 2026-10-08）。</summary>
    public bool IsFloatingEligible(string widgetType)
    {
        var descriptor = _runtime.Resolver.Resolve(widgetType)?.Descriptor;
        if (descriptor is not { PinSupported: true })
        {
            return false;
        }
        if (descriptor.FloatingSupported)
        {
            return true;
        }
        return descriptor.FloatingOptIn && _runtime.Config.App.NumericFloatingEnabled;
    }

    /// <summary>TopmostGuard 用：全部悬浮窗句柄（Loaded 前该窗句柄为 Zero，哨兵跳过）。</summary>
    public IReadOnlyList<IntPtr> Hwnds => _windows.Values
        .Where(w => w.Hwnd != IntPtr.Zero)
        .Select(w => w.Hwnd)
        .ToList();

    public void Initialize()
    {
        // EventBus.Publish 在刷新线程回调——UI 对象（FloatingTileWindow.Update）必须回 UI 线程碰
        // （Initialize 在 UI 线程被调：ReloadTiles 建 Window 本就要求 UI 线程）
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("FloatingTileHost.Initialize 必须在 UI 线程调用");
        _subscriptions.Add(_runtime.Bus.Subscribe<WidgetStateChanged>(evt =>
            dispatcherQueue.TryEnqueue(() =>
            {
                _latest[evt.State.WidgetId] = evt.State;
                if (_windows.TryGetValue(evt.State.WidgetId, out var window))
                {
                    window.Update(evt.State);
                }
            })));
        ReloadTiles();
    }

    /// <summary>关闭全部悬浮窗（退出清理用）。</summary>
    public void Close()
    {
        foreach (var window in _windows.Values)
        {
            window.Close();
        }
        _windows.Clear();
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }

    /// <summary>按 widgets.json 的 Pinned + PinSupported 重建窗集（Pin/Unpin/停用后调用，位置记忆不丢）。</summary>
    public void ReloadTiles()
    {
        var wanted = new List<WidgetConfig>();
        var migrated = 0;
        foreach (var widget in _runtime.Config.Widgets)
        {
            if (!widget.Pinned || !IsPinSupported(widget.Type))
            {
                continue;
            }
            // 模块停用（连接 Enabled=false）的组件不占桌面（与单宿主同口径）
            if (_runtime.Config.Connections.FirstOrDefault(c => c.Id == widget.ConnectionId) is { Enabled: false })
            {
                _latest.Remove(widget.Id);
                continue;
            }
            // 产品拍板：数值/额度类不上悬浮窗（信息密度低）——钉选数据不动，宿主面板承载（App 装配负责挂面板）
            if (!IsFloatingEligible(widget.Type))
            {
                migrated++;
                continue;
            }
            wanted.Add(widget);
        }

        if (migrated > 0 && !_migrationNoticeShown)
        {
            _migrationNoticeShown = true;
            _runtime.Logger?.LogInformation("悬浮形态：{Count} 个数值类钉选组件不适用悬浮窗，已在宿主面板展示（钉选数据未动）。", migrated);
            _sink?.Show(new NotificationRecord
            {
                Id = "floating-form-migrated",
                SourceWidgetId = "",
                Severity = Severity.Info,
                Title = "悬浮形态提示",
                Message = "数量类组件默认不显示悬浮窗（桌面零残留），钉选内容在宿主面板查看，数据未动；可在 设置 → 常规 → 数量悬浮窗 打开。",
                Timestamp = DateTimeOffset.UtcNow,
            }, new NotificationDelivery(Toast: true, Sound: false));
        }

        var wantedIds = wanted.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in _windows.Where(kv => !wantedIds.Contains(kv.Key)).ToList())
        {
            stale.Value.Close();
            _windows.Remove(stale.Key);
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            var widget = wanted[index];
            if (_windows.TryGetValue(widget.Id, out var existing))
            {
                existing.CascadeIndex = index;
                existing.EnsurePlaced();
            }
            else
            {
                var window = new FloatingTileWindow(this, widget, _palette, _motion, index, _fallbackMonitor);
                _windows[widget.Id] = window;
                window.Initialize();
                if (_latest.TryGetValue(widget.Id, out var state))
                {
                    window.Update(state); // 重放最近状态，tile 不空等下一轮刷新
                }
            }
        }
    }

    private bool IsPinSupported(string widgetType)
        => _runtime.Resolver.Resolve(widgetType)?.Descriptor.PinSupported == true;

    /// <summary>拖动落点持久化：每组件各记（widgets.json 便利引用 + pins.json 权威，同单宿主双写口径）。</summary>
    public void PersistPosition(string widgetId, int x, int y)
    {
        var widget = _runtime.Config.Widgets.FirstOrDefault(w => w.Id == widgetId);
        if (widget is null)
        {
            return;
        }
        var monitor = MonitorService.FromPixel(x + 8, y + 8) ?? MonitorService.Primary() ?? _fallbackMonitor;
        var layout = new PinLayout
        {
            Monitor = MonitorIdentity(monitor),
            Anchor = PinAnchor.TopLeft, // floating 模式锚点角不参与，占位防 required 缺省
            OffsetDips = widget.PinLayout?.OffsetDips ?? PinLayoutMath.DefaultOffset,
            Collapsed = false,
            FloatingX = x,
            FloatingY = y,
        };
        widget.PinLayout = layout;
        _runtime.Config.UpsertWidget(widget);

        var pins = _runtime.Config.Pins;
        var tileIndex = pins.Tiles.FindIndex(t => t.WidgetId == widgetId);
        if (tileIndex >= 0)
        {
            pins.Tiles[tileIndex] = new Core.Models.PinTile { WidgetId = widgetId, Layout = layout }; // PinTile.Layout 为 init-only，重建替换
        }
        else
        {
            pins.Tiles.Add(new Core.Models.PinTile { WidgetId = widgetId, Layout = layout });
        }
        _runtime.Config.SavePins();
    }

    private static string MonitorIdentity(MonitorInfo monitor)
    {
        var width = monitor.MonitorPx.Right - monitor.MonitorPx.Left;
        var height = monitor.MonitorPx.Bottom - monitor.MonitorPx.Top;
        return $"{monitor.DeviceName}-{width}x{height}";
    }

    private void OnTileActivated() => TileActivated?.Invoke();

    /// <summary>单个悬浮窗（组合 PinTile + 位置/拖动/持久化）。</summary>
    internal sealed class FloatingTileWindow
    {
        private const int TileHeightDips = 32;
        private const int TileWidthDips = 168;
        private const int CascadeStepDips = 36;
        private const int EdgeMarginPx = 8;

        private readonly FloatingTileHost _host;
        private readonly WidgetConfig _widget;
        private readonly MonitorInfo _fallbackMonitor;
        private readonly PinTile _tile;

        private Window _window = null!;
        private IntPtr _hwnd;
        private AppWindow? _appWindow;
        private DragSession? _drag;
        private bool _dragMoved;

        /// <summary>未拖过组件的级联序号（ReloadTiles 时随 pin 集变化更新）。</summary>
        public int CascadeIndex { get; set; }

        public IntPtr Hwnd => _hwnd;

        public FloatingTileWindow(FloatingTileHost host, WidgetConfig widget, UiPalette palette, MotionEngine motion, int cascadeIndex, MonitorInfo fallbackMonitor)
        {
            _host = host;
            _widget = widget;
            _fallbackMonitor = fallbackMonitor;
            CascadeIndex = cascadeIndex;
            _tile = new PinTile(widget, palette, motion);
            _tile.Root.PointerPressed += OnPointerPressed;
            _tile.Root.PointerMoved += OnPointerMoved;
            _tile.Root.PointerReleased += OnPointerReleased;
            _tile.Root.PointerCanceled += OnPointerReleased;
            _tile.Root.Tapped += OnTapped;
        }

        public void Initialize()
        {
            _window = new Window { Title = "Beacon Tile" };
            _window.Content = _tile.Root;
            ((FrameworkElement)_window.Content).Loaded += OnLoaded;
            _window.Activate();
        }

        public void Close() => _window?.Close();

        public void Update(WidgetState state) => _tile.Update(state);

        /// <summary>落位：优先记忆位置（PinLayout.FloatingX/Y），未拖过按序级联；越界收回工作区（显示器拓扑变化兜底）。</summary>
        public void EnsurePlaced()
        {
            if (GetAppWindow() is not { } appWindow)
            {
                return; // Loaded 前不落位，OnLoaded 会再调
            }
            var work = WorkArea();
            var dpi = GetDpi();
            var x = _widget.PinLayout?.FloatingX;
            var y = _widget.PinLayout?.FloatingY;
            if (x is not { } restoredX || y is not { } restoredY)
            {
                // 未拖过：主屏右上角起竖向级联（与单宿主默认锚点角一致）
                var step = (int)(CascadeStepDips * dpi);
                restoredX = work.Right - appWindow.Size.Width - (int)(PinLayoutMath.DefaultOffset.X * dpi);
                restoredY = work.Y + (int)(PinLayoutMath.DefaultOffset.Y * dpi) + CascadeIndex * step;
            }
            var clamped = PinLayoutMath.ClampInto(work, new PinRect(restoredX, restoredY, appWindow.Size.Width, appWindow.Size.Height), margin: EdgeMarginPx);
            appWindow.Move(new PointInt32(clamped.X, clamped.Y));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
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
            NativeMethods.AddWindowExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

            var dpi = GetDpi();
            _appWindow.Resize(new SizeInt32((int)(TileWidthDips * dpi), (int)(TileHeightDips * dpi)));
            EnsurePlaced();
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint(_tile.Root).Properties.IsLeftButtonPressed || GetAppWindow() is not { } appWindow)
            {
                return;
            }
            _dragMoved = false;
            _drag = new DragSession(_tile.Root, e.GetCurrentPoint(null).Position, appWindow.Position.X, appWindow.Position.Y);
            _tile.Root.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_drag is null || GetAppWindow() is not { } appWindow)
            {
                return;
            }
            var dpi = GetDpi();
            var position = e.GetCurrentPoint(null).Position;
            var dx = (int)Math.Round((position.X - _drag.StartDip.X) * dpi);
            var dy = (int)Math.Round((position.Y - _drag.StartDip.Y) * dpi);
            if (!_dragMoved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
            {
                return; // 死区，区分点击与拖动（同单宿主口径）
            }
            _dragMoved = true;
            appWindow.Move(new PointInt32(_drag.StartX + dx, _drag.StartY + dy));
            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_drag is null)
            {
                return;
            }
            _drag = null;
            _tile.Root.ReleasePointerCapture(e.Pointer);
            if (_dragMoved && GetAppWindow() is { } appWindow)
            {
                _host.PersistPosition(_widget.Id, appWindow.Position.X, appWindow.Position.Y);
            }
            e.Handled = true;
        }

        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (_dragMoved)
            {
                _dragMoved = false; // 拖动结束的合成点击忽略
                return;
            }
            _host.OnTileActivated(); // 点击下钻 L2（RFC §6.2.7）
            e.Handled = true;
        }

        private PinRect WorkArea()
        {
            var appWindow = GetAppWindow();
            var center = appWindow is { } window
                ? MonitorService.FromPixel(window.Position.X + window.Size.Width / 2, window.Position.Y + window.Size.Height / 2)
                : null;
            var monitor = center ?? MonitorService.Primary() ?? _fallbackMonitor;
            var work = monitor.WorkPx;
            return new PinRect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);
        }

        private AppWindow? GetAppWindow()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return _appWindow; // Loaded 后立即有值（OnLoaded 已取）
            }
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            return windowId == default ? null : AppWindow.GetFromWindowId(windowId);
        }

        private double GetDpi() => _hwnd == IntPtr.Zero ? 1.0 : NativeMethods.GetDpiForWindow(_hwnd) / 96.0;
    }
}
