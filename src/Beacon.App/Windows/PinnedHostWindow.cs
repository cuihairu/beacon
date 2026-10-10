using System.Runtime.InteropServices;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>
/// L0 悬浮 tile（B-702，RFC §6.2.1）：StatusLight + 标签 + 数字/摘要，32 DIP 高。
/// 组合而非派生（WinUI 3 的 Border 是 sealed，CS0509）；输入事件由宿主挂到 Root；
/// 状态变化只更新自身视觉。
/// </summary>
internal sealed class PinTile
{
    private const int TileHeight = 32;
    private const int LightSizeDips = 8;

    private readonly WidgetConfig _widget;
    private readonly UiPalette _palette;
    private readonly MotionEngine _motion;
    private WidgetState? _lastState;
    private Severity? _lastSeverity;
    private bool _suspended; // 收起态暂停动画循环（B-707 验收：隐藏/收起无动画循环）

    private readonly Border _root = new()
    {
        Height = TileHeight,
        Padding = new Thickness(10, 0, 10, 0),
        // 实色主题底（对比度可判定）：此前 12% 透明深灰 scrim 叠在窗口/桌面上——
        // 浅主题 + 浅桌面底时近白 icon 必然发虚（2026-10-09 用户实测「根本看不清」根因）
        Background = new SolidColorBrush(ThemeColors.Surface()),
    };
    private readonly Ellipse _light = new()
    {
        Width = 8,
        Height = 8,
        Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)),
        StrokeThickness = 2,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _label = new()
    {
        Foreground = new SolidColorBrush(ThemeColors.Label()),
        FontSize = 12,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _value = new()
    {
        Foreground = new SolidColorBrush(ThemeColors.Value()),
        FontSize = 11,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Right,
    };
    // 额度数值卡进度条（http.quota/bigmodel.usage 等 Progress 语义）：tile 底部 3px 条，按已用比例填色。
    // 2026-10-10 重构（用户实测「进度条看不到」，CI 截图证实前代 Star 列设计从未渲染出任何像素）：
    // 弃嵌套 Star 列 Grid，改两枚显式 Rectangle——Height 3、贴底 margin 0、宽度由 container.SizeChanged
    // 按实测像素设置，结构上不存在「布局余量被吃掉整条消失」路径；track=Value 色 35% 透明（双主题
    // 都可辨的底轨），fill=severity 色。比例缓存进 _barFraction，SizeChanged 到达即重放（首次
    // Update 时 ActualWidth 可能还是 0，不丢）。
    private readonly Rectangle _barTrack = new()
    {
        Height = 3,
        VerticalAlignment = VerticalAlignment.Bottom,
        Fill = new SolidColorBrush(ThemeColors.Value()) { Opacity = 0.35 },
        Visibility = Visibility.Collapsed,
    };
    private readonly Rectangle _barFill = new()
    {
        Height = 3,
        VerticalAlignment = VerticalAlignment.Bottom,
        HorizontalAlignment = HorizontalAlignment.Left,
        RadiusX = 1.5,
        RadiusY = 1.5,
        Visibility = Visibility.Collapsed,
    };
    private double _barFraction;

    /// <summary>宿主面板挂载与输入挂接的根元素。</summary>
    public Border Root => _root;

    public string WidgetId { get; }

    public PinTile(WidgetConfig widget, UiPalette palette, MotionEngine motion, string? connectionType = null)
    {
        WidgetId = widget.Id;
        _widget = widget;
        _palette = palette;
        _motion = motion;
        _label.Text = LabelOf(widget);
        _value.Text = "加载中…"; // 空白即 bug：首态也要有信息（首个状态事件到达即被覆盖）

        // 数据源品牌图标（用户令：悬浮框必须带对应 icon，不然分不清哪个悬浮框是哪个源）：
        // 组件级自选图标（widget.Config["icon"]）优先 → BrandIcons 连接品牌命中 → 通用 globe 字形兜底
        // 前景主题感知（浅底深字/深底亮字，≥4.5:1）——近白 icon 在浅底上不可辨（用户实测「根本看不清」）
        // 2026-10-10 修「icon 太浅看不清」：14→16 DIP + 同色 1px 描边增重（细线几何覆盖率 7-22% 是根因，非填色）；
        // 兜底字形同步提到 Label() 前景 + 14px，与品牌图同档可读
        var icon = BrandIconFactory.TryCreate(connectionType, 16, ThemeColors.Label(), iconOverride: widget.Config.GetValueOrDefault("icon"), strokeWidth: 1)
            ?? new FontIcon
            {
                Glyph = "\uE774", // Segoe Fluent World：通用数据源兜底
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(ThemeColors.Label()),
            };

        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_light, 0);
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(_label, 2);
        Grid.SetColumn(_value, 3);
        grid.Children.Add(_light);
        grid.Children.Add(icon);
        grid.Children.Add(_label);
        grid.Children.Add(_value);

        // 主行 + 底部进度条叠放（见 _barTrack/_barFill 注释）：单 Grid 三子，后加者在上层；
        // 主内容竖直居中（12px 文本居 32 DIP，下部留白 ≥6 DIP），与 3px 条互不碰撞
        var container = new Grid();
        container.Children.Add(grid);
        container.Children.Add(_barTrack);
        container.Children.Add(_barFill);
        container.SizeChanged += (_, e) => ApplyBarGeometry(e.NewSize.Width); // 布局实测后重放比例
        _root.Child = container;
    }

    /// <summary>
    /// 状态变化只更新本 tile（B-701）；离线/错误降级（B-704，RFC §6.2.6）：
    /// ConnectionHealthy=false → 灰空心灯 + Last update HH:mm，绝不弹异常；恢复后健康事件自动复位。
    /// 着色走 UiPalette（B-706：健康灯应用 Widget 的 colorOverride；offline 是连接健康派生态，
    /// 恒走 appearance.SeverityColors["offline"]，不吃 Widget 覆盖——灰要始终可辨）。
    /// 动效走 MotionEngine（B-707）：变色过渡/Critical 脉冲（reduced 默认）、
    /// full 档呼吸 + 变化闪烁；收起态 SuspendMotion 暂停循环。
    /// </summary>
    public void Update(WidgetState state)
    {
        _lastState = state;
        if (state.ConnectionHealthy)
        {
            var changed = _lastSeverity is { } previous && previous != state.Severity;
            _lastSeverity = state.Severity;

            if (!_suspended)
            {
                ApplyLoops(state.Severity);
            }

            var color = _palette.SeverityColor(state.Severity, widgetOverride: _widget.ColorOverride);
            var breathingOwnsOpacity = !_suspended
                && _motion.Mode == MotionMode.Full && state.Severity != Severity.Critical;
            if (_suspended || breathingOwnsOpacity)
            {
                _light.Fill = new SolidColorBrush(color); // 呼吸循环正占 Opacity，直接换色不打断
            }
            else
            {
                _motion.TransitionFill(_light, color); // ① 变色过渡（off 档内部退化为瞬时）
            }

            _light.Stroke = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _value.Text = ValueOf(state);
            UpdateBar(state, color);
            ToolTipService.SetToolTip(_root, state.IsStale ? $"Last update {state.FetchedAt.ToLocalTime():HH:mm:ss}" : null);

            if (changed && !_suspended)
            {
                _motion.Flash(_root); // ② 提醒闪烁（full 档内部自闸）
            }
        }
        else
        {
            _lastSeverity = null; // 降级后恢复视为新状态（闪烁重新可触发）
            _light.Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _light.Stroke = new SolidColorBrush(_palette.SeverityColor(state.Severity, offline: true)); // Offline 灰
            _value.Text = $"Last update {state.FetchedAt.ToLocalTime():HH:mm}";
            _barFraction = 0;
            _barTrack.Visibility = Visibility.Collapsed;
            _barFill.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(_root, $"Offline · Last update {state.FetchedAt.ToLocalTime():HH:mm:ss}");
            _motion.StopLoops(_light);
            _motion.StopLoops(_root);
        }
    }

    /// <summary>③/④ 循环落位：Critical 脉冲（reduced 起）优先，full 档非 Critical 走呼吸灯。</summary>
    private void ApplyLoops(Severity severity)
    {
        _motion.StopLoops(_light);
        if (severity == Severity.Critical)
        {
            _motion.StartPulse(_light, LightSizeDips);
        }
        else if (_motion.Mode == MotionMode.Full)
        {
            _motion.StartBreathing(_light);
        }
    }

    /// <summary>额度进度条：Progress（0-1）→ 底部 3px 条比例填色（fill=severity 色，track=Value 35%）；
    /// 无进度语义的类型保持隐藏。</summary>
    private void UpdateBar(WidgetState state, global::Windows.UI.Color color)
    {
        if (state.Progress is not { } progress)
        {
            _barFraction = 0;
            _barTrack.Visibility = Visibility.Collapsed;
            _barFill.Visibility = Visibility.Collapsed;
            return;
        }

        _barFraction = Math.Clamp(progress, 0, 1);
        _barFill.Fill = new SolidColorBrush(color);
        _barTrack.Visibility = Visibility.Visible;
        _barFill.Visibility = Visibility.Visible;
        ApplyBarGeometry(_root.ActualWidth - _root.Padding.Left - _root.Padding.Right);
    }

    /// <summary>按容器实测宽设条几何：track 全宽、fill=比例宽（≥2 DIP 保底可辨）；
    /// 未布局（宽 0）或已隐藏时跳过——SizeChanged 会带着实测宽再进来。</summary>
    private void ApplyBarGeometry(double innerWidth)
    {
        if (_barTrack.Visibility != Visibility.Visible || double.IsNaN(innerWidth) || innerWidth < 8)
        {
            return;
        }

        _barTrack.Width = Math.Floor(innerWidth);
        _barFill.Width = Math.Max(2, Math.Floor(innerWidth * _barFraction));
    }

    /// <summary>收起为细条（宿主 ApplyLayout 判定）：暂停全部动画循环省电；展开复位。</summary>
    public void SuspendMotion()
    {
        if (_suspended)
        {
            return;
        }
        _suspended = true;
        _motion.StopLoops(_light);
        _motion.StopLoops(_root);
    }

    public void ResumeMotion()
    {
        if (!_suspended)
        {
            return;
        }
        _suspended = false;
        if (_lastState is { ConnectionHealthy: true } state)
        {
            ApplyLoops(state.Severity);
        }
    }

    /// <summary>⑤ 滑入：tile 出现在未收起的展开态时调用（full 档）。</summary>
    public void PlaySlideIn() => _motion.SlideIn(_root);

    /// <summary>tile 标签：优先用户配的显示名（label），再取自然名（GitHub repo/owner），
    /// 否则取连接类型短名（WidgetTypeNames）。旧兜底取类型尾段——ark.usage tile 显示成「usage」，
    /// 字段名冒充了显示名（2026-10-09 用户实测悬浮框名字错误）；未知前缀才退尾段。</summary>
    private static string LabelOf(WidgetConfig widget)
    {
        foreach (var key in new[] { "label", "repo", "owner" })
        {
            if (widget.Config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return WidgetTypeNames.Of(widget.Type);
    }

    /// <summary>数值位短文本：映射规则在 Core（WidgetValueHint，Linux 可单测），此处仅委托。</summary>
    private static string ValueOf(WidgetState state) => WidgetValueHint.Of(state);
}

/// <summary>一次拖动会话（左键按住 tile 起手）；FreeX/FreeY = 吸附前的自由落点（收起判定用，防 snap 假贴边）。</summary>
internal sealed record DragSession(UIElement Element, global::Windows.Foundation.Point StartDip, int StartX, int StartY)
{
    public int LastFreeX { get; set; }
    public int LastFreeY { get; set; }
}

/// <summary>
/// L0 单窗口多 tile 宿主（B-701/703，RFC §6.2.3/6.2.4）：
/// 置顶 + WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW；窗口子类化——
/// WM_NCHITTEST tile 行 HTCLIENT / 空白 HTTRANSPARENT，WM_DPICHANGED 重排，WM_DISPLAYCHANGE 越界回收。
/// 位置按「显示器标识 + 锚点角 + DIP 偏移 + 收起态」持久化到 pins.json（PinLayoutMath 互算）；
/// 拖动吸附四边，拖至屏边收起为细条（悬停展开、点击进 L2）。
/// </summary>
internal sealed class PinnedHostWindow
{
    private const int PanelWidthDips = 168;
    private const int TileHeightDips = 32;
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
            _dispatcherQueue.TryEnqueue(() =>
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
            // 模块停用（连接 Enabled=false）的组件不占桌面
            if (_runtime.Config.Connections.FirstOrDefault(c => c.Id == widget.ConnectionId) is { Enabled: false })
            {
                _latest.Remove(widget.Id);
                continue;
            }
            // 数量悬浮框默认关（拍板 ×4）：数值/额度类（FloatingOptIn）受 设置→常规 数量悬浮窗 总闸，
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
                _runtime.Config.Connections.FirstOrDefault(c => c.Id == widget.ConnectionId)?.Type);
            AttachTileInput(tile.Root);
            _tiles[widget.Id] = tile;
            placed.Add(tile);
        }
        // UI/UX 审计：单列竖排在 tile 超工作区高时底部被裁且无任何提示——超阈值改双列
        // （18×32=576 DIP 起切），面板宽随列数翻倍，行高仍 32 不挤压 tile。
        var columns = placed.Count > TwoColumnThreshold ? 2 : 1;
        var rows = Math.Max(1, (placed.Count + columns - 1) / columns);
        for (var i = 0; i < columns; i++)
        {
            _tilePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelWidthDips) });
        }
        for (var i = 0; i < rows; i++)
        {
            _tilePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TileHeightDips) });
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

    private void UpdateTile(WidgetState state)
    {
        if (_tiles.TryGetValue(state.WidgetId, out var tile))
        {
            tile.Update(state);
        }
    }

    // —— B-703：位置记忆（monitor + anchor + offsetDips + collapsed，pins.json 权威承载） ——

    private static string Identity(MonitorInfo monitor)
    {
        var width = monitor.MonitorPx.Right - monitor.MonitorPx.Left;
        var height = monitor.MonitorPx.Bottom - monitor.MonitorPx.Top;
        return $"{monitor.DeviceName}-{width}x{height}";
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

    private void ApplyLayout()
    {
        var appWindow = GetAppWindow();
        if (appWindow is null || _layout is null)
        {
            return;
        }
        var dpi = GetDpi();
        var work = ToPinRect(ResolveMonitor(_layout.Monitor).WorkPx);
        // 与 ReloadTiles 同口径：列数随 tile 数切换，高度按行数、宽度按列数算（双列后 336 宽不再溢出单列假设）
        var columns = _tiles.Count > TwoColumnThreshold ? 2 : 1;
        var rows = Math.Max(1, (_tiles.Count + columns - 1) / columns);
        var height = rows * (int)(TileHeightDips * dpi);
        var width = columns * (int)(PanelWidthDips * dpi);

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

        if (strip)
        {
            // 吸边细条：贴锚点侧竖边，纵向按偏移（B-703：拖至屏边收起为细条/圆点）
            var stripWidth = Math.Max(1, (int)(CollapseStripDips * dpi));
            var anchorLeft = _layout.Anchor is PinAnchor.TopLeft or PinAnchor.BottomLeft;
            var x = anchorLeft ? work.X : work.Right - stripWidth;
            var y = Math.Clamp(work.Y + (int)(_layout.OffsetDips.Y * dpi), work.Y + 8, Math.Max(work.Y + 8, work.Bottom - height - 8));
            appWindow.Resize(new SizeInt32(stripWidth, height));
            appWindow.Move(new PointInt32(x, y));
            return;
        }

        var rect = PinLayoutMath.Place(work, _layout.Anchor, _layout.OffsetDips, dpi, width, height);
        appWindow.Resize(new SizeInt32(rect.Width, rect.Height));
        appWindow.Move(new PointInt32(rect.X, rect.Y));
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
        _drag = new DragSession(tile, e.GetCurrentPoint(null).Position, appWindow.Position.X, appWindow.Position.Y);
        tile.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnTilePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is null || !ReferenceEquals(_drag.Element, sender) || GetAppWindow() is not { } appWindow)
        {
            return;
        }
        var dpi = GetDpi();
        var position = e.GetCurrentPoint(null).Position;
        var dx = (int)Math.Round((position.X - _drag.StartDip.X) * dpi);
        var dy = (int)Math.Round((position.Y - _drag.StartDip.Y) * dpi);
        if (!_dragMoved && Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
        {
            return; // 死区，区分点击与拖动
        }
        _dragMoved = true;

        var work = CurrentWork();
        var width = appWindow.Size.Width;
        var height = appWindow.Size.Height;
        var snap = (int)(SnapDips * dpi);
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
        appWindow.Move(new PointInt32(x, y));
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
        _layout.Collapsed = edge <= Math.Max(2, (int)(2 * dpi)); // 贴死才收起；未贴死=回弹展开
        _hoverExpanded = false;
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
        // 水滴态点击 = 弹回展开（用户令：坍缩必须可逆——可拖出/可点击弹回）；进 L2 需在展开态点击
        if (_layout is { Collapsed: true } && !_hoverExpanded)
        {
            _hoverExpanded = true;
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
        if (_hoverExpanded && _drag is null)
        {
            _hoverExpanded = false; // 离开面板回到细条
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
                    // UI/UX 审计：双列后行数/列数与 ApplyLayout 同口径（rows×32 高、columns×168 宽）
                    var columns = _tiles.Count > TwoColumnThreshold ? 2 : 1;
                    var rows = Math.Max(1, (_tiles.Count + columns - 1) / columns);
                    var insideTile = point.Y >= 0 && point.Y < rows * (int)(TileHeightDips * dpi)
                        && point.X >= 0 && point.X < (int)(HitWidthDips(columns) * dpi);
                    return insideTile ? new IntPtr(NativeMethods.HTCLIENT) : new IntPtr(NativeMethods.HTTRANSPARENT);
                }
                break;
            case NativeMethods.WM_DPICHANGED:
                // 跨 DPI：XAML 内容按新 DPI 自动缩放，窗口尺寸/位置按新 DPI 重排（B-703 验收）
                if (_layout is not null)
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

    /// <summary>当前 hit-test 宽（DIP）：收起细条窄、展开态全宽（双列翻倍，与 ApplyLayout 同口径）。</summary>
    private double HitWidthDips(int columns = 1)
        => _layout is { Collapsed: true } && !_hoverExpanded ? CollapseStripDips : PanelWidthDips * columns;

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
