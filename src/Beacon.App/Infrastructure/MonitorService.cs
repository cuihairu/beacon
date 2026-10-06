using System.Runtime.InteropServices;

namespace Beacon.App.Infrastructure;

/// <summary>显示器信息（物理像素）。</summary>
internal sealed record MonitorInfo(
    IntPtr Handle,
    string DeviceName,
    bool IsPrimary,
    NativeMethods.RECT MonitorPx,
    NativeMethods.RECT WorkPx);

/// <summary>多显示器枚举与定位（RFC §40）：L0 宿主/胶囊按显示器标识恢复位置。</summary>
internal static class MonitorService
{
    private const uint MonitorDefaultToNearest = 2;

    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var monitors = new List<MonitorInfo>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            var info = new NativeMethods.MONITORINFOEXW { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
            if (NativeMethods.GetMonitorInfoW(hMonitor, ref info))
            {
                monitors.Add(new MonitorInfo(
                    hMonitor,
                    info.szDevice,
                    (info.dwFlags & 1) != 0,
                    info.rcMonitor,
                    info.rcWork));
            }
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    public static MonitorInfo? Primary()
        => GetAll() is { Count: > 0 } all ? all.FirstOrDefault(m => m.IsPrimary) ?? all[0] : null;

    public static MonitorInfo? FindByName(string deviceName)
        => GetAll().FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

    public static MonitorInfo? FromPixel(int x, int y)
    {
        var point = new NativeMethods.POINT { X = x, Y = y };
        var handle = NativeMethods.MonitorFromPoint(point, MonitorDefaultToNearest);
        return handle == IntPtr.Zero ? null : GetAll().FirstOrDefault(m => m.Handle == handle);
    }
}
