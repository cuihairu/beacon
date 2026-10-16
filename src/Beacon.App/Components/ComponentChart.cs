using System.Globalization;
using Beacon.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Beacon.App.Components;

/// <summary>
/// 图表组件（Phase 5「时间线、表格、图表与趋势组件」，三诊口径「图表走趋势序列」）：
/// sparkline 折线——Provider 往 payload 写 TrendSeries 键（不变文化数字串），
/// 归一化口径单源 Core TrendSeries.Normalize（本件不重算min/max），本件只画线。
/// 点列不足两点返回空折线（不画线），空态说明由调用面承担。
/// </summary>
internal static class ComponentChart
{
    private const double SparklineStrokeThickness = 1.5;
    private const double SparklineEdgeInsetDips = 1; // 端点贴边会裁半线宽，内缩 1 DIP

    /// <summary>sparkline：width × height 框内的归一化折线（Stroke 由调用面选色）。</summary>
    public static Polyline Sparkline(IReadOnlyList<double> points, double width, double height, Brush stroke)
    {
        var normalized = TrendSeries.Normalize(points);
        var polyline = new Polyline
        {
            Stroke = stroke,
            StrokeThickness = SparklineStrokeThickness,
            Fill = null,
        };
        if (normalized.Count < 2)
        {
            return polyline;
        }

        var innerWidth = Math.Max(width - 2 * SparklineEdgeInsetDips, 0);
        var innerHeight = Math.Max(height - 2 * SparklineEdgeInsetDips, 0);
        for (var index = 0; index < normalized.Count; index++)
        {
            var fraction = (double)index / (normalized.Count - 1);
            polyline.Points.Add(new Point(
                SparklineEdgeInsetDips + innerWidth * fraction,
                SparklineEdgeInsetDips + innerHeight * (1 - normalized[index])));
        }
        return polyline;
    }

    /// <summary>趋势图题注（min–max 不变文化，末值 ±净变动）；摘要为空返回空串。</summary>
    public static string Caption(TrendSummary? summary)
        => summary is null
            ? string.Empty
            : string.Format(CultureInfo.InvariantCulture,
                "{0:R} – {1:R} (last {2:R}, {3:R})",
                summary.Min, summary.Max, summary.Last, summary.Delta);
}
