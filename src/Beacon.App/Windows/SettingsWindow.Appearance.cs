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
    // —— 页面：外观 ——

    private UIElement BuildAppearancePage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("外观"));
        page.Children.Add(BuildAppearanceSection());
        return page;
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
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(box, $"appearance-color-{key}"); // UIA 取证/自动化入口（同 module-* 口径）
            _colorBoxes[key] = box;
            palette.Children.Add(box);
        }
        var reset = new Button { Content = "重置默认" };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(reset, "appearance-reset-button"); // UIA 取证：重置默认驱动
        reset.Click += (_, _) => ResetSeverityColors();

        _motionModeBox = new ComboBox { Header = "动效档位", Width = 170 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_motionModeBox, "appearance-motion-mode-box"); // UIA 取证：动效档位驱动
        foreach (var (value, label) in new[] { ("full", "完全"), ("reduced", "适度（默认）"), ("off", "关闭") })
        {
            var item = new ComboBoxItem { Content = label, Tag = value };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, $"motion-{value}"); // 同 poll-{n} 口径
            _motionModeBox.Items.Add(item);
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
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_motionIntensitySlider, "appearance-motion-intensity"); // UIA 取证：动效强度驱动
        _motionIntensitySlider.ValueChanged += (_, _) => SaveMotion();

        // 2026-10-10 用户令「设置里让用户自己选表现方式，比如一开始的进度条」：额度显示方式全局可选。
        // 2026-10-11 用户令重设计后 bar 为默认（4 DIP 圆角条 + NN% 读数），标签按用户易读序排列：
        // 进度条（默认）在前，圆池注水（旧特效）在后
        _progressStyleBox = new ComboBox { Header = "额度显示方式", Width = 260 };
        foreach (var (value, label) in new[]
        {
            ("bar", "进度条（默认：底部条 + 百分比读数）"),
            ("pool", "圆池注水（旧特效：水位=进度，满格变红）"),
        })
        {
            _progressStyleBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }

        // Phase 5 行 5：界面密度（regular 恒等 / compact 间距 ×0.75，Core UiDensity 单源 fail-open）。
        // 与 progressStyle 同「构造期实时读」口径：改档后下次开窗生效，无需重渲染。
        _densityBox = new ComboBox { Header = "界面密度", Width = 260 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_densityBox, "appearance-density-box"); // UIA 取证：密度档锚点（场景 AA 驱动）
        foreach (var (value, label) in new[]
        {
            ("regular", "常规（默认）"),
            ("compact", "紧凑（组件间距 ×0.75）"),
            ("spacious", "宽松（组件间距 ×1.25）"),
        })
        {
            _densityBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        _densityBox.SelectedIndex = Math.Max(0, IndexOfTag(_densityBox, _runtime.Config.App.Appearance.Density));
        _densityBox.SelectionChanged += (_, _) => SaveDensity();
        _progressStyleBox.SelectedIndex = Math.Max(0, IndexOfTag(_progressStyleBox, _runtime.Config.App.Appearance.ProgressStyle));
        _progressStyleBox.SelectionChanged += (_, _) => SaveProgressStyle();

        _previewLight = new Ellipse { Width = 24, Height = 24, Fill = new SolidColorBrush(SeverityPalette.Rgb(255, 63, 185, 80)) };
        _previewHost = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = DesignTokens.RadiusCard,
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
        form.Children.Add(_progressStyleBox);
        // 2026-10-22 评审 G10 口径声明：此档是「传统渲染」的全局额度表现（无展示样式面的组件）；
        // 组件行「展示样式」另选的面不受影响，想回传统渲染 = 组件中心「恢复传统渲染」。
        form.Children.Add(new TextBlock
        {
            Text = "此档管传统渲染的全局额度表现（未选展示样式的组件）；组件行「展示样式」所选面不受影响，回传统渲染在组件中心点「恢复传统渲染」。",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
            Width = 260,
        });
        form.Children.Add(_densityBox);
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
        form.Children.Add(_appearanceFeedback = new TextBlock { FontSize = DesignTokens.FontBody, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        form.Children.Add(new TextBlock
        {
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Text = "改动即时预览并写入 config.json appearance；off 档预览即全静止，静止按钮复位循环动画。",
            TextWrapping = TextWrapping.Wrap,
        });
        return new Border { Padding = new Thickness(10), CornerRadius = DesignTokens.RadiusCard, Background = new SolidColorBrush(SeverityPalette.Rgb(30, 31, 38, 40)), Child = form };
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
        _runtime.Config.SaveApp();
        // 状态去重（StateStore suppressUnchanged，已代决②）后「靠下一刷新 tick 重渲染」不再可靠：
        // mock/恒定值永不重发布 → 既有 tile 永不 Update → 呼吸起不来（场景 G7c 38960663429 实证 0 差分）。
        // 触发 SettingsApplied 重建 L0（同改色口径），新 PinTile 构造时按新档挂/摘动画。
        SettingsApplied?.Invoke();
    }

    /// <summary>额度显示方式（2026-10-10 用户令「设置里让用户自己选表现方式」；2026-10-11 用户令重设计后
    /// bar=4 DIP 圆角进度条 + NN% 读数为默认，pool=圆池注水为可选旧特效）。
    /// PinTile 构造时读 MotionEngine.ProgressStyle——状态去重后既有 tile 无重渲染时机，
    /// 保存即触发 SettingsApplied 重建 L0 让新样式即刻生效（同动效档口径）。</summary>
    private void SaveProgressStyle()
    {
        _runtime.Config.App.Appearance.ProgressStyle = TagOf(_progressStyleBox) ?? "bar";
        _runtime.Config.SaveApp();
        SettingsApplied?.Invoke();
    }

    /// <summary>界面密度（Phase 5 行 5）：regular 恒等 / compact ×0.75。组件化窗面（L3 Detail 等）构造时
    /// 读 Appearance.Density 经 DesignTokens.Spacing 缩放——同 progressStyle 的构造期实时读口径。</summary>
    private void SaveDensity()
    {
        _runtime.Config.App.Appearance.Density = TagOf(_densityBox) ?? "regular";
        _runtime.Config.SaveApp();
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
}
