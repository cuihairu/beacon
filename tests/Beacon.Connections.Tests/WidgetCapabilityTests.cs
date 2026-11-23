using Beacon.Core.Services;

namespace Beacon.Connections.Tests;

/// <summary>
/// SET-UX-05 类型级数据能力声明钉板（2026-11-24 原型落地批2）：每个已注册类型的 DataKinds
/// 必须与其 Provider 稳定产出的数据形状一致——能力契约是样式推荐/置灰原因的唯一真源，
/// 声明漂移=推荐面说谎（要么推荐了渲染不出的样式，要么把能用的置灰），这里逐类型钉死防退化。
/// 口径：progress=state.Progress 主值百分比；windows=payload 带标签并列百分比键；amount=余额金额；
/// status=纯状态读数。percent 选择器接通 payload 前（SET-UX-07 待办）：payload 有百分比键但
/// state.Progress 恒 null 的类型只声明 windows 不声明 progress——不给虚假的确定进度。
/// </summary>
public sealed class WidgetCapabilityTests
{
    [Fact]
    public void ProgressOnlyTypes_MatchProviderTruth()
    {
        // state.Progress 持续产出的八类（单窗口/无并列窗口）
        Assert.True(ArkWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.ProgressOnly), "ark.usage");
        Assert.True(BigModelWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.ProgressOnly), "bigmodel.usage");
        Assert.True(MiMoWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.ProgressOnly), "mimo.usage");
        Assert.True(QwenWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.ProgressOnly), "qwen.usage");
        Assert.True(SenseNovaWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.ProgressOnly), "sensenova.tokenplan");
        Assert.True(OpenCodeWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.ProgressOnly), "opencode.usage");
        Assert.True(HttpWidgetDescriptors.Quota.DataKinds.SetEquals(DisplayTemplateKinds.ProgressOnly), "http.quota");
    }

    [Fact]
    public void ProgressAndWindowsType_MatchesProviderTruth()
    {
        // kimi：state.Progress 最差窗主值 + 5h/周/套餐窗口并列读数
        Assert.True(KimiWidgetDescriptors.Coding.DataKinds.SetEquals(DisplayTemplateKinds.ProgressAndWindows), "kimi.coding");
    }

    [Fact]
    public void WindowsOnlyTypes_MatchProviderTruth()
    {
        // payload 有带标签百分比、state.Progress 恒 null：07 前不声明 progress（不给虚假进度）
        Assert.True(CopilotWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.WindowsOnly), "copilot.usage");
        Assert.True(MachineWidgetDescriptors.Local.DataKinds.SetEquals(DisplayTemplateKinds.WindowsOnly), "machine.local");
    }

    [Fact]
    public void AmountOnlyTypes_MatchProviderTruth()
    {
        // 只有余额金额、无任何百分比：进度样式须置灰并给原因
        Assert.True(DeepSeekWidgetDescriptors.Balance.DataKinds.SetEquals(DisplayTemplateKinds.AmountOnly), "deepseek.balance");
        Assert.True(MoonshotWidgetDescriptors.Balance.DataKinds.SetEquals(DisplayTemplateKinds.AmountOnly), "moonshot.balance");
    }

    [Fact]
    public void StatusOnlyTypes_MatchProviderTruth()
    {
        Assert.True(GitHubWidgetDescriptors.PullRequests.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "github.pull_requests");
        Assert.True(GitHubWidgetDescriptors.ActionsRuns.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "github.actions.runs");
        Assert.True(JenkinsWidgetDescriptors.Build.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "jenkins.build");
        Assert.True(HttpWidgetDescriptors.Status.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "http.status");
        Assert.True(MachineWidgetDescriptors.Net.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "machine.net");
        Assert.True(GitWorkspaceDescriptors.Git.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "machine.git");
        Assert.True(HeraldWidgetDescriptors.Incidents.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "herald.incidents");
        Assert.True(HeraldWidgetDescriptors.Feed.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "herald.feed");
        Assert.True(HeraldWidgetDescriptors.Deliveries.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "herald.deliveries");
        Assert.True(ClaudeWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "claude.usage");
        Assert.True(CodexWidgetDescriptors.Usage.DataKinds.SetEquals(DisplayTemplateKinds.StatusOnly), "codex.usage");
    }

    [Fact]
    public void EveryDescriptor_CarriesADeclaredCapability()
    {
        // 全量闭包：App 注册表（AllWidgetDescriptors 同源集合）无「漏声明」的描述符——
        // 漏声明走旧口径回退，推荐面退化成当前值推断（05 契约要求全量声明）。
        var all = new List<Beacon.Core.Models.WidgetTypeDescriptor>();
        foreach (var collection in new[]
        {
            GitHubWidgetDescriptors.All,
            JenkinsWidgetDescriptors.All,
            MachineWidgetDescriptors.All,
            GitWorkspaceDescriptors.All,
            HeraldWidgetDescriptors.All,
            HttpWidgetDescriptors.All,
            BigModelWidgetDescriptors.All,
            ArkWidgetDescriptors.All,
            ClaudeWidgetDescriptors.All,
            DeepSeekWidgetDescriptors.All,
            KimiWidgetDescriptors.All,
            MoonshotWidgetDescriptors.All,
            MiMoWidgetDescriptors.All,
            QwenWidgetDescriptors.All,
            SenseNovaWidgetDescriptors.All,
            CodexWidgetDescriptors.All,
            CopilotWidgetDescriptors.All,
            OpenCodeWidgetDescriptors.All,
        })
        {
            all.AddRange(collection);
        }
        var missing = all.Where(d => d.DataKinds.Count == 0).Select(d => d.Type).ToArray();
        Assert.Empty(missing);
    }
}
