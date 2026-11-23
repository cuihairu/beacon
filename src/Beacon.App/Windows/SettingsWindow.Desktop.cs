using System.Globalization;
using Beacon.App.Services; // DesignTokens（字号/圆角令牌，Beacon.App.Services 命名空间）
using Beacon.Core.Services; // StatusBarMode / UiStrings（SET-UX-02 单源）
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Windows;

/// <summary>
/// 「桌面显示」页（SET-UX-02/03，2026-11-23 用户令原型落地批1，原型 docs/design/settings-redesign/01-desktop.png）：
/// <list type="bullet">
/// <item>桌面状态条三选一（关闭/右下角胶囊/顶部悬浮岛，各配位置示意）：StatusBarMode.Core 单源投影
/// 两个旧字段（岛优先、胶囊其次）成组保存——此前常规页与组件中心两处开关分管两字段，
/// 关岛可能露出右下角胶囊；本页是唯一入口，旧配置四组合零迁移可读。</item>
/// <item>窗口与看板区：桌面组件布局（PinDisplayMode，重启生效）/ 在桌面显示数值和额度
/// （NumericFloatingEnabled——它同时闸集中面板与独立窗的数值类组件，原「悬浮框」总开关正名）/
/// 允许打开看板（BoardEnabled，提示当前热键与入口）/ 窗口不透明度（UiOpacity，百分比读数）/
/// 面板贴边时收起（EdgeCollapseEnabled，注明只作用于集中面板）。</item>
/// </list>
/// 全部写点 SaveApp + SettingsApplied 广播（设置即生效，2026-11-15 用户令面）；布局下拉按既有
/// PinDisplayMode 语义标「重启后生效」不广播即时面（ApplySettingsEffects 不重建宿主形态）。
/// </summary>
internal sealed partial class SettingsWindow
{
    private UIElement BuildDesktopPage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("桌面显示"));
        page.Children.Add(Hint("选择显示窗口，再决定每个组件放在哪里。组件显隐与上岛在「组件中心」。"));

        // —— 桌面状态条：三选一 + 位置示意（读投影岛优先/胶囊其次，选档成组保存）——
        page.Children.Add(SectionTitle("桌面状态条"));
        var mode = StatusBarMode.Read(_runtime.Config.App);
        _statusBarOff = BuildStatusBarChoice("status-bar-off", "关闭", "只保留托盘入口", StatusBarMonitorDemo(StatusBarMode.Off));
        _statusBarCapsule = BuildStatusBarChoice("status-bar-capsule", "右下角胶囊", "显示整体状态", StatusBarMonitorDemo(StatusBarMode.Capsule));
        _statusBarIsland = BuildStatusBarChoice("status-bar-island", "顶部悬浮岛", "可选显示的组件", StatusBarMonitorDemo(StatusBarMode.Island));
        // 先取投影态再挂事件（构造期赋值不触发写路径；用户点选才落库——SET-UX-01 页面构造零写入）
        if (mode == StatusBarMode.Capsule)
        {
            _statusBarCapsule.IsChecked = true;
        }
        else if (mode == StatusBarMode.Island)
        {
            _statusBarIsland.IsChecked = true;
        }
        else
        {
            _statusBarOff.IsChecked = true;
        }
        _statusBarOff.Checked += (_, _) => SaveStatusBarMode(StatusBarMode.Off);
        _statusBarCapsule.Checked += (_, _) => SaveStatusBarMode(StatusBarMode.Capsule);
        _statusBarIsland.Checked += (_, _) => SaveStatusBarMode(StatusBarMode.Island);
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        choices.Children.Add(_statusBarOff);
        choices.Children.Add(_statusBarCapsule);
        choices.Children.Add(_statusBarIsland);
        page.Children.Add(choices);

        // —— 窗口与看板 ——
        page.Children.Add(SectionTitle("窗口与看板"));

        // ① 桌面组件布局（PinDisplayMode 原字段：panel=集中在一个窗口 / floating=支持的组件独立显示）。
        // 悬浮形态档自组件中心收编至此正名：它是桌面布局档，不是载体开关；重启生效（ApplySettingsEffects 不重建宿主形态）。
        _desktopLayoutBox = new ComboBox { Header = "桌面组件布局（重启后生效）", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_desktopLayoutBox, "desktop-layout-box"); // UIA 取证：布局档驱动
        foreach (var item in new[] { ("panel", "集中在一个窗口"), ("floating", "支持的组件独立显示") })
        {
            var layoutItem = new ComboBoxItem { Content = item.Item2, Tag = item.Item1 };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(layoutItem, $"layout-{item.Item1}");
            _desktopLayoutBox.Items.Add(layoutItem);
        }
        _desktopLayoutBox.SelectedIndex = IndexOfTag(_desktopLayoutBox, _runtime.Config.App.PinDisplayMode);
        _desktopLayoutBox.SelectionChanged += (_, _) => SavePinDisplayMode();
        page.Children.Add(_desktopLayoutBox);
        page.Children.Add(Hint("切换到独立显示后，其余组件仍可留在集中面板。"));

        // ② 在桌面显示数值和额度（NumericFloatingEnabled 原字段正名）：同时闸集中面板与独立窗的数值类组件
        //     （WidgetDisplayPolicy.IsNumericSuppressed 两宿主同口径）。
        _numericFloatingToggle = new ToggleSwitch { Header = "在桌面显示数值和额度", IsOn = _runtime.Config.App.NumericFloatingEnabled };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_numericFloatingToggle, "numeric-floating-toggle"); // UIA 取证：数值总开关驱动
        _numericFloatingToggle.Toggled += (_, _) => SaveNumericFloating();
        page.Children.Add(_numericFloatingToggle);
        page.Children.Add(Hint("同时应用于集中面板和独立窗口。"));

        // ③ 允许打开看板（BoardEnabled 原字段收编至此）：关=热键/托盘/岛点击不可唤起且已开即收。
        // 提示当前热键与可用入口（热键改动后本页重建时刷新——SelectModule 每次重建右栏）。
        _boardToggle = new ToggleSwitch { Header = "允许打开看板", IsOn = _runtime.Config.App.BoardEnabled };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_boardToggle, "board-toggle"); // UIA 取证：看板开关驱动
        _boardToggle.Toggled += (_, _) => SaveBoardEnabled();
        page.Children.Add(_boardToggle);
        var boardHotkey = string.IsNullOrWhiteSpace(_runtime.Config.App.Hotkey) ? "Ctrl+Alt+B" : _runtime.Config.App.Hotkey;
        page.Children.Add(Hint($"可用 {boardHotkey}、托盘菜单或悬浮岛打开。"));

        // ④ 窗口不透明度（UiOpacity 原字段移入本页）：百分比读数实时随滑杆，改动即时生效。
        _desktopOpacityValue = new TextBlock
        {
            Text = $"{(int)Math.Round(Math.Clamp(_runtime.Config.App.UiOpacity, 0.2, 1.0) * 100)}%",
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(ThemeColors.Label()),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _desktopOpacitySlider = new Slider
        {
            Header = "窗口不透明度",
            Minimum = 0.2, // 与运行时 clamp(0.2,1.0) 同口径（UI/UX 审计：上下限不一致滑杆一碰就跳）
            Maximum = 1.0,
            StepFrequency = 0.05,
            Value = Math.Clamp(_runtime.Config.App.UiOpacity, 0.2, 1.0),
            Width = 240,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_desktopOpacitySlider, "desktop-opacity-slider"); // UIA 取证：不透明度驱动
        _desktopOpacitySlider.ValueChanged += (_, _) => SaveDesktopOpacity();
        var opacityRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        opacityRow.Children.Add(_desktopOpacitySlider);
        opacityRow.Children.Add(_desktopOpacityValue);
        page.Children.Add(opacityRow);

        // ⑤ 面板贴边时收起（EdgeCollapseEnabled 原字段移入本页；AutomationId 沿用 W4 场景锚点）。
        // 注明只作用于集中面板（独立悬浮窗无贴边收起语义）。
        _edgeCollapseToggle = new ToggleSwitch { Header = UiStrings.SettingsEdgeCollapse, IsOn = _runtime.Config.App.EdgeCollapseEnabled };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_edgeCollapseToggle, "edge-collapse-toggle"); // UIA 取证：开关驱动（场景 W4 锚点不变）
        _edgeCollapseToggle.Toggled += (_, _) => SaveEdgeCollapse();
        page.Children.Add(_edgeCollapseToggle);
        page.Children.Add(Hint("把桌面集中面板拖到屏幕边缘时收成细条（只作用于集中面板；独立悬浮窗不收起）。"));

        return page;
    }

    /// <summary>状态条选项卡：单选 + 位置示意（显示器小图）+ 名称 + 副题（原型 choice 卡等价物）。</summary>
    private Microsoft.UI.Xaml.Controls.RadioButton BuildStatusBarChoice(string automationId, string title, string subtitle, UIElement demo)
    {
        var titleLabel = new TextBlock
        {
            Text = title,
            FontSize = DesignTokens.FontBody,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ThemeColors.Label()),
        };
        var subtitleLabel = new TextBlock
        {
            Text = subtitle,
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            TextWrapping = TextWrapping.Wrap,
        };
        var textColumn = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        textColumn.Children.Add(titleLabel);
        textColumn.Children.Add(subtitleLabel);

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        content.Children.Add(demo);
        content.Children.Add(textColumn);

        var radio = new Microsoft.UI.Xaml.Controls.RadioButton
        {
            Content = content,
            Padding = new Thickness(6, 4, 6, 4),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(radio, automationId); // UIA 取证：状态条三档驱动
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(radio, title); // 读屏可辨（原型 aria-label 面）
        return radio;
    }

    /// <summary>位置示意：显示器小框（64×44），off=空框、capsule=右下角胶囊点、island=顶部居中岛块
    /// （原型 .monitor/.capsule/.island 等价物——用户不看文字也能认出位置）。</summary>
    private UIElement StatusBarMonitorDemo(string mode)
    {
        var monitor = new Grid { Width = 64, Height = 44 };
        var frame = new Border
        {
            Background = new SolidColorBrush(SeverityPalette.Rgb(255, 24, 28, 36)),
            BorderBrush = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            BorderThickness = new Thickness(1),
            CornerRadius = DesignTokens.RadiusControl,
        };
        monitor.Children.Add(frame);
        if (mode == StatusBarMode.Capsule)
        {
            // 右下角：小胶囊点（右下 margin 贴角）
            monitor.Children.Add(new Border
            {
                Width = 22,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(SeverityPalette.Rgb(255, 96, 165, 250)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 4, 4),
            });
        }
        else if (mode == StatusBarMode.Island)
        {
            // 顶部居中：岛块（Dynamic Island 位）
            monitor.Children.Add(new Border
            {
                Width = 26,
                Height = 9,
                CornerRadius = new CornerRadius(4.5),
                Background = new SolidColorBrush(SeverityPalette.Rgb(255, 5, 8, 12)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        return monitor;
    }

    // —— 桌面显示页落库（全部 SaveApp + SettingsApplied 广播；布局下拉除外——重启生效面） ——

    /// <summary>状态条选档（SET-UX-02）：StatusBarMode 成组保存两旧字段 → 广播（EnsureCapsule/Close、
    /// SetIslandMode 形态切换走既有 ApplySettingsEffects，即时生效）。</summary>
    private void SaveStatusBarMode(string mode)
    {
        StatusBarMode.Apply(_runtime.Config.App, mode);
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    /// <summary>桌面组件布局（SET-UX-03①）：PinDisplayMode 原字段，重启生效（UI 已标注；不广播即时面）。
    /// 命名避让 ComponentStyles.cs 既有 SaveDesktopLayout（组件实例布局保存器，partial 同类）。</summary>
    private void SavePinDisplayMode()
    {
        _runtime.Config.App.PinDisplayMode = TagOf(_desktopLayoutBox) ?? "panel";
        _runtime.Config.SaveApp();
    }

    /// <summary>数值总开关（SET-UX-03①）：NumericFloatingEnabled 原字段 → 广播（两宿主 ReloadTiles 即时面）。</summary>
    private void SaveNumericFloating()
    {
        _runtime.Config.App.NumericFloatingEnabled = _numericFloatingToggle.IsOn;
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    /// <summary>允许打开看板（SET-UX-03②）：BoardEnabled 原字段 → 广播（已开即收走 ApplySettingsEffects → quickPanel.Hide）。</summary>
    private void SaveBoardEnabled()
    {
        _runtime.Config.App.BoardEnabled = _boardToggle.IsOn;
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    /// <summary>窗口不透明度（SET-UX-03③）：UiOpacity 原字段 → 广播（四类悬浮面 ApplyOpacity 即时面）；
    /// 百分比读数同帧刷新。</summary>
    private void SaveDesktopOpacity()
    {
        _runtime.Config.App.UiOpacity = _desktopOpacitySlider.Value;
        _desktopOpacityValue.Text = $"{(int)Math.Round(_desktopOpacitySlider.Value * 100)}%";
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    /// <summary>贴边收起（SET-UX-03③）：EdgeCollapseEnabled 原字段 → 广播（已收起的 ApplyEdgeCollapseSetting 立刻弹回）。</summary>
    private void SaveEdgeCollapse()
    {
        _runtime.Config.App.EdgeCollapseEnabled = _edgeCollapseToggle.IsOn;
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }
}
