using Beacon.Core.Services;

namespace Beacon.Core.Selectors;

/// <summary>
/// L2 看板来源分组与点击过滤（2026-10-14 用户单①：默认全部 + 可点击过滤 + 当前类目界面可见）。
/// chip 标签与过滤判定同源（纯函数，Linux 可单测）：CI 运行单独成 chip（与 PR 计数区分），
/// 其余按 WidgetTypeNames 短名（与 tile 标签同源；旧 GLM/ARK 硬编码并入）。
/// </summary>
public static class QuickPanelFilter
{
    public static string SourceKey(string widgetType) => widgetType switch
    {
        "github.actions.runs" => "CI", // CI 运行单独成 chip（与 PR 计数区分）
        _ => WidgetTypeNames.Of(widgetType), // 短名统一 Core 单表（与 tile 标签同源）
    };

    /// <summary>过滤判定：<c>null</c> = 全部（默认态）；否则只看该来源。</summary>
    public static bool Matches(string? activeSource, string widgetType)
        => activeSource is null || SourceKey(widgetType) == activeSource;
}
