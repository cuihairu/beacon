using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 3 行 3：Selector 到组件属性的绑定引擎——纯函数、无 IO、无 Provider 依赖；
/// 一切取不到值的绑定回空串（fail-closed），不抛、不猜。
/// </summary>
public sealed class ComponentBindingEngineTests
{
    private static WidgetState State(IReadOnlyDictionary<string, string>? payload = null, string summary = "s")
        => new()
        {
            WidgetId = "w-1",
            WidgetType = "github.pr.list",
            ConnectionId = "conn-1",
            Severity = Severity.Warning,
            Summary = summary,
            DetailUrl = "https://example.invalid/run/9",
            Lifecycle = LifecycleState.Running,
            Progress = 0.5,
            Payload = payload ?? new Dictionary<string, string>(),
            FetchedAt = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero),
        };

    private static ComponentBinding Binding(string field, string kind, string refKey,
        string? value = null, string sourceName = "")
        => new()
        {
            Field = field,
            Source = new ComponentBindingSource
            {
                Kind = kind,
                Ref = refKey,
                SourceName = sourceName,
                Args = value is null
                    ? null
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["value"] = value },
            },
        };

    private static ComponentDescriptor Node(string id, ComponentBinding binding,
        params ComponentDescriptor[] children)
        => new() { Id = id, Type = "text", Bindings = [binding], Children = [.. children] };

    [Theory]
    [InlineData("summary", "s")]
    [InlineData("severity", "Warning")]
    [InlineData("progress", "0.5")]
    [InlineData("detailUrl", "https://example.invalid/run/9")]
    [InlineData("lifecycle", "Running")]
    [InlineData("connectionId", "conn-1")]
    [InlineData("widgetId", "w-1")]
    [InlineData("widgetType", "github.pr.list")]
    public void ResolveBinding_StatePaths_Resolve(string path, string expected)
    {
        var value = ComponentBindingEngine.ResolveBinding(
            Binding("text", ComponentBindingSource.KindState, path), State());

        Assert.Equal(expected, value);
    }

    [Fact]
    public void ResolveBinding_NullableStateFields_ReturnEmptyWhenAbsent()
    {
        var state = State();
        state = state with { Progress = null, DetailUrl = null, Lifecycle = null };

        Assert.Equal("", ComponentBindingEngine.ResolveBinding(Binding("t", ComponentBindingSource.KindState, "progress"), state));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(Binding("t", ComponentBindingSource.KindState, "detailUrl"), state));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(Binding("t", ComponentBindingSource.KindState, "lifecycle"), state));
    }

    [Fact]
    public void ResolveBinding_PayloadPath_ReadsKeyAndFailsClosedOnMissing()
    {
        var withKey = State(new Dictionary<string, string> { ["run_id"] = "42" });

        Assert.Equal("42", ComponentBindingEngine.ResolveBinding(
            Binding("v", ComponentBindingSource.KindState, "payload:run_id"), withKey));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(
            Binding("v", ComponentBindingSource.KindState, "payload:run_id"), State()));
    }

    [Fact]
    public void ResolveBinding_UnknownStatePath_ReturnsEmpty()
    {
        var value = ComponentBindingEngine.ResolveBinding(
            Binding("t", ComponentBindingSource.KindState, "summary.bogus"), State());

        Assert.Equal("", value);
    }

    [Fact]
    public void ResolveBinding_ConstSource_ReadsValueArg()
    {
        Assert.Equal("7.2", ComponentBindingEngine.ResolveBinding(
            Binding("value", ComponentBindingSource.KindConst, "", value: "7.2"), State()));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(
            Binding("value", ComponentBindingSource.KindConst, ""), State()));
    }

    [Fact]
    public void ResolveBinding_SelectorSource_UsesRegistry()
    {
        var state = State(new Dictionary<string, string> { ["open_count"] = "7" });

        Assert.Equal("7 open", ComponentBindingEngine.ResolveBinding(
            Binding("v", ComponentBindingSource.KindSelector, "value-hint"), state));
    }

    [Fact]
    public void ResolveBinding_UnknownSelector_ReturnsEmpty()
    {
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(
            Binding("v", ComponentBindingSource.KindSelector, "no-such-selector"), State()));
    }

    [Fact]
    public void ResolveBinding_WithoutState_ReturnsEmpty()
    {
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(
            Binding("t", ComponentBindingSource.KindState, "summary"), null));
        Assert.Equal("7.2", ComponentBindingEngine.ResolveBinding(
            Binding("t", ComponentBindingSource.KindConst, "", value: "7.2"), null));
    }

    [Fact]
    public void Resolve_Tree_CollectsValuesByNodePath()
    {
        var instance = new WidgetInstance
        {
            Id = "w-1",
            Type = "github.pr.list",
            Surface = "detail",
            Config = new Dictionary<string, string>(),
            Root = Node("card", Binding("text", ComponentBindingSource.KindState, "summary"),
                Node("metric", Binding("value", ComponentBindingSource.KindState, "payload:run_id")),
                Node("unit", Binding("unit", ComponentBindingSource.KindConst, "", value: "open"))),
        };
        var state = State(new Dictionary<string, string> { ["run_id"] = "42" });

        var values = ComponentBindingEngine.Resolve(instance, state);

        // 键 = "{节点路径}.{字段}"，路径与校验诊断同口径（id 或 child{index}，/ 连接）
        Assert.Equal("s", values["card.text"]);
        Assert.Equal("42", values["card/metric.value"]);
        Assert.Equal("open", values["card/unit.unit"]);
        Assert.Equal(3, values.Count);
    }

    [Fact]
    public void Resolve_WithoutBindings_ReturnsEmptyMap()
    {
        var instance = new WidgetInstance
        {
            Id = "w-1",
            Type = "github.pr.list",
            Surface = "row",
            Config = new Dictionary<string, string>(),
            Root = new ComponentDescriptor { Type = "surface-card", Children = [new ComponentDescriptor { Type = "text" }] },
        };

        Assert.Empty(ComponentBindingEngine.Resolve(instance, State()));
    }

    [Fact]
    public void Resolve_SourceNameAddressing_ReadsMappedStateAndFailsClosedOnMissing()
    {
        var primary = State(summary: "primary");
        var secondary = State(summary: "secondary");
        var instance = new WidgetInstance
        {
            Id = "w-1",
            Type = "github.pr.list",
            Surface = "detail",
            Config = new Dictionary<string, string>(),
            Root = Node("card",
                Binding("text", ComponentBindingSource.KindState, "summary", sourceName: "runs"),
                new ComponentDescriptor { Id = "orphan", Type = "text",
                    Bindings = [Binding("text", ComponentBindingSource.KindState, "summary", sourceName: "missing")] }),
        };
        var sources = new Dictionary<string, WidgetState>
        {
            [""] = primary,
            ["runs"] = secondary,
        };

        var values = ComponentBindingEngine.Resolve(instance, sources);

        Assert.Equal("secondary", values["card.text"]);
        Assert.Equal("", values["card/orphan.text"]);
        Assert.Equal("primary", ComponentBindingEngine.ResolveBinding(
            Binding("t", ComponentBindingSource.KindState, "summary"), primary, sources));
    }

    [Fact]
    public void Resolve_ReturnsIndependentMapPerCall()
    {
        var instance = new WidgetInstance
        {
            Id = "w-1",
            Type = "github.pr.list",
            Surface = "detail",
            Config = new Dictionary<string, string>(),
            Root = Node("card", Binding("text", ComponentBindingSource.KindState, "summary")),
        };

        var first = ComponentBindingEngine.Resolve(instance, State(summary: "a"));
        var second = ComponentBindingEngine.Resolve(instance, State(summary: "b"));

        Assert.Equal("a", first["card.text"]);
        Assert.Equal("b", second["card.text"]);
        Assert.NotSame(first, second);
    }
}
