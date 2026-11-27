using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>IslandSurfaceOutline：岛单连续曲面轮廓（BrimDeck IslandBorder.BuildOutline 对拍口径）。
/// 生产口径（2026-11-28 用户令恢复 BrimDeck 正形）：肩 16——顶缘 (0,0)→(w,0) 横贯全宽贴屏，
/// 两角向内弯喇叭肩（两段 C2 连接三次、肩深=肩×7/16=7），肩下主体两侧内收 16 DIP，底角 12 外凸。
/// 无肩分支（topRadius 弧 / radius 0 方角）仅 API 兼容路径，显式参数锁。</summary>
public sealed class IslandSurfaceOutlineTests
{
    private static double Shoulder => CapsuleIslandMath.ShoulderDips;      // 16（2026-11-28 恢复 BrimDeck）
    private static double Depth => CapsuleIslandMath.ShoulderDepthDips;    // 7
    private static double Bottom => CapsuleIslandMath.BottomRadiusDips;    // 12
    private static double Top => CapsuleIslandMath.TopRadiusDips;          // 0（肩>0 时被肩部覆盖）

    private static IReadOnlyList<IslandSurfaceOutline.Seg> Build(double w = 141, double h = 32)
        => IslandSurfaceOutline.Build(w, h, Shoulder, Bottom, Top);

    [Fact]
    public void DegenerateSize_ReturnsEmpty()
    {
        Assert.Empty(IslandSurfaceOutline.Build(0, 32, Shoulder, Bottom, Top));
        Assert.Empty(IslandSurfaceOutline.Build(141, 0, Shoulder, Bottom, Top));
        Assert.Empty(IslandSurfaceOutline.Build(-5, -5, Shoulder, Bottom, Top));
    }

    [Fact]
    public void TopEdge_FullWidthSquareCorners_LikeBrimDeckShoulderBranch()
    {
        // BrimDeck 正形（用户 2026-11-28「上面的角向内弯」）：顶缘 (0,0)→(w,0) 横贯全宽贴屏顶，
        // 顶角是方的（被肩部覆盖）；紧随右肩两段 C2 三次——第一段起点控制点在顶缘上（相切）
        var segs = Build(141);
        var move = Assert.IsType<IslandSurfaceOutline.Move>(segs[0]);
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), move.To);
        var top = Assert.IsType<IslandSurfaceOutline.Line>(segs[1]);
        Assert.Equal(new IslandSurfaceOutline.Point(141, 0), top.To);
        var s1 = Assert.IsType<IslandSurfaceOutline.Cubic>(segs[2]);
        Assert.Equal(141 - Shoulder * 0.28, s1.C1.X, 9);
        Assert.Equal(0, s1.C1.Y, 9);                                           // 起步切顶缘（曲率 0）
        Assert.Equal(141 - Shoulder * 0.82, s1.To.X, 9);
        Assert.Equal(Depth * 0.18, s1.To.Y, 9);
        var s2 = Assert.IsType<IslandSurfaceOutline.Cubic>(segs[3]);
        Assert.Equal(141 - Shoulder, s2.To.X, 9);                              // 落到主体右缘 (w-肩, 肩深)
        Assert.Equal(Depth, s2.To.Y, 9);
        Assert.Equal(141 - Shoulder, s2.C2.X, 9);                              // 收尾切直边（曲率 0）
    }

    [Fact]
    public void Body_SidesInsetByShoulder_BrimDeckFlareChannels()
    {
        // BrimDeck 肩形态本体：肩下主体两侧 x=肩 / x=w-肩（16 DIP 内收=喇叭肩让出的透明通道，
        // BrimDeck WPF 透明窗与 region 镂空观感一致）；曲线 6 条=右肩 2 + 底角 2 + 左肩 2
        var segs = Build(141);
        Assert.Equal(6, segs.OfType<IslandSurfaceOutline.Cubic>().Count());
        var lines = segs.OfType<IslandSurfaceOutline.Line>().Select(l => l.To).ToList();
        Assert.Contains(new IslandSurfaceOutline.Point(141, 0), lines);                      // 顶缘全宽
        Assert.Contains(new IslandSurfaceOutline.Point(141 - Shoulder, 32 - Bottom), lines); // 右直边（肩下内收）
        Assert.Contains(new IslandSurfaceOutline.Point(Shoulder, Depth), lines);             // 左直边上端（肩深处）
    }

    [Fact]
    public void Contour_IsClosedAndStaysInsideSurfaceBounds()
    {
        // 闭合：首点 = (0,0)、末段终点回 (0,0)；且所有端点/控制点都在曲面 [0,w]×[0,h] 内
        //（轮廓绝不出窗——越界即渲染裁切出毛边）
        foreach (var (w, h) in new[] { (141d, 32d), (72d, 32d), (320d, 96d), (40d, 16d) })
        {
            var segs = IslandSurfaceOutline.Build(w, h, Shoulder, Bottom, Top);
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
    }

    [Fact]
    public void Contour_IsMirrorSymmetric()
    {
        // 左右完全镜像。曲线六条按序 [右肩上, 右肩下, 底右, 底左, 左肩下, 左肩上]：
        // 右肩两段 ↔ 左肩两段反向镜像；底右 ↔ 底左反向镜像。
        const double w = 141;
        var curves = Curves(Build(w));
        Assert.Equal(6, curves.Count);

        AssertSame(Mirror(curves[0], w), Reverse(curves[5])); // 右肩上段 ↔ 左肩上段
        AssertSame(Mirror(curves[1], w), Reverse(curves[4])); // 右肩下段 ↔ 左肩下段
        AssertSame(Mirror(curves[2], w), Reverse(curves[3])); // 底右弧 ↔ 底左弧

        // 口径抽查：右肩起于顶缘右端 (w,0)、落到 (w-肩, 肩深)；底右弧终点 = (w-肩-底角, h)
        Assert.Equal(new IslandSurfaceOutline.Point(w, 0), curves[0].Start);
        Assert.Equal(new IslandSurfaceOutline.Point(w - Shoulder, Depth), curves[1].End);
        Assert.Equal(w - Shoulder, curves[1].C1.X); // 肩下段控制点贴主体右缘（收尾切直边）
        Assert.Equal(w - Shoulder, curves[1].C2.X);
        Assert.Equal(new IslandSurfaceOutline.Point(w - Shoulder - Bottom, 32), curves[2].End);
        Assert.Equal(w - Shoulder, curves[2].Start.X);
    }

    [Fact]
    public void Shoulder_ClampedLikeBrimDeck()
    {
        // BrimDeck 肩 clamp = min(w/4, h/2)：常规尺寸直接用 16；窄矮曲面（w=40,h=16）压到 8，
        // 肩深随之 3.5，主体内收同步收窄（低矮曲面肩不越界）
        var low = IslandSurfaceOutline.Build(40, 16, Shoulder, Bottom, Top);
        var s2 = Assert.IsType<IslandSurfaceOutline.Cubic>(low[3]);
        Assert.Equal(40 - 8, s2.To.X, 9);
        Assert.Equal(8 * IslandSurfaceOutline.ShoulderDepthRatio, s2.To.Y, 9);
    }

    [Fact]
    public void ShoulderPresent_IgnoresTopRadius_ClassicCapsuleUnchanged()
    {
        // 红线「经典胶囊观感零变化」：肩>0 时顶角参数被忽略——顶缘 (0,0)→(w,0) 横贯全宽、
        // 曲线=肩部 4 + 底角 2，末点回 (0,0)；topRadius 传 12 与传 0 段表逐点一致
        var withTop = IslandSurfaceOutline.Build(141, 32, 16, Bottom, 12);
        var noTop = IslandSurfaceOutline.Build(141, 32, 16, Bottom, 0);
        Assert.Equal(withTop.Count, noTop.Count);
        for (var i = 0; i < withTop.Count; i++)
        {
            Assert.Equal(PointsOf(withTop[i]), PointsOf(noTop[i]));
        }
    }

    [Fact]
    public void NoShoulder_TopRoundedCorners_ApiCompatBranch()
    {
        // API 兼容分支（肩=0）：顶两角走 tl/tr 圆象限弧（BrimDeck 无肩分支同式三段——
        // BeginFigure(tl,0)→Line(w-tr,0)→Bezier 右上角）；radius 0 = 旧方角退化口径
        var segs = IslandSurfaceOutline.Build(141, 32, 0, Bottom, 12);
        var move = Assert.IsType<IslandSurfaceOutline.Move>(segs[0]);
        Assert.Equal(new IslandSurfaceOutline.Point(12, 0), move.To);
        var top = Assert.IsType<IslandSurfaceOutline.Line>(segs[1]);
        Assert.Equal(new IslandSurfaceOutline.Point(141 - 12, 0), top.To);
        var trArc = Assert.IsType<IslandSurfaceOutline.Cubic>(segs[2]);
        Assert.Equal(new IslandSurfaceOutline.Point(141, 12), trArc.To);
        Assert.Equal(4, segs.OfType<IslandSurfaceOutline.Cubic>().Count()); // 顶两角 + 底两角

        var square = IslandSurfaceOutline.Build(200, 32, 0, Bottom);
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), ((IslandSurfaceOutline.Move)square[0]).To);
        Assert.Equal(200, Assert.IsType<IslandSurfaceOutline.Line>(square[1]).To.X);
        Assert.Equal(2, square.OfType<IslandSurfaceOutline.Cubic>().Count());
        var leftEdge = Assert.IsType<IslandSurfaceOutline.Line>(square[^1]);
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), leftEdge.To);
    }

    [Fact]
    public void BottomCorner_IsClampedLikeBrimDeck()
    {
        // BrimDeck limit = min(h-肩深, h/2, (right-left)/2)：常规高度底角 12 直接用；
        // 低矮曲面（w=40,h=16，肩被夹到 8）limit=min(16-3.5, 8, 12)=8 → 底角压到 8
        var tall = IslandSurfaceOutline.Build(141, 32, Shoulder, Bottom);
        var brCorner = tall.OfType<IslandSurfaceOutline.Cubic>().First(c => c.To.Y == 32);
        Assert.Equal(141 - Shoulder - Bottom, brCorner.To.X, 9);

        var low = IslandSurfaceOutline.Build(40, 16, Shoulder, Bottom);
        var lowShoulder = Math.Min(Shoulder, Math.Min(40d / 4, 16d / 2)); // 16 → min(10, 8) = 8
        var lowLimit = Math.Min(Math.Min(16 - lowShoulder * IslandSurfaceOutline.ShoulderDepthRatio, 16d / 2),
            (40 - 2 * lowShoulder) / 2);
        var lowCorner = low.OfType<IslandSurfaceOutline.Cubic>().First(c => c.To.Y == 16);
        Assert.Equal(8, lowLimit, 9);                                        // limit 压到半高 8
        Assert.Equal(40 - lowShoulder - lowLimit, lowCorner.To.X, 9);        // 底角半径被压到 limit（12 → 8）
    }

    [Fact]
    public void Constants_MatchBrimDeckBuildOutline()
    {
        Assert.Equal(0.5522847498307936, IslandSurfaceOutline.CircleK); // 圆象限三次近似 k
        Assert.Equal(7d / 16d, IslandSurfaceOutline.ShoulderDepthRatio);  // shoulderDepth = shoulder × 7/16
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

    private static IEnumerable<IslandSurfaceOutline.Point> PointsOf(IslandSurfaceOutline.Seg seg) => seg switch
    {
        IslandSurfaceOutline.Move m => [m.To],
        IslandSurfaceOutline.Line l => [l.To],
        IslandSurfaceOutline.Cubic c => [c.C1, c.C2, c.To],
        _ => [],
    };
}
