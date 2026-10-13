using Beacon.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Beacon.App.Services;

/// <summary>
/// DispatcherQueue 回调兜底（RFC §10「不闪退」，todo 行 24 V3 发现面）：WinUI 3 的
/// <c>DispatcherQueue</c> 无 HandlerException 事件，<c>TryEnqueue</c> 回调异常也不经
/// <c>Application.UnhandledException</c> 路由——裸抛=进程直接退出（Windows App SDK 1.6
/// 参考程序集 API 面实证：事件仅 ShutdownStarting/Completed、FrameworkShutdownStarting/Completed
/// 四个；nightly 38459944253 场景 V3 红取证）。全 App 的 UI 派发一律走
/// <see cref="EnqueueGuarded"/>：回调异常落应用日志（含堆栈）+ CrashLog，进程存活、
/// 刷新循环续跑。应用日志未就绪（Startup 前）时 CrashLog 独立兜底；包装逻辑在
/// <see cref="DispatchGuard"/>（纯函数，Core 单测覆盖）。
/// </summary>
internal static class DispatcherQueueGuard
{
    private static volatile ILogger? _logger;

    /// <summary>App.Startup 注入应用日志（DI 管线就绪后调用）；此前 CrashLog 独立可用。</summary>
    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// <see cref="DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueueHandler)"/> 的兜底版：
    /// 回调异常落日志不闪退。参数与返回值语义同 TryEnqueue（返回 false = 队列已关，回调未入队）。
    /// </summary>
    public static bool EnqueueGuarded(this DispatcherQueue queue, Action handler)
    {
        var guarded = DispatchGuard.Wrap(handler, OnGuarded);
        return queue.TryEnqueue(() => guarded());
    }

    /// <summary>
    /// 同兜底的同步版：给不经 TryEnqueue 但同骑派发循环的回调用（DispatcherTimer.Tick 等——
    /// tick 由派发循环派发，异常路由与 TryEnqueue 回调同为无系统级兜底）。
    /// </summary>
    public static void RunGuarded(Action handler)
    {
        DispatchGuard.Wrap(handler, OnGuarded)();
    }

    private static void OnGuarded(Exception exception)
    {
        _logger?.LogError(exception, "DispatcherQueue unhandled exception: {Message}", exception.Message);
        CrashLog.Write("DispatcherQueue 回调未处理异常（已兜底，进程存活）", exception);
    }
}
