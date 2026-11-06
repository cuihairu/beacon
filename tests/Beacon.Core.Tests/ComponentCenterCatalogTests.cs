using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 组件中心目录（Component Center）：真实注册类型的类别划分、分组序、显隐判定与写入单源。
/// </summary>
public sealed class ComponentCenterCatalogTests
{
    private static WidgetConfig Widget(string id, string type, bool pinned = false, string? label = null)
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (label is not null)
        {
            config["label"] = label;
        }
        return new WidgetConfig { Id = id, Type = type, ConnectionId = "conn-1", Config = config, Pinned = pinned };
    }

    // —— 类别划分（按真实注册类型键） ——

    [Theory]
    [InlineData("github.pull_requests", ComponentCenterCatalog.CategoryStatus)]
    [InlineData("github.actions.runs", ComponentCenterCatalog.CategoryStatus)]
    [InlineData("jenkins.build", ComponentCenterCatalog.CategoryStatus)]
    [InlineData("http.status", ComponentCenterCatalog.CategoryStatus)]
    [InlineData("machine.local", ComponentCenterCatalog.CategoryMachine)]
    [InlineData("machine.net", ComponentCenterCatalog.CategoryMachine)]
    [InlineData("machine.git", ComponentCenterCatalog.CategoryMachine)]
    [InlineData("herald.incidents", ComponentCenterCatalog.CategoryEvents)]
    [InlineData("herald.feed", ComponentCenterCatalog.CategoryEvents)]
    [InlineData("herald.deliveries", ComponentCenterCatalog.CategoryEvents)]
    [InlineData("http.quota", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("bigmodel.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("ark.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("claude.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("deepseek.balance", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("kimi.coding", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("moonshot.balance", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("mimo.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("qwen.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("sensenova.tokenplan", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("codex.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("copilot.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("opencode.usage", ComponentCenterCatalog.CategoryQuota)]
    [InlineData("unknown.thing", ComponentCenterCatalog.CategoryOther)]
    public void CategoryOf_KnownRegistryTypes_MapsToRealCategory(string type, string expected)
        => Assert.Equal(expected, ComponentCenterCatalog.CategoryOf(type));

    // —— 分组：空类别不出组、组内保配置序、other 殿后 ——

    [Fact]
    public void Group_MixedTypes_OrdersByCategoryAndKeepsConfigOrder()
    {
        var groups = ComponentCenterCatalog.Group(
        [
            Widget("w-ark", "ark.usage"),
            Widget("w-gh2", "github.actions.runs"),
            Widget("w-machine", "machine.net"),
            Widget("w-gh1", "github.pull_requests"),
            Widget("w-herald", "herald.feed"),
            Widget("w-unknown", "mystery.widget"),
        ]);

        Assert.Equal(6, groups.Count); // 五个真实类别 + 悬浮岛虚拟组
        Assert.Equal(ComponentCenterCatalog.CategoryStatus, groups[0].Key);
        Assert.Equal(new[] { "w-gh2", "w-gh1" }, groups[0].Items.Select(i => i.WidgetId).ToArray()); // 组内保配置序
        Assert.Equal(ComponentCenterCatalog.CategoryMachine, groups[1].Key);
        Assert.Equal(ComponentCenterCatalog.CategoryEvents, groups[2].Key);
        Assert.Equal(ComponentCenterCatalog.CategoryQuota, groups[3].Key);
        Assert.Equal(ComponentCenterCatalog.CategoryOther, groups[4].Key); // 兜底殿后
        Assert.Equal(ComponentCenterCatalog.CategoryIsland, groups[5].Key); // 岛虚拟组件组恒殿后
    }

    [Fact]
    public void Group_EmptyInput_IslandGroupStillPresent()
    {
        var groups = ComponentCenterCatalog.Group([]); // 岛进度组件不依赖 widgets.json：零组件也出岛组
        var island = Assert.Single(groups);
        Assert.Equal(ComponentCenterCatalog.CategoryIsland, island.Key);
    }

    [Fact]
    public void Group_ItemProjectsLabelAndPinState()
    {
        var groups = ComponentCenterCatalog.Group([Widget("w-1", "github.pull_requests", pinned: true, label: "API 网关")]);
        var item = Assert.Single(groups[0].Items);
        Assert.Equal("API 网关", item.Label);
        Assert.True(item.Pinned);
        Assert.Equal("github.pull_requests", item.TypeName);
    }

    // —— 条目显示名：label/repo/owner 优先，类型短名兜底（与 PinTile 标签同源） ——

    [Fact]
    public void ItemLabel_UserLabelWins()
        => Assert.Equal("我的面板", ComponentCenterCatalog.ItemLabel(Widget("w-1", "ark.usage", label: "我的面板")));

    [Fact]
    public void ItemLabel_NoLabel_FallsBackToTypeShortName()
        => Assert.Equal("火山方舟", ComponentCenterCatalog.ItemLabel(Widget("w-1", "ark.usage")));

    // —— 显隐判定与写入（缺省=可见、幂等） ——

    [Fact]
    public void IsHidden_AbsentId_NotHidden()
    {
        var app = new AppConfig();
        Assert.False(ComponentCenterCatalog.IsHidden(app, "w-1"));
    }

    [Fact]
    public void SetVisible_ToggleRoundTrip_Idempotent()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, "w-1", visible: false);
        ComponentCenterCatalog.SetVisible(app, "w-1", visible: false); // 幂等：重复勾隐藏不加重
        Assert.True(ComponentCenterCatalog.IsHidden(app, "w-1"));
        Assert.Single(app.HiddenComponents);

        ComponentCenterCatalog.SetVisible(app, "w-1", visible: true);
        Assert.False(ComponentCenterCatalog.IsHidden(app, "w-1"));
        Assert.Empty(app.HiddenComponents);
    }

    [Fact]
    public void AppConfig_RoundTrip_PersistsHiddenComponents()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, "w-1", visible: false);
        var restored = BeaconJson.RoundTrip(app);
        Assert.Equal(["w-1"], restored.HiddenComponents);
    }
}
