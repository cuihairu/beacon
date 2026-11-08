using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// Core 几何/色板/窗标/动效纯函数边缘收口（Batch 5）：DPI 互算兜底、pin 默认位、
/// 高对比色块 icon 黑白选择、未识别 QuotaWindow 标签空串、Bezier 二分兜底分支。
/// 全部纯函数无 IO，直接钉值。
/// </summary>
public sealed class CoreMathEdgeTests
{
    // ---------- DpiLayoutMath ----------

    [Fact]
    public void Dpi_ToPx_RoundsAwayFromZero()
    {
        Assert.Equal(336, DpiLayoutMath.ToPx(3.5, 96));
        Assert.Equal(0, DpiLayoutMath.ToPx(0, 96));
    }

    [Fact]
    public void Dpi_ToDips_NormalAndInvalidDpiFallback()
    {
        Assert.Equal(100.0, DpiLayoutMath.ToDips(100, 1.0), 6);
        Assert.Equal(200.0, DpiLayoutMath.ToDips(100, 0.5), 6);
        Assert.Equal(100, DpiLayoutMath.ToDips(100, 0), 6);   // dpi<=0 兜底 1:1
        Assert.Equal(100, DpiLayoutMath.ToDips(100, -1), 6);  // 不倒挂
    }

    [Fact]
    public void Dpi_LineHeightDips_ScalesWithFont()
    {
        Assert.Equal(10 * DpiLayoutMath.LineHeightFactor, DpiLayoutMath.LineHeightDips(10), 6);
    }

    // ---------- PinLayoutMath ----------

    [Fact]
    public void Pin_DefaultOffset_IsTopRightOfPrimary()
    {
        Assert.Equal(24, PinLayoutMath.DefaultOffset.X);
        Assert.Equal(96, PinLayoutMath.DefaultOffset.Y);
    }

    // ---------- HighContrastPalette ----------

    [Fact]
    public void HighContrast_IconOnTint_ChoosesHigherContrast()
    {
        // 深底 tint：白字对比更高 → 白
        Assert.Equal(((byte)255, (byte)255, (byte)255), HighContrastPalette.IconOnTint(20, 20, 20));
        // 亮底 tint：黑字对比更高 → 黑
        Assert.Equal(((byte)0, (byte)0, (byte)0), HighContrastPalette.IconOnTint(255, 165, 0));
    }

    // ---------- QuotaWindows ----------

    [Fact]
    public void QuotaWindows_Label_UnknownEnum_Empty()
    {
        Assert.Equal("", QuotaWindows.Label((QuotaWindow)999));
        Assert.Equal("5h", QuotaWindows.Label(QuotaWindow.FiveHour));
        Assert.Equal("周", QuotaWindows.Label(QuotaWindow.Weekly));
        Assert.Equal("月", QuotaWindows.Label(QuotaWindow.Monthly));
    }

    // ---------- IslandMotion（Bezier 二分兜底） ----------

    [Fact]
    public void IslandMotion_Bezier_NewtonFlatFallback_UsesBisection()
    {
        // x 曲线 f(t)=4t³-6t²+3t 在 t=0.5 处斜率为 0（驻点），Newton 从 t=x 起步会掉到错误枝，
        // 走二分兜底 → 结果仍落在合法 [0,1] 且收敛到唯一点（≈0.030）。
        var value = IslandMotion.Bezier(1.0, 0.0, 0.0, 1.0, 0.25);
        Assert.InRange(value, 0.0, 1.0);
        Assert.InRange(value, 0.020, 0.040);
    }

    [Fact]
    public void IslandMotion_Curves_ArePinned()
    {
        Assert.Equal(360, IslandMotion.OpenMs);
        Assert.Equal(270, IslandMotion.CloseMs);
        Assert.Equal(0.75, IslandMotion.CloseFactor, 6);
        Assert.Equal(0, IslandMotion.Bounce(0), 6);
        Assert.Equal(1, IslandMotion.Bounce(1), 6);
        Assert.InRange(IslandMotion.Out(0.5), 0.0, 1.0);
        Assert.InRange(IslandMotion.Drawer(0.5), 0.0, 1.0);
    }
}
