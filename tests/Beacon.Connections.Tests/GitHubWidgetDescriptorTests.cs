using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-304 验收：描述符完整；新增 Provider 零改动 Core（开闭性——描述符/Provider 均在 Connections 程序集）。</summary>
public sealed class GitHubWidgetDescriptorTests
{
    [Fact]
    public void All_ContainsBothTypes()
    {
        Assert.Equal(
            [GitHubWidgetDescriptors.PullRequestsType, GitHubWidgetDescriptors.ActionsRunsType],
            [.. GitHubWidgetDescriptors.All.Select(d => d.Type)]);
    }

    [Fact]
    public void BothTypes_ArePinSupported_WithSuggestedTiers()
    {
        Assert.True(GitHubWidgetDescriptors.PullRequests.PinSupported);
        Assert.Equal(RefreshTiers.Pr, GitHubWidgetDescriptors.PullRequests.SuggestedTier);

        Assert.True(GitHubWidgetDescriptors.ActionsRuns.PinSupported);
        Assert.Equal(RefreshTiers.Ci, GitHubWidgetDescriptors.ActionsRuns.SuggestedTier);
    }

    [Fact]
    public void RequiredFields_AreMarked()
    {
        var pullFields = GitHubWidgetDescriptors.PullRequests.Fields;
        Assert.Contains(pullFields, f => f.Key == "repo" && f.Required);

        var actionsFields = GitHubWidgetDescriptors.ActionsRuns.Fields;
        Assert.Contains(actionsFields, f => f.Key == "repo" && f.Required);
        Assert.Contains(actionsFields, f => f.Key == "workflow" && f.Required);
    }
}
