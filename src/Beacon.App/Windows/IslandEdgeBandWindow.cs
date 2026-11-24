using System.Runtime.InteropServices;
using Beacon.App.Infrastructure;
using Beacon.Core.Services;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Beacon.App.Windows;

/// <summary>
/// 岛边缘 alpha 边带窗（spike/island-alpha-edge，island-visual-precision 修复单）：HRGN
/// （SetWindowRgn）是 1bit 掩膜，曲面边缘恒硬锯齿且真机 DPI&gt;1 下更扎眼。本窗用纯 Win32
/// 分层窗（UpdateLayeredWindow 逐像素 alpha）沿同一 <see cref="IslandSurfaceOutline"/> 轮廓
/// 自绘 3px 环带——内半环盖住岛体 region 硬切边，外半环带 AA 渐变落在壁纸上，可见边缘由
/// 本窗的真曲线平滑 alpha 边承担（GDI+ AddBezier 原生曲线，不经 RegionPoint 取整）。
/// 岛体 XAML 内容与 region 链零改动；WS_EX_TRANSPARENT 全窗点击穿透（点击落到下层岛体）。
/// 上游依据（2026-11-18 调研）：WinUI3 无「XAML 内容+逐像素 alpha 边缘」现成路径
/// （#1247 未落地、alpha-0 SystemBackdrop WASDK 1.1+ 死路、色键分层硬边同病），
/// 唯 ULW 自绘位图可骑逐像素 alpha——本窗即其最小承载（只画环带，不接管内容）。
/// </summary>
internal sealed class IslandEdgeBandWindow : IDisposable
{
    private const string ClassName = "BeaconIslandEdgeBand";

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const int SW_HIDE = 0;
    private const int SW_SHOWNA = 8;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint ULW_ALPHA = 0x00000002;
    private const byte AC_SRC_OVER = 0;
    private const byte AC_SRC_ALPHA = 1;

    /// <summary>边带位图四周外扩物理像素：笔宽 3px 居中于轮廓 → 外侧最多 ~2px AA，余量防裁。</summary>
    private const int BandMarginPx = 4;

    /// <summary>环带笔宽（物理 px，居中于轮廓线）：内半 1.5px 盖住 region 硬切边、外半 1.5px AA 落壁纸。</summary>
    private const float BandPenWidthPx = 3f;

    private readonly NativeMethods.WndProcDelegate _wndProc; // 持有引用防 GC（Win32MessageWindow 同款）
    private IntPtr _classNamePtr;
    private IntPtr _hwnd;
    private bool _shown;

    public IslandEdgeBandWindow()
    {
        _wndProc = WndProc;
        var hInstance = NativeMethods.GetModuleHandleW(null);
        _classNamePtr = Marshal.StringToHGlobalUni(ClassName);
        var windowClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = _classNamePtr,
        };
        // 1410 = ERROR_CLASS_ALREADY_EXISTS（同进程已注册：类不 UnregisterClass 故常驻，关窗重建第二个实例即撞）
        // 放行不抛；1407 是 CANNOT_FIND_WND_CLASS，误当「已注册」会把真失败吞成静默无端带
        if (NativeMethods.RegisterClassExW(ref windowClass) == 0 && Marshal.GetLastWin32Error() != 1410)
        {
            throw new InvalidOperationException($"RegisterClassExW(band) failed: {Marshal.GetLastWin32Error()}");
        }
        _hwnd = NativeMethods.CreateWindowExW(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
            ClassName, string.Empty, WS_POPUP, 0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowExW(band) failed: {Marshal.GetLastWin32Error()}");
        }
    }

    /// <summary>沿轮廓重建环带位图并 ULW。clientRect=岛窗客户区屏幕矩形；scale=岛窗 DPI 标度；
    /// segs=曲面轮廓段表（DIP）；islandHwnd 保留岛窗身份（z 序用 HWND_TOP=TOPMOST band 顶，
    /// 岛窗重申 TOPMOST 后本窗经 Show 回场重钉仍在其上）。
    /// 位图左上角钉在客户区左上角外扩 <see cref="BandMarginPx"/> 处（贴屏幕顶时上方出屏部分由屏裁剪，
    /// 顶缘平齐刘海读法不受影响）。任何失败静默自隐藏（岛体 region 边缘照旧，spike 不扰主线）。</summary>
    public void Update(IntPtr islandHwnd, NativeMethods.RECT clientRect, double scale, IReadOnlyList<IslandSurfaceOutline.Seg> segs)
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.IsWindow(_hwnd) || segs.Count == 0)
        {
            return;
        }
        try
        {
            int clientW = clientRect.Right - clientRect.Left;
            int clientH = clientRect.Bottom - clientRect.Top;
            int bmpW = clientW + BandMarginPx * 2;
            int bmpH = clientH + BandMarginPx * 2;
            if (bmpW <= 0 || bmpH <= 0)
            {
                Hide();
                return;
            }

            using var bmp = new Bitmap(bmpW, bmpH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = BuildPath(segs, scale, BandMarginPx);
                var bounds = path.GetBounds(); // 取证面：路径若退化（近零尺寸），环带没画先从这里看
                CapsuleWindow.IslandTraceShared($"edge band path bounds={bounds.X:0.#},{bounds.Y:0.#} {bounds.Width:0.#}x{bounds.Height:0.#} step={scale:0.###}x{BandPenWidthPx:0.#}");
                using var pen = new Pen(Color.FromArgb(255, 0, 0, 0), BandPenWidthPx);
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawPath(pen, path); // 只描环不填充：内部透明露出下层岛体
            }
            var (inked, maxAlpha) = CountInk(bmp); // 取证面：DrawPath 后位图若近乎空，问题在绘制侧而非 ULW
            CapsuleWindow.IslandTraceShared($"edge band ink count={inked} maxA={maxAlpha} of {bmpW * bmpH}");
            if (inked == 0)
            {
                // 空帧门（ISLAND-BOTTOM-1）：路径退化/全透明帧不许 ULW+Show 挂出无内容带——
                // 「失败自隐」契约按零墨同口径处理（此前空帧也会 Show，IsAlive 门控把空带当活带放行）
                CapsuleWindow.IslandTraceShared("edge band empty ink → hide（ISLAND-BOTTOM-1 空帧门）");
                Hide();
                return;
            }
            Premultiply(bmp); // ULW AC_SRC_ALPHA 要求预乘 alpha（GDI+ 位图是非预乘 BGRA）

            var dst = new NativeMethods.POINT { X = clientRect.Left - BandMarginPx, Y = clientRect.Top - BandMarginPx };
            var size = new UlwSize { cx = bmpW, cy = bmpH };
            var src = new NativeMethods.POINT { X = 0, Y = 0 };
            var blend = new BlendFunction { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };

            var screenDc = GetDC(IntPtr.Zero);
            var memDc = CreateCompatibleDC(screenDc);
            var hBitmap = bmp.GetHbitmap(Color.FromArgb(0, 0, 0, 0));
            try
            {
                var old = SelectObject(memDc, hBitmap);
                try
                {
                    // hdcDst 恒 NULL：用 pptDst+psize 定位（ULW 两用签名之一）
                    if (!UpdateLayeredWindow(_hwnd, IntPtr.Zero, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA))
                    {
                        throw new InvalidOperationException($"UpdateLayeredWindow(band) failed: {Marshal.GetLastWin32Error()}");
                    }
                }
                finally
                {
                    SelectObject(memDc, old);
                }
            }
            finally
            {
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }

            if (!_shown)
            {
                ShowWindow(_hwnd, SW_SHOWNA);
                _shown = true;
            }
            // z 序：HWND_TOP=TOPMOST band 顶（岛窗紧随其下）。环带必须盖在岛窗之上，内半环才盖得住
            // region 硬切边——旧写法 SetWindowPos(本窗, islandHwnd)=插到岛窗之后（下层），内半环被
            // 岛窗盖死，全环只剩 region 台阶缝漏出的外沿 AA（CI 实证全环仅 2px 露出）
            NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            CapsuleWindow.IslandTraceShared($"edge band ok bmp={bmpW}x{bmpH} dst={dst.X},{dst.Y} scale={scale:0.###} segs={segs.Count}");
        }
        catch (Exception ex)
        {
            // 静默退场（岛体 region 边缘永远保底），但留取证面：边带首判全靠这行判「没画」vs「画了没用」
            CapsuleWindow.IslandTraceShared($"edge band update FAILED {ex.Message}");
            Hide();
        }
    }

    /// <summary>岛体回场时重显边带并重钉相对序（几何若在隐藏期变化，下一拍 ApplyIslandRegion 自愈）。</summary>
    public void Show(IntPtr islandHwnd)
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.IsWindow(_hwnd))
        {
            return;
        }
        if (!_shown)
        {
            ShowWindow(_hwnd, SW_SHOWNA);
            _shown = true;
        }
        // 回场重钉=同 Update 的 HWND_TOP 口径（岛窗 TOPMOST 重申后 band 仍在其上）
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void Hide()
    {
        if (_hwnd != IntPtr.Zero && NativeMethods.IsWindow(_hwnd) && _shown)
        {
            ShowWindow(_hwnd, SW_HIDE);
            _shown = false;
        }
    }

    /// <summary>TopmostGuard 注册面：环带窗句柄（未建/已销毁回 Zero，哨兵对 Zero 天然跳过）。</summary>
    internal IntPtr Handle => _hwnd;

    /// <summary>TopmostGuard 门控面：环带当前确在显示。隐藏期不放行——哨兵不得把失败自隐的
    /// 空带 Show 回来（IslandEdgeBandWindow 的静默退场契约）。</summary>
    internal bool IsAlive => _shown && _hwnd != IntPtr.Zero && NativeMethods.IsWindow(_hwnd);

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            if (NativeMethods.IsWindow(_hwnd))
            {
                NativeMethods.DestroyWindow(_hwnd);
            }
            _hwnd = IntPtr.Zero;
        }
        if (_classNamePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_classNamePtr);
            _classNamePtr = IntPtr.Zero;
        }
    }

    /// <summary>轮廓段表 → GDI+ 闭合路径（DIP×scale+外扩偏移；AddBezier 原生三次曲线，
    /// GDI+ 内部高精度展平——不经过 region 链的 RegionPoint 取整，这就是 AA 来源）。</summary>
    private static GraphicsPath BuildPath(IReadOnlyList<IslandSurfaceOutline.Seg> segs, double scale, int offsetPx)
    {
        var path = new GraphicsPath();
        var prev = PointF.Empty;
        var start = PointF.Empty;
        var hasStart = false;
        foreach (var seg in segs)
        {
            switch (seg)
            {
                case IslandSurfaceOutline.Move move:
                    prev = Pt(move.To, scale, offsetPx);
                    start = prev;
                    hasStart = true;
                    break;
                case IslandSurfaceOutline.Line line:
                {
                    var to = Pt(line.To, scale, offsetPx);
                    path.AddLine(prev, to);
                    prev = to;
                    break;
                }
                case IslandSurfaceOutline.Cubic cubic:
                {
                    var end = Pt(cubic.To, scale, offsetPx);
                    path.AddBezier(prev, Pt(cubic.C1, scale, offsetPx), Pt(cubic.C2, scale, offsetPx), end);
                    prev = end;
                    break;
                }
            }
        }
        if (hasStart && prev != start)
        {
            path.AddLine(prev, start); // 闭合段（首末点重合时零长无害）
        }
        return path;
    }

    private static PointF Pt(IslandSurfaceOutline.Point p, double scale, int offsetPx)
        => new((float)(p.X * scale + offsetPx), (float)(p.Y * scale + offsetPx));

    /// <summary>取证统计：alpha&gt;0 像素数与最大 alpha（LockBits 单趟）。</summary>
    private static (int Inked, byte MaxAlpha) CountInk(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var bytes = new byte[Math.Abs(stride) * bmp.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            int inked = 0;
            byte maxA = 0;
            for (var y = 0; y < bmp.Height; y++)
            {
                var rowBase = y * stride;
                for (var x = 0; x < bmp.Width; x++)
                {
                    var a = bytes[rowBase + x * 4 + 3];
                    if (a > 0)
                    {
                        inked++;
                        if (a > maxA)
                        {
                            maxA = a;
                        }
                    }
                }
            }
            return (inked, maxA);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    /// <summary>逐像素预乘 alpha（ULW AC_SRC_ALPHA 语义）：rgb×a/255；全不透明行短路。</summary>
    private static void Premultiply(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var bytes = new byte[Math.Abs(stride) * bmp.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (var y = 0; y < bmp.Height; y++)
            {
                var rowBase = y * stride;
                for (var x = 0; x < bmp.Width; x++)
                {
                    var i = rowBase + x * 4;
                    var a = bytes[i + 3];
                    if (a == 255)
                    {
                        continue;
                    }
                    bytes[i] = (byte)(bytes[i] * a / 255);
                    bytes[i + 1] = (byte)(bytes[i + 1] * a / 255);
                    bytes[i + 2] = (byte)(bytes[i + 2] * a / 255);
                }
            }
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        => NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct UlwSize
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hWnd, IntPtr hdcDst, ref NativeMethods.POINT pptDst, ref UlwSize psize,
        IntPtr hdcSrc, ref NativeMethods.POINT pptSrc, int crKey, ref BlendFunction pblend, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
