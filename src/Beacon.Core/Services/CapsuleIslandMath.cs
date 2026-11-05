using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 悬浮岛几何（display-modes-analysis §7 Phase 2）：顶部居中锚点 + DIP 偏移 ↔ 像素矩形互算。
/// 纯函数 Core 层（PinLayoutMath 同口径，Core.Tests 覆盖）：CapsuleWindow 的岛形态
/// 启动恢复与拖动落点持久化共用这一套，窗体侧只做换算调用，不含 Win32 依赖。
/// </summary>
public static class CapsuleIslandMath
{
    /// <summary>默认偏移：顶缘居中再下移 8 DIP（贴而不压系统 UI 顶缘，观感口径同经典胶囊 24/96 边距思路）。</summary>
    public static readonly PinOffset DefaultOffset = new(0, 8);

    /// <summary>顶部居中锚定矩形（不裁剪，供自定义场景）：x = 工作区水平中心 - 半宽 - 偏移，
    /// y = 工作区顶缘 + 偏移。偏移语义与 Locate 互逆：X 正值 = 窗口中心在屏幕中心左侧。</summary>
    public static PinRect AnchorToRect(PinRect work, PinOffset offsetDips, double dpi, int widthPx, int heightPx)
    {
        var ox = (int)Math.Round(offsetDips.X * dpi);
        var oy = (int)Math.Round(offsetDips.Y * dpi);
        var x = work.X + (work.Width - widthPx) / 2 - ox;
        var y = work.Y + oy;
        return new PinRect(x, y, widthPx, heightPx);
    }

    /// <summary>锚点放置并回收越界（分辨率变小/显示器更换后窗口保持完整可见，ClampInto 同 B-703 口径）。</summary>
    public static PinRect Place(PinRect work, PinOffset offsetDips, double dpi, int widthPx, int heightPx, int margin = 8)
        => PinLayoutMath.ClampInto(work, AnchorToRect(work, offsetDips, dpi, widthPx, heightPx), margin);

    /// <summary>像素位置 → 顶部居中锚偏移（拖动落点持久化）：水平以「窗口中心相对工作区中心的
    /// 左移量」记偏移，垂直以窗口顶缘距工作区顶缘记偏移；拖过中心/顶缘的负值按 0 记
    /// （恢复时 Place 的 ClampInto 兜底，永不越界）。</summary>
    public static PinOffset Locate(PinRect work, PinRect rect, double dpi)
    {
        var ox = (work.X + work.Width / 2) - (rect.X + rect.Width / 2);
        var oy = rect.Y - work.Y;
        return new PinOffset(Math.Max(0, ox) / dpi, Math.Max(0, oy) / dpi);
    }
}
