using Beacon.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Beacon.Core.Services;

/// <summary>线程安全进程内事件总线：处理器同步调用，单处理器异常不影响其他订阅方（B-203）。</summary>
public sealed class EventBus : IEventBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];
    private readonly ILogger? _logger;

    public EventBus(ILogger? logger = null)
    {
        _logger = logger;
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                _handlers[typeof(TEvent)] = list = [];
            }
            list.Add(handler);
        }
        return new Subscription<TEvent>(this, handler);
    }

    public void Publish<TEvent>(TEvent evt) where TEvent : notnull
    {
        Delegate[] snapshot;
        lock (_gate)
        {
            snapshot = _handlers.TryGetValue(typeof(TEvent), out var list) ? [.. list] : [];
        }
        foreach (var handler in snapshot)
        {
            try
            {
                ((Action<TEvent>)handler)(evt);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Event handler for {EventType} threw.", typeof(TEvent).Name);
            }
        }
    }

    private void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        lock (_gate)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                {
                    _handlers.Remove(typeof(TEvent));
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _handlers.Clear();
        }
    }

    private sealed class Subscription<TEvent>(EventBus bus, Action<TEvent> handler) : IDisposable
        where TEvent : notnull
    {
        public void Dispose() => bus.Unsubscribe(handler);
    }
}
