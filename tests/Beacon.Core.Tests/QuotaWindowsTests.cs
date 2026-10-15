using Beacon.Core.Selectors;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// QuotaWindows：嵌套额度窗口判定（2026-10-15 用户令，月 ⊇ 周 ⊇ 5h）。
/// 四类必测形态（月耗尽/周耗尽/5h 耗尽/三窗均有）各一，另锁「三窗并列 100%」回归——
/// 旧 worst-window 口径并列取先会命中 5h，把月度耗尽报成 5h 已用。
/// </summary>
public sealed class QuotaWindowsTests
{
    // ---- 耗尽判定：外层优先，命中即返回，不再看内层 ----

    [Fact]
    public void Select_MonthlyExhausted_IsTopLevelAndIgnoresInnerWindows()
    {
        // 月耗尽：最高级别。内层即使报低用量（接口独立计数），也不得再被拿来当主数值
        var decision = QuotaWindows.Select(fiveHourUsedPercent: 5, weeklyUsedPercent: 40, monthlyUsedPercent: 100);

        Assert.Equal(QuotaWindow.Monthly, decision.Exhausted);
        Assert.Equal(100, decision.ExhaustedPercent);
        Assert.Null(decision.Display); // 已耗尽态不展示内层，避免把内层读成「还有余额」
        Assert.Contains("月度", decision.ExhaustedReason);
    }

    [Fact]
    public void Select_WeeklyExhausted_NotMaskedByFreshFiveHour()
    {
        // 用户实测形态：周尽（100%），新 5h 窗刚重置（3%）——必须报周耗尽，不能被新窗掩盖
        var decision = QuotaWindows.Select(fiveHourUsedPercent: 3, weeklyUsedPercent: 100, monthlyUsedPercent: 12);

        Assert.Equal(QuotaWindow.Weekly, decision.Exhausted);
        Assert.Equal(100, decision.ExhaustedPercent);
        Assert.Null(decision.Display);
        Assert.Contains("周度", decision.ExhaustedReason);
    }

    [Fact]
    public void Select_FiveHourExhausted_OwnLevel()
    {
        // 仅 5h 耗尽（月/周仍有余额）：报 5h 耗尽，不抬升为周/月
        var decision = QuotaWindows.Select(fiveHourUsedPercent: 100, weeklyUsedPercent: 20, monthlyUsedPercent: 30);

        Assert.Equal(QuotaWindow.FiveHour, decision.Exhausted);
        Assert.Equal(100, decision.ExhaustedPercent);
        Assert.Contains("5 小时", decision.ExhaustedReason);
    }

    [Fact]
    public void Select_AllHaveBalance_ShowsFiveHour()
    {
        // 正常态：有额度时看 5h（限流档位）——不按已用最高取周/月
        var decision = QuotaWindows.Select(fiveHourUsedPercent: 10, weeklyUsedPercent: 95, monthlyUsedPercent: 80);

        Assert.Null(decision.Exhausted);
        Assert.Equal(QuotaWindow.FiveHour, decision.Display);
        Assert.Equal(10, decision.DisplayPercent);
        Assert.Null(decision.ExhaustedReason);
        Assert.False(decision.NoData);
    }

    // ---- 回归：三窗并列 100%（嵌套下同时耗尽），必须命中最外层 ----

    [Fact]
    public void Select_AllThreeAt100_NamesMonthly_NotFiveHour()
    {
        // 本次缺陷：旧口径并列取先（迭代序 5h→周→月）返回 5h，界面遂显示内层有余额
        var decision = QuotaWindows.Select(fiveHourUsedPercent: 100, weeklyUsedPercent: 100, monthlyUsedPercent: 100);

        Assert.Equal(QuotaWindow.Monthly, decision.Exhausted);
        Assert.Null(decision.Display);
    }

    [Fact]
    public void Select_WeeklyAndMonthlyAt100_FiveHourFresh_NamesMonthly()
    {
        var decision = QuotaWindows.Select(fiveHourUsedPercent: 5, weeklyUsedPercent: 100, monthlyUsedPercent: 100);

        Assert.Equal(QuotaWindow.Monthly, decision.Exhausted);
    }

    // ---- 耗尽边界与缺数据 ----

    [Theory]
    [InlineData(99.9, false)]  // 未到 100：未耗尽
    [InlineData(100d, true)]   // 恰 100：耗尽（整数字面量装箱成 Int32 无法转 double?，须显式 d）
    [InlineData(100.001, true)]
    public void IsExhausted_BoundaryAt100(double? usedPercent, bool expected)
    {
        Assert.Equal(expected, QuotaWindows.IsExhausted(usedPercent));
    }

    [Fact]
    public void IsExhausted_NoWindowData_IsNotExhausted()
    {
        // 该窗无数据不判尽（不能拿缺数据当「额度已耗尽」报）
        Assert.False(QuotaWindows.IsExhausted(null));
    }

    [Fact]
    public void Select_NoWindowData_NoData()
    {
        var decision = QuotaWindows.Select(null, null, null);

        Assert.Null(decision.Exhausted);
        Assert.Null(decision.Display);
        Assert.True(decision.NoData);
    }

    [Fact]
    public void Select_DisplayFallsBackFiveHourToWeeklyToMonthly()
    {
        // 正常态展示退让：5h 缺→周，周也缺→月（只影响展示，不影响耗尽判定）
        Assert.Equal(QuotaWindow.Weekly, QuotaWindows.Select(null, 20, 30).Display);
        Assert.Equal(20, QuotaWindows.Select(null, 20, 30).DisplayPercent);
        Assert.Equal(QuotaWindow.Monthly, QuotaWindows.Select(null, null, 30).Display);
        Assert.Equal(30, QuotaWindows.Select(null, null, 30).DisplayPercent);
    }

    [Fact]
    public void Label_FiveHourWeeklyMonthly()
    {
        Assert.Equal("5h", QuotaWindows.Label(QuotaWindow.FiveHour));
        Assert.Equal("周", QuotaWindows.Label(QuotaWindow.Weekly));
        Assert.Equal("月", QuotaWindows.Label(QuotaWindow.Monthly));
    }
}
