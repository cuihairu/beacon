using Microsoft.Win32;

namespace Beacon.App.Services;

/// <summary>开机自启（RFC §39）：HKCU Run 键。自启后本就无主窗口（仅托盘+胶囊），天然静默。</summary>
public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Beacon";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string path && !string.IsNullOrWhiteSpace(path);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
        {
            throw new InvalidOperationException("Cannot open HKCU Run key.");
        }
        if (enabled)
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Process path unavailable.");
            key.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>启动时把注册表状态对齐到配置（config.LaunchOnStartup 为准）。</summary>
    public void SyncWith(bool launchOnStartup)
    {
        if (IsEnabled() != launchOnStartup)
        {
            SetEnabled(launchOnStartup);
        }
    }
}
