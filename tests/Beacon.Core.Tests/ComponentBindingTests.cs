using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 3 行 2：Widget Instance 与 Binding 配置（契约.md §4 已代决⑥）——
/// 绑定声明随节点进树；路径寻址只认受支持的状态字段与 payload 键；
/// 实例解析 fail-closed（无树/非法树 → null，调用面回传统渲染）。
/// </summary>
public sealed class ComponentBindingTests
{
    private static ComponentBindingSource Source(string kind, string refKey, string? value = null)
        => new()
        {
            Kind = kind,
            Ref = refKey,
            Args = value is null
                ? null
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["value"] = value },
        };

    private static ComponentDescriptor Node(ComponentBinding binding)
        => new()
        {
            Type = "text",
            Bindings = [binding],
        };

    private static WidgetConfig Widget(string id = "w-1")
    {
        var config = new WidgetConfig { Id = id, Type = "github.pr.list" };
        return config;
    }

    [Fact]
    public void Binding_RoundTripsThroughJson()
    {
        var tree = new ComponentDescriptor
        {
            Id = "head",
            Type = "surface-card",
            Bindings =
            [
                new ComponentBinding { Field = "text", Source = Source(ComponentBindingSource.KindState, "summary") },
            ],
            Children =
            [
                new ComponentDescriptor
                {
                    Id = "metric",
                    Type = "metric",
                    Bindings =
                    [
                        new ComponentBinding
                        {
                            Field = "value",
                            Source = Source(ComponentBindingSource.KindConst, "", "7.2"),
                        },
                    ],
                },
            ],
        };

        var restored = BeaconJson.RoundTrip(tree);

        Assert.Equal("head", restored.Id);
        var binding = Assert.Single(restored.Bindings);
        Assert.Equal("text", binding.Field);
        Assert.Equal(ComponentBindingSource.KindState, binding.Source.Kind);
        Assert.Equal("summary", binding.Source.Ref);
        Assert.Null(restored.Bindings[0].Source.Args);
        var child = Assert.Single(restored.Children);
        var constBinding = Assert.Single(child.Bindings);
        Assert.Equal(ComponentBindingSource.KindConst, constBinding.Source.Kind);
        Assert.Equal("7.2", constBinding.Source.Args!["value"]);
    }

    [Theory]
    [InlineData("state", "summary")]
    [InlineData("state", "severity")]
    [InlineData("state", "progress")]
    [InlineData("state", "detailUrl")]
    [InlineData("state", "lifecycle")]
    [InlineData("state", "payload:run_id")]
    [InlineData("const", "")]
    [InlineData("selector", "value-hint")]
    public void Validate_KnownBindingShapes_AreValid(string kind, string refKey)
    {
        var tree = Node(new ComponentBinding { Field = "text", Source = Source(kind, refKey) });

        Assert.True(ComponentTreeValidator.Validate(tree).Ok);
    }

    [Fact]
    public void Validate_EmptyField_IsInvalid()
    {
        var tree = Node(new ComponentBinding { Field = "  ", Source = Source(ComponentBindingSource.KindState, "summary") });

        var result = ComponentTreeValidator.Validate(tree);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("field is empty"));
    }

    [Fact]
    public void Validate_UnknownKind_IsInvalid()
    {
        var tree = Node(new ComponentBinding { Field = "text", Source = Source("template", "summary") });

        var result = ComponentTreeValidator.Validate(tree);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("kind") && error.Contains("template"));
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("summary.detailUrl")]
    [InlineData("payload:")]
    [InlineData("payload:a:b")]
    public void Validate_UnsupportedStatePath_IsInvalid(string refKey)
    {
        var tree = Node(new ComponentBinding { Field = "text", Source = Source(ComponentBindingSource.KindState, refKey) });

        var result = ComponentTreeValidator.Validate(tree);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("unsupported state path"));
    }

    [Fact]
    public void Validate_BindingErrorCarriesNodePath()
    {
        var tree = new ComponentDescriptor
        {
            Id = "card",
            Type = "surface-card",
            Children =
            [
                new ComponentDescriptor
                {
                    Id = "text",
                    Type = "text",
                    Bindings = [new ComponentBinding { Field = "text", Source = Source(ComponentBindingSource.KindState, "bogus") }],
                },
            ],
        };

        var result = ComponentTreeValidator.Validate(tree);

        Assert.Contains(result.Errors, error => error.Contains("card/text"));
    }

    [Fact]
    public void Resolve_KnownSurface_ReturnsInstanceWithTreeAndBindings()
    {
        var config = new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            Components = new Dictionary<string, ComponentDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                ["detail"] = new ComponentDescriptor
                {
                    Id = "card",
                    Type = "surface-card",
                    Bindings = [new ComponentBinding { Field = "text", Source = Source(ComponentBindingSource.KindState, "summary") }],
                    Children = [new ComponentDescriptor { Type = "metric" }],
                },
            },
        };

        var instance = WidgetInstanceResolver.Resolve(config, "detail");

        Assert.NotNull(instance);
        Assert.Equal("w-1", instance!.Id);
        Assert.Equal("github.pr.list", instance.Type);
        Assert.Equal("detail", instance.Surface);
        Assert.Equal("surface-card", instance.Root.Type);
        Assert.Single(instance.Root.Children);
        Assert.Single(instance.Root.Bindings);
    }

    [Fact]
    public void Resolve_MissingSurface_ReturnsNullForLegacyRendering()
    {
        Assert.Null(WidgetInstanceResolver.Resolve(Widget(), "detail"));
    }

    [Fact]
    public void Resolve_EmptyComponentsMap_ReturnsNull()
    {
        var config = new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            Components = new Dictionary<string, ComponentDescriptor>(StringComparer.OrdinalIgnoreCase),
        };

        Assert.Null(WidgetInstanceResolver.Resolve(config, "tile"));
    }

    [Fact]
    public void Resolve_InvalidTree_ReturnsNullFailClosed()
    {
        var config = new WidgetConfig
        {
            Id = "w-bad",
            Type = "github.pr.list",
            Components = new Dictionary<string, ComponentDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                ["row"] = new ComponentDescriptor { Type = "list" },
            },
        };
        for (var depth = 0; depth < ComponentTreeValidator.MaxDepth + 1; depth++)
        {
            config.Components!["row"] = new ComponentDescriptor
            {
                Type = "list",
                Children = [config.Components["row"]!],
            };
        }

        Assert.Null(WidgetInstanceResolver.Resolve(config, "row"));
    }

    [Fact]
    public void Resolve_SameConfigTwice_ReturnsIndependentInstancesSharingTree()
    {
        var config = new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            Components = new Dictionary<string, ComponentDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                ["detail"] = new ComponentDescriptor
                {
                    Type = "surface-card",
                    Bindings = [new ComponentBinding { Field = "text", Source = Source(ComponentBindingSource.KindState, "summary") }],
                },
            },
        };

        var first = WidgetInstanceResolver.Resolve(config, "detail");
        var second = WidgetInstanceResolver.Resolve(config, "detail");

        // 实例独立；树是配置的一部分、按约定只读共享（与 Config/Parameters 字典同口径）
        Assert.NotSame(first, second);
        Assert.Same(first!.Root, second!.Root);
        Assert.Equal("summary", first.Root.Bindings[0].Source.Ref);
    }
}
