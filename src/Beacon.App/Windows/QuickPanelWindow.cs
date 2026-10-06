using Beacon.App.Infrastructure;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Beacon.App.Windows;

/// <summary>
/// L2 Quick Panel（RFC §6.4）。当前为 B-103 占位窗口：热键显隐、失焦自动隐藏；
/// Overview / Recent Events / Actions 在 B-601..603 实现。
/// </summary>
public sealed partial class QuickPanelWindow : Window
{
    private const int PanelWidth = 260;
    private const int PanelHeight = 160;

    private readonly NativeMethods.RECT _fallbackWorkArea = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

    private IntPtr _hwnd;
    private AppWindow _appWindow = null!;
    private bool _visible;

    public QuickPanelWindow()
    {
        InitializeComponent();
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

        _appWindow.Resize(new SizeInt32(PanelWidth, PanelHeight));
        _appWindow.Hide();
        _visible = false;
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

    private void PositionNearPrimaryCapsule()
    {
        var work = MonitorService.Primary()?.WorkPx ?? _fallbackWorkArea;
        var x = work.Right - PanelWidth - 24;
        var y = work.Bottom - PanelHeight - 140; // 胶囊（右下角）上方
        _appWindow.Move(new PointInt32(x, y));
    }
}
