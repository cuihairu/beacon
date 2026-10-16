using System.Globalization;

namespace Beacon.Core.Services;

/// <summary>
/// 趋势序列（Phase 5「时间线、表格、图表与趋势组件」，三诊口径「图表走趋势序列，不引入新 Provider」）：
/// Provider 往 payload 写 <see cref="PayloadKey"/>（逗号/空白分隔的不变文化数字，如 "12,9,14,20"），
/// 这里做纯解析与图形就绪计算。fail-closed：键缺席、任何一项解析失败、不足两点都回空序列——
/// 宁可不画线，不画错线。方向判定带死区：|last-first| ≤ 跨度的 10% 视为平（跨度 0 恒平），
/// 免得抖动序列全被读成「上涨/下跌」。
/// </summary>
public static class TrendSeries
{
    /// <summary>趋势样本的 payload 键（Provider 可选写入）。</summary>
    public const string PayloadKey = "trend";

    /// <summary>方向死区（占序列跨度的比例）。</summary>
    public const double FlatBand = 0.1;

    public static IReadOnlyList<double> Parse(IReadOnlyDictionary<string, string> payload)
    {
        if (!payload.TryGetValue(PayloadKey, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }
        // 空位（连续分隔符）当分隔符噪声跳过；非有限值（NaN/Infinity）与解析失败一样弃全序列——
        // 一条坏样本会把画线打穿或除出 NaN，宁可不画
        var tokens = raw.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var points = new List<double>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value))
            {
                return [];
            }
            points.Add(value);
        }
        return points.Count >= 2 ? points : [];
    }

    /// <summary>归一化到 [0,1] 供画线（sparkline 纵轴口径）；全等序列回全线 0.5（退化平线，不除零）。</summary>
    public static IReadOnlyList<double> Normalize(IReadOnlyList<double> points)
    {
        if (points.Count == 0)
        {
            return [];
        }
        var min = points.Min();
        var max = points.Max();
        if (max - min <= 0)
        {
            return [.. Enumerable.Repeat(0.5, points.Count)];
        }
        return [.. points.Select(value => (value - min) / (max - min))];
    }

    /// <summary>方向：Up / Down / Flat（<see cref="FlatBand"/> 死区；点数不足 2 恒 Flat）。</summary>
    public static TrendDirection Direction(IReadOnlyList<double> points)
    {
        if (points.Count < 2)
        {
            return TrendDirection.Flat;
        }
        var first = points[0];
        var last = points[^1];
        var span = points.Max() - points.Min();
        if (last > first + span * FlatBand)
        {
            return TrendDirection.Up;
        }
        return last < first - span * FlatBand ? TrendDirection.Down : TrendDirection.Flat;
    }

    /// <summary>摘要（图表标题行口径：首末极值一次算齐）；空序列回 null。</summary>
    public static TrendSummary? Summarize(IReadOnlyList<double> points)
        => points.Count == 0
            ? null
            : new TrendSummary(points[0], points[^1], points.Min(), points.Max(),
                points[^1] - points[0], Direction(points));
}

/// <summary>趋势摘要（首/末/极值/净变动/方向）。</summary>
public sealed record TrendSummary(double First, double Last, double Min, double Max, double Delta, TrendDirection Direction);

/// <summary>趋势方向（死区内的抖动归平）。</summary>
public enum TrendDirection
{
    /// <summary>持平（死区内或样本不足）。</summary>
    Flat,

    /// <summary>上行（last 超出 first + 死区）。</summary>
    Up,

    /// <summary>下行（last 低于 first − 死区）。</summary>
    Down,
}
