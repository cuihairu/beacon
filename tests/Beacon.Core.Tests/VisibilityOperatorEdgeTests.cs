using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// ComponentVisibility 关系运算符全臂收口（Batch 5）：severity 走 (int,int,op) 重载、
/// progress 走 (int,op) 重载，两重载的 >=/&lt;=/!=/&gt;/&lt;/= 六臂都要真实求值过一遍，
/// 钉住「<=」「!=」「= 默认臂」这些此前未触达的分支（含枚举序与不变文化双精度口径）。
/// </summary>
public sealed class VisibilityOperatorEdgeTests
{
    // ---------- severity：枚举序比较（(int,int,op) 重载） ----------

    [Theory]
    [InlineData("severity >= warning", Severity.Critical, true)]
    [InlineData("severity >= warning", Severity.Info, false)]
    [InlineData("severity <= warning", Severity.Warning, true)]
    [InlineData("severity <= warning", Severity.Error, false)]
    [InlineData("severity != warning", Severity.Error, true)]
    [InlineData("severity != warning", Severity.Warning, false)]
    [InlineData("severity > warning", Severity.Error, true)]
    [InlineData("severity > warning", Severity.Warning, false)]
    [InlineData("severity < warning", Severity.Info, true)]
    [InlineData("severity < warning", Severity.Warning, false)]
    [InlineData("severity = warning", Severity.Warning, true)]
    [InlineData("severity = warning", Severity.Info, false)]
    public void Severity_AllOperators_FollowEnumOrder(string condition, Severity state, bool expected)
    {
        var visible = ComponentVisibility.Evaluate(condition, State(severity: state));
        Assert.Equal(expected, visible);
    }

    // ---------- progress：不变文化双精度比较（(int,op) 重载） ----------

    [Theory]
    [InlineData("progress >= 0.5", 0.6, true)]
    [InlineData("progress >= 0.5", 0.4, false)]
    [InlineData("progress <= 0.5", 0.5, true)]
    [InlineData("progress <= 0.5", 0.8, false)]
    [InlineData("progress != 0.5", 0.4, true)]
    [InlineData("progress != 0.5", 0.5, false)]
    [InlineData("progress > 0.5", 0.6, true)]
    [InlineData("progress > 0.5", 0.5, false)]
    [InlineData("progress < 0.5", 0.4, true)]
    [InlineData("progress < 0.5", 0.5, false)]
    [InlineData("progress = 0.5", 0.5, true)]
    [InlineData("progress = 0.5", 0.6, false)]
    public void Progress_AllOperators_CompareOrdinal(string condition, double progress, bool expected)
    {
        var visible = ComponentVisibility.Evaluate(condition, State(progress: progress));
        Assert.Equal(expected, visible);
    }

    // ---------- 非数值字段：= / != 忽略大小写，关系运算恒 false ----------

    [Fact]
    public void TextField_Equality_IsCaseInsensitive()
    {
        var state = State() with { Payload = new Dictionary<string, string> { ["run_id"] = "42" } };
        Assert.True(ComponentVisibility.Evaluate("payload:run_id = 42", state));
        Assert.True(ComponentVisibility.Evaluate("payload:run_id = 42", state));
        Assert.True(ComponentVisibility.Evaluate("payload:run_id != 7", state));
    }

    [Fact]
    public void TextField_RelationalOperator_IsNeverSatisfied()
    {
        var state = State() with { Payload = new Dictionary<string, string> { ["run_id"] = "42" } };
        Assert.False(ComponentVisibility.Evaluate("payload:run_id > 10", state));
        Assert.False(ComponentVisibility.Evaluate("summary > a", state));
    }

    private static WidgetState State(
        Severity severity = Severity.Success,
        double? progress = null)
        => new()
        {
            WidgetId = "w-1",
            WidgetType = "test.type",
            ConnectionId = "conn-1",
            Severity = severity,
            Summary = "OK",
            Progress = progress,
        };
}
