using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-302 验收：夹具单测——计数/Severity 映射正确；304 → null。</summary>
public sealed class GitHubPullRequestsProviderTests
{
    private static readonly ConnectionConfig Connection = new()
    {
        Id = "gh-main",
        Type = "github",
        Endpoint = "https://api.github.test/",
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-pr",
        Type = GitHubWidgetDescriptors.PullRequestsType,
        ConnectionId = Connection.Id,
        RefreshTier = RefreshTiers.Pr,
        Config = config ?? new Dictionary<string, string> { ["repo"] = "owner/repo" },
    };

    private static async Task<WidgetState?> GetStateAsync(FakeHttpMessageHandler handler, WidgetConfig widget)
    {
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubPullRequestsProvider();
            return await provider.GetStateAsync(widget, Connection, new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task Count_And_WarningMapping_WhenReviewRequested()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Fixtures.PullRequests);

        var state = await GetStateAsync(handler, Widget());

        Assert.NotNull(state);
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("3 open PRs · 1 review needed", state.Summary);
        Assert.Equal("3", state.Payload["open_count"]);
        Assert.Equal("11", state.Payload["flagged_numbers"]);
        Assert.Equal("https://github.com/owner/repo/pulls", state.DetailUrl);
    }

    [Fact]
    public async Task CleanPulls_MapToInfo()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """[{"number": 7, "draft": false, "requested_reviewers": []}]""");

        var state = await GetStateAsync(handler, Widget());

        Assert.NotNull(state);
        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal("1 open PR", state.Summary);
    }

    [Fact]
    public async Task EmptyRepo_MapsToSuccess()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "[]");

        var state = await GetStateAsync(handler, Widget());

        Assert.NotNull(state);
        Assert.Equal(Severity.Success, state.Severity);
        Assert.Equal("No open PRs", state.Summary);
    }

    [Fact]
    public async Task WarnOnReviewRequestedDisabled_CleanList_MapsToInfo()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Fixtures.PullRequests);

        var state = await GetStateAsync(handler, Widget(new Dictionary<string, string>
        {
            ["repo"] = "owner/repo",
            ["warnOnReviewRequested"] = "false",
        }));

        Assert.NotNull(state);
        Assert.Equal(Severity.Info, state.Severity);
    }

    [Fact]
    public async Task NotModified_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Fixtures.PullRequests);
        handler.Enqueue(HttpStatusCode.NotModified);

        var first = await GetStateAsync(handler, Widget());
        var second = await GetStateAsync(handler, Widget());

        Assert.NotNull(first);
        Assert.Null(second); // 304：宿主跳过发布
    }

    [Fact]
    public async Task MissingRepoConfig_ThrowsDegraded()
    {
        var handler = new FakeHttpMessageHandler();
        var widget = Widget(new Dictionary<string, string>());

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => GetStateAsync(handler, widget));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }
}
