namespace Beacon.Core.Models;

/// <summary>统一五级严重度（全产品唯一口径），Provider 不允许自定义状态（RFC §4.1）。
/// 排序：Critical &gt; Error &gt; Warning &gt; Info &gt; Success（Success 为正常基线）。</summary>
public enum Severity
{
    /// <summary>正常基线（聚合空集的总体状态）。</summary>
    Success = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
}

public static class SeverityExtensions
{
    /// <summary>排序用数值：Critical &gt; Error &gt; Warning &gt; Info &gt; Success。</summary>
    public static int Rank(this Severity severity) => (int)severity;

    public static bool IsAtLeast(this Severity severity, Severity floor) => severity.Rank() >= floor.Rank();

    public static Severity Max(this Severity left, Severity right) => left.Rank() >= right.Rank() ? left : right;
}
