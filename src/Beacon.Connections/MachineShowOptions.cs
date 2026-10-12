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

/// <summary>
/// machine 悬浮 tile 宽度（2026-10-12 用户令「宽度按最长一行数值文本能完整显示自己算，不要拍脑袋」）：
/// 取该组件所选展示项的最坏值行（百分比顶格 100、速率顶格「999.9 MB/s」即 FormatRate 最大长度形态、
/// IP 顶格 15 位、net 数值位只放得下「速率行 / IP 行」二选一取宽者），按保守字宽模型折算 DIP，
/// 加固定件（padding/灯/icon/列间距）与标签，下限 168（非 machine 组件与窄组件维持旧全局宽）。
/// 同口径三处共用：面板列宽（ReloadTiles 列定义 / PanelPxSize / WM_NCHITTEST HitWidthPx）与
/// 独立悬浮窗（ApplyDesignSize）。纯函数 Linux 可单测。
/// </summary>
public static class MachineTileSizing
{
    public const int FloorDips = 168;
    private const int ChromeDips = 74; // PinTile 固定件：padding 10+10、灯 8、icon 16、6 列 ColumnSpacing 6×5
    private const int SafetyDips = 4;  // DIP→物理像素取整余量

    /// <summary>值位字宽模型（DIP，FontSize 11 保守上界）：空格=4、%=10、箭头/·=11、CJK 及全角=11、
    /// 其余可见 ASCII=7（实测数字 ≈6.1、字母 ≤7）。</summary>
    public static int ValueTextWidthDips(string text) => Measure(text, ascii: 7, cjk: 11);

    /// <summary>组件 tile 宽（DIP）：machine 类型按所选展示项最坏值行推算，其余类型维持旧全局宽。</summary>
    public static int TileWidthDips(WidgetConfig widget)
    {
        if (widget.Type is not (MachineWidgetDescriptors.LocalType or MachineWidgetDescriptors.NetType))
        {
            return FloorDips;
        }
        var show = MachineShowOptions.ResolveOrAll(widget.Type, widget);
        var value = widget.Type == MachineWidgetDescriptors.LocalType
            ? LocalWorstValue(show, widget)
            : NetWorstValue(show);
        // 标签与 PinTile.LabelOf 同优先级（label 配置 → 类型短名）；标签列虽是 Star 可裁，
        // 宽度仍按实测给足——长显示名不裁字，代价是用户自己配出来的
        var labelText = widget.Config.GetValueOrDefault("label") is { Length: > 0 } label ? label
            : WidgetTypeNames.Of(widget.Type);
        return Math.Max(FloorDips, ChromeDips + SafetyDips + Measure(labelText, ascii: 8, cjk: 12) + ValueTextWidthDips(value));
    }

    /// <summary>local 最坏值行：数值位 = 所选三段全拼（内存/磁盘恒有值，CPU 首刷缺基线不出段）。</summary>
    private static string LocalWorstValue(IReadOnlyList<string> show, WidgetConfig widget)
    {
        // 磁盘段前缀与 Provider 同构：disk_path 去尾部斜杠（未配 = 系统盘/根分区 → "C:" 最长 2 字符）
        var diskHead = widget.Config.GetValueOrDefault("disk_path") is { Length: > 0 } path
            ? path.TrimEnd('\\', '/')
            : "C:";
        List<string> segments = [];
        if (show.Contains("cpu_percent"))
        {
            segments.Add("CPU 100%");
        }
        if (show.Contains("mem_percent"))
        {
            segments.Add("内存 100%");
        }
        if (show.Contains("disk_percent"))
        {
            segments.Add($"{diskHead} 100%");
        }
        return string.Join(" · ", segments);
    }

    /// <summary>net 最坏值行：数值位同时只放一种形态（稳态 = 速率行；首刷无基线 = IP 行；无网卡 = 提示行），
    /// 取所选形态里的最宽者，不把互斥形态相加。</summary>
    private static string NetWorstValue(IReadOnlyList<string> show)
    {
        const string ratesForm = "↓999.9 MB/s ↑999.9 MB/s"; // FormatRate 最大长度形态（999.9 顶格）；速率对单空格并排（与 Summary 同构）
        const string ipForm = "255.255.255.255";
        return show.Contains("down_bps") || show.Contains("up_bps") ? ratesForm
            : show.Contains("ip") ? ipForm
            : string.Empty;
    }

    private static int Measure(string text, int ascii, int cjk)
    {
        var width = 0;
        foreach (var ch in text)
        {
            width += ch switch
            {
                ' ' => 4,
                '%' => 10,
                '↓' or '↑' or '·' => 11,
                >= (char)0x2E80 => cjk, // CJK/全角区
                _ => ascii,
            };
        }
        return width;
    }
}
