using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>L2 Quick Panel 当前状态的三分区归属（master-plan §3.2，2026-10-11 拍板④落地）。</summary>
public enum QuickPanelSection
{
    /// <summary>不单独成区：由 OVERVIEW 计数与 RECENT EVENTS 事件流承载。</summary>
    Normal = 0,

    /// <summary>正在运行（Running/Queued）。</summary>
    InProgress = 1,

    /// <summary>需要关注（失败/超时/离线/数据过期）。</summary>
    NeedsAttention = 2,
}

/// <summary>
/// 三分区映射（纯函数供单测）：需要关注 = Warning 及以上 / 数据过期 / 连接断——「运行超时」在 RFC §4.1 就是
/// Warning 语义，Yellow = 胶囊「需要注意」同口径；正在运行 = Running/Queued 且无告警；
/// 其余（成功/信息/无生命周期）不单独成区。分区只看当前状态，与事件流正交，同一 Widget 只落一个区。
/// </summary>
public static class QuickPanelSections
{
    public static QuickPanelSection Resolve(WidgetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Severity >= Severity.Warning || state.IsStale || !state.ConnectionHealthy)
        {
            return QuickPanelSection.NeedsAttention;
        }
        return state.Lifecycle is LifecycleState.Running or LifecycleState.Queued
            ? QuickPanelSection.InProgress
            : QuickPanelSection.Normal;
    }
}
