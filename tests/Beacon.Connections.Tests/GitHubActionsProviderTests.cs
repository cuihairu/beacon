using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>B-303 验收：状态映射表驱动单测 + 夹具端到端。</summary>
public sealed class GitHubActionsProviderTests
{
    public static TheoryData<string, string?, TimeSpan?, LifecycleState, Severity> RunMappingCases => new()
    {
        // (status, conclusion, elapsed) → (lifecycle, severity)
        { "queued", null, null, LifecycleState.Queued, Severity.Info },
        { "in_progress", null, TimeSpan.FromMinutes(5), LifecycleState.Running, Severity.Info },
        { "in_progress", null, TimeSpan.FromMinutes(16), LifecycleState.Running, Severity.Warning }, // 超 15min
        { "completed", "success", TimeSpan.FromMinutes(2), LifecycleState.Success, Severity.Success },
        { "completed", "failure", TimeSpan.FromMinutes(3), LifecycleState.Failed, Severity.Error },
        { "completed", "startup_failure", null, LifecycleState.Failed, Severity.Error },
        { "completed", "timed_out", TimeSpan.FromHours(1), LifecycleState.Failed, Severity.Critical },
        { "completed", "cancelled", null, LifecycleState.Cancelled, Severity.Warning },
        { "completed", "skipped", null, LifecycleState.Skipped, Severity.Success },
        { "completed", "neutral", null, LifecycleState.Success, Severity.Info },
        { "completed", null, null, LifecycleState.Failed, Severity.Error }, // 结论缺失按失败处理
        { "whatever", null, null, LifecycleState.Unknown, Severity.Warning },
    };

    [Theory]
    [MemberData(nameof(RunMappingCases))]
    public void MapRun_TableDriven(
        string status,
        string? conclusion,
        TimeSpan? elapsed,
        LifecycleState expectedLifecycle,
        Severity expectedSeverity)
    {
        var (lifecycle, severity) = GitHubActionsProvider.MapRun(status, conclusion, elapsed);

        Assert.Equal(expectedLifecycle, lifecycle);
        Assert.Equal(expectedSeverity, severity);
    }

    private static readonly ConnectionConfig Connection = new()
    {
        Id = "gh-main",
        Type = "github",
        Endpoint = "https://api.github.test/",
    };

    private static WidgetConfig Widget() => new()
    {
        Id = "w-ci",
        Type = GitHubWidgetDescriptors.ActionsRunsType,
        ConnectionId = Connection.Id,
        RefreshTier = RefreshTiers.Ci,
        Config = new Dictionary<string, string>
        {
            ["repo"] = "owner/repo",
            ["workflow"] = "ci.yml",
            ["branch"] = "main",
        },
    };

    private static async Task<WidgetState?> GetStateAsync(FakeHttpMessageHandler handler)
    {
        GitHubClientCache.SetForTests(Connection.Id, new GitHubApiClient(Connection.Endpoint, "pat", handler));
        try
        {
            var provider = new GitHubActionsProvider();
            return await provider.GetStateAsync(Widget(), Connection, new ConnectionContext { Secrets = new SecretStoreStub() }, CancellationToken.None);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task LatestRun_MapsFailedRun_EndToEnd()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Fixtures.ActionsFailedRun);

        var state = await GetStateAsync(handler);

        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        Assert.Equal(Severity.Error, state.Severity);
        Assert.Equal("ci.yml · failure · 3m12s", state.Summary);
        Assert.Equal("987654", state.Payload["run_id"]);
        Assert.Equal("main", state.Payload["branch"]);
        Assert.Equal("https://github.com/owner/repo/actions/runs/987654", state.DetailUrl);
        // 请求带 branch 过滤参数
        Assert.Contains("branch=main", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task NoRuns_YieldsUnknownState()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """{"total_count":0,"workflow_runs":[]}""");

        var state = await GetStateAsync(handler);

        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Unknown, state.Lifecycle);
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("ci.yml · unknown", state.Summary);
    }

    [Fact]
    public async Task LatestRun_Running_MapsInfo_NoDuration()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, RunsBody(111, "in_progress", null, DateTimeOffset.UtcNow.AddMinutes(-5), null));

        var state = await GetStateAsync(handler);

        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal("ci.yml · in_progress", state.Summary); // 运行中不拼时长，进度由 UI 按 Lifecycle 画不确定条
        Assert.Equal("111", state.Payload["run_id"]);
    }

    [Fact]
    public async Task LatestRun_StuckRunning_MapsWarning()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, RunsBody(112, "in_progress", null, DateTimeOffset.UtcNow.AddMinutes(-16), null));

        var state = await GetStateAsync(handler);

        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal(Severity.Warning, state.Severity); // 超 15 分钟疑似卡住
    }

    [Fact]
    public async Task LatestRun_Success_DurationFormatted()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, RunsBody(
            113, "completed", "success",
            new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 10, 2, 0, TimeSpan.Zero)));

        var state = await GetStateAsync(handler);

        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal(Severity.Success, state.Severity);
        Assert.Equal("ci.yml · success · 2m00s", state.Summary);
        Assert.Equal("success", state.Payload["conclusion"]);
    }

    [Fact]
    public async Task LatestRun_MalformedJson_ThrowsDegraded()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, "not json at all");

        var exception = await Assert.ThrowsAsync<ConnectionException>(() => GetStateAsync(handler));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
        Assert.Contains("Actions 响应解析失败", exception.Message);
    }

    /// <summary>以 JSON 序列化构造 runs 响应体（时间戳 ISO 8601，TryGetTime 可解析）。</summary>
    private static string RunsBody(long id, string status, string? conclusion, DateTimeOffset? startedAt, DateTimeOffset? updatedAt)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            total_count = 1,
            workflow_runs = new[]
            {
                new
                {
                    id,
                    status,
                    conclusion,
                    head_branch = "main",
                    html_url = $"https://github.com/owner/repo/actions/runs/{id}",
                    run_started_at = startedAt,
                    updated_at = updatedAt,
                },
            },
        });

    [Fact]
    public async Task NotModified_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, Fixtures.ActionsFailedRun);
        handler.Enqueue(HttpStatusCode.NotModified);

        Assert.NotNull(await GetStateAsync(handler));
        Assert.Null(await GetStateAsync(handler));
    }
}
