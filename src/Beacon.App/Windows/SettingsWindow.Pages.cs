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
        _hotkeyBox.LostFocus += (_, _) => SaveGeneral();
        _startupToggle = new ToggleSwitch { Header = "开机自启", IsOn = _runtime.Config.App.LaunchOnStartup };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_startupToggle, "startup-toggle"); // UIA 取证：开机自启驱动
        _startupToggle.Toggled += (_, _) => SaveGeneral();
        _themeBox = new ComboBox { Header = "主题（重启生效）", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var item in new[] { ("system", "跟随系统"), ("light", "浅色"), ("dark", "深色") })
        {
            _themeBox.Items.Add(new ComboBoxItem { Content = item.Item2, Tag = item.Item1 });
        }
        _themeBox.SelectedIndex = IndexOfTag(_themeBox, _runtime.Config.App.Theme);
        _themeBox.SelectionChanged += (_, _) => SaveGeneral();
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
        _languageBox.SelectionChanged += (_, _) => SaveGeneral();
        _opacitySlider = new Slider
        {
            Header = "界面透明度",
            Minimum = 0.2, // UI/UX 审计：滑杆下限 0.4 与运行时 clamp(0.2,1.0) 不一致——配了 0.2 的用户滑杆一碰就跳回 0.4
            Maximum = 1.0,
            StepFrequency = 0.05,
            Value = Math.Clamp(_runtime.Config.App.UiOpacity, 0.2, 1.0),
            Width = 240,
        };
        _opacitySlider.ValueChanged += (_, _) => SaveGeneral();
        _capsuleToggle = new ToggleSwitch { Header = "显示状态胶囊（L1）", IsOn = _runtime.Config.App.ShowCapsule };
        _capsuleToggle.Toggled += (_, _) => SaveGeneral();
        // 展示载体三项（悬浮岛/悬浮框/看板）已收编到 组件中心 → 展示载体（ISLAND-RING-1 扩围②，
        // 2026-11-07 用户令「一个页面决定显示什么+用什么显示」）：本页只留经典胶囊总开关，岛/悬浮框
        // 勾选在载体区（旧 config 字段继续用，行为与持久化向后兼容）。
        // 贴边收起开关（2026-10-14 用户单③，默认开=保持已发布行为）：关=拖到屏幕边缘不再收成细条，
        // 已收起的经 ApplySettingsEffects → ApplyEdgeCollapseSetting 立刻弹回
        _edgeCollapseToggle = new ToggleSwitch { Header = UiStrings.SettingsEdgeCollapse, IsOn = _runtime.Config.App.EdgeCollapseEnabled };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_edgeCollapseToggle, "edge-collapse-toggle"); // UIA 取证：开关驱动
        _edgeCollapseToggle.Toggled += (_, _) => SaveGeneral();
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
        page.Children.Add(new StackPanel { Spacing = 10, Children = { _hotkeyBox, _startupToggle, _themeBox, _languageBox, _opacitySlider, _capsuleToggle, _edgeCollapseToggle, _pollBox } });
        page.Children.Add(Hint("悬浮岛/悬浮框/看板等展示载体已收编到「组件中心 → 展示载体」（勾选=出现、取消=消失，即时生效）；信息密集组件的悬浮形态（panel|floating）同页可调。"));
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

        // —— 展示载体（收编四处散落开关：岛形态开关/数量悬浮窗开关/悬浮形态下拉/托盘零散入口）——
        page.Children.Add(SectionTitle("展示载体"));
        page.Children.Add(Hint("用什么显示：悬浮岛 / 悬浮框 / 看板，勾选=出现、取消=消失（即时生效、重启保留）。共存规则（真实窗口架构）：三者是不同窗口，任意组合共存；唯一互斥面=悬浮岛与经典胶囊是同一胶囊窗的两种形态（岛勾选时经典让位，岛取消按「通用→显示状态胶囊」回经典或整体关闭）。看板是唤起式面板：关闭=热键/岛点击不可唤起且已开即收。"));
        foreach (var carrier in CarrierCatalog.Carriers)
        {
            page.Children.Add(BuildCarrierRow(carrier));
        }

        // 信息密集组件（趋势图/灯组）的悬浮形态档收编至此（原「通用 → 悬浮形态」下拉，行为与持久化不变）
        _pinModeBox = new ComboBox { Header = "悬浮形态（信息密集组件：重启生效）", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var item in new[] { ("panel", "宿主面板（单窗多 tile）"), ("floating", "独立悬浮框（每组件一窗，任意拖放）") })
        {
            _pinModeBox.Items.Add(new ComboBoxItem { Content = item.Item2, Tag = item.Item1 });
        }
        _pinModeBox.SelectedIndex = IndexOfTag(_pinModeBox, _runtime.Config.App.PinDisplayMode);
        _pinModeBox.SelectionChanged += (_, _) => SaveCarrierPinMode();
        page.Children.Add(_pinModeBox);

        var groups = ComponentCenterCatalog.Group(_runtime.Config.Widgets);
        if (groups.Count == 0)
        {
            page.Children.Add(Hint("尚无组件——先在左侧模块页创建连接与组件，再回这里管理显隐。"));
            return page;
        }

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

    /// <summary>载体行：勾选框 + 载体名/真实能力说明。勾选变化即时落库（CarrierCatalog 单源写旧
    /// 字段/boardEnabled）并触发 SettingsApplied（建窗/关窗/岛↔经典切换走既有 ApplySettingsEffects）。</summary>
    private UIElement BuildCarrierRow(CarrierItem carrier)
    {
        var enabled = carrier.Id switch
        {
            CarrierCatalog.CarrierIslandId => CarrierCatalog.IslandEnabled(_runtime.Config.App),
            CarrierCatalog.CarrierFloatingId => CarrierCatalog.FloatingEnabled(_runtime.Config.App),
            _ => CarrierCatalog.BoardEnabled(_runtime.Config.App),
        };
        var toggle = new CheckBox
        {
            IsChecked = enabled,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(toggle, $"carrier-{carrier.Id}"); // UIA 取证：载体勾选驱动

        var label = new TextBlock
        {
            Text = carrier.Label,
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(ThemeColors.Label()),
        };
        var subtitle = new TextBlock
        {
            Text = carrier.Hint,
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            TextWrapping = TextWrapping.Wrap,
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
        toggle.Checked += (_, _) => SaveCarrier(carrier.Id, visible: true);
        toggle.Unchecked += (_, _) => SaveCarrier(carrier.Id, visible: false);
        return row;
    }

    /// <summary>载体勾选落库（设置页唯一写入口）：CarrierCatalog 单源改旧字段/boardEnabled → SaveApp
    /// → SettingsApplied（岛=EnsureCapsule/Close+形态切换、悬浮框=桌面零残留、看板=已开即收）。</summary>
    private void SaveCarrier(string carrierId, bool visible)
    {
        CarrierCatalog.SetEnabled(_runtime.Config.App, carrierId, visible);
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    /// <summary>悬浮形态档落库（收编自通用页下拉）：PinDisplayMode 旧字段继续用，行为不变。</summary>
    private void SaveCarrierPinMode()
    {
        _runtime.Config.App.PinDisplayMode = TagOf(_pinModeBox) ?? "panel";
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    /// <summary>组件中心单行：勾选框 + 显示名/副题（类型键 · 钉选态）。勾选变化即时落库并联动 L0。</summary>
    private UIElement BuildComponentCenterRow(ComponentCenterItem item)
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
            Foreground = new SolidColorBrush(ThemeColors.Label()),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var subtitle = new TextBlock
        {
            Text = $"{item.TypeName} · {(item.Pinned ? "已钉选" : "未钉选")}{(hidden ? " · 已隐藏" : "")}",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
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
    /// 全部真实组件的「上岛」勾选（AppConfig.IslandComponents 白名单，缺席=不上岛，列表序=上岛序）。</summary>
    private UIElement BuildIslandSection(ComponentCenterGroup group)
    {
        var panel = new StackPanel { Spacing = 4 };

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
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
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
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
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
    private void SaveGeneral()
    {
        var app = _runtime.Config.App;
        app.Hotkey = _hotkeyBox.Text.Trim();
        app.LaunchOnStartup = _startupToggle.IsOn;
        app.Theme = TagOf(_themeBox) ?? "system";
        app.PinDisplayMode = _runtime.Config.App.PinDisplayMode; // 悬浮形态档已收编到载体区（SaveCarrierPinMode 专写），此处保位不覆盖
        app.UiOpacity = _opacitySlider.Value;
        app.ShowCapsule = _capsuleToggle.IsOn;
        app.EdgeCollapseEnabled = _edgeCollapseToggle.IsOn;
        app.PollIntervalSeconds = int.TryParse(TagOf(_pollBox), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pollSeconds) && pollSeconds > 0 ? pollSeconds : 0;
        app.Language = TagOf(_languageBox) ?? "";
        UiStrings.SetLanguage(app.Language); // 语言切换即时广播（同值幂等）；看板/托盘下次取词即新语言
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
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
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
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
