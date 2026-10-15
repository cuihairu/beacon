using Beacon.Core.Json;
using Beacon.Core.Models;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 3 行 1：组件描述符契约（契约.md §3.2 增量可选字段）——嵌套上限、结构防呆、旧文件兼容。
/// 结构非法 = 整树弃用回传统渲染（常驻进程不因配置坏炸），错误信息进诊断而非静默裁剪。
/// </summary>
public sealed class ComponentDescriptorTests
{
    private static ComponentDescriptor Node(string type, params ComponentDescriptor[] children)
        => new() { Type = type, Children = [.. children] };

    /// <summary>纵深链：root → child → … 共 levels 层（root 计第 1 层）。</summary>
    private static ComponentDescriptor Chain(int levels, string type = "node")
    {
        var deepest = new ComponentDescriptor { Type = type };
        for (var level = levels - 1; level >= 1; level--)
        {
            deepest = new ComponentDescriptor { Type = type, Children = [deepest] };
        }
        return deepest;
    }

    [Fact]
    public void RoundTrip_PreservesNestedTreeShape()
    {
        var tree = new ComponentDescriptor
        {
            Id = "root",
            Type = "surface-card",
            Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["padding"] = "10" },
            Children =
            [
                Node("status-indicator", new ComponentDescriptor { Type = "text", Props = new() { ["text"] = "ok" } }),
                Node("metric"),
            ],
        };

        var restored = BeaconJson.RoundTrip(tree);

        Assert.Equal("root", restored.Id);
        Assert.Equal("surface-card", restored.Type);
        Assert.Equal("10", restored.Props["padding"]);
        Assert.Equal(2, restored.Children.Count);
        Assert.Equal("status-indicator", restored.Children[0].Type);
        Assert.Single(restored.Children[0].Children);
        Assert.Equal("text", restored.Children[0].Children[0].Type);
        Assert.Equal("ok", restored.Children[0].Children[0].Props["text"]);
    }

    [Fact]
    public void WidgetConfig_WithoutComponents_SerializesWithoutKey()
    {
        var config = new WidgetConfig { Id = "w-1", Type = "github.pr.list" };

        var json = BeaconJson.Serialize(config);

        Assert.DoesNotContain("components", json); // 兼容面：无树组件不落该键，存量文件零噪声
    }

    [Fact]
    public void WidgetConfig_OldJsonWithoutComponents_ParsesToNull()
    {
        var restored = BeaconJson.Deserialize<WidgetConfig>("{ \"id\": \"w-1\", \"type\": \"github.pr.list\", \"config\": { \"repo\": \"x\" } }");

        Assert.Null(restored.Components);
        Assert.Equal("x", restored.Config["repo"]);
    }

    [Fact]
    public void Validate_NullRoot_IsValid()
    {
        var result = ComponentTreeValidator.Validate(null);

        Assert.True(result.Ok);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_SimpleTree_IsValid()
    {
        var tree = Node("status-card",
            Node("status-indicator"),
            Node("text"),
            Node("key-value"));

        Assert.True(ComponentTreeValidator.Validate(tree).Ok);
    }

    [Fact]
    public void Validate_EmptyType_IsInvalid()
    {
        var result = ComponentTreeValidator.Validate(Node("   "));

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("type"));
    }

    [Fact]
    public void Validate_DepthAtMax_IsValid_BeyondMax_IsInvalid()
    {
        Assert.True(ComponentTreeValidator.Validate(Chain(ComponentTreeValidator.MaxDepth)).Ok);
        var result = ComponentTreeValidator.Validate(Chain(ComponentTreeValidator.MaxDepth + 1));

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("depth"));
    }

    [Fact]
    public void Validate_ChildrenAtMax_IsValid_BeyondMax_IsInvalid()
    {
        var atMax = Node("list",
            Enumerable.Range(0, ComponentTreeValidator.MaxChildrenPerNode).Select(_ => Node("row")).ToArray());
        Assert.True(ComponentTreeValidator.Validate(atMax).Ok);

        var beyond = Node("list",
            Enumerable.Range(0, ComponentTreeValidator.MaxChildrenPerNode + 1).Select(_ => Node("row")).ToArray());
        var result = ComponentTreeValidator.Validate(beyond);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("children"));
    }

    [Fact]
    public void Validate_TotalNodesAtMax_IsValid_BeyondMax_IsInvalid()
    {
        // 15 支 × 16 行 + 15 组 + root = 256（children/depth 上限同时满足，单独考总节点上限）
        Assert.True(ComponentTreeValidator.Validate(Grid(15, 16)).Ok);
        var result = ComponentTreeValidator.Validate(Grid(15, 17));

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("nodes"));
    }

    /// <summary>分支 × 每支行数 的规整树（总节点 = 1 + 支 + 支×行）。</summary>
    private static ComponentDescriptor Grid(int branches, int rowsPerBranch)
        => Node("list",
            Enumerable.Range(0, branches)
                .Select(_ => Node("group",
                    Enumerable.Range(0, rowsPerBranch).Select(_ => Node("row")).ToArray()))
                .ToArray());

    [Fact]
    public void Validate_DuplicateNonEmptyIds_IsInvalid()
    {
        var tree = Node("list",
            new ComponentDescriptor { Id = "same", Type = "row" },
            new ComponentDescriptor { Id = "same", Type = "row" });

        var result = ComponentTreeValidator.Validate(tree);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("duplicate id") && error.Contains("same"));
    }

    [Fact]
    public void Validate_EmptyIds_MultipleAllowed()
    {
        var tree = Node("list", Node("row"), Node("row"));

        Assert.True(ComponentTreeValidator.Validate(tree).Ok);
    }

    [Fact]
    public void Validate_NullChild_IsInvalid()
    {
        var tree = new ComponentDescriptor
        {
            Type = "list",
            Children = [null!, Node("row")],
        };

        var result = ComponentTreeValidator.Validate(tree);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("null child"));
    }

    [Fact]
    public void Validate_ReportsNodePathForDiagnosability()
    {
        var tree = Node("list",
            new ComponentDescriptor
            {
                Id = "first",
                Type = "list",
                Children = [new ComponentDescriptor { Id = "second", Type = "" }],
            });

        var result = ComponentTreeValidator.Validate(tree);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("first/second"));
    }
}
