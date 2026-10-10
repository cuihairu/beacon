using System.Globalization;
using System.Reflection;
using System.Threading;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Abstractions;
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
internal sealed class SettingsWindow : Window
{
    /// <summary>配置中心模块行（PowerToys 形态）：Provider 模块 ConnectionType 非空 → 带启用开关。</summary>
    private sealed record ModuleDef(string Key, string DisplayName, string Subtitle, string Glyph, string? ConnectionType);

    private readonly BeaconRuntime _runtime;
    private readonly MotionEngine _motion;

    private TextBox _hotkeyBox = null!;
    private ToggleSwitch _startupToggle = null!;
    private ComboBox _themeBox = null!;
    private ComboBox _pollBox = null!;
    private ComboBox _pinModeBox = null!;
    private Slider _opacitySlider = null!;
    private ToggleSwitch _capsuleToggle = null!;
    private ToggleSwitch _numericFloatingToggle = null!;

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

    // PowerToys 形态：每类连接的整体开关
    private ToggleSwitch _githubEnabled = null!;
    private ToggleSwitch _httpEnabled = null!;
    private ToggleSwitch _bigmodelEnabled = null!;

    private StackPanel _widgetList = null!;
    private ComboBox _widgetTypeBox = null!;
    private ComboBox _widgetConnectionBox = null!;
    private ComboBox _widgetTierBox = null!;
    private ComboBox _widgetIntervalBox = null!;
    private StackPanel _widgetFields = null!;
    private TextBox _widgetColorBox = null!;
    private ToggleSwitch _widgetPinBox = null!;
    private TextBlock _widgetFeedback = null!;

    private readonly Dictionary<string, TextBox> _colorBoxes = [];
    private TextBlock _appearanceFeedback = null!;
    private ComboBox _motionModeBox = null!;
    private Slider _motionIntensitySlider = null!;
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
        // 两栏目录形态需要比默认更宽：WinUI Window 没有 Width/Height，走 AppWindow（物理像素 × DPI）
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = Infrastructure.NativeMethods.GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32
        {
            Width = (int)Math.Round(880 * dpi),
            Height = (int)Math.Round(640 * dpi),
        });
        Services.AppIcon.Apply(AppWindow, _runtime.Logger); // 任务栏/Alt-Tab 图标（unpackaged 不会自动用 exe 图标）
    }

    private UIElement BuildRoot()
    {
        _modules =
        [
            new ModuleDef("general", "常规", "热键 · 自启 · 主题 · 胶囊", "\ue713", null),
            new ModuleDef("appearance", "外观", "级别色 · 动效", "\ue790", null),
            new ModuleDef("github", "GitHub", "Pull Requests · Actions", "\ue943", "github"),
            new ModuleDef("bigmodel", "智谱 GLM", "Coding Plan 额度", "\ue945", "bigmodel"),
            new ModuleDef("ark", "火山方舟", "Coding Plan Pro 额度", "\uE7C3", "ark"),
            new ModuleDef("claude", "Claude Code", "本机用量 · 零凭据", "\ue8bd", "claude"),
            new ModuleDef("kimi", "Kimi For Coding", "套餐余量 · 5h/周", "\ue823", "kimi"),
            new ModuleDef("deepseek", "DeepSeek", "开放平台余额", "\ue7bf", "deepseek"),
            new ModuleDef("mimo", "小米 MiMo", "模型目录 · 用量待官方开放", "\uE7F8", "mimo"),
            new ModuleDef("codex", "OpenAI Codex", "本机会话统计 · 零凭据", "\uE99A", "codex"),
            new ModuleDef("copilot", "GitHub Copilot", "套餐配额 · 高级请求", "\uE99B", "copilot"),
            new ModuleDef("opencode", "OpenCode Go", "Console Budgets · 月度用量", "\uEA8F", "opencode"),
            new ModuleDef("qwen", "阿里千问", "百炼 Token Plan · 额度/重置", "\uE753", "qwen"),
            new ModuleDef("http", "自定义 HTTP", "任意状态接口 · 点路径映射", "\ue774", "http"),
            new ModuleDef("advanced", "高级", "导入导出 · 通知规则", "\ue90f", null),
        ];

        _leftPanel = new StackPanel { Spacing = 2, MinWidth = 232, Margin = new Thickness(0, 0, 14, 0) };
        _rightHost = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_rightHost, 1);
        grid.Children.Add(_leftPanel);
        grid.Children.Add(_rightHost);

        RebuildModuleRows();
        SelectModule(_modules[0]);

        return new Grid
        {
            Padding = new Thickness(16, 14, 16, 14),
            Children = { grid },
        };
    }

    // —— PowerToys 形态：模块目录（左）与页面路由（右） ——

    private List<ModuleDef> _modules = [];
    private readonly Dictionary<string, Button> _moduleRows = [];
    private readonly Dictionary<string, ToggleSwitch> _moduleToggles = [];
    private StackPanel _leftPanel = null!;
    private ScrollViewer _rightHost = null!;
    private string _selectedKey = "";
    private bool _syncingToggles;

    /// <summary>当前页作用域：Provider 页锁定连接类型与组件描述符；常规/外观/高级页为 null（全量）。</summary>
    private string? _scopeType;
    private IReadOnlyList<WidgetTypeDescriptor>? _scopeDescriptors;

    private void RebuildModuleRows()
    {
        _leftPanel.Children.Clear();
        _moduleRows.Clear();
        _moduleToggles.Clear();
        _leftPanel.Children.Add(new TextBlock
        {
            Text = "配置中心",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Margin = new Thickness(10, 2, 0, 8),
        });
        foreach (var module in _modules)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            content.Children.Add(MakeModuleIcon(module));
            var nameStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock { Text = module.DisplayName, FontSize = 13 });
            nameStack.Children.Add(new TextBlock
            {
                Text = module.Subtitle,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            content.Children.Add(nameStack);

            if (module.ConnectionType is { } type)
            {
                var toggle = new ToggleSwitch
                {
                    OnContent = "",
                    OffContent = "",
                    IsOn = TypeEnabled(type),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                toggle.Toggled += (_, _) =>
                {
                    if (_syncingToggles)
                    {
                        return;
                    }
                    _runtime.SetConnectionTypeEnabled(type, toggle.IsOn);
                    if (_selectedKey == module.Key)
                    {
                        RebuildConnections();
                        RebuildWidgets();
                    }
                    PinsChanged?.Invoke(); // 2026-10-09 修:模块停用/启用必须重建 L0(悬浮窗/宿主)——漏发导致停用后悬浮框不消失
                };
                _moduleToggles[type] = toggle;
                content.Children.Add(toggle);
            }

            var row = new Button
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 7, 8, 7),
                CornerRadius = new CornerRadius(6),
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(row, $"module-{module.Key}"); // UIA 取证/自动化入口
            var captured = module;
            row.Click += (_, _) => SelectModule(captured);
            _moduleRows[module.Key] = row;
            _leftPanel.Children.Add(row);
        }
    }

    /// <summary>模块图标（PowerToys 形态：每模块独立 icon）——品牌图优先（BrandIconFactory 本地内置），
    /// 未收录品牌退色块 + Segoe Fluent 字形兜底；品牌渲染失败同样退字形并落日志（单块失败不拖死整页）。</summary>
    private Border MakeModuleIcon(ModuleDef module)
    {
        var tint = module.ConnectionType switch
        {
            "github" => SeverityPalette.Rgb(255, 110, 118, 129),
            "bigmodel" => SeverityPalette.Rgb(255, 56, 89, 255),
            "ark" => SeverityPalette.Rgb(255, 41, 112, 255), // 火山引擎蓝
            "mimo" => SeverityPalette.Rgb(255, 255, 105, 0), // 小米橙 #FF6900
            "qwen" => SeverityPalette.Rgb(255, 255, 106, 0), // 阿里橙 #FF6A00（Simple Icons qwen）
            "codex" => SeverityPalette.Rgb(255, 16, 22, 34), // OpenAI 黑（近墨）
            "copilot" => SeverityPalette.Rgb(255, 142, 78, 198), // Copilot 紫渐变主色 #8E4EC6
            "opencode" => SeverityPalette.Rgb(255, 13, 148, 136), // OpenCode 青 #0D9488
            "http" => SeverityPalette.Rgb(255, 63, 185, 80),
            _ => SeverityPalette.Rgb(255, 148, 163, 184),
        };
        // icon 前景按 tint 亮度选白/黑（≥4.5:1）：mimo 橙/http 绿/opencode 青等亮底白字仅 ≈2.9:1，必须用黑
        var iconForeground = ThemeColors.IconOnTint(tint);
        var content = BrandIconFactory.TryCreate(module.ConnectionType, 15, iconForeground, _runtime.Logger)
            ?? new FontIcon
            {
                Glyph = module.Glyph,
                FontSize = 15,
                Foreground = new SolidColorBrush(iconForeground),
            };
        return new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(tint),
            VerticalAlignment = VerticalAlignment.Center,
            Child = content,
        };
    }

    /// <summary>模块启用语义：该类型存在连接且全部 Enabled（无连接 = 未启用，开关引导去配置页）。</summary>
    private bool TypeEnabled(string connectionType)
    {
        var connections = _runtime.Config.Connections.Where(c => c.Type == connectionType).ToList();
        return connections.Count > 0 && connections.All(c => c.Enabled);
    }

    private void SelectModule(ModuleDef module)
    {
        _selectedKey = module.Key;
        _scopeType = module.ConnectionType;
        _scopeDescriptors = module.ConnectionType is { } type ? ModuleDescriptors(type) : null;
        foreach (var (key, row) in _moduleRows)
        {
            row.Background = new SolidColorBrush(key == module.Key
                ? SeverityPalette.Rgb(60, 127, 127, 127)
                : Microsoft.UI.Colors.Transparent);
        }
        _rightHost.Content = module.Key switch
        {
            "general" => BuildGeneralPage(),
            "appearance" => BuildAppearancePage(),
            "advanced" => BuildAdvancedPage(),
            _ => BuildProviderPage(module),
        };
    }

    /// <summary>模块下的组件描述符：按「组件类型前缀 = 连接类型」约定（github.* / http.* / bigmodel.*）。</summary>
    private static IReadOnlyList<WidgetTypeDescriptor> ModuleDescriptors(string connectionType)
        => AllWidgetDescriptors().Where(d => d.Type.StartsWith(connectionType + ".", StringComparison.Ordinal)).ToList();

    private static IReadOnlyList<WidgetTypeDescriptor> AllWidgetDescriptors()
        => [.. GitHubWidgetDescriptors.All, .. HttpWidgetDescriptors.All, .. BigModelWidgetDescriptors.All, .. ArkWidgetDescriptors.All, .. ClaudeWidgetDescriptors.All, .. DeepSeekWidgetDescriptors.All, .. KimiWidgetDescriptors.All, .. MiMoWidgetDescriptors.All, .. QwenWidgetDescriptors.All, .. CodexWidgetDescriptors.All, .. CopilotWidgetDescriptors.All, .. OpenCodeWidgetDescriptors.All];

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
        TextWrapping = TextWrapping.Wrap,
    };

    private static StackPanel PageShell() => new() { Spacing = 14, Padding = new Thickness(4, 2, 4, 16) };

    // —— 页面：常规 ——

    private UIElement BuildGeneralPage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("常规"));
        _hotkeyBox = new TextBox { Header = "全局热键（如 Ctrl+Alt+B）", Text = _runtime.Config.App.Hotkey, Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        _hotkeyBox.LostFocus += (_, _) => SaveGeneral();
        _startupToggle = new ToggleSwitch { Header = "开机自启", IsOn = _runtime.Config.App.LaunchOnStartup };
        _startupToggle.Toggled += (_, _) => SaveGeneral();
        _themeBox = new ComboBox { Header = "主题（重启生效）", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var item in new[] { ("system", "跟随系统"), ("light", "浅色"), ("dark", "深色") })
        {
            _themeBox.Items.Add(new ComboBoxItem { Content = item.Item2, Tag = item.Item1 });
        }
        _themeBox.SelectedIndex = IndexOfTag(_themeBox, _runtime.Config.App.Theme);
        _themeBox.SelectionChanged += (_, _) => SaveGeneral();
        _opacitySlider = new Slider
        {
            Header = "界面透明度",
            Minimum = 0.4,
            Maximum = 1.0,
            StepFrequency = 0.05,
            Value = Math.Clamp(_runtime.Config.App.UiOpacity, 0.4, 1.0),
            Width = 240,
        };
        _opacitySlider.ValueChanged += (_, _) => SaveGeneral();
        _capsuleToggle = new ToggleSwitch { Header = "显示状态胶囊（L1）", IsOn = _runtime.Config.App.ShowCapsule };
        _capsuleToggle.Toggled += (_, _) => SaveGeneral();
        // 拍板 2026-10-08：数量悬浮窗不删功能改可配置——默认关（新装/升级零残留），开=数值/额度类恢复独立悬浮窗
        _numericFloatingToggle = new ToggleSwitch { Header = "数量悬浮窗（数值/额度类，默认关）", IsOn = _runtime.Config.App.NumericFloatingEnabled };
        _numericFloatingToggle.Toggled += (_, _) => SaveGeneral();
        // 全局检查频率（2026-10-09 用户令）：组件级「检测间隔」优先，未设组件级的按此走；改后立即重建调度
        _pollBox = new ComboBox { Header = "检查频率（未单独设置间隔的组件）", Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_pollBox, "poll-interval-box"); // UIA 取证：检查频率改动驱动
        foreach (var item in new[] { ("0", "按组件档位（默认）"), ("15", "15 秒"), ("30", "30 秒"), ("60", "1 分钟"), ("300", "5 分钟") })
        {
            var pollItem = new ComboBoxItem { Content = item.Item2, Tag = item.Item1 };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(pollItem, $"poll-{item.Item1}");
            _pollBox.Items.Add(pollItem);
        }
        _pollBox.SelectedIndex = IndexOfTag(_pollBox, _runtime.Config.App.PollIntervalSeconds > 0 ? _runtime.Config.App.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture) : "0");
        _pollBox.SelectionChanged += (_, _) => SaveGeneral();
        _pinModeBox = new ComboBox { Header = "悬浮形态（重启生效）", Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var item in new[] { ("panel", "宿主面板（单窗多 tile）"), ("floating", "独立悬浮框（每组件一窗，任意拖放）") })
        {
            _pinModeBox.Items.Add(new ComboBoxItem { Content = item.Item2, Tag = item.Item1 });
        }
        _pinModeBox.SelectedIndex = IndexOfTag(_pinModeBox, _runtime.Config.App.PinDisplayMode);
        _pinModeBox.SelectionChanged += (_, _) => SaveGeneral();
        page.Children.Add(new StackPanel { Spacing = 10, Children = { _hotkeyBox, _startupToggle, _themeBox, _opacitySlider, _capsuleToggle, _pinModeBox, _numericFloatingToggle, _pollBox } });
        page.Children.Add(Hint("悬浮形态说明：独立悬浮窗默认只在「悬浮形态」下对信息密集组件（趋势图/灯组）生效；数值/额度类悬浮窗由上方「数量悬浮窗」开关控制（默认关=桌面零残留，开启即刻生效）。"));
        page.Children.Add(BuildAboutSection());
        return page;
    }

    /// <summary>版本信息：nightly exe 盖 yyyy.M.d 日期+commit（daily-build publish 步），本地构建 0.1.0——
    /// 装机排障先对构建日期，别再猜装的哪版。</summary>
    private static UIElement BuildAboutSection()
    {
        var informational = System.Reflection.Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var text = string.IsNullOrEmpty(informational) ? "0.1.0（本地构建）" : informational;
        return new TextBlock
        {
            Text = $"Beacon 版本：{text}",
            FontSize = 11,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
        };
    }

    // —— 页面：外观 ——

    private UIElement BuildAppearancePage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("外观"));
        page.Children.Add(BuildAppearanceSection());
        return page;
    }

    // —— 页面：高级（导入导出 + 通知规则） ——

    private UIElement BuildAdvancedPage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("自定义动作"));
        page.Children.Add(BuildActionEditorSection());
        page.Children.Add(SectionTitle("诊断"));
        page.Children.Add(Hint($"逐跳诊断日志：%APPDATA%\\Beacon\\logs\\beacon-*.log（每跳一行：一键添加→拉取→字段映射→渲染；组件不出数先看这里最新一份）。"));
        page.Children.Add(SectionTitle("导入导出"));
        page.Children.Add(BuildImportExportSection());
        page.Children.Add(SectionTitle("通知规则"));
        page.Children.Add(new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
            Text = DescribeRules(),
            TextWrapping = TextWrapping.Wrap,
        });
        page.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Text = "规则经 config.json 的 notificationRules 自定义；留空使用内置默认。",
            TextWrapping = TextWrapping.Wrap,
        });
        return page;
    }

    // —— 高级页：自定义动作库（actions.json；详情窗 ACTIONS 区按 widgetType 过滤出现） ——

    private UIElement BuildActionEditorSection()
    {
        _actionTypeBox = new ComboBox { Header = "类型", Width = 220 };
        foreach (var (type, label) in new[]
        {
            ("http", "HTTP 请求（触发打包 / 部署）"),
            ("webhook", "Webhook（带签名 / 凭据）"),
            ("local.command", "本机命令"),
            ("open.url", "打开链接"),
        })
        {
            _actionTypeBox.Items.Add(new ComboBoxItem { Content = label, Tag = type });
        }
        _actionTypeBox.SelectedIndex = 0;
        _actionTypeBox.SelectionChanged += (_, _) => SyncActionFields();

        _actionNameBox = new TextBox { Header = "按钮名", Width = 160, PlaceholderText = "触发打包" };
        _actionConfirmBox = new ToggleSwitch { Header = "执行前确认", IsOn = true, OnContent = "", OffContent = "", Margin = new Thickness(0, 0, 8, 0) };
        _actionScopeBox = new TextBox
        {
            Header = "限定组件类型（可空 = 全部组件可见）",
            Width = 240,
            PlaceholderText = "http.status / github.actions.runs …",
        };

        _actionUrlBox = new TextBox { Header = "URL", Width = 300, PlaceholderText = "http://192.168.5.9:8080/build" };
        _actionMethodBox = new TextBox { Header = "Method（默认 GET）", Width = 160, PlaceholderText = "POST" };
        _actionBodyBox = new TextBox { Header = "Body（可空）", Width = 300, PlaceholderText = "{\"job\":\"release\"}" };
        _actionCommandBox = new TextBox { Header = "命令", Width = 300, PlaceholderText = "C:\\build\\trigger.cmd" };
        _actionArgsBox = new TextBox { Header = "参数（可空）", Width = 300, PlaceholderText = "--job release" };
        _actionWorkDirBox = new TextBox { Header = "工作目录（可空）", Width = 300 };

        var save = new Button { Content = "保存动作" };
        save.Click += (_, _) => SaveAction();

        _actionFeedback = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _actionList = new StackPanel { Spacing = 4 };

        var form = new StackPanel { Spacing = 8 };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _actionTypeBox, _actionNameBox } });
        form.Children.Add(_actionUrlBox);
        form.Children.Add(_actionMethodBox);
        form.Children.Add(_actionBodyBox);
        form.Children.Add(_actionCommandBox);
        form.Children.Add(_actionArgsBox);
        form.Children.Add(_actionWorkDirBox);
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _actionScopeBox, _actionConfirmBox, save } });
        form.Children.Add(_actionFeedback);
        form.Children.Add(_actionList);
        SyncActionFields();
        RebuildActionList();
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = form,
        };
    }

    /// <summary>参数字段按类型显隐：http=URL/Method/Body；webhook=URL/Body；local.command=命令/参数/目录；open.url=URL。</summary>
    private void SyncActionFields()
    {
        if (_actionTypeBox is null || _actionUrlBox is null)
        {
            return;
        }
        var type = TagOf(_actionTypeBox) ?? "http";
        SetVisible(_actionUrlBox, type is "http" or "webhook" or "open.url");
        SetVisible(_actionMethodBox, type == "http");
        SetVisible(_actionBodyBox, type is "http" or "webhook");
        SetVisible(_actionCommandBox, type == "local.command");
        SetVisible(_actionArgsBox, type == "local.command");
        SetVisible(_actionWorkDirBox, type == "local.command");
    }

    private static void SetVisible(FrameworkElement? element, bool visible)
    {
        if (element is { } box)
        {
            box.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SaveAction()
    {
        var type = TagOf(_actionTypeBox) ?? "http";
        var name = _actionNameBox?.Text.Trim() ?? "";
        if (name.Length == 0)
        {
            Feedback(_actionFeedback, "✗ 请填写按钮名。", error: true);
            return;
        }
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var url = _actionUrlBox?.Text.Trim() ?? "";
        switch (type)
        {
            case "http" or "webhook" or "open.url" when url.Length == 0:
                Feedback(_actionFeedback, "✗ 请填写 URL。", error: true);
                return;
            case "http":
                parameters["url"] = url;
                parameters["method"] = _actionMethodBox?.Text.Trim() is { Length: > 0 } method ? method.ToUpperInvariant() : "GET";
                if (_actionBodyBox?.Text.Trim() is { Length: > 0 } body)
                {
                    parameters["body"] = body;
                }
                break;
            case "webhook":
                parameters["url"] = url;
                if (_actionBodyBox?.Text.Trim() is { Length: > 0 } hookBody)
                {
                    parameters["body"] = hookBody;
                }
                break;
            case "open.url":
                parameters["url"] = url;
                break;
            case "local.command":
                var command = _actionCommandBox?.Text.Trim() ?? "";
                if (command.Length == 0)
                {
                    Feedback(_actionFeedback, "✗ 请填写命令。", error: true);
                    return;
                }
                parameters["command"] = command;
                if (_actionArgsBox?.Text.Trim() is { Length: > 0 } args)
                {
                    parameters["args"] = args;
                }
                if (_actionWorkDirBox?.Text.Trim() is { Length: > 0 } workDir)
                {
                    parameters["workingDirectory"] = workDir;
                }
                break;
        }
        if (_actionScopeBox?.Text.Trim() is { Length: > 0 } scope)
        {
            parameters["widgetType"] = scope; // 详情窗按组件类型过滤（空=全部组件可见）
        }

        // 同名覆盖（再保存一次 = 编辑），Id 稳定可复现
        var id = $"action:{name.ToLowerInvariant().Replace(' ', '-')}";
        _runtime.Config.UpsertAction(new ActionConfig
        {
            Id = id,
            Type = type,
            Name = name,
            RequireConfirmation = _actionConfirmBox?.IsOn ?? true,
            Parameters = parameters,
        });
        _runtime.Config.SaveActions();
        Feedback(_actionFeedback, $"✓ 动作「{name}」已保存——点悬浮 tile 打开详情窗即可看到按钮。", error: false);
        RebuildActionList();
    }

    /// <summary>动作库列表（高级页）：名称 · 类型 · 确认标记 · 作用域，行内删除即落盘。</summary>
    private void RebuildActionList()
    {
        if (_actionList is null)
        {
            return; // 页面未构建（其他页切走）
        }
        _actionList.Children.Clear();
        foreach (var action in _runtime.Config.Actions)
        {
            var scope = action.Parameters.TryGetValue("widgetType", out var widgetType) && widgetType.Length > 0
                ? $" · 仅 {widgetType}"
                : "";
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = $"{action.Name ?? action.Id} · {action.Type}{(action.RequireConfirmation ? " · 需确认" : "")}{scope}",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var delete = new Button { Content = "删除", Padding = new Thickness(8, 2, 8, 2) };
            var captured = action;
            delete.Click += (_, _) =>
            {
                _runtime.Config.RemoveAction(captured.Id);
                _runtime.Config.SaveActions();
                RebuildActionList();
            };
            row.Children.Add(delete);
            _actionList.Children.Add(row);
        }
        if (_runtime.Config.Actions.Count == 0)
        {
            _actionList.Children.Add(Hint("尚无自定义动作。保存后出现在组件详情窗（点悬浮 tile）的 ACTIONS 区。"));
        }
    }

    // —— 页面：Provider 模块（GitHub / 智谱 GLM / 自定义 HTTP） ——

    private UIElement BuildProviderPage(ModuleDef module)
    {
        var type = module.ConnectionType!;
        var connections = _runtime.Config.Connections.Where(c => c.Type == type).ToList();
        var widgets = _runtime.Config.Widgets
            .Where(w => _scopeDescriptors!.Any(d => d.Type == w.Type))
            .ToList();

        var page = PageShell();
        page.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                MakeModuleIcon(module),
                new StackPanel
                {
                    Spacing = 2,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = module.DisplayName, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock
                        {
                            Text = ProviderStatus(module, connections, widgets.Count),
                            FontSize = 11.5,
                            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            },
        });

        page.Children.Add(SectionTitle("连接"));
        _connectionList = new StackPanel { Spacing = 6 };
        foreach (var connection in connections)
        {
            _connectionList.Children.Add(MakeConnectionRow(connection));
        }
        if (connections.Count == 0)
        {
            _connectionList.Children.Add(Hint(ProviderEmptyHint(type)));
        }
        page.Children.Add(_connectionList);
        page.Children.Add(BuildConnectionEditor(type));
        page.Children.Add(_connFeedback = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connFeedback, "conn-feedback"); // UIA 取证：测试反馈文本可硬断言

        page.Children.Add(SectionTitle("组件"));
        _widgetList = new StackPanel { Spacing = 6 };
        foreach (var widget in widgets)
        {
            _widgetList.Children.Add(MakeWidgetRow(widget));
        }
        if (widgets.Count == 0)
        {
            _widgetList.Children.Add(Hint("尚无组件——用下方向导添加，字段由组件类型决定。"));
        }
        page.Children.Add(_widgetList);
        page.Children.Add(BuildWidgetEditor());
        if (type == "bigmodel")
        {
            // 绑定流捷径（验收反馈：连上 GLM 却无组件可展示）——一条按钮从「连接就绪」直达「上板」
            var quick = new Button { Content = "一键添加 GLM 额度组件（示例）", HorizontalAlignment = HorizontalAlignment.Left };
            quick.Click += (_, _) => AddGlmQuotaExample();
            page.Children.Add(quick);
        }
        page.Children.Add(_widgetFeedback = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        return page;
    }

    /// <summary>绑定流捷径：GLM 连接就绪后一键建 bigmodel.usage 组件——直接钉选上板（白板三连根因之一：
    /// 以前 Pinned=false 只建不钉，用户看到的是空板），并经 ReloadWidgets 立即拉取（一次开即到位）。</summary>
    private void AddGlmQuotaExample()
    {
        var connection = _runtime.Config.Connections.FirstOrDefault(c => c.Type == "bigmodel");
        if (connection is null)
        {
            Feedback(_widgetFeedback, "✗ 先在上方添加并测试一个智谱 GLM 连接（bigmodel 类型）。", error: true);
            return;
        }
        var id = "bigmodel.usage:GLM";
        for (var suffix = 2; _runtime.Config.FindWidget(id) is not null; suffix++)
        {
            id = $"bigmodel.usage:GLM-{suffix}";
        }
        _runtime.Config.UpsertWidget(new WidgetConfig
        {
            Id = id,
            Type = BigModelWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            RefreshTier = RefreshTiers.Ci,
            Pinned = true, // 一键即上板——「添加了组件板面却空白」比「多一步钉选」更伤
            Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["label"] = "GLM" },
        });
        _runtime.Logger?.LogInformation("一键示例：组件 {Id} 已创建并钉选（连接 {ConnectionId}，端点 {Endpoint}）。", id, connection.Id, connection.Endpoint);
        _runtime.ReloadWidgets([id]); // 挂载即拉数：不等组循环，事件到达自动重渲
        RebuildWidgets();
        Feedback(_widgetFeedback, $"✓ GLM 额度组件 {id} 已上板（连接 {connection.Id}）——正在拉取，几秒内出数；若持续空白看日志 %APPDATA%\\Beacon\\logs。", error: false);
        PinsChanged?.Invoke();
    }

    private static string ProviderStatus(ModuleDef module, List<ConnectionConfig> connections, int widgetCount)
    {
        if (connections.Count == 0)
        {
            return "未配置——填下方连接后即启用。";
        }
        var enabled = connections.Count(c => c.Enabled);
        var state = enabled == 0 ? "已停用" : enabled == connections.Count ? "运行中" : $"部分启用（{enabled}/{connections.Count}）";
        return $"{state} · {connections.Count} 连接 · {widgetCount} 组件";
    }

    private static string ProviderEmptyHint(string type) => type switch
    {
        "github" => "尚无连接——填一个 GitHub PAT（需要 repo / workflow 读权限）。",
        "bigmodel" => "尚无连接——填智谱 API Key（open.bigmodel.cn 控制台获取，监控接口自动带默认端点；Z.AI 填国际站 Endpoint）。",
        "ark" => "尚无连接——火山控制台「API 访问密钥」创建 AK/SK（ArkReadOnlyAccess 权限即可），Token 框一次粘贴 AccessKey:SecretKey；推理用 ARK_API_KEY 调不了额度口（管控面实测 400 拒绝）。",
        "claude" => "无需连接配置——直接读本机 ~/.claude/projects 会话记录；名称随意填（如 claude-local），Endpoint 可空。",
        "kimi" => "尚无连接——填 Kimi Code 控制台 Key（sk-kimi-*，与 Moonshot 开放平台不通用；用量接口自动带默认端点）。",
        "deepseek" => "尚无连接——填 DeepSeek 开放平台 API Key（platform.deepseek.com；余额接口自动带默认端点）。",
        "mimo" => "尚无连接——填小米开放平台 API Key（platform.xiaomimimo.com，实测推理域可用）。官方未开放用量接口（推理域 /usage 实测 404），卡片显示模型目录真数据；日后开放可在连接 Settings 填 usage_endpoint。",
        "codex" => "无需连接配置——直接读本机 ~/.codex/sessions 会话记录；名称随意填（如 codex-local），Endpoint 可空。官方用量口需 ChatGPT OAuth（不同凭据体系），卡片为本地统计口径。",
        "copilot" => "尚无连接——填 GitHub PAT（github.com/settings/tokens 创建并勾选 copilot scope，官方扩展同款配额端点自动带默认地址）。卡片直读套餐配额（Pro 1500 高级请求/月、聊天/补全无限）与额度重置日；续费信息在 github.com/settings/copilot 查看。",
        "opencode" => "尚无连接——填 OpenCode Key（oc_sk，opencode.ai Console Keys 创建；读 Budgets 需 All 权限，inference-only Key 读不了）。卡片直读官方 Console Budgets 用量（本月消费/月度上限/重置日）。",
        "qwen" => "尚无连接——填百炼 Token Plan 专属 Key（bailian.console.aliyun.com 我的订阅页创建，sk-sp- 前缀）。官方额度查询 REST 口未开放（专属 Key 实测只授权模型调用），卡片默认显示模型目录真数据；有网关/代理用量口可在下方「用量端点」填入（GET + Bearer，响应含 percent/reset 即出额度+重置日）。",
        _ => "尚无连接——填局域网/内部接口地址（可空凭据）；状态词表/额度字段映射在组件向导里配。",
    };

    private void SaveGeneral()
    {
        var app = _runtime.Config.App;
        app.Hotkey = _hotkeyBox.Text.Trim();
        app.LaunchOnStartup = _startupToggle.IsOn;
        app.Theme = TagOf(_themeBox) ?? "system";
        app.PinDisplayMode = TagOf(_pinModeBox) ?? "panel";
        app.UiOpacity = _opacitySlider.Value;
        app.ShowCapsule = _capsuleToggle.IsOn;
        app.NumericFloatingEnabled = _numericFloatingToggle.IsOn;
        app.PollIntervalSeconds = int.TryParse(TagOf(_pollBox), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pollSeconds) && pollSeconds > 0 ? pollSeconds : 0;
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    // —— 外观：级别色编辑 + 动效档位与逐族预览（B-805，RFC §4.1/§6.2.8） ——

    private UIElement BuildAppearanceSection()
    {
        var palette = new StackPanel { Spacing = 6 };
        foreach (var key in new[] { "info", "success", "warning", "error", "critical", "offline" })
        {
            var box = new TextBox
            {
                Header = $"{ColorLabel(key)}（留空 = 默认 {DefaultHex(key)}）",
                Text = _runtime.Config.App.Appearance.SeverityColors.GetValueOrDefault(key, ""),
                PlaceholderText = DefaultHex(key),
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var captured = key;
            box.LostFocus += (_, _) => SaveSeverityColor(captured, box.Text.Trim());
            _colorBoxes[key] = box;
            palette.Children.Add(box);
        }
        var reset = new Button { Content = "重置默认" };
        reset.Click += (_, _) => ResetSeverityColors();

        _motionModeBox = new ComboBox { Header = "动效档位", Width = 170 };
        foreach (var (value, label) in new[] { ("full", "完全"), ("reduced", "适度（默认）"), ("off", "关闭") })
        {
            _motionModeBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        _motionModeBox.SelectedIndex = Math.Max(0, IndexOfTag(_motionModeBox, _runtime.Config.App.Appearance.Motion.Mode));
        _motionModeBox.SelectionChanged += (_, _) => SaveMotion();

        _motionIntensitySlider = new Slider
        {
            Header = "动效强度",
            Minimum = 0.5,
            Maximum = 2.0,
            StepFrequency = 0.1,
            Value = Math.Clamp(_runtime.Config.App.Appearance.Motion.Intensity, 0.5, 2.0),
            Width = 240,
        };
        _motionIntensitySlider.ValueChanged += (_, _) => SaveMotion();

        _previewLight = new Ellipse { Width = 24, Height = 24, Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 63, 185, 80)) };
        _previewHost = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = _previewLight,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Button PreviewButton(string label, string family)
        {
            var button = new Button { Content = label };
            button.Click += (_, _) => PreviewMotion(family);
            return button;
        }

        var form = new StackPanel { Spacing = 8 };
        form.Children.Add(palette);
        form.Children.Add(reset);
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _motionModeBox, _motionIntensitySlider } });
        form.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                PreviewButton("过渡", "transition"), PreviewButton("闪烁", "flash"), PreviewButton("呼吸", "breath"),
                PreviewButton("脉冲", "pulse"), PreviewButton("滑入", "slide"), PreviewButton("静止", "stop"), _previewHost,
            },
        });
        form.Children.Add(_appearanceFeedback = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        form.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Text = "改动即时预览并写入 config.json appearance；off 档预览即全静止，静止按钮复位循环动画。",
            TextWrapping = TextWrapping.Wrap,
        });
        return new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)), Child = form };
    }

    private void SaveSeverityColor(string key, string text)
    {
        var colors = _runtime.Config.App.Appearance.SeverityColors;
        if (text.Length == 0)
        {
            colors.Remove(key); // 留空 = 回落默认色表
        }
        else
        {
            var normalized = text.StartsWith('#') ? text : "#" + text;
            if (!PaletteResolver.TryParseHex(normalized, out _))
            {
                Feedback(_appearanceFeedback, $"✗ {ColorLabel(key)}格式无效（#RRGGBB 或 #AARRGGBB）。", error: true);
                return;
            }
            colors[key] = normalized;
        }
        _runtime.Config.SaveApp();
        Feedback(_appearanceFeedback, $"✓ {ColorLabel(key)}已保存。", error: false);
        SettingsApplied?.Invoke(); // App 侧重渲染托盘/胶囊/L0——改色即刻生效
    }

    private void ResetSeverityColors()
    {
        _runtime.Config.App.Appearance.SeverityColors.Clear();
        _runtime.Config.SaveApp();
        foreach (var (_, box) in _colorBoxes)
        {
            box.Text = "";
        }
        Feedback(_appearanceFeedback, "✓ 已重置为默认色表。", error: false);
        SettingsApplied?.Invoke();
    }

    private void SaveMotion()
    {
        var motion = _runtime.Config.App.Appearance.Motion;
        motion.Mode = TagOf(_motionModeBox) ?? "reduced";
        motion.Intensity = _motionIntensitySlider.Value;
        _runtime.Config.SaveApp(); // MotionEngine 每次调用实时读配置——无需重渲染
    }

    /// <summary>逐族预览（B-805）：与 L0 同一引擎，off 档内部即静止。</summary>
    private void PreviewMotion(string family)
    {
        switch (family)
        {
            case "transition":
                _motion.TransitionFill(_previewLight, SeverityPalette.Rgb(255, 248, 81, 73));
                break;
            case "flash":
                _motion.Flash(_previewHost);
                break;
            case "breath":
                _motion.StartBreathing(_previewLight);
                break;
            case "pulse":
                _motion.StartPulse(_previewLight, 24);
                break;
            case "slide":
                _motion.SlideIn(_previewHost);
                break;
            case "stop":
                _motion.StopLoops(_previewLight);
                _motion.StopLoops(_previewHost);
                break;
        }
    }

    // —— 导入导出（B-802）：五份配置单文件（含自定义动作）；secrets 绝不入包，导入后按 credentialRef 提示重录 ——

    private UIElement BuildImportExportSection()
    {
        var export = new Button { Content = "导出配置…" };
        export.Click += async (_, _) => await ExportAsync();
        var import = new Button { Content = "导入配置…" };
        import.Click += async (_, _) => await ImportAsync();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { export, import } });
        panel.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Text = "导出包含 config/connections/widgets/pins/actions 五份配置（含自定义动作库）；密钥绝不入包（JSON 只存 credentialRef），导入后按提示在连接里重录。",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(_importFeedback = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    private async Task ExportAsync()
    {
        var picker = new FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = $"beacon-config-{DateTimeOffset.Now:yyyyMMdd-HHmm}";
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }
        try
        {
            new ImportExport(_runtime.Config).Export(file.Path);
            Feedback(_importFeedback, $"✓ 已导出到 {file.Path}（不含密钥；导入方需重录）。", error: false);
        }
        catch (Exception exception)
        {
            Feedback(_importFeedback, $"✗ 导出失败：{exception.Message}", error: true);
        }
    }

    private async Task ImportAsync()
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }
        try
        {
            var result = new ImportExport(_runtime.Config).Import(file.Path);
            RebuildConnections();
            RebuildWidgets();
            RefreshWidgetConnectionOptions();
            RebuildActionList(); // 动作库随包替换（null 守卫：高级页未构建则跳过）
            var reentry = result.CredentialRefs.Count > 0
                ? $"；请在「连接」里重录密钥：{string.Join("、", result.CredentialRefs)}"
                : "";
            Feedback(_importFeedback, $"✓ 已导入 {result.Connections} 连接 / {result.Widgets} 组件 / {result.Pins} 钉选 / {result.Actions} 动作{reentry}。", error: false);
            PinsChanged?.Invoke(); // 组件集变化 → L0 重建
            SettingsApplied?.Invoke(); // 通用设置也随包变了 → 热键/自启/胶囊对齐
        }
        catch (Exception exception)
        {
            Feedback(_importFeedback, $"✗ 导入失败：{exception.Message}", error: true);
        }
    }

    private static string ColorLabel(string key) => key switch
    {
        "info" => "信息",
        "success" => "成功",
        "warning" => "警告",
        "error" => "错误",
        "critical" => "严重",
        "offline" => "离线",
        _ => key,
    };

    private static string DefaultHex(string key)
    {
        if (key == PaletteResolver.OfflineKey)
        {
            return PaletteResolver.DefaultOfflineHex;
        }
        return Enum.TryParse<Severity>(key, ignoreCase: true, out var severity)
            ? PaletteResolver.Defaults[severity]
            : PaletteResolver.DefaultOfflineHex;
    }

    // —— 连接：列表 + 表单 + 测试（B-801；token 只进 DPAPI） ——

    /// <summary>连接表单。lockType 非空 = Provider 模块页（类型锁定，凭证/端点提示按类型定制）。</summary>
    private UIElement BuildConnectionEditor(string? lockType)
    {
        _connTypeBox = new ComboBox { Header = "类型", Width = 160, IsEnabled = lockType is null };
        IEnumerable<string> types = lockType is { } locked ? [locked] : _runtime.ConnectionProviders.Keys;
        foreach (var type in types)
        {
            _connTypeBox.Items.Add(type);
        }
        if (_connTypeBox.Items.Count > 0)
        {
            _connTypeBox.SelectedIndex = 0;
        }
        _connIdBox = new TextBox { Header = "名称（唯一 Id）", Width = 200, PlaceholderText = ConnectionIdHint(lockType) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connIdBox, "conn-id-box"); // UIA 取证：save-flow 驱动
        _connIdBox.Text = PrefillConnectionId(lockType); // 预填唯一 Id：不改即用，改了以用户为准（不再强制手填）
        _connTypeBox.SelectionChanged += (_, _) => PrefillConnectionIdIfUntouched();
        _connTypeBox.SelectionChanged += (_, _) => SyncUsageBoxVisibility();
        _connEndpointBox = new TextBox { Header = ConnectionEndpointHeader(lockType), Width = 280, PlaceholderText = ConnectionEndpointHint(lockType) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connEndpointBox, "conn-endpoint-box");
        _connUsageBox = new TextBox
        {
            Header = "用量端点（可空 = 显示模型目录，官方额度口未开放）",
            Width = 280,
            PlaceholderText = "https://…/usage（GET + Bearer；响应含 percent/reset 即出额度卡）",
            Visibility = Visibility.Collapsed,
        };
        _connTokenBox = new PasswordBox { Header = ConnectionTokenHeader(lockType), Width = 280 };
        _connConsoleBox = new PasswordBox
        {
            Header = "控制台 Cookie（可选，录了组件才显示套餐用量）",
            Width = 280,
            PlaceholderText = "浏览器 F12 → 应用 → Cookies → api-platform_ph 的值",
            Visibility = Visibility.Collapsed,
        };

        _connSaveButton = new Button { Content = "保存连接" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_connSaveButton, "conn-save-button");
        _connSaveButton.Click += async (_, _) => await SaveConnectionAsync();
        var cancel = new Button { Content = "取消编辑" };
        cancel.Click += (_, _) => ResetConnectionEditor();

        var form = new StackPanel { Spacing = 8, Padding = new Thickness(0, 4, 0, 0) };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _connTypeBox, _connIdBox } });
        form.Children.Add(_connEndpointBox);
        form.Children.Add(_connUsageBox);
        form.Children.Add(_connTokenBox);
        form.Children.Add(_connConsoleBox);
        SyncUsageBoxVisibility();
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _connSaveButton, cancel } });
        return new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = form,
        };
    }

    private async Task SaveConnectionAsync()
    {
        var id = _connIdBox.Text.Trim();
        if (id.Length == 0)
        {
            Feedback(_connFeedback, "✗ 请填写连接名称。", error: true);
            return;
        }
        if (_editingConnectionId is null && _runtime.Config.Connections.Any(c => c.Id == id))
        {
            Feedback(_connFeedback, $"✗ 连接 {id} 已存在。", error: true);
            return;
        }

        var existing = _editingConnectionId is null ? null : _runtime.Config.Connections.FirstOrDefault(c => c.Id == _editingConnectionId);
        var credentialRef = existing?.CredentialRef;
        var token = _connTokenBox.Password;
        if (token.Length > 0)
        {
            credentialRef = $"conn:{id}";
            try
            {
                await _runtime.Secrets.SetAsync(credentialRef, token);
            }
            catch (Exception exception)
            {
                Feedback(_connFeedback, $"✗ 密钥写入失败：{exception.Message}", error: true);
                return;
            }
        }

        var connection = new ConnectionConfig
        {
            Id = id,
            Type = SelectedString(_connTypeBox) ?? "github",
            Endpoint = string.IsNullOrWhiteSpace(_connEndpointBox.Text) ? null : _connEndpointBox.Text.Trim(),
            CredentialRef = credentialRef,
            Enabled = existing?.Enabled ?? true, // 编辑保留启停状态
            Settings = MergeConnectionSettings(existing, _connUsageBox?.Text.Trim()), // 保留 auth_header 等高级项 + usage_endpoint
        };
        if (_connConsoleBox is { } console && console.Password.Trim().Length > 0)
        {
            // 控制台 Cookie 是登录态凭据：只进 DPAPI（ref 默认 mimo:console，与推理 Key 分开两格）
            try
            {
                await _runtime.Secrets.SetAsync(MiMoUsageProvider.DefaultConsoleCredentialRef, console.Password.Trim());
            }
            catch (Exception exception)
            {
                Feedback(_connFeedback, $"✗ 控制台 Cookie 写入失败：{exception.Message}", error: true);
                return;
            }
        }
        _runtime.Config.UpsertConnection(connection);
        ResetConnectionEditor();
        RebuildConnections();
        RefreshWidgetConnectionOptions(); // 保存连接后必须刷组件向导下拉——否则新建连接不出现在可选列表（用户实测「下拉空白」根因）
        Feedback(_connFeedback, $"✓ 连接 {id} 已保存。", error: false);
        SettingsApplied?.Invoke();
    }

    /// <summary>生成该类型的唯一连接 Id：默认名（ConnectionIdHint）+ 重名 -2/-3 递增；预填输入框，不改即用。</summary>
    private string PrefillConnectionId(string? lockType)
    {
        var seed = ConnectionIdHint(lockType ?? SelectedString(_connTypeBox) ?? "github");
        var id = seed;
        for (var suffix = 2; _runtime.Config.Connections.Any(c => c.Id == id); suffix++)
        {
            id = $"{seed}-{suffix}";
        }
        _lastPrefilledConnectionId = id;
        return id;
    }

    /// <summary>类型切换时仅当 Id 框未被用户改过（空或仍是上次预填值）才重预填，不覆盖用户输入。</summary>
    private void PrefillConnectionIdIfUntouched()
    {
        if (!_connIdBox.IsEnabled)
        {
            return; // 编辑已有连接（Id 锁定）
        }
        var current = _connIdBox.Text.Trim();
        if (current.Length == 0 || current == _lastPrefilledConnectionId)
        {
            _connIdBox.Text = PrefillConnectionId(null);
        }
    }

    private static string ConnectionIdHint(string? lockType) => lockType switch
    {
        "github" => "github-main",
        "bigmodel" => "zhipu",
        "ark" => "ark-main",
        "claude" => "claude-local",
        "kimi" => "kimi",
        "deepseek" => "deepseek",
        "mimo" => "mimo-main",
        "qwen" => "qwen-main",
        "codex" => "codex-local",
        "copilot" => "copilot-main",
        "opencode" => "opencode-main",
        "http" => "ci-local",
        _ => "github-main",
    };

    private static string ConnectionEndpointHeader(string? lockType) => lockType switch
    {
        "bigmodel" => "Endpoint（可空 = 官方监控接口；Z.AI 填国际站地址）",
        "ark" => "Endpoint（可空 = 官方网关 open.volcengineapi.com，一般不用改）",
        "claude" => "会话目录（可空 = 默认 ~/.claude/projects）",
        "kimi" => "Endpoint（可空 = 官方用量接口）",
        "deepseek" => "Endpoint（可空 = 官方余额接口）",
        "mimo" => "Endpoint（可空 = 推理域 token-plan-cn.xiaomimimo.com/v1）",
        "qwen" => "Endpoint（可空 = 百炼兼容口 token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1）",
        "codex" => "会话目录（可空 = 默认 ~/.codex/sessions）",
        "copilot" => "Endpoint（可空 = 官方配额端点 copilot_internal/user）",
        "opencode" => "Endpoint（可空 = 官方 Console Budgets 接口）",
        "http" => "Endpoint（必填，如局域网打包工具地址）",
        _ => "Endpoint（可空 = 官方 API）",
    };

    private static string ConnectionEndpointHint(string? lockType) => lockType switch
    {
        "bigmodel" => "https://open.bigmodel.cn/api/monitor/usage/quota/limit",
        "ark" => "https://open.volcengineapi.com/",
        "claude" => "C:\\Users\\me\\.claude\\projects",
        "kimi" => "https://api.kimi.com/coding/v1/usages",
        "deepseek" => "https://api.deepseek.com/user/balance",
        "mimo" => "https://token-plan-cn.xiaomimimo.com/v1",
        "qwen" => "https://token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1",
        "codex" => "C:\\Users\\me\\.codex\\sessions",
        "copilot" => "https://api.github.com/copilot_internal/user",
        "opencode" => "https://opencode.ai/console/api/v1/budgets/members",
        "http" => "http://192.168.1.10:8080/api/status",
        _ => "https://api.github.com",
    };

    private static string ConnectionTokenHeader(string? lockType) => lockType switch
    {
        "github" => "PAT（只写 DPAPI，JSON 仅存引用）",
        "bigmodel" => "API Key（只写 DPAPI；监控接口裸 Key 直传）",
        "ark" => "AccessKey:SecretKey（冒号分隔一次粘贴，只进 DPAPI；控制台「API 访问密钥」创建）",
        "claude" => "无需凭据（留空）",
        "kimi" => "API Key（sk-kimi-*，只写 DPAPI）",
        "deepseek" => "API Key（只写 DPAPI）",
        "mimo" => "API Key（只写 DPAPI；Bearer 直传）",
        "qwen" => "API Key（sk-sp- 专属 Key，只写 DPAPI；Bearer 直传）",
        "codex" => "无需凭据（留空）",
        "copilot" => "PAT（需 copilot scope，只写 DPAPI）",
        "opencode" => "API Key（oc_sk，只写 DPAPI；读 Budgets 需 All 权限）",
        "http" => "API Key（可选，只写 DPAPI；默认 Bearer 头，可在 config.json 改）",
        _ => "Token（只写 DPAPI，JSON 仅存引用）",
    };

    private void ResetConnectionEditor()
    {
        _editingConnectionId = null;
        _connSaveButton.Content = "保存连接";
        _connIdBox.IsEnabled = true;
        _connIdBox.Text = PrefillConnectionId(null);
        _connEndpointBox.Text = "";
        if (_connUsageBox is { } usage)
        {
            usage.Text = "";
        }
        if (_connConsoleBox is { } console)
        {
            console.Password = "";
            console.PlaceholderText = "浏览器 F12 → 应用 → Cookies → api-platform_ph 的值";
        }
        _connTokenBox.Password = "";
        _connTokenBox.PlaceholderText = "";
    }

    /// <summary>连接表单辅助框显隐：用量端点框仅 qwen（官方额度口未开放）；控制台 Cookie 框仅 mimo（plan-manage 套餐用量）。</summary>
    private void SyncUsageBoxVisibility()
    {
        var type = SelectedString(_connTypeBox);
        if (_connUsageBox is { } box)
        {
            box.Visibility = type == "qwen" ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_connConsoleBox is { } console)
        {
            console.Visibility = type == "mimo" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>连接 Settings 落盘：usage_endpoint 随表单值增删，其余高级项（auth_header 等）原样保留。</summary>
    private static Dictionary<string, string> MergeConnectionSettings(ConnectionConfig? existing, string? usageEndpoint)
    {
        var settings = existing?.Settings is { } keep
            ? new Dictionary<string, string>(keep, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(usageEndpoint))
        {
            settings.Remove("usage_endpoint");
        }
        else
        {
            settings["usage_endpoint"] = usageEndpoint!;
        }
        return settings;
    }

    private void RebuildConnections()
    {
        if (_connectionList is null)
        {
            return; // 常规/外观/高级页无连接区
        }
        _connectionList.Children.Clear();
        foreach (var connection in _runtime.Config.Connections.Where(c => _scopeType is null || c.Type == _scopeType))
        {
            _connectionList.Children.Add(MakeConnectionRow(connection));
        }
        if (!_runtime.Config.Connections.Any(c => _scopeType is null || c.Type == _scopeType))
        {
            _connectionList.Children.Add(Hint(_scopeType is { } type ? ProviderEmptyHint(type) : "尚无连接。"));
        }
        SyncModuleToggles();
    }

    /// <summary>左目录的模块开关与连接实际启停对齐（行内开关改动后回写）。</summary>
    private void SyncModuleToggles()
    {
        if (_moduleToggles.Count == 0)
        {
            return;
        }
        _syncingToggles = true;
        try
        {
            foreach (var (type, toggle) in _moduleToggles)
            {
                toggle.IsOn = TypeEnabled(type);
            }
        }
        finally
        {
            _syncingToggles = false;
        }
    }

    private UIElement MakeConnectionRow(ConnectionConfig connection)
    {
        var label = new TextBlock
        {
            Text = $"{connection.Id}{(connection.CredentialRef is null ? " · 无凭据" : " · 凭据已存")}{(connection.Enabled ? "" : " · 已停用")}",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 单连接启停（Enabled 是 init-only：with 表达式替换后落库）
        var enabledToggle = new ToggleSwitch
        {
            IsOn = connection.Enabled,
            OnContent = "",
            OffContent = "",
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        enabledToggle.Toggled += (_, _) =>
        {
            // Enabled init-only（class 非 record）：重建替换落库
            _runtime.Config.UpsertConnection(new ConnectionConfig
            {
                Id = connection.Id,
                Type = connection.Type,
                Endpoint = connection.Endpoint,
                CredentialRef = connection.CredentialRef,
                Enabled = enabledToggle.IsOn,
                Settings = connection.Settings,
            });
            if (!enabledToggle.IsOn)
            {
                // 停用即失活关联组件（不留旧灯），再重建列表
                _runtime.InvalidateWidgets(_runtime.Config.Widgets.Where(w => w.ConnectionId == connection.Id).Select(w => w.Id));
            }
            RebuildConnections();
            RebuildWidgets();
            RefreshWidgetConnectionOptions(); // 停用连接后下拉同步移除，启用后恢复
            PinsChanged?.Invoke(); // 2026-10-09 修:连接停用/启用同样要重建 L0(悬浮窗/宿主)——与模块开关同口径
        };

        var test = new Button { Content = "测试" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(test, $"test-{connection.Id}"); // UIA 取证：点测试截反馈
        test.Click += async (_, _) => await TestConnectionAsync(connection);
        var edit = new Button { Content = "编辑" };
        edit.Click += (_, _) =>
        {
            _editingConnectionId = connection.Id;
            _connSaveButton.Content = "更新连接";
            _connIdBox.Text = connection.Id;
            _connIdBox.IsEnabled = false;
            _connEndpointBox.Text = connection.Endpoint ?? "";
            if (_connTypeBox.SelectedItem as string != connection.Type)
            {
                var typeIndex = -1;
                for (var i = 0; i < _connTypeBox.Items.Count; i++)
                {
                    if (_connTypeBox.Items[i] is string typeName && typeName == connection.Type)
                    {
                        typeIndex = i;
                        break;
                    }
                }
                if (typeIndex >= 0)
                {
                    _connTypeBox.SelectedIndex = typeIndex; // 表单框头/用量框显隐随类型切换（编辑连接带出真实类型）
                }
            }
            if (_connUsageBox is { } usage)
            {
                usage.Text = connection.Settings.GetValueOrDefault("usage_endpoint") ?? "";
            }
            if (_connConsoleBox is { } console && connection.Type == "mimo")
            {
                console.Password = "";
                console.PlaceholderText = "已保存（留空保持不变）"; // cookie 在 DPAPI，读不回明文——只提示不变
            }
            _connTokenBox.Password = "";
            _connTokenBox.PlaceholderText = "已保存（留空保持不变）";
        };
        var delete = new Button { Content = "删除" };
        delete.Click += (_, _) =>
        {
            _runtime.InvalidateWidgets(_runtime.Config.Widgets.Where(w => w.ConnectionId == connection.Id).Select(w => w.Id));
            _runtime.Config.RemoveConnection(connection.Id);
            RebuildConnections();
            RebuildWidgets(); // ConnectionId 失联的组件如实展示
            RefreshWidgetConnectionOptions(); // 删除连接后下拉同步移除
            PinsChanged?.Invoke();
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { label, enabledToggle, test, edit, delete },
        };
    }

    private async Task TestConnectionAsync(ConnectionConfig connection)
    {
        if (!_runtime.ConnectionProviders.TryGetValue(connection.Type, out var provider))
        {
            Feedback(_connFeedback, $"✗ 连接类型 {connection.Type} 无 Provider。", error: true);
            return;
        }
        try
        {
            var result = await provider.TestAsync(connection, new ConnectionContext { Secrets = _runtime.Secrets }, CancellationToken.None);
            var health = result.Health;
            // Detail 原样透出：通用文案（「降级（限流等）」）吞掉真实原因（凭据格式/4xx 响应体/响应结构），
            // 用户无法判断「明明连上了却报降级」是哪一环——真实原因必须可见（2026-10-09 用户实测反馈）
            var detail = string.IsNullOrEmpty(result.Detail) ? "" : $"：{result.Detail}";
            Feedback(_connFeedback, health switch
            {
                ConnectionHealthState.Healthy => $"✓ {connection.Id} 连接正常——在下方组件向导选该连接，或用「一键添加」即可上板。",
                ConnectionHealthState.Degraded => $"△ {connection.Id} 降级{detail}——仍可添加组件上板。",
                ConnectionHealthState.Offline => $"✗ {connection.Id} 不可达{detail}。",
                ConnectionHealthState.Unauthorized => $"✗ {connection.Id} 认证失败{detail}，检查 token。",
                _ => $"✗ {connection.Id} 状态未知{detail}。",
            }, error: health is not ConnectionHealthState.Healthy and not ConnectionHealthState.Degraded);
        }
        catch (Exception exception)
        {
            Feedback(_connFeedback, $"✗ 测试失败：{exception.Message}", error: true);
        }
    }

    // —— 组件：列表 + 向导（描述符驱动动态字段） ——

    private UIElement BuildWidgetEditor()
    {
        _widgetTypeBox = new ComboBox { Header = "类型", Width = 220 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_widgetTypeBox, "widget-type-box"); // UIA 取证：类型下拉非空断言（小米类型空回归）
        foreach (var descriptor in _scopeDescriptors ?? AllWidgetDescriptors())
        {
            _widgetTypeBox.Items.Add(new ComboBoxItem { Content = descriptor.DisplayName, Tag = descriptor.Type });
        }
        if (_widgetTypeBox.Items.Count > 0)
        {
            _widgetTypeBox.SelectedIndex = 0;
        }
        _widgetTypeBox.SelectionChanged += (_, _) => RebuildWidgetFields();
        _widgetConnectionBox = new ComboBox { Header = "连接", Width = 200 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_widgetConnectionBox, "widget-connection-box"); // UIA 取证：展开截可选项
        _widgetTierBox = new ComboBox { Header = "刷新档", Width = 140 };
        foreach (var tier in new[] { RefreshTiers.Pr, RefreshTiers.Ci, RefreshTiers.Machine, RefreshTiers.Agent, RefreshTiers.Workflow, RefreshTiers.Static, RefreshTiers.Default })
        {
            _widgetTierBox.Items.Add(tier);
        }
        // 检测间隔覆盖（拍板：频率要可设）——不选=按刷新档默认；秒数落 WidgetConfig.RefreshIntervalSeconds
        _widgetIntervalBox = new ComboBox { Header = "检测间隔（可选）", Width = 150 };
        foreach (var (tag, label) in new[] { ("", "跟随刷新档"), ("30", "30 秒"), ("60", "1 分钟"), ("120", "2 分钟"), ("300", "5 分钟"), ("900", "15 分钟"), ("3600", "1 小时") })
        {
            var item = new ComboBoxItem { Content = label, Tag = tag };
            _widgetIntervalBox.Items.Add(item);
        }
        _widgetIntervalBox.SelectedIndex = 0;

        _widgetFields = new StackPanel { Spacing = 8 };
        _widgetColorBox = new TextBox { Header = "颜色覆盖（可选 #RRGGBB，作用于状态灯）", PlaceholderText = "#3fb950", Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        // 验收反馈：向导保存即上板——默认勾选钉到桌面，避免「加了组件板面却空白」（同白板三连根因收口）
        _widgetPinBox = new ToggleSwitch { Header = "添加到桌面（钉选）", IsOn = true };

        var save = new Button { Content = "添加组件" };
        save.Click += (_, _) => SaveWidget();

        var form = new StackPanel { Spacing = 8, Padding = new Thickness(0, 4, 0, 0) };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _widgetTypeBox, _widgetConnectionBox, _widgetTierBox, _widgetIntervalBox } });
        form.Children.Add(_widgetFields);
        form.Children.Add(_widgetColorBox);
        form.Children.Add(_widgetPinBox);
        form.Children.Add(save);
        var root = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)),
            Child = form,
        };
        RebuildWidgetFields();
        RefreshWidgetConnectionOptions();
        return root;
    }

    private void RebuildWidgetFields()
    {
        _widgetFields.Children.Clear();
        var descriptor = SelectedWidgetDescriptor();
        if (descriptor is null)
        {
            return;
        }
        foreach (var field in descriptor.Fields)
        {
            if (field.Choices is { } choices)
            {
                // 选择字段（icon 自选，用户令 2026-10-10）：下拉而非自由文本；首项=默认（按连接品牌）
                var picker = new ComboBox
                {
                    Header = field.DisplayName,
                    Tag = field.Key,
                    Width = 280,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                picker.Items.Add(new ComboBoxItem { Content = field.Placeholder ?? "默认", Tag = "" });
                foreach (var choice in choices)
                {
                    picker.Items.Add(new ComboBoxItem { Content = choice, Tag = choice });
                }
                picker.SelectedIndex = 0;
                _widgetFields.Children.Add(picker);
                continue;
            }
            _widgetFields.Children.Add(new TextBox
            {
                Header = field.DisplayName + (field.Required ? "（必填）" : ""),
                PlaceholderText = field.Placeholder ?? "",
                Tag = field.Key,
                Width = 280,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        _widgetColorBox.Text = "";
        var tier = descriptor.SuggestedTier;
        _widgetTierBox.SelectedIndex = _widgetTierBox.Items.IndexOf(tier) is var index && index >= 0 ? index : _widgetTierBox.Items.Count - 1;
    }

    private void RefreshWidgetConnectionOptions()
    {
        // 常规/外观/高级页无组件编辑器（_widgetConnectionBox 未构建）——导入等路径会调到，空守卫防 NRE
        if (_widgetConnectionBox is null)
        {
            return;
        }
        var previous = TagOf(_widgetConnectionBox);
        _widgetConnectionBox.Items.Clear();
        foreach (var connection in _runtime.Config.Connections.Where(c => _scopeType is null || c.Type == _scopeType))
        {
            _widgetConnectionBox.Items.Add(new ComboBoxItem { Content = connection.Id, Tag = connection.Id });
        }
        if (_widgetConnectionBox.Items.Count > 0)
        {
            _widgetConnectionBox.SelectedIndex = Math.Max(0, IndexOfTag(_widgetConnectionBox, previous ?? _runtime.Config.Connections[0].Id));
        }
    }

    private void SaveWidget()
    {
        var descriptor = SelectedWidgetDescriptor();
        if (descriptor is null)
        {
            Feedback(_widgetFeedback, "✗ 请选择组件类型。", error: true);
            return;
        }
        if (_widgetConnectionBox.SelectedItem is null)
        {
            Feedback(_widgetFeedback, "✗ 请先添加一个连接。", error: true);
            return;
        }

        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in _widgetFields.Children.OfType<TextBox>())
        {
            var key = child.Tag?.ToString() ?? "";
            if (child.Text.Trim().Length > 0)
            {
                config[key] = child.Text.Trim();
            }
        }
        foreach (var child in _widgetFields.Children.OfType<ComboBox>())
        {
            if (TagOf(child) is { Length: > 0 } choice) // 选择字段：未选（默认项）不落盘
            {
                config[child.Tag?.ToString() ?? ""] = choice;
            }
        }
        foreach (var field in descriptor.Fields.Where(f => f.Required))
        {
            if (!config.ContainsKey(field.Key))
            {
                Feedback(_widgetFeedback, $"✗ {field.DisplayName} 为必填。", error: true);
                return;
            }
        }
        string? colorOverride = null;
        var colorText = _widgetColorBox.Text.Trim();
        if (colorText.Length > 0)
        {
            var normalized = colorText.StartsWith('#') ? colorText : "#" + colorText;
            if (!PaletteResolver.TryParseHex(normalized, out _))
            {
                Feedback(_widgetFeedback, "✗ 颜色覆盖格式无效（#RRGGBB 或 #AARRGGBB）。", error: true);
                return;
            }
            colorOverride = normalized;
        }

        // 组件 Id 种子按类型取自然键：GitHub 用 repo，HTTP/额度用 label，缺省退类型尾段
        var seed = config.GetValueOrDefault("repo") ?? config.GetValueOrDefault("label") ?? descriptor.Type.Split('.')[^1];
        var id = $"{descriptor.Type}:{seed}";
        for (var suffix = 2; _runtime.Config.FindWidget(id) is not null; suffix++)
        {
            id = $"{descriptor.Type}:{seed}-{suffix}";
        }

        var widget = new WidgetConfig
        {
            Id = id,
            Type = descriptor.Type,
            ConnectionId = TagOf(_widgetConnectionBox) ?? "",
            Config = config,
            RefreshTier = SelectedString(_widgetTierBox) ?? descriptor.SuggestedTier,
            RefreshIntervalSeconds = TagOf(_widgetIntervalBox) is { } tag && int.TryParse(tag, out var seconds) && seconds > 0
                ? seconds
                : null,
            Pinned = _widgetPinBox.IsOn,
            ColorOverride = colorOverride,
        };
        _runtime.Config.UpsertWidget(widget);
        _runtime.ReloadWidgets([widget.Id]); // 新组件即建即拉（调度器重建+首拉，白板收口）
        RebuildWidgetFields(); // 清空已提交的字段输入（含颜色覆盖）
        RebuildWidgets();
        RefreshWidgetConnectionOptions();
        Feedback(_widgetFeedback, widget.Pinned
            ? $"✓ 组件 {id} 已添加并钉到桌面，正在拉取（几秒内出数）。"
            : $"✓ 组件 {id} 已添加（未钉选，可在上方组件行开启「钉」上板）。", error: false);
        PinsChanged?.Invoke();
    }

    private WidgetTypeDescriptor? SelectedWidgetDescriptor()
        => TagOf(_widgetTypeBox) is { } type
            ? AllWidgetDescriptors().FirstOrDefault(d => d.Type == type)
            : null;

    private void RebuildWidgets()
    {
        if (_widgetList is null)
        {
            return; // 常规/外观/高级页无组件区
        }
        _widgetList.Children.Clear();
        foreach (var widget in _runtime.Config.Widgets.Where(w => _scopeDescriptors is null || _scopeDescriptors.Any(d => d.Type == w.Type)))
        {
            _widgetList.Children.Add(MakeWidgetRow(widget));
        }
        if (!_runtime.Config.Widgets.Any(w => _scopeDescriptors is null || _scopeDescriptors.Any(d => d.Type == w.Type)))
        {
            _widgetList.Children.Add(Hint("尚无组件——用下方向导添加，字段由组件类型决定。"));
        }
    }

    private UIElement MakeWidgetRow(WidgetConfig widget)
    {
        var descriptor = AllWidgetDescriptors().FirstOrDefault(d => d.Type == widget.Type);
        var missingConnection = _runtime.Config.Connections.All(c => c.Id != widget.ConnectionId);
        var label = new TextBlock
        {
            Text = $"{widget.Id} · {widget.RefreshTier}"
                   + (widget.RefreshIntervalSeconds is { } seconds ? $" · {seconds}s" : "")
                   + (widget.Pinned ? " · 已钉" : "")
                   + (widget.ColorOverride is { } color ? $" · 覆盖 {color}" : "")
                   + (missingConnection ? " · 连接缺失" : ""),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (descriptor is { PinSupported: true })
        {
            var pin = new ToggleSwitch { OnContent = "钉", OffContent = "钉", IsOn = widget.Pinned, Margin = new Thickness(0, 0, 0, 0) };
            pin.Toggled += (_, _) =>
            {
                widget.Pinned = pin.IsOn;
                _runtime.Config.UpsertWidget(widget);
                _runtime.ReloadWidgets(widget.Pinned ? [widget.Id] : null); // 开钉=重建注册+立即拉数（用户实测：以前首开不出数，关/开几次才显）
                PinsChanged?.Invoke();
            };
            row.Children.Add(pin);
        }

        var delete = new Button { Content = "删除" };
        delete.Click += (_, _) =>
        {
            _runtime.InvalidateWidgets([widget.Id]); // 面板/聚合器丢弃残留状态
            _runtime.Config.RemoveWidget(widget.Id);
            _runtime.ReloadWidgets(); // 调度器同步移除（防幽灵刷新）
            RebuildWidgets();
            PinsChanged?.Invoke();
        };
        row.Children.Add(label);
        row.Children.Add(delete);
        return row;
    }

    // —— 通知规则与通用小件 ——

    private string DescribeRules()
    {
        var rules = _runtime.Config.App.NotificationRules.Count > 0
            ? _runtime.Config.App.NotificationRules
            : DefaultNotificationRules.All;
        if (rules.Count == 0)
        {
            return "（无规则）";
        }
        return string.Join("\n", rules.Select(rule =>
            $"· {(rule.WidgetType ?? "*")} ≥{rule.SeverityAtLeast}{(rule.SeverityAtMost is { } atMost ? $" ≤{atMost}" : "")}"
            + $"{(rule.Toast ? " · Toast" : "")}{(rule.Sound ? " · 声音" : "")}"
            + (rule.Cooldown is { } cooldown ? $" · 冷却 {cooldown.TotalMinutes:F0}min" : " · 每阈值一次")));
    }

    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 15,
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

    private static string? TagOf(ComboBox box)
        => box.SelectedItem is ComboBoxItem { Tag: { } tag } ? tag.ToString() : null;
}
