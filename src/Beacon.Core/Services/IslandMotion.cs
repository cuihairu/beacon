namespace Beacon.Core.Services;

/// <summary>
/// 悬浮岛开合动效（USER-RPT-4，BrimDeck PanelMotion/Curves 口径照抄）：
/// 展开时长 360ms（BrimDeck AnimationDuration 默认）、收起 0.75×（CloseFactor）=270ms；
/// 展开曲面走 Bounce（快攻+微过冲 ~0.3% 落定，reduced 档退化为 Out 无过冲），
/// 收起曲面走 Drawer（离弦即走、软着陆 .32,.72,0,1）且延迟 0.12 拍（CloseSurfaceDelay，
/// 内容先撤、曲面随后）；内容淡入出按行程占比（in .12–.80，out 0–.30）。
/// 纯函数 Core 层：无 Win32/Composition 依赖，Core.Tests 锁参数。
/// </summary>
public static class IslandMotion
{
    /// <summary>BrimDeck Settings.AnimationDuration 默认（展开全长）。</summary>
    public const int OpenMs = 360;

    /// <summary>BrimDeck PanelMotion.CloseFactor=.75：收起用展开的四分之三时长。</summary>
    public const double CloseFactor = 0.75;

    /// <summary>收起时长（=270ms）。</summary>
    public static int CloseMs => (int)Math.Round(OpenMs * CloseFactor);

    /// <summary>BrimDeck Curves.BounceDamping/BounceDecay：过冲 ~0.32%、包络尾部 0.2%。</summary>
    public const double BounceDamping = 0.91;
    public const double BounceDecay = 6.2;

    /// <summary>内容淡入的行程占比窗（BrimDeck content track 展开段 .12→.80）。</summary>
    public const double ContentInStart = 0.12;
    public const double ContentInEnd = 0.80;

    /// <summary>内容淡出的行程占比（BrimDeck content track 收起段 0→.30）。</summary>
    public const double ContentOutEnd = 0.30;

    /// <summary>收起时曲面启动延迟（BrimDeck PanelMotion.CloseSurfaceDelay=.12）。</summary>
    public const double CloseSurfaceDelay = 0.12;

    private static double BounceOmega => BounceDecay / BounceDamping * Math.Sqrt(1 - BounceDamping * BounceDamping);

    private static double BouncePath(double x) => 1 - Math.Exp(-BounceDecay * x) * Math.Cos(BounceOmega * x);

    private static readonly double BounceEnd = BouncePath(1);

    /// <summary>BrimDeck Curves.Bounce：1-e^(-decay x)·cos(ωx) 归一到终点（终值恰为 1，不跳帧）。</summary>
    public static double Bounce(double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        return BouncePath(x) / BounceEnd;
    }

    /// <summary>BrimDeck Soft.Out (.22,.8,.28,1)：快起长尾软着陆（reduced 档展开用，无过冲）。</summary>
    public static double Out(double x) => Bezier(0.22, 0.8, 0.28, 1.0, x);

    /// <summary>BrimDeck Soft.Drawer (.32,.72,0,1)：收起曲面——离弦即走、软着陆无回弹。</summary>
    public static double Drawer(double x) => Bezier(0.32, 0.72, 0.0, 1.0, x);

    /// <summary>CSS cubic-bezier(x1,y1,x2,y2) 求值（BrimDeck Curves.Bezier 同法：Newton 步+二分兜底）。</summary>
    public static double Bezier(double x1, double y1, double x2, double y2, double x)
    {
        x = Math.Clamp(x, 0, 1);
        double t = x;
        for (int i = 0; i < 8; i++)
        {
            double slope = BezierSlope(t, x1, x2);
            if (Math.Abs(slope) < 1e-6) break;
            t = Math.Clamp(t - (BezierSample(t, x1, x2) - x) / slope, 0, 1);
        }
        if (Math.Abs(BezierSample(t, x1, x2) - x) > 1e-5)
        {
            double low = 0, high = 1;
            for (int i = 0; i < 30; i++)
            {
                t = (low + high) / 2;
                if (BezierSample(t, x1, x2) < x) low = t; else high = t;
            }
        }
        return BezierSample(t, y1, y2);
    }

    private static double BezierSample(double t, double a1, double a2)
        => ((1 - 3 * a2 + 3 * a1) * t + (3 * a2 - 6 * a1)) * t * t + 3 * a1 * t;

    private static double BezierSlope(double t, double a1, double a2)
        => 3 * (1 - 3 * a2 + 3 * a1) * t * t + 2 * (3 * a2 - 6 * a1) * t + 3 * a1;
}
