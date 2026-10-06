namespace Beacon.Core.Abstractions;

/// <summary>线程安全的进程内事件总线（RFC §11：UI 订阅方负责 marshal 回 UI 线程）。</summary>
public interface IEventBus : IDisposable
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    void Publish<TEvent>(TEvent evt) where TEvent : notnull;
}

/// <summary>UI 线程调度抽象（App 侧以 DispatcherQueue 实现）。</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public static class EventBusUiExtensions
{
    /// <summary>订阅并把事件 marshal 到 UI 线程。</summary>
    public static IDisposable SubscribeOnUi<TEvent>(this IEventBus bus, IUiDispatcher dispatcher, Action<TEvent> handler)
        where TEvent : notnull
        => bus.Subscribe<TEvent>(e => dispatcher.Post(() => handler(e)));
}
