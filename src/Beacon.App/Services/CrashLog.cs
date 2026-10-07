using System.Runtime.InteropServices;

namespace Beacon.App.Services;

/// <summary>
/// 最早期的崩溃可见通道（B-105）：DI/日志管线就绪之前发生的异常，FileLoggerProvider
/// 接不到——直写 %AppData%\Beacon\logs\crash-*.log，并弹 MessageBox（托盘常驻无主窗，
/// 静默死亡在用户眼里就是「点了没反应」，所以启动失败必须可见）。
/// </summary>
public static class CrashLog
{
    public static string Write(string title, Exception? exception)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Beacon", "logs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}{Environment.NewLine}{exception}{Environment.NewLine}");
            return path;
        }
        catch
        {
            return string.Empty; // 崩溃通道自身不能再抛
        }
    }

    /// <summary>落盘 + 弹窗（弹窗内容截断，全文在日志文件里）。</summary>
    public static void Alert(string title, Exception? exception)
    {
        var path = Write(title, exception);
        var detail = exception?.ToString() ?? title;
        if (detail.Length > 1800)
        {
            detail = detail[..1800] + Environment.NewLine + "…（完整堆栈见日志）";
        }
        if (!string.IsNullOrEmpty(path))
        {
            detail += Environment.NewLine + Environment.NewLine + $"日志：{path}";
        }
        _ = MessageBoxW(IntPtr.Zero, detail, "Beacon", MB_ICONERROR);
    }

    private const uint MB_ICONERROR = 0x10;

    [DllImport("user32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
