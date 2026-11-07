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

internal sealed partial class SettingsWindow
{
    // —— PowerToys 形态：模块目录（左）与页面路由（右） ——

    private List<ModuleDef> _modules = [];
    private readonly Dictionary<string, Button> _moduleRows = [];
    private readonly Dictionary<string, ToggleSwitch> _moduleToggles = [];
    private StackPanel _leftPanel = null!;
    private ScrollViewer _rightHost = null!;
    private ColumnDefinition _leftColumn = null!;
    private TextBox _searchBox = null!;
    private TextBlock _moduleHeader = null!;
    private ModuleDef? _firstFilterHit;
    private double _splitterStartX;
    private double _splitterStartWidth;
    private string _selectedKey = "";
    private bool _syncingToggles;
    // 窗口尺寸记忆防抖（用户令「记住尺寸」）：连续 SizeChanged 只在静止 800ms 后写一次 config
    private DispatcherTimer? _windowSizeSave;

    /// <summary>左栏宽度钳位（2026-10-10 用户规格「固定起始 200」/右 min 420）：下限 200 保证
    /// 模块名（自动省略号）+开关不挤烂；上限 420 时 880 默认窗宽右栏仍 ≥420。</summary>
    private const double LeftColumnMin = 200;
    private const double LeftColumnMax = 420;

    /// <summary>拖拽分隔条：6px 命中区 + 居中 3px 高亮线，按指针位移重设左栏列宽（DIP 同单位，无需 DPI 换算）。</summary>
    private Border BuildColumnSplitter()
    {
        var grip = new Rectangle
        {
            Width = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 24, 0, 24),
            RadiusX = 1.5,
            RadiusY = 1.5,
            Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
        };
        var splitter = new Border
        {
            Width = 7,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), // 扩大命中区
            Child = grip,
        };
        ToolTipService.SetToolTip(splitter, "拖拽调整左栏宽度"); // 附加属性不能进对象初始化器（C# 语法）
        // 用户令：悬停变品牌色（#60A5FA 蓝），3px 抓握点一眼可辨
        splitter.PointerEntered += (_, _) => grip.Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 96, 165, 250));
        splitter.PointerExited += (_, _) => grip.Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139));
        splitter.PointerPressed += (sender, e) =>
        {
            if (sender is Border border && border.CapturePointer(e.Pointer))
            {
                _splitterStartX = e.GetCurrentPoint(border).Position.X;
                _splitterStartWidth = _leftColumn.Width.Value;
            }
        };
        splitter.PointerMoved += (sender, e) =>
        {
            if (sender is Border border && border.PointerCaptures?.Count > 0)
            {
                var x = e.GetCurrentPoint(border).Position.X;
                SetLeftColumnWidth(_splitterStartWidth + x - _splitterStartX);
            }
        };
        // 松手落盘（用户令「拖动宽度写进 config，启动恢复」——「可以拖动」的本体是持久化）
        splitter.PointerReleased += (sender, e) =>
        {
            ((Border)sender).ReleasePointerCapture(e.Pointer);
            _runtime.Config.App.SettingsSidebarWidth = _leftColumn.Width.Value;
            _runtime.Config.SaveApp();
        };
        // UIA 取证入口：Border 非 Control 无自带 peer，只设 AutomationId 不进 UIA 树（CI 38043812483 实证
        // 「未找到 settings-left-splitter」）——Name 强制创建 FrameworkElementAutomationPeer
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(splitter, "左栏宽度分隔条");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(splitter, "settings-left-splitter");
        return splitter;
    }

    private void SetLeftColumnWidth(double width)
        => _leftColumn.Width = new GridLength(Math.Clamp(width, LeftColumnMin, LeftColumnMax));

    /// <summary>窗口尺寸记忆：连续 SizeChanged 防抖 800ms 后按 Bounds（DIP）落盘，构造器启动恢复。
    /// Tick 走 DispatcherQueueGuard.RunGuarded——tick 与 TryEnqueue 同骑派发循环，异常同无系统级路由。</summary>
    private void ScheduleWindowSizeSave()
    {
        _windowSizeSave ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _windowSizeSave.Stop();
        _windowSizeSave.Tick -= OnWindowSizeSaveTick; // 成对 -=/+=：多次进入也只挂一个 handler
        _windowSizeSave.Tick += OnWindowSizeSaveTick;
        _windowSizeSave.Start();
    }

    private void OnWindowSizeSaveTick(object? sender, object e)
    {
        DispatcherQueueGuard.RunGuarded(() => SaveWindowSizeNow(sender, e));
    }

    private void SaveWindowSizeNow(object? sender, object e)
    {
        _windowSizeSave!.Stop();
        _windowSizeSave.Tick -= SaveWindowSizeNow;
        var b = Bounds;
        if (b.Width >= 600 && b.Height >= 400)
        {
            _runtime.Config.App.SettingsWindowWidth = Math.Round(b.Width);
            _runtime.Config.App.SettingsWindowHeight = Math.Round(b.Height);
            _runtime.Config.SaveApp();
        }
    }

    /// <summary>设置搜索（用户令兜底③）：词命中模块 DisplayName/Subtitle/Key 任一即留行，其余收起；
    /// 标题计数改「N/M 个模块」，首个命中记 _firstFilterHit 供回车跳转；空词全显复位。</summary>
    private void ApplyModuleFilter()
    {
        var query = _searchBox.Text.Trim();
        var hits = 0;
        _firstFilterHit = null;
        foreach (var module in _modules)
        {
            var match = query.Length == 0
                || module.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || module.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                || module.Key.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (_moduleRows.TryGetValue(module.Key, out var row))
            {
                row.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            }
            if (match)
            {
                hits++;
                _firstFilterHit ??= module;
            }
        }
        _moduleHeader.Text = query.Length == 0
            ? $"配置中心 · {_modules.Count} 个模块"
            : $"配置中心 · {hits}/{_modules.Count} 个模块匹配「{query}」";
    }

    /// <summary>当前页作用域：Provider 页锁定连接类型与组件描述符；常规/外观/高级页为 null（全量）。</summary>
    private string? _scopeType;
    private IReadOnlyList<WidgetTypeDescriptor>? _scopeDescriptors;

    private void RebuildModuleRows()
    {
        _leftPanel.Children.Clear();
        _moduleRows.Clear();
        _moduleToggles.Clear();
        _leftPanel.Children.Add(_moduleHeader = new TextBlock
        {
            // 2026-10-10 修：可见计数——用户要求列表条数一眼可见，不靠滚动到底确认
            Text = $"配置中心 · {_modules.Count} 个模块",
            FontSize = DesignTokens.FontBody,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Margin = new Thickness(10, 2, 0, 8),
        });
        foreach (var module in _modules)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            content.Children.Add(MakeModuleIcon(module));
            var nameStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock { Text = module.DisplayName, FontSize = DesignTokens.FontSubtitle, TextTrimming = TextTrimming.CharacterEllipsis }); // 200 窄列不挤烂（用户规格 min 200）
            nameStack.Children.Add(new TextBlock
            {
                Text = module.Subtitle,
                FontSize = DesignTokens.FontFootnote,
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
                CornerRadius = DesignTokens.RadiusCard,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(row, $"module-{module.Key}"); // UIA 取证/自动化入口
            var captured = module;
            row.Click += (_, _) => SelectModule(captured);
            _moduleRows[module.Key] = row;
            _leftPanel.Children.Add(row);
        }

        if (_searchBox.Text.Trim().Length > 0)
        {
            ApplyModuleFilter(); // 重建（切开关等）后重放当前过滤词，行数与计数一致
        }
    }

    /// <summary>模块图标（PowerToys 形态：每模块独立 icon）——品牌图优先（BrandIconFactory 本地内置），
    /// 未收录品牌退色块 + Segoe Fluent 字形兜底；品牌渲染失败同样退字形并落日志（单块失败不拖死整页）。</summary>
    private Border MakeModuleIcon(ModuleDef module)
    {
        // 2026-10-10 用户令「配置中心为啥有的图标有颜色，有的图标就黑灰色的呢」：全模块品牌色，
        // 不再有灰色兜底。无连接类型的四个本地模块（常规/外观/动作/高级）各给辨识色。
        var tint = module.Key switch
        {
            "general" => SeverityPalette.Rgb(255, 59, 130, 246), // 常规蓝 #3B82F6
            "appearance" => SeverityPalette.Rgb(255, 168, 85, 247), // 外观紫 #A855F7
            "github" => SeverityPalette.Rgb(255, 36, 41, 47), // GitHub 墨黑 #24292F
            "jenkins" => SeverityPalette.Rgb(255, 211, 56, 51), // Jenkins 红 #D33833
            "machine" => SeverityPalette.Rgb(255, 16, 124, 16), // Windows 绿 #107C10
            "herald" => SeverityPalette.Rgb(255, 249, 115, 22), // Herald 琥珀（自有项目，非厂商）
            "bigmodel" => SeverityPalette.Rgb(255, 56, 89, 255), // 智谱蓝 #3859FF
            "ark" => SeverityPalette.Rgb(255, 41, 112, 255), // 火山引擎蓝
            "claude" => SeverityPalette.Rgb(255, 217, 119, 87), // Claude 赭橙 #D97757
            "kimi" => SeverityPalette.Rgb(255, 37, 99, 235), // Moonshot 蓝 #2563EB
            "moonshot" => SeverityPalette.Rgb(255, 79, 70, 229), // Moonshot 靛蓝 #4F46E5：与 kimi 行区分（同司共用品牌标，凭据不通用）
            "deepseek" => SeverityPalette.Rgb(255, 77, 107, 254), // DeepSeek 蓝 #4D6BFE
            "mimo" => SeverityPalette.Rgb(255, 255, 105, 0), // 小米橙 #FF6900
            "qwen" => SeverityPalette.Rgb(255, 255, 106, 0), // 阿里橙 #FF6A00（Simple Icons qwen）
            "sensenova" => SeverityPalette.Rgb(255, 0, 100, 188), // 商汤蓝 #0064BC（SenseCore 品牌深蓝）
            "codex" => SeverityPalette.Rgb(255, 16, 22, 34), // OpenAI 黑（近墨）
            "copilot" => SeverityPalette.Rgb(255, 142, 78, 198), // Copilot 紫渐变主色 #8E4EC6
            "opencode" => SeverityPalette.Rgb(255, 13, 148, 136), // OpenCode 青 #0D9488
            "http" => SeverityPalette.Rgb(255, 63, 185, 80), // HTTP 绿 #3FB950
            "actions" => SeverityPalette.Rgb(255, 245, 158, 11), // 动作琥珀 #F59E0B
            "advanced" => SeverityPalette.Rgb(255, 244, 63, 94), // 高级玫红 #F43F5E
            _ => SeverityPalette.Rgb(255, 148, 163, 184),
        };
        // icon 前景按 tint 亮度选白/黑（≥4.5:1）：mimo 橙/http 绿/opencode 青等亮底白字仅 ≈2.9:1，必须用黑
        var iconForeground = ThemeColors.IconOnTint(tint);
        var content = BrandIconFactory.TryCreate(module.ConnectionType, 15, iconForeground, _runtime.Logger)
            ?? new FontIcon
            {
                Glyph = module.Glyph,
                FontSize = DesignTokens.FontHeader,
                Foreground = new SolidColorBrush(iconForeground),
            };
        return new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = DesignTokens.RadiusControl,
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
            // 用户令选中态：品牌色左缘 3px 条（与 rail 同款语言），底色保留灰底可辨
            var selected = key == module.Key;
            row.Background = new SolidColorBrush(selected
                ? SeverityPalette.Rgb(60, 127, 127, 127)
                : Microsoft.UI.Colors.Transparent);
            row.BorderThickness = new Thickness(3, 0, 0, 0);
            row.BorderBrush = new SolidColorBrush(selected
                ? SeverityPalette.Rgb(255, 96, 165, 250)
                : Microsoft.UI.Colors.Transparent);
        }
        _rightHost.Content = module.Key switch
        {
            "general" => BuildGeneralPage(),
            "appearance" => BuildAppearancePage(),
            "components" => BuildComponentCenterPage(),
            "actions" => BuildActionsPage(),
            "advanced" => BuildAdvancedPage(),
            _ => BuildProviderPage(module),
        };
    }

    /// <summary>外部直达入口（托盘「组件中心」）：按模块键选中左栏并重建右栏；键不存在保持当前页（不抛）。</summary>
    public void OpenModule(string key)
    {
        var module = _modules.FirstOrDefault(m => m.Key == key);
        if (module is not null)
        {
            SelectModule(module);
        }
    }

    /// <summary>模块下的组件描述符：按「组件类型前缀 = 连接类型」约定（github.* / http.* / bigmodel.*）。</summary>
    private static IReadOnlyList<WidgetTypeDescriptor> ModuleDescriptors(string connectionType)
        => AllWidgetDescriptors().Where(d => d.Type.StartsWith(connectionType + ".", StringComparison.Ordinal)).ToList();

    private static IReadOnlyList<WidgetTypeDescriptor> AllWidgetDescriptors()
        => [.. GitHubWidgetDescriptors.All, .. JenkinsWidgetDescriptors.All, .. MachineWidgetDescriptors.All, .. GitWorkspaceDescriptors.All, .. HeraldWidgetDescriptors.All, .. HttpWidgetDescriptors.All, .. BigModelWidgetDescriptors.All, .. ArkWidgetDescriptors.All, .. ClaudeWidgetDescriptors.All, .. DeepSeekWidgetDescriptors.All, .. KimiWidgetDescriptors.All, .. MoonshotWidgetDescriptors.All, .. MiMoWidgetDescriptors.All, .. QwenWidgetDescriptors.All, .. SenseNovaWidgetDescriptors.All, .. CodexWidgetDescriptors.All, .. CopilotWidgetDescriptors.All, .. OpenCodeWidgetDescriptors.All];

}
