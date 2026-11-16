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

    /// <summary>前层波振幅（DIP）：小幅度慢频率，不抢眼（用户令「波纹动画幅度小、频率慢」）。
    /// 2026-11-17 横灌改版（前沿转竖直）：下限=CI 列采样可证性——1px 物理分辨率下前沿领先幅
    /// ≥3 DIP 才有全色像素（波峰在场硬断言依赖，防「差分 0 两义」降级洗白波层缺席回归）。</summary>
    public const double FrontAmplitudeDips = 3.0;

    /// <summary>后层波周期（DIP）：与前层异周期，漂移速度差出视差。</summary>
    public const double BackPeriodDips = 34;

    /// <summary>后层波振幅（DIP）。</summary>
    public const double BackAmplitudeDips = 1.2;

    /// <summary>后层波相位（周期份数，π/3）：两层相位错开才叠得出活水，不糊成一条。</summary>
    public const double BackPhaseTurns = 1.0 / 6.0;

    /// <summary>每周期折线段数：16 段/周期在 4 DIP 振幅下视觉连续，点数预算可控。</summary>
    public const int SegmentsPerPeriod = 16;

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
