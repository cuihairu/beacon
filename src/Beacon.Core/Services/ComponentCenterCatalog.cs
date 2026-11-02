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

    /// <summary>分组展示序（非空组按此序排，other 恒殿后）；文案与 WidgetTypeNames 同款 Core 中文直配。</summary>
    private static readonly (string Key, string DisplayName)[] CategoryOrder =
    [
        (CategoryStatus, "状态监控"),
        (CategoryMachine, "本机信息"),
        (CategoryEvents, "事件与订阅"),
        (CategoryQuota, "额度与用量"),
        (CategoryOther, "其他"),
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
