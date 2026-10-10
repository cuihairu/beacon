using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// DPI 修复单回归门（2026-10-10，5K 200% 字体被裁）：自绘窗口容器与字号同源换算、
/// 按总高一次四舍五入（非逐行截断）、四档 dpi 下「文本高度 ≤ 容器高度」且 bounds 合理。
/// CI 恒 100% DPI，此门是唯一能在 CI 拦住裁字的断言——真数字真比较，非摆设。
/// </summary>
public sealed class DpiLayoutMathTests
{
    // 用户报障的两档（5K 200% / 4K 150%）+ 100% 基线 + 250% 上限档
    private static readonly double[] Dpis = [1.0, 1.5, 2.0, 2.5];

    // 窗口设计值（与 Beacon.App 同名常量同源，改动须同步）
    private const double TileHeightDips = 32;
    private const double TileMaxFontDips = 12;
    private const double CapsuleMaxFontDips = 12.5;
    private const double CapsuleMinWidthDips = 48;
    private const double CapsuleMaxWidthDips = 132;
    private const double QuickPanelWidthDips = 320;
    private const double QuickPanelHeightDips = 540;
    private const double QuickPanelMaxFontDips = 15;
    private const double DetailWidthDips = 520;
    private const double DetailMinHeightDips = 320;
    private const double DetailMaxHeightDips = 560;
    private const double DetailMaxFontDips = 16;

    /// <summary>③ 换算口径：四舍五入，任何 dpi 下偏差不超过半像素（截断在小数部分 &gt; 0.5 时欠 1 像素）。</summary>
    [Theory]
    [InlineData(TileHeightDips)]
    [InlineData(CapsuleMaxWidthDips)]
    [InlineData(QuickPanelHeightDips)]
    [InlineData(DetailWidthDips)]
    public void ToPx_RoundsToHalfPixel_NeverTruncates(double dips)
    {
        foreach (var dpi in Dpis)
        {
            var px = DpiLayoutMath.ToPx(dips, dpi);
            Assert.True(Math.Abs(px - dips * dpi) <= 0.5, $"{dips} DIP × {dpi} = {px} px，偏差超过半像素（截端口径）");
        }
    }

    /// <summary>③ 判别线：小数部分 &gt; 0.5 的真值必须进位（截断得 floor 值——回归时此断言先红）。</summary>
    [Fact]
    public void ToPx_RoundsUpPastHalf_NotFloor()
    {
        Assert.Equal(42, DpiLayoutMath.ToPx(32, 1.3));  // 41.6 → 42（截断得 41）
        Assert.Equal(125, DpiLayoutMath.ToPx(96, 1.3)); // 124.8 → 125（截断得 124）
    }

    /// <summary>② 裁字判定（tile）：32 DIP 行盒装得下 12 DIP 字号的一行文本，四档全绿。</summary>
    [Theory]
    [InlineData(1.0)] [InlineData(1.5)] [InlineData(2.0)] [InlineData(2.5)]
    public void TileRow_HoldsTextAtEveryDpi(double dpi)
    {
        Assert.True(DpiLayoutMath.TextFits(TileHeightDips, TileMaxFontDips, dpi), $"{dpi}：tile 32 DIP 装不下 12 DIP 文本");
        // 反例必须判出裁字——断言非空转
        Assert.False(DpiLayoutMath.TextFits(14, TileMaxFontDips, dpi), $"{dpi}：14 DIP 容器应判为裁字");
    }

    /// <summary>② 裁字判定（胶囊/L2/L3）：容器物理高 ≥ 最大字号物理行高，四档全绿。</summary>
    [Theory]
    [InlineData(CapsuleMaxFontDips, 36)]
    [InlineData(QuickPanelMaxFontDips, QuickPanelHeightDips)]
    [InlineData(DetailMaxFontDips, DetailMinHeightDips)]
    public void Containers_HoldTextAtEveryDpi(double fontSizeDips, double containerDips)
    {
        foreach (var dpi in Dpis)
        {
            Assert.True(DpiLayoutMath.TextFits(containerDips, fontSizeDips, dpi), $"{fontSizeDips} DIP 文本在 {dpi} 被 {containerDips} DIP 容器裁字");
        }
    }

    /// <summary>③ 多行等高总高：总 DIP 一次换算，逐行截断的累计欠高不复现。
    /// 判别值取 dpi=1.3（Windows 自定义缩放 130%）：32×1.3=41.6，逐行 41×3=123 px，总换算 125 px。</summary>
    [Fact]
    public void PanelHeightPx_SingleRounding_NoRowAccumulationLoss()
    {
        var rows = 3;
        Assert.Equal(DpiLayoutMath.ToPx(rows * TileHeightDips, 1.3), DpiLayoutMath.PanelHeightPx(rows, TileHeightDips, 1.3));
        Assert.Equal(125, DpiLayoutMath.PanelHeightPx(rows, TileHeightDips, 1.3));
        // 逐行截断口径的值——回归时若被改回，此断言失败
        Assert.True(DpiLayoutMath.PanelHeightPx(rows, TileHeightDips, 1.3) >= rows * (int)Math.Floor(TileHeightDips * 1.3));
    }

    /// <summary>③ 多行总高在四档 dpi 下都是各档真值的四舍五入（无截断欠高）。</summary>
    [Theory]
    [InlineData(1.0, 3, 96)]
    [InlineData(1.5, 3, 144)]
    [InlineData(2.0, 3, 192)]
    [InlineData(2.5, 3, 240)]
    public void PanelHeightPx_MatchesDipTimesDpi(double dpi, int rows, int expectedPx)
    {
        Assert.Equal(expectedPx, DpiLayoutMath.PanelHeightPx(rows, TileHeightDips, dpi));
    }

    /// <summary>③ 胶囊宽随内容 clamp 后换算：任何 dpi 下物理宽落在 [min,max] 的物理档内。</summary>
    [Theory]
    [InlineData(1.0)] [InlineData(1.5)] [InlineData(2.0)] [InlineData(2.5)]
    public void CapsuleWidth_InsideClampedRangeAtEveryDpi(double dpi)
    {
        var px = DpiLayoutMath.ToPx(CapsuleMaxWidthDips, dpi);
        Assert.True(px >= DpiLayoutMath.ToPx(CapsuleMinWidthDips, dpi) - 1 && px <= DpiLayoutMath.ToPx(CapsuleMaxWidthDips, dpi),
            $"{dpi}：胶囊宽 {px} px 越出 clamp 档");
    }

    /// <summary>③ 详情窗高度：自然内容高封顶走滚动，物理高 = 封顶 DIP × dpi（四舍五入），且不裁最大字号。</summary>
    [Theory]
    [InlineData(1.0)] [InlineData(1.5)] [InlineData(2.0)] [InlineData(2.5)]
    public void DetailHeight_ClampedToCeilingAtEveryDpi(double dpi)
    {
        var px = DpiLayoutMath.ClampedHeightPx(contentDips: 700, minDips: DetailMinHeightDips, maxDips: DetailMaxHeightDips, dpi);
        Assert.Equal(DpiLayoutMath.ToPx(DetailMaxHeightDips, dpi), px);
        Assert.True(DpiLayoutMath.TextFits(DetailMaxHeightDips, DetailMaxFontDips, dpi), $"{dpi}：详情窗裁字");
        // 短内容落在下限档
        Assert.Equal(DpiLayoutMath.ToPx(DetailMinHeightDips, dpi),
            DpiLayoutMath.ClampedHeightPx(contentDips: 120, minDips: DetailMinHeightDips, maxDips: DetailMaxHeightDips, dpi));
    }

    /// <summary>④ bounds 合理：详情窗物理 bounds = 设计 DIP × dpi，四档全对得上（内容 480 DIP 落中段档）。</summary>
    [Theory]
    [InlineData(1.0, 520, 480)]
    [InlineData(1.5, 780, 720)]
    [InlineData(2.0, 1040, 960)]
    [InlineData(2.5, 1300, 1200)]
    public void DetailBounds_ExactAtEveryDpi(double dpi, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(expectedWidth, DpiLayoutMath.ToPx(DetailWidthDips, dpi));
        Assert.Equal(expectedHeight, DpiLayoutMath.ClampedHeightPx(480, DetailMinHeightDips, DetailMaxHeightDips, dpi));
    }

    /// <summary>④ 快捷面板固定 DIP 尺寸四档换算（面板是 DIP 设计值 320×540，仅 Win32 边界换算）。</summary>
    [Theory]
    [InlineData(1.0, 320, 540)]
    [InlineData(1.5, 480, 810)]
    [InlineData(2.0, 640, 1080)]
    [InlineData(2.5, 800, 1350)]
    public void QuickPanelBounds_ExactAtEveryDpi(double dpi, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(expectedWidth, DpiLayoutMath.ToPx(QuickPanelWidthDips, dpi));
        Assert.Equal(expectedHeight, DpiLayoutMath.ToPx(QuickPanelHeightDips, dpi));
    }
}
