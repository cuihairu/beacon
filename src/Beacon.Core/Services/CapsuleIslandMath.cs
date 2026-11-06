using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 悬浮岛几何（USER-RPT-4，BrimDeck Notch 口径对拍重做）：岛=刘海式贴顶形态——
/// 窗口顶缘恒贴屏幕顶（y=屏幕顶，永不下移、不持久化拖动 Y），水平居中 + 仅 X 偏移可持久化；
/// 形态常量照抄 BrimDeck CollapsedLimits（shoulder 16、肩深 shoulder×7/16、底圆角 12、高 32）。
/// 纯函数 Core 层（Core.Tests 锁口径）：CapsuleWindow 的岛形态启动恢复与拖动落点持久化共用。
/// </summary>
public static class CapsuleIslandMath
{
    /// <summary>肩部半径（BrimDeck CollapsedLimits.NotchShoulder）：顶缘向两侧外扩的过渡曲线宽。</summary>
    public const double ShoulderDips = 16;

    /// <summary>肩深（BrimDeck IslandBorder.BuildOutline：shoulderDepth = shoulder × 7/16）。</summary>
    public const double ShoulderDepthDips = ShoulderDips * 7d / 16;

    /// <summary>底圆角（BrimDeck CollapsedLimits.NotchCorner）：岛底两角，顶两角恒 0（贴屏幕顶）。</summary>
    public const double BottomRadiusDips = 12;

    /// <summary>收起态高（BrimDeck CollapsedLimits.BaseHeight）。</summary>
    public const double HeightDips = 32;

    /// <summary>内容与岛两端的留白（BrimDeck CollapsedLimits.NotchInset=30——刘海形态按 Notch 口径，
    /// 内容须同时避让肩部 16 + 底角 12 = 28，Notch 的 SurfaceWidth 取 max(30, 28) = 30；16 是 Capsule 形态的值，不适用贴顶岛）。</summary>
    public const double InsetDips = 30;

    /// <summary>最小曲面宽（BrimDeck CollapsedLimits.NotchWidthMin=72：形态自身所需，内容不够时靠内缩保形）。</summary>
    public const double MinWidthDips = 72;

    /// <summary>默认曲面宽（BrimDeck CollapsedLimits.NotchWidthDefault=238）：收起内容短也保持刘海默认宽，
    /// 展开预览的内容区 = 238 - 2×30 = 178 DIP，与 BrimDeck 开面板的内容宽同口径。</summary>
    public const double DefaultWidthDips = 238;

    /// <summary>默认偏移：水平居中（X 偏移 0）；Y 恒 0——刘海式贴顶没有垂直偏移。</summary>
    public static readonly PinOffset DefaultOffset = new(0, 0);

    /// <summary>贴顶居中放置（BrimDeck CollapsedGeometry.Resolve Notch 分支口径）：
    /// x = 屏幕水平中心 - 半宽 - X 偏移（正偏移=窗口中心在屏幕中心左侧），y 恒 = 屏幕顶；
    /// X 夹回屏幕（margin 8，永不越界——分辨率变小/换屏后窗口保持完整可见）。
    /// 入参 screen 是显示器物理矩形（屏幕边界，非工作区——刘海越过任务栏贴屏幕顶）。</summary>
    public static PinRect Place(PinRect screen, PinOffset offsetDips, double dpi, int widthPx, int heightPx, int margin = 8)
    {
        var ox = (int)Math.Round(offsetDips.X * dpi);
        var x = screen.X + (screen.Width - widthPx) / 2 - ox;
        x = Math.Clamp(x, screen.X + margin, Math.Max(screen.X + margin, screen.X + screen.Width - widthPx - margin));
        return new PinRect(x, screen.Y, widthPx, heightPx);
    }

    /// <summary>像素位置 → 贴顶锚偏移（拖动落点持久化）：只记水平偏移（窗口中心相对屏幕中心的左移量），
    /// 拖过中心的负值按 0 记；Y 恒 0（岛贴顶，拖动中的垂直分量被窗体侧锁死）。</summary>
    public static PinOffset Locate(PinRect screen, PinRect rect, double dpi)
        => new(Math.Max(0, (screen.X + screen.Width / 2) - (rect.X + rect.Width / 2)) / dpi, 0);
}
