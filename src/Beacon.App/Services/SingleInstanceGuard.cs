namespace Beacon.App.Services;

/// <summary>单实例（RFC §10）：命名 Mutex 判重 + 命名 Event 通知既有实例唤出面板。</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Local\\Beacon.SingleInstance";
    private const string EventName = "Local\\Beacon.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;

    public bool IsPrimary { get; }

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsPrimary = createdNew;
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }

    /// <summary>副实例调用：通知主实例唤出面板。</summary>
    public void SignalExisting()
    {
        if (!IsPrimary)
        {
            _activateEvent.Set();
        }
    }

    /// <summary>主实例调用：监听副实例的唤出请求。</summary>
    public void OnActivate(Action handler)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    if (!_activateEvent.WaitOne())
                    {
                        return;
                    }
                    handler();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception)
                {
                    // 唤出失败不影响常驻
                }
            }
        });
    }

    public void Dispose()
    {
        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 非拥有线程释放，忽略
            }
        }
        _mutex.Dispose();
        _activateEvent.Dispose();
    }
}
