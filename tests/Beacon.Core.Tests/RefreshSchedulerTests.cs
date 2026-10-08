using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-204 验收：周期/±20% 抖动边界/×2^n 退避封顶/成功复位/(connection,tier) 合并/手动 kick（模拟时钟）。</summary>
public sealed class RefreshSchedulerTests
{
    private static RefreshScheduler Create(FakeDelayer delayer, Func<string, RefreshTierPolicy>? policy = null)
        => new(policy ?? (_ => new RefreshTierPolicy(TimeSpan.FromMilliseconds(1000))), delayer.Delay, randomSeed: 42);

    // ---- NextDelay（internal，经 InternalsVisibleTo 直接测）----

    [Fact]
    public void NextDelay_RespectsJitterBounds()
    {
        var scheduler = new RefreshScheduler(
            _ => new RefreshTierPolicy(TimeSpan.FromSeconds(15), JitterFraction: 0.2),
            randomSeed: 7);

        foreach (var failures in new[] { 0, 1, 3 })
        {
            var baseMilliseconds = 15_000d * Math.Pow(2, failures);
            for (var sample = 0; sample < 100; sample++)
            {
                var delay = scheduler.NextDelay(RefreshTiers.Ci, failures);
                Assert.InRange(delay.TotalMilliseconds, baseMilliseconds * 0.8, baseMilliseconds * 1.2);
            }
        }
    }

    [Fact]
    public void NextDelay_ExponentialBackoffGrowth()
    {
        var scheduler = new RefreshScheduler(_ => new RefreshTierPolicy(TimeSpan.FromSeconds(1), JitterFraction: 0));

        Assert.Equal(TimeSpan.FromSeconds(1), scheduler.NextDelay(RefreshTiers.Ci, 0));
        Assert.Equal(TimeSpan.FromSeconds(2), scheduler.NextDelay(RefreshTiers.Ci, 1));
        Assert.Equal(TimeSpan.FromSeconds(4), scheduler.NextDelay(RefreshTiers.Ci, 2));
        Assert.Equal(TimeSpan.FromSeconds(8), scheduler.NextDelay(RefreshTiers.Ci, 3));
    }

    [Fact]
    public void NextDelay_CappedAtMaxBackoffMultiplier()
    {
        var scheduler = new RefreshScheduler(
            _ => new RefreshTierPolicy(TimeSpan.FromSeconds(1), JitterFraction: 0, MaxBackoffMultiplier: 4));

        Assert.Equal(TimeSpan.FromSeconds(4), scheduler.NextDelay(RefreshTiers.Ci, 2));
        Assert.Equal(TimeSpan.FromSeconds(4), scheduler.NextDelay(RefreshTiers.Ci, 10)); // 封顶，不无限增长
    }

    [Fact]
    public void NextDelay_IntervalOverride_BeatsTierPolicy()
    {
        // 检测间隔覆盖（设置页「检测间隔」）：组件级秒数优先于档位表（档位表给 1s），抖动默认 0.2
        var scheduler = new RefreshScheduler(
            _ => new RefreshTierPolicy(TimeSpan.FromSeconds(1), JitterFraction: 0));

        var delay = scheduler.NextDelay(RefreshTiers.Ci, 0, TimeSpan.FromSeconds(60));
        Assert.InRange(delay.TotalSeconds, 48, 72); // 60s ±20% 抖动
        // 退避仍作用于覆盖间隔：×2^n
        var backed = scheduler.NextDelay(RefreshTiers.Ci, 1, TimeSpan.FromSeconds(60));
        Assert.InRange(backed.TotalSeconds, 96, 144); // 120s ±20%
        // null 回落档位表（tier 表 JitterFraction: 0 → 精确值）
        Assert.Equal(TimeSpan.FromSeconds(1), scheduler.NextDelay(RefreshTiers.Ci, 0, null));
    }

    [Fact]
    public void NextDelay_IntervalOverride_ManualTierWithOverride_PollsAtInterval()
    {
        // static 档（Manual/infinite）被组件间隔覆盖后按间隔轮询——覆盖是显式意图，优先于「不自动刷新」
        var scheduler = new RefreshScheduler(_ => RefreshTierPolicy.Manual, randomSeed: 3);

        var delay = scheduler.NextDelay(RefreshTiers.Static, 0, TimeSpan.FromSeconds(30));
        Assert.InRange(delay.TotalSeconds, 24, 36); // 30s ±20% 抖动
    }

    [Fact]
    public void NextDelay_ManualTier_ReturnsInfinite()
    {
        var scheduler = new RefreshScheduler(tier => tier == RefreshTiers.Static
            ? RefreshTierPolicy.Manual
            : new RefreshTierPolicy(TimeSpan.FromSeconds(1)));

        Assert.Equal(Timeout.InfiniteTimeSpan, scheduler.NextDelay(RefreshTiers.Static, 0));
        Assert.Equal(Timeout.InfiniteTimeSpan, scheduler.NextDelay(RefreshTiers.Static, 5));
    }

    // ---- 行为（FakeDelayer 模拟时钟）----

    [Fact]
    public async Task SameConnectionTier_MergedIntoSingleGroup()
    {
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer);
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));
        scheduler.Register(new WidgetRegistration("w-2", "conn-1", RefreshTiers.Ci));

        var requests = new List<RefreshRequest>();
        var gate = new object();
        scheduler.Start((request, _) => { lock (gate) requests.Add(request); return Task.CompletedTask; });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        delayer.Calls[0].Complete();
        await Poll.UntilAsync(() => requests.Count >= 1);

        Assert.Single(requests);
        Assert.Equal(["w-1", "w-2"], requests[0].WidgetIds);
    }

    [Fact]
    public async Task DifferentTiers_FormSeparateGroups()
    {
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer);
        scheduler.Register(new WidgetRegistration("w-ci", "conn-1", RefreshTiers.Ci));
        scheduler.Register(new WidgetRegistration("w-pr", "conn-1", RefreshTiers.Pr));

        var requests = new List<RefreshRequest>();
        var gate = new object();
        scheduler.Start((request, _) => { lock (gate) requests.Add(request); return Task.CompletedTask; });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 2);
        delayer.Calls[0].Complete();
        delayer.Calls[1].Complete();
        await Poll.UntilAsync(() => requests.Count >= 2);

        Assert.All(requests, request => Assert.Single(request.WidgetIds));
    }

    [Fact]
    public async Task RegisterWhileRunning_StartsNewGroupLoop()
    {
        // 白板根因守卫：组件在调度器已运行时注册（设置页新增组件）→ 新组循环须立即启动，不等重启
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer);
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));

        var requests = new List<RefreshRequest>();
        var gate = new object();
        scheduler.Start((request, _) => { lock (gate) requests.Add(request); return Task.CompletedTask; });
        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);

        scheduler.Register(new WidgetRegistration("w-2", "conn-2", RefreshTiers.Ci)); // 不同 (connection,tier) 新组
        await Poll.UntilAsync(() => delayer.Calls.Count >= 2);

        Assert.Equal(2, delayer.Calls.Count);
    }

    [Fact]
    public async Task Kick_WakesGroupBeforeDelayElapses()
    {
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer, _ => new RefreshTierPolicy(TimeSpan.FromSeconds(60)));
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));

        var refreshCount = 0;
        scheduler.Start((_, _) => { Interlocked.Increment(ref refreshCount); return Task.CompletedTask; });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        scheduler.KickWidget("w-1");
        await Poll.UntilAsync(() => Volatile.Read(ref refreshCount) >= 1);

        Assert.False(delayer.Calls[0].IsCompleted); // 周期未到期，纯靠 kick 唤醒
    }

    [Fact]
    public async Task Kick_UnregisteredWidget_IsNoOp()
    {
        await using var scheduler = Create(new FakeDelayer());
        scheduler.KickWidget("ghost"); // 不抛异常即可
    }

    [Fact]
    public async Task BackoffOnFailure_ResetOnSuccess()
    {
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer, _ => new RefreshTierPolicy(TimeSpan.FromSeconds(1), JitterFraction: 0));
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));

        var attempts = 0;
        scheduler.Start((_, _) =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new ConnectionException("down", ConnectionHealthState.Offline); // 第一次失败
            }
            return Task.CompletedTask;
        });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        delayer.Calls[0].Complete(); // 第 1 轮：失败 → 退避 ×2
        await Poll.UntilAsync(() => delayer.Calls.Count >= 2);
        Assert.Equal(TimeSpan.FromSeconds(2), delayer.Calls[1].Requested);

        delayer.Calls[1].Complete(); // 第 2 轮：成功 → 复位
        await Poll.UntilAsync(() => delayer.Calls.Count >= 3);
        Assert.Equal(TimeSpan.FromSeconds(1), delayer.Calls[2].Requested);
    }

    [Fact]
    public async Task UnregisterLastWidget_StopsGroupLoop()
    {
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer);
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));

        var refreshCount = 0;
        scheduler.Start((_, _) => { Interlocked.Increment(ref refreshCount); return Task.CompletedTask; });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        delayer.Calls[0].Complete();
        await Poll.UntilAsync(() => Volatile.Read(ref refreshCount) >= 1);

        scheduler.Unregister("w-1"); // 组空 → 循环取消
        if (delayer.Calls.Count >= 2)
        {
            delayer.Calls[1].Complete(); // 若已在等待，也只会被取消
        }
        await Task.Delay(50);
        Assert.Equal(1, Volatile.Read(ref refreshCount));
    }

    [Fact]
    public async Task ManualTier_NoAutoRefresh_KickTriggersRefresh()
    {
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer, tier => RefreshTierPolicy.Manual);
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Static));

        var refreshCount = 0;
        scheduler.Start((_, _) => { Interlocked.Increment(ref refreshCount); return Task.CompletedTask; });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        Assert.Equal(Timeout.InfiniteTimeSpan, delayer.Calls[0].Requested);
        await Task.Delay(50);
        Assert.Equal(0, Volatile.Read(ref refreshCount)); // 手动级别不自动刷

        scheduler.KickWidget("w-1");
        await Poll.UntilAsync(() => Volatile.Read(ref refreshCount) >= 1);
    }
}
