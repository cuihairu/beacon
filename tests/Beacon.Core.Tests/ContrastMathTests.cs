using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>WCAG 对比度数学：已知锚点（白/黑=21、同色=1）+ 主题调色板全部配对 ≥4.5:1。</summary>
public sealed class ContrastMathTests
{
    [Fact]
    public void WhiteOnBlack_Is21()
    {
        Assert.Equal(21, ContrastMath.ContrastRatio(255, 255, 255, 0, 0, 0), 3);
    }

    [Fact]
    public void SameColor_Is1()
    {
        Assert.Equal(1, ContrastMath.ContrastRatio(30, 41, 59, 30, 41, 59), 3);
    }

    [Fact]
    public void RelativeLuminance_BlackIs0_WhiteIs1()
    {
        Assert.Equal(0, ContrastMath.RelativeLuminance(0, 0, 0), 6);
        Assert.Equal(1, ContrastMath.RelativeLuminance(255, 255, 255), 6);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SurfaceLabel_MeetsAA(bool dark)
    {
        var label = HighContrastPalette.Label(dark);
        var surface = HighContrastPalette.Surface(dark);
        Assert.True(ContrastMath.ContrastRatio(label.R, label.G, label.B, surface.R, surface.G, surface.B) >= HighContrastPalette.MinContrast,
            $"标签/底对比不足 4.5:1（dark={dark}）");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SurfaceValue_MeetsAA(bool dark)
    {
        var value = HighContrastPalette.Value(dark);
        var surface = HighContrastPalette.Surface(dark);
        Assert.True(ContrastMath.ContrastRatio(value.R, value.G, value.B, surface.R, surface.G, surface.B) >= HighContrastPalette.MinContrast,
            $"数值/底对比不足 4.5:1（dark={dark}）");
    }

    [Theory]
    [InlineData(255, 105, 0)]   // mimo 橙
    [InlineData(63, 185, 80)]   // http 绿
    [InlineData(41, 112, 255)]  // ark 蓝
    [InlineData(110, 118, 129)] // github 灰
    [InlineData(56, 89, 255)]   // bigmodel 蓝
    [InlineData(16, 22, 34)]    // codex 近墨
    [InlineData(142, 78, 198)]  // copilot 紫
    [InlineData(13, 148, 136)]  // opencode 青
    [InlineData(148, 163, 184)] // 默认灰
    public void IconOnTint_MeetsAA(byte r, byte g, byte b)
    {
        var foreground = HighContrastPalette.IconOnTint(r, g, b);
        Assert.True(ContrastMath.ContrastRatio(foreground.R, foreground.G, foreground.B, r, g, b) >= HighContrastPalette.MinContrast,
            $"tint({r},{g},{b}) 上 icon 前景对比不足 4.5:1");
    }
}
