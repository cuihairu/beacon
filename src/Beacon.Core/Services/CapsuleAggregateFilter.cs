using Beacon.Core.Events;
using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 胶囊/岛消费面的组件中心过滤（ISLAND-RING-1 扩围①，2026-11-07 用户令「岛接上组件选择」）：
/// hiddenComponents 勾掉的组件不再给岛点灯/计徽标/撑进度环——此前该配置只被设置页/浮动 tile/
/// 快捷面板消费，岛 CapsuleWindow 零命中，勾掉的组件仍在给岛报状态。纯函数：输入聚合快照与
/// 最近组件状态集（App 侧 lastStates），输出岛口径的过滤快照；没有任何组件状态可判（冷启动
/// 首帧）时原样透传聚合器快照，不造数不回退。离线横幅按「仍持有可见组件的离线连接」计数
/// （按连接去重，与 StatusAggregator._unhealthyConnections.Count 同口径）——唯一组件被勾掉的
/// 离线连接不再点亮横幅。持久化与真实组件同一 config（AppConfig.HiddenComponents，Core 单源）。
/// </summary>
public static class CapsuleAggregateFilter
{
    /// <summary>过滤：states 为空（冷启动无可判状态）→ 原样透传；否则按可见组件重算
    /// 分级计数/总体级别/离线连接数（Timestamp 沿用聚合快照）。</summary>
    public static AggregateStatusChanged Filter(
        AppConfig app,
        AggregateStatusChanged aggregate,
        IReadOnlyCollection<WidgetState> states)
    {
        if (states.Count == 0)
        {
            return aggregate; // 冷启动无状态可判：不造数（透传同实例，消费方可引用比较）
        }

        var counts = new Dictionary<Severity, int>();
        var overall = Severity.Success;
        var offlineConnections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in states)
        {
            if (ComponentCenterCatalog.IsHidden(app, state.WidgetId))
            {
                continue; // 组件中心勾掉：不进岛的任何消费面（灯/徽标/环/条/横幅/预览）
            }

            counts[state.Severity] = counts.GetValueOrDefault(state.Severity) + 1;
            overall = overall.Max(state.Severity);
            if (!state.ConnectionHealthy)
            {
                offlineConnections.Add(state.ConnectionId);
            }
        }

        foreach (Severity severity in Enum.GetValues<Severity>())
        {
            counts.TryAdd(severity, 0);
        }

        return new AggregateStatusChanged(overall, counts, offlineConnections.Count, aggregate.Timestamp);
    }
}
