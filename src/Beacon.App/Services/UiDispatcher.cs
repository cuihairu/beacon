using Beacon.Core.Abstractions;
using Microsoft.UI.Dispatching;

namespace Beacon.App.Services;

/// <summary>IUiDispatcher 的 DispatcherQueue 实现：事件 marshal 回 UI 线程（RFC §11）。</summary>
internal sealed class UiDispatcher(DispatcherQueue dispatcherQueue) : IUiDispatcher
{
    public void Post(Action action)
    {
        if (!dispatcherQueue.TryEnqueue(() => action()))
        {
            action();
        }
    }
}
