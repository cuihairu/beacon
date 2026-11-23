using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 组件去向判定（SET-UX-04，2026-11-24 原型落地批2）：组件中心每行「桌面 / 悬浮岛」实际状态与
/// 阻挡原因的单源判定。行为锁：
/// ① 桌面列与 L0 宿主过滤链同序（不支持钉选 → 全局隐藏 → 来源停用 → 数值总开关 → 未钉选 → 已显示），
///    每档只报第一个命中的原因——组件中心行与桌面实况不可能各说各话；
/// ② 悬浮岛列沿 IslandDisplayPlan 渲染口径：岛关闭 / 已隐藏 / 未上岛 / 溢出（stack 4、single 1、
///    carousel 轮播全可达）/ 已上岛带位次；
/// ③ 全局隐藏双列同报（桌面 + 岛都不渲染，聚合同退——StatusAggregator 源头过滤口径）；
/// ④ 缺连接不算来源停用（与宿主「缺连接不拦」同口径）；
/// ⑤ floatingMode 注记独立窗形态（布局档语义面不改判定只改注记）。
/// </summary>
public sealed class ComponentPlacementTests
{
    private static readonly WidgetTypeDescriptor PinnedNumeric = new()
    {
        Type = "bigmodel.usage",
        DisplayName = "GLM 额度",
        PinSupported = true,
        FloatingOptIn = true,
    };

    private static readonly WidgetTypeDescriptor PinnedPlain = new()
    {
        Type = "github.actions.runs",
        DisplayName = "构建状态",
        PinSupported = true,
    };

    private static readonly WidgetTypeDescriptor NotPinnable = new()
    {
        Type = "unpinnable.thing",
        DisplayName = "不可钉",
        PinSupported = false,
    };

    private static WidgetConfig Widget(string type = "bigmodel.usage", bool pinned = true) => new()
    {
        Id = "w-test",
        Type = type,
        Pinned = pinned,
    };

    private static AppConfig App() => new()
    {
        // stack 为默认档：溢出上限 MaxCollapsedFaces（4）
        IslandDisplayMode = IslandDisplayPlan.ModeStack,
    };

    // —— 桌面列：与宿主过滤链同序 ——

    [Fact]
    public void Desktop_PinnedAndClear_ShownInPanel()
    {
        var report = ComponentPlacement.Describe(App(), Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: false, islandRank: 0);
        Assert.Equal(ComponentPlacement.DesktopShown, report.DesktopState);
        Assert.True(report.DesktopShown);
        Assert.Contains("集中面板", report.DesktopNote);
    }

    [Fact]
    public void Desktop_FloatingMode_NotesWindowForm()
    {
        var report = ComponentPlacement.Describe(App(), Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: true, islandOn: false, islandRank: 0);
        Assert.Equal(ComponentPlacement.DesktopShown, report.DesktopState);
        Assert.Contains("独立窗口", report.DesktopNote);
    }

    [Fact]
    public void Desktop_Unpinned_ReportsUnpinned()
    {
        var report = ComponentPlacement.Describe(App(), Widget(pinned: false), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: false, islandRank: 0);
        Assert.Equal(ComponentPlacement.DesktopUnpinned, report.DesktopState);
        Assert.False(report.DesktopShown);
    }

    [Fact]
    public void Desktop_NotPinSupported_ReportsUnsupportedBeforeAll()
    {
        // 不支持钉选是链首：即使已隐藏/停用叠加也只报它（与宿主第 1 判同序）
        var app = App();
        ComponentCenterCatalog.SetVisible(app, "w-test", visible: false);
        var report = ComponentPlacement.Describe(app, Widget(), NotPinnable,
            connectionEnabled: false, numericFloatingEnabled: false, floatingMode: false, islandOn: false, islandRank: 0);
        Assert.Equal(ComponentPlacement.DesktopUnsupported, report.DesktopState);
    }

    [Fact]
    public void Desktop_GlobalHidden_ReportsHiddenAndExitsAggregate()
    {
        var app = App();
        ComponentCenterCatalog.SetVisible(app, "w-test", visible: false);
        var report = ComponentPlacement.Describe(app, Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: true, islandRank: 1);
        Assert.Equal(ComponentPlacement.DesktopGlobalHidden, report.DesktopState);
        Assert.False(report.DesktopShown);
        Assert.Contains("聚合", report.DesktopNote); // 全局隐藏退出聚合的口径必须写在原因里
    }

    [Fact]
    public void Desktop_ConnectionDisabled_ReportsSourceDisabled()
    {
        var report = ComponentPlacement.Describe(App(), Widget(), PinnedNumeric,
            connectionEnabled: false, numericFloatingEnabled: true, floatingMode: false, islandOn: false, islandRank: 0);
        Assert.Equal(ComponentPlacement.DesktopSourceDisabled, report.DesktopState);
    }

    [Fact]
    public void Desktop_NumericSuppressed_ReportsSwitchOff()
    {
        var report = ComponentPlacement.Describe(App(), Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: false, floatingMode: false, islandOn: false, islandRank: 0);
        Assert.Equal(ComponentPlacement.DesktopNumericSuppressed, report.DesktopState);
        Assert.Contains("数值", report.DesktopNote); // 原因须点名「在桌面显示数值和额度」开关
    }

    [Fact]
    public void Desktop_NumericSwitchDoesNotAffectNonOptInTypes()
    {
        // 信息密集类（FloatingOptIn=false）不受数值总闸——判定链第 ④ 步跳过
        var report = ComponentPlacement.Describe(App(), Widget("github.actions.runs"), PinnedPlain,
            connectionEnabled: true, numericFloatingEnabled: false, floatingMode: false, islandOn: false, islandRank: 0);
        Assert.Equal(ComponentPlacement.DesktopShown, report.DesktopState);
    }

    // —— 悬浮岛列：IslandDisplayPlan 渲染口径 ——

    [Fact]
    public void Island_Off_ReportsIslandOff()
    {
        var report = ComponentPlacement.Describe(App(), Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: false, islandRank: 1);
        Assert.Equal(ComponentPlacement.IslandOff, report.IslandState);
        Assert.False(report.IslandShown);
    }

    [Fact]
    public void Island_OnboardedRank1_ShownWithRank()
    {
        var report = ComponentPlacement.Describe(App(), Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: true, islandRank: 1);
        Assert.Equal(ComponentPlacement.IslandOnboarded, report.IslandState);
        Assert.True(report.IslandShown);
        Assert.Contains("第 1 位", report.IslandNote);
    }

    [Fact]
    public void Island_NotOnboarded_ReportsNotOnboarded()
    {
        var report = ComponentPlacement.Describe(App(), Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: true, islandRank: 0);
        Assert.Equal(ComponentPlacement.IslandNotOnboarded, report.IslandState);
    }

    [Fact]
    public void Island_Hidden_ReportsHiddenEvenOnboarded()
    {
        var app = App();
        ComponentCenterCatalog.SetVisible(app, "w-test", visible: false);
        var report = ComponentPlacement.Describe(app, Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: true, islandRank: 1);
        Assert.Equal(ComponentPlacement.IslandHidden, report.IslandState);
        Assert.False(report.IslandShown);
    }

    [Theory]
    [InlineData(IslandDisplayPlan.ModeStack, 5, ComponentPlacement.IslandOverflows)]
    [InlineData(IslandDisplayPlan.ModeStack, 4, ComponentPlacement.IslandOnboarded)]
    [InlineData(IslandDisplayPlan.ModeSingle, 2, ComponentPlacement.IslandOverflows)]
    [InlineData(IslandDisplayPlan.ModeSingle, 1, ComponentPlacement.IslandOnboarded)]
    [InlineData(IslandDisplayPlan.ModeCarousel, 5, ComponentPlacement.IslandOnboarded)] // 轮播全可达不算溢出
    public void Island_OverflowFollowsDisplayMode(string mode, int rank, string expected)
    {
        var app = new AppConfig { IslandDisplayMode = mode };
        var report = ComponentPlacement.Describe(app, Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: true, islandRank: rank);
        Assert.Equal(expected, report.IslandState);
    }

    [Fact]
    public void Island_UnknownMode_FollowsStackLimit()
    {
        // NormalizeMode fail-open → stack：溢出上限同 stack（未知档不放大窗口位）
        var app = new AppConfig { IslandDisplayMode = "bogus" };
        var report = ComponentPlacement.Describe(app, Widget(), PinnedNumeric,
            connectionEnabled: true, numericFloatingEnabled: true, floatingMode: false, islandOn: true, islandRank: 5);
        Assert.Equal(ComponentPlacement.IslandOverflows, report.IslandState);
    }
}
