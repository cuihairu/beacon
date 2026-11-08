using System.Globalization;
using System.Reflection;
using System.Threading;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Abstractions;
using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Storage.Pickers;

namespace Beacon.App.Windows;

/// <summary>
/// Settings 窗口（B-801，RFC §38/§42，PowerToys 形态配置中心）：左侧模块目录（每模块独立 icon、
/// Provider 模块带启用开关——开启才见配置页），右侧对应页面：常规（热键/自启/主题/透明度/胶囊）、
/// 外观（级别色+动效）、GitHub/智谱 GLM/自定义 HTTP（连接 CRUD + 组件向导，类型锁定模块）、
/// 高级（导入导出+通知规则）。启停经 ConnectionConfig.Enabled 落库，宿主跳过刷新、状态即时失效。
/// 凭据只进 DPAPI（严禁明文 JSON）；改动即存 config.json。
/// </summary>
internal sealed partial class SettingsWindow : Window
{
    /// <summary>配置中心模块行（PowerToys 形态）：Provider 模块 ConnectionType 非空 → 带启用开关。</summary>
    private sealed record ModuleDef(string Key, string DisplayName, string Subtitle, string Glyph, string? ConnectionType);

    private readonly BeaconRuntime _runtime;
    private readonly MotionEngine _motion;
    private SampleGalleryWindow? _gallery; // 组件中心单实例（关于区入口）

    private TextBox _hotkeyBox = null!;
    private ToggleSwitch _startupToggle = null!;
    private ComboBox _themeBox = null!;
    private ComboBox _languageBox = null!;
    private ComboBox _pollBox = null!;
    private ComboBox _pinModeBox = null!; // 悬浮形态档（信息密集组件 panel|floating）：收编到组件中心「展示载体」区
    private ComboBox _islandModeBox = null!; // 岛收起态展示方式档（ISLAND-D1 single|stack|carousel）：岛组内
    private Slider _opacitySlider = null!;
    private ToggleSwitch _capsuleToggle = null!;
    private ToggleSwitch _edgeCollapseToggle = null!;

    private StackPanel _connectionList = null!;
    private ComboBox _connTypeBox = null!;
    private TextBox _connIdBox = null!;
    private string _lastPrefilledConnectionId = "";
    private TextBox _connEndpointBox = null!;
    private TextBox? _connUsageBox; // qwen 专属：自定义用量端点（官方额度口未开放，网关/代理口可填）
    private PasswordBox? _connConsoleBox; // mimo 专属：控制台登录 cookie（api-platform_ph，录了才显示套餐用量）
    private PasswordBox _connTokenBox = null!;

    // 自定义动作库编辑器（actions.json，高级页）：局域网打包机「触发打包」等用户自建动作
    private ComboBox? _actionTypeBox;
    private TextBox? _actionNameBox;
    private ToggleSwitch? _actionConfirmBox;
    private TextBox? _actionScopeBox;
    private TextBox? _actionUrlBox;
    private TextBox? _actionMethodBox;
    private TextBox? _actionBodyBox;
    private TextBox? _actionCommandBox;
    private TextBox? _actionArgsBox;
    private TextBox? _actionWorkDirBox;
    private StackPanel? _actionList;
    private TextBlock _actionFeedback = null!; // 与 _connFeedback 同风格：页面构建时赋值
    private Button _connSaveButton = null!;
    private TextBlock _connFeedback = null!;
    private string? _editingConnectionId;

    private StackPanel _widgetList = null!;
    private ComboBox _widgetTypeBox = null!;
    private ComboBox _widgetConnectionBox = null!;
    private ComboBox _widgetTierBox = null!;
    private ComboBox _widgetIntervalBox = null!;
    private StackPanel _widgetFields = null!;
    private TextBox _widgetColorBox = null!;
    private ToggleSwitch _widgetPinBox = null!;
    private TextBlock _widgetFeedback = null!;
    // 组件实例面（Phase 5 行 4）：组件树 JSON 编辑 + 尺寸档 + 桌面布局区——载入/校验/保存，清空=回传统渲染
    private WidgetConfig? _componentEditTarget;
    private TextBlock _componentEditHeader = null!;
    private TextBox _componentJsonBox = null!;
    private TextBlock _componentFeedback = null!;
    private ComboBox _sizeTierBox = null!;       // 尺寸档（TileSizing.SizeTierKey，三档沿 UiDensity 词汇）
    private ComboBox _layoutAnchorBox = null!;   // 桌面布局：锚点角
    private TextBox _layoutOffsetXBox = null!;   // 桌面布局：偏移 X（DIP）
    private TextBox _layoutOffsetYBox = null!;   // 桌面布局：偏移 Y（DIP）
    private ToggleSwitch _layoutCollapsedToggle = null!; // 桌面布局：吸边收起
    private ComboBox _layoutMonitorBox = null!;  // 桌面布局：显示器
    private bool _instanceLoading;               // 实例面载入回填守卫（SelectionChanged 不触发保存链）
    // 可视化树编辑器（Phase 5 行 4 第三片）：TreeView 结构化编辑——树操作回写 JSON 框，
    // 保存仍走 SaveComponentTree 单一校验落盘链（ComponentTreeValidator 前置 + UpsertWidget 二次防呆）
    private TreeView _componentTree = null!;
    private ComboBox _nodeTypeBox = null!;
    private TextBox _nodeIdBox = null!;
    private TextBox _nodePropsBox = null!;
    private TextBlock _nodeFeedback = null!;
    private Dictionary<string, ComponentDescriptor> _editSurfaces = []; // 树编辑工作副本（JSON 框是落盘真源）

    private readonly Dictionary<string, TextBox> _colorBoxes = [];
    private TextBlock _appearanceFeedback = null!;
    private ComboBox _motionModeBox = null!;
    private Slider _motionIntensitySlider = null!;
    private ComboBox _progressStyleBox = null!;
    private ComboBox _densityBox = null!;
    private Border _previewHost = null!;
    private Ellipse _previewLight = null!;
    private TextBlock _importFeedback = null!;

    /// <summary>常规设置落库后触发（App 侧重注册热键/自启/胶囊显隐/透明度）。</summary>
    public event Action? SettingsApplied;

    /// <summary>Pin/Unpin 或组件增删落库后触发（App 侧重建 L0 tile 集）。</summary>
    public event Action? PinsChanged;

    public SettingsWindow(BeaconRuntime runtime)
    {
        _runtime = runtime;
        _motion = new MotionEngine(runtime.Config); // 预览与运行时同引擎：设置改档即刻反映到预览
        Title = "Beacon 设置";
        Content = BuildRoot();
        // 两栏目录形态需要比默认更宽：WinUI Window 没有 Width/Height，走 AppWindow（物理像素 × DPI）。
        // 2026-10-10 用户令「窗口本身可调整大小并记住尺寸」：config 记忆上次的窗口 DIP 尺寸，
        // 0/非法值落默认 880×640；新值由 root.SizeChanged + 800ms 防抖落盘（ScheduleWindowSizeSave）
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = Infrastructure.NativeMethods.GetDpiForWindow(hwnd) / 96.0;
        var width = _runtime.Config.App.SettingsWindowWidth is double w && w >= 600 ? w : 880;
        var height = _runtime.Config.App.SettingsWindowHeight is double h && h >= 400 ? h : 640;
        // 记忆尺寸记的是 Bounds（客户区 DIP，SaveWindowSizeNow），AppWindow.Resize 设的是外框——
        // 两者差一条标题栏+边框，直接喂客户区需求会让每次开窗都比记忆尺寸矮一截、底部内容裁在
        // 可调边距底下（2026-10-14 缺陷单同型，与 tile 进度条裁切同根因）。按 ChromeSizePx 补回差值。
        var (chromeWidth, chromeHeight) = Infrastructure.NativeMethods.ChromeSizePx(hwnd);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32
        {
            Width = DpiLayoutMath.ToPx(width, dpi) + chromeWidth,
            Height = DpiLayoutMath.ToPx(height, dpi) + chromeHeight,
        });
        Services.AppIcon.Apply(AppWindow, _runtime.Logger); // 任务栏/Alt-Tab 图标（unpackaged 不会自动用 exe 图标）
    }

    private UIElement BuildRoot()
    {
        _modules =
        [
            new ModuleDef("general", "常规", "热键 · 自启 · 主题 · 胶囊", "\ue713", null),
            new ModuleDef("appearance", "外观", "级别色 · 动效", "\ue790", null),
            new ModuleDef("components", "组件中心", "全部组件按类型分组 · 勾选显隐", "\ue71d", null),
            new ModuleDef("github", "GitHub", "Pull Requests · Actions", "\ue943", "github"),
            new ModuleDef("jenkins", "Jenkins", "构建状态 · 开源标准 API", "\ue9d5", "jenkins"),
            new ModuleDef("machine", "本机", "本机资源 · 网络 · Git · 零凭据", "\ue772", "machine"),
            new ModuleDef("herald", "Herald", "事件台账 · 订阅源 · 投递失败", "\ue7be", "herald"),
            new ModuleDef("bigmodel", "智谱 GLM", "Coding Plan 额度", "\ue945", "bigmodel"),
            new ModuleDef("ark", "火山方舟", "Coding Plan Pro 额度", "\uE7C3", "ark"),
            new ModuleDef("claude", "Claude Code", "本机用量 · 零凭据", "\ue8bd", "claude"),
            new ModuleDef("kimi", "Kimi For Coding", "套餐余量 · 5h/周", "\ue823", "kimi"),
            new ModuleDef("moonshot", "Moonshot 开放平台", "开放平台余额 · 与 Kimi 不通用", "\ue8c7", "moonshot"),
            new ModuleDef("deepseek", "DeepSeek", "开放平台余额", "\ue7bf", "deepseek"),
            new ModuleDef("mimo", "小米 MiMo", "模型目录 · 用量待官方开放", "\uE7F8", "mimo"),
            new ModuleDef("codex", "OpenAI Codex", "本机会话统计 · 零凭据", "\uE99A", "codex"),
            new ModuleDef("copilot", "GitHub Copilot", "套餐配额 · 高级请求", "\uE99B", "copilot"),
            new ModuleDef("opencode", "OpenCode Go", "Console Budgets · 月度用量", "\uEA8F", "opencode"),
            new ModuleDef("qwen", "阿里千问", "百炼 Token Plan · 额度/重置", "\uE753", "qwen"),
            new ModuleDef("sensenova", "商汤", "Token Plan 积分池 · 5h/7d", "\uE706", "sensenova"),
            new ModuleDef("http", "自定义 HTTP", "任意状态接口 · 点路径映射", "\ue774", "http"),
            new ModuleDef("actions", "自定义动作", "HTTP · Webhook · 本机命令", "\ue945", null), // UI/UX 审计：原与诊断/导入导出/通知规则堆一页，动作编辑器占首屏把后半压到视口外
            new ModuleDef("advanced", "高级", "诊断 · 导入导出 · 通知规则", "\ue90f", null),
        ];

        // 2026-10-10 修：模块列表包 ScrollViewer（16 项在 640 高窗口必溢出，此前直接被裁掉看不见）
        _leftPanel = new StackPanel { Spacing = 2 };
        var leftHost = new ScrollViewer
        {
            // 常驻 Visible：底部还有模块这件事必须一眼可见，禁止靠拖窗口发现
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 10, 0),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(leftHost, "settings-left-host"); // UIA 取证/自动化入口
        leftHost.Content = _leftPanel;

        // 2026-10-10 用户令「设置搜索框」：输入过滤模块+字段（DisplayName/Subtitle），回车跳第一个命中。
        // 过滤改行 Visibility（不重建），计数标题同步「N/M」；左栏纵向堆叠 = 搜索框(自适高) + 列表(撑满)
        _searchBox = new TextBox
        {
            PlaceholderText = "搜索设置（模块/字段），回车跳转",
            FontSize = DesignTokens.FontBody,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_searchBox, "settings-search");
        // 可访问性（Phase 5 行 5）：占位文本输入后消失且不作 Name，屏幕阅读器只念「编辑」——补显式 Name
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_searchBox, "搜索设置（模块/字段）");
        _searchBox.TextChanged += (_, _) => ApplyModuleFilter();
        _searchBox.KeyDown += (sender, e) =>
        {
            if (e.Key is global::Windows.System.VirtualKey.Enter && _firstFilterHit is { } hit)
            {
                SelectModule(hit);
                if (sender is TextBox box)
                {
                    box.Focus(Microsoft.UI.Xaml.FocusState.Keyboard); // 跳转后焦点留在搜索框，可继续改词
                }
            }
        };
        var leftColumn = new Grid();
        leftColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        leftColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_searchBox, 0);
        Grid.SetRow(leftHost, 1);
        leftColumn.Children.Add(_searchBox);
        leftColumn.Children.Add(leftHost);

        _rightHost = new ScrollViewer
        {
            // 左内边距 16：右栏内容不贴分隔条黑线（2026-10-10 用户令「常规不要贴着黑线，保持一点距离」）；
            // 与左栏 ScrollViewer 右侧 10 的滚动条留白对称，两栏各自离中线有呼吸位
            Padding = new Thickness(16, 0, 0, 0),
            // UI/UX 审计：Auto 档在内容略溢出时只出细滚动条，底部还有内容这件事无提示——常驻 Visible
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        // 2026-10-10 用户令三段式：左导航(可拖宽) | GridSplitter | 右内容。列宽钳位 [200, 420]
        // （用户规格「固定起始 200」/右 min 420），初值读 config.SettingsSidebarWidth（拖动落盘、启动恢复）
        _leftColumn = new ColumnDefinition
        {
            Width = new GridLength(Math.Clamp(
                _runtime.Config.App.SettingsSidebarWidth is double saved && saved > 0 ? saved : 264,
                LeftColumnMin, LeftColumnMax)),
        };
        var splitter = BuildColumnSplitter();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(_leftColumn);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(leftColumn, 0);
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(_rightHost, 2);
        grid.Children.Add(leftColumn);
        grid.Children.Add(splitter);
        grid.Children.Add(_rightHost);

        RebuildModuleRows();
        SelectModule(_modules[0]);

        var root = new Grid
        {
            Padding = new Thickness(16, 14, 16, 14),
            Children = { grid },
        };
        root.SizeChanged += (_, _) => ScheduleWindowSizeSave(); // 拖窗/初始布局都进防抖，800ms 静止才落盘
        return root;
    }
    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontBody,
        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
        TextWrapping = TextWrapping.Wrap,
    };

    private static StackPanel PageShell() => new() { Spacing = 14, Padding = new Thickness(4, 2, 4, 16) };
    private static void SetVisible(FrameworkElement? element, bool visible)
    {
        if (element is { } box)
        {
            box.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontHeader,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 8, 0, 0),
    };

    private static void Feedback(TextBlock target, string message, bool error)
    {
        target.Text = message;
        target.Foreground = new SolidColorBrush(error
            ? SeverityPalette.Rgb(255, 248, 81, 73)
            : SeverityPalette.Rgb(255, 139, 148, 158));
    }

    /// <summary>取纯字符串项（如刷新档/类型直加 Items 的组合框）的当前值。</summary>
    private static string? SelectedString(ComboBox box) => box.SelectedItem?.ToString();

    private static int IndexOfTag(ComboBox box, string? tag)
    {
        for (var index = 0; index < box.Items.Count; index++)
        {
            if (box.Items[index] is ComboBoxItem { Tag: { } itemTag } && string.Equals(itemTag.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>ComboBox 选中项 Tag（可为 null：页面未构建/无选中时调用方以 ?? 兜底——CS8604 防运行时 NRE）。</summary>
    private static string? TagOf(ComboBox? box)
        => box?.SelectedItem is ComboBoxItem { Tag: { } tag } ? tag.ToString() : null;
}
