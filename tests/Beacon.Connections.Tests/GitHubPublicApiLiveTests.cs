using System.Net;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// Items 2–4（2026-10-18 用户令「改用可造数据验证，不再等凭据」）：
///   Item 2 — PR 计数走 GitHub 公共只读 API（本仓 cuihairu/beacon 自身），匿名无 PAT。
///   Item 3 — workflow 状态走同一公共 API（本仓 ci.yml）。
///   Item 4 — 运行中→终态全程：workflow_dispatch 匿名 401（需鉴权、gh 不可用），改用可造数据
///            （桩先出 in_progress 再出 completed）驱动 provider，证明 running→terminal 渲染翻转。
/// Items 2/3 是真实网络的 opt-in 集成测试：默认（未设 BEACON_LIVE_API）提前返回不发请求，
/// 离线 CI 恒绿；本机设 BEACON_LIVE_API=1 跑真实公共 API 取证。Item 4 恒跑（桩，无网络）。
/// </summary>
public sealed class GitHubPublicApiLiveTests
{
    private const string Repo = "cuihairu/beacon";

    private static ConnectionConfig Connection() => new()
    {
        Id = "gh-live",
        Type = "github",
        Endpoint = "https://api.github.com/",
        CredentialRef = "github:live", // 匿名：SecretStoreStub 空 → 无 Authorization 头
    };

    private static WidgetConfig Widget(string type, Dictionary<string, string> config) => new()
    {
        Id = "w-live",
        Type = type,
        ConnectionId = Connection().Id,
        RefreshTier = RefreshTiers.Pr,
        Config = config,
    };

    private static bool LiveEnabled =>
        Environment.GetEnvironmentVariable("BEACON_LIVE_API") == "1";

    private static async Task<WidgetState?> FetchAsync(WidgetConfig widget)
    {
        // 真实匿名客户端（token 空 → 无 Authorization 头）直连 api.github.com。
        GitHubClientCache.SetForTests(Connection().Id, new GitHubApiClient(Connection().Endpoint, ""));
        try
        {
            IWidgetProvider provider = widget.Type == GitHubWidgetDescriptors.PullRequestsType
                ? new GitHubPullRequestsProvider()
                : new GitHubActionsProvider();
            return await provider.GetStateAsync(
                widget, Connection(), new ConnectionContext { Secrets = new SecretStoreStub() },
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }

    [Fact]
    public async Task Item2_PublicApi_PullRequestCount_RendersWithoutPat()
    {
        if (!LiveEnabled) return; // opt-in：离线 CI 跳过（无 Assert.Skip，xunit 2.9）

        var state = await FetchAsync(Widget(
            GitHubWidgetDescriptors.PullRequestsType,
            new Dictionary<string, string> { ["repo"] = Repo })).ConfigureAwait(false);

        Assert.NotNull(state);
        // 公共只读 API 匿名可达：能解析出计数即证明 PR 计数渲染链路不依赖 PAT。
        Assert.True(state.Payload.TryGetValue("open_count", out var count));
        Assert.True(int.TryParse(count, out _), $"open_count 非数字：{count}");
        Assert.StartsWith($"https://github.com/{Repo}/pulls", state.DetailUrl);
    }

    [Fact]
    public async Task Item3_PublicApi_WorkflowStatus_RendersWithoutPat()
    {
        if (!LiveEnabled) return; // opt-in：离线 CI 跳过

        var state = await FetchAsync(Widget(
            GitHubWidgetDescriptors.ActionsRunsType,
            new Dictionary<string, string> { ["repo"] = Repo, ["workflow"] = "ci.yml" }))
            .ConfigureAwait(false);

        Assert.NotNull(state);
        // 公共 API 匿名返回最新 run 的 status/conclusion → provider 映射出 Lifecycle。
        Assert.NotNull(state.Lifecycle);
        Assert.False(string.IsNullOrWhiteSpace(state.Summary));
        Assert.StartsWith("https://github.com/", state.DetailUrl);
    }

    [Fact]
    public async Task Item4_RunningToTerminal_FlipRenders()
    {
        // 可造数据：workflow_dispatch 匿名 401（需鉴权），改用桩先出 in_progress 再出 completed，
        // 观察 provider 从 Running→Success 的渲染翻转（Item 4 的「运行中→终态全程」）。
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, """
            {"total_count":1,"workflow_runs":[{"id":1,"status":"in_progress","conclusion":null,
             "head_branch":"main","html_url":"https://github.com/cuihairu/beacon/actions/runs/1",
             "created_at":"2026-01-01T10:00:00Z","run_started_at":"2026-01-01T10:00:00Z","updated_at":"2026-01-01T10:01:00Z"}]}
            """);
        handler.Enqueue(HttpStatusCode.OK, """
            {"total_count":1,"workflow_runs":[{"id":1,"status":"completed","conclusion":"success",
             "head_branch":"main","html_url":"https://github.com/cuihairu/beacon/actions/runs/1",
             "created_at":"2026-01-01T10:00:00Z","run_started_at":"2026-01-01T10:00:00Z","updated_at":"2026-01-01T10:03:00Z"}]}
            """);
        GitHubClientCache.SetForTests(
            Connection().Id, new GitHubApiClient(Connection().Endpoint, "pat", handler));
        try
        {
            var widget = Widget(
                GitHubWidgetDescriptors.ActionsRunsType,
                new Dictionary<string, string> { ["repo"] = Repo, ["workflow"] = "ci.yml" });
            var provider = new GitHubActionsProvider();
            var ctx = new ConnectionContext { Secrets = new SecretStoreStub() };

            var running = await provider.GetStateAsync(widget, Connection(), ctx, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.Equal(LifecycleState.Running, running!.Lifecycle); // 运行中渲染

            var terminal = await provider.GetStateAsync(widget, Connection(), ctx, CancellationToken.None)
                .ConfigureAwait(false);
            Assert.Equal(LifecycleState.Success, terminal!.Lifecycle); // 终态渲染（翻转全程）
        }
        finally
        {
            GitHubClientCache.ClearForTests();
        }
    }
}
