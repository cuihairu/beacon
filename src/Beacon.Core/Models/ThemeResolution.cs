namespace Beacon.Core.Models;

/// <summary>
/// Theme config string → effective dark flag (BEACON-THEME-001).
/// Pure function so the three-state switch (light / dark / follow-system) stays Core-testable:
/// "light" and "dark" are absolute overrides; anything else (null, empty, unknown, "system")
/// follows the OS. Input is normalized the same way App startup normalizes it (trim + lower).
/// </summary>
public static class ThemeResolution
{
    public static bool IsDark(string? theme, bool systemIsDark)
    {
        return theme?.Trim().ToLowerInvariant() switch
        {
            "light" => false,
            "dark" => true,
            _ => systemIsDark,
        };
    }
}
