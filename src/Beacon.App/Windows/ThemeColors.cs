using Beacon.Core.Models;
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

    /// <summary>语义四态（2026-10-29 UI 升级）：默认色板的静态镜像，取自 PaletteResolver.Defaults 同一真源
    /// （success/warning=warn/error+critical=crit/offline=off）。可配置覆盖仍走 UiPalette.SeverityColor
    /// （实时读 config）——凡窗口持有 UiPalette 实例处一律用实例口径，本组仅供无实例的静态构造面回退。</summary>
    public static global::Windows.UI.Color StatusOk() => FromHex(PaletteResolver.Defaults[Severity.Success]);
    public static global::Windows.UI.Color StatusWarn() => FromHex(PaletteResolver.Defaults[Severity.Warning]);
    public static global::Windows.UI.Color StatusCrit() => FromHex(PaletteResolver.Defaults[Severity.Error]);
    public static global::Windows.UI.Color StatusOff() => FromHex(PaletteResolver.DefaultOfflineHex);

    /// <summary>强调色：填充面恒 #2563EB（白字 4.5:1，chip 激活态沿用值）；文本面深色主题提亮一档。</summary>
    public static global::Windows.UI.Color AccentFill() => SeverityPalette.Rgb(255, 37, 99, 235);
    public static global::Windows.UI.Color AccentText() => IsDark ? SeverityPalette.Rgb(255, 147, 197, 253) : SeverityPalette.Rgb(255, 37, 99, 235);

    /// <summary>文本角色三档（收编散落灰值：胶囊/面板/行内 241,245,249 与 30,41,59 等 hex 字面量统一于此）。</summary>
    public static global::Windows.UI.Color TextPrimary() => IsDark ? SeverityPalette.Rgb(255, 241, 245, 249) : SeverityPalette.Rgb(255, 15, 23, 42);
    public static global::Windows.UI.Color TextSecondary() => IsDark ? SeverityPalette.Rgb(255, 148, 163, 184) : SeverityPalette.Rgb(255, 100, 116, 139);
    public static global::Windows.UI.Color TextTertiary() => IsDark ? SeverityPalette.Rgb(255, 100, 116, 139) : SeverityPalette.Rgb(255, 148, 163, 184);

    /// <summary>卡片面：深色=6% 白叠层（A=16），浅色=6% 黑叠层（A=16）——比主题 CardBackground 更轻的内层底。</summary>
    public static global::Windows.UI.Color SurfaceCard() => IsDark ? SeverityPalette.Rgb(16, 255, 255, 255) : SeverityPalette.Rgb(16, 15, 23, 42);

    /// <summary>光晕色：状态色降透明度（微光晕层与核心灯同色异alpha，色相同源不漂移）。</summary>
    public static global::Windows.UI.Color Halo(global::Windows.UI.Color color, byte alpha) => new() { A = alpha, R = color.R, G = color.G, B = color.B };

    /// <summary>设置页模块图标前景：按 tint 亮度选白/黑（亮底必须用黑，白字在橙/绿/青底不足 4.5:1）。</summary>
    public static global::Windows.UI.Color IconOnTint(global::Windows.UI.Color tint)
    {
        var foreground = HighContrastPalette.IconOnTint(tint.R, tint.G, tint.B);
        return Solid(foreground);
    }

    private static global::Windows.UI.Color Solid((byte R, byte G, byte B) rgb) => SeverityPalette.Rgb(255, rgb.R, rgb.G, rgb.B);

    private static global::Windows.UI.Color FromHex(string hex)
    {
        PaletteResolver.TryParseHex(hex, out var argb);
        return new global::Windows.UI.Color
        {
            A = (byte)(argb >> 24),
            R = (byte)(argb >> 16),
            G = (byte)(argb >> 8),
            B = (byte)argb,
        };
    }
}
