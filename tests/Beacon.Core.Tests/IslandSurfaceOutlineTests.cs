using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>IslandSurfaceOutline：岛单连续曲面轮廓（BrimDeck IslandBorder.BuildOutline 对拍口径）。
/// 无肩态（生产口径，2026-11-25 肩归零）：顶两角 tl=tr=12 圆象限弧（BEACON-FIX-1126 病灶 A——
/// 旧顶角直角是抄漏的 BrimDeck 无肩分支），顶缘直边仍贴屏幕顶；底角 12；左右镜像。
/// 肩&gt;0（刘海态）顶缘 (0,0)→(w,0) 横贯全宽、上两角被肩部覆盖（经典胶囊观感零变化红线）；
/// topRadius 0 = 旧方角退化口径（API 向后兼容）。</summary>
public sealed class IslandSurfaceOutlineTests
{
    private static double Shoulder => CapsuleIslandMath.ShoulderDips;      // 0（2026-11-25 归零）
    private static double Depth => CapsuleIslandMath.ShoulderDepthDips;    // 0
    private static double Bottom => CapsuleIslandMath.BottomRadiusDips;    // 12
    private static double Top => CapsuleIslandMath.TopRadiusDips;          // 12（2026-11-26 顶角圆弧）

    private static IReadOnlyList<IslandSurfaceOutline.Seg> Build(double w = 141, double h = 32)
        => IslandSurfaceOutline.Build(w, h, Shoulder, Bottom, Top);

    private static double ClampedTop(double w, double h) => Math.Min(Top, Math.Min(w, h) / 2);

    [Fact]
    public void DegenerateSize_ReturnsEmpty()
    {
        Assert.Empty(IslandSurfaceOutline.Build(0, 32, Shoulder, Bottom, Top));
        Assert.Empty(IslandSurfaceOutline.Build(141, 0, Shoulder, Bottom, Top));
        Assert.Empty(IslandSurfaceOutline.Build(-5, -5, Shoulder, Bottom, Top));
    }

    [Fact]
    public void TopEdge_RoundedCorners_LikeBrimDeckNoShoulderBranch()
    {
        // BEACON-FIX-1126 病灶 A（用户令「上角不是直的」）：无肩分支顶两角走 tl/tr 圆象限弧
        // （BrimDeck BeginFigure(tl,0)→LineTo(w-tr,0)→Bezier 右上角三段同式）；
        // 顶缘直边仍在 y=0（窗顶缘恒贴屏幕顶，只是左右上角内圆）
        var segs = Build(141);
        var move = Assert.IsType<IslandSurfaceOutline.Move>(segs[0]);
        Assert.Equal(new IslandSurfaceOutline.Point(Top, 0), move.To);          // 顶左角弧起点
        var top = Assert.IsType<IslandSurfaceOutline.Line>(segs[1]);
        Assert.Equal(new IslandSurfaceOutline.Point(141 - Top, 0), top.To);     // 顶缘直边（两弧间弦）
        var trArc = Assert.IsType<IslandSurfaceOutline.Cubic>(segs[2]);
        Assert.Equal(141 - Top * (1 - IslandSurfaceOutline.CircleK), trArc.C1.X, 9);
        Assert.Equal(0, trArc.C1.Y, 9);
        Assert.Equal(141, trArc.C2.X, 9);
        Assert.Equal(Top * (1 - IslandSurfaceOutline.CircleK), trArc.C2.Y, 9);
        Assert.Equal(new IslandSurfaceOutline.Point(141, Top), trArc.To);       // 落到右缘 (w, tr)
    }

    [Fact]
    public void Sides_TouchWindowEdges_NoShoulderChannels()
    {
        // 2026-11-25 白板病灶收口：肩=0 后左右压窗缘（x=0 / x=w）——旧肩部内收会在窗口两侧留
        // 全高 16 DIP 透明通道，浅色桌面透进来读成「展开态左右白色底板」。顶角圆弧后直边不再
        // 全高（上端让位 12 DIP 弧段），但直线段仍压窗缘、曲线只剩四角。
        var segs = Build(141);
        Assert.Equal(4, segs.OfType<IslandSurfaceOutline.Cubic>().Count()); // 四角各 1（无肩部曲线）
        Assert.Equal(0, Depth);                                             // 肩深随肩归零
        var lines = segs.OfType<IslandSurfaceOutline.Line>().Select(l => l.To).ToList();
        Assert.Contains(new IslandSurfaceOutline.Point(141, 32 - Bottom), lines); // 右直边到底角弧起点
        Assert.Contains(new IslandSurfaceOutline.Point(0, Top), lines);           // 左直边到顶角弧起点
    }

    [Fact]
    public void Contour_IsClosedAndStaysInsideSurfaceBounds()
    {
        // 闭合：首点 = 顶左角弧起点 (tr,0)、末段终点回到同点；且所有端点/控制点都在曲面
        // [0,w]×[0,h] 内（轮廓绝不出窗——越界即渲染裁切出毛边）
        foreach (var (w, h) in new[] { (141d, 32d), (72d, 32d), (320d, 96d), (40d, 16d) })
        {
            var segs = IslandSurfaceOutline.Build(w, h, Shoulder, Bottom, Top);
            Assert.True(segs.Count >= 7);
            var tr = ClampedTop(w, h);
            Assert.Equal(new IslandSurfaceOutline.Point(tr, 0), ((IslandSurfaceOutline.Move)segs[0]).To);
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
            Assert.Equal(new IslandSurfaceOutline.Point(tr, 0), last);
        }
    }

    [Fact]
    public void Contour_IsMirrorSymmetric()
    {
        // 左右完全镜像。曲线四条（顶右/底右/底左/顶左）：顶右=顶左镜像、底右=底左镜像；
        // 左右直边压窗缘 x=0 / x=w（Sides_TouchWindowEdges 另锁）。
        const double w = 141;
        var curves = Curves(Build(w));
        Assert.Equal(4, curves.Count);

        AssertSame(Mirror(curves[0], w), Reverse(curves[3])); // 顶右弧 ↔ 顶左弧
        AssertSame(Mirror(curves[1], w), Reverse(curves[2])); // 底右弧 ↔ 底左弧

        // 口径抽查：顶右弧起于顶缘弦端、落到右缘 (w, tr)；底右弧终点 = (w-底角, h)
        Assert.Equal(new IslandSurfaceOutline.Point(w - Top, 0), curves[0].Start);
        Assert.Equal(new IslandSurfaceOutline.Point(w, Top), curves[0].End);
        Assert.Equal(new IslandSurfaceOutline.Point(w - Bottom, 32), curves[1].End);
        Assert.Equal(w, curves[1].Start.X);
        Assert.Equal(w, curves[1].C1.X);
    }

    [Fact]
    public void TopCorner_IsClampedLikeBrimDeck()
    {
        // BrimDeck 无肩分支顶角 clamp = min(w, h)/2：顶角 12 在常规尺寸直接用 12；
        // 低矮曲面（h=16）压到 8，不越半高
        var tall = IslandSurfaceOutline.Build(141, 32, Shoulder, Bottom, Top);
        Assert.Equal(Top, ((IslandSurfaceOutline.Move)tall[0]).To.X, 9);

        var low = IslandSurfaceOutline.Build(40, 16, Shoulder, Bottom, Top);
        Assert.Equal(8, ClampedTop(40, 16), 9); // min(12, 16/2) = 8
        Assert.Equal(8, ((IslandSurfaceOutline.Move)low[0]).To.X, 9);
        var trArc = Assert.IsType<IslandSurfaceOutline.Cubic>(low[2]);
        Assert.Equal(new IslandSurfaceOutline.Point(40, 8), trArc.To);
    }

    [Fact]
    public void ShoulderPresent_KeepsSquareTop_ClassicCapsuleUnchanged()
    {
        // 红线「经典胶囊观感零变化」：肩>0（刘海态）时顶角参数被忽略——顶缘 (0,0)→(w,0)
        // 横贯全宽、曲线=肩部 4 + 底角 2，末点回 (0,0)
        var segs = IslandSurfaceOutline.Build(141, 32, 16, Bottom, Top);
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), ((IslandSurfaceOutline.Move)segs[0]).To);
        var top = Assert.IsType<IslandSurfaceOutline.Line>(segs[1]);
        Assert.Equal(new IslandSurfaceOutline.Point(141, 0), top.To);
        Assert.Equal(6, segs.OfType<IslandSurfaceOutline.Cubic>().Count());
        var last = (IslandSurfaceOutline.Cubic)segs[^1];
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), last.To);
    }

    [Fact]
    public void TopRadiusZero_IsLegacySquareTop()
    {
        // API 向后兼容：topRadiusDips 缺省 0 = 旧方角口径（起点 (0,0)、顶缘横贯全宽、只底角圆角）
        var segs = IslandSurfaceOutline.Build(200, 32, 0, Bottom);
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), ((IslandSurfaceOutline.Move)segs[0]).To);
        var top = Assert.IsType<IslandSurfaceOutline.Line>(segs[1]);
        Assert.Equal(200, top.To.X);
        Assert.Equal(2, segs.OfType<IslandSurfaceOutline.Cubic>().Count());
        Assert.Equal(200, segs.OfType<IslandSurfaceOutline.Cubic>().First().C1.X, 9); // 右底角起于右缘
        var leftEdge = Assert.IsType<IslandSurfaceOutline.Line>(segs[^1]);
        Assert.Equal(new IslandSurfaceOutline.Point(0, 0), leftEdge.To);
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
        // 低矮曲面（h=16）limit=min(16,8,20) → 底角被压到 8，不越半高（肩 0 后无肩深项）
        var tall = IslandSurfaceOutline.Build(141, 32, Shoulder, Bottom);
        var brCorner = tall.OfType<IslandSurfaceOutline.Cubic>().First(c => c.To.Y == 32);
        Assert.Equal(141 - Shoulder - Bottom, brCorner.To.X, 9);

        var low = IslandSurfaceOutline.Build(40, 16, Shoulder, Bottom);
        var lowShoulder = Math.Min(Math.Min(Shoulder, 40d / 4), 16d / 2); // 肩 0（旧 16 → min(10, 8) = 8）
        var lowLimit = Math.Min(Math.Min(16 - lowShoulder * IslandSurfaceOutline.ShoulderDepthRatio, 16d / 2),
            (40 - 2 * lowShoulder) / 2);
        var lowCorner = low.OfType<IslandSurfaceOutline.Cubic>().First(c => c.To.Y == 16);
        Assert.Equal(8, lowLimit, 9);                                  // limit 压到半高 8
        Assert.Equal(40 - lowShoulder - lowLimit, lowCorner.To.X, 9);   // 底角半径被压到 limit（12 → 8）
    }

    [Fact]
    public void Constants_MatchBrimDeckBuildOutline()
    {
        Assert.Equal(0.5522847498307936, IslandSurfaceOutline.CircleK); // 圆象限三次近似 k
        Assert.Equal(7d / 16d, IslandSurfaceOutline.ShoulderDepthRatio);  // shoulderDepth = shoulder × 7/16
    }

    private static IEnumerable<IslandSurfaceOutline.Point> PointsOf(IslandSurfaceOutline.Seg seg) => seg switch
    {
        IslandSurfaceOutline.Move m => [m.To],
        IslandSurfaceOutline.Line l => [l.To],
        IslandSurfaceOutline.Cubic c => [c.C1, c.C2, c.To],
        _ => [],
    };
}
