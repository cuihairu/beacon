using System.Globalization;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// IStateStore 进程内实现（Phase 1，映射与迁移 §4.1；契约冻结见 docs/design/redesign-2026-10-16-契约.md）。
/// 内容键排除 FetchedAt：仅刷新时间变化不算内容变化；IsStale/ConnectionHealthy 翻转仍算变化
/// （断网降级/陈旧事件不得被去重吞掉，场景 F5）。默认 suppressUnchanged=true（PATROL1016 已代决②：
/// 设计稿 §4.1「状态相同的轮询结果不应导致所有组件重新绘制」；false 为回退开关，单 commit 可整体回滚）。
/// </summary>
public sealed class StateStore : IStateStore
{
    private readonly IEventBus _bus;
    private readonly bool _suppressUnchanged;
    private readonly object _gate = new();
    private readonly Dictionary<string, WidgetState> _states = [];
    private readonly Dictionary<string, string> _contentKeys = [];

    public StateStore(IEventBus bus, bool suppressUnchanged = true)
    {
        _bus = bus;
        _suppressUnchanged = suppressUnchanged;
    }

    public void Update(WidgetState state)
    {
        var key = ContentKey(state);
        var publish = true;
        lock (_gate)
        {
            if (_suppressUnchanged && _contentKeys.TryGetValue(state.WidgetId, out var previous) && previous == key)
            {
                publish = false; // 内容未变：快照仍更新（FetchedAt 保最新），只是不重发
            }
            _states[state.WidgetId] = state;
            _contentKeys[state.WidgetId] = key;
        }
        if (publish)
        {
            _bus.Publish(new WidgetStateChanged(state));
        }
    }

    public WidgetState? Get(string widgetId)
    {
        lock (_gate)
        {
            return _states.GetValueOrDefault(widgetId);
        }
    }

    public IReadOnlyDictionary<string, WidgetState> Snapshot()
    {
        lock (_gate)
        {
            return new Dictionary<string, WidgetState>(_states);
        }
    }

    public void Invalidate(IReadOnlyList<string> widgetIds)
    {
        lock (_gate)
        {
            foreach (var widgetId in widgetIds)
            {
                _states.Remove(widgetId);
                _contentKeys.Remove(widgetId);
            }
        }
    }

    /// <summary>内容键：除 FetchedAt 外全字段；字符串段带长度前缀避免分隔符歧义，Payload 按键排序（插入顺序无关）。</summary>
    internal static string ContentKey(WidgetState state)
    {
        var payload = string.Join(";", state.Payload
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{Segment(pair.Key)}={Segment(pair.Value)}"));
        return string.Join("\n",
            Segment(state.WidgetType),
            Segment(state.ConnectionId),
            ((int)state.Severity).ToString(CultureInfo.InvariantCulture),
            state.Lifecycle is { } lifecycle ? ((int)lifecycle).ToString(CultureInfo.InvariantCulture) : "?",
            state.Progress?.ToString("R", CultureInfo.InvariantCulture) ?? "?",
            Segment(state.Summary),
            Segment(state.DetailUrl ?? string.Empty),
            state.IsStale ? "1" : "0",
            state.ConnectionHealthy ? "1" : "0",
            Segment(payload));
    }

    private static string Segment(string value) => $"{value.Length}:{value}";
}
