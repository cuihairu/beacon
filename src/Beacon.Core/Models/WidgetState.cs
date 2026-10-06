namespace Beacon.Core.Models;

/// <summary>Widget 运行时状态：四层（L0~L3）共享同一份状态渲染（RFC §4.3 / §6.6）。</summary>
public sealed record WidgetState
{
    public required string WidgetId { get; init; }
    public required string WidgetType { get; init; }
    public required string ConnectionId { get; init; }
    public required Severity Severity { get; init; }
    public required string Summary { get; init; }
    /// <summary>深链（如 GitHub run 页面），供 Open 动作使用。</summary>
    public string? DetailUrl { get; init; }
    /// <summary>0..1 归一化进度（构建进度等），无进度为 null。</summary>
    public double? Progress { get; init; }
    public LifecycleState? Lifecycle { get; init; }
    /// <summary>提供方附加数据（run_id 等），供 Action 模板变量使用。</summary>
    public IReadOnlyDictionary<string, string> Payload { get; init; } = new Dictionary<string, string>();
    public DateTimeOffset FetchedAt { get; init; }
    /// <summary>数据过期（离线/连续失败），UI 显示 Last update 而不甩异常（RFC §6.2.6 / §12）。</summary>
    public bool IsStale { get; init; }
    public bool ConnectionHealthy { get; init; } = true;

    /// <summary>内容相等（Payload 字典按内容比较）——供事件/聚合去重使用。</summary>
    public bool Equals(WidgetState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return WidgetId == other.WidgetId
            && WidgetType == other.WidgetType
            && ConnectionId == other.ConnectionId
            && Severity == other.Severity
            && Summary == other.Summary
            && DetailUrl == other.DetailUrl
            && Nullable.Equals(Progress, other.Progress)
            && Lifecycle == other.Lifecycle
            && FetchedAt == other.FetchedAt
            && IsStale == other.IsStale
            && ConnectionHealthy == other.ConnectionHealthy
            && DictionaryEquals(Payload, other.Payload);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(WidgetId);
        hash.Add(WidgetType);
        hash.Add(ConnectionId);
        hash.Add(Severity);
        hash.Add(Summary);
        hash.Add(DetailUrl);
        hash.Add(Progress);
        hash.Add(Lifecycle);
        hash.Add(FetchedAt);
        hash.Add(IsStale);
        hash.Add(ConnectionHealthy);
        hash.Add(Payload.Count);
        return hash.ToHashCode();
    }

    private static bool DictionaryEquals(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count) return false;
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue) || value != otherValue) return false;
        }
        return true;
    }
}
