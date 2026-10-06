using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage.Tests;

/// <summary>B-201 验收：四文件往返；写入中断不损坏现役配置（.tmp 原子替换 + .bak 回退）。</summary>
public sealed class JsonConfigurationStoreTests
{
    private static ConnectionConfig Connection(string id, string endpoint) => new()
    {
        Id = id,
        Type = "github",
        Endpoint = endpoint,
        CredentialRef = "github:default", // 只存 ref，无明文
    };

    [Fact]
    public void Upsert_RoundTripsAcrossInstances()
    {
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.UpsertConnection(Connection("gh-main", "https://github.invalid"));
        first.UpsertWidget(new WidgetConfig { Id = "w-1", Type = "github.pr.list", ConnectionId = "gh-main" });

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();

        var connection = Assert.Single(second.Connections);
        Assert.Equal("gh-main", connection.Id);
        Assert.Equal("https://github.invalid", connection.Endpoint);
        var widget = Assert.Single(second.Widgets);
        Assert.Equal("w-1", widget.Id);
        Assert.Equal("github.pr.list", widget.Type);
        Assert.Equal("gh-main", widget.ConnectionId);
    }

    [Fact]
    public void UpsertSameId_ReplacesEntry()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.UpsertConnection(Connection("gh-main", "https://old.invalid"));
        store.UpsertConnection(Connection("gh-main", "https://new.invalid"));

        var connection = Assert.Single(store.Connections);
        Assert.Equal("https://new.invalid", connection.Endpoint);
    }

    [Fact]
    public void RemoveConnection_PersistsAcrossReload()
    {
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.UpsertConnection(Connection("gh-main", "https://github.invalid"));
        first.RemoveConnection("gh-main");

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();

        Assert.Empty(second.Connections);
    }

    [Fact]
    public async Task CorruptMainFile_FallsBackToBackup()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.UpsertConnection(Connection("gh-first", "https://first.invalid"));  // 初版 → bak 前身
        store.UpsertConnection(Connection("gh-second", "https://second.invalid")); // Replace → bak = 初版

        var mainPath = System.IO.Path.Combine(dir.Path, "connections.json");
        await File.WriteAllTextAsync(mainPath, "{ this is not json"); // 模拟写入中断损坏

        var recovered = new JsonConfigurationStore(dir.Path);
        recovered.LoadAll();

        var connection = Assert.Single(recovered.Connections);
        Assert.Equal("gh-first", connection.Id); // 回退到备份版
    }

    [Fact]
    public void CorruptEverything_FallsBackToDefaults()
    {
        using var dir = new TempDir();
        File.WriteAllText(System.IO.Path.Combine(dir.Path, "connections.json"), "garbage");
        File.WriteAllText(System.IO.Path.Combine(dir.Path, "config.json"), "garbage");

        var store = new JsonConfigurationStore(dir.Path);
        store.LoadAll();

        Assert.Empty(store.Connections);
        Assert.Equal("Ctrl+Alt+B", store.App.Hotkey); // 默认值兜底，不抛异常
    }

    [Fact]
    public void MissingFiles_LoadsDefaults_AndSaveAppCreatesFile()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.LoadAll();

        Assert.Empty(store.Connections);
        Assert.Empty(store.Widgets);
        Assert.Empty(store.Pins.Tiles);

        store.SaveApp();
        Assert.True(File.Exists(System.IO.Path.Combine(dir.Path, "config.json")));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFilesBehind()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.UpsertConnection(Connection("gh-main", "https://github.invalid"));
        store.UpsertConnection(Connection("gh-second", "https://second.invalid"));

        Assert.DoesNotContain(Directory.GetFiles(dir.Path), f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void Pins_RoundTrips()
    {
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.Pins.Tiles.Add(new PinTile
        {
            WidgetId = "w-1",
            Layout = new PinLayout
            {
                Monitor = "DISPLAY1@2560x1440",
                Anchor = PinAnchor.TopRight,
                OffsetDips = new PinOffset(12, 48),
                Collapsed = false,
            },
        });
        first.SavePins();

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();

        var tile = Assert.Single(second.Pins.Tiles);
        Assert.Equal("w-1", tile.WidgetId);
        Assert.Equal(PinAnchor.TopRight, tile.Layout.Anchor);
        Assert.Equal(12, tile.Layout.OffsetDips.X);
        Assert.False(tile.Layout.Collapsed);
    }
}
