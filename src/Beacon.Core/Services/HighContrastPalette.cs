namespace Beacon.Core.Services;

/// <summary>
/// 高对比调色板（WCAG AA 正文 4.5:1，全部配对经 Core.Tests 断言）：
/// 悬浮 tile 实色底 + 主题感知前景（浅底深字/深底亮字）；设置页模块图标按 tint 亮度选白/黑。
/// 实色底是对比度可判定的前提——半透明 scrim 叠在未知桌面上，前景取色无从保证。
/// </summary>
public static class HighContrastPalette
{
    public const double MinContrast = 4.5;

    /// <summary>tile 实色底：深 slate-900 (15,23,42) / 浅 slate-100 (241,245,249)。</summary>
    public static (byte R, byte G, byte B) Surface(bool dark) => dark ? ((byte)15, (byte)23, (byte)42) : ((byte)241, (byte)245, (byte)249);

    /// <summary>主文本（标签/icon）：深底近白 (226,232,240) / 浅底 slate-900 (30,41,59)。
    /// 浅底不用品牌主色——品牌色在浅底上普遍不足 4.5:1（如 ark 蓝 ≈3.8）。</summary>
    public static (byte R, byte G, byte B) Label(bool dark) => dark ? ((byte)226, (byte)232, (byte)240) : ((byte)30, (byte)41, (byte)59);

    /// <summary>次文本（数值）：深底 slate-400 (148,163,184) / 浅底 slate-600 (71,85,105)。</summary>
    public static (byte R, byte G, byte B) Value(bool dark) => dark ? ((byte)148, (byte)163, (byte)184) : ((byte)71, (byte)85, (byte)105);

    /// <summary>品牌 icon 前景：同主文本（浅底深色可辨）。</summary>
    public static (byte R, byte G, byte B) Icon(bool dark) => Label(dark);

    /// <summary>色块上 icon 前景：白/黑取对比高者——mimo 橙/http 绿/opencode 青等亮底必须用黑
    /// （白字在橙底仅 ≈2.9:1）。</summary>
    public static (byte R, byte G, byte B) IconOnTint(byte tintR, byte tintG, byte tintB)
        => ContrastMath.ContrastRatio(255, 255, 255, tintR, tintG, tintB)
            >= ContrastMath.ContrastRatio(0, 0, 0, tintR, tintG, tintB)
            ? ((byte)255, (byte)255, (byte)255)
            : ((byte)0, (byte)0, (byte)0);
}
