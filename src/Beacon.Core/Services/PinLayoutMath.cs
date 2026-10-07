using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>物理像素矩形（L0 布局纯几何域，避免 Core 依赖 Win32 RECT）。</summary>
public readonly record struct PinRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}

/// <summary>
/// L0 布局几何（B-703，RFC §6.2.3）：锚点角 + DIP 偏移 ↔ 像素位置互算、越界回收。
/// 纯函数：PinnedHostWindow 的拖动落点持久化与启动恢复共用一套口径，Core.Tests 覆盖。
/// </summary>
public static class PinLayoutMath
{
    /// <summary>默认布局：主屏右上角 (24, 96) DIP。</summary>
    public static readonly PinOffset DefaultOffset = new(24, 96);

    /// <summary>锚点角 + 偏移(DIP) → 窗口矩形（不裁剪，供吸附/细条等自定义场景）。</summary>
    public static PinRect AnchorToRect(PinRect work, PinAnchor anchor, PinOffset offsetDips, double dpi, int widthPx, int heightPx)
    {
        var ox = (int)Math.Round(offsetDips.X * dpi);
        var oy = (int)Math.Round(offsetDips.Y * dpi);
        var x = anchor is PinAnchor.TopLeft or PinAnchor.BottomLeft ? work.X + ox : work.Right - widthPx - ox;
        var y = anchor is PinAnchor.TopLeft or PinAnchor.TopRight ? work.Y + oy : work.Bottom - heightPx - oy;
        return new PinRect(x, y, widthPx, heightPx);
    }

    /// <summary>锚点放置并回收越界（分辨率变小/显示器更换后窗口保持完整可见，B-703 验收）。</summary>
    public static PinRect Place(PinRect work, PinAnchor anchor, PinOffset offsetDips, double dpi, int widthPx, int heightPx, int margin = 8)
        => ClampInto(work, AnchorToRect(work, anchor, offsetDips, dpi, widthPx, heightPx), margin);

    /// <summary>像素位置 → 最近锚点角 + DIP 偏移（拖动落点持久化）。</summary>
    public static (PinAnchor Anchor, PinOffset OffsetDips) Locate(PinRect work, PinRect rect, double dpi)
    {
        var toLeft = Math.Abs(rect.X - work.X) <= Math.Abs(work.Right - rect.Right);
        var toTop = Math.Abs(rect.Y - work.Y) <= Math.Abs(work.Bottom - rect.Bottom);
        var anchor = (toLeft, toTop) switch
        {
            (true, true) => PinAnchor.TopLeft,
            (false, true) => PinAnchor.TopRight,
            (true, false) => PinAnchor.BottomLeft,
            _ => PinAnchor.BottomRight,
        };
        var (ox, oy) = anchor switch
        {
            PinAnchor.TopLeft => (rect.X - work.X, rect.Y - work.Y),
            PinAnchor.TopRight => (work.Right - rect.Right, rect.Y - work.Y),
            PinAnchor.BottomLeft => (rect.X - work.X, work.Bottom - rect.Bottom),
            _ => (work.Right - rect.Right, work.Bottom - rect.Bottom),
        };
        return (anchor, new PinOffset(Math.Max(0, ox) / dpi, Math.Max(0, oy) / dpi));
    }

    /// <summary>把矩形压回工作区（完全可见优先；工作区比窗口还小则贴锚边并保留 margin）。</summary>
    public static PinRect ClampInto(PinRect work, PinRect rect, int margin = 8)
    {
        var x = Math.Clamp(rect.X, work.X + margin, Math.Max(work.X + margin, work.Right - rect.Width - margin));
        var y = Math.Clamp(rect.Y, work.Y + margin, Math.Max(work.Y + margin, work.Bottom - rect.Height - margin));
        return new PinRect(x, y, rect.Width, rect.Height);
    }

    /// <summary>窗口是否大部分落在工作区外（显示拓扑变化后需回收，B-703 验收）。</summary>
    public static bool IsMostlyOutside(PinRect work, PinRect rect)
    {
        var ix = Math.Max(0, Math.Min(rect.Right, work.Right) - Math.Max(rect.X, work.X));
        var iy = Math.Max(0, Math.Min(rect.Bottom, work.Bottom) - Math.Max(rect.Y, work.Y));
        return (double)(ix * iy) / Math.Max(1, rect.Width * (long)rect.Height) < 0.5;
    }
}
