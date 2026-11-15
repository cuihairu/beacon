using Beacon.Core.Models;

namespace Beacon.Storage.Tests;

/// <summary>
/// 设置即时生效不变式（2026-11-15 用户令「设置之后没有立刻生效」，SET-INST-001）——动作库写点半。
/// 缺口曾是：SettingsWindow.Actions 的 SaveAction/行内删除只调 SaveActions() 就静默，App 侧无从得知，
/// 详情窗按钮集要等下次冷启动才同步（SettingsApplied 广播已补回写路径）。
/// 本测试锁的是「不必等重启」这条链路的地基：详情窗每次打开读的是**同一个 store 实例的内存集合**
/// （DetailWindow 经 runtime.Config.Actions 取候选，不重新 LoadAll），所以
/// ① 增/删必须**当场**反映在 Actions 上（不能只写盘、留一份内存旧值等重启）；
/// ② 同时要落盘（跨重启可用是既有验收，本轮不回归）。
/// </summary>
public sealed class SettingsImmediateEffectTests
{
    private static ActionConfig Action(string id, string widgetType = "")
        => new()
        {
            Id = id,
            Type = "http",
            Name = id,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["url"] = $"https://example.invalid/{id}",
                ["widgetType"] = widgetType,
            },
        };

    [Fact]
    public void UpsertAction_IsVisibleToNextDetailWindowOpen_WithoutReload()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.LoadAll();

        store.UpsertAction(Action("a-build"));

        // 详情窗下次打开走同一个 store 实例的内存集合（无 LoadAll）——新动作必须立刻在候选里
        var live = store.Actions.Single(a => a.Id == "a-build");
        Assert.Equal("https://example.invalid/a-build", live.Parameters["url"]);

        var reloaded = new JsonConfigurationStore(dir.Path);
        reloaded.LoadAll();
        Assert.Equal("a-build", Assert.Single(reloaded.Actions).Id); // 同时落盘，跨重启不丢
    }

    [Fact]
    public void RemoveAction_IsVisibleToNextDetailWindowOpen_WithoutReload()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.UpsertAction(Action("a-build"));
        store.UpsertAction(Action("a-cmd"));

        store.RemoveAction("a-cmd");

        Assert.Equal(["a-build"], store.Actions.Select(a => a.Id)); // 内存当场收窄，不等冷启动
        Assert.Null(store.FindAction("a-cmd"));

        var reloaded = new JsonConfigurationStore(dir.Path);
        reloaded.LoadAll();
        Assert.Equal(["a-build"], reloaded.Actions.Select(a => a.Id));
    }

    [Fact]
    public void UpsertAction_SameId_ReplacesWithoutLeftover()
    {
        using var dir = new TempDir();
        var store = new JsonConfigurationStore(dir.Path);
        store.UpsertAction(Action("a-build"));

        store.UpsertAction(Action("a-build")); // 同 Id 二次保存=替换，不留两份按钮

        Assert.Equal(["a-build"], store.Actions.Select(a => a.Id));
    }
}
