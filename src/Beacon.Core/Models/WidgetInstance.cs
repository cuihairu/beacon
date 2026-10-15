namespace Beacon.Core.Models;

/// <summary>
/// 组件实例（Phase 3 行 2）：组件配置在某个渲染面上解析出的装配结果——树 + 静态配置的唯一读面。
/// App 渲染段与 Core 绑定引擎都只读它（各自再叠加状态/选择器）；解析是纯函数、无 IO，
/// 改布局只重解析实例，不触发 Provider 请求（Phase 3 行 6 不变量）。
/// Root 与 Config 指向配置内的原对象（同一配置重复解析共享同一棵树）——按约定只读共享，
/// 与 WidgetConfig.Config / ActionConfig.Parameters 字典同口径，不复制。
/// </summary>
public sealed record WidgetInstance
{
    /// <summary>组件配置 Id（同 WidgetConfig.Id）。</summary>
    public required string Id { get; init; }

    /// <summary>组件类型键（同 WidgetConfig.Type，23 个既有类型键不动）。</summary>
    public required string Type { get; init; }

    /// <summary>渲染面键（tile / row / detail，面键开集）。</summary>
    public required string Surface { get; init; }

    /// <summary>组件静态配置（WidgetConfig.Config 直通，供 selector 取参与 Action 模板变量）。</summary>
    public required IReadOnlyDictionary<string, string> Config { get; init; }

    /// <summary>该面组件树根。</summary>
    public required ComponentDescriptor Root { get; init; }
}
