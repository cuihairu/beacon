using System.Globalization;
using Beacon.App.Services;
using Beacon.App.Windows;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Components;

/// <summary>
/// 声明式组件渲染器（P3-01，契约 §3.5-10 渲染侧注册表）：ComponentDescriptor 树 → WinUI 可视树。
/// 节点类型键在本类注册表解析（契约已代决⑤：类型词汇归渲染侧、不进 Core）；绑定值经
/// ComponentBindingEngine.ResolveBinding 按状态求值（fail-closed 空串，不猜）；
/// Props 字符串换算（数值/枚举）失败回默认值——非法声明不炸常驻进程。
/// 结构合法性由 WidgetInstanceResolver.Resolve 前置保证（非法树拿不到实例，面回传统渲染），
/// 本类不再重复校验深度/节点上限。未知类型/渲染异常出诊断占位（可见可查，不静默吞）；
/// 状态每次变化整树重渲（树 ≤256 节点、无跨帧局部状态，重渲即「改布局只重解析」的运行时同款）。
/// </summary>
internal static class ComponentSurfaceRenderer
{
    /// <summary>渲染节点上下文：注册表工厂只从这里取数据（节点 + 自身状态 + 配色）。</summary>
    private sealed class NodeContext(ComponentDescriptor node, WidgetState? state, UiPalette palette)
    {
        public ComponentDescriptor Node { get; } = node;
        public WidgetState? State { get; } = state;
        public UiPalette Palette { get; } = palette;

        /// <summary>绑定槽取值（fail-closed 空串）：只看本节点绑定，不猜别的槽。</summary>
        public string Binding(string field)
        {
            var binding = Node.Bindings.FirstOrDefault(b =>
                b is not null && string.Equals(b.Field, field, StringComparison.OrdinalIgnoreCase));
            return binding is null ? "" : ComponentBindingEngine.ResolveBinding(binding, State);
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
    }

    private delegate FrameworkElement NodeFactory(NodeContext context);

    /// <summary>类型注册表（渲染侧唯一事实来源）：类型键 → 工厂。容器挂子见 RenderNode（row/column 面板、card 单卡）。</summary>
    private static readonly Dictionary<string, NodeFactory> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["row"] = context => ComponentLayout.Row(context.PropDouble("spacing", DesignTokens.Space2)),
        ["column"] = context => ComponentLayout.Column(context.PropDouble("spacing", DesignTokens.Space2)),
        ["card"] = _ => new Grid(), // 占位：有子节点由 RenderNode 换装 Card（单卡面）；无子=空 Grid
        ["text"] = context =>
        {
            var text = DisplayText(context.Binding("text"), context.Prop("text"));
            return string.Equals(context.Prop("size"), "caption", StringComparison.OrdinalIgnoreCase)
                ? ComponentText.Caption(text, context.Foreground)
                : ComponentText.Body(text, context.Foreground);
        },
        ["sectionlabel"] = context =>
            ComponentLayout.SectionLabel(DisplayText(context.Binding("text"), context.Prop("text")), context.Foreground),
        ["metric"] = context => ComponentMetric.Value(
            DisplayText(context.Binding("value"), context.Prop("value")),
            context.Node.Props.TryGetValue("unit", out var unit) && !string.IsNullOrWhiteSpace(unit) ? unit : null,
            context.Foreground,
            context.PropDouble("fontSize", DesignTokens.FontHeader)),
        ["progress"] = Progress,
        ["statusdot"] = context => ComponentIcon.StatusDot(
            context.SeverityBrush(DisplayText(context.Binding("severity"), context.Prop("severity"))),
            context.PropDouble("size", 8)),
        ["badge"] = context => ComponentBadge.Count(
            new TextBlock
            {
                Text = DisplayText(context.Binding("text"), context.Prop("text")),
                Foreground = context.Foreground,
            },
            new SolidColorBrush(context.Palette.SeverityColor(Severity.Info))),
        ["keyvalue"] = context => ComponentKeyValue.Row(
            context.Prop("key"),
            DisplayText(context.Binding("value"), context.Prop("value")),
            context.Foreground),
        ["divider"] = context => ComponentDivider.Horizontal(context.Foreground),
    };

    /// <summary>整树渲染。tile 首切片只接自身状态源（附加源映射属 P3-03 依赖路由）。</summary>
    public static FrameworkElement Render(WidgetInstance instance, WidgetState? state, UiPalette palette)
        => RenderNode(instance.Root, state, palette);

    private static FrameworkElement RenderNode(ComponentDescriptor node, WidgetState? state, UiPalette palette)
    {
        if (!Registry.TryGetValue(node.Type, out var factory))
        {
            // 未知类型：诊断占位可见可查（P3-01：定位到面的诊断），不静默吞也不炸
            return ComponentText.Caption($"⚠ 未注册组件类型：{node.Type}", new SolidColorBrush(ThemeColors.Label()));
        }

        FrameworkElement element;
        try
        {
            element = factory(new NodeContext(node, state, palette));
        }
        catch (Exception)
        {
            // 属性换算/工厂异常按坏节点处理：占位诊断顶住，兄弟节点照常渲染
            return ComponentText.Caption($"⚠ 组件 {node.Type} 渲染失败", new SolidColorBrush(ThemeColors.Label()));
        }

        // card 单卡面（契约允许 Children，卡语义单内容）：首子进 Card，其余忽略
        if (element is Grid && string.Equals(node.Type, "card", StringComparison.OrdinalIgnoreCase))
        {
            var first = node.Children.FirstOrDefault(c => c is not null);
            element = ComponentSurface.Card(
                first is null ? new Grid() : RenderNode(first, state, palette),
                new SolidColorBrush(ThemeColors.Surface()));
        }
        else if (element is Panel panel)
        {
            foreach (var child in node.Children)
            {
                if (child is not null)
                {
                    panel.Children.Add(RenderNode(child, state, palette));
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

    /// <summary>进度条：绑定槽 progress（0..1，越界钳位）优先，回退静态 Props.progress；width 默认 96 DIP。</summary>
    /// <summary>注册类型键（设置页可视化树编辑器的类型词汇单源——渲染侧已知什么才允许声明什么）。
    /// 必须在 Registry 字段初始化之后取值（静态初始化按文本序）。</summary>
    public static IReadOnlyList<string> KnownTypes { get; } = Registry.Keys.Order().ToArray();

    private static FrameworkElement Progress(NodeContext context)
    {
        var fraction = double.TryParse(context.Binding("progress"), CultureInfo.InvariantCulture, out var bound)
            ? Math.Clamp(bound, 0, 1)
            : Math.Clamp(context.PropDouble("progress", 0), 0, 1);
        var (track, fill) = ComponentProgress.Bar(fraction, context.PropDouble("width", 96));
        var host = new Grid();
        host.Children.Add(track);
        host.Children.Add(fill);
        return host;
    }

    /// <summary>显示文本：绑定值优先，回退静态 Props 同名键，再回退空串（fail-closed 口径）。</summary>
    private static string DisplayText(string bound, string fallback)
        => !string.IsNullOrEmpty(bound) ? bound : fallback;
}
