using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage.Tests;

/// <summary>B-802 验收：四份配置打包往返一致；导出全文无 token（仅 credentialRef）；版本不符拒收。</summary>
public sealed class ImportExportTests
{
    private static JsonConfigurationStore PopulatedStore(string path)
    {
        var store = new JsonConfigurationStore(path);
        store.LoadAll();
        store.UpsertConnection(new ConnectionConfig
        {
            Id = "gh-main",
            Type = "github",
            Endpoint = "https://github.invalid",
            CredentialRef = "conn:gh-main", // 只存引用；token 在 DPAPI，绝不进配置
        });
        store.UpsertWidget(new WidgetConfig
        {
            Id = "github.pull_requests:beacon",
            Type = "github.pull_requests",
            ConnectionId = "gh-main",
            Config = new Dictionary<string, string> { ["repo"] = "cuihairu/beacon" },
            RefreshTier = RefreshTiers.Pr,
            Pinned = true,
            PinLayout = new PinLayout { Monitor = "DISPLAY1", Anchor = PinAnchor.TopRight, OffsetDips = new PinOffset(24, 24) },
        });
        store.Pins.Tiles.Add(new PinTile { WidgetId = "github.pull_requests:beacon", Layout = new PinLayout { Monitor = "DISPLAY1", Anchor = PinAnchor.TopRight, OffsetDips = new PinOffset(24, 24), Collapsed = true } });
        store.SavePins();
        return store;
    }

    [Fact]
    public void ExportImport_RoundTripsAllFourFiles()
    {
        var sourceDir = new TempDir();
        var source = PopulatedStore(sourceDir.Path);
        var bundlePath = Path.Combine(sourceDir.Path, "beacon-config.json");

        new ImportExport(source).Export(bundlePath);

        var targetDir = new TempDir();
        var target = new JsonConfigurationStore(targetDir.Path);
        target.LoadAll();
        var result = new ImportExport(target).Import(bundlePath);

        Assert.Equal(1, result.Connections);
        Assert.Equal(1, result.Widgets);
        Assert.Equal(1, result.Pins);
        // 全量保真：导入后的四份配置与原机逐字节一致
        Assert.Equal(ReadStore(source), ReadStore(target));
    }

    [Fact]
    public void Import_ReplacesPreviousStateInsteadOfMerging()
    {
        var sourceDir = new TempDir();
        var source = new JsonConfigurationStore(sourceDir.Path);
        source.LoadAll();
        source.UpsertWidget(new WidgetConfig { Id = "w-1", Type = "github.pull_requests", ConnectionId = "x" });
        source.UpsertWidget(new WidgetConfig { Id = "w-2", Type = "github.actions.runs", ConnectionId = "x" });
        var bundlePath = Path.Combine(sourceDir.Path, "bundle.json");
        new ImportExport(source).Export(bundlePath);

        var targetDir = new TempDir();
        var target = PopulatedStore(targetDir.Path); // 已有 1 连接 + 1 组件
        new ImportExport(target).Import(bundlePath);

        Assert.Empty(target.Connections);
        Assert.Equal(2, target.Widgets.Count);
    }

    [Fact]
    public void Export_ContainsCredentialRefButNeverSecretMaterial()
    {
        using var dir = new TempDir();
        var store = PopulatedStore(dir.Path);
        var bundlePath = Path.Combine(dir.Path, "bundle.json");
        new ImportExport(store).Export(bundlePath);

        var text = File.ReadAllText(bundlePath);
        // 引用随包走（导入后据此提示重录）
        Assert.Contains("conn:gh-main", text);
        // 明文 token 无处容身：包结构没有承载它的字段
        Assert.DoesNotContain("ghp_", text);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_ListsCredentialRefsForReEntry()
    {
        using var sourceDir = new TempDir();
        var source = PopulatedStore(sourceDir.Path);
        source.UpsertConnection(new ConnectionConfig { Id = "gh-second", Type = "github", CredentialRef = "conn:gh-second" });
        var bundlePath = Path.Combine(sourceDir.Path, "bundle.json");
        new ImportExport(source).Export(bundlePath);

        using var targetDir = new TempDir();
        var target = new JsonConfigurationStore(targetDir.Path);
        var result = new ImportExport(target).Import(bundlePath);

        Assert.Equal(["conn:gh-main", "conn:gh-second"], result.CredentialRefs);
    }

    [Fact]
    public void Import_RejectsUnknownBundleVersion()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.LoadAll();
        var json = BeaconJson.Serialize(new ConfigBundle { Version = 999 });
        var bundlePath = Path.Combine(dir.Path, "bundle.json");
        File.WriteAllText(bundlePath, json);

        var exception = Assert.Throws<InvalidOperationException>(() => new ImportExport(store).Import(bundlePath));
        Assert.Contains("999", exception.Message);
    }

    private static string ReadStore(JsonConfigurationStore store) => BeaconJson.Serialize(new
    {
        app = store.App,
        connections = store.Connections,
        widgets = store.Widgets,
        pins = store.Pins,
    });
}
