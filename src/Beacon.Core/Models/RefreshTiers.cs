namespace Beacon.Core.Models;

/// <summary>单个刷新级别的策略：基础周期 + 抖动 + 失败退避上限（RFC §7.1）。</summary>
public sealed record RefreshTierPolicy(TimeSpan Interval, double JitterFraction = 0.2, int MaxBackoffMultiplier = 10)
{
    /// <summary>手动级别：不自动刷新，仅手动/事件触发。</summary>
    public static RefreshTierPolicy Manual { get; } = new(Timeout.InfiniteTimeSpan);
}

/// <summary>分级刷新（RFC §7.1 唯一策略表）：L0/L1 不引入新频率，只复用这里。</summary>
public static class RefreshTiers
{
    public const string Pr = "pr";
    public const string Ci = "ci";
    public const string Machine = "machine";
    public const string Agent = "agent";
    public const string Workflow = "workflow";
    public const string Static = "static";
    public const string Default = "default";

    public static readonly IReadOnlyDictionary<string, RefreshTierPolicy> Defaults =
        new Dictionary<string, RefreshTierPolicy>(StringComparer.OrdinalIgnoreCase)
        {
            [Pr] = new(TimeSpan.FromSeconds(60)),       // GitHub PR 30~120s
            [Ci] = new(TimeSpan.FromSeconds(15)),       // CI 10~30s
            [Machine] = new(TimeSpan.FromSeconds(15)),  // Machine 5~30s
            [Agent] = new(TimeSpan.FromSeconds(10)),    // Agent 5~15s
            [Workflow] = new(TimeSpan.FromSeconds(10)), // Workflow 事件/10s
            [Static] = RefreshTierPolicy.Manual,
            [Default] = new(TimeSpan.FromSeconds(60)),
        };
}
