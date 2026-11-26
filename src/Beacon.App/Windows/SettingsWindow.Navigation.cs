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
    private SettingSearchEntry? _firstFieldHit; // SET-UX-08：字段级命中（回车优先于模块命中）
    private string? _pendingAnchorId; // SET-UX-08：待聚焦锚（切页重建后延一拍，布局量出几何再聚焦）
    private int _anchorRetries; // SET-UX-08：视觉树未就绪重试计数（150ms×20 拍 ≈3s 上限后放弃）
    private DispatcherTimer? _anchorFocusTimer; // SET-UX-08：锚聚焦重试泵（TryEnqueue 突发全在同批排空、跨不过布局帧——45356987498 实证，改定时器）
    private readonly Dictionary<string, TextBlock> _groupHeaders = []; // SET-UX-08：左栏分组头（组键 → 头控件）
    private readonly Dictionary<string, List<string>> _groupMembers = []; // SET-UX-08：组键 → 成员模块键（过滤时决定头显隐）
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

    /// <summary>SET-UX-08 字段搜索条目：设置词 →（模块, 控件锚）。空锚 = 模块级命中（整页跳转）。</summary>
    private sealed record SettingSearchEntry(string ModuleKey, string AnchorId, string[] Keywords);

    /// <summary>SET-UX-08 搜索索引：覆盖实际设置词与旧名（悬浮形态/悬浮框/检查频率/检测间隔…）。
    /// 匹配双向 Contains（词命中关键字或关键字命中词，忽略大小写）；回车按首个命中进页并聚焦控件。</summary>
    private static readonly SettingSearchEntry[] SearchEntries =
    [
        new("general", "poll-interval-box", ["检查频率", "刷新频率", "默认刷新间隔", "刷新间隔", "检测间隔", "轮询间隔"]),
        new("general", "hotkey-box", ["热键", "快捷键", "全局热键"]),
        new("general", "startup-toggle", ["开机自启", "自启动", "开机启动"]),
        new("general", "theme-box", ["主题", "深色", "浅色"]),
        new("general", "language-box", ["语言", "翻译"]),
        new("desktop", "desktop-layout-box", ["桌面组件布局", "组件布局", "悬浮形态", "集中面板", "独立显示"]),
        new("desktop", "numeric-floating-toggle", ["数值和额度", "悬浮框", "数值悬浮框", "在桌面显示数值"]),
        new("desktop", "board-toggle", ["允许打开看板", "看板入口", "看板"]),
        new("desktop", "desktop-opacity-slider", ["透明度", "不透明度"]),
        new("desktop", "edge-collapse-toggle", ["贴边收起", "贴边", "收起"]),
        new("desktop", "status-bar-island", ["悬浮岛", "状态条", "顶部悬浮岛"]),
        new("desktop", "status-bar-capsule", ["胶囊", "右下角胶囊"]),
        new("appearance", "appearance-motion-mode-box", ["动效", "动画"]),
        new("appearance", "appearance-motion-intensity", ["动效强度", "动画强度"]),
        new("appearance", "appearance-color-info", ["级别色", "状态颜色", "颜色覆盖"]),
        new("appearance", "appearance-density-box", ["密度"]),
        new("appearance", "appearance-progress-style-box", ["额度显示方式", "进度条", "圆池", "注水桶", "水桶"]),
        new("components", "", ["组件中心", "显隐", "上岛", "传统渲染", "组件树"]),
        new("actions", "", ["自定义动作", "动作", "打包", "webhook"]),
        new("advanced", "", ["导入", "导出", "备份", "诊断", "日志", "通知规则"]),
        new("github", "conn-save-button", ["GitHub", "仓库", "拉取请求"]),
        new("jenkins", "conn-save-button", ["Jenkins"]),
        new("machine", "conn-save-button", ["本机", "CPU", "内存", "Git"]),
        new("herald", "conn-save-button", ["Herald", "事件台账"]),
        new("bigmodel", "conn-save-button", ["智谱", "GLM", "额度"]),
        new("ark", "conn-save-button", ["火山方舟", "火山", "方舟"]),
        new("claude", "conn-save-button", ["Claude"]),
        new("kimi", "conn-save-button", ["Kimi"]),
        new("moonshot", "conn-save-button", ["Moonshot"]),
        new("deepseek", "conn-save-button", ["DeepSeek"]),
        new("mimo", "conn-save-button", ["小米", "MiMo"]),
        new("codex", "conn-save-button", ["Codex", "OpenAI"]),
        new("copilot", "conn-save-button", ["Copilot"]),
        new("opencode", "conn-save-button", ["OpenCode"]),
        new("qwen", "conn-save-button", ["千问", "百炼", "通义"]),
        new("sensenova", "conn-save-button", ["商汤", "SenseNova"]),
        new("http", "conn-save-button", ["HTTP", "自定义接口", "状态接口"]),
    ];

    /// <summary>左栏分组（SET-UX-08）：Provider 模块归「数据来源」，动作/高级归「备份与诊断」；
    /// 前四个一级入口（常规/桌面显示/外观/组件中心）不设头。null = 无分组。</summary>
    private static string? GroupOf(ModuleDef module)
        => module.ConnectionType is not null ? "sources"
        : module.Key is "actions" or "advanced" ? "advanced"
        : null;

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

    /// <summary>设置搜索（用户令兜底③ + SET-UX-08）：词命中模块 DisplayName/Subtitle/Key 任一即留行，
    /// 其余收起；字段索引（SearchEntries）另算命中数供回车跳控件；分组头在成员全被滤掉时隐藏；
    /// 标题计数「N/M 个模块 · K 设置项」，首个命中记 _firstFilterHit/_firstFieldHit；空词全显复位。</summary>
    private void ApplyModuleFilter()
    {
        var query = _searchBox.Text.Trim();
        var hits = 0;
        _firstFilterHit = null;
        _firstFieldHit = null;
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
        var fieldHits = 0;
        if (query.Length > 0)
        {
            foreach (var entry in SearchEntries)
            {
                if (entry.Keywords.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || query.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    fieldHits++;
                    _firstFieldHit ??= entry;
                }
            }
        }
        foreach (var (group, header) in _groupHeaders)
        {
            var anyVisible = _groupMembers.TryGetValue(group, out var members)
                && members.Any(member => _moduleRows.TryGetValue(member, out var memberRow)
                    && memberRow.Visibility == Visibility.Visible);
            header.Visibility = query.Length == 0 || anyVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        _moduleHeader.Text = query.Length == 0
            ? $"配置中心 · {_modules.Count} 个模块"
            : $"配置中心 · {hits}/{_modules.Count} 个模块匹配「{query}」"
              + (fieldHits > 0 ? $" · {fieldHits} 设置项" : "");
    }

    /// <summary>SET-UX-08 搜索跳转：切到命中模块页，再按锚聚焦控件。空锚只切页。</summary>
    private void JumpToSetting(SettingSearchEntry entry)
    {
        var module = _modules.FirstOrDefault(m => m.Key == entry.ModuleKey);
        if (module is null)
        {
            return;
        }
        SelectModule(module);
        if (entry.AnchorId.Length == 0)
        {
            return; // 模块级命中（组件中心/动作/高级整页），无控件锚
        }
        FocusAnchor(entry.AnchorId);
    }

    /// <summary>SET-UX-08/09 聚焦入口：按 AutomationId 定位右栏控件并滚动聚焦。切页后调用安全——
    /// 走 150ms 定时重试到视觉树就绪（Content 刚换时布局 pass 未跑，树可能还空；
    /// TryEnqueue 突发重试全在同一 dispatcher 批次排空、跨不过布局帧，run 45356987498 SG 实证）。</summary>
    private void FocusAnchor(string automationId)
    {
        _pendingAnchorId = automationId;
        _anchorRetries = 0;
        _anchorFocusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _anchorFocusTimer.Tick -= FocusPendingAnchor;
        _anchorFocusTimer.Tick += FocusPendingAnchor;
        _anchorFocusTimer.Start();
    }

    private void FocusPendingAnchor(object? sender, object e)
    {
        // Tick 与 TryEnqueue 同骑派发循环，异常无系统级路由——同 OnWindowSizeSaveTick 走 RunGuarded
        DispatcherQueueGuard.RunGuarded(() =>
        {
            if (_pendingAnchorId is not { } anchorId)
            {
                _anchorFocusTimer?.Stop();
                return;
            }
            if (_rightHost.Content is DependencyObject root)
            {
                var target = FindInVisualTree(root, anchorId) ?? ExpandAllAndFind(root, anchorId);
                if (target is not null)
                {
                    _anchorFocusTimer?.Stop();
                    _pendingAnchorId = null;
                    _anchorRetries = 0;
                    FocusTarget(target);
                    return;
                }
            }
            if (++_anchorRetries > 20)
            {
                _anchorFocusTimer?.Stop(); // 放弃：锚不在本页（150ms×20 ≈3s 上限，不无限重试）
                _pendingAnchorId = null;
                _anchorRetries = 0;
            }
        });
    }

    /// <summary>SET-UX-08：展开祖先折叠区（Expander）后滚动并聚焦目标控件。</summary>
    private void FocusTarget(FrameworkElement target)
    {
        var expanded = false;
        var parent = VisualTreeHelper.GetParent(target);
        while (parent is not null)
        {
            if (parent is Expander { IsExpanded: false } expander)
            {
                expander.IsExpanded = true; // 目标在折叠区内：先展开，否则 BringIntoView 量不到几何
                expanded = true;
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        void FocusStep()
        {
            if (target is Control control)
            {
                control.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
            }
            target.StartBringIntoView();
        }
        if (expanded)
        {
            DispatcherQueue.TryEnqueue(FocusStep); // 展开动画/布局完成后再量几何
        }
        else
        {
            FocusStep();
        }
    }

    /// <summary>视觉树 BFS：按 AutomationId 找 FrameworkElement（右栏单页规模小，无性能忧）。</summary>
    private static FrameworkElement? FindInVisualTree(DependencyObject root, string automationId)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(node, i) is not DependencyObject child)
                {
                    continue;
                }
                if (child is FrameworkElement element
                    && string.Equals(Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(element), automationId, StringComparison.Ordinal))
                {
                    return element;
                }
                queue.Enqueue(child);
            }
        }
        return null;
    }

    private static FrameworkElement? ExpandAllAndFind(DependencyObject root, string automationId)
    {
        foreach (var expander in CollectExpanders(root))
        {
            expander.IsExpanded = true;
        }
        return FindInVisualTree(root, automationId);
    }

    private static List<Expander> CollectExpanders(DependencyObject root)
    {
        var result = new List<Expander>();
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(node, i) is DependencyObject child)
                {
                    if (child is Expander expander)
                    {
                        result.Add(expander);
                    }
                    queue.Enqueue(child);
                }
            }
        }
        return result;
    }

    /// <summary>当前页作用域：Provider 页锁定连接类型与组件描述符；常规/外观/高级页为 null（全量）。</summary>
    private string? _scopeType;
    private IReadOnlyList<WidgetTypeDescriptor>? _scopeDescriptors;

    private void RebuildModuleRows()
    {
        _leftPanel.Children.Clear();
        _moduleRows.Clear();
        _moduleToggles.Clear();
        _groupHeaders.Clear();
        _groupMembers.Clear();
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
            // SET-UX-ROW-1：行容器改 Grid 三列（Auto 图标 / * 文字 / Auto 开关）。横向 StackPanel 不约束
            // 子项宽度——文字长时 nameStack 无限伸长把开关挤出可视区，副标题长短不一又让各列开关
            // 右缘漂移；星号列才是 TextTrimming 真正生效的宽度约束。
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = MakeModuleIcon(module);
            icon.VerticalAlignment = VerticalAlignment.Center; // 列宽 Auto，纵轴不拉伸（色块高度不变）
            // UIA 取证/CI 入口（BEACON-FIX-1126 病灶 C 间距断言）：Border 只设 AutomationId 不进 UIA 树，
            // SetName 强制造 peer（38138106644 教训同口径）——间距门按 icon 右缘→文字左缘直读
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(icon, $"{module.DisplayName} 图标");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(icon, $"module-icon-{module.Key}");
            Grid.SetColumn(icon, 0);
            content.Children.Add(icon);
            var nameStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
            var nameBlock = new TextBlock { Text = module.DisplayName, FontSize = DesignTokens.FontSubtitle, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap }; // 200 窄列不挤烂（用户规格 min 200）
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(nameBlock, $"module-name-{module.Key}"); // UIA 取证/CI 入口（病灶 C 间距断言文字缘锚）
            nameStack.Children.Add(nameBlock);
            nameStack.Children.Add(new TextBlock
            {
                Text = module.Subtitle,
                FontSize = DesignTokens.FontFootnote,
                Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            });
            Grid.SetColumn(nameStack, 1);
            content.Children.Add(nameStack);

            if (module.ConnectionType is { } type)
            {
                var toggle = new ToggleSwitch
                {
                    OnContent = "",
                    OffContent = "",
                    IsOn = TypeEnabled(type),
                    Margin = new Thickness(DesignTokens.RowInline, 0, 0, 0), // SET-UX-ROW-1：与连接行同口径
                    Width = 44, // 病灶 F：MinWidth 40 < 实际轨宽——固定 44 实测值，列宽恒定不挤文字列
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
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(toggle, $"module-toggle-{module.Key}"); // UIA 取证/CI 入口（SET-UX-ROW-1 开关列对齐断言锚）
                Grid.SetColumn(toggle, 2); // 开关列 Auto 右贴：行右缘锚点统一
                content.Children.Add(toggle);
            }

            // SET-UX-08：首个成员行前插分组头（数据来源 / 备份与诊断），一级四入口不设头
            if (GroupOf(module) is { } group)
            {
                if (!_groupHeaders.TryGetValue(group, out var header))
                {
                    header = new TextBlock
                    {
                        Text = group == "sources" ? "数据来源" : "备份与诊断",
                        FontSize = DesignTokens.FontFootnote,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
                        Margin = new Thickness(10, 10, 0, 4),
                    };
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(header, group == "sources" ? "nav-group-sources" : "nav-group-advanced");
                    _groupHeaders[group] = header;
                    _groupMembers[group] = [];
                    _leftPanel.Children.Add(header);
                }
                _groupMembers[group].Add(module.Key);
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
            "desktop" => SeverityPalette.Rgb(255, 6, 182, 212), // 桌面显示青 #06B6D4（TVMonitor 字形）
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
            Margin = new Thickness(0, 0, DesignTokens.IconTextGap, 0), // 病灶 C：图标与文字零间距→10 DIP
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
            "desktop" => BuildDesktopPage(), // SET-UX-02/03：状态条三选一 + 窗口与看板（桌面显示统一入口）
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
