namespace Beacon.Core.Models;

/// <summary>
/// 绑定源（Phase 3 行 2，契约.md §4 已代决⑥）：组件属性从哪取值。
/// state = 按路径取当前状态字段（summary/severity/progress/detailUrl/lifecycle/connectionId/
/// widgetId/widgetType）或 payload 键（payload:run_id）；selector = 按选择器注册表名取派生值；
/// const = 静态字面值（Args 的 value）。SourceName 指向实例的附加数据源（Phase 3 行 4）。
/// </summary>
public sealed record ComponentBindingSource
{
    public const string KindState = "state";
    public const string KindSelector = "selector";
    public const string KindConst = "const";

    /// <summary>
    /// 源名映射的保留键：实例自身状态。附加数据源声明不得使用（ComponentSourcesValidator
    /// 拒绝），所以空 SourceName = 自身状态这一约定不会被声明撞掉。
    /// </summary>
    public const string OwnStateKey = "";

    /// <summary>source 取值来源键（state 路径 / selector 注册表名 / const 留空）。</summary>
    public string Ref { get; init; } = "";

    /// <summary>源类型：state / selector / const。</summary>
    public string Kind { get; init; } = KindState;

    /// <summary>附加参数：const 字面值（Args["value"]）、selector 取参；可空。</summary>
    public Dictionary<string, string>? Args { get; init; }

    /// <summary>附加数据源名（指向 WidgetInstance.Sources 的源名 → widgetId）；空 = 本组件自身状态（OwnStateKey）。</summary>
    public string SourceName { get; init; } = OwnStateKey;
}

/// <summary>
/// 组件绑定声明（随节点进树，Phase 3 行 2）：把绑定源的值填进所属节点的属性槽 Field。
/// </summary>
public sealed record ComponentBinding
{
    /// <summary>目标属性槽（所属节点的 Props 键，如 text / value / unit）。</summary>
    public string Field { get; init; } = "";

    /// <summary>取值来源。</summary>
    public ComponentBindingSource Source { get; init; } = new();
}
