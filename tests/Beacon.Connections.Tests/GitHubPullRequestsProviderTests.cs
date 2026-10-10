using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-302 验收：夹具单测——计数/Severity 映射正确；304 → null；红 CI 判据（有状态：终态复用、pending 续探、304 期间翻转可回报）。</summary>
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
    public async Task SinglePull_Flagged_SingularSummary()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """[{"number": 9, "draft": false, "requested_reviewers": [{ "login": "bob" }]}]""");

        var state = await GetStateAsync(handler, Widget());

        Assert.NotNull(state);
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("1 open PR · 1 review needed", state.Summary); // 单数分支（复数文案覆盖在 Count_And_WarningMapping）
        Assert.Equal("1", state.Payload["flagged_count"]);
        Assert.Equal("9", state.Payload["flagged_numbers"]);
    }

    [Fact]
    public async Task DraftWithRequestedReviewer_Flagged()
    {
        // 草稿不豁免：规格只有「有 review-requested → Warning」一条判据
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """[{"number": 5, "draft": true, "requested_reviewers": [{ "login": "carol" }]}]""");

        var state = await GetStateAsync(handler, Widget());

        Assert.NotNull(state);
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("5", state.Payload["flagged_numbers"]);
    }

    [Fact]
    public async Task MissingReviewerField_CountsZero()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """[{"number": 3, "draft": false}]""");

        var state = await GetStateAsync(handler, Widget());

        Assert.NotNull(state);
        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal("1 open PR", state.Summary);
        Assert.Equal("0", state.Payload["flagged_count"]);
    }

    [Fact]
    public async Task MalformedJson_ThrowsDegraded()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "<html>gateway error page</html>");

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => GetStateAsync(handler, Widget()));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("PR 响应解析失败", exception.Message);
    }

    [Fact]
    public async Task MissingRepoConfig_ThrowsDegraded()
    {
        var handler = new FakeHttpMessageHandler();
        var widget = Widget(new Dictionary<string, string>());

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => GetStateAsync(handler, widget));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // ---- 红 CI 判据（warnOnRedCi，combined status，有状态）----

    private static WidgetConfig RedCiWidget() => Widget(new Dictionary<string, string>
    {
        ["repo"] = "owner/repo",
        ["warnOnRedCi"] = "true",
    });

    private static string PullsBody(params object[] pulls) => System.Text.Json.JsonSerializer.Serialize(pulls);

    private static object Pull(int number, string sha) => new { number, draft = false, head = new { sha } };

    private static string StatusBody(string state) => $"{{\"state\":\"{state}\"}}";

    /// <summary>多轮刷新共用同一 provider 实例——红 CI 记忆按实例存续。</summary>
    private sealed class RedCiFixture
    {
        public FakeHttpMessageHandler Handler { get; } = new();
        private readonly GitHubPullRequestsProvider _provider = new();

        public async Task<WidgetState?> NextAsync()
        {
            GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", Handler));
            try
            {
                return await _provider.GetStateAsync(RedCiWidget(), Connection, new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None);
            }
            finally
            {
                GitHubClientCache.ClearForTests();
            }
        }
    }

    [Fact]
    public async Task RedCiDisabled_MakesNoStatusRequests()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """[{"number": 9, "draft": false, "head": {"sha": "abc"}}]""");

        var state = await GetStateAsync(handler, Widget()); // 默认关

        Assert.NotNull(state);
        Assert.Equal("1 open PR", state.Summary);
        Assert.Equal("0", state.Payload["red_ci_count"]);
        Assert.Single(handler.Requests); // 只有 pulls 一发，不逐 PR 探测
    }

    [Fact]
    public async Task RedCi_FreshList_ProbesNewSha_FailureFlagged()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        handler.Enqueue(HttpStatusCode.OK, StatusBody("failure"));

        var state = await GetStateAsync(handler, RedCiWidget());

        Assert.NotNull(state);
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("1 open PR · 1 red ci", state.Summary);
        Assert.Equal("9", state.Payload["red_ci_numbers"]);
        Assert.Equal(2, handler.Requests.Count); // pulls + combined status
        Assert.Contains("/commits/abc/status", handler.Requests[1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task RedCi_TerminalStateReused_NoRequery()
    {
        // 终态跨刷新复用：同 sha 二轮只打 pulls，不重查 combined status（稳态零额外请求）
        var fixture = new RedCiFixture();
        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("success"));
        var first = await fixture.NextAsync();

        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        var second = await fixture.NextAsync();

        Assert.Equal(Severity.Info, first!.Severity);
        Assert.NotNull(second);
        Assert.Equal(Severity.Info, second.Severity);
        Assert.Equal(3, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task RedCi_PendingRequeried_FlipsToFailure()
    {
        var fixture = new RedCiFixture();
        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("pending"));
        var first = await fixture.NextAsync();

        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("failure"));
        var second = await fixture.NextAsync();

        Assert.Equal(Severity.Info, first!.Severity); // pending 不算红
        Assert.Equal("1 open PR", first.Summary);
        Assert.NotNull(second);
        Assert.Equal(Severity.Warning, second.Severity);
        Assert.Equal("1 open PR · 1 red ci", second.Summary);
        Assert.Equal(4, fixture.Handler.Requests.Count); // pending 非终态，二轮重查
    }

    [Fact]
    public async Task RedCi_OnNotModified_PendingFlipStillReported()
    {
        // 304 核心价值：pulls 口 304 期间 pending→failure 翻转仍能按记忆续探回报
        var fixture = new RedCiFixture();
        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "aaa"), Pull(7, "bbb")));
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("success")); // #9 终态
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("pending")); // #7 非终态
        var first = await fixture.NextAsync();

        fixture.Handler.Enqueue(HttpStatusCode.NotModified); // pulls 304
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("failure")); // 续探 #7 → 翻红
        var second = await fixture.NextAsync();

        Assert.Equal("2 open PRs", first!.Summary); // pending 不计红
        Assert.NotNull(second);
        Assert.Equal(Severity.Warning, second.Severity);
        Assert.Equal("2 open PRs · 1 red ci", second.Summary); // 计数取记忆
        Assert.Equal("7", second.Payload["red_ci_numbers"]);
        Assert.Equal(5, fixture.Handler.Requests.Count); // pulls、#9、#7、pulls(304)、#7 续探
        Assert.Contains("/commits/bbb/status", fixture.Handler.Requests[^1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task RedCi_OnNotModified_AllTerminal_ReturnsNull()
    {
        var fixture = new RedCiFixture();
        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("success"));
        var first = await fixture.NextAsync();

        fixture.Handler.Enqueue(HttpStatusCode.NotModified);
        var second = await fixture.NextAsync();

        Assert.NotNull(first);
        Assert.Null(second); // 全终态：304 后无翻转会话，宿主沿用缓存
        Assert.Equal(3, fixture.Handler.Requests.Count); // 304 后无续探请求
    }

    [Fact]
    public async Task RedCi_ShaChange_Requeries()
    {
        var fixture = new RedCiFixture();
        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("success"));
        var first = await fixture.NextAsync();

        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "def"))); // 新 push 换 sha
        fixture.Handler.Enqueue(HttpStatusCode.OK, StatusBody("failure"));
        var second = await fixture.NextAsync();

        Assert.Equal(Severity.Info, first!.Severity);
        Assert.NotNull(second);
        Assert.Equal(Severity.Warning, second.Severity);
        Assert.Contains("/commits/def/status", fixture.Handler.Requests[^1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task RedCi_MalformedStatusJson_ThrowsDegraded()
    {
        var fixture = new RedCiFixture();
        fixture.Handler.Enqueue(HttpStatusCode.OK, PullsBody(Pull(9, "abc")));
        fixture.Handler.Enqueue(HttpStatusCode.OK, "<html>gateway error page</html>");

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => fixture.NextAsync());

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("combined status 解析失败", exception.Message);
    }
}
