using Microsoft.UI.Windowing;

namespace Beacon.App.Services;

/// <summary>
/// 应用图标唯一来源（用户实测：任务栏/Alt-Tab 显示默认通用图标——unpackaged WinUI 3 的
/// Window 不带 exe 图标进任务栏，必须逐窗 AppWindow.SetIcon）。
/// exe 图标走 csproj ApplicationIcon；托盘由 TrayIconService 运行时绘制；本类管窗口侧。
/// </summary>
internal static class AppIcon
{
    /// <summary>与 csproj ApplicationIcon、托盘加载同源：Assets\beacon.ico（发布形态随 Content 落盘）。</summary>
    public static string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "beacon.ico");

    /// <summary>给窗口补任务栏/Alt-Tab 图标；图标缺失只记错不致命（缺文件不该拦住窗口）。</summary>
    public static void Apply(AppWindow appWindow, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        try
        {
            if (System.IO.File.Exists(Path))
            {
                appWindow.SetIcon(Path);
            }
            else
            {
                logger?.LogWarning("窗口图标缺失：{Path}", Path);
            }
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "AppWindow.SetIcon 失败（不致命）。");
        }
    }
}
