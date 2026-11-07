using System.Runtime.InteropServices;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>
/// L0 单窗口多 tile 宿主（B-701/703，RFC §6.2.3/6.2.4）：
/// 置顶 + WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW；窗口子类化——
/// WM_NCHITTEST tile 行 HTCLIENT / 空白 HTTRANSPARENT，WM_DPICHANGED 重排，WM_DISPLAYCHANGE 越界回收。
/// 位置按「显示器标识 + 锚点角 + DIP 偏移 + 收起态」持久化到 pins.json（PinLayoutMath 互算）；
/// 拖动吸附四边，拖至屏边贴死收起为细条（config.edgeCollapseEnabled 开关，默认开；悬停临时展开、
/// 点击粘性展开不移出即缩、拖动落点重判，2026-10-14 用户单③）。
/// </summary>
internal sealed class PinnedHostWindow
{
    private const int TwoColumnThreshold = 18; // UI/UX 审计：tile 超此数改双列——单列在超工作区高时底部裁切且无提示（18×32=576 DIP）
    private const int CollapseStripDips = 12; // 收起细条宽（DIP）
    private const int SnapDips = 16;          // 拖动吸边判定距离（DIP）

    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly MotionEngine _motion;
    private readonly MonitorInfo _fallbackMonitor;
    private readonly Dictionary<string, PinTile> _tiles = [];
    private readonly Dictionary<string, WidgetState> _latest = [];
    private readonly List<IDisposable> _subscriptions = [];

    private Window _window = null!;
    private Grid _tilePanel = null!; // 单列/双列随 tile 数切换（ReloadTiles 重建行列定义）
    private AppWindow _appWindow = null!;
    private IntPtr _hwnd;
    private DispatcherQueue _dispatcherQueue = null!; // 事件回调 marshal 回 UI 线程（Initialize 在 UI 线程取）

    /// <summary>TopmostGuard 用：宿主窗口句柄（Loaded 前为 Zero，哨兵会跳过）。</summary>
    public IntPtr Hwnd => _hwnd;
    private NativeMethods.WndProcDelegate? _wndProc; // 字段持有防 GC 回收子类化回调
    private IntPtr _prevWndProc;

    private PinLayout _layout = null!;
    private DragSession? _drag;
    private bool _dragMoved;      // 拖动已位移（抑制随后的合成 Tapped）
    private bool _hoverExpanded;  // 收起态悬停临时展开
    private bool _stickyExpanded; // 收起态点击的可靠展开（2026-10-14 用户单③：不随移出回缩，拖动重判/开关关闭时清除）

    /// <summary>tile 点击（收起态点击含）：下钻 L2（RFC §6.2.7）。</summary>
    public event Action? TileActivated;

    public PinnedHostWindow(BeaconRuntime runtime, MonitorInfo fallbackMonitor)
    {
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config); // B-706：每次渲染实时读 appearance，改色即刻生效
        _motion = new MotionEngine(runtime.Config); // B-707：动效档位/强度实时读 appearance.Motion
        _fallbackMonitor = fallbackMonitor;
    }

    public void Initialize()
    {
        _window = new Window { Title = "Beacon Pinned" };
        _tilePanel = new Grid();
        _tilePanel.PointerEntered += OnPanelPointerEntered;
        _tilePanel.PointerExited += OnPanelPointerExited;
        _window.Content = _tilePanel;
        ((FrameworkElement)_window.Content).Loaded += OnLoaded;
        _window.Activate();
        _dispatcherQueue = _window.DispatcherQueue; // EventBus.Publish 在刷新线程，UI 对象必须回 UI 线程碰

        _subscriptions.Add(_runtime.Bus.Subscribe<WidgetStateChanged>(evt =>
            _dispatcherQueue.EnqueueGuarded(() =>
            {
                _latest[evt.State.WidgetId] = evt.State;
                UpdateTile(evt.State);
            })));

        ReloadTiles();
    }

    /// <summary>关闭宿主窗口（退出清理用）：包装类没有原生 Close，转给内部 Window。</summary>
    public void Close()
    {
        _window?.Close();
    }

    /// <summary>config.uiOpacity（B-801 界面透明度）：挂宿主面板整窗——MotionEngine Flash/SlideIn 动的是
    /// tile 根（子元素），互不覆盖（批8 2026-10-10：透明度与胶囊/独立悬浮框/快捷面板统一同一来源）。</summary>
    public void ApplyOpacity(double opacity)
        => ((FrameworkElement)_window.Content).Opacity = Math.Clamp(opacity, 0.2, 1.0);

    /// <summary>按 widgets.json 的 Pinned + PinSupported 重建 tile 集（B-702 入口一致性：右键/设置开关后调用）。</summary>
    public void ReloadTiles()
    {
        _tilePanel.Children.Clear();
        _tilePanel.RowDefinitions.Clear();
        _tilePanel.ColumnDefinitions.Clear();
        _tiles.Clear();
        var placed = new List<PinTile>();
        foreach (var widget in _runtime.Config.Widgets)
        {
            if (!widget.Pinned || !IsPinSupported(widget.Type))
            {
                continue;
            }
            // 组件中心（Component Center）勾选隐藏的组件不占桌面（config.hiddenComponents，与设置页同口径 Core 单源）
            if (ComponentCenterCatalog.IsHidden(_runtime.Config.App, widget.Id))
            {
                _latest.Remove(widget.Id);
                continue;
            }
            // 模块停用（连接 Enabled=false）的组件不占桌面
            if (_runtime.Config.Connections.FirstOrDefault(c => c.Id == widget.ConnectionId) is { Enabled: false })
            {
                _latest.Remove(widget.Id);
                continue;
            }
            // 数量悬浮窗默认开（2026-10-11 用户令，推翻 2026-10-08 拍板）：数值/额度类（FloatingOptIn）受 设置→常规 数量悬浮窗 总闸，
            // 关 = 钉选也不画上桌面。此前只闸了悬浮形态窗口（IsFloatingEligible），本面板照画
            // widget.Pinned 的数值卡（GLM 绑定流一键建卡即钉选）——「改了还弹」的第二条渲染路径。
            if (WidgetDisplayPolicy.IsNumericSuppressed(
                    _runtime.Resolver.Resolve(widget.Type)?.Descriptor,
                    _runtime.Config.App.NumericFloatingEnabled))
            {
                _latest.Remove(widget.Id);
                continue;
            }
            var tile = new PinTile(widget, _palette, _motion,
                _runtime.Config.Connections.FirstOrDefault(c => c.Id == widget.ConnectionId)?.Type)
            {
                StateLookup = id => _latest.TryGetValue(id, out var source) ? source : null, // P3-03 附加源快照
            };
            AttachTileInput(tile.Root);
            _tiles[widget.Id] = tile;
            placed.Add(tile);
        }
        // UI/UX 审计：单列竖排在 tile 超工作区高时底部被裁且无任何提示——超阈值改双列
        // （18×32=576 DIP 起切），面板宽随列数翻倍，行高仍 32 不挤压 tile。
        // 列宽按钉选 tile 的推算宽取最大（全类型逐列字宽预算），三处（列定义/PanelPxSize/HitWidthPx）同源。
        var columns = placed.Count > TwoColumnThreshold ? 2 : 1;
        var rows = Math.Max(1, (placed.Count + columns - 1) / columns);
        for (var i = 0; i < columns; i++)
        {
            _tilePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelWidthDips()) });
        }
        for (var i = 0; i < rows; i++)
        {
            _tilePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeightDips()) });
        }
        for (var i = 0; i < placed.Count; i++)
        {
            Grid.SetColumn(placed[i].Root, i % columns);
            Grid.SetRow(placed[i].Root, i / columns);
            _tilePanel.Children.Add(placed[i].Root);
        }
        // 重放最近状态，tile 不空等下一轮刷新
        foreach (var tile in _tiles.Values)
        {
            if (_latest.TryGetValue(tile.WidgetId, out var state))
            {
                tile.Update(state);
            }
        }
        if (_layout is not null)
        {
            ApplyLayout(); // 收起态/重建后尺寸随 tile 数变化
        }
        // ⑤ 滑入（B-707）：展开态首次出现的 tile 自上落位（收起态不播）
        if (_layout is null || !_layout.Collapsed || _hoverExpanded)
        {
            foreach (var tile in _tiles.Values)
            {
                tile.PlaySlideIn();
            }
        }
        SyncHostVisibility(); // Pin/Unpin 到 0 或从 0 恢复时同步显隐
    }

    private bool IsPinSupported(string widgetType)
        => _runtime.Resolver.Resolve(widgetType)?.Descriptor.PinSupported == true;

    /// <summary>状态路由（P3-03 依赖路由）：自身状态直达本面（B-701）；声明式面声明的附加源变化
    /// 只重渲消费它的面——无关源变化不重建任何 tile。</summary>
    private void UpdateTile(WidgetState state)
    {
        foreach (var tile in _tiles.Values)
        {
            if (string.Equals(tile.WidgetId, state.WidgetId, StringComparison.Ordinal))
            {
                tile.Update(state);
            }
            else if (tile.ConsumesWidget(state.WidgetId))
            {
                tile.RefreshSources();
            }
        }
    }

    // —— B-703：位置记忆（monitor + anchor + offsetDips + collapsed，pins.json 权威承载） ——

    internal static string Identity(MonitorInfo monitor) // 设置页桌面布局区复用同一标识格式（单源）
    {
        var width = monitor.MonitorPx.Right - monitor.MonitorPx.Left;
        var height = monitor.MonitorPx.Bottom - monitor.MonitorPx.Top;
        return $"{monitor.DeviceName}-{width}x{height}";
    }

    /// <summary>设置页桌面布局区改共享布局后的重排：从存储重读布局并重排（拖动中跳过防互抢，
    /// 与 WM_DPICHANGED 同口径——ApplyLayout 的 Resize+Move 会和拖拽 Move 互抢窗口位置）。</summary>
    public void ReloadLayout()
    {
        if (_drag is not null)
        {
            return;
        }
        _layout = RestoreLayout();
        ApplyLayout();
    }

    private MonitorInfo ResolveMonitor(string? identity)
        => MonitorService.GetAll().FirstOrDefault(m => string.Equals(Identity(m), identity, StringComparison.OrdinalIgnoreCase))
           ?? MonitorService.Primary()
           ?? _fallbackMonitor;

    private PinLayout RestoreLayout()
    {
        var stored = _runtime.Config.Widgets.Where(w => w.Pinned)
            .Select(w => w.PinLayout).FirstOrDefault(l => l is not null)
            ?? _runtime.Config.Pins.Tiles.Select(t => t.Layout).FirstOrDefault(l => l is not null);
        if (stored is not null)
        {
            return new PinLayout
            {
                Monitor = stored.Monitor,
                Anchor = stored.Anchor,
                OffsetDips = stored.OffsetDips,
                Collapsed = stored.Collapsed,
            };
        }
        return new PinLayout
        {
            Monitor = Identity(_fallbackMonitor),
            Anchor = PinAnchor.TopRight,
            OffsetDips = PinLayoutMath.DefaultOffset,
            Collapsed = false,
        };
    }

    /// <summary>宿主布局落到每个 pinned tile 的记录（widgets.json 便利引用 + pins.json 权威），单宿主共享同一位置。</summary>
    private void PersistLayout()
    {
        var pinned = _runtime.Config.Widgets.Where(w => w.Pinned).ToList();
        foreach (var widget in pinned)
        {
            widget.PinLayout = new PinLayout
            {
                Monitor = _layout.Monitor,
                Anchor = _layout.Anchor,
                OffsetDips = _layout.OffsetDips,
                Collapsed = _layout.Collapsed,
            };
            _runtime.Config.UpsertWidget(widget);
        }
        var pins = _runtime.Config.Pins;
        pins.Tiles = [.. pinned.Select(w => new Beacon.Core.Models.PinTile
        {
            WidgetId = w.Id,
            Layout = new PinLayout
            {
                Monitor = _layout.Monitor,
                Anchor = _layout.Anchor,
                OffsetDips = _layout.OffsetDips,
                Collapsed = _layout.Collapsed,
            },
        })];
        _runtime.Config.SavePins();
    }

    /// <summary>面板列宽（DIP）：钉选 tile 推算宽（PinTile.TileWidthDips，全类型逐列字宽预算）取最大；
    /// 零钉选退 TileSizing.FloorDips。列定义/PanelPxSize/HitWidthPx 三处唯一口径。</summary>
    private int PanelWidthDips()
        => _tiles.Count == 0 ? TileSizing.FloorDips : _tiles.Values.Max(tile => tile.TileWidthDips);

    /// <summary>面板行高（DIP）：钉选 tile 尺寸档高（TileSizing.TileHeightDips，常规 32）取最大——
    /// 与列宽同口径（不同尺寸档混排时整行取高者不挤压），零钉选退基准 32。</summary>
    private int RowHeightDips()
        => _tiles.Count == 0 ? TileSizing.BaseTileHeightDips : _tiles.Values.Max(tile => tile.TileHeightDips);

    /// <summary>面板物理尺寸（ApplyLayout 与 WM_NCHITTEST 唯一口径）：列数随 tile 数切换，
    /// DIP 设计值只在 Win32 边界换算一次；总高一次四舍五入而非逐行（DPI 修复单 2026-10-10）。</summary>
    private (int Width, int Height) PanelPxSize(double dpi)
    {
        var columns = _tiles.Count > TwoColumnThreshold ? 2 : 1;
        var rows = Math.Max(1, (_tiles.Count + columns - 1) / columns);
        return (DpiLayoutMath.ToPx(columns * PanelWidthDips(), dpi), DpiLayoutMath.PanelHeightPx(rows, RowHeightDips(), dpi));
    }

    private void ApplyLayout()
    {
        var appWindow = GetAppWindow();
        if (appWindow is null || _layout is null)
        {
            return;
        }
        var dpi = GetDpi();
        var work = ToPinRect(ResolveMonitor(_layout.Monitor).WorkPx);
        // 面板物理尺寸唯一口径 PanelPxSize（ApplyLayout 与 WM_NCHITTEST 共用）：列数随 tile 数切换，
        // DIP 设计值只在 Win32 边界换算一次——逐行截断（32 DIP×1.3=41）会累计欠高，高缩放裁字
        var (width, height) = PanelPxSize(dpi);

        // 收起态暂停动画循环（B-707 验收：隐藏/收起时无动画循环）；展开/悬停恢复
        var strip = _layout.Collapsed && !_hoverExpanded;
        foreach (var tile in _tiles.Values)
        {
            if (strip)
            {
                tile.SuspendMotion();
            }
            else
            {
                tile.ResumeMotion();
            }
        }

        // 客户区需求 → 外框需求：AppWindow.Resize 设的是窗口外框，量出来的是客户区需求，必须补回
        // 非客户区差值（无边框 OverlappedPresenter 仍保留 ~6px resize 边框）。旧实现直接喂客户区
        // 需求尺寸 → 底部 6px 不可见，tile 底部 4 DIP 进度条整条落进不可见区（2026-10-14 缺陷单
        // 「底部进度条显示不全」根因；DetailWindow 2026-10-11 同型修复）。
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);

        if (strip)
        {
            // 吸边细条 = 展开面板的锚边窄条：同一 Place 数学、宽度换细条宽，条缘与面板缘重合——
            // 光标悬停细条任何位置都在展开面板 footprint 内。旧实现细条贴屏幕边（忽略 offsetX）而面板
            // 内缩 offsetX：悬停展开瞬间光标落在面板外 → PointerExited 立刻坍缩 → 再悬停再展开死循环，
            // 表现为「竖线贴着屏幕边无法恢复」（2026-10-10 用户实测根因）。高度仍取整面板高（细条=面板边条）。
            var stripWidth = Math.Max(1, DpiLayoutMath.ToPx(CollapseStripDips, dpi));
            var stripRect = PinLayoutMath.Place(work, _layout.Anchor, _layout.OffsetDips, dpi, stripWidth + chromeWidth, height + chromeHeight);
            appWindow.Resize(new SizeInt32(stripRect.Width, stripRect.Height));
            appWindow.Move(new PointInt32(stripRect.X, stripRect.Y));
            return;
        }

        var rect = PinLayoutMath.Place(work, _layout.Anchor, _layout.OffsetDips, dpi, width + chromeWidth, height + chromeHeight);
        appWindow.Resize(new SizeInt32(rect.Width, rect.Height));
        appWindow.Move(new PointInt32(rect.X, rect.Y));
    }

    /// <summary>
    /// 设置「贴边收起」开关回落（2026-10-14 用户单③）：开关关闭且当前已收起 → 弹回展开并落盘
    /// （用户失去控制的根因之一：坍缩默认开且无开关）。开关开=不主动收起，等下次拖到边自然判定；
    /// 未收起/开关仍开时为无操作（设置每次改动都会走本方法，保持幂等）。
    /// </summary>
    public void ApplyEdgeCollapseSetting()
    {
        if (_layout is not { Collapsed: true } || _runtime.Config.App.EdgeCollapseEnabled)
        {
            return;
        }
        _layout.Collapsed = false;
        _stickyExpanded = false;
        _hoverExpanded = false;
        ApplyLayout();
        PersistLayout();
    }

    /// <summary>显示拓扑变化：按显示器标识恢复；标识消失落主屏；Place 内部完成越界回收（B-703 验收）。</summary>
    private void Reconcile()
    {
        if (_layout is null || GetAppWindow() is null)
        {
            return;
        }
        var resolved = ResolveMonitor(_layout.Monitor);
        _layout.Monitor = Identity(resolved);
        _hoverExpanded = false;
        _stickyExpanded = false; // 拓扑变化后收起/展开态重算，粘性展开一律清
        ApplyLayout();
        PersistLayout();
    }

    private static PinRect ToPinRect(NativeMethods.RECT rect)
        => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    // —— B-703：拖动 + 吸附 + 吸边收起 ——

    private void AttachTileInput(UIElement element)
    {
        element.PointerPressed += OnTilePointerPressed;
        element.PointerMoved += OnTilePointerMoved;
        element.PointerReleased += OnTilePointerReleased;
        element.PointerCanceled += OnTilePointerReleased;
        element.Tapped += OnTileTapped;
    }

    private void OnTilePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var tile = (UIElement)sender;
        if (!e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed || GetAppWindow() is not { } appWindow)
        {
            return;
        }
        // 水滴坍缩修复（用户实测：细条拖不回桌面）：收起态起拖先弹回全宽，
        // 拖拽会话坐标用弹回后的窗口位置——杜绝会话锁死在 12px 收纳位、拖动整窗只有细条跟着走
        if (_layout is { Collapsed: true } && !_hoverExpanded)
        {
            _hoverExpanded = true; // 临时展开（不落库），FinalizeDrag 按落点重新判收起
            ApplyLayout();
            appWindow = GetAppWindow() ?? appWindow;
        }
        _dragMoved = false;
        _drag = new DragSession(tile, CursorPx(), appWindow.Position.X, appWindow.Position.Y);
        tile.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnTilePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null || !ReferenceEquals(_drag.Element, sender) || GetAppWindow() is not { } appWindow)
        {
            return;
        }
        // 光标屏幕物理像素锚定：与 AppWindow.Position/Move 同一坐标系，跨缩放屏拖动不换算不混算
        // （旧实现每帧重读 GetDpi 与按下时的 DIP 空间相减，跨到不同缩放的屏位移比例就错乱——拖不动/跳变）
        var (cursorX, cursorY) = CursorPx();
        var dx = cursorX - _drag.StartCursor.X;
        var dy = cursorY - _drag.StartCursor.Y;
        if (!_dragMoved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
        {
            return; // 死区，区分点击与拖动
        }
        _dragMoved = true;

        var work = CurrentWork();
        var width = appWindow.Size.Width;
        var height = appWindow.Size.Height;
        var snap = (int)(SnapDips * GetDpi()); // 吸附半径是 DIP 手感阈值，按当前屏 DPI 换算（拖动位移本身不经 DPI）
        var x = _drag.StartX + dx;
        var y = _drag.StartY + dy;
        _drag.LastFreeX = x; // 记录吸附前自由落点：收起判定看它，snap 对齐不算「用户贴边」
        _drag.LastFreeY = y;
        // 吸附四边（RFC §6.2.3：拖动中吸附到四边）
        if (Math.Abs(x - work.X) < snap)
        {
            x = work.X;
        }
        if (Math.Abs(x + width - work.Right) < snap)
        {
            x = work.Right - width;
        }
        if (Math.Abs(y - work.Y) < snap)
        {
            y = work.Y;
        }
        if (Math.Abs(y + height - work.Bottom) < snap)
        {
            y = work.Bottom - height;
        }
        if (x != appWindow.Position.X || y != appWindow.Position.Y)
        {
            appWindow.Move(new PointInt32(x, y)); // 目标没变不重发 Move：高频事件下减少 SetWindowPos 抖动
        }
        e.Handled = true;
    }

    private void OnTilePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null || !ReferenceEquals(_drag.Element, sender))
        {
            return;
        }
        var element = _drag.Element;
        var freeX = _drag.LastFreeX; // 先取后置空（FinalizeDrag 用自由落点判收起）
        var freeY = _drag.LastFreeY;
        _drag = null;
        element.ReleasePointerCapture(e.Pointer);
        if (_dragMoved)
        {
            FinalizeDrag(freeX: freeX, freeY: freeY);
        }
        e.Handled = true;
    }

    /// <summary>
    /// 拖动落点：跨显示器迁移记录、最近锚点角 + DIP 偏移持久化、收起判定。
    /// 防抖/回弹（用户令：短暂路过不误坍缩）：收起只看吸附前的自由落点贴死边缘（≤2px，snap 对齐后的刻意贴边）；
    /// 近边但未贴死的松手 = 回弹保持展开（旧口径 12 DIP 内即收起，路过边缘的随手一放就没了面板）。
    /// </summary>
    private void FinalizeDrag(int freeX, int freeY)
    {
        if (GetAppWindow() is not { } appWindow || _layout is null)
        {
            return;
        }
        var position = appWindow.Position;
        var size = appWindow.Size;
        var dpi = GetDpi();
        var monitor = MonitorService.FromPixel(position.X + size.Width / 2, position.Y + size.Height / 2)
            ?? MonitorService.Primary()
            ?? _fallbackMonitor;
        var work = ToPinRect(monitor.WorkPx);
        var rect = PinLayoutMath.ClampInto(work, new PinRect(position.X, position.Y, size.Width, size.Height), margin: 0);
        (_layout.Anchor, _layout.OffsetDips) = PinLayoutMath.Locate(work, rect, dpi);
        _layout.Monitor = Identity(monitor); // 跨显示器拖动 = 迁移 pinLayout 记录
        var edge = Math.Min(
            Math.Min(freeX - work.X, work.Right - (freeX + size.Width)),
            Math.Min(freeY - work.Y, work.Bottom - (freeY + size.Height)));
        // 贴死才收起；未贴死=回弹展开。开关（config.edgeCollapseEnabled）关=拖边只吸附不收起
        //（2026-10-14 用户单③：坍缩默认开且无关闭开关是可用性缺陷，开关给控制权）
        _layout.Collapsed = _runtime.Config.App.EdgeCollapseEnabled && edge <= Math.Max(2, (int)(2 * dpi));
        _hoverExpanded = false;
        _stickyExpanded = false; // 拖动重判收起/展开，粘性展开不跨拖动会话
        ApplyLayout();
        PersistLayout();
    }

    private void OnTileTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_dragMoved)
        {
            _dragMoved = false; // 拖动结束的合成点击忽略
            return;
        }
        // 水滴态点击 = 弹回展开（用户令：坍缩必须可逆——可拖出/可点击弹回）；进 L2 需在展开态点击。
        // 粘性展开（2026-10-14 用户单③）：点击展开必须可靠——旧实现点击也走 _hoverExpanded 临时展开，
        // 指针一移出面板立刻回缩成细条，用户体感「很难点开」；现在点击=粘住，直到拖动重判或开关关闭
        if (_layout is { Collapsed: true } && !_hoverExpanded)
        {
            _hoverExpanded = true;
            _stickyExpanded = true;
            ApplyLayout();
            e.Handled = true;
            return;
        }
        TileActivated?.Invoke(); // 点击下钻 L2（RFC §6.2.7）
        e.Handled = true;
    }

    private void OnPanelPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_layout is { Collapsed: true } && _drag is null && !_hoverExpanded)
        {
            _hoverExpanded = true; // 悬停展开（临时，不落库）
            ApplyLayout();
        }
    }

    private void OnPanelPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_hoverExpanded && _drag is null && !_stickyExpanded)
        {
            _hoverExpanded = false; // 离开面板回到细条（点击粘住的粘性展开不移出即缩）
            ApplyLayout();
        }
    }

    private PinRect CurrentWork() => ToPinRect(ResolveMonitor(CurrentMonitorIdentity()).WorkPx);

    private string? CurrentMonitorIdentity()
    {
        if (GetAppWindow() is not { } appWindow)
        {
            return null;
        }
        var position = appWindow.Position;
        var size = appWindow.Size;
        return MonitorService.FromPixel(position.X + size.Width / 2, position.Y + size.Height / 2) is { } monitor
            ? Identity(monitor)
            : null;
    }

    // —— 窗口生命周期与 Win32 ——

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

        _layout = RestoreLayout();
        ApplyLayout();
        ApplyEdgeCollapseSetting(); // 开关关着时清 pins.json 残留收起态（手改配置兜底，关=立刻弹回并落盘）
        SubclassForHitTest();
        SyncHostVisibility(); // 零钉选启动：Loaded 内立即隐藏，白板不进首帧
    }

    /// <summary>空宿主不占桌面：零钉选时隐藏（否则是一块白板悬浮物），首个 pin 出现时再显示。</summary>
    private void SyncHostVisibility()
    {
        if (_appWindow is null)
        {
            return;
        }
        if (_tiles.Count == 0)
        {
            _appWindow.Hide();
        }
        else
        {
            _appWindow.Show();
        }
    }

    private void SubclassForHitTest()
    {
        _wndProc = WndProc;
        _prevWndProc = NativeMethods.SetWindowLongPtr(
            _hwnd, NativeMethods.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_NCHITTEST:
                // tile 行内可交互；面板其余空白穿透到桌面（B-701 验收）；收起态按细条宽判定
                var point = new NativeMethods.POINT { X = unchecked((short)(long)lParam), Y = unchecked((short)((long)lParam >> 16)) };
                if (NativeMethods.ScreenToClient(hWnd, ref point))
                {
                    var dpi = GetDpi();
                    // 命中区与 ApplyLayout 同口径（PanelPxSize/HitWidthPx）：DIP 设计值只在 Win32 边界换算一次
                    var (_, panelHeightPx) = PanelPxSize(dpi);
                    var insideTile = point.Y >= 0 && point.Y < panelHeightPx
                        && point.X >= 0 && point.X < HitWidthPx(dpi);
                    return insideTile ? new IntPtr(NativeMethods.HTCLIENT) : new IntPtr(NativeMethods.HTTRANSPARENT);
                }
                break;
            case NativeMethods.WM_DPICHANGED:
                // 跨 DPI：XAML 内容按新 DPI 自动缩放，窗口尺寸/位置按新 DPI 重排（B-703 验收）。
                // 拖动中跳过重排：ApplyLayout 的 Resize+Move 会和拖拽 Move 互抢窗口位置，跨屏瞬间
                // 来回跳就是用户看到的闪烁（2026-10-10 实测）；落点按新 DPI 重排交给 FinalizeDrag。
                if (_layout is not null && _drag is null)
                {
                    _layout.Monitor = CurrentMonitorIdentity() ?? _layout.Monitor;
                    ApplyLayout();
                }
                break;
            case NativeMethods.WM_DISPLAYCHANGE:
                // 显示拓扑变化：按锚点恢复/越界回收（B-703 验收：拔显示器再接回 tile 不丢）
                if (_layout is not null)
                {
                    Reconcile();
                }
                break;
        }
        return _prevWndProc == IntPtr.Zero
            ? NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam)
            : NativeMethods.CallWindowProcW(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>光标屏幕物理像素（GetCursorPos；与 AppWindow.Position/Move 同一坐标系）。</summary>
    private static (int X, int Y) CursorPx()
    {
        NativeMethods.GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    /// <summary>当前 hit-test 宽（物理像素）：收起细条窄、展开态全宽（双列翻倍，与 ApplyLayout 同口径）。</summary>
    private int HitWidthPx(double dpi)
    {
        var columns = _tiles.Count > TwoColumnThreshold ? 2 : 1;
        var hitDips = _layout is { Collapsed: true } && !_hoverExpanded ? CollapseStripDips : PanelWidthDips() * columns;
        return DpiLayoutMath.ToPx(hitDips, dpi);
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
