using Beacon.App.Infrastructure;

namespace Beacon.App.Services;

/// <summary>
/// 置顶悬浮物哨兵（RFC §6.2.7 常驻可见性）：右键菜单、开始菜单、任意应用抢前台都会
/// 触发前台/菜单事件，系统随之重排 topmost 带——WS_EX_NOACTIVATE 的悬浮窗（胶囊/L0
/// 宿主）不抢焦点、守不住位次，被菜单盖住或挤出，表现为「右键一弹菜单悬浮框就没了」。
/// 哨兵在事件后把它们重新钉回 topmost（SWP_NOACTIVATE，不抢焦点）。
/// 需在 UI 线程构造（WINEVENT_OUTOFCONTEXT 回调随钩子线程的消息泵派发）。
/// </summary>
public sealed class TopmostGuard : IDisposable
{
    private readonly NativeMethods.WinEventProc _callback; // 字段持有防委托 GC，native 钩子只存裸指针
    private readonly List<(Func<IntPtr> Hwnd, Func<bool> Allowed)> _clients = [];
    private readonly IntPtr _hook;

    public TopmostGuard()
    {
        _callback = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_MENUPOPUPEND,
            IntPtr.Zero, _callback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    /// <summary>
    /// 注册悬浮窗：hwnd 用访问器（注册时 Loaded 可能未跑、句柄尚为 Zero，取现值才不会钉空）；
    /// allowed = 是否允许可见（尊重 showCapsule 等用户开关）。
    /// </summary>
    public void Watch(Func<IntPtr> hwnd, Func<bool> allowed)
    {
        lock (_clients)
        {
            _clients.Add((hwnd, allowed));
        }
    }

    private void OnWinEvent(IntPtr hHook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (evt is < NativeMethods.EVENT_SYSTEM_FOREGROUND or > NativeMethods.EVENT_SYSTEM_MENUPOPUPEND)
        {
            return;
        }
        lock (_clients)
        {
            foreach (var (hwndAccessor, allowed) in _clients)
            {
                var clientHwnd = hwndAccessor();
                if (!allowed() || clientHwnd == IntPtr.Zero)
                {
                    continue;
                }
                if (!NativeMethods.IsWindowVisible(clientHwnd))
                {
                    NativeMethods.ShowWindow(clientHwnd, NativeMethods.SW_SHOWNOACTIVATE);
                }
                NativeMethods.SetWindowPos(
                    clientHwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
        }
    }
}
