namespace Beacon.Core.Services;

/// <summary>
/// 水族箱波纹几何（AQUA-001）：水面 = 正弦折线（PoolGeometry 同纪律——贝塞尔方向歧义在 CI 才现形，
/// 折线零歧义且 Linux 可单测）。两层波相位错开叠加出活水感；波形以周期 P 平移后与原波形重合，
/// 所以路径宽 = 通栏宽 + P、动画只漂移 −P 即无缝循环（App 侧消费本函数生成 XAML PathGeometry）。
/// Y 向下为正（XAML 同向）；y = A·sin(2πx/P + 2π·phase)。
/// </summary>
public static class AquariumWave
{
    /// <summary>前层波周期（DIP）：漂移一个周期即无缝循环。</summary>
    public const double FrontPeriodDips = 40;

    /// <summary>前层波振幅（DIP）默认值（桶度量不可用时兜底）：BEACON-AQUA-002 提到 4，仍守住
    /// 2026-11-17 横灌改版的 CI 列采样可证下限——1px 物理分辨率下前沿领先幅 ≥3 DIP 才有全色像素
    /// （波峰在场硬断言依赖，防「差分 0 两义」降级洗白波层缺席回归）。运行时按桶度量自适应走
    /// AdaptiveAmplitudeDips。</summary>
    public const double FrontAmplitudeDips = 4.0;

    /// <summary>自适应振幅下限（DIP）：CI 列采样可证下限（同上），clamp 再小也不破波峰断言。</summary>
    public const double AmplitudeFloorDips = 3.0;

    /// <summary>自适应振幅上限（DIP）：大桶波幅封顶，不回到「幅度小、频率慢」的用户令反面。</summary>
    public const double AmplitudeCeilingDips = 6.0;

    /// <summary>自适应周期下限（DIP）。</summary>
    public const double PeriodFloorDips = 28;

    /// <summary>自适应周期上限（DIP）。</summary>
    public const double PeriodCeilingDips = 56;

    /// <summary>前层漂移一个周期的时长（ms）：BEACON-AQUA-002 由 3800 提速到 2200（波感更活）。</summary>
    public const double FrontDriftMilliseconds = 2200;

    /// <summary>后层漂移一个周期的时长（ms）：BEACON-AQUA-002 由 5400 提速到 3400，与前层速差保留视差。</summary>
    public const double BackDriftMilliseconds = 3400;

    /// <summary>水面高光扫过循环周期（ms）：约每 6s 扫过一遍、其余时间隐没（间歇不抢眼）。</summary>
    public const double HighlightCycleMilliseconds = 6000;

    /// <summary>后层波周期（DIP）：与前层异周期，漂移速度差出视差。</summary>
    public const double BackPeriodDips = 34;

    /// <summary>后层波振幅（DIP）。</summary>
    public const double BackAmplitudeDips = 1.2;

    /// <summary>后层波相位（周期份数，π/3）：两层相位错开才叠得出活水，不糊成一条。</summary>
    public const double BackPhaseTurns = 1.0 / 6.0;

    /// <summary>每周期折线段数：16 段/周期在 4 DIP 振幅下视觉连续，点数预算可控。</summary>
    public const int SegmentsPerPeriod = 16;

    /// <summary>自适应振幅（DIP）：amp = clamp(tileH/8, 3, 6)——桶越高波幅越大、上限封顶不抢眼，
    /// 下限 3 守 CI 列采样可证性（BEACON-AQUA-002；spec 原值 2 被可证性要求抬到 3）。
    /// tileH 取波形所在桶的纵向跨度（横灌后波沿纵向起伏）。</summary>
    public static double AdaptiveAmplitudeDips(double tileHeightDips)
        => Math.Clamp(tileHeightDips / 8.0, AmplitudeFloorDips, AmplitudeCeilingDips);

    /// <summary>自适应周期（DIP）：P = clamp(tileW/3, 28, 56)——桶越宽周期越长（波峰更疏朗），
    /// 钳位保证小桶不挤成锯齿、大桶不摊平成直线（BEACON-AQUA-002）。tileW 取桶身横向跨度。</summary>
    public static double AdaptivePeriodDips(double tileWidthDips)
        => Math.Clamp(tileWidthDips / 3.0, PeriodFloorDips, PeriodCeilingDips);

    /// <summary>水面波形采样点（x=0 起，周期对齐步进 period/SegmentsPerPeriod，覆盖 ≥ widthDips）：
    /// 无缝循环只在这种采样下成立——按宽均匀劈宽采样在宽非周期整倍数时末段漂相（43729489131 修复）。
    /// 返回点数 = ceil(widthDips/periodDips)·SegmentsPerPeriod + 1；末点 X 为 period 整倍数且 ≥
    /// widthDips（越界部分由 App 侧 Clip 裁掉）。</summary>
    public static (double X, double Y)[] Crest(double widthDips, double periodDips, double amplitudeDips, double phaseTurns)
    {
        var periods = Math.Max(1, (int)Math.Ceiling(widthDips / periodDips));
        var segments = periods * SegmentsPerPeriod;
        var points = new (double X, double Y)[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            points[i] = (periodDips * i / SegmentsPerPeriod,
                amplitudeDips * Math.Sin(2 * Math.PI * i / SegmentsPerPeriod + 2 * Math.PI * phaseTurns));
        }
        return points;
    }

    /// <summary>波形平移一个周期后与自身重合（无缝循环的数学根据）：x 与 x+period 处 Y 相等。</summary>
    public static bool IsSeamlessAt(double x, double periodDips, double amplitudeDips, double phaseTurns)
        => Math.Abs(
               amplitudeDips * Math.Sin(2 * Math.PI * x / periodDips + 2 * Math.PI * phaseTurns)
               - amplitudeDips * Math.Sin(2 * Math.PI * (x + periodDips) / periodDips + 2 * Math.PI * phaseTurns))
           < 1e-9;
}
