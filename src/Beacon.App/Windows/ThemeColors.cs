using Beacon.Core.Services;
using Microsoft.UI.Xaml;

namespace Beacon.App.Windows;

/// <summary>主题色桥：Core 高对比调色板（可测元组）→ WinUI 画刷。
/// 悬浮 tile 实色底 + 主题感知前景（浅底深字/深底亮字，WCAG AA 4.5:1，
/// 配对断言见 Core.Tests/ContrastMathTests）。主题按应用启动口径（重启生效）。</summary>
internal static class ThemeColors
{
    private static bool IsDark => Application.Current.RequestedTheme == ApplicationTheme.Dark;

    public static global::Windows.UI.Color Surface() => Solid(HighContrastPalette.Surface(IsDark));
    public static global::Windows.UI.Color Label() => Solid(HighContrastPalette.Label(IsDark));
    public static global::Windows.UI.Color Value() => Solid(HighContrastPalette.Value(IsDark));

    /// <summary>设置页模块图标前景：按 tint 亮度选白/黑（亮底必须用黑，白字在橙/绿/青底不足 4.5:1）。</summary>
    public static global::Windows.UI.Color IconOnTint(global::Windows.UI.Color tint)
    {
        var foreground = HighContrastPalette.IconOnTint(tint.R, tint.G, tint.B);
        return Solid(foreground);
    }

    private static global::Windows.UI.Color Solid((byte R, byte G, byte B) rgb) => SeverityPalette.Rgb(255, rgb.R, rgb.G, rgb.B);
}
