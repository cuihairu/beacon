using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>Recent Events 列表行（状态变化流，与通知同源，RFC §6.4）。</summary>
internal sealed record RecentEvent(DateTimeOffset At, WidgetState State);

/// <summary>
/// L2 Quick Panel（RFC §6.4）：Flyout 语义——失焦自动隐藏、ESC 关闭、Tab 可达、不进任务栏。
/// 打开零网络等待（B-601/B-602）：内容常驻订阅事件流 + 启动时从缓存水合，打开 &lt;100ms。
/// Overview 按来源计数；Recent Events 单击进 L3（DetailRequested），右键 Pin to desktop 落 widgets.json。
/// </summary>
public sealed partial class QuickPanelWindow : Window
{
    private const int PanelWidth = 320;
    private const int PanelHeight = 480;
    private const int MaxRecentEvents = 50;

    private readonly NativeMethods.RECT _fallbackWorkArea = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly IUiDispatcher _dispatcher;
    private readonly Dictionary<string, WidgetState> _states = [];
    private readonly List<RecentEvent> _events = [];

    private IntPtr _hwnd;
    private AppWindow _appWindow = null!;
    private bool _visible;

    /// <summary>请求打开某 Widget 的 L3 详情窗（B-603 接线）。</summary>
    public event Action<WidgetState>? DetailRequested;

    /// <summary>Pin/Unpin 落库后通知（B-702：L0 宿主重建 tile 集）。</summary>
    public event Action? PinsChanged;

    public QuickPanelWindow(BeaconRuntime runtime, IUiDispatcher dispatcher)
    {
        InitializeComponent();
        // RFC §6.4：Flyout 语义——WinUI 栈规范「Acrylic 用于 flyout/瞬态面」，系统亚克力背板
        SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config); // B-706：渲染时实时读 appearance.SeverityColors
        _dispatcher = dispatcher;

        runtime.Bus.Subscribe<WidgetStateChanged>(evt => dispatcher.Post(() => OnStateChanged(evt.State, record: true)));
        runtime.Bus.Subscribe<WidgetsInvalidated>(evt => dispatcher.Post(() => OnWidgetsInvalidated(evt.WidgetIds)));
        runtime.Bus.Subscribe<AggregateStatusChanged>(_ => dispatcher.Post(UpdateHeader));

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
        NativeMethods.AddWindowExStyle(_hwnd, NativeMethods.WS_EX_TOOLWINDOW);

        // 设计值是 DIP，AppWindow.Resize 收物理像素——按 DPI 换算，否则高缩放下内容被裁
        var dpi = GetDpi();
        _appWindow.Resize(new SizeInt32((int)Math.Round(PanelWidth * dpi), (int)Math.Round(PanelHeight * dpi)));
        _appWindow.Hide();
        _visible = false;

        // B-601 验收：ESC 可完整关闭（global:: 限定：Beacon.App.Windows 会遮蔽 Windows 根命名空间）
        var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = global::Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, _) => Hide();
        ((FrameworkElement)Content).KeyboardAccelerators.Add(escape);

        UpdateHeader();
        UpdateOverview();
        RebuildRecent();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // 失焦自动关闭（RFC §6.4：Flyout 语义）
        if (_visible && args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Hide();
        }
    }

    /// <summary>热键切换显隐。</summary>
    public void Toggle()
    {
        if (_visible)
        {
            Hide();
        }
        else
        {
            PositionNearPrimaryCapsule();
            _appWindow.Show();
            Activate(); // L2 需要键盘（ESC 关闭、Tab 导航）
            _visible = true;
        }
    }

    public void Hide()
    {
        _appWindow.Hide();
        _visible = false;
    }

    private double GetDpi() => NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

    /// <summary>运行时主题——动态构建的行/胶囊配色随之切换（静态部分走 XAML ThemeResource）。</summary>
    private bool IsDarkTheme => ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark;

    private void PositionNearPrimaryCapsule()
    {
        var work = MonitorService.Primary()?.WorkPx ?? _fallbackWorkArea;
        var dpi = GetDpi(); // work 是物理像素，偏移设计值是 DIP——同单位换算
        var x = work.Right - (int)Math.Round(PanelWidth * dpi) - (int)Math.Round(24 * dpi);
        var y = work.Bottom - (int)Math.Round(PanelHeight * dpi) - (int)Math.Round(140 * dpi); // 胶囊（右下角）上方
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
        RebuildRecent();
    }

    private void UpdateHeader()
    {
        var warn = _states.Values.Count(s => s.Severity == Severity.Warning);
        var error = _states.Values.Count(s => s.Severity >= Severity.Error);
        OverallSummary.Text = warn == 0 && error == 0
            ? "all clear"
            : $"{warn} warn · {error} error";
        var overall = _states.Values.Count == 0
            ? Severity.Success
            : _states.Values.Max(s => s.Severity);
        OverallLight.Fill = new SolidColorBrush(_palette.SeverityColor(overall));
    }

    private void UpdateOverview()
    {
        SourceCounts.Children.Clear();
        foreach (var group in _states.Values
            .GroupBy(state => SourceOf(state.WidgetType))
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var worst = group.Max(state => state.Severity);
            SourceCounts.Children.Add(MakeChip(group.Key, group.Count(), worst));
        }
    }

    private static string SourceOf(string widgetType) => widgetType switch
    {
        "github.pull_requests" => "GitHub",
        "github.actions.runs" => "CI",
        var type when type.StartsWith("github.", StringComparison.Ordinal) => "GitHub",
        var type when type.StartsWith("http.", StringComparison.Ordinal) => "HTTP",
        var type when type.StartsWith("bigmodel.", StringComparison.Ordinal) => "GLM",
        var type => Capitalize(type.Split('.')[0]),
    };

    private static string Capitalize(string source)
        => source.Length == 0 ? "Other" : char.ToUpperInvariant(source[0]) + source[1..];

    private Border MakeChip(string label, int count, Severity worst)
    {
        var dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(_palette.SeverityColor(worst)) };
        var text = new TextBlock
        {
            Text = $"{label} {count}",
            Foreground = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(255, 241, 245, 249) : SeverityPalette.Rgb(255, 30, 41, 59)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        row.Children.Add(dot);
        row.Children.Add(text);
        return new Border
        {
            Background = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(28, 255, 255, 255) : SeverityPalette.Rgb(20, 15, 23, 42)),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 8, 3),
            Child = row,
        };
    }

    private void RebuildRecent()
    {
        RecentList.Items.Clear();
        foreach (var evt in _events)
        {
            RecentList.Items.Add(MakeEventRow(evt));
        }
    }

    private ListViewItem MakeEventRow(RecentEvent evt)
    {
        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new SolidColorBrush(_palette.SeverityColor(evt.State.Severity,
                widgetOverride: _runtime.Config.FindWidget(evt.State.WidgetId)?.ColorOverride)),
        };
        var time = new TextBlock
        {
            Text = evt.At.ToString("HH:mm:ss"),
            Foreground = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(255, 148, 163, 184) : SeverityPalette.Rgb(255, 100, 116, 139)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text = evt.State.Summary,
            Foreground = new SolidColorBrush(IsDarkTheme ? SeverityPalette.Rgb(255, 226, 232, 240) : SeverityPalette.Rgb(255, 30, 41, 59)),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 210,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        row.Children.Add(dot);
        row.Children.Add(time);
        row.Children.Add(label);

        var item = new ListViewItem { Content = row, Padding = new Thickness(2, 3, 2, 3), Tag = evt };
        var pinned = _runtime.Config.FindWidget(evt.State.WidgetId)?.Pinned == true;
        var pin = new MenuFlyoutItem { Text = pinned ? "Unpin from desktop" : "Pin to desktop" };
        pin.Click += (_, _) => PinWidget(evt.State.WidgetId);
        item.ContextFlyout = new MenuFlyout { Items = { pin } };
        return item;
    }

    private void OnRecentItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListViewItem { Tag: RecentEvent evt })
        {
            DetailRequested?.Invoke(evt.State); // B-603：打开 L3 详情窗
        }
    }

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
        PinsChanged?.Invoke();
    }
}

/// <summary>中性灰阶色（L1/L2/L3 文本与底色共用）；级别色已由 UiPalette 承载（B-706）。</summary>
internal static class SeverityPalette
{
    public static global::Windows.UI.Color Rgb(byte a, byte r, byte g, byte b) => new() { A = a, R = r, G = g, B = b };
}
