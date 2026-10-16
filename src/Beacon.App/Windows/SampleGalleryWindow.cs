using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Windows;

/// <summary>
/// 组件视觉示例页（Phase 2 末行）：Components 全部工厂的实样目录——词汇件（Surface/Badge/Divider/
/// Metric/StatusCard/Link/Column/Feedback 四态）的证明面。从设置「关于」区进入；
/// 纯静态展示，不接数据、不回写配置。配色口径同 DetailWindow（暗色静态）。
/// </summary>
internal sealed class SampleGalleryWindow : Window
{
    // 与 DetailWindow 同口径：设计值 DIP，Loaded 后按内容实测 + DPI/非客户区补差（DpiLayoutMath）。
    private const double GalleryWidthDips = 520;
    private const double GalleryMinHeightDips = 320;
    private const double GalleryMaxHeightDips = 720;

    private readonly BeaconRuntime _runtime;
    private readonly UiPalette _palette;
    private readonly Brush _heading = new SolidColorBrush(Microsoft.UI.Colors.White);
    private readonly Brush _body = new SolidColorBrush(SeverityPalette.Rgb(255, 226, 232, 240));
    private readonly Brush _muted = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184));

    public SampleGalleryWindow(BeaconRuntime runtime, Action? closed = null)
    {
        _runtime = runtime;
        _palette = new UiPalette(runtime.Config); // 档位/状态色同正式窗解析链

        Title = "Beacon · 组件示例页";
        Content = BuildContent();
        ((FrameworkElement)Content).Loaded += OnContentLoaded;
        Closed += (_, _) => closed?.Invoke();
        Services.AppIcon.Apply(AppWindow, runtime.Logger);
    }

    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = NativeMethods.GetDpiForWindow(hwnd) / 96.0;
        var content = (FrameworkElement)Content;
        content.Measure(new global::Windows.Foundation.Size(GalleryWidthDips, double.PositiveInfinity));
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(hwnd);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32
        {
            Width = DpiLayoutMath.ToPx(GalleryWidthDips, dpi) + chromeWidth,
            Height = DpiLayoutMath.ClampedHeightPx(content.DesiredSize.Height, GalleryMinHeightDips, GalleryMaxHeightDips, dpi) + chromeHeight,
        });
    }

    private UIElement BuildContent()
    {
        var root = new StackPanel { Spacing = 10, Padding = new Thickness(18, 16, 18, 16) };
        root.Children.Add(new TextBlock
        {
            Text = "组件库实样",
            FontSize = DesignTokens.FontDisplay,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = _heading,
        });
        root.Children.Add(ComponentText.Body("全部工厂的静态样例；色板走 UiPalette 解析链，排版/几何走 DesignTokens。", _muted));

        AddTextIconSection(root);
        AddSurfaceSection(root);
        AddStatusMetricSection(root);
        AddKeyValueListSection(root);
        AddActionSection(root);
        AddLayoutSection(root);
        AddFeedbackSection(root);

        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
    }

    private void AddTextIconSection(StackPanel root)
    {
        root.Children.Add(Section("TEXT · ICON"));
        var row = ComponentLayout.Row(8);
        row.Children.Add(ComponentIcon.StatusDot(new SolidColorBrush(_palette.SeverityColor(Severity.Success)), 8));
        row.Children.Add(ComponentIcon.StatusDot(new SolidColorBrush(_palette.SeverityColor(Severity.Warning)), 8));
        row.Children.Add(ComponentIcon.StatusDot(new SolidColorBrush(_palette.SeverityColor(Severity.Error)), 8));
        row.Children.Add(ComponentStatus.Indicator(new SolidColorBrush(_palette.SeverityColor(Severity.Critical))));
        row.Children.Add(ComponentStatus.OfflineHollow(_muted));
        root.Children.Add(row);
        root.Children.Add(ComponentText.Caption("caption 次要说明（FontCaption）", _muted));
        root.Children.Add(ComponentText.Body("body 正文一行，超出 maxWidth 走省略号截断口径", _body, maxWidth: 260));
    }

    private void AddSurfaceSection(StackPanel root)
    {
        root.Children.Add(Section("SURFACE · BADGE · DIVIDER"));
        root.Children.Add(ComponentSurface.Card(
            ComponentText.Body("Card 卡面：RadiusCard + Space10 均匀内边距。", _body),
            new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40))));
        var badge = ComponentBadge.Count(new TextBlock
        {
            Text = "3",
            FontSize = DesignTokens.FontCaption,
            Foreground = _heading,
        }, new SolidColorBrush(_palette.SeverityColor(Severity.Error)));
        var badgeRow = ComponentLayout.Row(8);
        badgeRow.Children.Add(badge);
        badgeRow.Children.Add(ComponentText.Caption("Count 计数徽标（通知未读数口径）", _muted));
        root.Children.Add(badgeRow);
        root.Children.Add(ComponentDivider.Horizontal(_muted));
    }

    private void AddStatusMetricSection(StackPanel root)
    {
        root.Children.Add(Section("STATUS · METRIC · PROGRESS"));
        root.Children.Add(ComponentMetric.Value("7.2", "GB", _heading));
        root.Children.Add(ComponentMetric.Value("12", null, _heading, DesignTokens.FontTitle));
        foreach (var fraction in new[] { 0.25, 0.85, 0.99 })
        {
            var (track, fill) = ComponentProgress.Bar(fraction, 300);
            fill.Fill = new SolidColorBrush(ComponentProgress.FillColor(fraction,
                _palette.SeverityColor(Severity.Success), tier => _palette.SeverityColor(tier)));
            var barHost = new Grid();
            barHost.Children.Add(track);
            barHost.Children.Add(fill);
            root.Children.Add(barHost);
        }
        root.Children.Add(ComponentText.Caption("进度档位：≥80% warning、≥98% error（Core ProgressTiers 单源）", _muted));
    }

    private void AddKeyValueListSection(StackPanel root)
    {
        root.Children.Add(Section("KEY-VALUE · LIST · CARD"));
        var table = ComponentList.Vertical();
        table.Children.Add(ComponentKeyValue.Row("repo", "cuihairu/beacon", _body));
        table.Children.Add(ComponentKeyValue.Row("branch", "main", _body));
        table.Children.Add(ComponentKeyValue.Row("duration", "4m12s", _body));
        root.Children.Add(table);

        var cardHeader = ComponentLayout.Row(8);
        cardHeader.Children.Add(ComponentStatus.Indicator(new SolidColorBrush(_palette.SeverityColor(Severity.Warning))));
        cardHeader.Children.Add(ComponentText.Body("StatusCard：单对象状态摘要", _heading));
        root.Children.Add(ComponentCard.Status(cardHeader,
            ComponentText.Body("头行（指示灯 + 标题）+ 摘要行，卡面同设置卡片口径。", _muted),
            new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40))));
    }

    private void AddActionSection(StackPanel root)
    {
        root.Children.Add(Section("ACTION"));
        var row = ComponentLayout.Row(8);
        row.Children.Add(ComponentAction.Button("Action Button", async () => await Task.CompletedTask));
        row.Children.Add(ComponentAction.Link("Link（项目主页）", new Uri("https://github.com/cuihairu/beacon")));
        var menuHost = ComponentText.Body("Action Menu：右键本行", _body);
        menuHost.ContextFlyout = ComponentAction.Menu(
            ComponentAction.Item("菜单项一", () => { }),
            ComponentAction.Item("菜单项二", () => { }));
        root.Children.Add(row);
        root.Children.Add(menuHost);
    }

    private void AddLayoutSection(StackPanel root)
    {
        root.Children.Add(Section("LAYOUT"));
        var columns = ComponentLayout.Columns(6, GridLength.Auto, new GridLength(1, GridUnitType.Star));
        columns.Children.Add(ComponentText.Caption("标签列", _muted));
        columns.Children.Add(ComponentText.Body("Columns 列网格：Auto + Star（PinnedHost 主行口径）", _body));
        root.Children.Add(columns);

        // Column 竖排 + When 条件显示（Phase 5 行 2 收编：条件从写死 if 走 Core
        // ComponentVisibility 状态字段求值；两条同条件行各喂一份状态样例，可见性由右值决定）
        var column = ComponentLayout.Column(6);
        column.Children.Add(ComponentText.Body("Column 竖排容器：通用竖列（DetailWindow 主列口径）", _body));
        var errorState = new WidgetState
        {
            WidgetId = "sample",
            WidgetType = "sample",
            ConnectionId = "sample",
            Severity = Severity.Error,
            Summary = "sample",
        };
        var visibleRow = ComponentText.Caption("severity>=error → 本行可见（Error 样例）", _muted);
        visibleRow.Visibility = ComponentLayout.When(ComponentVisibility.Evaluate("severity>=error", errorState));
        var hiddenRow = ComponentText.Caption("severity>=error → 本行隐藏（Success 样例）", _muted);
        hiddenRow.Visibility = ComponentLayout.When(
            ComponentVisibility.Evaluate("severity>=error", errorState with { Severity = Severity.Success }));
        column.Children.Add(visibleRow);
        column.Children.Add(hiddenRow);
        column.Children.Add(ComponentText.Caption(
            "同条件两条目各喂一份状态样例：条件显示按状态字段驱动而非写死", _muted));
        root.Children.Add(column);
    }

    private void AddFeedbackSection(StackPanel root)
    {
        root.Children.Add(Section("FEEDBACK"));
        root.Children.Add(ComponentFeedback.Empty("Empty：无数据/无事件/尚未配置", _muted));
        root.Children.Add(ComponentFeedback.Loading("Loading：加载中…", _muted));
        root.Children.Add(ComponentFeedback.Error("Error：失败原因与错误摘要（换行正文）", _muted));
        root.Children.Add(ComponentFeedback.Offline("Offline：Last update 14:32", _muted, _muted));
    }

    private TextBlock Section(string text)
        => ComponentLayout.SectionLabel(text, new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)));
}
