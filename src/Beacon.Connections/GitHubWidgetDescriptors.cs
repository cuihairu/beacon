using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>GitHub Widget 类型元数据注册表（B-304）：驱动 Settings 向导与 L0 准入。
/// 新增 Provider 只需在此追加描述符——Core 零改动（开闭性）。</summary>
public static class GitHubWidgetDescriptors
{
    public const string PullRequestsType = "github.pull_requests";
    public const string ActionsRunsType = "github.actions.runs";

    public static WidgetTypeDescriptor PullRequests { get; } = new()
    {
        Type = PullRequestsType,
        DisplayName = "GitHub Pull Requests",
        PinSupported = true,
        SuggestedTier = RefreshTiers.Pr,
        Fields =
        [
            new WidgetFieldDescriptor("repo", "仓库", Required: true, Placeholder: "owner/repo"),
            new WidgetFieldDescriptor("warnOnReviewRequested", "有待你 review 时告警", Placeholder: "true/false"),
            new WidgetFieldDescriptor("warnOnRedCi", "PR 的 CI 红灯时计入告警", Placeholder: "true/false（默认关：每 PR 一请求）"),
        ],
    };

    public static WidgetTypeDescriptor ActionsRuns { get; } = new()
    {
        Type = ActionsRunsType,
        DisplayName = "GitHub Actions Runs",
        PinSupported = true,
        SuggestedTier = RefreshTiers.Ci,
        Fields =
        [
            new WidgetFieldDescriptor("repo", "仓库", Required: true, Placeholder: "owner/repo"),
            new WidgetFieldDescriptor("workflow", "Workflow 文件", Required: true, Placeholder: "ci.yml"),
            new WidgetFieldDescriptor("branch", "分支", Placeholder: "默认分支"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [PullRequests, ActionsRuns];
}
