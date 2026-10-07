using System.Threading;
using Beacon.App.Services;
using Beacon.Connections;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Windows;

/// <summary>
/// Settings 窗口（B-801，RFC §38/§42）：常规（热键/自启/主题/透明度/胶囊开关）、
/// 连接 CRUD（测试连接；GitHub token 录入只进 DPAPI，严禁明文 JSON）、
/// 组件 CRUD（按 WidgetTypeDescriptor 动态字段，钉桌面开关）、通知规则展示。
/// 常规项改动即存 config.json 并触发 SettingsApplied（App 侧重注册热键/自启/胶囊显隐）。
/// 调色与动效页属 B-805。persist 即生效于下一刷新周期（WidgetHost 每轮读 config）。
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly BeaconRuntime _runtime;
    private readonly MotionEngine _motion;

    private TextBox _hotkeyBox = null!;
    private ToggleSwitch _startupToggle = null!;
    private ComboBox _themeBox = null!;
    private Slider _opacitySlider = null!;
    private ToggleSwitch _capsuleToggle = null!;

    private StackPanel _connectionList = null!;
    private ComboBox _connTypeBox = null!;
    private TextBox _connIdBox = null!;
    private TextBox _connEndpointBox = null!;
    private PasswordBox _connTokenBox = null!;
    private Button _connSaveButton = null!;
    private TextBlock _connFeedback = null!;
    private string? _editingConnectionId;

    private StackPanel _widgetList = null!;
    private ComboBox _widgetTypeBox = null!;
    private ComboBox _widgetConnectionBox = null!;
    private ComboBox _widgetTierBox = null!;
    private StackPanel _widgetFields = null!;
    private TextBox _widgetColorBox = null!;
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
    }

    private UIElement BuildRoot()
    {
        var root = new StackPanel { Spacing = 14, Padding = new Thickness(20, 16, 20, 16) };

        root.Children.Add(SectionTitle("常规"));
        _hotkeyBox = new TextBox { Header = "全局热键（如 Ctrl+Alt+B）", Text = _runtime.Config.App.Hotkey, Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
        _hotkeyBox.LostFocus += (_, _) => SaveGeneral();
        _startupToggle = new ToggleSwitch { Header = "开机自启", IsOn = _runtime.Config.App.LaunchOnStartup };
        _startupToggle.Toggled += (_, _) => SaveGeneral();
        _themeBox = new ComboBox { Header = "主题", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
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
        root.Children.Add(new StackPanel { Spacing = 10, Children = { _hotkeyBox, _startupToggle, _themeBox, _opacitySlider, _capsuleToggle } });

        root.Children.Add(SectionTitle("外观"));
        root.Children.Add(BuildAppearanceSection());

        root.Children.Add(SectionTitle("连接"));
        _connectionList = new StackPanel { Spacing = 6 };
        root.Children.Add(_connectionList);
        root.Children.Add(BuildConnectionEditor());
        root.Children.Add(_connFeedback = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });

        root.Children.Add(SectionTitle("组件"));
        _widgetList = new StackPanel { Spacing = 6 };
        root.Children.Add(_widgetList);
        root.Children.Add(BuildWidgetEditor());
        root.Children.Add(_widgetFeedback = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });

        root.Children.Add(SectionTitle("导入导出"));
        root.Children.Add(BuildImportExportSection());

        root.Children.Add(SectionTitle("通知规则"));
        root.Children.Add(new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184)),
            Text = DescribeRules(),
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            Text = "规则经 config.json 的 notificationRules 自定义；留空使用内置默认。",
            TextWrapping = TextWrapping.Wrap,
        });

        RebuildConnections();
        RebuildWidgets();

        return new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void SaveGeneral()
    {
        var app = _runtime.Config.App;
        app.Hotkey = _hotkeyBox.Text.Trim();
        app.LaunchOnStartup = _startupToggle.IsOn;
        app.Theme = TagOf(_themeBox) ?? "system";
        app.UiOpacity = _opacitySlider.Value;
        app.ShowCapsule = _capsuleToggle.IsOn;
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

    // —— 导入导出（B-802）：四份配置单文件；secrets 绝不入包，导入后按 credentialRef 提示重录 ——

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
            Text = "导出包含 config/connections/widgets/pins 四份配置；密钥绝不入包（JSON 只存 credentialRef），导入后按提示在连接里重录。",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(_importFeedback = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 139, 148, 158)), TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    private async Task ExportAsync()
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
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
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
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
            var reentry = result.CredentialRefs.Count > 0
                ? $"；请在「连接」里重录密钥：{string.Join("、", result.CredentialRefs)}"
                : "";
            Feedback(_importFeedback, $"✓ 已导入 {result.Connections} 连接 / {result.Widgets} 组件 / {result.Pins} 钉选{reentry}。", error: false);
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

    private UIElement BuildConnectionEditor()
    {
        _connTypeBox = new ComboBox { Header = "类型", Width = 160 };
        foreach (var type in _runtime.ConnectionProviders.Keys)
        {
            _connTypeBox.Items.Add(type);
        }
        if (_connTypeBox.Items.Count > 0)
        {
            _connTypeBox.SelectedIndex = 0;
        }
        _connIdBox = new TextBox { Header = "名称（唯一 Id）", Width = 200, PlaceholderText = "github-main" };
        _connEndpointBox = new TextBox { Header = "Endpoint（可空 = 官方 API）", Width = 280, PlaceholderText = "https://api.github.com" };
        _connTokenBox = new PasswordBox { Header = "Token（只写 DPAPI，JSON 仅存引用）", Width = 280 };

        _connSaveButton = new Button { Content = "保存连接" };
        _connSaveButton.Click += async (_, _) => await SaveConnectionAsync();
        var cancel = new Button { Content = "取消编辑" };
        cancel.Click += (_, _) => ResetConnectionEditor();

        var form = new StackPanel { Spacing = 8, Padding = new Thickness(0, 4, 0, 0) };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _connTypeBox, _connIdBox } });
        form.Children.Add(_connEndpointBox);
        form.Children.Add(_connTokenBox);
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
        };
        _runtime.Config.UpsertConnection(connection);
        ResetConnectionEditor();
        RebuildConnections();
        Feedback(_connFeedback, $"✓ 连接 {id} 已保存。", error: false);
        SettingsApplied?.Invoke();
    }

    private void ResetConnectionEditor()
    {
        _editingConnectionId = null;
        _connSaveButton.Content = "保存连接";
        _connIdBox.Text = "";
        _connIdBox.IsEnabled = true;
        _connEndpointBox.Text = "";
        _connTokenBox.Password = "";
        _connTokenBox.PlaceholderText = "";
    }

    private void RebuildConnections()
    {
        _connectionList.Children.Clear();
        foreach (var connection in _runtime.Config.Connections)
        {
            _connectionList.Children.Add(MakeConnectionRow(connection));
        }
        if (_runtime.Config.Connections.Count == 0)
        {
            _connectionList.Children.Add(new TextBlock
            {
                Text = "尚无连接——先添加一个 GitHub 连接（PAT），组件才能拉取状态。",
                FontSize = 12,
                Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            });
        }
    }

    private UIElement MakeConnectionRow(ConnectionConfig connection)
    {
        var label = new TextBlock
        {
            Text = $"{connection.Id} · {connection.Type}{(connection.CredentialRef is null ? " · 无凭据" : " · 凭据已存")}",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var test = new Button { Content = "测试" };
        test.Click += async (_, _) => await TestConnectionAsync(connection);
        var edit = new Button { Content = "编辑" };
        edit.Click += (_, _) =>
        {
            _editingConnectionId = connection.Id;
            _connSaveButton.Content = "更新连接";
            _connIdBox.Text = connection.Id;
            _connIdBox.IsEnabled = false;
            _connEndpointBox.Text = connection.Endpoint ?? "";
            _connTokenBox.Password = "";
            _connTokenBox.PlaceholderText = "已保存（留空保持不变）";
        };
        var delete = new Button { Content = "删除" };
        delete.Click += (_, _) =>
        {
            _runtime.Config.RemoveConnection(connection.Id);
            RebuildConnections();
            RebuildWidgets(); // ConnectionId 失联的组件如实展示
            PinsChanged?.Invoke();
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { label, test, edit, delete },
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
            var health = await provider.TestAsync(connection, new ConnectionContext { Secrets = _runtime.Secrets }, CancellationToken.None);
            Feedback(_connFeedback, health switch
            {
                ConnectionHealthState.Healthy => $"✓ {connection.Id} 连接正常。",
                ConnectionHealthState.Degraded => $"△ {connection.Id} 降级（限流等）。",
                ConnectionHealthState.Offline => $"✗ {connection.Id} 不可达。",
                ConnectionHealthState.Unauthorized => $"✗ {connection.Id} 认证失败，检查 token。",
                _ => $"✗ {connection.Id} 状态未知。",
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
        foreach (var descriptor in GitHubWidgetDescriptors.All)
        {
            _widgetTypeBox.Items.Add(new ComboBoxItem { Content = descriptor.DisplayName, Tag = descriptor.Type });
        }
        if (_widgetTypeBox.Items.Count > 0)
        {
            _widgetTypeBox.SelectedIndex = 0;
        }
        _widgetTypeBox.SelectionChanged += (_, _) => RebuildWidgetFields();
        _widgetConnectionBox = new ComboBox { Header = "连接", Width = 200 };
        _widgetTierBox = new ComboBox { Header = "刷新档", Width = 140 };
        foreach (var tier in new[] { RefreshTiers.Pr, RefreshTiers.Ci, RefreshTiers.Machine, RefreshTiers.Agent, RefreshTiers.Workflow, RefreshTiers.Static, RefreshTiers.Default })
        {
            _widgetTierBox.Items.Add(tier);
        }

        _widgetFields = new StackPanel { Spacing = 8 };
        _widgetColorBox = new TextBox { Header = "颜色覆盖（可选 #RRGGBB，作用于状态灯）", PlaceholderText = "#3fb950", Width = 200, HorizontalAlignment = HorizontalAlignment.Left };

        var save = new Button { Content = "添加组件" };
        save.Click += (_, _) => SaveWidget();

        var form = new StackPanel { Spacing = 8, Padding = new Thickness(0, 4, 0, 0) };
        form.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _widgetTypeBox, _widgetConnectionBox, _widgetTierBox } });
        form.Children.Add(_widgetFields);
        form.Children.Add(_widgetColorBox);
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
        var previous = TagOf(_widgetConnectionBox);
        _widgetConnectionBox.Items.Clear();
        foreach (var connection in _runtime.Config.Connections)
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

        var repo = config.GetValueOrDefault("repo", "widget");
        var id = $"{descriptor.Type}:{repo}";
        for (var suffix = 2; _runtime.Config.FindWidget(id) is not null; suffix++)
        {
            id = $"{descriptor.Type}:{repo}-{suffix}";
        }

        var widget = new WidgetConfig
        {
            Id = id,
            Type = descriptor.Type,
            ConnectionId = TagOf(_widgetConnectionBox) ?? "",
            Config = config,
            RefreshTier = SelectedString(_widgetTierBox) ?? descriptor.SuggestedTier,
            Pinned = false,
            ColorOverride = colorOverride,
        };
        _runtime.Config.UpsertWidget(widget);
        RebuildWidgetFields(); // 清空已提交的字段输入（含颜色覆盖）
        RebuildWidgets();
        RefreshWidgetConnectionOptions();
        Feedback(_widgetFeedback, $"✓ 组件 {id} 已添加。", error: false);
        PinsChanged?.Invoke();
    }

    private WidgetTypeDescriptor? SelectedWidgetDescriptor()
        => TagOf(_widgetTypeBox) is { } type
            ? GitHubWidgetDescriptors.All.FirstOrDefault(d => d.Type == type)
            : null;

    private void RebuildWidgets()
    {
        _widgetList.Children.Clear();
        foreach (var widget in _runtime.Config.Widgets)
        {
            _widgetList.Children.Add(MakeWidgetRow(widget));
        }
        if (_runtime.Config.Widgets.Count == 0)
        {
            _widgetList.Children.Add(new TextBlock
            {
                Text = "尚无组件——用下方向导添加（仓库/工作流等字段由类型决定）。",
                FontSize = 12,
                Foreground = new SolidColorBrush(SeverityPalette.Rgb(255, 100, 116, 139)),
            });
        }
    }

    private UIElement MakeWidgetRow(WidgetConfig widget)
    {
        var descriptor = GitHubWidgetDescriptors.All.FirstOrDefault(d => d.Type == widget.Type);
        var missingConnection = _runtime.Config.Connections.All(c => c.Id != widget.ConnectionId);
        var label = new TextBlock
        {
            Text = $"{widget.Id} · {widget.RefreshTier}"
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
                PinsChanged?.Invoke();
            };
            row.Children.Add(pin);
        }

        var delete = new Button { Content = "删除" };
        delete.Click += (_, _) =>
        {
            _runtime.Config.RemoveWidget(widget.Id);
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
