using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>IslandSurfaceOutline：岛单连续曲面轮廓（BrimDeck IslandBorder.BuildOutline 对拍口径）。
/// 贴顶铁律：顶缘 (0,0)→(w,0) 横贯全宽、上两角 0；肩 16/肩深 7；底角 12；左右镜像。</summary>
public sealed class IslandSurfaceOutlineTests
{
    private static double Shoulder => CapsuleIslandMath.ShoulderDips;      // 16
    private static double Depth => CapsuleIslandMath.ShoulderDepthDips;    // 7
    private static double Bottom => CapsuleIslandMath.BottomRadiusDips;    // 12

    private static IReadOnlyList<IslandSurfaceOutline.Seg> Build(double w = 141, double h = 32)
        => IslandSurfaceOutline.Build(w, h, Shoulder, Bottom);

    [Fact]
    public void DegenerateSize_ReturnsEmpty()
    {
        Assert.Empty(IslandSurfaceOutline.Build(0, 32, Shoulder, Bottom));
        Assert.Empty(IslandSurfaceOutline.Build(141, 0, Shoulder, Bottom));
        Assert.Empty(IslandSurfaceOutline.Build(-5, -5, Shoulder, Bottom));
    }

    [Fact]
    public void TopEdge_SpansFullWidth_AtScreenTop()
    {
        // 贴屏幕顶的读法根基：起点 (0,0)，第一段直线到 (w,0)——岛与屏顶是一条连续边，无上圆角
        var segs = Build(141);
        var move = Assert.IsType<IslandSurfaceOutline.Move>(segs[0]);
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), move.To);
        var top = Assert.IsType<IslandSurfaceOutline.Line>(segs[1]);
        Assert.Equal(new IslandSurfaceOutline.Point(141, 0), top.To);
    }

    [Fact]
    public void Shoulder_ReachesBodyAtDepth()
    {
        // 右肩两段三次：第一段终点在 x = w - shoulder×0.82、y = depth×0.18；第二段终点在主体右缘 (w-shoulder, depth)
        var segs = Build();
        var s1 = Assert.IsType<IslandSurfaceOutline.Cubic>(segs[2]);
        Assert.Equal(Shoulder, Depth * 16 / 7); // 肩深恒 = 肩×7/16（BrimDeck BuildOutline）
        Assert.Equal(141 - Shoulder * 0.82, s1.To.X, 9);
        Assert.Equal(Depth * 0.18, s1.To.Y, 9);
        var s2 = Assert.IsType<IslandSurfaceOutline.Cubic>(segs[3]);
        Assert.Equal(141 - Shoulder, s2.To.X, 9);
        Assert.Equal(Depth, s2.To.Y, 9);
        // 肩与直线边相切：两段控制点都落在顶缘 y=0 / 主体右缘 x=w-shoulder 上
        Assert.Equal(0, s1.C1.Y);
        Assert.Equal(0, s1.C2.Y);
        Assert.Equal(141 - Shoulder, s2.C1.X);
        Assert.Equal(141 - Shoulder, s2.C2.X);
    }

    [Fact]
    public void Contour_IsClosedAndStaysInsideSurfaceBounds()
    {
        // 闭合：首点 (0,0)、末段终点回 (0,0)；且所有端点/控制点都在曲面 [0,w]×[0,h] 内
        // （轮廓绝不出窗——贴顶岛的顶边就是窗口上缘，越界即渲染裁切出毛边）
        foreach (var (w, h) in new[] { (141d, 32d), (72d, 32d), (320d, 96d), (40d, 16d) })
        {
            var segs = IslandSurfaceOutline.Build(w, h, Shoulder, Bottom);
            Assert.True(segs.Count >= 7);
            Assert.Equal(new IslandSurfaceOutline.Point(0, 0), ((IslandSurfaceOutline.Move)segs[0]).To);
            Assert.Single(segs.OfType<IslandSurfaceOutline.Move>()); // 段序隐含连接，不得再出现 Move

            foreach (var seg in segs)
            {
                foreach (var p in PointsOf(seg))
                {
                    Assert.InRange(p.X, -0.001, w + 0.001);
                    Assert.InRange(p.Y, -0.001, h + 0.001);
                }
            }

            var last = segs[^1] switch
            {
                IslandSurfaceOutline.Line l => l.To,
                IslandSurfaceOutline.Cubic c => c.To,
                _ => default,
            };
            Assert.Equal(new IslandSurfaceOutline.Point(0, 0), last);
        }

        static IEnumerable<IslandSurfaceOutline.Point> PointsOf(IslandSurfaceOutline.Seg seg) => seg switch
        {
            IslandSurfaceOutline.Move m => [m.To],
            IslandSurfaceOutline.Line l => [l.To],
            IslandSurfaceOutline.Cubic c => [c.C1, c.C2, c.To],
            _ => [],
        };
    }

    [Fact]
    public void Contour_IsMirrorSymmetric()
    {
        // 左右完全镜像（BrimDeck BuildOutline：左肩 = 右肩镜像后反向走一遍）。
        // 按「带起点的曲线」比对：镜像后的右肩 S1 == 反向的左 S2，右 S2 == 反向左 S1，底角同理。
        const double w = 141;
        var curves = Curves(Build(w));
        Assert.Equal(6, curves.Count); // 右肩 2 + 底角 2 + 左肩 2

        AssertSame(Mirror(curves[0], w), Reverse(curves[5])); // 右 S1（顶缘→肩窝）↔ 左 S2
        AssertSame(Mirror(curves[1], w), Reverse(curves[4])); // 右 S2（肩窝→主体）↔ 左 S1
        AssertSame(Mirror(curves[2], w), Reverse(curves[3])); // 右底角 ↔ 左底角

        // 口径抽查：右肩末点 = (w-肩, 肩深)；右底角终点 = (w-肩-底角, h)
        Assert.Equal(new IslandSurfaceOutline.Point(w - Shoulder, Depth), curves[1].End);
        Assert.Equal(new IslandSurfaceOutline.Point(w - Shoulder - Bottom, 32), curves[2].End);
    }

    /// <summary>段表 → 带起点的三次曲线列表（起点 = 上一段终点）。</summary>
    private static List<(IslandSurfaceOutline.Point Start, IslandSurfaceOutline.Point C1, IslandSurfaceOutline.Point C2, IslandSurfaceOutline.Point End)> Curves(
        IReadOnlyList<IslandSurfaceOutline.Seg> segs)
    {
        var curves = new List<(IslandSurfaceOutline.Point, IslandSurfaceOutline.Point, IslandSurfaceOutline.Point, IslandSurfaceOutline.Point)>();
        var cursor = default(IslandSurfaceOutline.Point);
        foreach (var seg in segs)
        {
            switch (seg)
            {
                case IslandSurfaceOutline.Move move:
                    cursor = move.To;
                    break;
                case IslandSurfaceOutline.Line line:
                    cursor = line.To;
                    break;
                case IslandSurfaceOutline.Cubic cubic:
                    curves.Add((cursor, cubic.C1, cubic.C2, cubic.To));
                    cursor = cubic.To;
                    break;
            }
        }

        return curves;
    }

    private static (IslandSurfaceOutline.Point Start, IslandSurfaceOutline.Point C1, IslandSurfaceOutline.Point C2, IslandSurfaceOutline.Point End) Mirror(
        (IslandSurfaceOutline.Point Start, IslandSurfaceOutline.Point C1, IslandSurfaceOutline.Point C2, IslandSurfaceOutline.Point End) c, double w)
        => (Mirror(c.Start, w), Mirror(c.C1, w), Mirror(c.C2, w), Mirror(c.End, w));

    private static IslandSurfaceOutline.Point Mirror(IslandSurfaceOutline.Point p, double w) => new(w - p.X, p.Y);

    private static (IslandSurfaceOutline.Point Start, IslandSurfaceOutline.Point C1, IslandSurfaceOutline.Point C2, IslandSurfaceOutline.Point End) Reverse(
        (IslandSurfaceOutline.Point Start, IslandSurfaceOutline.Point C1, IslandSurfaceOutline.Point C2, IslandSurfaceOutline.Point End) c)
        => (c.End, c.C2, c.C1, c.Start);

    private static void AssertSame(
        (IslandSurfaceOutline.Point Start, IslandSurfaceOutline.Point C1, IslandSurfaceOutline.Point C2, IslandSurfaceOutline.Point End) a,
        (IslandSurfaceOutline.Point Start, IslandSurfaceOutline.Point C1, IslandSurfaceOutline.Point C2, IslandSurfaceOutline.Point End) b)
    {
        Assert.Equal(a.Start.X, b.Start.X, 9);
        Assert.Equal(a.Start.Y, b.Start.Y, 9);
        Assert.Equal(a.C1.X, b.C1.X, 9);
        Assert.Equal(a.C1.Y, b.C1.Y, 9);
        Assert.Equal(a.C2.X, b.C2.X, 9);
        Assert.Equal(a.C2.Y, b.C2.Y, 9);
        Assert.Equal(a.End.X, b.End.X, 9);
        Assert.Equal(a.End.Y, b.End.Y, 9);
    }

    [Fact]
    public void BottomCorner_IsClampedLikeBrimDeck()
    {
        // BrimDeck limit = min(h-肩深, h/2, (right-left)/2)：底角 12 在常规高度直接用 12；
        // 低矮曲面（h=16）limit=min(12.5,8,(w-2×8)/2…) → 底角被压到 8，不越半高
        var tall = IslandSurfaceOutline.Build(141, 32, Shoulder, Bottom);
        var brCorner = tall.OfType<IslandSurfaceOutline.Cubic>().First(c => c.To.Y == 32);
        Assert.Equal(141 - Shoulder - Bottom, brCorner.To.X, 9);

        var low = IslandSurfaceOutline.Build(40, 16, Shoulder, Bottom);
        var lowShoulder = Math.Min(Math.Min(Shoulder, 40d / 4), 16d / 2); // clamp 16 → min(10, 8) = 8
        var lowLimit = Math.Min(Math.Min(16 - lowShoulder * IslandSurfaceOutline.ShoulderDepthRatio, 16d / 2),
            (40 - 2 * lowShoulder) / 2);
        var lowCorner = low.OfType<IslandSurfaceOutline.Cubic>().First(c => c.To.Y == 16);
        Assert.Equal(8, lowLimit, 9);                                  // limit 压到半高 8
        Assert.Equal(40 - lowShoulder - lowLimit, lowCorner.To.X, 9);   // 底角半径被压到 limit（12 → 8）
    }

    [Fact]
    public void NoShoulder_Case_IsSquareTopRectangle()
    {
        // shoulder 请求 0：肩部三次段缺席，主体横贯全宽（仅底角圆角）
        var segs = IslandSurfaceOutline.Build(200, 32, 0, Bottom);
        Assert.DoesNotContain(segs, s => s is IslandSurfaceOutline.Cubic c && c.To.Y is > 0 and < 16 && c.To.X < 200 && c.To.X > 0);
        var top = Assert.IsType<IslandSurfaceOutline.Line>(segs[1]);
        Assert.Equal(200, top.To.X);
        Assert.Equal(200, segs.OfType<IslandSurfaceOutline.Cubic>().First().C1.X, 9); // 右底角起于右缘
    }

    [Fact]
    public void Constants_MatchBrimDeckBuildOutline()
    {
        Assert.Equal(0.5522847498307936, IslandSurfaceOutline.CircleK); // 圆象限三次近似 k
        Assert.Equal(7d / 16d, IslandSurfaceOutline.ShoulderDepthRatio);  // shoulderDepth = shoulder × 7/16
    }
}
