using System.Globalization;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Windows;

/// <summary>
/// 组件中心（原静态组件示例页改造）：12 个声明式组件类型的「选 → 调 → 用」单一闭环——
/// 左列类型目录（点击切换），中列实时预览 + 生成 JSON（渲染走 ComponentSurfaceRenderer
/// 与真实面同一条链，所见即所得；预览盒定高，换参数不挤挪周边内容），右列参数表单
/// （按类型声明可编辑属性、通用控件渲染；改参数只重渲预览不重建表单，输入不丢焦点）。
/// 产出口径与设置页「组件树 JSON」框同构（surface 键 → 树根）——「载入组件树编辑器」
/// 把当前组件推进设置页编辑器，仍走既有单一保存链（SaveComponentTree 前置校验 +
/// UpsertWidget 二次防呆），本窗不新增写入路径。配色口径同 DetailWindow（暗色静态）。
/// 2026-10-18 评审 P0/P1 双模式：组件行「展示样式」带入该组件真实状态定向打开——预览即真实
/// 数据、左列为按数据能力筛选的展示模板（DisplayTemplates，bar/ring/number/pool/statusline）、
/// 应用面可选 tile/row/detail、应用一步落盘即时重渲；关于区入口保持静态工具箱语义（12 类
/// 目录 + 参数表单所见即调所得，高级组合走树编辑器）。
/// </summary>
internal sealed class SampleGalleryWindow : Window
{
    // 与 DetailWindow 同口径：设计值 DIP，Loaded 后按内容实测 + DPI/非客户区补差（DpiLayoutMath）。
    private const double CenterWidthDips = 860;
    private const double CenterMinHeightDips = 460;
    private const double CenterMaxHeightDips = 780;

    private const double CatalogColumnDips = 168;
    private const double PreviewColumnDips = 300;

    /// <summary>参数控件形态：文本 / 字号档 / 色调令牌 / 严重度枚举 / 间距令牌 / 数值滑杆。</summary>
    private enum PropKind { Text, Size, Tone, Severity, Spacing, Slider }

    /// <summary>可编辑属性声明：类型词汇的「可调什么」单源（Default 同时是初值与空值语义缺省）。</summary>
    private sealed record PropSpec(string Key, string Label, PropKind Kind, string Default, double Min = 0, double Max = 1);

    private sealed record CatalogEntry(string Type, string Name, string Desc);

    /// <summary>类型目录（键即 ComponentSurfaceRenderer.Registry 词汇，顺序即展示序）。</summary>
    private static readonly CatalogEntry[] Catalog =
    [
        new("text", "文本", "主体或说明文一行；可调字号与色调"),
        new("sectionlabel", "区块标签", "小节标题（弱化基色）"),
        new("metric", "指标", "大数字 + 单位，字号可调"),
        new("keyvalue", "键值对", "左标签右值（配 row 排布）"),
        new("progress", "进度条", "0..1 归一化，档位色随值"),
        new("statusdot", "状态灯", "按严重度取色的圆点"),
        new("badge", "徽标", "计数圆标（未读数口径）"),
        new("divider", "分隔线", "水平分隔线"),
        new("actionbutton", "动作按钮", "执行动作；预览恒出诊断占位（动作全集只在真实面接入）"),
        new("row", "横排", "横向排列子件，间距可调（示例含演示子件）"),
        new("column", "竖排", "纵向排列子件，间距可调（示例含演示子件）"),
        new("card", "卡片", "单内容卡面，内边距可调（示例含演示子件）"),
    ];

    /// <summary>类型 → 可编辑属性（when 条件对所有类型通用，表单构建时统一追加）。</summary>
    private static readonly Dictionary<string, PropSpec[]> PropSchema = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] =
        [
            new("text", "文本", PropKind.Text, "GLM · ¥110.00"),
            new("size", "字号", PropKind.Size, "body"),
            new("tone", "色调", PropKind.Tone, ""),
        ],
        ["sectionlabel"] = [new("text", "标签", PropKind.Text, "用量")],
        ["metric"] =
        [
            new("value", "数值", PropKind.Text, "72"),
            new("unit", "单位", PropKind.Text, "GB"),
            new("fontSize", "字号", PropKind.Slider, "20", 12, 40),
        ],
        ["keyvalue"] =
        [
            new("key", "标签", PropKind.Text, "repo"),
            new("value", "值", PropKind.Text, "cuihairu/beacon"),
        ],
        ["progress"] =
        [
            new("progress", "进度值", PropKind.Slider, "0.52", 0, 1),
            new("width", "宽度", PropKind.Slider, "220", 40, 320),
        ],
        ["statusdot"] =
        [
            new("severity", "严重度", PropKind.Severity, "success"),
            new("size", "尺寸", PropKind.Slider, "8", 4, 24),
        ],
        ["badge"] =
        [
            new("text", "文本", PropKind.Text, "3"),
            new("tone", "色调", PropKind.Tone, "bad"),
        ],
        ["divider"] = [],
        ["actionbutton"] =
        [
            new("text", "按钮文字", PropKind.Text, "打开文档"),
            new("data-action", "动作 ID", PropKind.Text, "open-quota-doc"),
        ],
        ["row"] = [new("gap", "间距", PropKind.Spacing, "md")],
        ["column"] = [new("gap", "间距", PropKind.Spacing, "md")],
        ["card"] = [new("pad", "内边距", PropKind.Spacing, "md")],
    };

    private readonly UiPalette _palette;
    private readonly Brush _heading = new SolidColorBrush(Microsoft.UI.Colors.White);
    private readonly Brush _body = new SolidColorBrush(SeverityPalette.Rgb(255, 226, 232, 240));
    private readonly Brush _muted = new SolidColorBrush(SeverityPalette.Rgb(255, 148, 163, 184));
    private readonly Brush _surface = new SolidColorBrush(SeverityPalette.Rgb(30, 30, 30, 34));
    private readonly Brush _hairline = new SolidColorBrush(SeverityPalette.Rgb(255, 48, 54, 61));
    private readonly Func<ComponentDescriptor, string>? _applyToSettings;
    private readonly Func<ComponentDescriptor, string, string>? _applyTargeted; // 定向模式（树根, 面）→ 反馈
    private readonly WidgetState? _targetState; // 定向模式：预览用真实状态、样式走 DisplayTemplates

    private string _selectedType = "text";
    private Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private string _templateId = "bar"; // 定向模式当前模板（ctor 按能力筛选后的首项覆写）
    private string _applySurface = "tile"; // 定向模式应用面（tile/row/detail，评审 P1；_surface 已是画刷名）

    private ListView _catalogList = null!;
    private StackPanel _previewHost = null!;
    private StackPanel _formHost = null!;
    private TextBox _jsonBox = null!;
    private TextBlock _jsonHeader = null!;
    private TextBlock _formHeader = null!;
    private TextBlock _feedback = null!;

    /// <param name="applyToSettings">「载入组件树编辑器」回调：推进设置页编辑器，返回反馈文案（✗ 前缀=失败）。</param>
    /// <param name="targetState">定向模式状态（组件行「展示样式」带入该组件最新真实状态）：
    /// 预览即真实数据、样式列表按数据能力筛选（DisplayTemplates.Compatible）、应用一步落盘。</param>
    /// <param name="applyTargeted">定向模式应用回调（树根, 面）→ 反馈文案；与 targetState 成对传入。</param>
    public SampleGalleryWindow(
        BeaconRuntime runtime,
        Action? closed = null,
        Func<ComponentDescriptor, string>? applyToSettings = null,
        WidgetState? targetState = null,
        Func<ComponentDescriptor, string, string>? applyTargeted = null)
    {
        _applyToSettings = applyToSettings;
        _applyTargeted = applyTargeted;
        _targetState = targetState;
        _templateId = DisplayTemplates.Compatible(targetState).FirstOrDefault()?.Id
            ?? DisplayTemplates.All.First(template => !template.RequiresProgress).Id; // 无进度数据也必有可选项
        _palette = new UiPalette(runtime.Config); // 档位/状态色同正式窗解析链

        Title = "Beacon · 组件中心";
        Content = BuildContent();
        ((FrameworkElement)Content).Loaded += OnContentLoaded;
        Closed += (_, _) => closed?.Invoke();
        Services.AppIcon.Apply(AppWindow, runtime.Logger);
    }

    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var dpi = NativeMethods.GetDpiForWindow(hwnd) / 96.0;
        var content = (FrameworkElement)Content;
        content.Measure(new global::Windows.Foundation.Size(CenterWidthDips, double.PositiveInfinity));
        var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(hwnd);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32
        {
            Width = DpiLayoutMath.ToPx(CenterWidthDips, dpi) + chromeWidth,
            Height = DpiLayoutMath.ClampedHeightPx(content.DesiredSize.Height, CenterMinHeightDips, CenterMaxHeightDips, dpi) + chromeHeight,
        });
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Padding = new Thickness(16, 14, 16, 14) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CatalogColumnDips) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PreviewColumnDips) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // —— 左列：定向模式 = 展示样式（模板按数据能力筛选，评审 P1）；通用模式 = 组件类型目录 ——
        var catalog = new StackPanel { Spacing = 8 };
        catalog.Children.Add(new TextBlock
        {
            Text = _targetState is null ? "组件" : "展示样式",
            FontSize = DesignTokens.FontHeader,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = _heading,
        });
        catalog.Children.Add(ComponentText.Caption(
            _targetState is null
                ? "12 个声明式组件类型（渲染注册表词汇）。"
                : _targetState.Progress.HasValue
                    ? "该组件有百分比数据——横条/圆环/数字卡/水位均可选。"
                    : "该组件无百分比数据——只列状态/摘要样式（不显虚假的确定进度）。",
            _muted));

        _catalogList = new ListView { IsItemClickEnabled = true, Height = 452 };
        if (_targetState is null)
        {
            foreach (var entry in Catalog)
            {
                var line = new StackPanel { Spacing = 1 };
                line.Children.Add(new TextBlock { Text = entry.Name, FontSize = DesignTokens.FontBody, Foreground = _body });
                line.Children.Add(new TextBlock { Text = entry.Type, FontSize = DesignTokens.FontCaption, Foreground = _muted });
                _catalogList.Items.Add(new ListViewItem { Content = line, Tag = entry.Type, Padding = new Thickness(8, 5, 8, 5) });
            }
        }
        else
        {
            foreach (var template in DisplayTemplates.Compatible(_targetState))
            {
                var line = new StackPanel { Spacing = 1 };
                line.Children.Add(new TextBlock { Text = template.Name, FontSize = DesignTokens.FontBody, Foreground = _body });
                line.Children.Add(new TextBlock { Text = template.Desc, FontSize = DesignTokens.FontCaption, Foreground = _muted, TextWrapping = TextWrapping.Wrap });
                _catalogList.Items.Add(new ListViewItem { Content = line, Tag = template.Id, Padding = new Thickness(8, 5, 8, 5) });
            }
        }
        _catalogList.ItemClick += OnCatalogItemClicked;
        catalog.Children.Add(_catalogList);
        Grid.SetColumn(catalog, 0);
        root.Children.Add(catalog);

        // —— 中列：实时预览（定高盒，换参数不挤挪）+ 生成 JSON ——
        var preview = new StackPanel { Spacing = 8 };
        preview.Children.Add(new TextBlock
        {
            Text = "预览",
            FontSize = DesignTokens.FontHeader,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = _heading,
        });
        _previewHost = new StackPanel { Spacing = 6 };
        var previewBox = new Border
        {
            Background = _surface,
            BorderBrush = _hairline,
            BorderThickness = new Thickness(1),
            CornerRadius = DesignTokens.RadiusCard,
            Padding = new Thickness(12),
            MinHeight = 176,
            Child = new ScrollViewer
            {
                Content = _previewHost,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
        };
        preview.Children.Add(previewBox);
        preview.Children.Add(ComponentText.Caption(
            _targetState is null
                ? "预览样例状态：severity=warning、progress=0.52、summary=GLM · ¥110.00（when 条件按此求值）。"
                : "预览即该组件最新真实状态（模板树的动态值经绑定注入）。",
            _muted));
        _jsonHeader = new TextBlock
        {
            FontSize = DesignTokens.FontSubtitle,
            Foreground = _heading,
        };
        preview.Children.Add(_jsonHeader);
        _jsonBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 168,
            FontFamily = new FontFamily("Consolas"),
            FontSize = DesignTokens.FontCaption,
        };
        preview.Children.Add(_jsonBox);
        Grid.SetColumn(preview, 2);
        root.Children.Add(preview);

        // —— 右列：参数表单（选类型时整建；改参数只重渲，不丢输入焦点）+ 载入动作 ——
        var editor = new StackPanel { Spacing = 8 };
        _formHeader = new TextBlock
        {
            FontSize = DesignTokens.FontHeader,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = _heading,
        };
        editor.Children.Add(_formHeader);

        _formHost = new StackPanel { Spacing = 2 };
        editor.Children.Add(new ScrollViewer
        {
            Content = _formHost,
            Height = 330,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });

        _feedback = new TextBlock
        {
            FontSize = DesignTokens.FontCaption,
            Foreground = _muted,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 32, // 预留两行，反馈出现/消失不推挤下方按钮
        };
        editor.Children.Add(_feedback);

        var apply = new Button
        {
            Content = _targetState is null ? "载入组件树编辑器" : "应用展示样式",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        apply.Click += OnApplyToSettings;
        editor.Children.Add(apply);
        editor.Children.Add(ComponentText.Caption(
            _targetState is null
                ? "载入后回设置页「组件」标签点「保存组件树」落盘（单一保存链）；JSON 框可选中复制。"
                : "应用即校验落盘并按该组件最新状态即时重渲（不取数）；高级组合仍可回设置页「组件树」编辑器。",
            _muted));
        Grid.SetColumn(editor, 4);
        root.Children.Add(editor);

        // 初始态一次建齐（字段就位后）
        SelectCatalogItem(_targetState is null ? _selectedType : _templateId);
        BuildForm();
        RefreshPreviewAndJson();
        return root;
    }

    private void OnCatalogItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ListViewItem { Tag: string key })
        {
            return;
        }
        if (_targetState is null)
        {
            _selectedType = key; // 通用模式：组件类型
        }
        else
        {
            _templateId = key; // 定向模式：展示模板
        }
        SelectCatalogItem(key);
        BuildForm();
        RefreshPreviewAndJson();
    }

    private void SelectCatalogItem(string type)
    {
        foreach (var item in _catalogList.Items)
        {
            if (item is ListViewItem { Tag: string tag } && string.Equals(tag, type, StringComparison.Ordinal))
            {
                _catalogList.SelectedItem = item;
                return;
            }
        }
    }

    /// <summary>参数表单整建（仅选择切换时；改参数走 OnParamChanged 只重渲预览）。
    /// 定向模式（评审 P1）= 模板说明 + 应用面选择（样式本体不再调参——自由组合回树编辑器，P2 口径）；
    /// 通用模式 = 该类型的 Props 表单 + 通用 when 条件。</summary>
    private void BuildForm()
    {
        _formHost.Children.Clear();
        if (_targetState is not null)
        {
            var template = TemplateOf(_templateId);
            _formHeader.Text = $"样式 · {template.Name}";
            _formHost.Children.Add(ComponentText.Caption(template.Desc, _muted));
            _formHost.Children.Add(SurfacePicker());
            _formHost.Children.Add(ComponentText.Caption(
                "预览即该组件真实数据；应用只改所选面，其余面保留；自由组合回设置页「组件树」编辑器。", _muted));
            return;
        }

        var entry = Catalog.FirstOrDefault(item => string.Equals(item.Type, _selectedType, StringComparison.Ordinal))
            ?? Catalog[0];
        _formHeader.Text = $"参数 · {entry.Name}（{entry.Type}）";
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var specs = new List<PropSpec>();
        if (PropSchema.TryGetValue(_selectedType, out var declared))
        {
            specs.AddRange(declared);
        }
        specs.Add(new PropSpec("when", "条件", PropKind.Text, ""));

        foreach (var spec in specs)
        {
            _values[spec.Key] = spec.Default;
            var row = new Grid { Margin = new Thickness(0, 0, 0, DesignTokens.Space8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var label = new TextBlock
            {
                Text = spec.Label,
                FontSize = DesignTokens.FontCaption,
                Foreground = _muted,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);
            var control = BuildControl(spec);
            Grid.SetColumn(control, 1);
            row.Children.Add(control);
            _formHost.Children.Add(row);
        }
    }

    // 返回口径用 FrameworkElement：WinUI 的 Grid.SetColumn 只收 FrameworkElement（比 WPF 窄一档）。
    private FrameworkElement BuildControl(PropSpec spec)
    {
        return spec.Kind switch
        {
            PropKind.Size => BuildCombo(spec, [("正文", "body"), ("说明文", "caption")]),
            PropKind.Tone => BuildCombo(spec, [("默认", ""), ("信息", "info"), ("正常", "ok"), ("警告", "warn"), ("错误", "bad")]),
            PropKind.Severity => BuildCombo(spec, [("信息", "info"), ("成功", "success"), ("警告", "warning"), ("错误", "error"), ("严重", "critical")]),
            PropKind.Spacing => BuildCombo(spec, [("默认", ""), ("紧凑", "sm"), ("标准", "md"), ("宽松", "lg")]),
            PropKind.Slider => BuildSlider(spec),
            _ => BuildTextBox(spec),
        };
    }

    private TextBox BuildTextBox(PropSpec spec)
    {
        var box = new TextBox { Text = spec.Default };
        box.TextChanged += (_, _) => OnParamChanged(spec, box.Text);
        return box;
    }

    private FrameworkElement BuildCombo(PropSpec spec, (string Display, string Value)[] options)
    {
        var combo = new ComboBox { MinWidth = 120 };
        foreach (var option in options)
        {
            combo.Items.Add(new ComboBoxItem { Content = option.Display, Tag = option.Value });
        }
        combo.SelectedIndex = IndexOfTag(combo, spec.Default);
        combo.SelectionChanged += (_, _) => OnParamChanged(spec, TagOf(combo));
        return combo;
    }

    private FrameworkElement BuildSlider(PropSpec spec)
    {
        var start = double.TryParse(spec.Default, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, spec.Min, spec.Max)
            : spec.Min;
        var host = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.Space6 };
        var slider = new Slider
        {
            Minimum = spec.Min,
            Maximum = spec.Max,
            StepFrequency = spec.Max - spec.Min <= 2 ? 0.01 : 1,
            Value = start,
            Width = 148,
        };
        var readout = new TextBlock
        {
            Text = FormatNumber(start),
            FontSize = DesignTokens.FontCaption,
            Foreground = _muted,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.ValueChanged += (_, _) =>
        {
            readout.Text = FormatNumber(slider.Value);
            OnParamChanged(spec, slider.Value.ToString(CultureInfo.InvariantCulture));
        };
        host.Children.Add(slider);
        host.Children.Add(readout);
        return host;
    }

    private static string FormatNumber(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static int IndexOfTag(ComboBox combo, string tag)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return 0;
    }

    private static string TagOf(ComboBox combo)
        => combo.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() ?? "" : "";

    /// <summary>参数变化：值落槽（空串=不写进 Props），预览与 JSON 同步重渲。</summary>
    private void OnParamChanged(PropSpec spec, string value)
    {
        _values[spec.Key] = value;
        RefreshPreviewAndJson();
    }

    /// <summary>定向模式当前模板定义（未知 Id 回状态行——能力门槛最低的兜底）。</summary>
    private DisplayTemplates.DisplayTemplate TemplateOf(string id)
        => DisplayTemplates.Match(id).FirstOrDefault()
            ?? DisplayTemplates.All.First(template => !template.RequiresProgress);

    /// <summary>应用面选择（评审 P1：同一数据在 tile/row/detail 各面可各选样式）。</summary>
    private FrameworkElement SurfacePicker()
    {
        var combo = new ComboBox { MinWidth = 120 };
        foreach (var (label, value) in new[] { ("悬浮 tile 面", "tile"), ("面板行 row 面", "row"), ("详情 detail 面", "detail") })
        {
            combo.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        combo.SelectedIndex = IndexOfTag(combo, _applySurface);
        combo.SelectionChanged += (_, _) =>
        {
            _applySurface = TagOf(combo);
            RefreshPreviewAndJson();
        };
        return combo;
    }

    private void RefreshPreviewAndJson()
    {
        var targeted = _targetState is not null;
        var surface = targeted ? _applySurface : "tile";
        // 定向模式：模板生成器整树出（动态值经绑定注入）；通用模式：表单静态树
        var rootDescriptor = targeted ? TemplateOf(_templateId).Build() : BuildDescriptor();
        var instance = new WidgetInstance
        {
            Id = "component-center",
            Type = "component-center",
            Surface = surface,
            Config = new Dictionary<string, string>(),
            Root = rootDescriptor,
        };
        // 渲染走真实链（同一注册表/同一绑定引擎口径）：所见即落盘后所得
        _previewHost.Children.Clear();
        _previewHost.Children.Add(ComponentSurfaceRenderer.Render(instance, PreviewState(), _palette));

        _jsonHeader.Text = $"JSON（{surface} 面）";
        _jsonBox.Text = BeaconJson.Serialize(
            new Dictionary<string, ComponentDescriptor>(StringComparer.OrdinalIgnoreCase) { [surface] = rootDescriptor },
            indented: true);
    }

    /// <summary>当前表单 → 组件树根（空串属性不落 Props；容器类型带演示子件）。</summary>
    private ComponentDescriptor BuildDescriptor()
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in _values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                props[key] = value;
            }
        }
        return new ComponentDescriptor
        {
            Type = _selectedType,
            Props = props,
            Children = DemoChildren(_selectedType),
        };
    }

    /// <summary>容器演示子件（row/column 两个、card 单内容）：预览与 JSON 同带，所改间距/内边距即刻可见。</summary>
    private static List<ComponentDescriptor> DemoChildren(string type)
    {
        if (type.Equals("card", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new ComponentDescriptor
                {
                    Type = "text",
                    Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["text"] = "卡面演示子件（落盘后可在树编辑器删改）",
                    },
                },
            ];
        }
        var isContainer = type.Equals("row", StringComparison.OrdinalIgnoreCase)
            || type.Equals("column", StringComparison.OrdinalIgnoreCase);
        if (!isContainer)
        {
            return [];
        }
        return
        [
            new ComponentDescriptor
            {
                Type = "text",
                Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["text"] = "子件一",
                    ["tone"] = "ok",
                },
            },
            new ComponentDescriptor
            {
                Type = "statusdot",
                Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["severity"] = "success",
                },
            },
        ];
    }

    /// <summary>预览状态：定向模式 = 该组件最新真实状态；通用模式 = 中性样例值。</summary>
    private WidgetState PreviewState() => _targetState ?? new WidgetState
    {
        WidgetId = "preview",
        WidgetType = "preview",
        ConnectionId = "preview",
        Severity = Severity.Warning,
        Summary = "GLM · ¥110.00",
        Progress = 0.52,
    };

    private void OnApplyToSettings(object sender, RoutedEventArgs e)
    {
        if (_targetState is not null)
        {
            if (_applyTargeted is null)
            {
                Feedback("✗ 本窗未接入设置页（请从组件行「展示样式」进入）。", error: true);
                return;
            }
            var targetedMessage = _applyTargeted(TemplateOf(_templateId).Build(), _applySurface);
            Feedback(targetedMessage, error: targetedMessage.StartsWith('✗'));
            return;
        }
        if (_applyToSettings is null)
        {
            Feedback("✗ 本窗未接入设置页（请从设置「关于」区的「组件中心」进入）。", error: true);
            return;
        }
        var message = _applyToSettings(BuildDescriptor());
        Feedback(message, error: message.StartsWith('✗'));
    }

    private void Feedback(string message, bool error = false)
    {
        _feedback.Text = message;
        _feedback.Foreground = error ? new SolidColorBrush(_palette.SeverityColor(Severity.Error)) : _muted;
    }
}
