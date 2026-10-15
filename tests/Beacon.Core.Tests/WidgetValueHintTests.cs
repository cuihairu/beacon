using Beacon.Core.Models;
using Beacon.Core.Selectors;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// WidgetValueHint：悬浮 tile 数值位短文本。payload 夹具照九家 provider 实产键值形状裁剪
/// （src/Beacon.Connections/*UsageProvider.cs），表驱动锁定「数不出值」的回归。
/// </summary>
public sealed class WidgetValueHintTests
{
    private static WidgetState State(
        string summary,
        Dictionary<string, string>? payload = null,
        bool stale = false) => new()
    {
        WidgetId = "w",
        WidgetType = "test",
        ConnectionId = "c",
        Severity = Severity.Success,
        Summary = summary,
        Payload = payload ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        IsStale = stale,
    };

    public static TheoryData<WidgetState, string> ValueCases => new()
    {
        // github.pull_requests
        { State("PRs · 3 open", new() { ["open_count"] = "3" }), "3 open" },
        // bigmodel.usage / ark.usage / http.quota：最差窗口已用%
        { State("GLM · 42% 已用", new() { ["percent"] = "42" }), "42%" },
        { State("方舟 · 5h 77%", new() { ["percent"] = "77" }), "77%" },
        // kimi.coding：已用%（2026-10-13 用户令，与 bigmodel/ark 统一已用口径）
        { State("Kimi · 周已用 70%", new() { ["weekly_percent"] = "70" }), "70%" },
        { State("Kimi · 滚动已用 11.5%", new() { ["rolling_percent"] = "11.5" }), "11.5%" },
        // copilot.usage：premium 请求 已用/含额（1500 含额 - 1499 剩余 = 1）
        { State("Copilot · Pro", new() { ["premium_interactions_entitlement"] = "1500", ["premium_interactions_remaining"] = "1499" }), "1/1500" },
        // opencode.usage：有限额=限额换算已用%（2026-10-13 用户令）；无上限=消费金额兜底
        { State("OpenCode · 25/100", new() { ["spent_usd"] = "25.00", ["percent"] = "25" }), "25%" },
        { State("OpenCode · 12.34/无上限", new() { ["spent_usd"] = "12.34" }), "$12.34" },
        // deepseek.balance：余额（币种符号自适应）
        { State("DeepSeek · ¥110.00", new() { ["total"] = "110.00", ["currency"] = "CNY" }), "¥110.00" },
        { State("DeepSeek · $4.50", new() { ["total"] = "4.50", ["currency"] = "USD" }), "$4.50" },
        { State("DeepSeek · 7.00 EUR", new() { ["total"] = "7.00", ["currency"] = "EUR" }), "7.00" },
        // moonshot.balance：开放平台余额（平台只按 CNY 计价）
        { State("Moonshot · ¥110.00", new() { ["available_balance"] = "110.00" }), "¥110.00" },
        // claude.usage：今日成本
        { State("Claude · $0.00 · in 1.2K", new() { ["cost"] = "0.00" }), "$0.00" },
        // claude.usage：无成本时退 token 合计
        { State("Claude · in 1.2K out 300", new() { ["tokens_in"] = "1200", ["tokens_out"] = "300" }), "1.5K tok" },
        { State("Claude · tokens", new() { ["tokens_in"] = "999", ["tokens_out"] = "1" }), "1K tok" },
        // codex.usage：本机会话累计 token
        { State("Codex · 3 会话", new() { ["tokens_in"] = "1234567" }), "1.2M tok" },
        // mimo.usage：本机计数（2026-10-10 用户令：额度位给真实口径，模型数不得占额度位）
        { State("MiMo · 本机累计 128 次", new() { ["total_calls"] = "128" }), "128 次" },
        { State("MiMo · 大数", new() { ["total_calls"] = "1234567" }), "1.2M 次" },
        // mimo.usage：无计数源时 provider 显式 value_text（模型数降 Summary 次行）
        { State("MiMo · 官方无额度接口 · 8 模型可用", new() { ["value_text"] = "无额度口", ["models"] = "8" }), "无额度口" },
        // 多窗口额度（2026-10-12 用户令）：两窗以上拼「NN%/窗」——已用口径（bigmodel/ark）
        { State("GLM · 三窗", new() { ["rolling_percent"] = "5", ["weekly_percent"] = "70", ["monthly_percent"] = "80" }), "5%/5h 70%/w 80%/M" },
        // 只出实际存在的窗口（空串 = 缺，provider 对空窗写 ""）；月窗缺失
        { State("方舟 · 双窗", new() { ["rolling_percent"] = "5", ["weekly_percent"] = "70.5", ["monthly_percent"] = "" }), "5%/5h 70.5%/w" },
        // 窗序固定 5h→周→月（与 payload 书写序无关）
        { State("GLM · 乱序", new() { ["monthly_percent"] = "80", ["rolling_percent"] = "5", ["weekly_percent"] = "70" }), "5%/5h 70%/w 80%/M" },
        // kimi 三窗形态（2026-10-13 用户令）：窗口带「窗口」字样 + 套餐「剩%」前缀（非窗口口径）；
        // 门禁 = kimi 独有 *_remaining_percent 键（bigmodel/ark 同有 rolling/weekly 键，不可劫持）
        { State("Kimi · 三窗", new() { ["rolling_percent"] = "0", ["rolling_remaining_percent"] = "100", ["weekly_percent"] = "62", ["weekly_remaining_percent"] = "38", ["plan_remaining_percent"] = "38" }), "0%/5h窗口 62%/w窗口 剩38%/套餐" },
        { State("Kimi · 双窗", new() { ["rolling_percent"] = "5", ["rolling_remaining_percent"] = "95", ["weekly_percent"] = "20", ["weekly_remaining_percent"] = "80" }), "5%/5h窗口 20%/w窗口" },
        // kimi 单窗不进三窗形态（段数 <2），走既有单窗链
        { State("Kimi · 单窗", new() { ["rolling_percent"] = "5", ["rolling_remaining_percent"] = "95", ["weekly_percent"] = "" }), "5%" },
        // 单窗不进多窗口形态，走既有单窗链（bigmodel/ark 主窗 = percent；kimi = *_percent 已用键）
        { State("Kimi · 单窗", new() { ["rolling_percent"] = "5", ["weekly_percent"] = "" }), "5%" },
        { State("GLM · 单窗", new() { ["percent"] = "5" }), "5%" },
        // 优先级：value_text（provider 显式）压过多窗口
        { State("MiMo · 显式", new() { ["value_text"] = "无额度口", ["rolling_percent"] = "5", ["weekly_percent"] = "70" }), "无额度口" },
        // 优先级：percent 压过 models
        { State("x", new() { ["percent"] = "50", ["models"] = "9" }), "50%" },
        // stale 优先于 Summary 兜底，但压不过已有数值
        { State("卡住了", stale: true), "stale" },
    };

    [Theory]
    [MemberData(nameof(ValueCases))]
    public void Of_MapsProviderPayloadToShortValue(WidgetState state, string expected)
        => Assert.Equal(expected, WidgetValueHint.Of(state));

    [Fact]
    public void Of_UnknownPayload_FallsBackToTrimmedSummary()
    {
        Assert.Equal("演示组件 AB…", WidgetValueHint.Of(State("演示组件 ABCDEF"))); // 7 字符 + …
        Assert.Equal("短摘要", WidgetValueHint.Of(State("短摘要"))); // ≤8 字符不截
    }

    [Fact]
    public void Of_EmptyValues_AreTreatedAsMissing()
    {
        // provider 对空值写空串（claude cost 无成本时 ""）：不能渲染成 "$"
        Assert.Equal("Kimi · …", WidgetValueHint.Of(State("Kimi · 无数据", new() { ["cost"] = "" })));
    }

    [Fact]
    public void SuppressProgressReadout_MultiWindowTrue_SingleWindowFalse()
    {
        // 多窗口：数值文本已含主位窗口百分比 → 读数收起（各家统一已用口径同规则）
        var multi = State("GLM", new() { ["rolling_percent"] = "5", ["weekly_percent"] = "70" });
        Assert.True(WidgetValueHint.SuppressProgressReadout(multi));
        var kimi = State("Kimi", new() { ["rolling_percent"] = "5", ["weekly_percent"] = "20" });
        Assert.True(WidgetValueHint.SuppressProgressReadout(kimi));
        // 单窗：读数维持常驻（现状不变）
        var single = State("GLM", new() { ["percent"] = "5" });
        Assert.False(WidgetValueHint.SuppressProgressReadout(single));
        // 无额度键的组件（machine/github）：读数逻辑不受影响
        Assert.False(WidgetValueHint.SuppressProgressReadout(State("本机", new() { ["cpu_percent"] = "12" })));
    }

    [Fact]
    public void SuppressProgressReadout_ExplicitValueText_Suppressed()
    {
        // provider 显式 value_text（sensenova 池名+窗名+已用% / mimo 45.5%）：数值位文本即最终口径，
        // 自动读数是重复占宽 → 收起（2026-10-13）
        Assert.True(WidgetValueHint.SuppressProgressReadout(State("商汤", new() { ["value_text"] = "Flash-Lite池 5h 已用100%" })));
        // value_text 与 Progress 缺一不可：无 Progress 的类型本就走收起早退，不受此键影响
        Assert.False(WidgetValueHint.SuppressProgressReadout(State("GLM", new() { ["total"] = "42.5" })));
    }
}
