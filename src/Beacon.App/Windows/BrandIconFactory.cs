using Beacon.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Beacon.App.Windows;

/// <summary>
/// 品牌图标工厂（设置目录/悬浮 tile 共用）：BrandIcons path → Path(Stretch=Uniform) 等比缩放，
/// 渲染失败退 null 落日志（单块失败不拖死调用方 UI）；未收录品牌由调用方选通用字形兜底。
/// 从 SettingsWindow 抽出：悬浮 tile（PinTile）同样要带数据源品牌图（用户令：悬浮框必须带对应 icon）。
/// </summary>
internal static class BrandIconFactory
{
    /// <summary>品牌图标元素；connectionType 未收录或渲染异常 → null。size 为显示 DIP（24×24 原生 path 等比缩入）。
    /// iconOverride = 组件级自选图标（widget.Config["icon"]，用户令 2026-10-10：图标要自带可选）——
    /// 非空时优先于连接品牌；未配/未收录仍退连接品牌 → 调用方兜底。
    /// 返回类型 FrameworkElement：WinUI 3 的 Grid.SetColumn 只收 FrameworkElement（UIElement 过不了，CI 实证），
    /// 调用方（PinTile 图标列）要直接进 Grid 布局。</summary>
    public static FrameworkElement? TryCreate(string? connectionType, double size, global::Windows.UI.Color foreground, ILogger? logger = null, string? iconOverride = null)
    {
        var brand = iconOverride is { Length: > 0 } ? iconOverride : connectionType;
        if (brand is null || !BrandIcons.TryGet(brand, out var pathData))
        {
            return null;
        }
        try
        {
            var geometry = Geometry(pathData);
            if (geometry.Figures.Count == 0)
            {
                return null; // 空几何照样渲染空块——退 null 让调用方字形兜底可见
            }
            // Shape 直接渲染（2026-10-09 三修）：此前 Viewbox(PathIcon) 的组合在 WinUI 3 量测为空
            // （IconElement 无固有尺寸），实际渲染一片空白且非 null 返回挡掉兜底字形——用户三次实测
            // 「icon 不显示」的根因。Path 有几何固有 bounds，Stretch.Uniform 按显示尺寸可靠 fit。
            return new global::Microsoft.UI.Xaml.Shapes.Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(foreground),
                Stretch = Stretch.Uniform,
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "品牌图标渲染失败，退回调用方兜底：{Type}", brand);
            return null;
        }
    }

    /// <summary>Simple Icons path d → PathGeometry：SvgPathParser 中性模型 → WinUI 代码构建。
    /// 不走 XamlReader——其对 Geometry 元素文本的处理与 WPF/UWP 不一致，解析失败会炸掉承载窗口
    /// （2026-10-08 用户实测设置打不开的根因），代码构建零字符串魔法、解析层在 Core 单测覆盖。
    /// 返回具体类型 PathGeometry（基类 Geometry 无 Figures，空几何检查要用）。</summary>
    public static PathGeometry Geometry(string pathData)
    {
        var geometry = new PathGeometry { FillRule = FillRule.Nonzero }; // SVG 填充默认 nonzero
        foreach (var figure in SvgPathParser.Parse(pathData))
        {
            var pathFigure = new PathFigure
            {
                StartPoint = Point(figure.Start),
                IsClosed = figure.Closed,
                IsFilled = true, // SVG 填充形：开放子路径也闭合填充
            };
            foreach (var segment in figure.Segments)
            {
                pathFigure.Segments.Add(segment switch
                {
                    SvgLine line => new LineSegment { Point = Point(line.To) },
                    SvgCubic cubic => new BezierSegment
                    {
                        Point1 = Point(cubic.Control1),
                        Point2 = Point(cubic.Control2),
                        Point3 = Point(cubic.To),
                    },
                    SvgQuadratic quad => new QuadraticBezierSegment { Point1 = Point(quad.Control), Point2 = Point(quad.To) },
                    SvgArc arc => new ArcSegment
                    {
                        Size = new global::Windows.Foundation.Size(arc.RadiusX, arc.RadiusY),
                        RotationAngle = arc.Rotation,
                        IsLargeArc = arc.LargeArc,
                        SweepDirection = arc.Sweep ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                        Point = Point(arc.To),
                    },
                    _ => throw new InvalidOperationException($"未知路径段类型：{segment.GetType().Name}"),
                });
            }
            geometry.Figures.Add(pathFigure);
        }
        return geometry;
    }

    /// <summary>Beacon.App.Windows 命名空间遮蔽全局 Windows.*，WinRT Point 必须 global::（CI 实证教训）。</summary>
    private static global::Windows.Foundation.Point Point(SvgPoint point) => new(point.X, point.Y);
}
