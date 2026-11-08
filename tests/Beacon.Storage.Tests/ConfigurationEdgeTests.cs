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

    // ---------- AppConfigFile（ApplicationData，经 XDG_CONFIG_HOME 隔离） ----------

    private static string? SwapConfigHome(string? next)
    {
        var previous = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", next);
        return previous;
    }

    [Fact]
    public void AppConfig_Load_MissingFile_ReturnsDefaults()
    {
        using var dir = new TempDir();
        var previous = SwapConfigHome(dir.Path);
        try
        {
            var config = new AppConfigFile().Load();
            Assert.NotNull(config);
        }
        finally
        {
            SwapConfigHome(previous);
        }
    }

    [Fact]
    public void AppConfig_SaveThenLoad_RoundTrips()
    {
        using var dir = new TempDir();
        var previous = SwapConfigHome(dir.Path);
        try
        {
            var file = new AppConfigFile();
            var config = new AppConfig { Hotkey = "Ctrl+Shift+B" };
            file.Save(config);

            var reloaded = new AppConfigFile().Load();
            Assert.Equal("Ctrl+Shift+B", reloaded.Hotkey);
        }
        finally
        {
            SwapConfigHome(previous);
        }
    }

    [Fact]
    public void AppConfig_Load_CorruptFile_ReturnsDefaults()
    {
        using var dir = new TempDir();
        var previous = SwapConfigHome(dir.Path);
        try
        {
            var beaconDir = Path.Combine(dir.Path, "Beacon");
            Directory.CreateDirectory(beaconDir);
            File.WriteAllText(Path.Combine(beaconDir, "config.json"), "{not valid json");

            var config = new AppConfigFile().Load();
            Assert.NotNull(config);
        }
        finally
        {
            SwapConfigHome(previous);
        }
    }
}
