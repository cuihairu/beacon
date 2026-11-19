using System.Runtime.InteropServices;
using Beacon.App.Components;
using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Beacon.App.Windows;


public sealed partial class CapsuleWindow
{
    /// <summary>悬浮岛形态开关（display-modes-analysis §7 Phase 2，config.capsuleIsland 默认关=现状）：
    /// 开=顶部居中锚定，收起态计数文本让位给「品牌标 + 未读/异常徽标」；关=经典胶囊逐字节不动。
    /// §1 只读契约不变：点击仍走 OpenPanelRequested（App.xaml.cs 接 quickPanel.Toggle，不新增岛内操作）。
    /// Activate 前调用时 _appWindow 未就绪——只翻标志与元素可见性，位置由 OnLoaded 的 RestorePosition
    /// 按 _island 分支落；运行中切换则即时重排。</summary>
    public void SetIslandMode(bool island)
    {
        if (_island == island)
        {
            return;
        }
        _island = island;
        if (!island && _ruleHidden)
        {
            _ruleHidden = false; // 避让只对岛形态生效：切回经典胶囊立即回场
            if (_showAllowed && _appWindow is not null)
            {
                _appWindow.Show();
            }
        }

        ApplyIslandChrome();
        CountText.Visibility = island ? Visibility.Collapsed : Visibility.Visible;
        BrandMark.Visibility = island ? Visibility.Visible : Visibility.Collapsed;
        RefreshBadge();
        if (_appWindow is not null)
        {
            ApplySize(); // 收起态内容宽变了（计数文本 ↔ 徽标+品牌标），重排后按锚点重贴
            RestorePosition();
        }
    }

    /// <summary>岛形态视觉重做（Dynamic Island 口径，2026-11-06 实机截图诊断驱动）：近实色黑药丸
    /// （DesktopAcrylic 在亮壁纸上把灰底稀释成「看不清」，恒黑底对比恒定）、发丝白描边只做形态分层、
    /// 健康环退场（整环读作 spinner 且与灯点双绿冗余——单灯点+晕扛状态）、字恒近白、间距放宽。
    /// 经典胶囊逐字节不动：首切岛前快照 XAML 原值，切回即还原。</summary>
    private void ApplyIslandChrome()
    {
        if (!_chromeCaptured)
        {
            _classicPillBackground = RootBorder.Background;
            _classicPillBorder = RootBorder.BorderBrush;
            _classicPadding = RootBorder.Padding;
            _classicRowSpacing = PillRow.Spacing;
            _classicBrandForeground = BrandMark.Foreground;
            _classicPreviewBackground = SourcePreview.Background;
            _classicPreviewBorder = SourcePreview.BorderBrush;
            _classicBorderThickness = RootBorder.BorderThickness;
            _classicCornerRadius = RootBorder.CornerRadius;
            _classicPreviewThickness = SourcePreview.BorderThickness;
            _classicStackMargin = SurfaceStack.Margin;      // 经典无内缩（岛 NotchInset 30 才有）
            _classicPillAlign = PillRow.HorizontalAlignment; // 经典 Stretch（岛才居中）——default() 会落 Left，必须快照
            _classicStackAlign = SurfaceStack.VerticalAlignment;
            _chromeCaptured = true;
        }
        if (_island)
        {
            // 底色交给曲面 Path（否则 Border 的矩形底会把肩部/底角的轮廓填平，岛退化成方块——
            // 真机实测：矩形底 + 同色曲面叠加后四边全直，肩部完全看不见）
            RootBorder.Background = null; // 无底色：曲面填充只由 IslandSurface 画（Background 可空，切回经典由快照还原）
            // 窗口级亚克力摘除（岛灰板根因修复，2026-11-16 用户拍板方案 A）：DesktopAcrylic 是窗矩形
            // 全幅材质、不随内容镂空，黑曲面外那圈窗内区域露出灰蒙底=「岛周围一层灰色底板」。岛态
            // 收起/展开同窗恒摘；切回经典由 else 分支重挂 _classicBackdrop 单例。
            SystemBackdrop = null;
            RootBorder.BorderBrush = new SolidColorBrush(ThemeColors.IslandHairline());
            // 药丸画法退场：曲面由 IslandSurface 单路径自绘（顶缘贴屏满宽 + 肩部 + 底角12），
            // 描边/圆角/内边距全从 Border 移走——Path 是 Border 的子元素，Border 若留圆角会把贴顶两角裁掉，
            // 若留内边距 Path 就铺不满曲面（肩部必须伸到窗口两角才读得出「刘海」）。
            RootBorder.BorderThickness = new Thickness(0);
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.Padding = new Thickness(0);
            SurfaceStack.Margin = new Thickness(CapsuleIslandMath.InsetDips, 0, CapsuleIslandMath.InsetDips, 0); // NotchInset 30
            PillArea.MinHeight = CapsuleIslandMath.HeightDips; // 收起行=曲面高 32，行内容垂直居中（BrimDeck 收起态行高口径）
            SurfaceStack.VerticalAlignment = VerticalAlignment.Top; // 窗口含底部 8 DIP 阴影带：内容行钉顶，与 y=0 起的曲面对齐
            PillRow.HorizontalAlignment = HorizontalAlignment.Center; // 刘海默认宽 238：内容居中才左右留白对称
            PillRow.Spacing = 8;
            HealthRing.Visibility = Visibility.Collapsed;
            OverallLightHost.Visibility = Visibility.Collapsed; // 岛收起态：环/条组件扛级别色，灯点双显冗余（ISLAND-RING-1）
            BrandMark.Foreground = new SolidColorBrush(ThemeColors.IslandText());
            IslandSurface.Visibility = Visibility.Visible;
            IslandSurface.Fill = new SolidColorBrush(ThemeColors.IslandPill());
            // 发丝内缘（BrimDeck 技法）：2px 描边 + UpdateSurfaceOutlineCore 里按曲面矩形裁掉外侧一半
            IslandSurface.Stroke = new SolidColorBrush(ThemeColors.IslandHairline());
            IslandSurface.StrokeThickness = 2;
            // 软阴影层退场（2026-11-08 实机诊断）：刘海=贴顶平齐（Dynamic Island 口径），窗高恒曲面高，
            // 无底部阴影带——旧的两层不透明剪影（Fill=IslandPill + 粗描边 6/14 + 下沉 2/3）在面下叠出
            // 全宽暗带，把岛读成矩形板（真机截图 slab + 桌面剪影随高度变宽实证）。剪影层随窗口一并裁掉，
            // 保留 XAML 节点与 ApplyShadowGeometry 骨架（经典形态切回仍走既有 Collapsed 早退口径）。
            SourcePreview.Background = null; // 预览在曲面内无缝长出（同底色/同描边都多余——展开读作岛变高）
            SourcePreview.BorderThickness = new Thickness(0);
            UpdateSurfaceOutline();
        }
        else
        {
            // 经典亚克力还原（重挂构造期单例，观感与修复前一致——经典形态逐字节不动红线）
            SystemBackdrop = _classicBackdrop;
            RestoreWindowRegion(); // 经典=矩形窗：摘岛曲面裁剪 region（方案 B 配对操作）
            RootBorder.Background = _classicPillBackground;
            RootBorder.BorderBrush = _classicPillBorder;
            RootBorder.Padding = _classicPadding;
            RootBorder.BorderThickness = _classicBorderThickness;
            RootBorder.CornerRadius = _classicCornerRadius;
            SurfaceStack.Margin = _classicStackMargin;
            PillArea.MinHeight = 0;
            PillRow.HorizontalAlignment = _classicPillAlign;
            SurfaceStack.VerticalAlignment = _classicStackAlign;
            PillRow.Spacing = _classicRowSpacing;
            HealthRing.Visibility = Visibility.Visible;
            OverallLightHost.Visibility = Visibility.Visible; // 经典胶囊灯点照旧（G4/G5 像素锚不动）
            HealthRing.Content = StatusRing.Build(16, 2.5, _ringRatio, _ringColor); // 岛期间环停画，切回补最近份额
            RefreshBadge(); // 岛进度面随形态退场（经典无环/条徽标，Content 清空）
            BrandMark.Foreground = _classicBrandForeground;
            IslandSurface.Visibility = Visibility.Collapsed;
            IslandSurface.Data = null;
            IslandShadowStack.Visibility = Visibility.Collapsed; // 经典无阴影带，剪影层一并退场
            IslandShadowNear.Data = IslandShadowFar.Data = null;
            SourcePreview.Background = _classicPreviewBackground;
            SourcePreview.BorderBrush = _classicPreviewBorder;
            SourcePreview.BorderThickness = _classicPreviewThickness;
        }
    }

    /// <summary>按曲面实际尺寸重建岛轮廓（IslandSurfaceOutline → PathGeometry，BrimDeck BuildOutline 同式控制点）。
    /// RootBorder.SizeChanged 每次触发（展开/切形态/换 DPI），经典形态与未就绪早退。</summary>
    private void UpdateSurfaceOutline()
    {
        try
        {
            UpdateSurfaceOutlineCore();
        }
        catch (Exception ex)
        {
            IslandTrace($"UpdateSurfaceOutline FAILED\n{ex}");
            throw;
        }
    }

    private void UpdateSurfaceOutlineCore()
    {
        if (!_island || IslandSurface.Visibility != Visibility.Visible)
        {
            return;
        }

        var width = RootBorder.ActualWidth;
        // 曲面高：收起态恒 32（窗口比曲面多出的 8 DIP 是阴影带，不进轮廓）；展开态铺满整窗（岛变高）
        var height = SourcePreview.Visibility == Visibility.Visible
            ? RootBorder.ActualHeight
            : CapsuleIslandMath.HeightDips;
        if (!(width > 0) || !(height > 0))
        {
            return;
        }

        PathGeometry BuildOutlineGeometry()
        {
            // WinUI 3 的同一 Geometry 实例不能挂多个 Path.Data（抛「值不在预期范围」）——
            // 曲面与两层阴影剪影各自新建一份（控制点序列相同、实例独立）
            var figure = new PathFigure
            {
                StartPoint = new global::Windows.Foundation.Point(0, 0),
                IsClosed = true,
                IsFilled = true,
            };
            foreach (var seg in IslandSurfaceOutline.Build(width, height, CapsuleIslandMath.ShoulderDips, CapsuleIslandMath.BottomRadiusDips))
            {
                switch (seg)
                {
                    case IslandSurfaceOutline.Move:
                        break; // 首点已作 StartPoint
                    case IslandSurfaceOutline.Line line:
                        figure.Segments.Add(new LineSegment { Point = ToPoint(line.To) });
                        break;
                    case IslandSurfaceOutline.Cubic cubic:
                        figure.Segments.Add(new BezierSegment
                        {
                            Point1 = ToPoint(cubic.C1),
                            Point2 = ToPoint(cubic.C2),
                            Point3 = ToPoint(cubic.To),
                        });
                        break;
                }
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        var outlineGeometry = BuildOutlineGeometry();
        IslandSurface.Data = outlineGeometry;
        // 发丝内缘收口：按曲面矩形裁掉 2px 描边的外侧一半（BrimDeck「画大一半、裁半」技法）。
        // 展开态同样裁——预览长出后轮廓随窗高，描边仍只留内侧
        IslandSurface.Clip = new RectangleGeometry { Rect = new global::Windows.Foundation.Rect(0, 0, width, height) };
        ApplyShadowGeometry(BuildOutlineGeometry(), BuildOutlineGeometry(), width, height);
        ApplyIslandRegion(); // 窗裁成曲面轮廓（方案 B）：弧外月牙出窗，壁纸直接可见
        IslandTrace($"outline {width}x{height} segs={outlineGeometry.Figures[0].Segments.Count} " +
            $"row={PillRow.ActualWidth}x{PillRow.ActualHeight} " +
            $"pill={PillArea.ActualWidth}x{PillArea.ActualHeight} " +
            $"stack={SurfaceStack.ActualWidth} margin={SurfaceStack.Margin} " +
            $"brand={BrandMark.Visibility}/{BrandMark.ActualWidth} " +
            $"count={CountText.Visibility} progress={IslandProgress.Visibility}/{IslandProgress.ActualWidth} " +
            $"align={PillRow.HorizontalAlignment} root={Root.ActualWidth}x{Root.ActualHeight}");

        static global::Windows.Foundation.Point ToPoint(IslandSurfaceOutline.Point p) => new(p.X, p.Y);
    }

    /// <summary>窗口 region 裁剪成岛曲面轮廓（岛灰板修复方案 B，2026-11-16）：方案 A（摘
    /// SystemBackdrop）被 CI 像素实证否定——窗矩形内曲面外的月牙区露出的是 WinUI3 窗背板
    /// （黑，白壁纸上色距 765=3×255 满值）而非壁纸，摘亚克力只是灰板换黑板。真镂空唯一路径=
    /// OS 级裁剪：SetWindowRgn 把窗裁成轮廓多边形，弧外区域物理不在窗内，壁纸直接可见（与
    /// 亚克力/背板无关）。坐标换算：轮廓 DIP→物理像素（GetDpiForWindow）+ 客户区原点在窗口
    /// 坐标的偏移（ClientToScreen − GetWindowRect）。失败（句柄未就绪/region 构建失败）保持
    /// 矩形窗=现状，不崩。展开态/换 DPI/切形态都走 UpdateSurfaceOutline 重设。</summary>
    /// <summary>最近一次成功应用的 region 几何键（窗尺寸+chrome）：SetWindowRgn 会引发
    /// AppWindow.Changed frame 回调，同键跳过防重建环；RestoreWindowRegion 清零（经典切回后
    /// 同几何也要重设，region 已被摘）。</summary>
    private (int WinW, int WinH, int ChromeW, int ChromeH) _lastRegionKey;

    /// <summary>岛边缘 alpha 边带窗（spike/island-alpha-edge）：ULW 逐像素 alpha 环带盖住 region
    /// 硬切边。仅岛态存在，经典态隐藏；失败静默退场不扰岛体（IslandEdgeBandWindow）。</summary>
    private IslandEdgeBandWindow? _edgeBand;

    /// <summary>上次边带同步的 DPI 标度与轮廓段表：纯移动重钉（SyncEdgeBandPosition）时几何未变，
    /// 沿用缓存；首拍 region 前为空（Count=0 即跳过）。</summary>
    private double _lastBandScale = 1.0;
    private IReadOnlyList<IslandSurfaceOutline.Seg> _lastBandSegs = [];

    /// <summary>TopmostGuard 注册面：边带窗句柄与存活态。哨兵每个前台/菜单事件先钉岛窗再钉边带，
    /// 相对序（band 恒在岛正上层）才守得住——否则事件把岛钉回边带上方，内半环被岛体背板盖死
    /// （44288775018 实证）。allowed 门控含存活位：边带失败自隐期间哨兵不放行，不把空带 Show 回来。</summary>
    internal IntPtr EdgeBandHwnd => _edgeBand?.Handle ?? IntPtr.Zero;
    internal bool EdgeBandAlive => _edgeBand is { } band && band.IsAlive;

    private void ApplyIslandRegion()
    {
        if (_hwnd == IntPtr.Zero || _appWindow is null)
        {
            IslandTrace("region skipped hwnd/appWindow not ready");
            return;
        }
        try
        {
            var dpi = NativeMethods.GetDpiForWindow(_hwnd);
            if (dpi <= 0)
            {
                dpi = 96;
            }
            var scale = dpi / 96.0;
            // 轮廓尺寸量窗矩形（客户区物理像素→DIP），不取布局回调时的 ActualWidth/Height——
            // 启动期窗默认近全屏，布局回调先把 region 钉在全屏轮廓、随后 Resize 收窄时序脱节
            // （43812011171 实证：窗顶 28px 被 stale region 裁掉、GLM 面不在树），量窗=每拍都以
            // 窗矩形真相为准，收窄后 Region 跟 Resize 走。
            var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
            var windowSize = _appWindow.Size;
            var key = (windowSize.Width, windowSize.Height, chromeWidth, chromeHeight);
            if (key == _lastRegionKey)
            {
                return; // 同几何已应用（SetWindowRgn 自身触发的 frame 回调走这里，防环）
            }
            var clientWidthDips = (windowSize.Width - chromeWidth) / scale;
            var clientHeightDips = (windowSize.Height - chromeHeight) / scale;
            var segs = IslandSurfaceOutline.Build(clientWidthDips, clientHeightDips, CapsuleIslandMath.ShoulderDips, CapsuleIslandMath.BottomRadiusDips);
            if (segs.Count == 0)
            {
                IslandTrace("region skipped empty outline");
                return;
            }
            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            NativeMethods.ClientToScreen(_hwnd, out origin);
            NativeMethods.GetWindowRect(_hwnd, out var window);
            var offsetX = origin.X - window.Left;
            var offsetY = origin.Y - window.Top;
            var points = new List<NativeMethods.POINT>(segs.Count * FlattenStepsPerCubic);
            foreach (var seg in segs)
            {
                switch (seg)
                {
                    case IslandSurfaceOutline.Move move:
                        points.Add(RegionPoint(move.To, scale, offsetX, offsetY));
                        break;
                    case IslandSurfaceOutline.Line line:
                        points.Add(RegionPoint(line.To, scale, offsetX, offsetY));
                        break;
                    case IslandSurfaceOutline.Cubic cubic:
                        FlattenCubic(cubic, points, scale, offsetX, offsetY); // 起点取 points 末尾（前段终点）
                        break;
                }
            }
            if (points.Count < 3)
            {
                IslandTrace($"region skipped thin polygon points={points.Count}");
                return;
            }
            var region = NativeMethods.CreatePolygonRgn(points.ToArray(), points.Count, 2 /* WINDING */);
            if (region == IntPtr.Zero)
            {
                IslandTrace("region skipped CreatePolygonRgn failed");
                return;
            }
            if (NativeMethods.SetWindowRgn(_hwnd, region, true) == 0)
            {
                IslandTrace($"region SetWindowRgn FAILED points={points.Count}");
                NativeMethods.DeleteObject(region); // 失败自持清理（成功后 region 归系统，不可再删）
                return;
            }
            IslandTrace($"region ok points={points.Count} client={clientWidthDips:0.#}x{clientHeightDips:0.#} offset={offsetX},{offsetY}");
            _lastRegionKey = key;
            EnsureTopmostBand();
            // spike：alpha 边带随 region 同步（同一轮廓段表，GDI+ 真 AA；先钉 TOPMOST band 再插带）
            SyncEdgeBand(scale, new NativeMethods.RECT
            {
                Left = origin.X,
                Top = origin.Y,
                Right = origin.X + (windowSize.Width - chromeWidth),
                Bottom = origin.Y + (windowSize.Height - chromeHeight),
            }, segs);
        }
        catch (Exception ex)
        {
            IslandTrace($"ApplyIslandRegion FAILED\n{ex}");
        }
    }

    /// <summary>三次贝塞尔折线展平（GDI region 只吃多边形）：起点=已有末点，每段 Cubic 采样
    /// FlattenStepsPerCubic 步（曲线半径 7-12 DIP，24 步误差远小于 0.1 DIP，视觉连续）。</summary>
    private const int FlattenStepsPerCubic = 24;

    /// <summary>边缘 alpha 边带同步（spike/island-alpha-edge）：region 应用成功后按同一轮廓重建
    /// ULW 环带并插到岛窗正上层。边带自身任何异常静默退场（IslandEdgeBandWindow 内部也自兜），
    /// 岛体 region 边缘永远保底——spike 面不破主线。</summary>
    private void SyncEdgeBand(double scale, NativeMethods.RECT clientRect, IReadOnlyList<IslandSurfaceOutline.Seg> segs)
    {
        try
        {
            _edgeBand ??= new IslandEdgeBandWindow();
            _lastBandScale = scale; // 纯移动重钉（SyncEdgeBandPosition）复用：region 去重键无位置，
            _lastBandSegs = segs;   // 岛窗挪动不触发 ApplyIslandRegion，band 只能靠这里缓存的重钉
            _edgeBand.Update(_hwnd, clientRect, scale, segs);
        }
        catch (Exception ex)
        {
            IslandTrace($"edge band sync FAILED\n{ex}");
        }
    }

    /// <summary>纯移动重钉边带（AppWindow.Changed DidPositionChange）：band 位图钉的是屏幕坐标
    /// （dst=clientRect−外扩），region 是窗相对坐标会跟窗走、band 不会——44288775018 实证：启动期
    /// 岛窗 (378,0)→(370,−31)→(378,0) 弹跳，末段回挪不触发 region 重算，band 停在旧位，环带底缘
    /// 整段压岛体黑面（黑对黑隐形）只漏 2px AA 斑，②③ 指标零收敛。几何未变，轮廓段表/DPI 沿用
    /// 上次缓存；band 未同步过（segs 空）则无事可做。Update 重画位图+ULW+回钉 HWND_TOP 一并完成。</summary>
    private void SyncEdgeBandPosition()
    {
        if (_appWindow is null || _lastBandSegs.Count == 0)
        {
            return;
        }
        try
        {
            var (chromeWidth, chromeHeight) = NativeMethods.ChromeSizePx(_hwnd);
            var windowSize = _appWindow.Size;
            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            NativeMethods.ClientToScreen(_hwnd, out origin);
            SyncEdgeBand(_lastBandScale, new NativeMethods.RECT
            {
                Left = origin.X,
                Top = origin.Y,
                Right = origin.X + (windowSize.Width - chromeWidth),
                Bottom = origin.Y + (windowSize.Height - chromeHeight),
            }, _lastBandSegs);
        }
        catch (Exception ex)
        {
            IslandTrace($"edge band reposition FAILED\n{ex}");
        }
    }

    private static void FlattenCubic(IslandSurfaceOutline.Cubic cubic, List<NativeMethods.POINT> points, double scale, int offsetX, int offsetY)
    {
        if (points.Count == 0)
        {
            return;
        }
        var start = points[^1];
        var startX = (start.X - offsetX) / scale;
        var startY = (start.Y - offsetY) / scale;
        for (var i = 1; i <= FlattenStepsPerCubic; i++)
        {
            var t = (double)i / FlattenStepsPerCubic;
            var u = 1 - t;
            var x = u * u * u * startX + 3 * u * u * t * cubic.C1.X + 3 * u * t * t * cubic.C2.X + t * t * t * cubic.To.X;
            var y = u * u * u * startY + 3 * u * u * t * cubic.C1.Y + 3 * u * t * t * cubic.C2.Y + t * t * t * cubic.To.Y;
            points.Add(RegionPoint(new IslandSurfaceOutline.Point(x, y), scale, offsetX, offsetY));
        }
    }

    private static NativeMethods.POINT RegionPoint(IslandSurfaceOutline.Point dip, double scale, int offsetX, int offsetY)
    {
        return new NativeMethods.POINT
        {
            X = (int)Math.Round(dip.X * scale) + offsetX,
            Y = (int)Math.Round(dip.Y * scale) + offsetY,
        };
    }

    /// <summary>摘窗 region 还原矩形（经典切回）：SetWindowRgn(Zero)=无裁剪；句柄未就绪跳过
    /// （经典态本就无 region）。</summary>
    private void RestoreWindowRegion()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        try
        {
            NativeMethods.SetWindowRgn(_hwnd, IntPtr.Zero, true);
            _edgeBand?.Hide(); // 经典态无边带（region 摘除同步）
            _lastRegionKey = default; // 同几何再切回岛也须重设（region 已被摘，去重键不能拦）
            EnsureTopmostBand();
        }
        catch (Exception ex)
        {
            IslandTrace($"RestoreWindowRegion FAILED\n{ex}");
        }
    }

    /// <summary>region 操作后重钉 TOPMOST band（43850072289 实证：SetWindowRgn 可能把窗带出
    /// TOPMOST band——岛窗 IsWindowVisible=True 却被非置顶浏览器盖住，坐标点击按 z 序落浏览器，
    /// 看板不开；同构造的 tile 未走 region 无此症）。回读 WS_EX_TOPMOST 位写 trace 供 CI 取证。</summary>
    private void EnsureTopmostBand()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        IslandTrace($"topmost reasserted exStyle=0x{NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE).ToInt64():X}");
    }

    /// <summary>岛软阴影（BrimDeck 技法的原生实现）：两层同轮廓剪影，逐层加粗描边模拟模糊半径、
    /// 下沉模拟投影深度。2026-11-08 实机诊断后退休：不透明剪影 + 下沉在面下叠出全宽暗带（slab），
    /// 且收起态窗高改恒 32（无落影带）后剪影层被窗口裁掉——整层恒 Collapsed，此处早退不画。
    /// 保留骨架供经典形态/未来换用真模糊时挂回，现态不参与渲染。</summary>
    private void ApplyShadowGeometry(PathGeometry nearGeometry, PathGeometry farGeometry, double width, double height)
    {
        if (IslandShadowStack.Visibility != Visibility.Visible)
        {
            return;
        }
        IslandShadowNear.Data = nearGeometry;
        IslandShadowFar.Data = farGeometry;
        IslandShadowNear.StrokeThickness = 6;
        IslandShadowFar.StrokeThickness = 14;
        IslandShadowNear.RenderTransform = new TranslateTransform { Y = 2 };
        IslandShadowFar.RenderTransform = new TranslateTransform { Y = 3 };
    }
}
