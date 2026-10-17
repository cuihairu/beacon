using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;

namespace Beacon.Storage.Tests;

/// <summary>
/// Phase 4 HTML 载体面（2026-10-17 裁定）加载/写入防呆：UpsertWidget 与加载同一
/// fail-closed 口径——坏模板加载即报错并整面弃用（诊断带模板首错），不等首次渲染
/// 才发现；好模板原样透传（展开发生在解析器，不在存储层改写配置）。
/// </summary>
public sealed class HtmlTemplateSanitizeTests
{
    private static ComponentDescriptor Carrier(string html)
        => new()
        {
            Type = HtmlTemplateParser.CarrierType,
            Props = { ["html"] = html },
        };

    [Fact]
    public void Upsert_BadTemplate_DropsSurfaceWithTemplateDiagnostic()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);

        store.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Components = new Dictionary<string, ComponentDescriptor>
            {
                ["detail"] = Carrier("<card><text>{{state.bogus}}</text></card>"),
            },
        });

        var widget = Assert.Single(store.Widgets);
        Assert.Empty(widget.Components!); // 整面弃用回传统渲染
        Assert.Contains(store.LoadErrors, error => error.Contains("w-1/detail"));
        Assert.Contains(store.LoadErrors, error => error.Contains("unsupported state field"));
    }

    [Fact]
    public void Upsert_ScriptTemplate_RejectedByClosedVocabulary()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);

        store.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Components = new Dictionary<string, ComponentDescriptor>
            {
                ["detail"] = Carrier("<script>alert(1)</script>"),
            },
        });

        var widget = Assert.Single(store.Widgets);
        Assert.Empty(widget.Components!);
        Assert.Contains(store.LoadErrors, error => error.Contains("unknown element"));
    }

    [Fact]
    public void Upsert_GoodTemplate_PassesThroughUntouched()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        var carrier = Carrier("<card><text>ok</text></card>");

        store.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Components = new Dictionary<string, ComponentDescriptor> { ["detail"] = carrier },
        });

        var widget = Assert.Single(store.Widgets);
        var surface = Assert.Single(widget.Components!);
        Assert.Equal("detail", surface.Key);
        Assert.Same(carrier, surface.Value); // 透传不重建（展开在解析器）
        Assert.Empty(store.LoadErrors);
    }

    [Fact]
    public void SanitizedWrite_PersistsAcrossReload_NoResidualDiagnostics()
    {
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.UpsertWidget(new WidgetConfig
        {
            Id = "w-1",
            Type = "github.pr.list",
            ConnectionId = "c",
            Components = new Dictionary<string, ComponentDescriptor>
            {
                ["detail"] = Carrier("<img src=\"file:///c:/x.png\"/>"),
            },
        });

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();

        var widget = Assert.Single(second.Widgets);
        Assert.Empty(widget.Components!); // 落盘即净化，重载零诊断
        Assert.Contains(first.LoadErrors, error => error.Contains("w-1/detail"));
        Assert.Empty(second.LoadErrors);
    }
}
