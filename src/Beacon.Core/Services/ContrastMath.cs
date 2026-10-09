namespace Beacon.Core.Services;

/// <summary>WCAG 2.x 对比度数学（纯函数，Linux 门禁可测）。</summary>
public static class ContrastMath
{
    /// <summary>相对亮度：sRGB 逐通道线性化（≤0.04045 走分段）后按 0.2126/0.7152/0.0722 加权。</summary>
    public static double RelativeLuminance(byte r, byte g, byte b)
    {
        double Channel(byte value)
        {
            var s = value / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    /// <summary>对比度 = (L亮 + 0.05) / (L暗 + 0.05)，范围 1–21（白/黑 = 21）。</summary>
    public static double ContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var l1 = RelativeLuminance(r1, g1, b1);
        var l2 = RelativeLuminance(r2, g2, b2);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
}
