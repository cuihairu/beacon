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
    // —— 页面：常规 ——

    private UIElement BuildGeneralPage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("常规"));
        _hotkeyBox = new TextBox { Header = "全局热键（如 Ctrl+Alt+B）", Text = _runtime.Config.App.Hotkey, Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_hotkeyBox, "hotkey-box"); // UIA 取证：热键驱动（SET-UX-11 场景 SJ）
        _hotkeyBox.LostFocus += (_, _) => SaveHotkey(); // SET-UX-01 单项保存：只写 Hotkey，不碰其他字段
        // SET-UX-11：生效时机与影响面写在控件旁；失败反馈行紧贴热键框（修复入口=上方框，改完即再注册）
        _hotkeyFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_hotkeyFeedback, "hotkey-feedback");
        _startupToggle = new ToggleSwitch { Header = "开机自启", IsOn = _runtime.Config.App.LaunchOnStartup };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_startupToggle, "startup-toggle"); // UIA 取证：开机自启驱动
        _startupToggle.Toggled += (_, _) => SaveStartup(); // SET-UX-01 单项保存：只写 LaunchOnStartup
        _themeBox = new ComboBox { Header = "主题（重启生效）", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_themeBox, "theme-box"); // UIA 取证/搜索跳转锚（SET-UX-08 场景 SG）
        foreach (var item in new[] { ("system", "跟随系统"), ("light", "浅色"), ("dark", "深色") })
        {
            _themeBox.Items.Add(new ComboBoxItem { Content = item.Item2, Tag = item.Item1 });
        }
        _themeBox.SelectedIndex = IndexOfTag(_themeBox, _runtime.Config.App.Theme);
        _themeBox.SelectionChanged += (_, _) => SaveTheme(); // SET-UX-01 单项保存；主题构造期锁死（App 构造器读），免广播
        // 界面语言（2026-10-14 用户单②）：看板/托盘即时生效（UiStrings.LanguageChanged 广播重画）；
        // 设置页存量中文文案暂未整体迁移（UiStrings 覆盖面记录在案）。语言名按惯例用本名不翻译。
        _languageBox = new ComboBox { Header = UiStrings.SettingsLanguage, Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_languageBox, "language-box"); // UIA 取证：语言切换驱动
        foreach (var item in new[] { ("", UiStrings.SettingsLanguageAuto), ("zh", "中文"), ("en", "English") })
        {
            var languageTag = item.Item1.Length == 0 ? "auto" : item.Item1; // 插值内不放条件表达式（WinUI 坑记录），先算出 id
            var languageItem = new ComboBoxItem { Content = item.Item2, Tag = item.Item1 };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(languageItem, $"language-{languageTag}"); // UIA 取证：语言切换驱动（场景 W）
            _languageBox.Items.Add(languageItem);
        }
        _languageBox.SelectedIndex = IndexOfTag(_languageBox, _runtime.Config.App.Language);
        _languageBox.SelectionChanged += (_, _) => SaveLanguage(); // SET-UX-01 单项保存：只写 Language
        // 全局检查频率（2026-10-09 用户令）：组件级「检测间隔」优先，未设组件级的按此走；改后立即重建调度。
        // SET-UX-01：预设外合法旧值（如 120 秒）补「自定义」项按原值回显——此前无档可选且全字段回写会把它洗回默认。
        // SET-UX-10 正名：「检查频率」→「默认刷新间隔」（与组件行「此组件刷新频率」同一套继承词汇）。
        _pollBox = new ComboBox { Header = "默认刷新间隔（未单独设置的组件）", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_pollBox, "poll-interval-box"); // UIA 取证：检查频率改动驱动
        foreach (var preset in PollIntervalOptions.Presets)
        {
            var pollItem = new ComboBoxItem { Content = preset.Label, Tag = preset.Seconds.ToString(CultureInfo.InvariantCulture) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(pollItem, $"poll-{preset.Seconds}");
            _pollBox.Items.Add(pollItem);
        }
        var configuredPoll = _runtime.Config.App.PollIntervalSeconds;
        if (configuredPoll > 0 && !PollIntervalOptions.IsPreset(configuredPoll))
        {
            var customItem = new ComboBoxItem { Content = PollIntervalOptions.CustomLabel(configuredPoll), Tag = configuredPoll.ToString(CultureInfo.InvariantCulture) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(customItem, "poll-custom");
            _pollBox.Items.Add(customItem); // 自定义项在预设后追加，原值原样保留
        }
        _pollBox.SelectedIndex = IndexOfTag(_pollBox, configuredPoll > 0 ? configuredPoll.ToString(CultureInfo.InvariantCulture) : "0");
        _pollBox.SelectionChanged += (_, _) => SavePoll(); // SET-UX-01 单项保存：只写 PollIntervalSeconds
        page.Children.Add(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                _hotkeyBox,
                Hint("全局热键即时生效（改完即重新注册）；被占用或无效时保留原热键，原因见下一行反馈。"),
                _hotkeyFeedback!,
                _startupToggle,
                // SET-UX-11：生效时机与影响面注明（写 HKCU Run 键保存即落，下次登录随 explorer 拉起）
                Hint("写入当前用户注册表 Run 键（保存即时生效）；下次登录 Windows 时随 explorer 自动拉起。"),
                _themeBox,
                _languageBox,
                // SET-UX-11：语言覆盖面注明（看板/托盘即时，其余界面暂未全面迁移）
                Hint("语言当前作用于看板与托盘（即时生效）；设置窗口等其余界面暂未全面翻译。"),
                _pollBox,
                Hint("默认刷新间隔即时重建调度；只对未单独设置刷新频率的组件生效，组件覆盖优先（组件向导设「此组件刷新频率」）。"),
            },
        });
        page.Children.Add(Hint("状态条（关闭/右下角胶囊/顶部悬浮岛）、窗口布局、看板入口、不透明度与贴边收起在「桌面显示」页（即时生效）；组件显隐与上岛在「组件中心」。"));
        page.Children.Add(BuildAboutSection());
        return page;
    }

    /// <summary>组件中心（Component Center）：当前全部可展示组件（widgets.json 实例）按真实注册类型
    /// 分组（ComponentCenterCatalog 类别，纯 Core 单源）列出，勾选=显示、取消=隐藏；隐藏集落
    /// config.json hiddenComponents（AppConfig.HiddenComponents），主宿主面板与独立悬浮框只渲染
    /// 未隐藏组件（两宿主 ReloadTiles 同口径过滤），重启保留。钉选/布局数据不动，重新勾选即恢复。
    /// 页首「展示载体」区（ISLAND-RING-1 扩围②，2026-11-07 用户令）：悬浮岛/悬浮框/看板三项勾选
    /// ——「一个页面决定显示什么 + 用什么显示」；旧开关收编到这一处，本页其它位置零重复入口。</summary>
    private UIElement BuildComponentCenterPage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("组件中心"));
        page.Children.Add(Hint("按类型分组的全部可展示组件：勾选=显示到悬浮面板，取消=隐藏（即时生效、重启保留）。钉选与布局数据不受影响，重新勾选即恢复。"));

        // —— 展示载体（SET-UX-02/03 收编进「桌面显示」页，2026-11-23 用户令原型落地批1）：
        // 状态条三选一（关闭/右下角胶囊/顶部悬浮岛，投影 CapsuleIsland+ShowCapsule 成组保存）+
        // 窗口与看板区（桌面组件布局=PinDisplayMode / 在桌面显示数值和额度=NumericFloatingEnabled /
        // 允许打开看板=BoardEnabled / 不透明度 / 贴边收起）。本页不再放重复入口——此前两处开关
        // 分管两字段，关岛可能露出右下角胶囊（SET-UX-02 问题面）。岛内容设置保留在本页岛组内，
        // 岛未启用时折叠（BuildIslandSection 口径），列表与顺序在 config 里原样保留。
        page.Children.Add(SectionTitle("展示载体"));
        page.Children.Add(Hint("载体入口已统一到「桌面显示」页：状态条三选一（关闭 / 右下角胶囊 / 顶部悬浮岛）+ 窗口与看板（桌面组件布局 / 在桌面显示数值和额度 / 允许打开看板 / 不透明度 / 贴边收起），全部即时生效。"));

        var groups = ComponentCenterCatalog.Group(_runtime.Config.Widgets);
        if (groups.Count == 0)
        {
            page.Children.Add(Hint("尚无组件——先在左侧模块页创建连接与组件，再回这里管理显隐。"));
            return page;
        }

        // SET-UX-04 四列表头（原型：组件 | 桌面 | 悬浮岛 | 设置）：列宽与行内 Grid 同像素值对齐；
        // 岛组（末位、虚拟环/条两列行）在表头覆盖范围之外，不受影响。
        page.Children.Add(BuildComponentCenterHeader());

        foreach (var group in groups)
        {
            page.Children.Add(SectionTitle($"{group.DisplayName}（{group.Items.Count}）"));
            if (group.Key == ComponentCenterCatalog.CategoryIsland)
            {
                page.Children.Add(BuildIslandSection(group)); // ISLAND-D1/D2：展示方式 + 上岛勾选
            }
            else
            {
                foreach (var item in group.Items)
                {
                    page.Children.Add(BuildComponentCenterRow(item));
                }
            }
        }
        return page;
    }

    /// <summary>组件中心四列表头（SET-UX-04，2026-11-24 原型落地批2）：与行内 Grid 同列宽，
    /// 岛组（末位、旧两列行）不在此口径内。</summary>
    private UIElement BuildComponentCenterHeader()
    {
        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170, GridUnitType.Pixel) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160, GridUnitType.Pixel) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foreach (var (column, text) in new[] { (1, "组件"), (2, "桌面"), (3, "悬浮岛"), (4, "设置") })
        {
            var cell = new TextBlock
            {
                Text = text,
                FontSize = DesignTokens.FontCaption,
                Foreground = new SolidColorBrush(ThemeColors.TextTertiary()),
            };
            Grid.SetColumn(cell, column);
            header.Children.Add(cell);
        }
        header.Padding = new Thickness(0, 2, 0, 2);
        return header;
    }

    /// <summary>组件中心单行（SET-UX-04，2026-11-24 原型落地批2）：四列「组件 | 桌面 | 悬浮岛 | 设置」。
    /// 桌面/悬浮岛两列由 Core ComponentPlacement 单源判定（镜像 L0 宿主过滤链与岛渲染口径），
    /// 状态主行按三档配色（绿=在位 / 灰=未就位 / 琥珀=需处理）+ 阻挡原因小字——组件中心行与桌面
    /// 实况共用同一判定，不再各说各话。勾选框仍是全局显隐恢复入口（component-center-{id} 锚不变）；
    /// 设置列：编辑样式（样式中心）/ 加入悬浮岛（白名单尾插，落库后岛列就地刷新）/ 打开数据来源
    /// （来源已停用时直达模块页）。副题钉选态/隐藏态改由两列状态承接（不再重复一行文字）。</summary>
    private UIElement BuildComponentCenterRow(ComponentCenterItem item)
    {
        var widget = _runtime.Config.Widgets.FirstOrDefault(w => w.Id == item.WidgetId);
        if (widget is null)
        {
            return BuildVirtualCenterRow(item); // 岛虚拟环/条不占桌面、不走去向判定（旧两列口径保留）
        }

        var app = _runtime.Config.App;
        var descriptor = AllWidgetDescriptors().FirstOrDefault(d => d.Type == widget.Type);
        var connection = _runtime.Config.Connections.FirstOrDefault(c => c.Id == widget.ConnectionId);
        var desktop = ComponentPlacement.DescribeDesktop(widget, descriptor, app,
            connectionEnabled: connection?.Enabled ?? true, // 缺连接不拦（宿主同口径，不算停用）
            numericFloatingEnabled: app.NumericFloatingEnabled,
            floatingMode: string.Equals(app.PinDisplayMode, "floating", StringComparison.OrdinalIgnoreCase));
        var islandOn = StatusBarMode.Read(app) == StatusBarMode.Island;
        var islandRank = app.IslandComponents.IndexOf(item.WidgetId) + 1;
        var island = ComponentPlacement.DescribeIsland(app, item.WidgetId, islandOn, islandRank);

        // 列0：全局显隐勾选（隐藏=全载体消失 + 退出聚合；取消隐藏即恢复）
        var toggle = new CheckBox
        {
            IsChecked = !ComponentCenterCatalog.IsHidden(app, item.WidgetId),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(toggle, $"component-center-{item.WidgetId}");

        // 列1：组件（显示名 + 类型键 · 来源）
        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var subtitle = new TextBlock
        {
            Text = $"{item.TypeName} · {connection?.Id ?? "未连接来源"}",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var textColumn = new StackPanel { Spacing = 2 };
        textColumn.Children.Add(label);
        textColumn.Children.Add(subtitle);

        // 列2/3：桌面 / 悬浮岛（状态主行 + 原因小字；TextWrapping 保证长原因不被截断）
        var desktopText = new TextBlock
        {
            Text = DesktopStateLabel(desktop.State),
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(PlacementStateColor(desktop.State)),
            TextWrapping = TextWrapping.Wrap,
        };
        var desktopNote = new TextBlock
        {
            Text = desktop.Note,
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.TextTertiary()),
            TextWrapping = TextWrapping.Wrap,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(desktopText, $"component-desktop-state-{item.WidgetId}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(desktopNote, $"component-desktop-note-{item.WidgetId}");
        var desktopColumn = new StackPanel { Spacing = 2 };
        desktopColumn.Children.Add(desktopText);
        desktopColumn.Children.Add(desktopNote);

        var islandText = new TextBlock
        {
            Text = IslandStateLabel(island.State),
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(PlacementStateColor(island.State)),
            TextWrapping = TextWrapping.Wrap,
        };
        var islandNote = new TextBlock
        {
            Text = island.Note,
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.TextTertiary()),
            TextWrapping = TextWrapping.Wrap,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(islandText, $"component-island-state-{item.WidgetId}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(islandNote, $"component-island-note-{item.WidgetId}");
        var islandColumn = new StackPanel { Spacing = 2 };
        islandColumn.Children.Add(islandText);
        islandColumn.Children.Add(islandNote);

        // 列4：设置（编辑样式恒有；未上岛补「加入悬浮岛」；来源显式停用补「打开数据来源」直达模块页）
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Top };
        var styleButton = new Button { Content = "编辑样式", Padding = new Thickness(8, 2, 8, 2) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(styleButton, $"component-style-{item.WidgetId}");
        styleButton.Click += (_, _) => OpenComponentStyleCenter(widget);
        actions.Children.Add(styleButton);
        if (island.State != ComponentPlacement.IslandOnboarded)
        {
            var onboardButton = new Button { Content = "加入悬浮岛", Padding = new Thickness(8, 2, 8, 2) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(onboardButton, $"component-onboard-{item.WidgetId}");
            onboardButton.Click += (_, _) =>
            {
                SaveIslandOnboard(item.WidgetId, onboarded: true);
                // 岛列就地刷新（不重建整页）：上岛后重跑 DescribeIsland 同一判定源
                var rank = app.IslandComponents.IndexOf(item.WidgetId) + 1;
                var refreshed = ComponentPlacement.DescribeIsland(app, item.WidgetId,
                    StatusBarMode.Read(app) == StatusBarMode.Island, rank);
                islandText.Text = IslandStateLabel(refreshed.State);
                islandText.Foreground = new SolidColorBrush(PlacementStateColor(refreshed.State));
                islandNote.Text = refreshed.Note;
                actions.Children.Remove(onboardButton);
            };
            actions.Children.Add(onboardButton);
        }
        if (connection is { Enabled: false })
        {
            var sourceButton = new Button { Content = "打开数据来源", Padding = new Thickness(8, 2, 8, 2) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(sourceButton, $"component-source-{item.WidgetId}");
            // 模块键=组件类型前缀（与 ModuleDescriptors「前缀=连接类型」约定同源）
            sourceButton.Click += (_, _) => OpenModule(widget.Type.Split('.')[0]);
            actions.Children.Add(sourceButton);
        }

        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170, GridUnitType.Pixel) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160, GridUnitType.Pixel) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(toggle, 0);
        Grid.SetColumn(textColumn, 1);
        Grid.SetColumn(desktopColumn, 2);
        Grid.SetColumn(islandColumn, 3);
        Grid.SetColumn(actions, 4);
        row.Children.Add(toggle);
        row.Children.Add(textColumn);
        row.Children.Add(desktopColumn);
        row.Children.Add(islandColumn);
        row.Children.Add(actions);
        row.Padding = new Thickness(0, 2, 0, 2);

        // 先取 IsChecked 再挂事件（构造期赋值不触发写路径；用户点击才落库）
        toggle.Checked += (_, _) => SaveComponentVisibility(item.WidgetId, visible: true);
        toggle.Unchecked += (_, _) => SaveComponentVisibility(item.WidgetId, visible: false);
        return row;
    }

    /// <summary>岛虚拟环/条行（旧两列口径）：不占桌面、不走去向判定，勾选显隐直读（行为不变）。</summary>
    private UIElement BuildVirtualCenterRow(ComponentCenterItem item)
    {
        var hidden = ComponentCenterCatalog.IsHidden(_runtime.Config.App, item.WidgetId);
        var toggle = new CheckBox
        {
            IsChecked = !hidden,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(toggle, $"component-center-{item.WidgetId}");

        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var subtitle = new TextBlock
        {
            Text = $"{item.TypeName}{(hidden ? " · 已隐藏" : "")}",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var textColumn = new StackPanel { Spacing = 2 };
        textColumn.Children.Add(label);
        textColumn.Children.Add(subtitle);

        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(toggle, 0);
        Grid.SetColumn(textColumn, 1);
        row.Children.Add(toggle);
        row.Children.Add(textColumn);
        row.Padding = new Thickness(0, 2, 0, 2);

        // 先取 IsChecked 再挂事件（构造期赋值不触发写路径；用户点击才落库）
        toggle.Checked += (_, _) => SaveComponentVisibility(item.WidgetId, visible: true);
        toggle.Unchecked += (_, _) => SaveComponentVisibility(item.WidgetId, visible: false);
        return row;
    }

    /// <summary>桌面列状态短句（封闭词表直配；完整原因由行内小字承接）。</summary>
    private static string DesktopStateLabel(string state) => state switch
    {
        ComponentPlacement.DesktopShown => "已显示",
        ComponentPlacement.DesktopUnpinned => "未钉选",
        ComponentPlacement.DesktopUnsupported => "不支持钉选",
        ComponentPlacement.DesktopGlobalHidden => "已隐藏",
        ComponentPlacement.DesktopSourceDisabled => "来源已停用",
        ComponentPlacement.DesktopNumericSuppressed => "数值总开关关闭",
        _ => "未知",
    };

    /// <summary>悬浮岛列状态短句（封闭词表直配）。</summary>
    private static string IslandStateLabel(string state) => state switch
    {
        ComponentPlacement.IslandOnboarded => "已上岛",
        ComponentPlacement.IslandNotOnboarded => "未上岛",
        ComponentPlacement.IslandOff => "岛未启用",
        ComponentPlacement.IslandHidden => "已隐藏",
        ComponentPlacement.IslandOverflows => "收起行溢出",
        _ => "未知",
    };

    /// <summary>去向状态三档配色：绿=在位（桌面已显示/已上岛），琥珀=需处理（隐藏/来源停用/不支持），
    /// 灰=未就位但属正常可配置态（未钉选/未上岛/岛未启用/溢出）。</summary>
    private static global::Windows.UI.Color PlacementStateColor(string state) => state switch
    {
        ComponentPlacement.DesktopShown or ComponentPlacement.IslandOnboarded => ThemeColors.StatusOk(),
        ComponentPlacement.DesktopGlobalHidden or ComponentPlacement.DesktopSourceDisabled
            or ComponentPlacement.DesktopUnsupported or ComponentPlacement.IslandHidden => ThemeColors.StatusWarn(),
        _ => ThemeColors.TextSecondary(),
    };

    /// <summary>组件中心显隐落库（设置页唯一写入口）：Core SetVisible 单源改集 → SaveApp → PinsChanged
    /// 联动（App 侧既有路径重挂 L0 两宿主 ReloadTiles，隐藏/恢复即时生效）。</summary>
    private void SaveComponentVisibility(string widgetId, bool visible)
    {
        ComponentCenterCatalog.SetVisible(_runtime.Config.App, widgetId, visible);
        _runtime.Config.SaveApp();
        PinsChanged?.Invoke();
    }

    /// <summary>岛区（ISLAND-D1..D2，2026-11-09 用户令）：岛收起态展示方式三档（单组件常驻/多组件堆叠/
    /// 徽标+轮播，落 AppConfig.IslandDisplayMode）+ 虚拟环/条显隐（既有行，勾选口径不变）+
    /// 全部真实组件的「上岛」勾选（AppConfig.IslandComponents 白名单，缺席=不上岛，列表序=上岛序）。
    /// SET-UX-02：状态条未选「顶部悬浮岛」时整块折叠（Visibility 面不动配置——白名单列表与顺序
    /// 在 config 原样保留，恢复岛后原样回来），留一行引导提示。</summary>
    private UIElement BuildIslandSection(ComponentCenterGroup group)
    {
        var panel = new StackPanel { Spacing = 4 };
        var islandOn = StatusBarMode.Read(_runtime.Config.App) == StatusBarMode.Island;

        if (!islandOn)
        {
            panel.Children.Add(Hint("悬浮岛未启用（桌面显示 → 桌面状态条选「顶部悬浮岛」后可配置岛收起态内容；下方设置已折叠，列表与顺序原样保留）。"));
            return panel;
        }

        _islandModeBox = new ComboBox
        {
            Header = "岛收起态展示方式",
            Width = 260,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        foreach (var (mode, label) in new[]
        {
            (IslandDisplayPlan.ModeSingle, "单组件常驻"),
            (IslandDisplayPlan.ModeStack, "多组件堆叠"),
            (IslandDisplayPlan.ModeCarousel, "徽标+轮播"),
        })
        {
            _islandModeBox.Items.Add(new ComboBoxItem { Content = label, Tag = mode });
        }
        var modeIndex = IndexOfTag(_islandModeBox, _runtime.Config.App.IslandDisplayMode);
        _islandModeBox.SelectedIndex = modeIndex >= 0 ? modeIndex : 1; // 非法/缺键回堆叠（计划侧 Normalize 同口径）
        _islandModeBox.SelectionChanged += (_, _) => SaveIslandDisplayMode();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_islandModeBox, "island-display-mode");
        panel.Children.Add(_islandModeBox);

        // 虚拟环/条显隐（既有行：组件中心勾选口径不变）
        foreach (var item in group.Items)
        {
            panel.Children.Add(BuildComponentCenterRow(item));
        }

        panel.Children.Add(Hint("真实组件上岛：勾选=岛收起态按勾选序渲染（虚拟环/条在前、此列表序续后）；" +
            "取消=不上岛。组件中心勾掉的组件即使上岛也不渲染（全载体消失口径）。"));
        foreach (var item in ComponentCenterCatalog.OnboardCandidates(_runtime.Config.Widgets))
        {
            panel.Children.Add(BuildIslandOnboardRow(item));
        }
        return panel;
    }

    /// <summary>上岛勾选行（ISLAND-D2）：勾选框 + 显示名/副题（类型键 · 上岛态）。勾选变化即时落库并联动岛面。</summary>
    private UIElement BuildIslandOnboardRow(ComponentCenterItem item)
    {
        var onboarded = ComponentCenterCatalog.IslandOnboarded(_runtime.Config.App, item.WidgetId);
        var toggle = new CheckBox
        {
            IsChecked = onboarded,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(toggle, $"island-onboard-{item.WidgetId}");

        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(ThemeColors.Label()),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var subtitle = new TextBlock
        {
            Text = $"{item.TypeName} · {(onboarded ? "已上岛" : "未上岛")}",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var textColumn = new StackPanel { Spacing = 2 };
        textColumn.Children.Add(label);
        textColumn.Children.Add(subtitle);

        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(toggle, 0);
        Grid.SetColumn(textColumn, 1);
        row.Children.Add(toggle);
        row.Children.Add(textColumn);
        row.Padding = new Thickness(0, 2, 0, 2);

        toggle.Checked += (_, _) => SaveIslandOnboard(item.WidgetId, onboarded: true);
        toggle.Unchecked += (_, _) => SaveIslandOnboard(item.WidgetId, onboarded: false);
        return row;
    }

    /// <summary>岛展示方式落库（ISLAND-D1）：IslandDisplayMode 单源 → SaveApp → PinsChanged
    /// （App → capsule.RefreshComponents → 计划重算，即时生效）。</summary>
    private void SaveIslandDisplayMode()
    {
        _runtime.Config.App.IslandDisplayMode = TagOf(_islandModeBox) ?? IslandDisplayPlan.ModeStack;
        _runtime.Config.SaveApp();
        PinsChanged?.Invoke();
    }

    /// <summary>上岛勾选落库（ISLAND-D2）：Core SetIslandOnboarded 单源 → SaveApp → PinsChanged
    /// （同上即时生效；列表序=上岛序）。</summary>
    private void SaveIslandOnboard(string widgetId, bool onboarded)
    {
        ComponentCenterCatalog.SetIslandOnboarded(_runtime.Config.App, widgetId, onboarded);
        _runtime.Config.SaveApp();
        PinsChanged?.Invoke();
    }

    /// <summary>「关于」（master-plan §10）：版本行 + 检查更新（B-806 真比较）+ 开源许可证 + 项目主页。
    /// 三行口径：当前版本 / 远程最新版本 / 检查结果（+ 最后检查时间）。启动约 20 秒自检一次并缓存
    /// （GitHub 未认证 60 次/小时/IP），手动按钮强刷；失败一律「无法检查更新 + 时间」，不弹异常框。</summary>
    private UIElement BuildAboutSection()
    {
        var panel = new StackPanel { Spacing = 6 };
        var informational = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        panel.Children.Add(new TextBlock
        {
            Text = $"Beacon 版本：{(string.IsNullOrEmpty(informational) ? UpdateCheckService.LocalVersion() : informational)}",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
        });
        var remoteText = new TextBlock { FontSize = DesignTokens.FontCaption, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)) };
        var verdictText = new TextBlock { FontSize = DesignTokens.FontCaption, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)) };
        var checkedText = new TextBlock { FontSize = DesignTokens.FontCaption, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)) };
        panel.Children.Add(remoteText);
        panel.Children.Add(verdictText);
        panel.Children.Add(checkedText);

        var downloadButton = AboutLinkButton("打开下载页", UpdateCheckService.DownloadPage);
        downloadButton.Visibility = Visibility.Collapsed;

        var checkButton = new Button { Content = "检查更新", HorizontalAlignment = HorizontalAlignment.Left };
        checkButton.Click += async (_, _) =>
        {
            checkButton.IsEnabled = false;
            checkButton.Content = "检查中…";
            try
            {
                Render(await _runtime.UpdateCheck.CheckAsync(force: true));
            }
            finally
            {
                checkButton.IsEnabled = true;
                checkButton.Content = "检查更新";
            }
        };

        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        links.Children.Add(checkButton);
        links.Children.Add(downloadButton);
        links.Children.Add(AboutLinkButton("项目主页", "https://github.com/cuihairu/beacon"));
        var galleryButton = new Button { Content = "组件中心", HorizontalAlignment = HorizontalAlignment.Left };
        galleryButton.Click += (_, _) =>
        {
            if (_gallery is null)
            {
                _gallery = new SampleGalleryWindow(_runtime, () => _gallery = null, ApplyGalleryComponent);
                _gallery.Activate();
            }
        };
        links.Children.Add(galleryButton);
        panel.Children.Add(links);

        panel.Children.Add(new TextBlock
        {
            Text = "检查更新走 GitHub Releases：仅正式发布参与比较（nightly 预发布不参与；每日构建日期版本恒视为已最新）。"
                + "启动约 20 秒后自动查一次并缓存，结果仅供提示、不自动安装；升级 = 下载覆盖安装后托盘 Exit 重启（单实例常驻）。",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            TextWrapping = TextWrapping.Wrap,
        });

        // 构建即渲染缓存（启动自检若已完成，打开设置直接看到结论）。
        void Render(UpdateCheckOutcome? outcome)
        {
            if (outcome is null)
            {
                remoteText.Text = "远程最新版本：—（未检查）";
                verdictText.Text = "检查结果：未检查（启动约 20 秒后自动查一次，也可手动）";
                checkedText.Text = "最后检查：—";
                downloadButton.Visibility = Visibility.Collapsed;
                return;
            }
            remoteText.Text = $"远程最新版本：{outcome.RemoteTag ?? "—"}";
            checkedText.Text = $"最后检查：{outcome.CheckedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)}";
            verdictText.Text = outcome switch
            {
                { NetworkFailure: true } => $"检查结果：无法检查更新（{outcome.CheckedAt:HH:mm}）",
                { Verdict: UpdateCheck.Verdict.UpToDate } => "检查结果：已是最新",
                { Verdict: UpdateCheck.Verdict.UpdateAvailable } => $"检查结果：有新版本 {outcome.RemoteTag}",
                _ => "检查结果：无法比较版本号",
            };
            downloadButton.Visibility = outcome.Verdict == UpdateCheck.Verdict.UpdateAvailable
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        Render(_runtime.UpdateCheck.Last);
        return panel;
    }

    /// <summary>关于页外链按钮：系统默认浏览器打开（与 open.url Action 同 UseShellExecute 口径）；
    /// 打不开落运行日志不弹窗（无上下文反馈面，日志足够定位）。</summary>
    private Button AboutLinkButton(string label, string url)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                _runtime.Logger?.LogWarning(exception, "关于页外链打开失败：{Url}", url);
            }
        };
        return button;
    }
    // —— SET-UX-01 单项保存（2026-11-23 用户令原型落地批1）：每个控件只写自己的字段——
    // 此前 SaveGeneral 全字段回写，预设外合法旧值（如 120 秒频率）在框里无档可选，改任意
    // 无关项（开机自启/热键/语言）都会把它洗回默认。落盘与广播面不变（SaveApp + SettingsApplied；
    // 主题构造期锁死免广播），无效值兜底沿用原口径（fail-safe 不炸设置页）。

    private void SaveHotkey()
    {
        _runtime.Config.App.Hotkey = _hotkeyBox.Text.Trim();
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke(); // 热键重注册走 ApplySettingsEffects
    }

    /// <summary>热键注册结果反馈（SET-UX-11，App 侧 ApplySettingsEffects 重挂后回调）：成功明示生效值；
    /// 失败明示原因 + 原热键仍生效 + 修复入口（上方框改完即再注册），并把框回写为实际生效值
    /// （config 已由 App 侧同步回写，框与存档一致）。窗口未开/页面未构建时静默（托盘气泡已覆盖）。</summary>
    internal void ReportHotkeyState(bool ok, string attempted, string effective)
    {
        if (_hotkeyFeedback is null)
        {
            return;
        }
        Feedback(_hotkeyFeedback, ok
            ? $"✓ 热键 {effective} 已生效（即时注册）。"
            : $"✗ 热键 {attempted} 注册失败（被其他程序占用或格式无效），原热键 {effective} 仍生效——请在上方框更换后重试。",
            error: !ok);
        if (!ok)
        {
            _hotkeyBox.Text = effective;
        }
    }

    private void SaveStartup()
    {
        _runtime.Config.App.LaunchOnStartup = _startupToggle.IsOn;
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke(); // 自启对齐走 ApplySettingsEffects → StartupService.SyncWith
    }

    private void SaveTheme()
    {
        _runtime.Config.App.Theme = TagOf(_themeBox) ?? "system";
        _runtime.Config.SaveApp();
        // 主题不广播（约束例外：构造期锁死，UI 标注「重启生效」；App 构造器读）
    }

    private void SaveLanguage()
    {
        _runtime.Config.App.Language = TagOf(_languageBox) ?? "";
        UiStrings.SetLanguage(_runtime.Config.App.Language); // 语言切换即时广播（同值幂等）；看板/托盘下次取词即新语言
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    private void SavePoll()
    {
        _runtime.Config.App.PollIntervalSeconds = int.TryParse(TagOf(_pollBox), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pollSeconds) && pollSeconds > 0 ? pollSeconds : 0;
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke(); // 频率变更重建调度走 ApplySettingsEffects（未变不动，避免全量重拉）
    }
    // —— 导入导出（B-802）：五份配置单文件（含自定义动作）；secrets 绝不入包，导入后按 credentialRef 提示重录 ——

    private UIElement BuildImportExportSection()
    {
        var export = new Button { Content = "导出配置…" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(export, "export-config-button"); // UIA 取证/搜索跳转锚（SET-UX-08）
        export.Click += async (_, _) => await ExportAsync();
        var import = new Button { Content = "导入配置…" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(import, "import-config-button"); // UIA 取证/搜索跳转锚（SET-UX-08）
        import.Click += async (_, _) => await ImportAsync();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { export, import } });
        panel.Children.Add(new TextBlock
        {
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
            Text = "导出包含 config/connections/widgets/pins/actions 五份配置（含自定义动作库）；密钥绝不入包（JSON 只存 credentialRef），导入后按提示在连接里重录。",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(_importFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
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
}
