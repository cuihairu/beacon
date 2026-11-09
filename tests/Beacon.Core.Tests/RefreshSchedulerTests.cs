using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.Extensions.Logging;

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

    [Theory]
    [InlineData(RefreshTiers.Pr)]
    [InlineData(RefreshTiers.Ci)]
    [InlineData(RefreshTiers.Machine)]
    [InlineData(RefreshTiers.Agent)]
    [InlineData(RefreshTiers.Workflow)]
    [InlineData(RefreshTiers.Default)]
    public void DefaultTierPolicyTable_CoversAllRefreshTiers(string tier)
    {
        var policy = RefreshTiers.Defaults[tier];

        Assert.NotEqual(Timeout.InfiniteTimeSpan, policy.Interval);
        Assert.Equal(0.2, policy.JitterFraction);
        Assert.Equal(10, policy.MaxBackoffMultiplier);
    }

    [Fact]
    public void DefaultTierPolicyTable_StaticIsManual()
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, RefreshTiers.Defaults[RefreshTiers.Static].Interval);
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

    // ---- 收口：Register 重注册（行 60）/ Unregister 未知 ID 早退（行 94）/ Stop 聚合故障任务（行 144/147）
    //      / delay 赢 WhenAny + 取消窗口 break（行 188）/ kick-drain 吸收挂起 kick（行 227）----

    [Fact]
    public async Task ReRegisterSameWidget_MovesToNewGroupWithoutDuplicate()
    {
        // 已注册 ID 再次 Register → UnregisterCore（行 60）先摘旧组再注册新组，不得重复条目
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer, _ => new RefreshTierPolicy(TimeSpan.FromMilliseconds(1000)));
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));
        scheduler.Register(new WidgetRegistration("w-1", "conn-2", RefreshTiers.Ci)); // 重注册：换连接

        var requests = new List<RefreshRequest>();
        var gate = new object();
        scheduler.Start((request, _) => { lock (gate) requests.Add(request); return Task.CompletedTask; });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        delayer.Calls[0].Complete();
        await Poll.UntilAsync(() => requests.Count >= 1);

        var request = Assert.Single(requests); // 旧组已拆，只剩新组一条
        Assert.Equal("conn-2", request.ConnectionId);
        Assert.Equal(["w-1"], request.WidgetIds); // 组内无重复条目
    }

    [Fact]
    public async Task UnregisterUnknownWidget_IsNoOp()
    {
        // 未注册 ID 走 UnregisterCore 早退（行 94）：不抛异常、不影响既有组
        var delayer = new FakeDelayer();
        await using var scheduler = Create(delayer);
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));
        scheduler.Unregister("ghost");

        var requests = new List<RefreshRequest>();
        var gate = new object();
        scheduler.Start((request, _) => { lock (gate) requests.Add(request); return Task.CompletedTask; });

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        delayer.Calls[0].Complete();
        await Poll.UntilAsync(() => requests.Count >= 1);

        Assert.Equal(["w-1"], requests[0].WidgetIds); // 既有组未受影响
    }

    private sealed class ThrowingLogger : ILogger
    {
        public int LogCalls;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            where TState : notnull
        {
            Interlocked.Increment(ref LogCalls);
            throw new InvalidOperationException("logger fault");
        }
    }

    [Fact]
    public async Task Stop_AggregatesFaultedLoopTask_WithoutThrowing()
    {
        // 循环任务因 logger 抛异常从 catch 逸出而故障 → Stop 的 WaitAll 聚合成 AggregateException，
        // 被 catch（行 144/147）吞掉，不向调用方抛出（进程生命周期对象口径）。
        var delayer = new FakeDelayer();
        var logger = new ThrowingLogger();
        await using var scheduler = new RefreshScheduler(
            _ => new RefreshTierPolicy(TimeSpan.FromMilliseconds(1000)),
            delayer.Delay,
            logger,
            randomSeed: 42);
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));
        scheduler.Start((_, _) => throw new InvalidOperationException("provider down")); // 刷新失败 → LogWarning 抛 → 循环故障

        await Poll.UntilAsync(() => delayer.Calls.Count >= 1);
        delayer.Calls[0].Complete();
        await Poll.UntilAsync(() => logger.LogCalls >= 1); // 故障已发生（loop task 进入 Faulted）

        scheduler.Stop(); // 不抛异常（WaitAll 的 AggregateException 被吞）
    }

    [Fact]
    public async Task DelayWinsWithPendingKick_BreaksOnCancelBeforeRefresh()
    {
        // 行 227 + 行 188：第 2 轮 delay 委托同步 Unregister 取消组 token → WaitAsync(ct) 因 token 已取消
        // 返回取消任务且不消耗 count → delay 赢 WhenAny → kick-drain 吸收挂起 kick（行 227）→
        // await completed 后查 token 已取消 → break（行 188），不再触发第 2 次刷新。
        RefreshScheduler? scheduler = null;
        var delayCalls = 0;
        var refreshCount = 0;
        scheduler = new RefreshScheduler(
            _ => new RefreshTierPolicy(TimeSpan.FromMilliseconds(1000)),
            (_, _) =>
            {
                if (Interlocked.Increment(ref delayCalls) >= 2)
                {
                    scheduler!.Unregister("w-1"); // 取消组循环（行 101）
                }
                return Task.CompletedTask;
            },
            randomSeed: 42);
        await using var owned = scheduler;
        scheduler.Register(new WidgetRegistration("w-1", "conn-1", RefreshTiers.Ci));
        scheduler.Start((_, _) =>
        {
            Interlocked.Increment(ref refreshCount);
            scheduler!.KickWidget("w-1"); // 为下一轮 drain 预置 count=1
            return Task.CompletedTask;
        });

        await Poll.UntilAsync(() => Volatile.Read(ref delayCalls) >= 2); // 第 2 轮 delay 已发出（且已取消）
        await Poll.UntilAsync(() => Volatile.Read(ref refreshCount) >= 1); // 第 1 轮已刷新
        Assert.Equal(1, Volatile.Read(ref refreshCount)); // 第 2 轮在 refresh 前 break，不再刷新
    }
}
