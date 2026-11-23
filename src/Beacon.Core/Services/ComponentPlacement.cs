using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 组件去向判定（SET-UX-04，2026-11-24 用户令原型落地批2）：每组件一行显示「实际状态 + 阻挡原因」
/// 的单一判定源。桌面列镜像 L0 宿主过滤链（PinnedHostWindow.ReloadTiles 与 FloatingTileHost.ReloadTiles
/// 同序）：①不支持钉选 ②全局隐藏（同时退出聚合，StatusAggregator 源头过滤） ③来源停用
/// （连接 Enabled=false 显式停用；缺连接不算停用——与宿主同口径） ④数值总开关关闭
/// （WidgetDisplayPolicy.IsNumericSuppressed）→ 未钉选 → 桌面已显示（形态按布局档注记：集中面板 / 独立窗口）。
/// 悬浮岛列按 IslandDisplayPlan 渲染口径：岛关闭 → 已隐藏（岛不渲染已隐藏组件）→ 未上岛 →
/// 溢出（收起窗位次超过档位上限：stack 4 面 / single 1 面；carousel 轮播可达不算溢出）→ 已上岛。
/// 纯函数、无 IO：只投影配置与描述符，判定与两宿主/岛计划共用同一词表，不再各写各的。
/// 状态键为封闭词表（App 侧按键配色/选恢复入口）。
/// </summary>
public static class ComponentPlacement
{
    // —— 桌面列状态词表 ——
    public const string DesktopShown = "shown";
    public const string DesktopUnpinned = "unpinned";
    public const string DesktopUnsupported = "unsupported";
    public const string DesktopGlobalHidden = "globalhidden";
    public const string DesktopSourceDisabled = "sourcedisabled";
    public const string DesktopNumericSuppressed = "numericsuppressed";

    // —— 悬浮岛列状态词表 ——
    public const string IslandOnboarded = "onboarded";
    public const string IslandOverflows = "overflows";
    public const string IslandNotOnboarded = "notonboarded";
    public const string IslandOff = "islandoff";
    public const string IslandHidden = "islandhidden";

    /// <summary>去向报告：状态键（封闭词表）+ 中文短句（App 行内直显）。Shown=该载体当前渲染此组件。</summary>
    public sealed record PlacementReport(
        string DesktopState,
        string DesktopNote,
        bool DesktopShown,
        string IslandState,
        string IslandNote,
        bool IslandShown);

    /// <summary>单个真实组件的去向判定（virtual 环/条不走此口径：它们不占桌面，显隐直读勾选）。
    /// <param name="connectionEnabled">连接显式 Enabled=false 才 false；缺连接=true（与宿主「缺连接不拦」同口径）。</param>
    /// <param name="islandRank">组件在 AppConfig.IslandComponents 的 1 基位次；0=未上岛（列表序=上岛序）。</param></summary>
    public static PlacementReport Describe(
        AppConfig app,
        WidgetConfig widget,
        WidgetTypeDescriptor? descriptor,
        bool connectionEnabled,
        bool numericFloatingEnabled,
        bool floatingMode,
        bool islandOn,
        int islandRank)
    {
        var desktop = DescribeDesktop(widget, descriptor, app, connectionEnabled, numericFloatingEnabled, floatingMode);
        var island = DescribeIsland(app, widget.Id, islandOn, islandRank);
        return new PlacementReport(desktop.State, desktop.Note, desktop.Shown, island.State, island.Note, island.Shown);
    }

    /// <summary>桌面列判定（宿主过滤链同序的单一判定链；floatingMode=true 且可独立悬浮时注记窗口形态）。</summary>
    public static (string State, string Note, bool Shown) DescribeDesktop(
        WidgetConfig widget,
        WidgetTypeDescriptor? descriptor,
        AppConfig app,
        bool connectionEnabled,
        bool numericFloatingEnabled,
        bool floatingMode)
    {
        if (descriptor is not { PinSupported: true })
        {
            return (DesktopUnsupported, "类型不支持钉选", false);
        }
        if (ComponentCenterCatalog.IsHidden(app, widget.Id))
        {
            return (DesktopGlobalHidden, "已隐藏（同时退出状态聚合）", false);
        }
        if (!connectionEnabled)
        {
            return (DesktopSourceDisabled, "来源已停用", false);
        }
        if (WidgetDisplayPolicy.IsNumericSuppressed(descriptor, numericFloatingEnabled))
        {
            return (DesktopNumericSuppressed, "「在桌面显示数值和额度」已关闭", false);
        }
        if (!widget.Pinned)
        {
            return (DesktopUnpinned, "未钉选到桌面", false);
        }
        return (DesktopShown, floatingMode ? "已显示（独立窗口）" : "已显示（集中面板）", true);
    }

    /// <summary>悬浮岛列：IslandDisplayPlan 渲染口径的投影（stack 4 面 / single 1 面 / carousel 全可达）。</summary>
    public static (string State, string Note, bool Shown) DescribeIsland(AppConfig app, string widgetId, bool islandOn, int islandRank)
    {
        if (!islandOn)
        {
            return (IslandOff, "悬浮岛未启用", false);
        }
        if (ComponentCenterCatalog.IsHidden(app, widgetId))
        {
            return (IslandHidden, "已隐藏（岛不渲染）", false);
        }
        if (islandRank <= 0)
        {
            return (IslandNotOnboarded, "未上岛", false);
        }
        // 溢出口径沿 IslandDisplayPlan.Compute：stack 取前 MaxCollapsedFaces 面、single 只取第 1 面、
        // carousel 轮播全量可达（未知档按 stack 同口径——NormalizeMode fail-open 的镜像）。
        var limit = IslandDisplayPlan.NormalizeMode(app.IslandDisplayMode) switch
        {
            IslandDisplayPlan.ModeSingle => 1,
            IslandDisplayPlan.ModeCarousel => int.MaxValue,
            _ => IslandDisplayPlan.MaxCollapsedFaces,
        };
        if (islandRank > limit)
        {
            return (IslandOverflows, $"上岛序第 {islandRank} 位，收起行放不下（轮播档可轮到）", false);
        }
        return (IslandOnboarded, $"已上岛 · 第 {islandRank} 位", true);
    }
}
