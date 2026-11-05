using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>CapsuleIslandMath：悬浮岛顶部居中锚点几何（display-modes-analysis §7 Phase 2）。
/// CapsuleWindow 的岛形态启动恢复与拖动落点持久化共用口径，回归在这里锁（PinLayoutMath 同位）。</summary>
public sealed class CapsuleIslandMathTests
{
    private static readonly PinRect Work = new(0, 0, 1920, 1040);

    [Fact]
    public void Place_DefaultOffset_IsTopCentered()
    {
        // 200px 宽窗口：中心对齐 960 → x=860；默认偏移只下移 8
        var rect = CapsuleIslandMath.Place(Work, CapsuleIslandMath.DefaultOffset, dpi: 1, widthPx: 200, heightPx: 32);

        Assert.Equal(860, rect.X);
        Assert.Equal(8, rect.Y);
        Assert.Equal((860 + 100) * 2, rect.X * 2 + rect.Width); // 中心恒等式：x + 半宽 = 工作区中心
    }

    [Fact]
    public void Place_OffsetShiftsLeftAndDown()
    {
        // X 正值 = 窗口中心在屏幕中心左侧（Locate 互逆语义）
        var rect = CapsuleIslandMath.Place(Work, new PinOffset(100, 40), dpi: 1, widthPx: 200, heightPx: 32);

        Assert.Equal(760, rect.X);
        Assert.Equal(40, rect.Y);
    }

    [Fact]
    public void Place_ScalesOffsetWithDpi()
    {
        var rect = CapsuleIslandMath.Place(Work, new PinOffset(100, 40), dpi: 1.5, widthPx: 200, heightPx: 32);

        Assert.Equal(760 - 50, rect.X);  // 100 DIP × 1.5 = 150px 左移
        Assert.Equal(60, rect.Y);        // 40 DIP × 1.5 = 60px 下移
    }

    [Fact]
    public void Place_ClampsIntoWorkArea_WhenOffsetWouldEscape()
    {
        // 巨大偏移把窗口顶出工作区：ClampInto 回收（margin 8）
        var rect = CapsuleIslandMath.Place(Work, new PinOffset(0, 5000), dpi: 1, widthPx: 200, heightPx: 32);

        Assert.True(rect.Bottom <= Work.Bottom - 8);
        Assert.Equal(860, rect.X);
    }

    [Fact]
    public void Locate_Place_RoundTrips()
    {
        var placed = CapsuleIslandMath.Place(Work, new PinOffset(120, 56), dpi: 1, widthPx: 240, heightPx: 36);

        var offset = CapsuleIslandMath.Locate(Work, placed, dpi: 1);

        Assert.Equal(120, offset.X);
        Assert.Equal(56, offset.Y);
    }

    [Fact]
    public void Locate_RightOfCenter_RecordsZero()
    {
        // 拖过中心线（窗口中心在屏幕中心右侧）：负偏移按 0 记，恢复时居中贴回
        var rightOfCenter = new PinRect(Work.X + Work.Width / 2, 20, 200, 32);

        var offset = CapsuleIslandMath.Locate(Work, rightOfCenter, dpi: 1);

        Assert.Equal(0, offset.X);
        Assert.Equal(20, offset.Y);
    }
}
