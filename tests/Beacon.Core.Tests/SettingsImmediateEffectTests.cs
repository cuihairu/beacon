using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 设置即时生效不变式（2026-11-15 用户令「设置之后没有立刻生效」，SET-INST-001）——Core 侧两半。
/// ① 详情窗按钮集：DetailWindow 每次打开把 Config.Actions 过滤成按钮，纯函数 ActionVisibility 决定
///    哪些动作留在集里；集合是**当下重算**的，所以动作增删当场改变按钮集，不必等重启。
/// ② 界面密度档：DesignTokens.Spacing 在窗构造期按当前档换算（regular 恒等 / compact ×0.75），
///    值只在重建时取——这正是 SaveDensity 必须广播 SettingsApplied 重建 L0 两宿主的根因
///    （不重建=旧值一直在用，表现成「改完不生效」）。
/// </summary>
public sealed class SettingsImmediateEffectTests
{
    private static ActionConfig Action(string id, string widgetType = "", string connectionId = "")
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["url"] = $"https://example.invalid/{id}",
        };
        if (widgetType.Length > 0)
        {
            parameters["widgetType"] = widgetType;
        }
        if (connectionId.Length > 0)
        {
            parameters["connection_id"] = connectionId;
        }

        return new ActionConfig
        {
            Id = id,
            Type = "http",
            Name = id,
            Parameters = parameters,
        };
    }

    /// <summary>按钮集推导口径镜像 DetailWindow 的候选循环（Config.Actions 顺序过滤）。</summary>
    private static string[] ButtonSet(IEnumerable<ActionConfig> candidates, string widgetType, string connectionId)
        => candidates.Where(a => ActionVisibility.IsVisible(a.Parameters, widgetType, connectionId)).Select(a => a.Id).ToArray();

    [Fact]
    public void AddingAction_GrowsNextDetailWindowButtonSet()
    {
        var store = new InMemoryConfigStore();
        store.UpsertAction(Action("a-global")); // 无作用域=全组件可见

        Assert.Equal(["a-global"], ButtonSet(store.Actions, "http.status", "conn-1"));
    }

    [Fact]
    public void RemovingAction_ShrinksNextDetailWindowButtonSet()
    {
        var store = new InMemoryConfigStore();
        store.UpsertAction(Action("a-global"));
        store.UpsertAction(Action("a-http", widgetType: "http.status"));

        Assert.Equal(["a-global", "a-http"], ButtonSet(store.Actions, "http.status", "conn-1"));

        store.RemoveAction("a-global"); // 行内删除（SettingsWindow.Actions 已补 SettingsApplied）

        Assert.Equal(["a-http"], ButtonSet(store.Actions, "http.status", "conn-1"));
    }

    [Fact]
    public void ScopedAction_OnlyAppearsOnItsWidgetType()
    {
        var store = new InMemoryConfigStore();
        store.UpsertAction(Action("a-jenkins", widgetType: "jenkins.build"));

        Assert.Empty(ButtonSet(store.Actions, "http.status", "conn-1"));
        Assert.Equal(["a-jenkins"], ButtonSet(store.Actions, "jenkins.build", "conn-1"));
    }

    [Fact]
    public void DensityTier_OnlyTakesEffectOnRebuild_ValuesDivergePerTier()
    {
        // 8 DIP 间距：三档给出三个不同像素值——证明档是构造期读的实时值，
        // 已建好的窗不会自己变，SaveDensity 落库后必须重建宿主（SettingsApplied）
        Assert.Equal(8, UiDensity.Spacing(8, UiDensity.Parse("regular")));
        Assert.Equal(6, UiDensity.Spacing(8, UiDensity.Parse("compact")));
        Assert.Equal(10, UiDensity.Spacing(8, UiDensity.Parse("spacious")));
    }

    [Fact]
    public void DensityUnknownOrBlank_FallsBackToRegular()
    {
        // fail-open：配置写坏不构成降级理由，间距回恒等
        Assert.Equal(8, UiDensity.Spacing(8, UiDensity.Parse(null)));
        Assert.Equal(8, UiDensity.Spacing(8, UiDensity.Parse("  ")));
        Assert.Equal(8, UiDensity.Spacing(8, UiDensity.Parse("cozy")));
    }
}
