using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 3 行 5：不同组件消费同一份状态——解析是纯函数、状态按引用共享不复制、
/// 同一份状态可同时供多个实例/多个渲染面消费且互不影响；消费索引反查
/// 「这份状态被哪些组件消费」，供数据变化只更新必要消费者（行 6 的前置）。
/// </summary>
public sealed class ComponentSourceIndexTests
{
    private static WidgetState State(string summary, string widgetId = "w-1")
        => new()
        {
            WidgetId = widgetId,
            WidgetType = "github.pr.list",
            ConnectionId = "conn-1",
            Severity = Severity.Warning,
            Summary = summary,
            FetchedAt = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero),
        };

    private static Dictionary<string, string> Surface(string widgetId)
        => new() { ["runs"] = widgetId };

    private static Func<string, WidgetState?> Lookup(IDictionary<string, WidgetState> snapshot)
        => widgetId => snapshot.TryGetValue(widgetId, out var state) ? state : null;

    private static WidgetInstance Instance(string surface, Dictionary<string, string>? sources = null,
        string sourceName = "", string id = "w-1")
        => new()
        {
            Id = id,
            Type = "github.pr.list",
            Surface = surface,
            Config = new Dictionary<string, string>(),
            Root = new ComponentDescriptor
            {
                Id = "card",
                Type = "surface-card",
                Bindings =
                [
                    new ComponentBinding
                    {
                        Field = "text",
                        Source = new ComponentBindingSource
                        {
                            Kind = ComponentBindingSource.KindState,
                            Ref = "summary",
                            SourceName = sourceName,
                        },
                    },
                ],
            },
            Sources = sources ?? new Dictionary<string, string>(),
        };

    [Fact]
    public void ConsumedWidgetIds_OwnAndAdditionalSources_NoDuplicates()
    {
        var instance = Instance("detail", new Dictionary<string, string>
        {
            ["runs"] = "w-2",
            ["quota"] = "w-2",
        });

        var ids = ComponentSourceIndex.ConsumedWidgetIds(instance);

        // 两个源名指向同一 widgetId：只登记一次，自身 Id 在前
        Assert.Equal(2, ids.Count);
        Assert.Equal("w-1", ids[0]);
        Assert.Equal("w-2", ids[1]);
    }

    [Fact]
    public void ConsumedWidgetIds_OwnOnly_WithoutAdditionalSources()
    {
        var ids = ComponentSourceIndex.ConsumedWidgetIds(Instance("tile"));

        Assert.Single(ids);
        Assert.Equal("w-1", ids[0]);
    }

    [Fact]
    public void SourcesOf_IndexesEveryConsumerByWidgetId()
    {
        var tile = Instance("tile", Surface("w-2"), id: "w-1");
        var row = Instance("row", Surface("w-2"), id: "w-3");
        var isolated = Instance("detail", id: "w-4");

        var index = ComponentSourceIndex.SourcesOf([tile, row, isolated]);

        Assert.Equal(2, index["w-2"].Count);
        Assert.Contains(tile, index["w-2"]);
        Assert.Contains(row, index["w-2"]);
        Assert.Single(index["w-1"]);
        Assert.Contains(tile, index["w-1"]);
        Assert.Single(index["w-4"]);
        Assert.Contains(isolated, index["w-4"]);
        Assert.False(index.ContainsKey("w-9"));
    }

    [Fact]
    public void SourcesOf_OneInstanceConsumingTwoStates_AppearsUnderBoth()
    {
        var instance = Instance("detail", new Dictionary<string, string>
        {
            ["runs"] = "w-2",
            ["quota"] = "w-3",
        });

        var index = ComponentSourceIndex.SourcesOf([instance]);

        Assert.Single(index["w-1"]);
        Assert.Contains(instance, index["w-1"]);
        Assert.Single(index["w-2"]);
        Assert.Contains(instance, index["w-2"]);
        Assert.Single(index["w-3"]);
        Assert.Contains(instance, index["w-3"]);
    }

    [Fact]
    public void Resolve_SameStateAcrossInstances_IsIndependent()
    {
        var shared = State("s", "w-1");

        var tileValues = ComponentBindingEngine.Resolve(Instance("tile"), shared);
        var rowValues = ComponentBindingEngine.Resolve(Instance("row"), shared);

        Assert.Equal("s", tileValues["card.text"]);
        Assert.Equal("s", rowValues["card.text"]);
        Assert.NotSame(tileValues, rowValues);
    }

    [Fact]
    public void Resolve_SharedStateIsNotMutated()
    {
        var before = State("s", "w-1"); // 同值不同对象，作为解析前的内容基准
        var shared = State("s", "w-1");

        var values = ComponentBindingEngine.Resolve(Instance("detail"), shared);

        Assert.Equal("s", values["card.text"]);
        Assert.True(before.Equals(shared)); // 内容相等：解析只读，不改状态
    }

    [Fact]
    public void ResolveSources_SharedWidgetStateIsNotCopied()
    {
        var tile = Instance("tile", Surface("w-2"), id: "w-1");
        var row = Instance("row", Surface("w-2"), id: "w-3");
        var shared = State("runs", "w-2");
        var lookup = new Dictionary<string, WidgetState> { ["w-2"] = shared };
        var stateOf = Lookup(lookup);

        var tileSources = WidgetInstanceResolver.ResolveSources(tile, stateOf);
        var rowSources = WidgetInstanceResolver.ResolveSources(row, stateOf);

        // 一份状态供不同组件消费：按引用共享，不克隆
        Assert.Same(shared, tileSources["runs"]);
        Assert.Same(shared, rowSources["runs"]);
    }

    [Fact]
    public void Resolve_AllSurfacesOfOneWidget_ConsumeTheSameState()
    {
        var shared = State("s", "w-1");
        string[] surfaces = ["tile", "row", "detail"];
        var instances = surfaces.Select(surface => Instance(surface, id: "w-1")).ToList();

        var maps = instances.ToDictionary(instance => instance.Surface,
            instance => ComponentBindingEngine.Resolve(instance, shared));

        Assert.Equal(3, maps.Count);
        foreach (var values in maps.Values)
        {
            Assert.Equal("s", values["card.text"]);
        }
        Assert.NotSame(maps["tile"], maps["row"]);
        Assert.NotSame(maps["row"], maps["detail"]);
    }
}
