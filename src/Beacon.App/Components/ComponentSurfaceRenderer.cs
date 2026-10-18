using System.Globalization;
using Beacon.App.Services;
using Beacon.App.Windows;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 动作桥接上下文（Phase 4 Action ID 桥，2026-10-17 裁定 §6）：渲染侧执行动作的窄口——
/// 动作全集（actions.json + 连接 DSL）与执行回调。null = 该消费面不承载动作（tile / row 面），
/// actionbutton 出诊断占位而非可点按钮。存在性 / 可见性校验与执行都在渲染侧（类型词汇归
/// 渲染侧的已代决⑤口径）；执行回调沿用调用面既有链（确认 / 反馈语义不变）。
/// </summary>
internal sealed record ComponentActionContext(
    IReadOnlyList<ActionConfig> Actions,
    Func<ActionConfig, Task> Run);

/// <summary>
/// 声明式组件渲染器（P3-01，契约 §3.5-10 渲染侧注册表）：ComponentDescriptor 树 → WinUI 可视树。
/// 节点类型键在本类注册表解析（契约已代决⑤：类型词汇归渲染侧、不进 Core）；绑定值经
/// ComponentBindingEngine.ResolveBinding 按状态求值（fail-closed 空串，不猜）；
/// Props 字符串换算（数值/枚举）失败回默认值——非法声明不炸常驻进程。
/// 结构合法性由 WidgetInstanceResolver.Resolve 前置保证（非法树拿不到实例，面回传统渲染），
/// 本类不再重复校验深度/节点上限。未知类型/渲染异常出诊断占位（可见可查，不静默吞）；
/// 状态每次变化整树重渲（树 ≤256 节点、无跨帧局部状态，重渲即「改布局只重解析」的运行时同款）。
/// Phase 4（2026-10-17 裁定）：HTML 载体面展开后的合成树同样进本注册表——类令牌
/// tone/pad/gap 在此换算档位色与 DIP（Core 只存令牌词，类型换算留渲染期）；
/// actionbutton 经 ComponentActionContext 桥到既有动作执行链。
/// </summary>
internal static class ComponentSurfaceRenderer
{
    /// <summary>类令牌 tone → 五级档位（裁定 §7 词表：info/ok/warn/bad）。</summary>
    private static readonly Dictionary<string, Severity> Tones = new(StringComparer.Ordinal)
    {
        ["info"] = Severity.Info,
        ["ok"] = Severity.Success,
        ["warn"] = Severity.Warning,
        ["bad"] = Severity.Error,
    };

    /// <summary>渲染节点上下文：注册表工厂只从这里取数据（节点 + 自身状态 + 附加源映射 + 配色 + 动作桥）。</summary>
    private sealed class NodeContext(
        ComponentDescriptor node,
        WidgetState? state,
        UiPalette palette,
        IReadOnlyDictionary<string, WidgetState>? sources,
        ComponentActionContext? actions = null)
    {
        public ComponentDescriptor Node { get; } = node;
        public WidgetState? State { get; } = state;
        public UiPalette Palette { get; } = palette;
        public ComponentActionContext? Actions { get; } = actions;

        /// <summary>绑定槽取值（fail-closed 空串）：只看本节点绑定，不猜别的槽；
        /// 带源映射时 SourceName 按精确源名取附加源（P3-03），映射缺席仍回空串。</summary>
        public string Binding(string field)
        {
            var binding = Node.Bindings.FirstOrDefault(b =>
                b is not null && string.Equals(b.Field, field, StringComparison.OrdinalIgnoreCase));
            return binding is null ? "" : ComponentBindingEngine.ResolveBinding(binding, State, sources);
        }

        public string Prop(string key, string fallback = "") => Node.Props.GetValueOrDefault(key, fallback);

        public double PropDouble(string key, double fallback)
            => double.TryParse(Prop(key), CultureInfo.InvariantCulture, out var value) ? value : fallback;

        public Brush Foreground => new SolidColorBrush(ThemeColors.Label());

        /// <summary>severity 文本 → 档位色（widget 覆盖色由面层 palette 决策，这里走默认档位色）；解析失败灰。</summary>
        public Brush SeverityBrush(string severityText)
            => Enum.TryParse<Severity>(severityText, ignoreCase: true, out var severity)
                ? new SolidColorBrush(Palette.SeverityColor(severity))
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);

        /// <summary>类令牌 tone → 档位色；缺省 / 未知词回基色（Core 词表封闭，此处兜底不猜）。</summary>
        public Brush ToneOr(Brush fallback)
            => Tones.TryGetValue(Prop("tone"), out var severity)
                ? new SolidColorBrush(Palette.SeverityColor(severity))
                : fallback;

        /// <summary>类令牌 gap（sm/md/lg → DesignTokens 4/8/12，裁定 §7）；无 gap 回 spacing 数值
        /// prop（手写树口径），再回 Space2——同一属性槽两个来源，令牌词优先。</summary>
        public double Spacing => Prop("gap") switch
        {
            "sm" => DesignTokens.Space4,
            "md" => DesignTokens.Space8,
            "lg" => DesignTokens.Space12,
            _ => PropDouble("spacing", DesignTokens.Space2),
        };

        /// <summary>类令牌 pad（sm/md/lg → 4/8/12）；无 pad 回调用面默认（卡片 Space10 口径）。</summary>
        public double PadOr(double fallback) => Prop("pad") switch
        {
            "sm" => DesignTokens.Space4,
            "md" => DesignTokens.Space8,
            "lg" => DesignTokens.Space12,
            _ => fallback,
        };
    }

    private delegate FrameworkElement NodeFactory(NodeContext context);

    /// <summary>类型注册表（渲染侧唯一事实来源）：类型键 → 工厂。容器挂子见 RenderNode（row/column 面板、card 单卡）。</summary>
    private static readonly Dictionary<string, NodeFactory> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["row"] = context => ComponentLayout.Row(context.Spacing),
        ["column"] = context => ComponentLayout.Column(context.Spacing),
        ["card"] = _ => new Grid(), // 占位：有子节点由 RenderNode 换装 Card（单卡面）；无子=空 Grid
        ["text"] = context =>
        {
            var text = DisplayText(context.Binding("text"), context.Prop("text"));
            var brush = context.ToneOr(context.Foreground);
            return string.Equals(context.Prop("size"), "caption", StringComparison.OrdinalIgnoreCase)
                ? ComponentText.Caption(text, brush)
                : ComponentText.Body(text, brush);
        },
        ["sectionlabel"] = context =>
            ComponentLayout.SectionLabel(DisplayText(context.Binding("text"), context.Prop("text")), context.Foreground),
        ["metric"] = context => ComponentMetric.Value(
            DisplayText(context.Binding("value"), context.Prop("value")),
            context.Node.Props.TryGetValue("unit", out var unit) && !string.IsNullOrWhiteSpace(unit) ? unit : null,
            context.Foreground,
            context.PropDouble("fontSize", DesignTokens.FontHeader)),
        ["progress"] = Progress,
        ["ring"] = Ring,
        ["pool"] = Pool,
        ["statusdot"] = context => ComponentIcon.StatusDot(
            context.SeverityBrush(DisplayText(context.Binding("severity"), context.Prop("severity"))),
            context.PropDouble("size", 8)),
        ["badge"] = context => ComponentBadge.Count(
            new TextBlock
            {
                Text = DisplayText(context.Binding("text"), context.Prop("text")),
                Foreground = context.Foreground,
            },
            context.ToneOr(new SolidColorBrush(context.Palette.SeverityColor(Severity.Info)))),
        ["keyvalue"] = context => ComponentKeyValue.Row(
            context.Prop("key"),
            DisplayText(context.Binding("value"), context.Prop("value")),
            context.Foreground),
        ["divider"] = context => ComponentDivider.Horizontal(context.Foreground),
        ["actionbutton"] = ActionButton,
    };

    /// <summary>整树渲染（自身状态单源）：SourceName 绑定 fail-closed 空串（P3-01 切片语义保留）。
    /// actions 缺省 null = 该面不承载动作（actionbutton 出诊断占位，裁定 §6）。</summary>
    public static FrameworkElement Render(WidgetInstance instance, WidgetState? state, UiPalette palette, ComponentActionContext? actions = null)
        => RenderNode(instance.Root, state, palette, null, actions);

    /// <summary>
    /// 带附加源映射的整树渲染（P3-03 依赖路由）：own 状态在保留键 ""，绑定 SourceName 按精确源名取值；
    /// 映射由 WidgetInstanceResolver.ResolveSources 按 Sources 声明构建（调用面快照口径，不触发数据请求）。
    /// 声明却缺席的源出缺失态标题（缺源不是空值，不拿健康旧值冒充）；零缺失零包装，与无附加源的面同构。
    /// </summary>
    public static FrameworkElement Render(
        WidgetInstance instance,
        IReadOnlyDictionary<string, WidgetState> sources,
        UiPalette palette,
        ComponentActionContext? actions = null)
    {
        var own = sources.TryGetValue(ComponentBindingSource.OwnStateKey, out var state) ? state : null;
        var content = RenderNode(instance.Root, own, palette, sources, actions);
        var missing = ComponentSourceIndex.MissingSources(instance, sources);
        if (missing.Count == 0)
        {
            return content;
        }
        var host = new StackPanel { Spacing = 2 };
        foreach (var name in missing)
        {
            var widgetId = instance.Sources.GetValueOrDefault(name, "");
            host.Children.Add(ComponentText.Caption(
                $"⚠ 缺少数据源：{name}（{widgetId}）",
                new SolidColorBrush(ThemeColors.Label())));
        }
        host.Children.Add(content);
        return host;
    }

    private static FrameworkElement RenderNode(
        ComponentDescriptor node,
        WidgetState? state,
        UiPalette palette,
        IReadOnlyDictionary<string, WidgetState>? sources,
        ComponentActionContext? actions = null)
    {
        if (!Registry.TryGetValue(node.Type, out var factory))
        {
            // 未知类型：诊断占位可见可查（P3-01：定位到面的诊断），不静默吞也不炸
            return ComponentText.Caption($"⚠ 未注册组件类型：{node.Type}", new SolidColorBrush(ThemeColors.Label()));
        }

        NodeContext context;
        FrameworkElement element;
        try
        {
            context = new NodeContext(node, state, palette, sources, actions);
            element = factory(context);
        }
        catch (Exception)
        {
            // 属性换算/工厂异常按坏节点处理：占位诊断顶住，兄弟节点照常渲染
            return ComponentText.Caption($"⚠ 组件 {node.Type} 渲染失败", new SolidColorBrush(ThemeColors.Label()));
        }

        // card 单卡面（契约允许 Children，卡语义单内容）：首子进 Card，其余忽略；
        // 类令牌 pad 在此换算内边距（HTML 卡片面，裁定 §7），无 pad 回 Space10 默认
        if (element is Grid && string.Equals(node.Type, "card", StringComparison.OrdinalIgnoreCase))
        {
            var first = node.Children.FirstOrDefault(c => c is not null);
            element = ComponentSurface.Card(
                first is null ? new Grid() : RenderNode(first, state, palette, sources, actions),
                new SolidColorBrush(ThemeColors.Surface()),
                context.PadOr(DesignTokens.Space10));
        }
        else if (element is Panel panel)
        {
            foreach (var child in node.Children)
            {
                if (child is not null)
                {
                    panel.Children.Add(RenderNode(child, state, palette, sources, actions));
                }
            }
        }

        // 条件显示（契约 §3 / ComponentVisibility 语法）：缺省可见；state 缺位或解析不出 = 隐藏
        if (node.Props.TryGetValue("when", out var condition)
            && !string.IsNullOrWhiteSpace(condition)
            && (state is null || !ComponentVisibility.Evaluate(condition, state)))
        {
            element.Visibility = Visibility.Collapsed;
        }
        return element;
    }

    /// <summary>
    /// Action ID 桥（Phase 4 裁定 §6）：动作 ID 来自模板静态字面（Core 解析期已拒插值/空白）；
    /// 存在性 + ActionVisibility 在渲染侧校验（类型词汇归渲染侧）——动作全集缺席（tile/row 面不
    /// 承载动作）、ID 不存在或不可见都出诊断占位，不渲染可点按钮；存在且可见走既有执行回调
    /// （确认 / 反馈语义沿用调用面链，RequireConfirmation 照常）。
    /// </summary>
    private static FrameworkElement ActionButton(NodeContext context)
    {
        var id = context.Prop("data-action");
        var match = context.Actions?.Actions.FirstOrDefault(action =>
            string.Equals(action.Id, id, StringComparison.Ordinal));
        if (match is null
            || !ActionVisibility.IsVisible(match.Parameters, context.State?.WidgetType ?? "", context.State?.ConnectionId ?? ""))
        {
            return ComponentText.Caption($"⚠ 动作不可用：{id}", new SolidColorBrush(ThemeColors.Label()));
        }
        var action = match;
        return ComponentAction.Button(
            DisplayText(context.Binding("text"), context.Prop("text")),
            () => context.Actions!.Run(action));
    }

    /// <summary>进度条：绑定槽 progress（0..1，越界钳位）优先，回退静态 Props.progress；width 默认 96 DIP。</summary>
    /// <summary>注册类型键（设置页可视化树编辑器的类型词汇单源——渲染侧已知什么才允许声明什么）。
    /// 必须在 Registry 字段初始化之后取值（静态初始化按文本序）。</summary>
    public static IReadOnlyList<string> KnownTypes { get; } = Registry.Keys.Order().ToArray();

    private static FrameworkElement Progress(NodeContext context)
    {
        var fraction = BoundFraction(context);
        var (track, fill) = ComponentProgress.Bar(fraction, context.PropDouble("width", 96));
        // 2026-10-18 修复：fill 色原漏上（Shape.Fill 默认 null=透明，声明式树进度条只剩浅轨）——
        // 补档位色（ProgressTiers 判档），与传统 tile 链（PinnedHostWindow）同口径。
        fill.Fill = new SolidColorBrush(ComponentProgress.FillColor(fraction, ThemeColors.Value(), severity => context.Palette.SeverityColor(severity)));
        var host = new Grid();
        host.Children.Add(track);
        host.Children.Add(fill);
        return host;
    }

    /// <summary>进度槽取值（Progress/Ring/Pool 同口径）：绑定槽 progress 优先（0..1 钳位），
    /// 回退静态 Props.progress；绑定为空串（无状态/字段缺）即回退静态值。</summary>
    private static double BoundFraction(NodeContext context)
        => double.TryParse(context.Binding("progress"), CultureInfo.InvariantCulture, out var bound)
            ? Math.Clamp(bound, 0, 1)
            : Math.Clamp(context.PropDouble("progress", 0), 0, 1);

    /// <summary>静态数值 Props 的有界钳位（Ring/Pool 专用）：解析值越界取边界，非有限值
    /// （NaN/Infinity 可被 TryParse 解析出来）回缺省——NaN 进 Width/Height 恒 E_INVALIDARG。</summary>
    private static double ClampedProp(NodeContext context, string key, double fallback, double min, double max)
    {
        var value = context.PropDouble(key, fallback);
        return double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    }

    /// <summary>圆环进度（2026-10-18 评审 P1「圆环」模板）：双 Ellipse 同心——track=值色 35% 透明、
    /// fill=档位色 StrokeDashArray 弧段（中线周长 π×(size−thickness)，DashOffset 四分之一周长
    /// 从 12 点方向起笔）；size/thickness 静态 Props 可调（有界钳位）。</summary>
    private static FrameworkElement Ring(NodeContext context)
    {
        var fraction = BoundFraction(context);
        var size = ClampedProp(context, "size", 30, 16, 96);
        var thickness = ClampedProp(context, "thickness", 3, 1, size / 4);
        var circumference = Math.PI * (size - thickness);
        var host = new Grid { Width = size, Height = size };
        host.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(ThemeColors.Value()) { Opacity = 0.35 },
            StrokeThickness = thickness,
        });
        host.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(ComponentProgress.FillColor(fraction, ThemeColors.Value(), severity => context.Palette.SeverityColor(severity))),
            StrokeThickness = thickness,
            StrokeDashArray = new DoubleCollection { Math.Max(fraction * circumference, 0), circumference },
            StrokeDashOffset = circumference / 4,
        });
        return host;
    }

    /// <summary>水位池（2026-10-18 评审 P1「水位」模板）：圆角矩形容器 + 底部注水矩形（档位色、
    /// Radius 随容器；不用 Clip——UIElement.Clip 在 WinUI 只收 RectangleGeometry，8b94469 实证）。</summary>
    private static FrameworkElement Pool(NodeContext context)
    {
        var fraction = BoundFraction(context);
        var width = ClampedProp(context, "width", 30, 16, 96);
        var height = ClampedProp(context, "height", 30, 16, 96);
        const double radius = 6;
        var host = new Grid { Width = width, Height = height };
        host.Children.Add(new Border
        {
            Width = width,
            Height = height,
            BorderBrush = new SolidColorBrush(ThemeColors.Value()) { Opacity = 0.35 },
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
        });
        host.Children.Add(new Rectangle
        {
            Width = width - 2,
            Height = Math.Max(0, (height - 2) * fraction),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            RadiusX = radius - 1,
            RadiusY = radius - 1,
            Fill = new SolidColorBrush(ComponentProgress.FillColor(fraction, ThemeColors.Value(), severity => context.Palette.SeverityColor(severity))),
        });
        return host;
    }

    /// <summary>显示文本：绑定值优先，回退静态 Props 同名键，再回退空串（fail-closed 口径）。</summary>
    private static string DisplayText(string bound, string fallback)
        => !string.IsNullOrEmpty(bound) ? bound : fallback;
}
