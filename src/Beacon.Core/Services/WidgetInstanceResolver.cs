using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 组件实例解析（Phase 3 行 2）：WidgetConfig + 面键 → WidgetInstance。
/// 纯函数、无 IO、fail-closed——无该面树或结构非法返回 null，调用面回传统渲染（同
/// JsonConfigurationStore 加载侧的整树弃用口径）；同一配置重复解析得独立实例。
/// </summary>
public static class WidgetInstanceResolver
{
    public static WidgetInstance? Resolve(WidgetConfig config, string surface)
    {
        if (config.Components is null
            || !config.Components.TryGetValue(surface, out var root)
            || root is null
            || !ComponentTreeValidator.Validate(root).Ok)
        {
            return null;
        }
        return new WidgetInstance
        {
            Id = config.Id,
            Type = config.Type,
            Surface = surface,
            Config = config.Config,
            Root = root,
        };
    }
}
