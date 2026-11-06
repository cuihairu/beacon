using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 岛进度组件（ISLAND-RING-1，2026-11-07 用户令）：进度环与进度条是组件中心里的两个组件——
/// 与其它 23 类组件走同一套机制（按类型分组、勾选显隐、hiddenComponents 持久化）；
/// 岛显示什么完全由勾选状态决定：勾环显环、勾条显条、都勾都显、都不勾不显——无「默认样式」概念。
/// 行为锁：勾选变化（HiddenComponents 集变化）→ 岛渲染面（Ring/Bar 可见性）随之变化。
/// </summary>
public sealed class IslandProgressComponentTests
{
    // —— 注册：组件中心恒含悬浮岛组，两个进度组件固定在册 ——

    [Fact]
    public void Group_AlwaysContainsIslandGroup_WithRingAndBar()
    {
        var groups = ComponentCenterCatalog.Group([]); // 零 widget 也出岛组：岛进度组件不依赖 widgets.json
        var island = Assert.Single(groups, g => g.Key == ComponentCenterCatalog.CategoryIsland);
        Assert.Equal(
            [ComponentCenterCatalog.IslandRingId, ComponentCenterCatalog.IslandBarId],
            island.Items.Select(i => i.WidgetId).ToArray());
    }

    [Fact]
    public void Group_IslandGroupOrdersBeforeOther_AndAfterRealGroups()
    {
        var groups = ComponentCenterCatalog.Group(
        [
            new WidgetConfig { Id = "w-1", Type = "ark.usage", ConnectionId = "conn-1", Config = [] },
            new WidgetConfig { Id = "w-2", Type = "mystery.widget", ConnectionId = "conn-1", Config = [] },
        ]);
        // 悬浮岛组是虚拟组件组：按 CategoryOrder 殿后于 other 之后，不抢真实组件的列表位置
        Assert.Equal(
            [ComponentCenterCatalog.CategoryQuota, ComponentCenterCatalog.CategoryOther, ComponentCenterCatalog.CategoryIsland],
            groups.Select(g => g.Key).ToArray());
    }

    [Theory]
    [InlineData("island.progressring", ComponentCenterCatalog.CategoryIsland)]
    [InlineData("island.progressbar", ComponentCenterCatalog.CategoryIsland)]
    public void CategoryOf_IslandProgressTypes_MapToIslandCategory(string type, string expected)
        => Assert.Equal(expected, ComponentCenterCatalog.CategoryOf(type));

    // —— 勾选 → 岛渲染面（行为锁核心：勾选变化 → 岛渲染变化） ——

    [Fact]
    public void Faces_Default_BothVisible()
    {
        var app = new AppConfig();
        Assert.True(ComponentCenterCatalog.IslandRingVisible(app));
        Assert.True(ComponentCenterCatalog.IslandBarVisible(app));
    }

    [Fact]
    public void Faces_RingUnchecked_RingDisappearsBarStays()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandRingId, visible: false);
        Assert.False(ComponentCenterCatalog.IslandRingVisible(app));
        Assert.True(ComponentCenterCatalog.IslandBarVisible(app));
    }

    [Fact]
    public void Faces_BarUnchecked_BarDisappearsRingStays()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandBarId, visible: false);
        Assert.True(ComponentCenterCatalog.IslandRingVisible(app));
        Assert.False(ComponentCenterCatalog.IslandBarVisible(app));
    }

    [Fact]
    public void Faces_BothUnchecked_NeitherShows()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandRingId, visible: false);
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandBarId, visible: false);
        Assert.False(ComponentCenterCatalog.IslandRingVisible(app));
        Assert.False(ComponentCenterCatalog.IslandBarVisible(app));
    }

    // —— 持久化走同一 config（hiddenComponents 单源，重启保留） ——

    [Fact]
    public void Config_RoundTrip_PersistsIslandProgressCheckState()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandRingId, visible: false);
        var restored = BeaconJson.RoundTrip(app);
        Assert.Contains(ComponentCenterCatalog.IslandRingId, restored.HiddenComponents);
        Assert.DoesNotContain(ComponentCenterCatalog.IslandBarId, restored.HiddenComponents);
        Assert.False(ComponentCenterCatalog.IslandRingVisible(restored));
        Assert.True(ComponentCenterCatalog.IslandBarVisible(restored));
    }
}
