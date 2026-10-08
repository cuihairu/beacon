using Beacon.Core.Models;
using Microsoft.Extensions.Logging;

namespace Beacon.Core.Services;

/// <summary>Widget → 调度组 的注册项。IntervalOverride：组件级检测间隔覆盖（null=按 Tier 策略表）。</summary>
public sealed record WidgetRegistration(string WidgetId, string ConnectionId, string Tier, TimeSpan? IntervalOverride = null);

/// <summary>同一 (Connection, Tier) 的合并刷新请求（RFC §7.1：不搞 N 个 tile = N 倍请求）。</summary>
public sealed record RefreshRequest(string ConnectionId, string Tier, IReadOnlyList<string> WidgetIds);

/// <summary>
/// 分级刷新调度器（B-204，RFC §7.1）：周期 + 抖动 + 失败指数退避（×2^n 封顶 MaxBackoffMultiplier）+ 成功复位。
/// 组回调抛异常视为整组失败（触发退避），正常返回视为成功；Cancellation 传播停止。
/// InfiniteTimeSpan 级别（static）不自动刷新，仅手动 kick。
/// </summary>
public sealed class RefreshScheduler : IAsyncDisposable
{
    private sealed class GroupState
    {
        public required string ConnectionId { get; init; }
        public required string Tier { get; init; }
        public TimeSpan? IntervalOverride { get; init; } // 同档不同间隔的组件各成组，合并模型在组内仍成立
        public List<string> WidgetIds { get; } = [];
        public int ConsecutiveFailures { get; set; }
        public SemaphoreSlim Kick { get; } = new(0, 1);
        public CancellationTokenSource? Cancellation { get; set; }
        public Task? LoopTask { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<(string ConnectionId, string Tier, TimeSpan? Override), GroupState> _groups = [];
    private readonly Dictionary<string, (string ConnectionId, string Tier, TimeSpan? Override)> _widgetToGroup = [];
    private readonly Func<string, RefreshTierPolicy> _tierPolicy;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly ILogger? _logger;
    private readonly Random _random;
    private Func<RefreshRequest, CancellationToken, Task>? _refreshAsync;
    private bool _running;

    public RefreshScheduler(
        Func<string, RefreshTierPolicy>? tierPolicy = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        ILogger? logger = null,
        int? randomSeed = null)
    {
        _tierPolicy = tierPolicy
            ?? (tier => RefreshTiers.Defaults.GetValueOrDefault(tier, RefreshTiers.Defaults[RefreshTiers.Default]));
        _delay = delay ?? ((interval, cancellationToken) => Task.Delay(interval, cancellationToken));
        _logger = logger;
        _random = randomSeed is null ? Random.Shared : new Random(randomSeed.Value);
    }

    public void Register(WidgetRegistration registration)
    {
        lock (_gate)
        {
            if (_widgetToGroup.ContainsKey(registration.WidgetId))
            {
                UnregisterCore(registration.WidgetId);
            }
            var key = (registration.ConnectionId, registration.Tier, registration.IntervalOverride);
            if (!_groups.TryGetValue(key, out var group))
            {
                group = new GroupState
                {
                    ConnectionId = registration.ConnectionId,
                    Tier = registration.Tier,
                    IntervalOverride = registration.IntervalOverride,
                };
                _groups[key] = group;
            }
            group.WidgetIds.Add(registration.WidgetId);
            _widgetToGroup[registration.WidgetId] = key;
            if (_running && group.LoopTask is null)
            {
                StartGroupCore(group);
            }
        }
    }

    public void Unregister(string widgetId)
    {
        lock (_gate)
        {
            UnregisterCore(widgetId);
        }
    }

    private void UnregisterCore(string widgetId)
    {
        if (!_widgetToGroup.Remove(widgetId, out var key))
        {
            return;
        }
        if (_groups.TryGetValue(key, out var group))
        {
            group.WidgetIds.Remove(widgetId);
            if (group.WidgetIds.Count == 0 && _groups.Remove(key))
            {
                group.Cancellation?.Cancel();
            }
        }
    }

    public void Start(Func<RefreshRequest, CancellationToken, Task> refreshAsync)
    {
        _refreshAsync = refreshAsync;
        lock (_gate)
        {
            _running = true;
            foreach (var group in _groups.Values)
            {
                if (group.LoopTask is null)
                {
                    StartGroupCore(group);
                }
            }
        }
    }

    private void StartGroupCore(GroupState group)
    {
        group.Cancellation = new CancellationTokenSource();
        group.LoopTask = Task.Run(() => RunLoopAsync(group, group.Cancellation.Token));
    }

    /// <summary>停止所有组循环（不注销注册表）。</summary>
    public void Stop()
    {
        Task[] tasks;
        lock (_gate)
        {
            _running = false;
            tasks = _groups.Values
                .Where(g => g.LoopTask is not null)
                .Select(g => { g.Cancellation?.Cancel(); return g.LoopTask!; })
                .ToArray();
        }
        try
        {
            Task.WaitAll(tasks, TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // 停止超时：进程生命周期对象，忽略
        }
        lock (_gate)
        {
            foreach (var group in _groups.Values)
            {
                group.LoopTask = null;
                group.Cancellation = null;
                group.ConsecutiveFailures = 0;
            }
        }
    }

    /// <summary>清空注册表并停止（WidgetHost.Reload 用）。</summary>
    public void Clear()
    {
        Stop();
        lock (_gate)
        {
            _groups.Clear();
            _widgetToGroup.Clear();
        }
    }

    private async Task RunLoopAsync(GroupState group, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            List<string> widgetIds;
            lock (_gate)
            {
                widgetIds = [.. group.WidgetIds];
            }
            if (widgetIds.Count == 0)
            {
                break;
            }
            try
            {
                await WaitAsync(group, NextDelay(group.Tier, group.ConsecutiveFailures, group.IntervalOverride), cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                await _refreshAsync!(new RefreshRequest(group.ConnectionId, group.Tier, widgetIds), cancellationToken)
                    .ConfigureAwait(false);
                lock (_gate)
                {
                    group.ConsecutiveFailures = 0;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    group.ConsecutiveFailures++;
                }
                _logger?.LogWarning(
                    exception,
                    "Refresh group {ConnectionId}/{Tier} failed (streak {Streak}).",
                    group.ConnectionId, group.Tier, group.ConsecutiveFailures);
            }
        }
    }

    private async Task WaitAsync(GroupState group, TimeSpan delay, CancellationToken cancellationToken)
    {
        var delayTask = _delay(delay, cancellationToken);
        var kickTask = group.Kick.WaitAsync(cancellationToken);
        var completed = await Task.WhenAny(delayTask, kickTask).ConfigureAwait(false);
        if (completed == delayTask)
        {
            // delay 先到：吸收挂起的陈旧 kick，避免下次循环被立即误触发
            try
            {
                while (group.Kick.CurrentCount > 0)
                {
                    group.Kick.Wait(TimeSpan.Zero);
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }
        await completed.ConfigureAwait(false); // 传播取消/完成
    }

    /// <summary>
    /// 下一次刷新延迟：周期 × 退避倍数 ± 抖动；static/infinite 级别返回 Infinite。
    /// intervalOverride 优先于档位策略表（组件级「检测间隔」），退避/抖动参数同档位默认。
    /// </summary>
    internal TimeSpan NextDelay(string tier, int consecutiveFailures, TimeSpan? intervalOverride = null)
    {
        var policy = intervalOverride is { } interval
            ? new RefreshTierPolicy(interval)
            : _tierPolicy(tier);
        if (policy.Interval <= TimeSpan.Zero || policy.Interval == Timeout.InfiniteTimeSpan)
        {
            return Timeout.InfiniteTimeSpan;
        }
        var multiplier = Math.Clamp(
            (int)Math.Pow(2, Math.Max(0, consecutiveFailures)),
            1,
            Math.Max(1, policy.MaxBackoffMultiplier));
        var effective = policy.Interval.TotalMilliseconds * multiplier;
        var jitter = Math.Clamp(policy.JitterFraction, 0, 0.9);
        var low = effective * (1 - jitter);
        var high = effective * (1 + jitter);
        return TimeSpan.FromMilliseconds(Math.Max(50, low + _random.NextDouble() * (high - low)));
    }

    /// <summary>手动刷新：唤醒分组循环立即拉取（退避不重置，成功后自然复位）。</summary>
    public void KickWidget(string widgetId)
    {
        lock (_gate)
        {
            if (_widgetToGroup.TryGetValue(widgetId, out var key)
                && _groups.TryGetValue(key, out var group)
                && group.Kick.CurrentCount == 0)
            {
                group.Kick.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        lock (_gate)
        {
            foreach (var group in _groups.Values)
            {
                group.Kick.Dispose();
            }
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
