namespace Beacon.Core.Services;

/// <summary>
/// 悬浮岛单连续曲面轮廓（USER-RPT-4，BrimDeck <c>IslandBorder.BuildOutline</c> 同式对拍重做）：
/// 顶缘贴屏幕顶、横贯全宽（刘海读法：岛与屏顶是一条连续边），两侧肩部曲线向内收出主体（肩 16 DIP、
/// 肩深 = 肩 × 7/16 = 7），底两角 12 DIP 圆角、上两角恒 0（贴顶无上圆角）。
/// 肩部是两段 C2 连接的三次贝塞尔（曲率起止为 0，与直线边相切），底/上圆角用圆象限的
/// 三次近似系数 k = 0.5522847498307936——控制点与 BrimDeck BuildOutline 逐点同式。
/// 纯函数 Core 层（Core.Tests 锁几何口径）：App 层只把段表换成 PathGeometry，不含几何决策。
/// </summary>
public static class IslandSurfaceOutline
{
    /// <summary>圆象限三次贝塞尔近似系数（BrimDeck BuildOutline const k）。</summary>
    public const double CircleK = 0.5522847498307936;

    /// <summary>肩深 / 肩 宽比（BrimDeck BuildOutline shoulderDepth = shoulder × 7/16）。</summary>
    public const double ShoulderDepthRatio = 7d / 16d;

    /// <summary>轮廓点（DIP，相对曲面左上角）。</summary>
    public readonly record struct Point(double X, double Y);

    /// <summary>轮廓段：Move 只作首点，Line 直线边，Cubic 三次贝塞尔（C1/C2 控制点 + 终点）。</summary>
    public abstract record Seg;

    public sealed record Move(Point To) : Seg;

    public sealed record Line(Point To) : Seg;

    public sealed record Cubic(Point C1, Point C2, Point To) : Seg;

    /// <summary>
    /// 生成闭合轮廓段表（首段 Move，其余 Line/Cubic，末点回到首点由调用方闭合）。
    /// 夹取与 BrimDeck 完全同式：肩夹在 [0, min(w/4, h/2)]；底角上限
    /// min(h - 肩深, h/2, (right-left)/2)（肩存在时上半），且恒不超过 (right-left)/2——
    /// 低矮曲面（岛展开收窄）不会因肩部收尾跳变。
    /// 宽或高 ≤ 0 返回空表（App 层据此不画）。
    /// </summary>
    public static IReadOnlyList<Seg> Build(double w, double h, double shoulderDips, double bottomRadiusDips)
    {
        if (!(w > 0) || !(h > 0))
        {
            return [];
        }

        double shoulder = Math.Clamp(shoulderDips, 0, Math.Min(w / 4, h / 2));
        double depth = shoulder * ShoulderDepthRatio;
        double left = shoulder, right = w - shoulder;
        double limit = Math.Max(0, Math.Min(shoulder > 0 ? Math.Min(h - depth, h / 2) : h / 2, (right - left) / 2));
        double br = Math.Clamp(bottomRadiusDips, 0, limit);

        // 贴屏幕顶：顶缘 (0,0)→(w,0) 横贯全宽、上两角恒 0（BrimDeck Notch TopRadius=0）
        var segs = new List<Seg>(10) { new Move(new Point(0, 0)), new Line(new Point(w, 0)) };
        if (shoulder > 0)
        {
            // 肩（右）：两段 C2 连接的三次——曲率起止为 0，与直线边相切（同 BrimDeck）
            segs.Add(new Cubic(
                new Point(w - shoulder * 0.28, 0),
                new Point(w - shoulder * 0.64, 0),
                new Point(w - shoulder * 0.82, depth * 0.18)));
            segs.Add(new Cubic(
                new Point(right, depth * 0.36),
                new Point(right, depth * 0.72),
                new Point(right, depth)));
        }

        segs.Add(new Line(new Point(right, h - br)));
        segs.Add(new Cubic(
            new Point(right, h - br * (1 - CircleK)),
            new Point(right - br * (1 - CircleK), h),
            new Point(right - br, h)));
        segs.Add(new Line(new Point(left + br, h)));
        segs.Add(new Cubic(
            new Point(left + br * (1 - CircleK), h),
            new Point(left, h - br * (1 - CircleK)),
            new Point(left, h - br)));

        if (shoulder > 0)
        {
            segs.Add(new Line(new Point(left, depth)));
            // 肩（左）：右肩的镜像，末点回到 (0,0)（闭合段为零长度，不产生额外边）
            segs.Add(new Cubic(
                new Point(left, depth * 0.72),
                new Point(left, depth * 0.36),
                new Point(shoulder * 0.82, depth * 0.18)));
            segs.Add(new Cubic(
                new Point(shoulder * 0.64, 0),
                new Point(shoulder * 0.28, 0),
                new Point(0, 0)));
        }
        else
        {
            segs.Add(new Line(new Point(0, h - br))); // 无肩：左直边（贴顶方角）
            segs.Add(new Line(new Point(0, 0)));
        }

        return segs;
    }
}
