using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>
/// 进程内统一状态入口（组件化重构 Phase 1，redesign-plan §4.1）：按 WidgetId 存最新状态，
/// 内容级变更检测后经 IEventBus 发布 WidgetStateChanged。订阅与取消订阅沿用事件总线
/// （<c>Subscribe&lt;WidgetStateChanged&gt;</c>），事件契约不变，UI 无感。
/// </summary>
public interface IStateStore
{
    /// <summary>写入最新状态：快照始终更新；内容未变时是否重发由实现方决定（见 <see cref="Services.StateStore"/>）。</summary>
    void Update(WidgetState state);

    /// <summary>单组件最新状态；无则 null。</summary>
    WidgetState? Get(string widgetId);

    /// <summary>当前快照（widgetId → 最新状态）；线程安全，返回副本。</summary>
    IReadOnlyDictionary<string, WidgetState> Snapshot();

    /// <summary>按组件失效丢弃快照（模块停用/删除，配合 WidgetsInvalidated）；不发布事件。</summary>
    void Invalidate(IReadOnlyList<string> widgetIds);
}
