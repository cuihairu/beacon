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
        // 2026-11-17 横灌改版（前沿转竖直）重定标：桶高 32 DIP，振幅 ≤ 1/10 桶高仍是「小幅」；
        // 且 ≥3 是 CI 列采样的波峰在场硬断言下限（1px 分辨率下领先幅 <3 DIP 无全色像素可断）
        // BEACON-AQUA-002：默认振幅提到 4（仍在 [可证下限 3, 自适应上限 6] 带内）
        Assert.InRange(AquariumWave.FrontAmplitudeDips, AquariumWave.AmplitudeFloorDips, AquariumWave.AmplitudeCeilingDips);
        Assert.True(AquariumWave.FrontPeriodDips >= 30);     // 慢波：周期长，肉眼是缓动不是抖动
        Assert.NotEqual(AquariumWave.FrontPeriodDips, AquariumWave.BackPeriodDips); // 异周期出视差
    }

    [Fact]
    public void Constants_Aqua002_FasterDriftAndHighlightCycle() // AQUA-002 提速锚：前层最快、后层次之（视差反差保留），高光 6s 间歇
    {
        Assert.Equal(2200, AquariumWave.FrontDriftMilliseconds);
        Assert.Equal(3400, AquariumWave.BackDriftMilliseconds);
        Assert.True(AquariumWave.FrontDriftMilliseconds < AquariumWave.BackDriftMilliseconds); // 前层比后层快：速差=活水
        Assert.Equal(6000, AquariumWave.HighlightCycleMilliseconds);
    }

    [Fact]
    public void AdaptiveAmplitude_LowTiles_ClampAtProvableFloor() // 下限 3=CI 列采样可证下限：spec 原值 2 被 3 压制（AQUA-002 拍板）
    {
        Assert.Equal(3.0, AquariumWave.AdaptiveAmplitudeDips(16), 9); // 16/8=2.0 → 抬到 3
        Assert.Equal(3.0, AquariumWave.AdaptiveAmplitudeDips(24), 9); // 24/8=3.0 → 恰在下限
        Assert.Equal(3.0, AquariumWave.AdaptiveAmplitudeDips(0), 9);  // 退化输入也守下限
        Assert.Equal(3.0, AquariumWave.AdaptiveAmplitudeDips(-40), 9); // 负数（未测量）同口径
    }

    [Fact]
    public void AdaptiveAmplitude_LargeTiles_ClampAtCeiling() // 上限 6：大桶不回到「幅度大抢眼」反面
    {
        Assert.Equal(6.0, AquariumWave.AdaptiveAmplitudeDips(80), 9);  // 80/8=10 → 压到 6
        Assert.Equal(6.0, AquariumWave.AdaptiveAmplitudeDips(48), 9);  // 48/8=6.0 → 恰在上限
    }

    [Fact]
    public void AdaptiveAmplitude_Midrange_Proportional() // 带内线性：桶高/8 直通（钳位只在带外生效）
    {
        Assert.Equal(4.0, AquariumWave.AdaptiveAmplitudeDips(32), 9); // 与默认振幅同值：32 DIP 桶是标定桶
        Assert.Equal(5.0, AquariumWave.AdaptiveAmplitudeDips(40), 9);
    }

    [Fact]
    public void AdaptivePeriod_SmallAndWideTiles_Clamped() // 周期钳位 [28, 56]：小桶不挤成锯齿、大桶不摊平
    {
        Assert.Equal(28.0, AquariumWave.AdaptivePeriodDips(60), 9);   // 60/3=20 → 抬到 28
        Assert.Equal(28.0, AquariumWave.AdaptivePeriodDips(84), 9);   // 84/3=28 → 恰在下限
        Assert.Equal(56.0, AquariumWave.AdaptivePeriodDips(240), 9);  // 240/3=80 → 压到 56
        Assert.Equal(56.0, AquariumWave.AdaptivePeriodDips(168), 9);  // 168/3=56 → 恰在上限
    }

    [Fact]
    public void AdaptivePeriod_NominalTile_MatchesDefaultPeriod() // 标定桶宽 120 → 40=FrontPeriodDips：自适应对常规桶不改观感
    {
        Assert.Equal(AquariumWave.FrontPeriodDips, AquariumWave.AdaptivePeriodDips(120));
    }

    [Fact]
    public void AdaptiveValues_ProduceSeamlessCrest() // 自适应值喂回波形：无缝循环不变式仍成立（漂移循环方案的前提）
    {
        const double amp = 5.25, period = 37.5; // 带内非整值：钳位与采样解耦的证明
        Assert.True(AquariumWave.IsSeamlessAt(0, period, amp, 0));
        Assert.True(AquariumWave.IsSeamlessAt(period * 0.37, period, amp, AquariumWave.BackPhaseTurns));
        var points = AquariumWave.Crest(96, period, amp, 0);
        Assert.Equal(points[0].Y, points[AquariumWave.SegmentsPerPeriod].Y, 9);
    }
}
