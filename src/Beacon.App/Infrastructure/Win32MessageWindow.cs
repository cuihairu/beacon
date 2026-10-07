using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Beacon.App.Infrastructure;

/// <summary>
/// 消息专用窗口（message-only）：承载托盘回调、全局热键等无 UI 的 Win32 消息。
/// 在 UI 线程创建，消息经 DispatcherQueue 泵派发（RFC §11）。
/// </summary>
internal sealed class Win32MessageWindow : IDisposable
{
    private const string ClassName = "BeaconMessageWindow";

    private readonly ILogger? _logger;
    private readonly NativeMethods.WndProcDelegate _wndProc; // 持有引用防 GC 回收
    private IntPtr _classNamePtr; // RegisterClassW 的原生类名串
    private readonly IntPtr _hwnd;

    /// <summary>(msg, wParam, lParam)</summary>
    public event Action<uint, IntPtr, IntPtr>? MessageReceived;

    public IntPtr Hwnd => _hwnd;

    public Win32MessageWindow(ILogger<Win32MessageWindow>? logger = null)
    {
        _logger = logger;
        _wndProc = WndProc; // 委托实例由字段持有，原生函数指针才不会悬空

        var hInstance = NativeMethods.GetModuleHandleW(null);
        _classNamePtr = Marshal.StringToHGlobalUni(ClassName); // 类名串随类存续，Dispose 时释放
        var windowClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = _classNamePtr,
        };
        var atom = NativeMethods.RegisterClassW(ref windowClass);
        if (atom == 0)
        {
            Marshal.FreeHGlobal(_classNamePtr);
            _classNamePtr = IntPtr.Zero;
            throw new InvalidOperationException($"RegisterClassW failed: {Marshal.GetLastWin32Error()}");
        }

        // HWND_MESSAGE = (IntPtr)(-3)：消息专用窗口，不可见、不进任务栏
        _hwnd = NativeMethods.CreateWindowExW(
            0, ClassName, string.Empty, 0, 0, 0, 0, 0,
            (IntPtr)(-3), IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowExW(message-only) failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            MessageReceived?.Invoke(msg, wParam, lParam);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Message window handler threw for msg 0x{Message:X}", msg);
        }
        return NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        MessageReceived = null;
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
        }
        if (_classNamePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_classNamePtr);
            _classNamePtr = IntPtr.Zero;
        }
    }
}
