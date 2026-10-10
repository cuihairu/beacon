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
    public async Task CorruptMainFile_KeepsCorruptCopy_AndReportsDiagnostics()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.UpsertConnection(Connection("gh-first", "https://first.invalid"));
        store.UpsertConnection(Connection("gh-second", "https://second.invalid"));

        var mainPath = System.IO.Path.Combine(dir.Path, "connections.json");
        await File.WriteAllTextAsync(mainPath, "{ this is not json");

        var recovered = new JsonConfigurationStore(dir.Path);
        recovered.LoadAll();

        Assert.Single(recovered.Connections); // .bak 恢复生效
        Assert.Contains(recovered.LoadErrors, e => e.StartsWith("connections.json", StringComparison.Ordinal));
        Assert.Contains(recovered.LoadErrors, e => e.Contains(".bak", StringComparison.Ordinal));
        var corrupt = System.IO.Path.Combine(dir.Path, "connections.json.corrupt");
        Assert.True(File.Exists(corrupt)); // 损坏原件留档（后续落盘不再冲掉恢复依据）
        Assert.Contains("not json", await File.ReadAllTextAsync(corrupt));
    }

    [Fact]
    public void LoadAllBeforeMutate_PreservesPreexistingConfig()
    {
        // 「退出再启动，之前的配置消失了」回归锚（2026-10-08 实证：LoadAll 全仓只有测试在调，
        // App 启动跑在空配置上，会话内任何落盘把空集原子写回真实文件）——
        // 写路径前必须 LoadAll（BeaconRuntime.Start 装配第一步），无关落盘不得波及其他文件内容
        using var dir = new TempDir();
        var previous = new JsonConfigurationStore(dir.Path);
        previous.UpsertConnection(Connection("gh-main", "https://github.invalid"));
        previous.UpsertWidget(new WidgetConfig { Id = "w-1", Type = "github.pr.list", ConnectionId = "gh-main" });

        var session = new JsonConfigurationStore(dir.Path);
        session.LoadAll();
        session.App.LaunchOnStartup = false;
        session.SaveApp(); // 只改设置，连接/组件必须原样保留

        var after = new JsonConfigurationStore(dir.Path);
        after.LoadAll();
        Assert.Single(after.Connections);
        Assert.Single(after.Widgets);
        Assert.False(after.App.LaunchOnStartup);
    }

    [Fact]
    public void AppConfig_AllFields_SurviveSaveAndReload()
    {
        // B-801 本地半（设置持久化）：全部设置项经 SaveApp 落盘、新实例 LoadAll（= 启动路径）后逐字段不变。
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.LoadAll();
        store.App.Hotkey = "Ctrl+Alt+T";
        store.App.LaunchOnStartup = false;
        store.App.Theme = "light";
        store.App.UiOpacity = 0.75;
        store.App.ShowCapsule = true;
        store.App.ConfigVersion = 1;
        store.App.PinDisplayMode = "floating";
        store.App.NumericFloatingEnabled = true;
        store.App.NumericFloatingResetDone = true;
        store.App.PollIntervalSeconds = 30;
        store.App.SettingsSidebarWidth = 336; // 2026-10-10 拖拽边栏/窗口记忆三字段往返
        store.App.SettingsWindowWidth = 1024;
        store.App.SettingsWindowHeight = 768;
        store.App.NotificationRules =
        [
            new NotificationRule { Id = "r1", WidgetType = "github.actions.runs", SeverityAtLeast = Severity.Error, Toast = true, Cooldown = TimeSpan.FromMinutes(10) },
        ];
        store.App.Appearance.SeverityColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["warning"] = "#ffaa00",
            ["offline"] = "#808080",
        };
        store.App.Appearance.Motion = new MotionConfig { Mode = "full", Intensity = 1.5 };
        store.SaveApp();

        var reloaded = new JsonConfigurationStore(dir.Path);
        reloaded.LoadAll();

        Assert.Equal("Ctrl+Alt+T", reloaded.App.Hotkey);
        Assert.False(reloaded.App.LaunchOnStartup);
        Assert.Equal("light", reloaded.App.Theme);
        Assert.Equal(0.75, reloaded.App.UiOpacity);
        Assert.True(reloaded.App.ShowCapsule);
        Assert.Equal(1, reloaded.App.ConfigVersion);
        Assert.Equal("floating", reloaded.App.PinDisplayMode);
        Assert.True(reloaded.App.NumericFloatingEnabled);
        Assert.True(reloaded.App.NumericFloatingResetDone);
        Assert.Equal(30, reloaded.App.PollIntervalSeconds);
        Assert.Equal(336, reloaded.App.SettingsSidebarWidth);
        Assert.Equal(1024, reloaded.App.SettingsWindowWidth);
        Assert.Equal(768, reloaded.App.SettingsWindowHeight);
        var rule = Assert.Single(reloaded.App.NotificationRules);
        Assert.Equal("r1", rule.Id);
        Assert.Equal(Severity.Error, rule.SeverityAtLeast);
        Assert.Equal(TimeSpan.FromMinutes(10), rule.Cooldown);
        Assert.Equal("#ffaa00", reloaded.App.Appearance.SeverityColors["warning"]);
        Assert.Equal("#808080", reloaded.App.Appearance.SeverityColors["offline"]);
        Assert.Equal("full", reloaded.App.Appearance.Motion.Mode);
        Assert.Equal(1.5, reloaded.App.Appearance.Motion.Intensity);
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

    [Fact]
    public void Actions_RoundTrip_UpsertAndRemovePersistAcrossReload()
    {
        // 自定义动作库（actions.json）：局域网打包机「触发打包」等用户自建动作跨重启可用
        using var dir = new TempDir();
        var first = new JsonConfigurationStore(dir.Path);
        first.UpsertAction(new ActionConfig
        {
            Id = "a-build",
            Type = "http",
            Name = "触发打包",
            RequireConfirmation = true,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["url"] = "http://192.168.5.9:8080/build",
                ["method"] = "POST",
                ["body"] = """{"job":"release"}""",
                ["widgetType"] = "http.status",
            },
        });
        first.UpsertAction(new ActionConfig { Id = "a-cmd", Type = "local.command", Name = "清理缓存", RequireConfirmation = false });

        var second = new JsonConfigurationStore(dir.Path);
        second.LoadAll();

        Assert.Equal(2, second.Actions.Count);
        var build = Assert.Single(second.Actions, a => a.Id == "a-build");
        Assert.Equal("触发打包", build.Name);
        Assert.Equal("POST", build.Parameters["method"]);
        Assert.Equal("http.status", build.Parameters["widgetType"]);
        Assert.True(build.RequireConfirmation);
        Assert.False(Assert.Single(second.Actions, a => a.Id == "a-cmd").RequireConfirmation);

        second.RemoveAction("a-cmd");
        var third = new JsonConfigurationStore(dir.Path);
        third.LoadAll();
        Assert.Single(third.Actions);
        Assert.Equal("a-build", third.Actions[0].Id);
        Assert.NotNull(third.FindAction("a-build"));
        Assert.Null(third.FindAction("a-cmd"));
    }
}
