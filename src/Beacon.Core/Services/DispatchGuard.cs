namespace Beacon.Core.Services;

/// <summary>
/// UI 派发回调兜底（RFC §10「不闪退」，todo 行 24 V3 发现面）：WinUI 3 的
/// <c>DispatcherQueue</c> 无 HandlerException 事件，<c>TryEnqueue</c> 回调异常也不经
/// <c>Application.UnhandledException</c> 路由——裸抛=进程直接退出（Windows App SDK 1.6
/// 参考程序集 API 面实证 + nightly 38459944253 场景 V3 红取证）。本助手把回调包一层
/// try/catch：异常转交 <paramref name="onException"/> 落日志/取证，进程存活。
/// onException 自身异常也吞——兜底通道不能再抛（否则兜底反噬成新崩溃源）。
/// 纯包装函数供单测；DispatcherQueue 侧入口见 App 的 DispatcherQueueGuard。
/// </summary>
public static class DispatchGuard
{
    /// <summary>包装回调：正常执行直通；异常被捕获并转交 onException（其自身异常亦吞）。</summary>
    public static Action Wrap(Action handler, Action<Exception> onException)
    {
        return () =>
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                try
                {
                    onException(exception);
                }
                catch
                {
                    // 兜底通道自身不能再抛——日志/取证失败也不许把进程带崩
                }
            }
        };
    }
}
