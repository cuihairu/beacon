using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-703 验收（几何部分）：锚点+DIP 偏移 ↔ 像素互算、越界回收、拓扑变化判定。</summary>
public sealed class PinLayoutMathTests
{
    private static readonly PinRect Work = new(0, 0, 1920, 1040);
    private const double Dpi = 1.0;

    [Theory]
    [InlineData(24, 96, 1728, 96)]      // TopRight：x = 1920-168-24
    [InlineData(0, 0, 1752, 0)]         // TopRight 零偏移贴角
    public void AnchorToRect_TopRight_OffsetMeasuredFromCorner(double ox, double oy, int expectX, int expectY)
    {
        var rect = PinLayoutMath.AnchorToRect(Work, PinAnchor.TopRight, new PinOffset(ox, oy), Dpi, 168, 32);
        Assert.Equal(expectX, rect.X);
        Assert.Equal(expectY, rect.Y);
    }

    [Fact]
    public void AnchorToRect_BottomLeft_DpiScaled()
    {
        var work = new PinRect(0, 0, 3840, 2160);
        var rect = PinLayoutMath.AnchorToRect(work, PinAnchor.BottomLeft, new PinOffset(24, 96), 2.0, 336, 64);
        Assert.Equal(48, rect.X);           // 24 DIP × 2.0
        Assert.Equal(2160 - 64 - 192, rect.Y); // 96 DIP × 2.0
    }

    [Fact]
    public void Place_ClampsIntoWork_WhenResolutionShrinks()
    {
        // 旧屏 3840 宽存的偏移，落到 1920 宽的新屏必须回收为完整可见（B-703 验收）
        var small = new PinRect(0, 0, 1920, 1040);
        var rect = PinLayoutMath.Place(small, PinAnchor.TopRight, new PinOffset(2000, 96), Dpi, 168, 32);
        Assert.True(small.Contains(rect.X, rect.Y), "左上角必须可见");
        Assert.True(small.Contains(rect.Right - 1, rect.Bottom - 1), "右下角必须可见");
    }

    /// <summary>坍缩细条 = 展开面板的锚边窄条（同一 Place 数学、宽度不同）：细条必须落在面板 footprint 内。
    /// 否则光标悬停细条 → 展开瞬间光标在面板外 → PointerExited 立刻坍缩 → 死循环
    /// （2026-10-10 用户实测「竖线贴屏幕边无法恢复」：旧细条贴屏边、面板内缩 offsetX）。
    /// 覆盖四锚点 × 三类偏移（默认内缩/贴角/越界回收）。</summary>
    [Fact]
    public void Place_CollapsedStrip_StaysWithinExpandedPanelFootprint()
    {
        const int stripWidth = 12;   // PinnedHostWindow CollapseStripDips
        const int panelWidth = 168;  // PinnedHostWindow PanelWidthDips
        const int height = 3 * 32;   // rows × TileHeightDips
        foreach (var anchor in new[] { PinAnchor.TopLeft, PinAnchor.TopRight, PinAnchor.BottomLeft, PinAnchor.BottomRight })
        {
            foreach (var offset in new[] { new PinOffset(24, 96), new PinOffset(0, 0), new PinOffset(300, 500) })
            {
                var strip = PinLayoutMath.Place(Work, anchor, offset, Dpi, stripWidth, height);
                var panel = PinLayoutMath.Place(Work, anchor, offset, Dpi, panelWidth, height);
                Assert.True(panel.Contains(strip.X, strip.Y), $"{anchor}/{offset}：细条左上须落在面板内");
                Assert.True(panel.Contains(strip.Right - 1, strip.Bottom - 1), $"{anchor}/{offset}：细条右下须落在面板内");
            }
        }
    }

    [Fact]
    public void ClampInto_WindowLargerThanWork_PinsToLeftTopMargin()
    {
        var tiny = new PinRect(0, 0, 200, 100);
        var rect = PinLayoutMath.ClampInto(tiny, new PinRect(-50, -50, 300, 200));
        Assert.Equal(8, rect.X);
        Assert.Equal(8, rect.Y);
    }

    [Fact]
    public void Locate_RoundTrip_PreservesAnchorAndOffset()
    {
        foreach (var anchor in new[] { PinAnchor.TopLeft, PinAnchor.TopRight, PinAnchor.BottomLeft, PinAnchor.BottomRight })
        {
            var offset = new PinOffset(24, 120);
            var rect = PinLayoutMath.AnchorToRect(Work, anchor, offset, Dpi, 168, 32);
            var (resolved, resolvedOffset) = PinLayoutMath.Locate(Work, rect, Dpi);
            Assert.Equal(anchor, resolved);
            Assert.Equal(offset.X, resolvedOffset.X, 6);
            Assert.Equal(offset.Y, resolvedOffset.Y, 6);
        }
    }

    [Fact]
    public void Locate_PicksNearestCorner()
    {
        var nearBottomRight = new PinRect(Work.Right - 168 - 10, Work.Bottom - 32 - 5, 168, 32);
        var (anchor, _) = PinLayoutMath.Locate(Work, nearBottomRight, Dpi);
        Assert.Equal(PinAnchor.BottomRight, anchor);
    }

    [Theory]
    [InlineData(3000, 500, true)]   // 完全在工作区外
    [InlineData(1860, 0, true)]     // 可见 60/168 ≈ 36% → 大半在外
    [InlineData(1800, 0, false)]    // 可见 120/168 ≈ 71% → 仍在屏内
    [InlineData(960, 500, false)]   // 居中
    public void IsMostlyOutside_DetectsTopologyChange(int x, int y, bool expected)
    {
        Assert.Equal(expected, PinLayoutMath.IsMostlyOutside(Work, new PinRect(x, y, 168, 32)));
    }
}
