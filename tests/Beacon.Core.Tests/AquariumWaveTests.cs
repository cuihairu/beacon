using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 水族箱波纹几何（AQUA-001）：正弦折线采样、无缝循环不变式（平移一个周期波形重合——
/// App 侧路径宽=通栏宽+周期、动画只漂移 −周期 的循环方案靠这条性质成立）、振幅包络。
/// </summary>
public sealed class AquariumWaveTests
{
    [Fact]
    public void Crest_PointCount_FollowsPeriodCoverage()
    {
        var points = AquariumWave.Crest(120, AquariumWave.FrontPeriodDips, AquariumWave.FrontAmplitudeDips, 0);
        // 120 DIP 宽 / 40 周期 = 3 周期 → 3×16+1 = 49 点
        Assert.Equal(3 * AquariumWave.SegmentsPerPeriod + 1, points.Length);
        Assert.Equal(0, points[0].X);
        Assert.Equal(120, points[^1].X);
    }

    [Fact]
    public void Crest_WidthBelowPeriod_StillReturnsOnePeriodOfPoints()
    {
        var points = AquariumWave.Crest(20, AquariumWave.FrontPeriodDips, AquariumWave.FrontAmplitudeDips, 0);
        Assert.Equal(AquariumWave.SegmentsPerPeriod + 1, points.Length); // 至少一整周期（循环方案的前提）
    }

    [Fact]
    public void Crest_WidthNotPeriodMultiple_LastPointPeriodAlignedBeyondWidth() // 采样周期对齐（无缝循环前提）：宽非整倍数时末点越过宽度（App 侧 Clip 裁带外），且末点在周期边界上与首点同形
    {
        var points = AquariumWave.Crest(185, AquariumWave.FrontPeriodDips, AquariumWave.FrontAmplitudeDips, 0);
        Assert.Equal(5 * AquariumWave.SegmentsPerPeriod + 1, points.Length); // ceil(185/40)=5
        Assert.Equal(200, points[^1].X, 9);
        Assert.Equal(0, points[^1].Y, 9);
    }

    [Fact]
    public void Crest_PeriodSpan_HasIdenticalY() // 无缝循环：x 与 x+P 处 Y 重合（宽取周期整倍数，采样点才落在周期边界上）
    {
        var points = AquariumWave.Crest(68, AquariumWave.BackPeriodDips, AquariumWave.BackAmplitudeDips, AquariumWave.BackPhaseTurns);
        var perPeriod = AquariumWave.SegmentsPerPeriod;
        for (var i = 0; i + perPeriod < points.Length; i++)
        {
            Assert.Equal(points[i].X + AquariumWave.BackPeriodDips, points[i + perPeriod].X, 9);
            Assert.Equal(points[i].Y, points[i + perPeriod].Y, 9);
        }
        Assert.True(AquariumWave.IsSeamlessAt(0, AquariumWave.BackPeriodDips, AquariumWave.BackAmplitudeDips, AquariumWave.BackPhaseTurns));
    }

    [Fact]
    public void Crest_AmplitudeBounded_PhaseShiftsCurve()
    {
        var front = AquariumWave.Crest(120, AquariumWave.FrontPeriodDips, AquariumWave.FrontAmplitudeDips, 0);
        Assert.All(front, p => Assert.InRange(p.Y, -AquariumWave.FrontAmplitudeDips, AquariumWave.FrontAmplitudeDips));

        var shifted = AquariumWave.Crest(120, AquariumWave.FrontPeriodDips, AquariumWave.FrontAmplitudeDips, 0.25);
        Assert.NotEqual(front[4].Y, shifted[4].Y); // 相位差 1/4 周期：同 x 处 Y 必不同
        Assert.All(shifted, p => Assert.InRange(p.Y, -AquariumWave.FrontAmplitudeDips, AquariumWave.FrontAmplitudeDips));
    }

    [Fact]
    public void Crest_PhaseHalfTurn_InvertsWave() // sin(+π) = −sin：半周期相位翻转整条波形（两层波相位错开的极性锚）
    {
        var baseWave = AquariumWave.Crest(80, 40, 2, 0);
        var inverted = AquariumWave.Crest(80, 40, 2, 0.5);
        for (var i = 0; i < baseWave.Length; i++)
        {
            Assert.Equal(-baseWave[i].Y, inverted[i].Y, 9);
        }
    }

    [Fact]
    public void Constants_SmallAmplitudeSlowPeriod_PerUserBrief() // 「幅度小、频率慢」的量化锚：改值必须自觉过这条
    {
        Assert.True(AquariumWave.FrontAmplitudeDips <= 2.0); // 波峰不越过通栏一半高（12 DIP 带）
        Assert.True(AquariumWave.FrontPeriodDips >= 30);     // 慢波：周期长，肉眼是缓动不是抖动
        Assert.NotEqual(AquariumWave.FrontPeriodDips, AquariumWave.BackPeriodDips); // 异周期出视差
    }
}
