using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 组件消费索引（Phase 3 行 5）：一份状态被哪些组件消费、每个组件消费哪些状态。
/// 同一份 WidgetState 对象可被任意多个实例消费（解析是纯函数、按引用共享不复制），
/// 本索引把这种共享变成可反查的事实，供两个方向使用：
/// ① widgetId → 消费该状态的实例集合——数据变化只更新必要的消费者（设计稿验收 7）；
/// ② 实例 → 它需要的 widgetId 全集（自身 Id + Sources 声明的附加源）——订阅与刷新只拉必要的源。
/// 纯函数、无 IO：不改布局、不触发数据请求。
/// </summary>
public static class ComponentSourceIndex
{
    /// <summary>该实例消费的全部 widgetId（自身在前，然后按 Sources 声明顺序；同一 widgetId 去重）。</summary>
    public static IReadOnlyList<string> ConsumedWidgetIds(WidgetInstance instance)
    {
        var ids = new List<string> { instance.Id };
        foreach (var pair in instance.Sources)
        {
            if (!ids.Contains(pair.Value))
            {
                ids.Add(pair.Value);
            }
        }
        return ids;
    }

    /// <summary>widgetId → 消费它的实例集合（同一实例消费多份状态时在每个键下各出现一次）。</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<WidgetInstance>> SourcesOf(
        IEnumerable<WidgetInstance> instances)
    {
        var index = new Dictionary<string, IReadOnlyList<WidgetInstance>>(StringComparer.Ordinal);
        var buffers = new Dictionary<string, List<WidgetInstance>>(StringComparer.Ordinal);
        foreach (var instance in instances)
        {
            foreach (var widgetId in ConsumedWidgetIds(instance))
            {
                if (!buffers.TryGetValue(widgetId, out var consumers))
                {
                    buffers[widgetId] = consumers = [];
                    index[widgetId] = consumers;
                }
                consumers.Add(instance);
            }
        }
        return index;
    }
}
