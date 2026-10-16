using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 5「更丰富的布局与条件显示」：状态字段驱动的条件显示判定（封闭语法，缺省=可见、
/// 声明解析不出=隐藏）。
/// </summary>
public sealed class ComponentVisibilityTests
{
    private static WidgetState State(Func<WidgetState, WidgetState>? with = null)
    {
        var state = new WidgetState
        {
            WidgetId = "w-1",
            WidgetType = "github.pr.list",
            ConnectionId = "conn-1",
            Severity = Severity.Warning,
            Summary = "3 open",
            FetchedAt = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero),
        };
        return with is null ? state : with(state);
    }

    // —— 缺省面 ——

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_BlankCondition_IsVisible(string? condition)
        => Assert.True(ComponentVisibility.Evaluate(condition, State()));

    // —— 标志位 ——

    [Fact]
    public void Evaluate_StaleFlag_FollowsIsStale()
    {
        Assert.True(ComponentVisibility.Evaluate("stale", State(s => s with { IsStale = true })));
        Assert.False(ComponentVisibility.Evaluate("stale", State()));
        Assert.True(ComponentVisibility.Evaluate("!stale", State()));
    }

    [Fact]
    public void Evaluate_OfflineFlag_MatchesUnhealthyOrStale()
    {
        Assert.True(ComponentVisibility.Evaluate("offline", State(s => s with { ConnectionHealthy = false })));
        Assert.True(ComponentVisibility.Evaluate("offline", State(s => s with { IsStale = true })));
        Assert.False(ComponentVisibility.Evaluate("offline", State()));
    }

    // —— has: 值存在 ——

    [Fact]
    public void Evaluate_HasStatePath_FollowsPresence()
    {
        Assert.True(ComponentVisibility.Evaluate("has:progress", State(s => s with { Progress = 0.5 })));
        Assert.False(ComponentVisibility.Evaluate("has:progress", State()));
        Assert.True(ComponentVisibility.Evaluate("has:detailUrl", State(s => s with { DetailUrl = "https://x" })));
        Assert.False(ComponentVisibility.Evaluate("has:detailUrl", State()));
    }

    [Fact]
    public void Evaluate_HasPayloadKey_FollowsPresence()
    {
        var state = State(s => s with { Payload = new Dictionary<string, string> { ["branch"] = "main" } });

        Assert.True(ComponentVisibility.Evaluate("has:payload:branch", state));
        Assert.False(ComponentVisibility.Evaluate("has:payload:missing", state));
        Assert.True(ComponentVisibility.Evaluate("!has:payload:missing", state));
    }

    [Fact]
    public void Evaluate_HasDetails_ExcludesValueHintOnlyRows()
    {
        Assert.True(ComponentVisibility.Evaluate("has:details",
            State(s => s with { Payload = new Dictionary<string, string> { ["repo"] = "o/r" } })));
        Assert.False(ComponentVisibility.Evaluate("has:details",
            State(s => s with { Payload = new Dictionary<string, string> { [WidgetFeedback.ValueHintKey] = "38%" } })));
    }

    // —— severity 比较 ——

    [Theory]
    [InlineData("Success", false)]
    [InlineData("Info", false)]
    [InlineData("Warning", true)]
    [InlineData("Error", true)]
    [InlineData("Critical", true)]
    public void Evaluate_SeverityAtLeastWarning_FollowsEnumOrder(string severity, bool expected)
        => Assert.Equal(expected,
            ComponentVisibility.Evaluate("severity>=warning", State(s => s with { Severity = Enum.Parse<Severity>(severity) })));

    [Fact]
    public void Evaluate_SeverityOperators()
    {
        Assert.True(ComponentVisibility.Evaluate("severity=warning", State()));
        Assert.True(ComponentVisibility.Evaluate("severity!=critical", State()));
        Assert.True(ComponentVisibility.Evaluate("severity<warning", State(s => s with { Severity = Severity.Info })));
        Assert.False(ComponentVisibility.Evaluate("severity>critical", State(s => s with { Severity = Severity.Critical })));
    }

    // —— progress 数值比较 ——

    [Fact]
    public void Evaluate_ProgressNumericComparison()
    {
        Assert.True(ComponentVisibility.Evaluate("progress>=0.75", State(s => s with { Progress = 0.75 })));
        Assert.False(ComponentVisibility.Evaluate("progress>0.75", State(s => s with { Progress = 0.75 })));
        Assert.True(ComponentVisibility.Evaluate("progress<1", State(s => s with { Progress = 0.5 })));
        Assert.False(ComponentVisibility.Evaluate("progress>=0.5", State())); // Progress 缺席不可求值
    }

    // —— 字符串等值 ——

    [Fact]
    public void Evaluate_LifecycleAndSummaryEquality_IgnoreCase()
    {
        Assert.True(ComponentVisibility.Evaluate("lifecycle=running",
            State(s => s with { Lifecycle = LifecycleState.Running })));
        Assert.False(ComponentVisibility.Evaluate("lifecycle=queued",
            State(s => s with { Lifecycle = LifecycleState.Running })));
        Assert.True(ComponentVisibility.Evaluate("summary=3 OPEN", State()));
    }

    // —— 缺省缺省：声明解析不出 = 不满足 = 隐藏 ——

    [Theory]
    [InlineData("foo=bar")] // 未知字段
    [InlineData("severity~warning")] // 未知运算
    [InlineData("severity>=")] // 缺操作数
    [InlineData("=warning")] // 缺字段
    [InlineData("noword")] // 纯词不成条件
    [InlineData("!")] // 孤立取反
    [InlineData("!garbage")] // 取反不翻声明错误
    [InlineData("progress>abc")] // 操作数解析失败
    [InlineData("summary>warning")] // 非数值字段关系运算
    public void Evaluate_MalformedOrUnknown_Hides(string condition)
        => Assert.False(ComponentVisibility.Evaluate(condition, State()));

    [Fact]
    public void Evaluate_WhitespaceAroundTokens_IsTolerated()
    {
        Assert.True(ComponentVisibility.Evaluate(" severity >= warning ", State()));
        Assert.True(ComponentVisibility.Evaluate("has: payload:branch ",
            State(s => s with { Payload = new Dictionary<string, string> { ["branch"] = "main" } })));
    }

    [Fact]
    public void Evaluate_PayloadKeyContainingOperatorChar_SplitsAtFirstOperator()
    {
        // 含运算符形如 "rating>=3" 的键：在首个运算符截断（path=payload:rating, op=>=, value=3=value），
        // path 解析不到值且关系运算不可求值 → 隐藏；不让键名里的符号拼出半真条件
        var state = State(s => s with { Payload = new Dictionary<string, string> { ["rating>=3"] = "yes" } });

        Assert.False(ComponentVisibility.Evaluate("payload:rating>=3=yes", state));
    }
}
