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
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Storage.Pickers;

namespace Beacon.App.Windows;

internal sealed partial class SettingsWindow
{
    // —— 页面：外观 ——

    // BEACON-THEME-002: theme card selector state (tag -> card chrome / corner check), rebuilt
    // together with the page; the dictionary is capped at the three fixed tags (system/light/dark).
    private readonly Dictionary<string, Border> _themeCards = [];
    private readonly Dictionary<string, TextBlock> _themeChecks = [];

    private const double ThemeCardWidth = 150;
    private const double ThemeCardPreviewHeight = 56;

    private UIElement BuildAppearancePage()
    {
        var page = PageShell();
        page.Children.Add(SectionTitle("主题"));
        page.Children.Add(BuildThemeCardsSection());
        page.Children.Add(SectionTitle("配色方案"));
        page.Children.Add(BuildPaletteSection());
        page.Children.Add(Hint("主题与配色即时生效（不重启）；级别色自定义收在下方「更多选项」折叠区。"));
        page.Children.Add(SectionTitle("外观"));
        page.Children.Add(BuildAppearanceSection());
        return page;
    }

    // —— 外观：主题卡片选择器（BEACON-THEME-002，样式语言取自原型 02-style.png 四卡网格） ——

    /// <summary>Three cards (follow system / light / dark), each a mini window preview + label +
    /// brand border + corner check on selection. Tap to switch, live effect via the THEME-001 chain.
    /// The container keeps the legacy "theme-box" automation anchor (search jump / CI forensics).</summary>
    private UIElement BuildThemeCardsSection()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var (tag, label) in new[] { ("system", "跟随系统"), ("light", "浅色"), ("dark", "深色") })
        {
            row.Children.Add(BuildThemeCard(tag, label));
        }
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(row, "theme-box"); // UIA 取证/搜索跳转锚（SET-UX-08 场景 SG 沿用）
        ApplyThemeCardSelection(NormalizeTheme(_runtime.Config.App.Theme)); // 首挂/页重建即按 config 现值高亮（重建后字典指向新卡）
        return row;
    }

    private UIElement BuildThemeCard(string tag, string label)
    {
        var check = new TextBlock
        {
            Text = "\uE73E", // Segoe checkmark glyph (U+E73E), corner badge on the selected card
            FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(ThemeColors.Brand()),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(DesignTokens.Space6, 0, 0, 0),
        };
        // UIA selection readback: a Collapsed element leaves the tree entirely, so "check found
        // by id" == "card selected" - the CI proxy for the selected state (no pattern on Border).
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(check, $"theme-check-{tag}");
        var preview = tag == "system"
            ? PreviewSplit()
            : MiniPreview(dark: tag == "dark", outer: new CornerRadius(4));

        var grid = new Grid { RowSpacing = DesignTokens.Space6 };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ThemeCardPreviewHeight) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(preview, 0);
        grid.Children.Add(preview);
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = DesignTokens.FontBody,
            Foreground = new SolidColorBrush(ThemeColors.TextPrimary()),
            VerticalAlignment = VerticalAlignment.Center,
        });
        nameRow.Children.Add(check);
        Grid.SetRow(nameRow, 1);
        grid.Children.Add(nameRow);

        var card = new Border
        {
            Width = ThemeCardWidth,
            Padding = new Thickness(DesignTokens.Space8),
            CornerRadius = DesignTokens.RadiusCard,
            Background = new SolidColorBrush(ThemeColors.SurfaceCard()),
            Child = grid,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(ThemeColors.TextTertiary()),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(card, $"theme-card-{tag}"); // UIA 取证：卡片点选驱动
        ToolTipService.SetToolTip(card, label);
        var captured = tag;
        card.Tapped += (_, _) => SaveThemeFromCard(captured);
        _themeCards[tag] = card;
        _themeChecks[tag] = check;
        return card;
    }

    /// <summary>Mini window mock: title bar strip + content surface + two text lines, in the fixed
    /// palette of the mode it represents (never the live theme — a light card must show light even
    /// while the app is dark, that is the point of the preview).</summary>
    private static Grid MiniPreview(bool dark, CornerRadius outer)
    {
        var background = dark ? SeverityPalette.Rgb(255, 31, 31, 31) : SeverityPalette.Rgb(255, 255, 255, 255);
        var bar = dark ? SeverityPalette.Rgb(255, 45, 45, 45) : SeverityPalette.Rgb(255, 241, 245, 249);
        var line = dark ? SeverityPalette.Rgb(255, 74, 74, 74) : SeverityPalette.Rgb(255, 203, 213, 225);

        var grid = new Grid { CornerRadius = outer };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var barRect = new Border { Background = new SolidColorBrush(bar), Height = 12 };
        Grid.SetRow(barRect, 0);
        grid.Children.Add(barRect);
        var body = new Border
        {
            Background = new SolidColorBrush(background),
            Child = new StackPanel
            {
                Margin = new Thickness(10, 8, 10, 0),
                Spacing = 5,
                Children =
                {
                    new Border { Height = 4, CornerRadius = new CornerRadius(2), Width = 96, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(line) },
                    new Border { Height = 4, CornerRadius = new CornerRadius(2), Width = 64, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(line) },
                },
            },
        };
        Grid.SetRow(body, 1);
        grid.Children.Add(body);
        return grid;
    }

    /// <summary>Follow-system card: light mock on the left half, dark mock on the right half.</summary>
    private static Grid PreviewSplit()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = MiniPreview(dark: false, outer: new CornerRadius(4, 0, 0, 4));
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);
        var right = MiniPreview(dark: true, outer: new CornerRadius(0, 4, 4, 0));
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>Refresh card chrome for the given tag (thick brand border + visible check on the
    /// selected card). Explicit call keeps cards in sync even when the theme flip is silent
    /// (system→light while the system already resolves light — ThemeRuntime.Apply stays quiet).</summary>
    private void ApplyThemeCardSelection(string tag)
    {
        foreach (var (key, card) in _themeCards)
        {
            var selected = key == tag;
            card.BorderThickness = new Thickness(selected ? 2 : 1);
            card.BorderBrush = new SolidColorBrush(selected ? ThemeColors.Brand() : ThemeColors.TextTertiary());
            if (_themeChecks.TryGetValue(key, out var check))
            {
                check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    /// <summary>BEACON-THEME-002: card-driven theme save — same two-stage live broadcast as the
    /// former combo path: ThemeRuntime.Apply flips the live IsDark source (Changed repaints static
    /// color surfaces), SettingsApplied re-applies root themes across the three live windows.</summary>
    private void SaveThemeFromCard(string theme)
    {
        _runtime.Config.App.Theme = NormalizeTheme(theme);
        ThemeRuntime.Apply(_runtime.Config.App.Theme, Application.Current.RequestedTheme == ApplicationTheme.Dark);
        _runtime.Config.SaveApp();
        ApplyThemeCardSelection(_runtime.Config.App.Theme);
        SettingsApplied?.Invoke();
    }

    /// <summary>Unknown/missing values fall back to follow-system (App constructor resolution).</summary>
    private static string NormalizeTheme(string? theme)
    {
        var value = (theme ?? "system").Trim().ToLowerInvariant();
        return value is "light" or "dark" ? value : "system";
    }

    // —— 外观：配色方案色卡组（BEACON-THEME-002，原型 02-style.png 可视化色卡语言） ——

    /// <summary>Six key UI colors as round swatches with hover names. Display-only: level colors
    /// stay customizable in the folded area below; this row is the at-a-glance palette of the
    /// current theme (re-resolved on page rebuild after a live theme flip).</summary>
    private UIElement BuildPaletteSection()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var (name, color) in new[]
        {
            ("主色", ThemeColors.Brand()),
            ("强调", ThemeColors.AccentFill()),
            ("背景", ThemeColors.Surface()),
            ("文字", ThemeColors.TextPrimary()),
            ("成功", ThemeColors.StatusOk()),
            ("警告", ThemeColors.StatusWarn()),
        })
        {
            var swatch = new Ellipse { Width = 28, Height = 28, Fill = new SolidColorBrush(color) };
            ToolTipService.SetToolTip(swatch, name); // hover shows the name (原型悬停显名)
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(swatch, name); // UIA 取证可读
            row.Children.Add(swatch);
        }
        return row;
    }

    // —— 外观：级别色编辑 + 动效档位与逐族预览（B-805，RFC §4.1/§6.2.8） ——

    private UIElement BuildAppearanceSection()
    {
        // SET-UX-06 外观页口径：主面保留动效档位/额度显示方式/密度/逐族预览；颜色（级别色）与
        // 强度（动效强度）收进「更多选项」折叠区——高频档不占主视线，锚点全部保留（UIA 取证口
        // 不迁移，场景 G4/G5/G6 先展开 appearance-more-options 再找锚）。
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

        var moreOptions = new Expander
        {
            Header = "更多选项（级别色与动效强度）",
            Content = new StackPanel { Spacing = 8, Children = { palette, reset, _motionIntensitySlider } },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(moreOptions, "appearance-more-options"); // UIA 取证：折叠区展开驱动（G4/G5/G6 前置）

        // 2026-10-10 用户令「设置里让用户自己选表现方式，比如一开始的进度条」：额度显示方式全局可选。
        // 2026-10-11 用户令重设计后 bar 为默认（4 DIP 圆角条 + NN% 读数），标签按用户易读序排列：
        // 进度条（默认）在前，圆池注水（旧特效）在后；第三档 aquarium（2026-11-15 用户令「整个一行都
        // 是进度条…做成水族箱」立档，2026-11-17 用户令横向改版「整个就是注水桶」——桶身=整个 tile，
        // 水从左往右灌满，标签随之改注水桶）。保存走既有 SaveProgressStyle（SettingsApplied 已广播，即时重建）。
        _progressStyleBox = new ComboBox { Header = "额度显示方式", Width = 260 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_progressStyleBox, "appearance-progress-style-box"); // UIA 取证：显示方式锚点（对齐 appearance-density-box 命名法）
        foreach (var (value, label) in new[]
        {
            ("bar", "进度条（默认：底部条 + 百分比读数）"),
            ("pool", "圆池注水（旧特效：水位=进度，满格变红）"),
            ("aquarium", "注水桶（整桶横灌：水面从左往右推进=用量）"),
        })
        {
            _progressStyleBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }

        // Phase 5 行 5：界面密度（regular 恒等 / compact 间距 ×0.75，Core UiDensity 单源 fail-open）。
        // 与 progressStyle 同「构造期实时读」口径：保存即广播 SettingsApplied 重建两宿主（SET-INST-001 已补）。
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
        form.Children.Add(_motionModeBox);
        form.Children.Add(moreOptions); // SET-UX-06：颜色与强度收进折叠区（主面只留档位）
        form.Children.Add(_progressStyleBox);
        // 2026-10-22 评审 G10 口径声明：此档是「传统渲染」的全局额度表现（无展示样式面的组件）；
        // 组件行「展示样式」另选的面不受影响，想回传统渲染 = 组件中心「恢复传统渲染」。
        form.Children.Add(new TextBlock
        {
            Text = "此档管传统渲染的全局额度表现（未选展示样式的组件），切换即时生效；组件行「展示样式」所选面不受影响，回传统渲染在组件中心点「恢复传统渲染」。",
            FontSize = DesignTokens.FontCaption,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
            Width = 260,
        });
        form.Children.Add(_densityBox);
        // SET-UX-11：密度档生效时机注明（SaveDensity 广播重建两宿主，桌面 tile 即刻重排；已开详情窗构造期读，下次打开生效）
        form.Children.Add(Hint("界面密度改档即时重排桌面组件；已打开的详情窗下次打开时生效。"));
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
            Foreground = new SolidColorBrush(ThemeColors.TextSecondary()),
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
        // 密度是构造期读 DesignTokens.Spacing 的档位——不重建 L0 就要等重启才看到变化，
        // 与 progressStyle/动效档同口径：保存即触发 SettingsApplied 重建两宿主。
        SettingsApplied?.Invoke();
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
