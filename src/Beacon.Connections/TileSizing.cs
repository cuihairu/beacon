using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Connections;

/// <summary>
/// 悬浮 tile 宽度（2026-10-12 用户令二修「真字宽预算，适用所有 tile」）：按字号（em = 列 FontSize）
/// 逐字符类别折算最坏文本宽，对 tile 内每一列分别预算——标签列（最长 label）、数值列（该组件最坏
/// ValueHint 文本）、进度读数列（最坏 100%）——再加灯/icon/圆池固定件、ColumnSpacing 与左右 padding，
/// 下限 <see cref="FloorDips"/>。所有类型同一路径（machine 只是值行按所选展示项构造，其余类型查表）；
/// 面板列宽 = 全部钉选 tile 的最大值，独立悬浮窗按各自组件宽。
/// 字宽系数（Segoe UI 上界、上一代实测模型校准）：数字/字母/ASCII ≈0.62em（数字实测 0.556，
/// +0.06 补大写字母 C/M/B ≈0.6–0.9 的抬升——硬口径「数值列不裁字」优先于系数精度）、% ≈0.9em
/// （Segoe ≈0.84，0.56 装不下）、空格 ≈0.36em、箭头/·/…/CJK/全角 ≈1.0em；标签列 SemiBold 用
/// ≈0.66em。纯函数 Linux 可单测；同口径三处共用：面板列宽（ReloadTiles 列定义 / PanelPxSize /
/// WM_NCHITTEST HitWidthPx）与独立悬浮窗（ApplyDesignSize）。
/// </summary>
public static class TileSizing
{
    public const int FloorDips = 168;
    private const int PaddingDips = 20;  // PinTile Root padding 10+10
    private const int LightDips = 8;     // 状态灯
    private const int IconDips = 16;     // 品牌图标
    private const int PoolDips = 16;     // 圆池列（progressStyle=pool 时可见，bar 模式 Collapsed）
    private const int SpacingDips = 6;   // Grid ColumnSpacing
    private const int SafetyDips = 4;    // DIP→物理像素取整余量 + 标签 SemiBold 残差
    private const double LabelFontDips = 12;    // PinTile._label FontSize
    private const double ValueFontDips = 11;    // PinTile._value FontSize
    private const double ProgressFontDips = 12; // PinTile.ProgressFontDips = 32×0.375（与 PinTile 同步改）

    /// <summary>数值位兜底形态的最坏文本：Summary[..7]+"…" 的最宽可能是 7 个 CJK + 省略号。</summary>
    public static string FallbackWorstValue { get; } = new string('状', 7) + "…";

    /// <summary>字宽预算（DIP）：按字号逐字符类别折算后向上取整。semibold = 标签列（SemiBold 略宽）。</summary>
    public static int TextWidthDips(string text, double fontSize, bool semibold = false)
    {
        var em = 0.0;
        foreach (var ch in text)
        {
            em += ch switch
            {
                ' ' => 0.36,
                '%' => 0.9,
                '…' => 1.0,
                '↓' or '↑' or '·' => 1.0,
                >= (char)0x2E80 => 1.0, // CJK/全角区
                _ => semibold ? 0.66 : 0.62,
            };
        }
        return (int)Math.Ceiling(em * fontSize);
    }

    /// <summary>组件 tile 宽（DIP）：逐列最坏文本预算，所有类型同一路径。</summary>
    public static int TileWidthDips(WidgetConfig widget)
    {
        var value = WorstValueText(widget);
        var progress = ProgressTypes.Contains(widget.Type); // 有进度语义：池列可能可见、读数列可能常驻
        var readout = progress && !MultiWindowWorstTypes.Contains(widget.Type); // 多窗口最坏形态收起读数（数值已含百分比）
        // 标签与 PinTile.LabelOf 同优先级（label → repo → owner → 类型短名）；标签列虽是 Star 可裁，
        // 预算仍按最坏给足——长显示名不裁字，代价是用户自己配出来的
        var label = LabelOf(widget);
        var columns = 4 + (progress ? 1 : 0) + (readout ? 1 : 0); // 灯/icon/标签/数值（+池）（+读数）
        var width = PaddingDips + LightDips + IconDips
            + TextWidthDips(label, LabelFontDips, semibold: true)
            + TextWidthDips(value, ValueFontDips)
            + (progress ? PoolDips : 0)
            + (readout ? TextWidthDips("100%", ProgressFontDips) : 0)
            + SpacingDips * (columns - 1)
            + SafetyDips;
        return Math.Max(FloorDips, width);
    }

    /// <summary>有进度语义的类型（Provider 会给非空 Progress → 池/读数列可能出现）。</summary>
    private static readonly HashSet<string> ProgressTypes =
        [HttpWidgetDescriptors.QuotaType, BigModelWidgetDescriptors.UsageType, ArkWidgetDescriptors.UsageType,
         QwenWidgetDescriptors.UsageType, MiMoWidgetDescriptors.UsageType, KimiWidgetDescriptors.CodingType];

    /// <summary>最坏值行 = 多窗口形态的类型：同口径窗口键 ≥2 个非空时读数被收起（WidgetValueHint.SuppressProgressReadout
    /// 的 ≥2 窗判据），预算不含读数列；单窗形态值行骤窄，腾出的宽远多于读数所需（147→31 vs +34+6）。</summary>
    private static readonly HashSet<string> MultiWindowWorstTypes =
        [BigModelWidgetDescriptors.UsageType, ArkWidgetDescriptors.UsageType, KimiWidgetDescriptors.CodingType];

    /// <summary>每类组件数值列的最坏 ValueHint 文本（WidgetValueHint.Of 优先级链可达形态的并集取最宽；
    /// 预算只需 ≥ 实际，取 generous 上界——数值列必须完整，宁可宽几 DIP）。</summary>
    public static string WorstValueText(WidgetConfig widget) => widget.Type switch
    {
        // 多窗口全拼（三窗 100% 顶格；两窗/单窗都是其子集形态）
        BigModelWidgetDescriptors.UsageType or ArkWidgetDescriptors.UsageType => "100%/5h 100%/w 100%/M",
        // kimi 已用口径双窗（2026-10-13 统一已用口径，Progress = 最差窗已用占比 → 池列预算、
        // 读数被多窗口收起）；接口只给 rolling/weekly 无月窗键，接口加月窗时同步抬一行
        KimiWidgetDescriptors.CodingType => "100%/5h 100%/w",
        // machine 按所选展示项构造最坏值行（数值位 = value_text 同构）
        MachineWidgetDescriptors.LocalType => LocalWorstValue(MachineShowOptions.ResolveOrAll(widget.Type, widget), widget),
        MachineWidgetDescriptors.NetType => NetWorstValue(MachineShowOptions.ResolveOrAll(widget.Type, widget)),
        // 单百分比：http.quota 的 percent = used/total 计算值**不封顶**（可超 100 供严重度判定），
        // 按 3 位带小数顶格；qwen percent 已 Clamp(0,100)，最坏 = 用尽路径显式「已用尽」（33 > "100%" 31）
        HttpWidgetDescriptors.QuotaType => "999.9%",
        QwenWidgetDescriptors.UsageType => "已用尽",
        // 计数：open PR 顶格 5 位
        GitHubWidgetDescriptors.PullRequestsType => "99999 open",
        // copilot：premium 已用/含额（Pro 1500/月，4 位 generous）
        CopilotWidgetDescriptors.UsageType => "9999/9999",
        // 金额（0.00 顶格）
        OpenCodeWidgetDescriptors.UsageType => "$9999.99",
        DeepSeekWidgetDescriptors.BalanceType => "¥99999.99",
        // claude 两形态取宽者：$成本 48 < token 合计（FormatCount M 形态无上界，尾数顶格 9999.9 ≈ 百亿 token）
        ClaudeWidgetDescriptors.UsageType => "9999.9M tok",
        // token 短格式最大形态（FormatCount 0.# M 尾数顶格 9999.9）
        CodexWidgetDescriptors.UsageType => "9999.9M tok",
        // mimo 四路径（$月消费 55/无额度口 44/NN% 31/本机计数 63）取最宽
        MiMoWidgetDescriptors.UsageType => "9999.9M 次",
        // moonshot：开放平台余额（0.00 顶格，CNY 与 Summarize 同口径）
        MoonshotWidgetDescriptors.BalanceType => "¥99999.99",
        // machine.git：计数段五数顶格（ahead/behind/dirty/stash 5 位，与 PR 计数同档 generous）——
        // 无上游形态「改 99999 · stash 99999」更窄，上游全拼为最坏
        GitWorkspaceDescriptors.Type => "↑99999↓99999 · 改 99999 · stash 99999",
        // 无 hint 键的类型（github.actions/http.status/jenkins/herald×3）走 Summary[..7]+"…" 兜底
        _ => FallbackWorstValue,
    };

    /// <summary>tile 标签最坏文本：与 PinTile.LabelOf 同优先级（label → repo → owner → 类型短名）。</summary>
    public static string LabelOf(WidgetConfig widget)
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
        // FormatRate 档内尾数上限 1023.9（≥1024 才换档，"0.0" 一位小数），非 999.9——千兆/万兆网卡
        // MB/KB 档 6 位尾数真实可达；速率对单空格并排（与 Summary 同构）
        const string ratesForm = "↓1023.9 MB/s ↑1023.9 MB/s";
        const string ipForm = "255.255.255.255";
        return show.Contains("down_bps") || show.Contains("up_bps") ? ratesForm
            : show.Contains("ip") ? ipForm
            : string.Empty;
    }
}
