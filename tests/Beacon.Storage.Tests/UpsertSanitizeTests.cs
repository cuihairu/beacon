using Beacon.Core.Models;
using Beacon.Storage;

namespace Beacon.Storage.Tests;

/// <summary>
/// Phase 5 行 3（组件实例编辑、尺寸调整与桌面布局配置）写入面防呆：UpsertWidget 与加载
/// 同一 fail-closed 口径——结构非法的组件树整树弃用、非法附加源整面弃用、诊断进 LoadErrors。
/// 此前只加载时设防：编辑器/手改经 UpsertWidget 落盘的坏树要到下次重启才被清。
/// </summary>
public sealed class UpsertSanitizeTests
{
    /// <summary>超深树（上限 8 层）。</summary>
    private static ComponentDescriptor DeepTree(int levels)
    {
        var node = new ComponentDescriptor { Type = "text" };
        for (var level = 0; level < levels; level++)
        {
            node = new ComponentDescriptor { Type = "list", Children = [node] };
        }
        return node;
    }

    [Fact]
    public void Upsert_InvalidTree_DropsSurfaceAndDiagnoses()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);

        store.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Components = new Dictionary<string, ComponentDescriptor> { ["detail"] = DeepTree(20) },
        });

        var widget = Assert.Single(store.Widgets);
        Assert.Empty(widget.Components!); // 整树弃用回传统渲染，不静默裁成半棵
        Assert.Contains(store.LoadErrors, error => error.Contains("w-1/detail"));
    }

    [Fact]
    public void Upsert_InvalidSources_DropsSurfaceAndDiagnoses()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);

        store.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Sources = new Dictionary<string, Dictionary<string, string>>
            {
                ["detail"] = new() { [""] = "w-2" }, // 空源名 = 保留键，非法
            },
        });

        var widget = Assert.Single(store.Widgets);
        Assert.False(widget.Sources!.ContainsKey("detail")); // 声明非法的面整面弃用附加源（面整个移除）
        Assert.Contains(store.LoadErrors, error => error.Contains("w-1/detail"));
    }

    [Fact]
    public void Upsert_ValidSurfaces_PassThroughUntouched()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        var tree = DeepTree(3);

        store.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Components = new Dictionary<string, ComponentDescriptor> { ["detail"] = tree },
            Sources = new Dictionary<string, Dictionary<string, string>>
            {
                ["runs"] = new() { ["repo"] = "w-2" },
            },
        });

        var widget = Assert.Single(store.Widgets);
        var surface = Assert.Single(widget.Components!);
        Assert.Equal("detail", surface.Key);
        Assert.Same(tree, surface.Value); // 合法面原样透传，不重建不裁剪
        Assert.Single(widget.Sources!);
        Assert.Empty(store.LoadErrors);
    }

    [Fact]
    public void Upsert_SanitizedWrite_PersistsAcrossReload()
    {
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Components = new Dictionary<string, ComponentDescriptor> { ["detail"] = DeepTree(20) },
        });

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();

        var widget = Assert.Single(second.Widgets);
        Assert.Empty(widget.Components!); // 落盘的就是净化后的（不是等重启再清）
        Assert.Contains(first.LoadErrors, error => error.Contains("w-1/detail")); // 诊断在写入侧
        Assert.Empty(second.LoadErrors); // 重载读到的是已净化文件，无新诊断
    }
}
