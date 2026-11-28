using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// Live theme state singleton (BEACON-THEME-001). The single realtime source behind
/// ThemeColors.IsDark: App startup seeds it from config, the settings page flips it on
/// save. Volatile because writers (startup, settings save) and readers (window repaint
/// paths) are all UI-thread today, but nothing forces that to stay true.
/// Changed mirrors the UiStrings.LanguageChanged broadcast pattern; raising only on an
/// actual flip keeps same-value saves (re-picking the current combo item) repaint-free.
/// OS theme changes while running are NOT tracked: "system" resolves against the value
/// Application.RequestedTheme captured at startup, refreshed on next launch.
/// </summary>
public static class ThemeRuntime
{
    private static volatile bool _isDark;

    /// <summary>Effective dark flag for the current config value (realtime, no restart).</summary>
    public static bool IsDark => _isDark;

    /// <summary>Raised only when IsDark actually flips. UI thread by construction.</summary>
    public static event Action? Changed;

    /// <summary>Startup seed: set the initial dark flag from config without broadcasting.</summary>
    public static void Initialize(string? theme, bool systemIsDark)
        => _isDark = ThemeResolution.IsDark(theme, systemIsDark);

    /// <summary>Apply a (possibly new) config theme; raises Changed exactly when IsDark flips.</summary>
    public static void Apply(string? theme, bool systemIsDark)
    {
        var next = ThemeResolution.IsDark(theme, systemIsDark);
        if (next == _isDark)
        {
            return;
        }
        _isDark = next;
        Changed?.Invoke();
    }

    /// <summary>Test seam: static state leaks across test methods, so each test resets it (internal,
    /// visible to Core.Tests via InternalsVisibleTo). Does not touch subscribers.</summary>
    internal static void ResetForTests() => _isDark = false;
}
