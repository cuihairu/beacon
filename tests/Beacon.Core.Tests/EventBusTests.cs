namespace Beacon.Core.Tests;
using Beacon.Core.Abstractions;
using Beacon.Core.Services;

/// <summary>B-203 验收：并发发布无死锁/丢发；UI marshal 辅助器可用。</summary>
public sealed class EventBusTests
{
    [Fact]
    public void Publish_InvokesAllSubscribers()
    {
        var bus = new EventBus();
        var first = 0;
        var second = 0;
        using var _ = bus.Subscribe<int>(_ => Interlocked.Increment(ref first));
        using var __ = bus.Subscribe<int>(_ => Interlocked.Increment(ref second));

        bus.Publish(1);
        bus.Publish(2);

        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public void HandlerException_DoesNotAffectOtherSubscribers()
    {
        var bus = new EventBus();
        var survived = 0;
        using var _ = bus.Subscribe<int>(_ => throw new InvalidOperationException("boom"));
        using var __ = bus.Subscribe<int>(_ => survived++);

        bus.Publish(1);

        Assert.Equal(1, survived);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var bus = new EventBus();
        var count = 0;
        var subscription = bus.Subscribe<int>(_ => count++);

        bus.Publish(1);
        subscription.Dispose();
        bus.Publish(2);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Publish_UsesSnapshot_HandlerAddedDuringPublishNotInvokedUntilNext()
    {
        var bus = new EventBus();
        var late = 0;
        using var _ = bus.Subscribe<int>(_ => bus.Subscribe<int>(__ => Interlocked.Increment(ref late)));

        bus.Publish(1);
        Assert.Equal(0, late); // 本轮快照里没有新订阅者

        bus.Publish(2);
        Assert.Equal(1, late);
    }

    [Fact]
    public async Task ConcurrentPublish_NoDeadlockNoLoss()
    {
        var bus = new EventBus();
        var count = 0;
        using var _ = bus.Subscribe<string>(_ => Interlocked.Increment(ref count));
        const int threads = 8;
        const int perThread = 200;

        await Task.WhenAll(Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                bus.Publish("evt");
            }
        })));

        Assert.Equal(threads * perThread, count);
    }

    [Fact]
    public void SubscribeOnUi_MarshalsToDispatcher()
    {
        var bus = new EventBus();
        var dispatcher = new RecordingDispatcher();
        var received = 0;
        using var _ = bus.SubscribeOnUi<int>(dispatcher, _ => received++);

        bus.Publish(1);
        Assert.Equal(0, received); // 未 flush 前不执行
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.FlushAll();
        Assert.Equal(1, received);
    }
}
