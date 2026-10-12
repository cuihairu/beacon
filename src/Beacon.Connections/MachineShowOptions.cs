using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Connections;

/// <summary>
/// machine.local / machine.net 可选展示项（2026-10-12 用户令「本地组件可选展示项」）：
/// 组件 config["show"] 逗号分隔键名（如 "cpu_percent,mem_percent"），留空 = 全部默认项
/// （默认 = 可选展示项上线前的版式，存量组件零迁移）。三处同源的根在 Provider：
/// 未选中的键不出 payload——悬浮 tile 数值位（value_text）、快捷面板 Summary、详情页
/// payload 表自然一致省略，不占位。原始台账键（mem_used/total_sent 等）不参与选择，
/// 恒留详情页（tile/Summary 本就不展示它们）。
/// </summary>
public static class MachineShowOptions
{
    public const string SettingKey = "show";

    /// <summary>规范序 = tile 数值位段序（local：CPU → 内存 → 磁盘）。</summary>
    public static readonly IReadOnlyList<string> LocalKeys = ["cpu_percent", "mem_percent", "disk_percent"];

    /// <summary>规范序（net：下行 → 上行 → IP）；Summary 段序仍维持既有「IP · ↓ · ↑」。</summary>
    public static readonly IReadOnlyList<string> NetKeys = ["down_bps", "up_bps", "ip"];

    /// <summary>默认 = 全选（缺 show 配置时的口径），与上线前默认版式逐字节一致。</summary>
    public static IReadOnlyList<string> DefaultsFor(string widgetType)
        => widgetType == MachineWidgetDescriptors.LocalType ? LocalKeys : NetKeys;

    /// <summary>
    /// 解析组件 config["show"]：空白 = 默认全选；键名大小写不敏感、去重、按规范序输出；
    /// 未知键或选空是配置错误，fail-loud Degraded（与阈值校验同口径，不静默）。
    /// </summary>
    public static IReadOnlyList<string> Resolve(string widgetType, WidgetConfig widget)
    {
        var known = DefaultsFor(widgetType);
        var raw = widget.Config.GetValueOrDefault(SettingKey)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return known;
        }
        var picked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            picked.Add(item.ToLowerInvariant());
        }
        var unknown = new List<string>();
        foreach (var item in picked)
        {
            if (!known.Contains(item))
            {
                unknown.Add(item);
            }
        }
        if (unknown.Count > 0)
        {
            throw new ConnectionException(
                $"show 只认 {string.Join(" / ", known)}，填了「{string.Join(",", unknown)}」。",
                ConnectionHealthState.Degraded);
        }
        if (picked.Count == 0)
        {
            throw new ConnectionException("show 至少要选一项（留空 = 全部默认项）。", ConnectionHealthState.Degraded);
        }
        return [.. known.Where(picked.Contains)];
    }

    /// <summary>宽度和最坏值行用的宽松解析：配置错误不抛（sizing 在 UI 侧不能炸），退默认全选
    /// （全选是最宽口径，按它算宽恒安全）。</summary>
    public static IReadOnlyList<string> ResolveOrAll(string widgetType, WidgetConfig widget)
    {
        try
        {
            return Resolve(widgetType, widget);
        }
        catch (ConnectionException)
        {
            return DefaultsFor(widgetType);
        }
    }
}
