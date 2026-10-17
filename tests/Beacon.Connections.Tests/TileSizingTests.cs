using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// tile 宽度全类型字宽预算（2026-10-12 用户令二修「真字宽预算，适用所有 tile」）：
/// 字宽模型逐字符可复算（数字/字母/ASCII=0.62em、%=0.9em、空格=0.36em、箭头/·/…/CJK=1.0em，
/// 标签列 SemiBold ASCII=0.66em），各类型最坏值行 → 各列宽 → 合计（chrome 44 = padding 20+灯 8+icon 16，
/// 列间距 6/列间，安全余量 4，下限 168）。硬口径：数值列预算 ≥ 任何可达形态，TextTrimming 不触发。
/// </summary>
public sealed class TileSizingTests
{
    private static WidgetConfig Widget(string type, params (string Key, string Value)[] config)
    {
        var widget = new WidgetConfig { Id = "w1", Type = type, ConnectionId = "c1" };
        foreach (var (key, value) in config)
        {
            widget.Config[key] = value;
        }
        return widget;
    }

    // —— 字宽模型 ——

    [Theory]
    [InlineData("100%", 11, false, 31)]    // (3×0.62+0.9)×11 = 30.36
    [InlineData("内存 100%", 11, false, 57)] // (2+0.36+1.86+0.9)×11 = 56.32
    [InlineData("↓·", 11, false, 22)]      // 2×1.0×11
    [InlineData(" ", 11, false, 4)]        // 0.36×11 = 3.96
    [InlineData("本机", 12, true, 24)]     // 2×1.0×12（CJK 与字重无关）
    [InlineData("GitHub", 12, true, 48)]   // 6×0.66×12 = 47.52
    public void TextWidth_PerClassPerFontSize(string text, double font, bool semibold, int expected)
        => Assert.Equal(expected, TileSizing.TextWidthDips(text, font, semibold));

    // —— 额度类：多窗口最坏值行 ——

    [Fact]
    public void TileWidth_BigModel_MultiWindowWorst_PoolWithoutReadout()
    {
        // 值行 "100%/5h 100%/w 100%/M" = 147；标签「智谱」24；池 16（读数被多窗口收起不预算）；
        // 5 列（灯/icon/标签/数值/池）间距 24 → 44+24+147+16+24+4 = 259
        Assert.Equal(259, TileSizing.TileWidthDips(Widget(BigModelWidgetDescriptors.UsageType)));
    }

    [Fact]
    public void TileWidth_Ark_MultiWindowWorst()
    {
        // 值行同 bigmodel 147；标签「火山方舟」48 → 44+48+147+16+24+4 = 283
        Assert.Equal(283, TileSizing.TileWidthDips(Widget(ArkWidgetDescriptors.UsageType)));
    }

    [Fact]
    public void TileWidth_Kimi_ThreeWindows_PoolWithoutReadout()
    {
        // 值行三窗全拼 "100%/5h窗口 100%/w窗口 剩100%/套餐" = 217（11 ASCII×0.62 + %×3×0.9 + 空格×2×0.36 +
        // CJK 7×1.0 = 19.72em×11 上取整，与 KimiWindows 同构）；Progress = 最差窗已用占比 → 池 16；
        // 多窗形态读数被收起；标签 Kimi 32；5 列间距 24 → 44+32+217+16+24+4 = 337
        Assert.Equal(337, TileSizing.TileWidthDips(Widget(KimiWidgetDescriptors.CodingType)));
    }

    [Fact]
    public void TileWidth_Sensenova_PoolNameWindowPercent()
    {
        // 值行最坏 = 夹具形态「Flash-Lite池 5h 已用100%」= 154（10 ASCII×0.62 + 池/已用 3.0 + 空格 0.36×2 +
        // 5h 1.24 + 100 1.86 + % 0.9 = 13.92em×11 上取整）；Progress = 最差窗已用占比 → 池 16；
        // value_text 已自带百分比 → 读数收起；标签「商汤」CJK 24；5 列间距 24 → 44+24+154+16+24+4 = 266
        Assert.Equal(266, TileSizing.TileWidthDips(Widget(SenseNovaWidgetDescriptors.UsageType)));
    }

    // —— 单百分比额度类：读数 + 池都预算 ——

    [Fact]
    public void TileWidth_Qwen_ExhaustedTextIsTheWorst()
    {
        // 值行最坏 = 用尽路径显式「已用尽」3 CJK = 33（> "100%" 31）；读数 "100%"@12 = 34；池 16；
        // 6 列间距 30；标签「阿里千问」48 → 44+48+33+16+34+30+4 = 209
        Assert.Equal(209, TileSizing.TileWidthDips(Widget(QwenWidgetDescriptors.UsageType)));
    }

    [Fact]
    public void TileWidth_HttpQuota_Percent_WithReadoutAndPool()
    {
        // percent = used/total 计算值不封顶（可超 100）→ 最坏 "999.9%" 44；读数 "100%"@12 = 34（读数来自
        // Clamp 后的 Progress，恒 ≤100）；池 16；6 列间距 30 → 44+32+44+16+34+30+4 = 204
        Assert.Equal(204, TileSizing.TileWidthDips(Widget(HttpWidgetDescriptors.QuotaType)));
    }

    [Fact]
    public void TileWidth_Mimo_WorstPathIsLocalCount()
    {
        // 四路径最宽 = 本机计数「9999.9M 次」63（FormatCount M 尾数顶格；$消费 55 / 无额度口 44 / NN% 31 都更窄）；
        // 标签「小米 MiMo」60 → 44+60+63+16+34+30+4 = 251
        Assert.Equal(251, TileSizing.TileWidthDips(Widget(MiMoWidgetDescriptors.UsageType)));
    }

    // —— 无进度语义类型 ——

    [Fact]
    public void TileWidth_GitHubPullRequests_CountBound()
    {
        // 值行 "99999 open" 66；标签 GitHub 48；4 列间距 18 → 180
        Assert.Equal(180, TileSizing.TileWidthDips(Widget(GitHubWidgetDescriptors.PullRequestsType)));
    }

    [Fact]
    public void TileWidth_LabelPriority_MatchesPinTile()
    {
        // 标签优先级 label → repo → owner → 类型短名（与 PinTile.LabelOf 同源）：
        // repo 配置进宽（github 卡常用 repo 当显示名）
        var repo = TileSizing.TileWidthDips(Widget(GitHubWidgetDescriptors.PullRequestsType, ("repo", "microsoft/vscode")));
        Assert.Equal(44 + 127 + 66 + 18 + 4, repo); // "microsoft/vscode" = 16×0.66×12 = 126.72 → 127
        Assert.True(TileSizing.TileWidthDips(Widget(GitHubWidgetDescriptors.PullRequestsType, ("label", "x"), ("repo", "microsoft/vscode")))
            < repo); // label 压过 repo
    }

    [Theory]
    [InlineData("copilot.usage")]   // "9999/9999" 62 + Copilot 56 → 184
    [InlineData("opencode.usage")]  // "$9999.99" 55 + OpenCode 64 → 185
    [InlineData("deepseek.balance")] // "¥99999.99" 62 + DeepSeek 64 → 192
    [InlineData("moonshot.balance")] // 同款余额形态 62 + Moonshot 64 → 192
    [InlineData("codex.usage")]     // "9999.9M tok" 73 + Codex 40 → 179
    public void TileWidth_NumericTypes_BudgetOwnWorstText(string type)
    {
        var width = TileSizing.TileWidthDips(Widget(type));
        Assert.Equal(type switch
        {
            "copilot.usage" => 184,
            "opencode.usage" => 185,
            "deepseek.balance" => 192,
            "moonshot.balance" => 192,
            _ => 179,
        }, width);
    }

    [Fact]
    public void TileWidth_Claude_TokenFormIsTheWorst()
    {
        // 两形态取宽者：$成本 "$999.99" 48 < token 合计 "9999.9M tok" 73（FormatCount M 尾数顶格）
        // → 44+48+73+18+4 = 187（成本形态 162 会落 168 下限，token 形态顶开后不再触底）
        Assert.Equal(187, TileSizing.TileWidthDips(Widget(ClaudeWidgetDescriptors.UsageType)));
    }

    [Theory]
    [InlineData("github.actions.runs", "GitHub", 48, 202)]   // 无 hint 键 → 兜底 7 CJK+… = 88
    [InlineData("http.status", "HTTP", 32, 186)]
    [InlineData("jenkins.build", "Jenkins", 56, 210)]
    [InlineData("herald.incidents", "Herald", 48, 202)]
    public void TileWidth_FallbackTypes_SummaryBound(string type, string _, int labelWidth, int expected)
        => Assert.Equal(expected, TileSizing.TileWidthDips(Widget(type)));

    [Fact]
    public void TileWidth_GitWorkspace_CountsFormIsTheWorst()
    {
        // 值行 = 计数段五数顶格 "↑99999↓99999 · 改 99999 · stash 99999"：↑↓···改 5×1.0 + 20 数字×0.62
        // + 6 空格×0.36 + stash 5×0.62 = 22.66em×11 = 250；标签 = 类型尾段「git」24 → 44+24+250+18+4 = 340
        // （无 hint 键时代的兜底 226 会把 ↑↓/改 计数全截没——2026-10-12 巡检补 value_text 后按真形态预算）
        Assert.Equal(340, TileSizing.TileWidthDips(Widget(GitWorkspaceDescriptors.Type)));
    }

    [Fact]
    public void WorstValueText_Fallback_IsSevenCjkPlusEllipsis()
        => Assert.Equal("状状状状状状状…", TileSizing.FallbackWorstValue);

    // —— machine：值行按所选展示项构造（新字宽模型重算） ——

    [Fact]
    public void TileWidth_LocalDefault_FitsWorstValueLine()
    {
        // 值行 "CPU 100% · 内存 100% · C: 100%"：14 ASCII×0.62 + 3%×0.9 + 7 空格×0.36 + 2·×1.0 + 2CJK×1.0
        // = 17.9em × 11 = 196.9 → 197；标签「本机」24 → 44+24+197+18+4 = 287
        Assert.Equal(287, TileSizing.TileWidthDips(Widget(MachineWidgetDescriptors.LocalType)));
    }

    [Fact]
    public void TileWidth_NetDefault_RatesFormIsTheWorst()
    {
        // 值行 "↓1023.9 MB/s ↑1023.9 MB/s"（FormatRate 尾数上限 1023.9，非 999.9）：
        // 22 ASCII×0.62 + 3 空格×0.36 + 2 箭头×1.0 = 15.48em×11 = 170.28 → 171 → 44+24+171+18+4 = 261
        Assert.Equal(261, TileSizing.TileWidthDips(Widget(MachineWidgetDescriptors.NetType)));
    }

    [Fact]
    public void TileWidth_NarrowSelection_LandsOnFloor()
    {
        // 只留 CPU：「CPU 100%」55 → 44+24+55+18+4 = 145 < 168 → 下限兜底
        Assert.Equal(168, TileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.LocalType, ("show", "cpu_percent"))));
    }

    [Fact]
    public void TileWidth_LongDiskPath_CountsIntoWidth()
    {
        // disk_path 进磁盘段前缀（与 Provider 的 diskHead 同构）："/mnt/data" 比默认 "C:" 多 7 ASCII
        // = +4.34em×11 = +48 → 值行 245 → 44+24+245+18+4 = 335
        Assert.Equal(335, TileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.LocalType, ("disk_path", "/mnt/data"))));
    }

    [Fact]
    public void TileWidth_LabelConfig_CountsIntoWidth()
    {
        // 标签配置进宽：「工位机」= 3×12 = 36 → 44+36+197+18+4 = 299
        Assert.Equal(299, TileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.LocalType, ("label", "工位机"))));
    }

    [Fact]
    public void TileWidth_NetIpOnly_FitsAddress()
    {
        // "255.255.255.255" = 15×0.62em ×11 = 102.3 → 103 → 44+24+103+18+4 = 193
        Assert.Equal(193, TileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.NetType, ("show", "ip"))));
    }

    // —— 尺寸档（Phase 5 行 4）：Config["sizeTier"] → tile 高，词汇/系数沿 UiDensity 单源 ——

    [Theory]
    [InlineData(null, 32)]        // 缺省 = 常规（fail-open，G 系像素锚不动）
    [InlineData("", 32)]          // 空白 = 常规
    [InlineData("regular", 32)]   // 显式常规 = 恒等
    [InlineData("compact", 24)]   // 32×0.75
    [InlineData("spacious", 40)]  // 32×1.25
    [InlineData("huge", 32)]      // 未知词 = 常规（打错不构成降级理由）
    [InlineData("COMPACT", 24)]   // 大小写不敏感
    public void TileHeight_SizeTier_FailOpenToRegular(string? tier, int expected)
    {
        var widget = Widget(MachineWidgetDescriptors.NetType);
        if (tier is not null)
        {
            widget.Config[TileSizing.SizeTierKey] = tier;
        }
        Assert.Equal(expected, TileSizing.TileHeightDips(widget));
    }

    [Fact]
    public void TileWidth_SpaciousTier_ReadoutColumnBudgetGrows()
    {
        // 宽度只随读数列字号联动（进度高×0.375 → 12→15）：「100%」预算 34→42（+8），
        // 其余列字号不随高度缩放——「数值列不裁字」硬口径在 spacious 档不破
        var regular = Widget(HttpWidgetDescriptors.QuotaType);
        var spacious = Widget(HttpWidgetDescriptors.QuotaType, (TileSizing.SizeTierKey, "spacious"));
        Assert.Equal(TileSizing.TileWidthDips(regular) + 8, TileSizing.TileWidthDips(spacious));
    }

    [Fact]
    public void TileWidth_NonProgressType_WidthIndependentOfTier()
    {
        // 无进度语义类型没有读数列：尺寸档只缩高，宽不动（缩宽必裁字）
        var regular = Widget(MachineWidgetDescriptors.NetType, ("show", "ip"));
        var compact = Widget(MachineWidgetDescriptors.NetType, ("show", "ip"), (TileSizing.SizeTierKey, "compact"));
        Assert.Equal(TileSizing.TileWidthDips(regular), TileSizing.TileWidthDips(compact));
    }
}
