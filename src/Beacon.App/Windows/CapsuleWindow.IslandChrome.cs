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
        IslandTrace($"outline {width}x{height} segs={outlineGeometry.Figures[0].Segments.Count} " +
            $"row={PillRow.ActualWidth}x{PillRow.ActualHeight} " +
            $"pill={PillArea.ActualWidth}x{PillArea.ActualHeight} " +
            $"stack={SurfaceStack.ActualWidth} margin={SurfaceStack.Margin} " +
            $"brand={BrandMark.Visibility}/{BrandMark.ActualWidth} " +
            $"count={CountText.Visibility} progress={IslandProgress.Visibility}/{IslandProgress.ActualWidth} " +
            $"align={PillRow.HorizontalAlignment} root={Root.ActualWidth}x{Root.ActualHeight}");

        static global::Windows.Foundation.Point ToPoint(IslandSurfaceOutline.Point p) => new(p.X, p.Y);
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
