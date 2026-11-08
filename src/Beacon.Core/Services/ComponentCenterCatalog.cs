using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 组件中心目录（Component Center，设置 → 组件中心 / 托盘菜单）：把当前全部可展示组件
/// （widgets.json 的 WidgetConfig 实例）按功能类别分组列出，供勾选显隐。
/// 类别划分按真实注册的组件类型（各连接 Provider 的 *WidgetDescriptors.All，类型键 = 前缀.子键）：
/// <list type="bullet">
/// <item>状态监控 status：github.* / jenkins.* / http.status（CI 灯、PR 数、服务健康）</item>
/// <item>本机信息 machine：machine.local / machine.net / machine.git（零凭据本机资源）</item>
/// <item>事件与订阅 events：herald.*（事件台账、订阅源、投递失败）</item>
/// <item>额度与用量 quota：bigmodel/ark/claude/deepseek/kimi/moonshot/mimo/qwen/sensenova/
/// codex/copilot/opencode 的 usage|balance|coding|tokenplan + http.quota</item>
/// <item>其他 other：未知类型兜底（不虚构类别，命名缺失不阻断渲染）</item>
/// </list>
/// 纯函数、无 IO：分组只投影配置，不改布局不触发数据请求（Phase 3 行 6 不变量同守）；
/// 显隐判定与落库写入单源（IsHidden / SetVisible），App 两宿主与设置页共用此一份口径。
/// </summary>
public static class ComponentCenterCatalog
{
    public const string CategoryStatus = "status";
    public const string CategoryMachine = "machine";
    public const string CategoryEvents = "events";
    public const string CategoryQuota = "quota";
    public const string CategoryOther = "other";
    /// <summary>悬浮岛（ISLAND-RING-1，2026-11-07 用户令）：进度环/进度条是组件中心里的两个组件——
    /// 岛显示什么完全由勾选状态决定（勾环显环、勾条显条、都勾都显、都不勾不显），无「默认样式」概念。</summary>
    public const string CategoryIsland = "island";
    public const string IslandRingId = "island.progressring";
    public const string IslandBarId = "island.progressbar";

    /// <summary>分组展示序（非空组按此序排，other 兜底、island 虚拟组殿后——不抢真实组件的列表位置）；文案与 WidgetTypeNames 同款 Core 中文直配。</summary>
    private static readonly (string Key, string DisplayName)[] CategoryOrder =
    [
        (CategoryStatus, "状态监控"),
        (CategoryMachine, "本机信息"),
        (CategoryEvents, "事件与订阅"),
        (CategoryQuota, "额度与用量"),
        (CategoryOther, "其他"),
        (CategoryIsland, "悬浮岛"),
    ];

    /// <summary>额度/用量类前缀（usage/balance/coding/tokenplan 家族）。</summary>
    private static readonly HashSet<string> QuotaPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bigmodel", "ark", "claude", "deepseek", "kimi", "moonshot",
        "mimo", "qwen", "sensenova", "codex", "copilot", "opencode",
    };

    /// <summary>组件类型 → 类别键（未知前缀 = other，不抛）。</summary>
    public static string CategoryOf(string widgetType)
    {
        var prefix = widgetType.Split('.')[0].ToLowerInvariant();
        return prefix switch
        {
            "github" or "jenkins" => CategoryStatus,
            "machine" => CategoryMachine,
            "herald" => CategoryEvents,
            "island" => CategoryIsland,
            "http" => widgetType.EndsWith(".quota", StringComparison.OrdinalIgnoreCase)
                ? CategoryQuota
                : CategoryStatus,
            _ => QuotaPrefixes.Contains(prefix) ? CategoryQuota : CategoryOther,
        };
    }

    /// <summary>按类别分组（空类别不出组，组内保持 widgets.json 配置序）。</summary>
    public static IReadOnlyList<ComponentCenterGroup> Group(IEnumerable<WidgetConfig> widgets)
    {
        var buckets = new Dictionary<string, List<ComponentCenterItem>>(StringComparer.Ordinal);
        // 悬浮岛进度组件（虚拟组件）恒在册：零 widget 也出岛组，组件中心里随时可勾
        buckets[CategoryIsland] = [.. IslandItems];
        foreach (var widget in widgets)
        {
            var category = CategoryOf(widget.Type);
            if (!buckets.TryGetValue(category, out var items))
            {
                buckets[category] = items = [];
            }
            items.Add(new ComponentCenterItem(widget.Id, ItemLabel(widget), widget.Type, widget.Pinned));
        }

        var groups = new List<ComponentCenterGroup>();
        foreach (var (key, displayName) in CategoryOrder)
        {
            if (buckets.TryGetValue(key, out var items) && items.Count > 0)
            {
                groups.Add(new ComponentCenterGroup(key, displayName, items));
            }
        }
        return groups;
    }

    /// <summary>悬浮岛进度组件在册条目（虚拟组件：不来自 widgets.json，恒在册不依赖任何连接；
    /// 勾选显隐与落库走与真实组件同一套 IsHidden/SetVisible → HiddenComponents）。</summary>
    public static IReadOnlyList<ComponentCenterItem> IslandItems { get; } =
    [
        new(IslandRingId, "进度环", IslandRingId, Pinned: false),
        new(IslandBarId, "进度条", IslandBarId, Pinned: false),
    ];

    /// <summary>岛进度环组件是否被勾选显示（缺省=显示；勾掉即岛撤环）。</summary>
    public static bool IslandRingVisible(AppConfig app) => !IsHidden(app, IslandRingId);

    /// <summary>岛进度条组件是否被勾选显示（缺省=显示；勾掉即岛撤条）。</summary>
    public static bool IslandBarVisible(AppConfig app) => !IsHidden(app, IslandBarId);

    /// <summary>该组件是否已「上岛」（ISLAND-D2，2026-11-09 用户令）：读 AppConfig.IslandComponents
    /// 白名单，缺席=不上岛（opt-in：存量配置升级后岛上零真实组件，行为不变）。</summary>
    public static bool IslandOnboarded(AppConfig app, string widgetId)
        => app.IslandComponents.Contains(widgetId);

    /// <summary>上岛勾选落库写入（设置页唯一写入口）：onboarded=true 追加进白名单尾（列表序=上岛序）、
    /// false 移出；幂等。与 SetVisible 同一套纪律：只改集不落盘，SaveApp 由调用方收口。</summary>
    public static void SetIslandOnboarded(AppConfig app, string widgetId, bool onboarded)
    {
        if (onboarded)
        {
            if (!app.IslandComponents.Contains(widgetId))
            {
                app.IslandComponents.Add(widgetId);
            }
        }
        else
        {
            app.IslandComponents.RemoveAll(id => string.Equals(id, widgetId, StringComparison.Ordinal));
        }
    }

    /// <summary>上岛候选清单（ISLAND-D2）：全部真实组件按 widgets.json 配置序列出，供岛组「上岛」
    /// 勾选区渲染（与 Group() 分离——通用分组页的岛组只放虚拟环/条，托盘/设置页消费口径不变）。</summary>
    public static IReadOnlyList<ComponentCenterItem> OnboardCandidates(IReadOnlyList<WidgetConfig> widgets)
        => [.. widgets.Select(widget => new ComponentCenterItem(widget.Id, ItemLabel(widget), widget.Type, widget.Pinned))];

    /// <summary>组件条目显示名：与 PinTile 标签同源口径——优先用户配置 label/repo/owner，否则类型短名。</summary>
    public static string ItemLabel(WidgetConfig widget)
    {
        foreach (var key in new[] { "label", "repo", "owner" })
        {
            if (widget.Config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return WidgetTypeNames.Of(widget.Type);
    }

    /// <summary>该组件是否被组件中心勾选隐藏（L0 两宿主共用判定；Ordinal 精确匹配 WidgetId）。</summary>
    public static bool IsHidden(AppConfig app, string widgetId)
        => app.HiddenComponents.Contains(widgetId);

    /// <summary>勾选显隐落库写入（设置页唯一写入口）：visible=false 进隐藏集、true 移出；幂等。</summary>
    public static void SetVisible(AppConfig app, string widgetId, bool visible)
    {
        if (visible)
        {
            app.HiddenComponents.RemoveAll(id => string.Equals(id, widgetId, StringComparison.Ordinal));
        }
        else if (!app.HiddenComponents.Contains(widgetId))
        {
            app.HiddenComponents.Add(widgetId);
        }
    }
}

/// <summary>组件中心条目：一个可展示组件实例（widgets.json WidgetConfig）的展示投影。</summary>
public sealed record ComponentCenterItem(
    string WidgetId,
    string Label,
    string TypeName,
    bool Pinned);

/// <summary>组件中心分组：同一功能类别的组件集合（Key 稳定序，DisplayName 中文直配）。</summary>
public sealed record ComponentCenterGroup(
    string Key,
    string DisplayName,
    IReadOnlyList<ComponentCenterItem> Items);
