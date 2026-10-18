using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 展示模板注册表（2026-10-18 外部评审 P1）：「数据显示成什么样」的成品样式单源。
/// 每个模板 = 一份声明式树生成器（纯函数，动态值全部经绑定注入，状态到了才显值——
/// fail-closed 口径与渲染链一致）+ 能力门槛 RequiresProgress（无百分比的数据不给虚假的
/// 确定进度，评审 §四）。类型词汇只用渲染注册表已知键（ring / pool 与本批 App 侧工厂
/// 同落地）；百分比数值取数走 percent 选择器（ComponentBindingEngine）。
/// 高级组合（手工拼树）不经过本表——树编辑器口径不变（评审 P2）。
/// </summary>
public static class DisplayTemplates
{
    private const string State = ComponentBindingSource.KindState;
    private const string Selector = ComponentBindingSource.KindSelector;

    /// <summary>模板声明：Id 稳定键 / Name 中文名 / Desc 一句话说明 / RequiresProgress 能力门槛 /
    /// Build 生成树根（每次调用新实例，调用方可改可存，避免多面共用引用）。</summary>
    public sealed record DisplayTemplate(string Id, string Name, string Desc, bool RequiresProgress, Func<ComponentDescriptor> Build);

    public static IReadOnlyList<DisplayTemplate> All { get; } =
    [
        new("bar", "横条", "摘要一行 + 底部进度条（默认档）", true, static () => Group("column", Props(("gap", "sm")),
            Leaf("text", null, Bind("text", State, "summary")),
            Leaf("progress", Props(("width", "220")), Bind("progress", State, "progress")))),
        new("ring", "圆环", "环形进度 + 摘要一行", true, static () => Group("row", Props(("gap", "md")),
            Leaf("ring", Props(("size", "30"), ("thickness", "3")), Bind("progress", State, "progress")),
            Leaf("text", null, Bind("text", State, "summary")))),
        new("number", "数字卡", "百分比大数字 + 摘要（percent 选择器取数）", true, static () => Group("column", Props(("gap", "sm")),
            Leaf("metric", Props(("unit", "%"), ("fontSize", "22")), Bind("value", Selector, "percent")),
            Leaf("text", Props(("size", "caption")), Bind("text", State, "summary")))),
        new("pool", "水位", "圆池注水 + 摘要一行", true, static () => Group("row", Props(("gap", "md")),
            Leaf("pool", Props(("width", "30"), ("height", "30")), Bind("progress", State, "progress")),
            Leaf("text", null, Bind("text", State, "summary")))),
        new("statusline", "状态行", "状态灯 + 摘要（无百分比数据也兼容）", false, static () => Group("row", Props(("gap", "sm")),
            Leaf("statusdot", Props(("size", "10")), Bind("severity", State, "severity")),
            Leaf("text", null, Bind("text", State, "summary")))),
    ];

    /// <summary>能力筛选（评审 P1）：无状态或无百分比 → 只给不要求进度的模板，
    /// 不给虚假的确定进度样式（进度值不可靠的数据源不显假数字）。</summary>
    public static IReadOnlyList<DisplayTemplate> Compatible(WidgetState? state)
        => All.Where(template => !template.RequiresProgress || state is { Progress: { } }).ToArray();

    /// <summary>取模板定义（未知 Id 返回空列表，调用方按空处理）。</summary>
    public static IReadOnlyList<DisplayTemplate> Match(string id)
        => All.Where(template => template.Id == id).ToArray();

    /// <summary>叶子节点：无子件；绑定参数即该叶子的取数槽。</summary>
    private static ComponentDescriptor Leaf(string type, Dictionary<string, string>? props = null, params ComponentBinding[] bindings)
        => new()
        {
            Type = type,
            Props = props ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Bindings = [.. bindings],
        };

    /// <summary>容器节点：子件按声明序排列，容器自身不声明绑定槽。</summary>
    private static ComponentDescriptor Group(string type, Dictionary<string, string>? props, params ComponentDescriptor[] children)
        => new()
        {
            Type = type,
            Props = props ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Children = [.. children],
        };

    private static Dictionary<string, string> Props(params (string Key, string Value)[] items)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in items)
        {
            map[key] = value;
        }
        return map;
    }

    private static ComponentBinding Bind(string field, string kind, string reference)
        => new()
        {
            Field = field,
            Source = new ComponentBindingSource { Kind = kind, Ref = reference },
        };
}
