using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 样式推荐（SET-UX-05，2026-11-24 原型落地批2）：按类型数据能力给模板候选推荐排序。行为锁：
/// ① 兼容项在前（注册表序），首个兼容项即推荐档（画廊默认选中位同源）；
/// ② 不兼容项殿后但保留（不再静默剔除），带 CanApply 同源中文原因——「为什么没有进度样式」要看得见；
/// ③ 判定与画廊共用 CanApply（能力声明优先，缺声明回退当前值推断旧口径）；
/// ④ 能力摘要（CapabilityNote）按声明能力给中文说明：进度+窗口 / 仅进度 / 仅窗口 /
///    仅金额 / 纯状态 / 未声明六种口吻各一句。
/// </summary>
public sealed class WidgetStyleAdviceTests
{
    private static WidgetState State(double? progress) => new()
    {
        WidgetId = "w",
        WidgetType = "t",
        ConnectionId = "c",
        Severity = Severity.Info,
        Lifecycle = LifecycleState.Success,
        Progress = progress,
        Summary = "摘要",
        FetchedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void ProgressType_RecommendsRegistryOrderAndBlocksNothing()
    {
        var advice = WidgetStyleAdvice.List(DisplayTemplateKinds.ProgressOnly, State(null));
        Assert.True(advice[0].Recommended);
        Assert.Equal("bar", advice[0].TemplateId); // 注册表序首位=横条=默认档
        Assert.All(advice, row => Assert.True(row.Ok)); // 全兼容：无置灰行
        Assert.All(advice, row => Assert.Null(row.Reason));
        Assert.Equal(5, advice.Count); // bar/ring/number/pool/statusline 全列
    }

    [Fact]
    public void StatusType_AppendsBlockedRowsWithReason()
    {
        var advice = WidgetStyleAdvice.List(DisplayTemplateKinds.StatusOnly, State(null));
        var ok = advice.Where(row => row.Ok).ToArray();
        var blocked = advice.Where(row => !row.Ok).ToArray();
        Assert.Single(ok); // 状态行唯一兼容
        Assert.Equal("statusline", ok[0].TemplateId);
        Assert.True(ok[0].Recommended);
        Assert.Equal(4, blocked.Length); // 横条/圆环/数字卡/水位全部殿后
        Assert.All(blocked, row => Assert.False(row.Recommended));
        Assert.All(blocked, row => Assert.Contains("百分比", row.Reason!)); // 原因点名无百分比
    }

    [Fact]
    public void AmountType_KeepsReasonVisibleNotSilent()
    {
        var advice = WidgetStyleAdvice.List(DisplayTemplateKinds.AmountOnly, State(0.5));
        // 有当前值也不放行（能力声明优先，「这次有值」≠「支持进度」）；原因点名缺的是百分比
        var blocked = advice.Where(row => !row.Ok).ToArray();
        Assert.Equal(4, blocked.Length);
        Assert.All(blocked, row => Assert.Contains("百分比", row.Reason!));
        // 金额类的独有口吻在能力摘要（CapabilityNote）里——见下测试
    }

    [Fact]
    public void WindowsOnlyType_BlockedLikeStatusType()
    {
        // 仅窗口（copilot/本机）：percent 选择器接通 payload 前不算 progress
        var advice = WidgetStyleAdvice.List(DisplayTemplateKinds.WindowsOnly, State(null));
        var ok = advice.Where(row => row.Ok).ToArray();
        Assert.Single(ok);
        Assert.Equal("statusline", ok[0].TemplateId);
    }

    [Fact]
    public void MissingDeclaration_FallsBackToCurrentValueInference()
    {
        // dataKinds=null 才走旧口径回退：state.Progress 有值 → 进度样式兼容
        var advice = WidgetStyleAdvice.List(null, State(0.52));
        Assert.All(advice, row => Assert.True(row.Ok));
        var adviceEmpty = WidgetStyleAdvice.List(null, null); // 无状态也无声明 → 只剩状态行
        Assert.All(adviceEmpty, row => Assert.Equal(row.TemplateId == "statusline", row.Ok));
        // 空集是「声明了没有任何能力」（不是缺声明）：即使当前值有进度也不放行——声明优先
        var adviceDeclaredNone = WidgetStyleAdvice.List(DisplayTemplateKinds.None, State(0.52));
        Assert.All(adviceDeclaredNone, row => Assert.Equal(row.TemplateId == "statusline", row.Ok));
    }

    [Fact]
    public void CapabilityNote_CoversAllDeclaredShapes()
    {
        Assert.Contains("多窗口", WidgetStyleAdvice.CapabilityNote(DisplayTemplateKinds.ProgressAndWindows));
        Assert.Contains("横条", WidgetStyleAdvice.CapabilityNote(DisplayTemplateKinds.ProgressOnly));
        Assert.Contains("置灰", WidgetStyleAdvice.CapabilityNote(DisplayTemplateKinds.WindowsOnly));
        Assert.Contains("余额金额", WidgetStyleAdvice.CapabilityNote(DisplayTemplateKinds.AmountOnly));
        Assert.Contains("状态行", WidgetStyleAdvice.CapabilityNote(DisplayTemplateKinds.StatusOnly));
        Assert.Contains("置灰", WidgetStyleAdvice.CapabilityNote(DisplayTemplateKinds.None)); // 未声明=通用句
        Assert.Contains("置灰", WidgetStyleAdvice.CapabilityNote(null));
    }

    [Fact]
    public void Describe_CoversFullVocabulary()
    {
        Assert.Equal("百分比", DisplayTemplateKinds.Describe(DisplayTemplateKinds.Progress));
        Assert.Equal("多窗口读数", DisplayTemplateKinds.Describe(DisplayTemplateKinds.Windows));
        Assert.Equal("余额金额", DisplayTemplateKinds.Describe(DisplayTemplateKinds.Amount));
        Assert.Equal("状态读数", DisplayTemplateKinds.Describe(DisplayTemplateKinds.Status));
        Assert.Equal("bogus", DisplayTemplateKinds.Describe("bogus")); // 未认词表回原词
    }
}
