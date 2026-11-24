using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 刷新频率解析（SET-UX-10，2026-11-24 用户令批3）：「检查频率/刷新档/检测间隔」三名归一为
/// 「默认刷新间隔」（全局）与「此组件刷新频率」（组件）。行为锁：
/// ① 注册优先级 组件覆盖 > 全局间隔 > 档位策略表（与 WidgetHost.RegisterWidgets 同实现锁死）；
/// ② 继承来源四态封闭词表（component/global/tier/manual）；
/// ③ 手动档（static）展示「手动刷新」且生效间隔为 null——与运行期失败退避在文案上分开；
/// ④ 未知档回落 default；周期格式化分秒 60 进位（PollIntervalOptions 同口径）。
/// </summary>
public sealed class RefreshIntervalResolverTests
{
    // —— 注册优先级：EffectiveOverrideSeconds 与 WidgetHost 注册链逐分支等价 ——

    [Theory]
    [InlineData(120, 60, 120)] // 组件覆盖赢全局
    [InlineData(120, 0, 120)]  // 组件覆盖、无全局
    [InlineData(null, 300, 300)] // 全局赢档位
    public void EffectiveOverrideSeconds_Priority_WinsOverLower(int? widget, int global, int expected)
        => Assert.Equal(expected, RefreshIntervalResolver.EffectiveOverrideSeconds(widget, global));

    [Fact]
    public void EffectiveOverrideSeconds_NoneSet_ReturnsNull_TierTableDecides()
    {
        // 都未设：null=调度器按档位策略表走（ci=15s、static=不自动）——注册链行为不变
        Assert.Null(RefreshIntervalResolver.EffectiveOverrideSeconds(null, 0));
        Assert.Null(RefreshIntervalResolver.EffectiveOverrideSeconds(0, 0)); // 非正值视同未设
        Assert.Null(RefreshIntervalResolver.EffectiveOverrideSeconds(-5, -1));
    }

    [Fact]
    public void WidgetHost_PriorityChain_MirrorsOldInlineLogic()
    {
        // 与旧内联逻辑的逐分支等价门（重构回归锁）：组件>0 → 组件；否则全局>0 → 全局；否则 null
        var samples = new (int? Widget, int Global)[]
        {
            (null, 0), (null, 60), (30, 0), (30, 300), (0, 60), (120, 15), (-1, 45),
        };
        foreach (var (widget, global) in samples)
        {
            var expected = widget is { } seconds && seconds > 0
                ? seconds
                : global > 0 ? global : (int?)null;
            Assert.Equal(expected, RefreshIntervalResolver.EffectiveOverrideSeconds(widget, global));
        }
    }

    // —— 继承来源：四态封闭词表 ——

    [Fact]
    public void SourceOf_ComponentOverride_Component()
        => Assert.Equal(RefreshIntervalResolver.SourceComponent,
            RefreshIntervalResolver.SourceOf(120, 60, RefreshTiers.Ci));

    [Fact]
    public void SourceOf_GlobalOverride_Global()
        => Assert.Equal(RefreshIntervalResolver.SourceGlobal,
            RefreshIntervalResolver.SourceOf(null, 300, RefreshTiers.Ci));

    [Fact]
    public void SourceOf_NoOverride_Tier()
        => Assert.Equal(RefreshIntervalResolver.SourceTier,
            RefreshIntervalResolver.SourceOf(null, 0, RefreshTiers.Ci));

    [Fact]
    public void SourceOf_StaticTier_Manual()
        => Assert.Equal(RefreshIntervalResolver.SourceManual,
            RefreshIntervalResolver.SourceOf(null, 0, RefreshTiers.Static));

    [Fact]
    public void SourceOf_UnknownTier_FallsBackToDefaultTier()
        // 未知档回落 default（60 秒，非手动）→ tier 来源
        => Assert.Equal(RefreshIntervalResolver.SourceTier,
            RefreshIntervalResolver.SourceOf(null, 0, "no-such-tier"));

    // —— 展示口径：生效秒数与人话标签 ——

    [Fact]
    public void EffectiveDisplaySeconds_TierDefault_Resolved()
        => Assert.Equal(15, RefreshIntervalResolver.EffectiveDisplaySeconds(null, 0, RefreshTiers.Ci));

    [Fact]
    public void EffectiveDisplaySeconds_Manual_Null()
        => Assert.Null(RefreshIntervalResolver.EffectiveDisplaySeconds(null, 0, RefreshTiers.Static));

    [Theory]
    [InlineData(120, 60, RefreshTiers.Ci, "每 2 分钟 · 此组件刷新频率")]
    [InlineData(null, 300, RefreshTiers.Ci, "每 5 分钟 · 默认刷新间隔")]
    [InlineData(null, 0, RefreshTiers.Ci, "每 15 秒 · 刷新档 ci")]
    [InlineData(null, 0, RefreshTiers.Pr, "每 1 分钟 · 刷新档 pr")]
    [InlineData(null, 0, RefreshTiers.Static, "手动刷新 · 不自动拉取")]
    public void Describe_Labels_NameSourceAndPeriod(int? widget, int global, string tier, string expected)
        => Assert.Equal(expected, RefreshIntervalResolver.Describe(widget, global, tier));

    // —— 周期格式化：分秒 60 进位（PollIntervalOptions.CustomLabel 同口径） ——

    [Theory]
    [InlineData(120, "2 分钟")]
    [InlineData(3600, "60 分钟")]
    [InlineData(45, "45 秒")]
    [InlineData(90, "1 分 30 秒")]
    [InlineData(75, "1 分 15 秒")]
    public void FormatPeriod_FormatsMinutesAndSeconds(int seconds, string expected)
        => Assert.Equal(expected, RefreshIntervalResolver.FormatPeriod(seconds));
}
