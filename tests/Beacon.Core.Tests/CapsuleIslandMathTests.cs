using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>CapsuleIslandMath：悬浮岛刘海式贴顶几何（USER-RPT-4，BrimDeck Notch 口径）。
/// 贴顶铁律：y 恒 = 屏幕顶（拖动 Y 锁死、旧持久化 y 一律作废）；只水平偏移可持久化。</summary>
public sealed class CapsuleIslandMathTests
{
    private static readonly PinRect Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void Place_DefaultOffset_IsFlushTopCentered()
    {
        // 200px 宽窗口：中心对齐 960 → x=860；y=屏幕顶 0（BrimDeck Notch：surface.Y=0）
        var rect = CapsuleIslandMath.Place(Screen, CapsuleIslandMath.DefaultOffset, dpi: 1, widthPx: 200, heightPx: 32);

        Assert.Equal(860, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal((860 + 100) * 2, rect.X * 2 + rect.Width); // 中心恒等式：x + 半宽 = 屏幕中心
    }

    [Fact]
    public void Place_OffsetShiftsHorizontallyOnly_YStaysZero()
    {
        // 旧口径的垂直偏移被作废：Y 恒贴屏幕顶（岛=刘海，没有「下移 169px」这种状态）
        var rect = CapsuleIslandMath.Place(Screen, new PinOffset(100, 169), dpi: 1, widthPx: 200, heightPx: 32);

        Assert.Equal(760, rect.X);
        Assert.Equal(0, rect.Y);
    }

    [Fact]
    public void Place_ScalesOffsetWithDpi()
    {
        var screen = new PinRect(0, 0, 3000, 2000);
        var rect = CapsuleIslandMath.Place(screen, new PinOffset(100, 40), dpi: 1.5, widthPx: 200, heightPx: 32);

        Assert.Equal(1250, rect.X);   // 中心 1500 - 半宽 100 - 150
        Assert.Equal(0, rect.Y);      // 贴顶不随 DPI 漂
    }

    [Fact]
    public void Place_ClampsHorizontally_WhenOffsetWouldEscape()
    {
        // 巨大偏移把窗口推出左缘：夹回屏幕（margin 8）
        var rect = CapsuleIslandMath.Place(Screen, new PinOffset(5000, 0), dpi: 1, widthPx: 200, heightPx: 32);

        Assert.Equal(8, rect.X);
        Assert.Equal(0, rect.Y);
    }

    [Fact]
    public void Locate_Place_RoundTrips_XOnly()
    {
        var placed = CapsuleIslandMath.Place(Screen, new PinOffset(120, 0), dpi: 1, widthPx: 240, heightPx: 36);

        var offset = CapsuleIslandMath.Locate(Screen, placed, dpi: 1);

        Assert.Equal(120, offset.X);
        Assert.Equal(0, offset.Y); // Y 恒 0：拖动落点不记垂直分量
    }

    [Fact]
    public void Locate_RightOfCenter_RecordsZero()
    {
        // 拖过中心线（窗口中心在屏幕中心右侧）：负偏移按 0 记，恢复时居中贴回
        var rightOfCenter = new PinRect(Screen.X + Screen.Width / 2, 0, 200, 32);

        var offset = CapsuleIslandMath.Locate(Screen, rightOfCenter, dpi: 1);

        Assert.Equal(0, offset.X);
        Assert.Equal(0, offset.Y);
    }

    [Fact]
    public void Notch_Constants_MatchBrimDeckLimits()
    {
        // BrimDeck CollapsedLimits：NotchShoulder=16、NotchCorner=12、BaseHeight=32、肩深=shoulder×7/16
        Assert.Equal(16, CapsuleIslandMath.ShoulderDips);
        Assert.Equal(12, CapsuleIslandMath.BottomRadiusDips);
        Assert.Equal(32, CapsuleIslandMath.HeightDips);
        Assert.Equal(7, CapsuleIslandMath.ShoulderDepthDips);
        // 内缩按 Notch 口径 30（NotchInset；Capsule 的 16 不适用于贴顶岛），并锁 NotchWidthMin=72
        Assert.Equal(30, CapsuleIslandMath.InsetDips);
        Assert.Equal(72, CapsuleIslandMath.MinWidthDips);
        Assert.Equal(238, CapsuleIslandMath.DefaultWidthDips); // NotchWidthDefault（展开内容区 238-60=178）
        Assert.Equal(178, CapsuleIslandMath.DefaultWidthDips - CapsuleIslandMath.InsetDips * 2);
    }
}

/// <summary>IslandMotion：岛开合动效曲线与时长（BrimDeck PanelMotion/Curves 参数锁）。</summary>
public sealed class IslandMotionTests
{
    [Fact]
    public void Durations_MatchBrimDeck()
    {
        Assert.Equal(360, IslandMotion.OpenMs);                       // AnimationDuration 默认
        Assert.Equal(270, IslandMotion.CloseMs);                      // CloseFactor .75
    }

    [Fact]
    public void Bounce_LandsExactlyAndOvershootsSlightly()
    {
        Assert.Equal(0, IslandMotion.Bounce(0));
        Assert.Equal(1, IslandMotion.Bounce(1));
        // 过冲 ~0.32%（BrimDeck damping .91）：峰值在 ~2/3 行程处
        double peak = 0;
        for (double x = 0; x <= 1; x += 0.001) peak = Math.Max(peak, IslandMotion.Bounce(x));
        Assert.InRange(peak - 1, 0.002, 0.005);
    }

    [Fact]
    public void Drawer_And_Out_AreMonotonicUnitCurves()
    {
        foreach (var curve in new Func<double, double>[] { IslandMotion.Drawer, IslandMotion.Out })
        {
            double prev = 0;
            for (double x = 0; x <= 1.0001; x += 0.01)
            {
                var v = curve(x);
                Assert.InRange(v, prev, 1.0000001); // 单调不越 1
                prev = v;
            }
        }
    }

    [Fact]
    public void ContentTracks_MatchBrimDeck()
    {
        Assert.Equal(0.12, IslandMotion.ContentInStart);  // 展开内容：.12→.80
        Assert.Equal(0.80, IslandMotion.ContentInEnd);
        Assert.Equal(0.30, IslandMotion.ContentOutEnd);   // 收起内容：0→.30
        Assert.Equal(0.12, IslandMotion.CloseSurfaceDelay); // 收起曲面延迟一拍
    }

    [Fact]
    public void Bezier_EndpointsExact()
    {
        Assert.Equal(0, IslandMotion.Bezier(.3, .7, 0, 1, 0));
        Assert.Equal(1, IslandMotion.Bezier(.3, .7, 0, 1, 1));
    }
}
