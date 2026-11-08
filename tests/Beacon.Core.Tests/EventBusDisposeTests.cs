using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>EventBus Dispose 收口（Batch 5）：Dispose 清空全部处理器，之后 Publish 静默无操作不抛。</summary>
public sealed class EventBusDisposeTests
{
    [Fact]
    public void Dispose_ClearsAllHandlers_PublishIsNoOp()
    {
        var bus = new EventBus();
        var calls = 0;
        bus.Subscribe<StubEvent>(_ => calls++);
        bus.Publish(new StubEvent());
        Assert.Equal(1, calls);

        bus.Dispose();
        bus.Publish(new StubEvent());

        Assert.Equal(1, calls); // Dispose 后不再触发
    }

    [Fact]
    public void SubscribeAfterDispose_ThenPublish_Delivers()
    {
        var bus = new EventBus();
        bus.Dispose();
        var calls = 0;
        bus.Subscribe<StubEvent>(_ => calls++);
        bus.Publish(new StubEvent());
        Assert.Equal(1, calls);
    }

    private sealed record StubEvent;
}
