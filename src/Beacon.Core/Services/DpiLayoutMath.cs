namespace Beacon.Core.Services;

/// <summary>
/// 自绘窗口的 DIP → 物理像素换算唯一口径（2026-10-10 DPI 修复单，5K 200% 字体被裁）。
/// WinUI 侧 XAML 布局与 FontSize 一律 DIP；Win32 侧 AppWindow.Resize/Move、WM_NCHITTEST、
/// Win32 定位收物理像素——只在 Win32 边界经此处换算一次，窗口类不得再出现裸 (int)(dips * dpi)。
/// 四舍五入而非截断：按行/按边逐次截断会累计亚像素欠高（32 DIP×1.3=41.6→41，三行欠 1.8 px），
/// 高缩放下窗口底部/右缘的内容被裁。
/// </summary>
public static class DpiLayoutMath
{
    /// <summary>Segoe UI 行盒系数（字号的倍率，覆盖常规/半粗字重的行盒高度）。
    /// 真值测量须 Windows 上的排版引擎；Linux/CI 测试门用此常数做同源判定（两侧同走本表，结论与 dpi 无关）。</summary>
    public const double LineHeightFactor = 1.33;

    /// <summary>DIP → 物理像素（四舍五入；同值重算不累计误差，与截断的逐次累加相反）。</summary>
    public static int ToPx(double dips, double dpi)
        => (int)Math.Round(dips * dpi, MidpointRounding.AwayFromZero);

    /// <summary>物理像素 → DIP（拖动落点持久化用；dpi 非法按 1:1 兜底，不倒挂）。</summary>
    public static double ToDips(int px, double dpi) => dpi <= 0 ? px : px / dpi;

    /// <summary>字号 DIP → 行盒高度 DIP（裁字判定的文本侧）。</summary>
    public static double LineHeightDips(double fontSizeDips) => fontSizeDips * LineHeightFactor;

    /// <summary>裁字判定：容器 DIP 在给定 dpi 下的物理高度是否容得下字号 DIP 的一行文本。
    /// 容器与字号同源换算（都走 <see cref="ToPx"/>），任一 dpi 下不满足即裁字点。</summary>
    public static bool TextFits(double containerDips, double fontSizeDips, double dpi)
        => ToPx(containerDips, dpi) >= ToPx(LineHeightDips(fontSizeDips), dpi);

    /// <summary>多行等高的总物理高度：总 DIP 一次换算。逐行换算会累计舍入误差
    /// （rows=3、32 DIP、dpi=1.3：逐行 41×3=123 px，总换算 ToPx(96,1.3)=125 px）。</summary>
    public static int PanelHeightPx(int rows, double rowDips, double dpi) => ToPx(rows * rowDips, dpi);

    /// <summary>内容自然高度 clamp 到 [min,max] DIP 后换算（详情窗内容可无限长，须封顶走滚动条）。</summary>
    public static int ClampedHeightPx(double contentDips, double minDips, double maxDips, double dpi)
        => ToPx(Math.Clamp(Math.Ceiling(contentDips), minDips, maxDips), dpi);
}
