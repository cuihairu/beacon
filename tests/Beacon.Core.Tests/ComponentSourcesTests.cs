using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 3 行 4：多数据源组合——面级源声明（源名 → widgetId）进 WidgetConfig.Sources；
/// 保留键 "" 恒指实例自身状态、源名大小写敏感精确匹配；运行时源映射按 widgetId 从状态
/// 快照查取（查不到 = 缺席，绑定侧 fail-closed 回空串）；非法源声明整面弃用（只剩自身
/// 状态）+ 诊断，口径同组件树。组合发生在组件层聚合多个 WidgetId，WidgetState 单
/// ConnectionId 的 Provider 口径不动。
/// </summary>
public sealed class ComponentSourcesTests
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

    private static Dictionary<string, Dictionary<string, string>> At(string surface, Dictionary<string, string> sources)
        => new() { [surface] = sources };

    private static WidgetConfig Config(Dictionary<string, Dictionary<string, string>>? sources = null,
        string surface = "detail")
        => new()
        {
            Id = "w-1",
            Type = "github.pr.list",
            Components = new Dictionary<string, ComponentDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                [surface] = new ComponentDescriptor { Id = "card", Type = "surface-card" },
            },
            Sources = sources,
        };

    private static ComponentDescriptor Ghost() => new()
    {
        Id = "ghost",
        Type = "text",
        Bindings =
        [
            new ComponentBinding
            {
                Field = "text",
                Source = new ComponentBindingSource
                {
                    Kind = ComponentBindingSource.KindState,
                    Ref = "summary",
                    SourceName = "missing",
                },
            },
        ],
    };

    private static WidgetInstance Instance(Dictionary<string, string>? sources = null,
        params ComponentDescriptor[] children)
        => new()
        {
            Id = "w-1",
            Type = "github.pr.list",
            Surface = "detail",
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
                            SourceName = "runs",
                        },
                    },
                ],
                Children = [.. children],
            },
            Sources = sources ?? new Dictionary<string, string>(),
        };

    private static Dictionary<string, WidgetState> Snapshot(params (string Id, string Summary)[] states)
    {
        var lookup = new Dictionary<string, WidgetState>();
        foreach (var (id, summary) in states)
        {
            lookup[id] = State(summary, id);
        }
        return lookup;
    }

    [Fact]
    public void Sources_RoundTripsThroughJson()
    {
        var config = Config(At("detail", new Dictionary<string, string>
        {
            ["runs"] = "w-2",
            ["quota"] = "w-3",
        }));

        var restored = BeaconJson.RoundTrip(config);

        Assert.Equal("w-2", restored.Sources!["detail"]["runs"]);
        Assert.Equal("w-3", restored.Sources!["detail"]["quota"]);
        Assert.Equal("surface-card", restored.Components!["detail"].Type);
    }

    [Fact]
    public void Config_WithoutSources_SerializesWithoutKey()
    {
        Assert.DoesNotContain("sources", BeaconJson.Serialize(Config()));
    }

    [Fact]
    public void OldConfigWithoutSources_ParsesToNull()
    {
        var restored = BeaconJson.Deserialize<WidgetConfig>("""{"id":"w-old","type":"github.pr.list"}""");

        Assert.Null(restored.Sources);
        Assert.Null(restored.Components);
    }

    [Fact]
    public void Validate_NullMap_IsValid()
    {
        Assert.True(ComponentSourcesValidator.Validate(null).Ok);
    }

    [Fact]
    public void Validate_ValidMap_IsValid()
    {
        var result = ComponentSourcesValidator.Validate(
            new Dictionary<string, string> { ["runs"] = "w-2", ["quota"] = "w-3" });

        Assert.True(result.Ok);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptySourceName_IsInvalid(string name)
    {
        var result = ComponentSourcesValidator.Validate(new Dictionary<string, string> { [name] = "w-2" });

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("name"));
    }

    [Fact]
    public void Validate_ReservedOwnName_IsInvalid()
    {
        var result = ComponentSourcesValidator.Validate(
            new Dictionary<string, string> { [ComponentBindingSource.OwnStateKey] = "w-2" });

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("reserved"));
    }

    [Fact]
    public void Validate_EmptyWidgetId_IsInvalid()
    {
        var result = ComponentSourcesValidator.Validate(new Dictionary<string, string> { ["runs"] = "  " });

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("widget id"));
    }

    [Fact]
    public void Validate_TooManySources_IsInvalid()
    {
        var sources = new Dictionary<string, string>();
        for (var i = 0; i <= ComponentSourcesValidator.MaxSourcesPerSurface; i++)
        {
            sources[$"s{i}"] = $"w-{i}";
        }

        var result = ComponentSourcesValidator.Validate(sources);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("exceeds max"));
    }

    [Fact]
    public void Resolve_ValidSources_MappedIntoInstance()
    {
        var instance = WidgetInstanceResolver.Resolve(Config(At("detail", Surface("w-2"))), "detail");

        Assert.NotNull(instance);
        Assert.Equal("w-2", instance!.Sources["runs"]);
    }

    [Fact]
    public void Resolve_InvalidSources_FallsBackToOwnStateOnly()
    {
        var instance = WidgetInstanceResolver.Resolve(Config(
            At("detail", new Dictionary<string, string> { [""] = "w-2" })), "detail");

        // 附加源声明非法：整面弃用附加源（自身状态恒可用），实例不因此弃用
        Assert.NotNull(instance);
        Assert.Empty(instance!.Sources);
    }

    [Fact]
    public void Resolve_OtherSurfaceSources_NotMapped()
    {
        var instance = WidgetInstanceResolver.Resolve(Config(At("tile", Surface("w-2"))), "detail");

        Assert.NotNull(instance);
        Assert.Empty(instance!.Sources);
    }

    [Fact]
    public void ResolveSources_OwnStateAndAdditionalSourcesFromSnapshot()
    {
        var instance = Instance(Surface("w-2"));
        var lookup = Snapshot(("w-1", "own"), ("w-2", "runs"));
        var seen = new List<string>();

        var sources = WidgetInstanceResolver.ResolveSources(instance, id =>
        {
            seen.Add(id);
            return lookup.TryGetValue(id, out var state) ? state : null;
        });

        Assert.Equal("own", sources[ComponentBindingSource.OwnStateKey].Summary);
        Assert.Equal("runs", sources["runs"].Summary);
        Assert.Equal(2, sources.Count);
        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public void ResolveSources_MissingStatesAbsentFromMap()
    {
        var instance = Instance(Surface("w-2"));

        var sources = WidgetInstanceResolver.ResolveSources(instance, _ => null);

        Assert.Empty(sources);
    }

    [Fact]
    public void Resolve_EndToEnd_AdditionalSourceReadsOtherWidgetState()
    {
        var instance = Instance(Surface("w-2"), Ghost());
        var lookup = Snapshot(("w-1", "own"), ("w-2", "runs"));

        var values = ComponentBindingEngine.Resolve(instance,
            WidgetInstanceResolver.ResolveSources(instance, id => lookup.TryGetValue(id, out var state) ? state : null));

        Assert.Equal("runs", values["card.text"]);
        Assert.Equal("", values["card/ghost.text"]);
        Assert.Equal(2, values.Count);
    }

    [Fact]
    public void Resolve_SourceName_MatchesCaseSensitive()
    {
        var instance = new WidgetInstance
        {
            Id = "w-1",
            Type = "github.pr.list",
            Surface = "detail",
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
                            SourceName = "Runs",
                        },
                    },
                ],
            },
            Sources = Surface("w-2"),
        };

        var values = ComponentBindingEngine.Resolve(instance,
            WidgetInstanceResolver.ResolveSources(instance, id =>
                id == "w-2" ? State("runs", "w-2") : null));

        // 源名大小写敏感精确匹配："Runs" 不等于声明的 "runs" → 缺值回空串
        Assert.Equal("", values["card.text"]);
    }
}
