using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 岛收起态展示计划（ISLAND-D1..D3）：展示方式三档（single 单组件常驻 / stack 多组件堆叠 /
/// carousel 徽标+轮播）、上岛组件选择（IslandComponents 白名单，列表序=上岛序）、
/// 真实数据面构建（severity/progress 进面，零数据/断连/过期按占位不造假）。
/// 纯函数决策全部沉 Core：App 侧只做渲染与喂数（Linux 本地可测，CS 风险不上推）。
/// </summary>
public sealed class IslandDisplayPlanTests
{
    private static WidgetConfig Widget(string id, string type, string? label = null)
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (label is not null)
        {
            config["label"] = label;
        }
        return new WidgetConfig { Id = id, Type = type, ConnectionId = "conn-1", Config = config };
    }

    private static WidgetState State(string id, Severity severity = Severity.Info, double? progress = null,
        bool stale = false, bool healthy = true, string summary = "运行中")
        => new()
        {
            WidgetId = id,
            WidgetType = "t",
            ConnectionId = "c-1",
            Severity = severity,
            Summary = summary,
            Progress = progress,
            IsStale = stale,
            ConnectionHealthy = healthy,
        };

    private static IslandContext Ctx(int unread = 0, int abnormal = 0, double okShare = 1.0)
        => new(unread, abnormal, okShare, Severity.Success);

    private static IslandPlan Plan(AppConfig app, IReadOnlyList<WidgetConfig> widgets,
        IReadOnlyDictionary<string, WidgetState> states, IslandContext ctx, int carouselIndex = 0)
        => IslandDisplayPlan.Compute(app, widgets, states, ctx, carouselIndex);

    // —— 展示方式归一（ISLAND-D1：fail-open，非法值不炸不造假） ——

    [Theory]
    [InlineData(IslandDisplayPlan.ModeSingle, IslandDisplayPlan.ModeSingle)]
    [InlineData(IslandDisplayPlan.ModeStack, IslandDisplayPlan.ModeStack)]
    [InlineData(IslandDisplayPlan.ModeCarousel, IslandDisplayPlan.ModeCarousel)]
    [InlineData("", IslandDisplayPlan.ModeStack)]
    [InlineData("junk", IslandDisplayPlan.ModeStack)]
    [InlineData(null, IslandDisplayPlan.ModeStack)]
    public void NormalizeMode_UnknownValues_FallOpenToStack(string? mode, string expected)
        => Assert.Equal(expected, IslandDisplayPlan.NormalizeMode(mode));

    [Fact]
    public void Config_DefaultDisplayMode_IsStack()
        => Assert.Equal(IslandDisplayPlan.ModeStack, new AppConfig().IslandDisplayMode);

    // —— 基线：零上岛真实组件 = 环/条虚拟面（与 ISLAND-RING-1 现状同形） ——

    [Fact]
    public void Compute_ZeroOnboarded_RingBarVirtualFacesInOrder()
    {
        var plan = Plan(new AppConfig(), [], new Dictionary<string, WidgetState>(),
            Ctx(unread: 2, okShare: 0.75));

        Assert.Equal(IslandDisplayPlan.ModeStack, plan.Mode);
        Assert.Equal(2, plan.Faces.Count);
        Assert.Equal(ComponentCenterCatalog.IslandRingId, plan.Faces[0].WidgetId);
        Assert.Equal("ring", plan.Faces[0].Kind);
        Assert.Equal(0.75, plan.Faces[0].Progress);
        Assert.Equal("2", plan.Faces[0].Text); // 计数=未读优先（与现环心数字同口径）
        Assert.Equal(ComponentCenterCatalog.IslandBarId, plan.Faces[1].WidgetId);
        Assert.Equal("bar", plan.Faces[1].Kind);
        Assert.False(plan.Faces[0].Placeholder);
        Assert.Equal(0, plan.OverflowCount);
    }

    [Fact]
    public void Compute_CountPrecedes_UnreadThenAbnormalThenEmpty()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandRingId, visible: false); // 只留条
        var plan = Plan(app, [], new Dictionary<string, WidgetState>(), Ctx(abnormal: 3));
        Assert.Equal("3", plan.Faces.Single(f => f.Kind == "bar").Text);

        var silent = Plan(new AppConfig(), [], new Dictionary<string, WidgetState>(), Ctx());
        Assert.All(silent.Faces, face => Assert.Equal("", face.Text)); // 全零不落字
    }

    [Fact]
    public void Compute_RingBarBothHidden_ZeroOnboarded_EmptyPlan()
    {
        var app = new AppConfig();
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandRingId, visible: false);
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandBarId, visible: false);

        var plan = Plan(app, [], new Dictionary<string, WidgetState>(), Ctx());

        Assert.Empty(plan.Faces); // 岛收起态只剩品牌标（组件中心说了算，无兜底）
    }

    // —— 真实组件上岛（ISLAND-D2）与数据面（ISLAND-D3） ——

    [Fact]
    public void Compute_OnboardedWidgetWithProgress_BarFaceCarriesRealData()
    {
        var app = new AppConfig();
        app.IslandComponents.Add("w-1");
        var widgets = new[] { Widget("w-1", "jenkins.build", label: "主线构建") };
        var states = new Dictionary<string, WidgetState> { ["w-1"] = State("w-1", Severity.Warning, progress: 0.4) };

        var plan = Plan(app, widgets, states, Ctx());

        var face = Assert.Single(plan.Faces.Skip(2)); // 虚拟环/条之后第 3 面
        Assert.Equal("w-1", face.WidgetId);
        Assert.Equal("bar", face.Kind);
        Assert.Equal(0.4, face.Progress); // 真实进度进面
        Assert.Equal(Severity.Warning, face.Severity); // 真实级别进面
        Assert.Equal("主线构建", face.Text); // 面上文字=组件标签（摘要走 hover 预览）
        Assert.Equal("运行中", face.Summary);
        Assert.False(face.Placeholder);
    }

    [Fact]
    public void Compute_OnboardedWidgetWithoutProgress_ChipFace()
    {
        var app = new AppConfig();
        app.IslandComponents.Add("w-1");
        var widgets = new[] { Widget("w-1", "github.pull_requests", label: "PR 数") };
        var states = new Dictionary<string, WidgetState> { ["w-1"] = State("w-1", Severity.Success) };

        var face = Plan(app, widgets, states, Ctx()).Faces.Skip(2).Single();

        Assert.Equal("chip", face.Kind);
        Assert.Null(face.Progress);
        Assert.Equal(Severity.Success, face.Severity);
        Assert.False(face.Placeholder);
    }

    [Fact]
    public void Compute_MissingState_RenderPlaceholderNotFake()
    {
        var app = new AppConfig();
        app.IslandComponents.Add("w-1");
        var widgets = new[] { Widget("w-1", "claude.usage", label: "Claude 用量") };

        var face = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx()).Faces.Skip(2).Single();

        Assert.True(face.Placeholder); // 零数据=占位
        Assert.Null(face.Progress); // 不造进度
        Assert.Equal("Claude 用量", face.Text);
        Assert.Null(face.Summary);
    }

    [Theory]
    [InlineData(true, true)] // 数据过期（离线/连续失败）：连接仍健康
    [InlineData(false, false)] // 断连：连接不健康
    public void Compute_StaleOrDisconnected_RenderPlaceholder(bool stale, bool healthy)
    {
        var app = new AppConfig();
        app.IslandComponents.Add("w-1");
        var widgets = new[] { Widget("w-1", "kimi.coding", label: "Kimi") };
        var states = new Dictionary<string, WidgetState>
        {
            ["w-1"] = State("w-1", Severity.Error, progress: 0.9, stale: stale, healthy: healthy),
        };

        var face = Plan(app, widgets, states, Ctx()).Faces.Skip(2).Single();

        Assert.True(face.Placeholder); // 占位口径：过期/断连不照常渲染
        Assert.Null(face.Progress);
        Assert.Equal(Severity.Error, face.Severity); // 级别仍如实（数据最后一次真实级别）
    }

    [Fact]
    public void Compute_HiddenWidgetOnboarded_ExcludedFromIsland()
    {
        var app = new AppConfig();
        app.IslandComponents.Add("w-1");
        ComponentCenterCatalog.SetVisible(app, "w-1", visible: false); // 组件中心勾掉=全载体消失
        var widgets = new[] { Widget("w-1", "codex.usage", label: "Codex") };

        var plan = Plan(app, widgets, new Dictionary<string, WidgetState>
        {
            ["w-1"] = State("w-1"),
        }, Ctx());

        Assert.Equal(2, plan.Faces.Count); // 只剩虚拟环/条
    }

    [Fact]
    public void Compute_UnknownOnboardedId_SkippedSilently()
    {
        var app = new AppConfig();
        app.IslandComponents.Add("w-gone"); // 配置里已删的组件（陈旧白名单）

        var plan = Plan(app, [], new Dictionary<string, WidgetState>(), Ctx());

        Assert.Equal(2, plan.Faces.Count); // 不抛不占位：跳过
    }

    [Fact]
    public void Compute_OnboardingOrder_IslandsFollowListOrder()
    {
        var app = new AppConfig();
        app.IslandComponents.Add("w-b");
        app.IslandComponents.Add("w-a");
        var widgets = new[] { Widget("w-a", "ark.usage", label: "A"), Widget("w-b", "qwen.usage", label: "B") };

        var faces = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx()).Faces;

        Assert.Equal("w-b", faces[2].WidgetId); // 上岛序=白名单列表序（非 widgets.json 序）
        Assert.Equal("w-a", faces[3].WidgetId);
    }

    // —— 档位窗口（宽度上限/堆叠裁剪/轮播取窗） ——

    [Fact]
    public void Compute_SingleMode_KeepsFirstFaceOnly()
    {
        var app = new AppConfig() { IslandDisplayMode = IslandDisplayPlan.ModeSingle };
        app.IslandComponents.Add("w-1");
        app.IslandComponents.Add("w-2");
        var widgets = new[] { Widget("w-1", "ark.usage", label: "A"), Widget("w-2", "qwen.usage", label: "B") };

        var plan = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx());

        Assert.Equal(IslandDisplayPlan.ModeSingle, plan.Mode);
        Assert.Single(plan.Faces);
        Assert.Equal(ComponentCenterCatalog.IslandRingId, plan.Faces[0].WidgetId); // 常驻=序首（环）
        Assert.Equal(3, plan.OverflowCount); // 其余进溢出池
    }

    [Fact]
    public void Compute_StackMode_CapsAtCollapsedRowLimit()
    {
        var app = new AppConfig();
        for (var i = 0; i < 5; i++)
        {
            app.IslandComponents.Add($"w-{i}");
        }
        var widgets = Enumerable.Range(0, 5).Select(i => Widget($"w-{i}", "ark.usage", label: $"W{i}")).ToList();

        var plan = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx());

        Assert.Equal(IslandDisplayPlan.MaxCollapsedFaces, plan.Faces.Count); // 收起行上限
        Assert.Equal(3, plan.OverflowCount); // 2 虚拟 + 5 真实 = 7 候选，溢出 3
    }

    [Fact]
    public void Compute_CarouselMode_BadgePlusRotatingFace()
    {
        var app = new AppConfig() { IslandDisplayMode = IslandDisplayPlan.ModeCarousel };
        app.IslandComponents.Add("w-1");
        app.IslandComponents.Add("w-2");
        var widgets = new[] { Widget("w-1", "ark.usage", label: "A"), Widget("w-2", "qwen.usage", label: "B") };

        var first = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx(unread: 4), carouselIndex: 0);
        Assert.True(first.BadgeVisible);
        Assert.Equal(4, first.BadgeCount); // 徽标=未读优先、异常次之（与现计数同裁决）
        Assert.Single(first.Faces);
        Assert.Equal(ComponentCenterCatalog.IslandRingId, first.Faces[0].WidgetId); // 第 0 拍=池首（环）
        Assert.Equal(3, first.OverflowCount); // 池=4 候选，可见 1

        var rotated = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx(), carouselIndex: 2);
        Assert.False(rotated.BadgeVisible); // 全零徽标隐藏
        Assert.Equal("w-1", rotated.Faces.Single().WidgetId); // 第 2 拍轮到首个真实组件

        var wrapped = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx(), carouselIndex: 4);
        Assert.Equal(ComponentCenterCatalog.IslandRingId, wrapped.Faces.Single().WidgetId); // 4 mod 4=回卷

        var negative = Plan(app, widgets, new Dictionary<string, WidgetState>(), Ctx(), carouselIndex: -1);
        Assert.Equal("w-2", negative.Faces.Single().WidgetId); // 负下标归一回卷（不抛）
    }

    [Fact]
    public void Compute_CarouselMode_ZeroCandidates_BadgeOnlyPlan()
    {
        var app = new AppConfig() { IslandDisplayMode = IslandDisplayPlan.ModeCarousel };
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandRingId, visible: false);
        ComponentCenterCatalog.SetVisible(app, ComponentCenterCatalog.IslandBarId, visible: false);

        var plan = Plan(app, [], new Dictionary<string, WidgetState>(), Ctx(unread: 1));

        Assert.True(plan.BadgeVisible);
        Assert.Empty(plan.Faces); // 池空：只剩徽标（App 兜底品牌标口径不变）
    }

    // —— 上岛白名单单源（与 HiddenComponents 同一套落库纪律） ——

    [Fact]
    public void SetIslandOnboarded_IdempotentAndOrderPreserving()
    {
        var app = new AppConfig();
        Assert.False(ComponentCenterCatalog.IslandOnboarded(app, "w-1")); // 缺席=不上岛（opt-in）
        ComponentCenterCatalog.SetIslandOnboarded(app, "w-1", onboarded: true);
        ComponentCenterCatalog.SetIslandOnboarded(app, "w-1", onboarded: true); // 幂等
        Assert.Equal(["w-1"], app.IslandComponents);
        ComponentCenterCatalog.SetIslandOnboarded(app, "w-2", onboarded: true);
        Assert.Equal(["w-1", "w-2"], app.IslandComponents); // 追加保序
        ComponentCenterCatalog.SetIslandOnboarded(app, "w-1", onboarded: false);
        Assert.Equal(["w-2"], app.IslandComponents);
    }

    [Fact]
    public void OnboardCandidates_ListsAllWidgetsInConfigOrder()
    {
        var candidates = ComponentCenterCatalog.OnboardCandidates(
        [
            Widget("w-2", "ark.usage", label: "B"),
            Widget("w-1", "github.pull_requests", label: "A"),
        ]);

        Assert.Equal(["w-2", "w-1"], candidates.Select(item => item.WidgetId));
        Assert.Equal("B", candidates[0].Label);
    }

    // —— 持久化（config.json 同一单源，重启保留） ——

    [Fact]
    public void Config_RoundTrip_PersistsDisplayModeAndOnboardList()
    {
        var app = new AppConfig();
        app.IslandDisplayMode = IslandDisplayPlan.ModeCarousel;
        ComponentCenterCatalog.SetIslandOnboarded(app, "w-9", onboarded: true);

        var restored = BeaconJson.RoundTrip(app);

        Assert.Equal(IslandDisplayPlan.ModeCarousel, restored.IslandDisplayMode);
        Assert.Equal(["w-9"], restored.IslandComponents);
        Assert.True(ComponentCenterCatalog.IslandOnboarded(restored, "w-9"));
    }
}
