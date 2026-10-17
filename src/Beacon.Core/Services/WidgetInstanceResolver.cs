using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 组件实例解析（Phase 3 行 2/4）：WidgetConfig + 面键 → WidgetInstance。
/// 纯函数、无 IO、fail-closed——无该面树或结构非法返回 null，调用面回传统渲染（同
/// JsonConfigurationStore 加载侧的整树弃用口径）；附加数据源声明非法时整面弃用附加源
/// （自身状态恒可用），实例不因此弃用。同一配置重复解析得独立实例。
/// Phase 4 裁定（2026-10-17）：根节点为 htmltemplate 载体时在此展开为组件子树——
/// HTML 只是另一种写组件树的语言，tile / row / detail 消费面零改动即获得 HTML 面。
/// </summary>
public static class WidgetInstanceResolver
{
    private static readonly Dictionary<string, string> EmptySources = new();

    public static WidgetInstance? Resolve(WidgetConfig config, string surface)
    {
        if (config.Components is null
            || !config.Components.TryGetValue(surface, out var declared)
            || declared is null)
        {
            return null;
        }
        // Phase 4 裁定：htmltemplate 载体面在解析期展开为组件子树（解析失败 fail-closed null
        // → 传统渲染，与结构非法同一口径）；展开后的树照过防爆校验。
        var expanded = HtmlTemplateParser.ExpandCarrier(declared);
        if (expanded is null || !ComponentTreeValidator.Validate(expanded).Ok)
        {
            return null;
        }
        return new WidgetInstance
        {
            Id = config.Id,
            Type = config.Type,
            Surface = surface,
            Config = config.Config,
            Root = expanded,
            Sources = SurfaceSources(config, surface),
        };
    }

    /// <summary>
    /// 运行时数据源映射（Phase 3 行 4）：自身状态落在保留键 ""，附加源按实例声明的 widgetId
    /// 从状态快照查取；查不到的源缺席（绑定侧 fail-closed 回空串）。纯函数、无 IO——状态由
    /// 调用面以快照传入，改布局只重解析不触发数据请求。
    /// </summary>
    public static IReadOnlyDictionary<string, WidgetState> ResolveSources(
        WidgetInstance instance,
        Func<string, WidgetState?> stateOf)
    {
        var map = new Dictionary<string, WidgetState>(StringComparer.Ordinal);
        foreach (var pair in instance.Sources)
        {
            var state = stateOf(pair.Value);
            if (state is not null)
            {
                map[pair.Key] = state;
            }
        }
        var own = stateOf(instance.Id);
        if (own is not null)
        {
            map[ComponentBindingSource.OwnStateKey] = own;
        }
        return map;
    }

    /// <summary>该面声明的附加数据源：校验通过才纳入，非法整面弃用（只剩自身状态）。</summary>
    private static IReadOnlyDictionary<string, string> SurfaceSources(WidgetConfig config, string surface)
    {
        if (config.Sources is null
            || !config.Sources.TryGetValue(surface, out var declared)
            || declared is null
            || !ComponentSourcesValidator.Validate(declared).Ok)
        {
            return EmptySources;
        }
        return declared;
    }
}
