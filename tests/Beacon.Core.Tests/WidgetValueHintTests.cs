using Beacon.Core.Models;
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
        // kimi.coding：剩余%（卡面同口径）
        { State("Kimi · 周剩 30%", new() { ["weekly_remaining_percent"] = "30" }), "剩30%" },
        { State("Kimi · 滚动剩 88.5%", new() { ["rolling_remaining_percent"] = "88.5" }), "剩88.5%" },
        // copilot.usage：premium 请求 已用/含额（1500 含额 - 1499 剩余 = 1）
        { State("Copilot · Pro", new() { ["premium_interactions_entitlement"] = "1500", ["premium_interactions_remaining"] = "1499" }), "1/1500" },
        // opencode.usage：自然月消费
        { State("OpenCode · 12.34/无上限", new() { ["spent_usd"] = "12.34" }), "$12.34" },
        // deepseek.balance：余额（币种符号自适应）
        { State("DeepSeek · ¥110.00", new() { ["total"] = "110.00", ["currency"] = "CNY" }), "¥110.00" },
        { State("DeepSeek · $4.50", new() { ["total"] = "4.50", ["currency"] = "USD" }), "$4.50" },
        { State("DeepSeek · 7.00 EUR", new() { ["total"] = "7.00", ["currency"] = "EUR" }), "7.00" },
        // claude.usage：今日成本
        { State("Claude · $0.00 · in 1.2K", new() { ["cost"] = "0.00" }), "$0.00" },
        // claude.usage：无成本时退 token 合计
        { State("Claude · in 1.2K out 300", new() { ["tokens_in"] = "1200", ["tokens_out"] = "300" }), "1.5K tok" },
        { State("Claude · tokens", new() { ["tokens_in"] = "999", ["tokens_out"] = "1" }), "1K tok" },
        // codex.usage：本机会话累计 token
        { State("Codex · 3 会话", new() { ["tokens_in"] = "1234567" }), "1.2M tok" },
        // mimo.usage：模型目录
        { State("MiMo · 模型目录", new() { ["models"] = "12" }), "12 模型" },
        // 优先级：percent 压过其余
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
}
