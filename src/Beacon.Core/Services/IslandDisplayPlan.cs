using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 岛收起态展示计划（ISLAND-D1..D3，2026-11-09 用户令）：把「展示方式档位 + 上岛白名单 + 真实
/// widget 状态」三者折叠成一份**可见面清单**的纯函数。决策全部沉 Core（Linux 本地红先行可测），
/// App 侧只消费计划做渲染/喂数/计时——CS 风险不上推到本地编不了的 App 层。
/// 面序=虚拟环/条在前（继承 ISLAND-RING-1 勾选口径）、上岛白名单序续后；
/// 数据面规则：state.Progress 有值走条面、无值走灯点+标签 chip 面；
/// 零数据/断连/过期一律占位面（不造进度、不造摘要——data_quality 口径）。
/// 档位语义：single=序首常驻、stack=收起行上限内全显、carousel=徽标+单面轮转（i mod n 回卷）。
/// </summary>
public static class IslandDisplayPlan
{
    /// <summary>单组件常驻：只渲染序首面，其余进溢出池。</summary>
    public const string ModeSingle = "single";

    /// <summary>多组件堆叠：上限内全显（默认档，与既有环/条并置同形，升级零行为变更）。</summary>
    public const string ModeStack = "stack";

    /// <summary>徽标+轮播：徽标（未读/异常计数）+ 单面轮转。</summary>
    public const string ModeCarousel = "carousel";

    /// <summary>收起行可见面上限（宽度自适应由 App 按 DIP 实测收口，此处是决策侧硬顶）。</summary>
    public const int MaxCollapsedFaces = 4;

    /// <summary>展示方式归一：非法/空值 fail-open 回 stack（默认档=现状同形，不炸不造假）。</summary>
    public static string NormalizeMode(string? mode) => mode switch
    {
        ModeSingle => ModeSingle,
        ModeCarousel => ModeCarousel,
        _ => ModeStack,
    };

    /// <summary>计算岛收起态可见面计划（纯投影：读配置与状态，不改布局不发请求）。</summary>
    /// <param name="app">全局设置（IslandDisplayMode / IslandComponents / HiddenComponents）。</param>
    /// <param name="widgets">当前全部真实组件（widgets.json 配置序）。</param>
    /// <param name="states">运行时状态表（缺键=尚未取到数据 → 占位）。</param>
    /// <param name="context">聚合上下文：未读/异常计数、健康份额、总体级别（虚拟环/条数据源）。</param>
    /// <param name="carouselIndex">轮播档轮转下标（负值归一，其他档不读）。</param>
    public static IslandPlan Compute(
        AppConfig app,
        IReadOnlyList<WidgetConfig> widgets,
        IReadOnlyDictionary<string, WidgetState> states,
        IslandContext context,
        int carouselIndex = 0)
    {
        var candidates = new List<IslandFace>();
        if (ComponentCenterCatalog.IslandRingVisible(app))
        {
            candidates.Add(new IslandFace(
                ComponentCenterCatalog.IslandRingId, "ring", Sanitize(context.OkShare), context.Overall,
                CountText(context.UnreadCount, context.AbnormalCount), null, Placeholder: false));
        }
        if (ComponentCenterCatalog.IslandBarVisible(app))
        {
            candidates.Add(new IslandFace(
                ComponentCenterCatalog.IslandBarId, "bar", Sanitize(context.OkShare), context.Overall,
                CountText(context.UnreadCount, context.AbnormalCount), null, Placeholder: false));
        }
        foreach (var widgetId in app.IslandComponents)
        {
            var widget = widgets.FirstOrDefault(candidate => string.Equals(candidate.Id, widgetId, StringComparison.Ordinal));
            if (widget is null || ComponentCenterCatalog.IsHidden(app, widgetId))
            {
                continue; // 白名单里的陈旧 id / 组件中心勾掉的组件：跳过不占位（ISLAND-RING-1 口径）
            }
            candidates.Add(WidgetFace(widget, states.TryGetValue(widgetId, out var state) ? state : null));
        }

        var mode = NormalizeMode(app.IslandDisplayMode);
        var visible = mode switch
        {
            ModeSingle => candidates.Take(1),
            ModeCarousel => CarouselWindow(candidates, carouselIndex),
            _ => candidates.Take(MaxCollapsedFaces),
        };
        var faces = visible.ToList();

        // 徽标裁决与计数文本同源：未读优先、异常次之、全零隐藏
        var (badgeVisible, badgeCount) = context.UnreadCount > 0
            ? (true, context.UnreadCount)
            : context.AbnormalCount > 0 ? (true, context.AbnormalCount) : (false, 0);
        return new IslandPlan(mode, faces, candidates.Count - faces.Count, badgeVisible, badgeCount, carouselIndex);
    }

    /// <summary>真实组件 → 数据面：进度有值走条面，否则灯点 chip 面；零数据/断连/过期占位不造假。</summary>
    private static IslandFace WidgetFace(WidgetConfig widget, WidgetState? state)
    {
        var label = ComponentCenterCatalog.ItemLabel(widget);
        if (state is null || state.IsStale || !state.ConnectionHealthy)
        {
            // 占位：不造进度不造摘要；级别仍如实（断连保留最后一次真实级别，零数据退 Info 灰点）
            return new IslandFace(widget.Id, "chip", null, state?.Severity ?? Severity.Info, label, null, Placeholder: true);
        }
        string kind;
        double? progress;
        if (state.Progress is { } value)
        {
            kind = "bar";
            progress = Sanitize(value);
        }
        else
        {
            kind = "chip";
            progress = null;
        }
        return new IslandFace(widget.Id, kind, progress, state.Severity, label, state.Summary, Placeholder: false);
    }

    /// <summary>轮转窗：第 i 拍取池内单面，负下标归一回卷（((i % n) + n) % n）；空池给空窗。</summary>
    private static IEnumerable<IslandFace> CarouselWindow(List<IslandFace> candidates, int carouselIndex)
    {
        if (candidates.Count == 0)
        {
            yield break;
        }
        var index = ((carouselIndex % candidates.Count) + candidates.Count) % candidates.Count;
        yield return candidates[index];
    }

    /// <summary>进度份额清洗：非有限值归零（占位口径不造数）。</summary>
    private static double Sanitize(double fraction) => double.IsFinite(fraction) ? Math.Clamp(fraction, 0.0, 1.0) : 0.0;

    /// <summary>计数文本裁决：未读优先、异常次之、全零不落字（与现环心数字逐字节同口径）。</summary>
    private static string CountText(int unread, int abnormal)
        => unread > 0 ? unread.ToString() : abnormal > 0 ? abnormal.ToString() : string.Empty;
}

/// <summary>岛聚合上下文：App 从聚合状态/通知计数采样后传入（App 侧只采样不决策）。</summary>
public sealed record IslandContext(
    int UnreadCount,
    int AbnormalCount,
    double OkShare,
    Severity Overall);

/// <summary>岛收起态单面：虚拟环/条或真实组件的可见投影（App 按面上的值渲染，不再自行裁决）。</summary>
/// <param name="WidgetId">island.progressring / island.progressbar / 真实 WidgetId。</param>
/// <param name="Kind">ring | bar | chip（环/条/灯点+标签）。</param>
/// <param name="Progress">0..1 份额（chip/占位恒 null——不造假）。</param>
/// <param name="Severity">级别色锚（占位面保留最后真实级别，零数据退 Info）。</param>
/// <param name="Text">环/条=计数文本（全零空串）；chip=组件标签。</param>
/// <param name="Summary">真实摘要（chip tooltip 用；虚拟面/占位面 null）。</param>
/// <param name="Placeholder">true=零数据/断连/过期占位（App 画灰点淡字，不渲染真实数值）。</param>
public sealed record IslandFace(
    string WidgetId,
    string Kind,
    double? Progress,
    Severity Severity,
    string Text,
    string? Summary,
    bool Placeholder);

/// <summary>岛收起态展示计划（Compute 输出）：Faces=本拍可见面（已按档裁窗）、
/// OverflowCount=溢出池大小（Faces.Count+OverflowCount=候选总数）、Badge=轮播徽标、
/// CarouselIndex=本次取窗下标（App 下一拍 +1 传回）。</summary>
public sealed record IslandPlan(
    string Mode,
    IReadOnlyList<IslandFace> Faces,
    int OverflowCount,
    bool BadgeVisible,
    int BadgeCount,
    int CarouselIndex);
