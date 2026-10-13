using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// debug 注入面单测（todo 行 71）：BEACON_DEBUG_SEVERITY=critical 时把刷新成功的 state 改判
/// Critical/Failed（摘要/负载不动），其余值（含缺省/空串/不认识）一律原样返回——注入面默认零行为，
/// 生产路径不受影响。环境变量门控的「进程内缓存读取」半无法在共享进程的单测里隔离，故 OverrideSeverity
/// 的 value 参数纯函数化（显式传值），环境分支即同一条 string.Equals 路径。
/// </summary>
public sealed class DebugInjectionTests
{
    private static WidgetState State(Severity severity = Severity.Success, LifecycleState? lifecycle = null)
        => new()
        {
            WidgetId = "w1",
            WidgetType = "test.type",
            ConnectionId = "c1",
            Severity = severity,
            Summary = "已用 38%",
            Lifecycle = lifecycle,
        };

    [Fact]
    public void Critical_OverridesSeverityAndLifecycle_KeepsSummary()
    {
        var source = State(Severity.Success, LifecycleState.Success);

        var injected = DebugInjection.OverrideSeverity(source, "critical");

        Assert.Equal(Severity.Critical, injected.Severity);
        Assert.Equal(LifecycleState.Failed, injected.Lifecycle);
        Assert.Equal("已用 38%", injected.Summary); // 摘要不动：注入只改级别，数据面由 provider 负责
    }

    [Fact]
    public void Critical_MatchesCaseInsensitive()
    {
        Assert.Equal(Severity.Critical, DebugInjection.OverrideSeverity(State(), "CRITICAL").Severity);
        Assert.Equal(Severity.Critical, DebugInjection.OverrideSeverity(State(), "Critical").Severity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("error")]
    [InlineData("warn")]
    [InlineData("CRITICAL_X")]
    public void OtherValues_ReturnSameReference(string? value)
    {
        var source = State(Severity.Warning, LifecycleState.Running);

        var result = DebugInjection.OverrideSeverity(source, value);

        Assert.Same(source, result); // 原样=同一引用：不进 with，不产生新对象
    }
}
