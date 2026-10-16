namespace Beacon.Core.Services;

/// <summary>
/// 密度刻度（Phase 5「主题、密度与可访问性完善」）：紧凑/常规两档，间距缩放单一来源。
/// 常规 = 恒等；紧凑 = ×0.75 并取整（AwayFromZero，6→4.5→5 不走 banker 舍入）。
/// 配置字段是字符串（appearance.density）：缺省/空白/未知一律回退常规——密度是观感增强，
/// 配置打错不构成降级理由，fail-open 与「缺省=可见」同型。
/// App 侧消费点：DesignTokens 间距刻度经本类换算（本轮先落 Core 单源与配置字段，
/// 窗面逐间距收编随 CI 编译门批次走）。
/// </summary>
public static class UiDensity
{
    /// <summary>紧凑档缩放系数（间距 ×0.75）。</summary>
    public const double CompactScale = 0.75;

    /// <summary>解析密度字符串；未知/空白回常规。</summary>
    public static UiDensityKind Parse(string? raw)
        => string.IsNullOrWhiteSpace(raw) || raw.Trim().ToLowerInvariant() != "compact"
            ? UiDensityKind.Regular
            : UiDensityKind.Compact;

    /// <summary>档位缩放系数（常规 1.0、紧凑 0.75）。</summary>
    public static double ScaleOf(UiDensityKind kind)
        => kind == UiDensityKind.Compact ? CompactScale : 1.0;

    /// <summary>间距换算：×档位系数后取整（AwayFromZero）。</summary>
    public static double Spacing(double space, UiDensityKind kind)
        => Math.Round(space * ScaleOf(kind), MidpointRounding.AwayFromZero);
}

/// <summary>密度档位（紧凑/常规）。</summary>
public enum UiDensityKind
{
    /// <summary>常规（恒等间距）。</summary>
    Regular,

    /// <summary>紧凑（间距 ×0.75）。</summary>
    Compact,
}
