using Beacon.Core.Models;

namespace Beacon.Storage.Tests;

/// <summary>
/// 配置面边界收口（Batch 4）：RemoveWidget 持久化、UpsertAction 替换语义、
/// AppConfigFile 最小读写（Load 缺失/往返/损坏回退）。AppConfigFile 走 ApplicationData，
/// 用 XDG_CONFIG_HOME 定向临时目录，不触碰真实用户配置。
/// </summary>
public sealed class ConfigurationEdgeTests
{
    // ---------- JsonConfigurationStore 增删改 ----------

    [Fact]
    public void RemoveWidget_PersistsAcrossReload()
    {
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.UpsertWidget(new WidgetConfig { Id = "w-1", Type = "github.pr.list", ConnectionId = "c" });
        first.UpsertWidget(new WidgetConfig { Id = "w-2", Type = "copilot.usage", ConnectionId = "c" });
        first.RemoveWidget("w-1");

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();
        Assert.Single(second.Widgets);
        Assert.Equal("w-2", second.Widgets[0].Id);
    }

    [Fact]
    public void RemoveWidget_UnknownId_IsNoOp()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.RemoveWidget("ghost"); // 不抛即可
    }

    [Fact]
    public void UpsertAction_SameId_ReplacesInPlace()
    {
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.UpsertAction(new ActionConfig { Id = "a-1", Type = "http", Parameters = new Dictionary<string, string> { ["url"] = "https://one" } });
        first.UpsertAction(new ActionConfig { Id = "a-1", Type = "http", Parameters = new Dictionary<string, string> { ["url"] = "https://two" } });

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();
        var action = Assert.Single(second.Actions);
        Assert.Equal("https://two", action.Parameters["url"]);
    }

    // ---------- AppConfigFile（根目录注入隔离：构造传 TempDir，Linux/Windows 双平台真隔离） ----------

    [Fact]
    public void AppConfig_Load_MissingFile_ReturnsDefaults()
    {
        using var dir = new TempDir();
        var config = new AppConfigFile(dir.Path).Load();
        Assert.NotNull(config);
    }

    [Fact]
    public void AppConfig_SaveThenLoad_RoundTrips()
    {
        using var dir = new TempDir();
        var file = new AppConfigFile(dir.Path);
        file.Save(new AppConfig { Hotkey = "Ctrl+Shift+B" });

        var reloaded = new AppConfigFile(dir.Path).Load();
        Assert.Equal("Ctrl+Shift+B", reloaded.Hotkey);
    }

    [Fact]
    public void AppConfig_Load_CorruptFile_ReturnsDefaults()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Path);
        File.WriteAllText(Path.Combine(dir.Path, "config.json"), "{not valid json");

        var config = new AppConfigFile(dir.Path).Load();
        Assert.NotNull(config);
    }

    [Fact]
    public void AppConfig_Save_RetainsBackup_CorruptLoadRecoversFromIt()
    {
        // P1 审核修复（AppConfigFile）：Save 此前 File.Move(overwrite) 不留 .bak；Load 损坏时
        // 静默回默认（落盘窗口被读 = 热键/主题/自启归零）。修复后 Save 留 .bak，损坏走 *.corrupt
        // 留档 + .bak 恢复（与 JsonConfigurationStore 同口径）。
        using var dir = new TempDir();
        var file = new AppConfigFile(dir.Path);
        file.Save(new AppConfig { Hotkey = "Ctrl+Shift+B" });
        file.Save(new AppConfig { Hotkey = "Ctrl+Alt+T" }); // 旧值进 .bak

        Assert.True(File.Exists(Path.Combine(dir.Path, "config.json.bak"))); // Save 留备份

        File.WriteAllText(Path.Combine(dir.Path, "config.json"), "{not valid json"); // 落盘窗口被读
        var recovered = file.Load();

        Assert.Equal("Ctrl+Shift+B", recovered.Hotkey); // .bak 恢复，不归默认
        Assert.True(File.Exists(Path.Combine(dir.Path, "config.json.corrupt"))); // 坏文件留档
    }
}
