using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// ComponentBindingEngine 单条解析边界收口（Batch 5）：空字段回空串、const 取参缺参、
/// percent 选择器整百分位与无进度 fail-closed、quota-windows 多窗口选择器、
/// 受支持字段/选择器白名单表。均走 public 纯函数，无 IO。
/// </summary>
public sealed class BindingEngineFieldEdgeTests
{
    private static WidgetState State(double? progress = null, IReadOnlyDictionary<string, string>? payload = null) => new()
    {
        WidgetId = "w-1",
        WidgetType = "test.type",
        ConnectionId = "conn-1",
        Severity = Severity.Success,
        Summary = "OK",
        Progress = progress,
        Payload = payload ?? new Dictionary<string, string>(),
    };

    [Fact]
    public void ResolveBinding_EmptyOrWhitespaceField_ReturnsEmpty()
    {
        var state = State();
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(new ComponentBinding { Field = "   " }, state));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(new ComponentBinding { Field = "" }, state));
    }

    [Fact]
    public void ResolveBinding_ConstSource_WithValue_ReturnsValue()
    {
        var binding = new ComponentBinding
        {
            Field = "tone",
            Source = new ComponentBindingSource { Kind = "const", Args = new Dictionary<string, string> { ["value"] = "good" } },
        };
        Assert.Equal("good", ComponentBindingEngine.ResolveBinding(binding, State()));
    }

    [Fact]
    public void ResolveBinding_ConstSource_WithoutValue_ReturnsEmpty()
    {
        var binding = new ComponentBinding
        {
            Field = "tone",
            Source = new ComponentBindingSource { Kind = "const", Args = new Dictionary<string, string>() },
        };
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(binding, State()));
    }

    [Fact]
    public void ResolveBinding_PercentSelector_RoundsToWholePercent()
    {
        var binding = new ComponentBinding
        {
            Field = "text",
            Source = new ComponentBindingSource { Kind = "selector", Ref = "percent" },
        };
        Assert.Equal("26%", ComponentBindingEngine.ResolveBinding(binding, State(progress: 0.256)));
        Assert.Equal("100%", ComponentBindingEngine.ResolveBinding(binding, State(progress: 1.0)));
    }

    [Fact]
    public void ResolveBinding_PercentSelector_NoProgress_FailsClosedToEmpty()
    {
        var binding = new ComponentBinding
        {
            Field = "text",
            Source = new ComponentBindingSource { Kind = "selector", Ref = "percent" },
        };
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(binding, State(progress: null)));
    }

    [Fact]
    public void ResolveBinding_QuotaWindowsSelector_JoinsMultiWindow()
    {
        var binding = new ComponentBinding
        {
            Field = "text",
            Source = new ComponentBindingSource { Kind = "selector", Ref = "quota-windows" },
        };
        var state = State(payload: new Dictionary<string, string>
        {
            ["rolling_percent"] = "5",
            ["weekly_percent"] = "7",
            ["monthly_percent"] = "8",
        });
        Assert.Equal("5%/5h 7%/w 8%/M", ComponentBindingEngine.ResolveBinding(binding, state));
    }

    [Fact]
    public void ResolveBinding_QuotaWindowsSelector_SingleWindow_FallsThroughEmpty()
    {
        var binding = new ComponentBinding
        {
            Field = "text",
            Source = new ComponentBindingSource { Kind = "selector", Ref = "quota-windows" },
        };
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(binding, State(payload: new Dictionary<string, string> { ["rolling_percent"] = "5" })));
    }

    [Fact]
    public void SupportedNames_ContainExpectedEntries()
    {
        Assert.Contains("summary", ComponentBindingEngine.StateFieldNames);
        Assert.Contains("detailUrl", ComponentBindingEngine.StateFieldNames);
        Assert.Contains("percent", ComponentBindingEngine.SelectorNames);
        Assert.Contains("quota-windows", ComponentBindingEngine.SelectorNames);
        Assert.Contains("value-hint", ComponentBindingEngine.SelectorNames);
    }
}
