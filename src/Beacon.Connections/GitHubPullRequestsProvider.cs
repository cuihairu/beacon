using System.Collections.Concurrent;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>
/// github.pull_requests（B-302）：指定仓库 open PR 数与列表。
/// Severity：0 → Success；有 review-requested（可配置）或红 CI（warnOnRedCi）→ Warning；其余 >0 → Info。
/// 红 CI 有状态设计：按 PR 记忆 (head sha → combined status state)，终态跨刷新复用不重查，
/// 仅探测 sha 变化或未到终态（pending 等）的 PR——稳态零额外请求；
/// pulls 口 304 期间 pending→failure 翻转仍能按记忆续探并回报，全终态时才返回 null。
/// 默认关（warnOnRedCi=false）：开启后首次全量每 PR 一请求，大仓库首刷代价高。
/// </summary>
public sealed class GitHubPullRequestsProvider : IWidgetProvider
{
    private static readonly HashSet<string> TerminalStates = new(StringComparer.OrdinalIgnoreCase) { "success", "failure" };

    /// <summary>每个 widget 一份 CI 记忆（provider 单例，多 widget 共存）。</summary>
    private readonly ConcurrentDictionary<string, WidgetCiMemory> _ciMemory = new();

    public WidgetTypeDescriptor Descriptor => GitHubWidgetDescriptors.PullRequests;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var repo = RequireConfig(widget, "repo");
        var warnOnReviewRequested = widget.Config.GetValueOrDefault("warnOnReviewRequested", "true") != "false";
        var warnOnRedCi = widget.Config.GetValueOrDefault("warnOnRedCi", "false") == "true";
        var client = GitHubClientCache.Get(connection, context);

        var response = await client
            .GetAsync($"/repos/{repo}/pulls", "state=open&per_page=100", cancellationToken)
            .ConfigureAwait(false);
        if (response.NotModified)
        {
            return warnOnRedCi
                ? await ProbeCachedAsync(widget, connection.Id, repo, client, cancellationToken).ConfigureAwait(false)
                : null;
        }

        var pulls = ParsePulls(response.Body!);
        var reviewFlagged = pulls
            .Where(p => warnOnReviewRequested && p.RequestedReviewers > 0)
            .Select(p => p.Number)
            .ToList();

        var redCi = new List<int>();
        if (warnOnRedCi)
        {
            var memory = _ciMemory.GetOrAdd(widget.Id, _ => new WidgetCiMemory());
            var next = new Dictionary<int, (string Sha, string State)>();
            foreach (var pull in pulls)
            {
                if (pull.HeadSha is null)
                {
                    continue; // 无 head sha（异常响应）跳过探测，不误报
                }

                string state;
                if (memory.Ci.TryGetValue(pull.Number, out var known)
                    && known.Sha == pull.HeadSha
                    && TerminalStates.Contains(known.State))
                {
                    state = known.State; // 终态跨刷新复用，零请求
                }
                else
                {
                    state = await ProbeStatusAsync(repo, pull.HeadSha, known.State, client, cancellationToken)
                        .ConfigureAwait(false);
                }

                next[pull.Number] = (pull.HeadSha, state);
                if (state == "failure")
                {
                    redCi.Add(pull.Number);
                }
            }

            memory.Ci = next; // 重建，剪掉已关闭/已合并的 PR
            memory.LastOpenCount = pulls.Count;
            memory.LastReviewFlagged = reviewFlagged;
        }

        return BuildState(widget, connection.Id, repo, pulls.Count, reviewFlagged, redCi);
    }

    /// <summary>pulls 304 时按记忆续探非终态 PR：有翻转才重建状态，否则维持 null（宿主沿用缓存）。</summary>
    private async Task<WidgetState?> ProbeCachedAsync(
        WidgetConfig widget,
        string connectionId,
        string repo,
        GitHubApiClient client,
        CancellationToken cancellationToken)
    {
        var memory = _ciMemory.GetOrAdd(widget.Id, _ => new WidgetCiMemory());
        if (memory.LastOpenCount == 0)
        {
            return null; // 上次就是空仓库，无可续
        }

        var flipped = false;
        foreach (var (number, known) in memory.Ci)
        {
            if (TerminalStates.Contains(known.State))
            {
                continue; // 终态不再查（成功/失败在 sha 不变时不会变）
            }

            var state = await ProbeStatusAsync(repo, known.Sha, known.State, client, cancellationToken)
                .ConfigureAwait(false);
            memory.Ci[number] = (known.Sha, state);
            flipped |= state != known.State;
        }

        if (!flipped)
        {
            return null;
        }

        var redCi = memory.Ci
            .Where(kv => kv.Value.State == "failure")
            .Select(kv => kv.Key)
            .ToList();
        return BuildState(widget, connectionId, repo, memory.LastOpenCount, memory.LastReviewFlagged, redCi);
    }

    /// <summary>打 combined status 口；304 = 与上次该 URL 响应一致 → 沿用记忆态。</summary>
    private static async Task<string> ProbeStatusAsync(
        string repo,
        string sha,
        string fallbackState,
        GitHubApiClient client,
        CancellationToken cancellationToken)
    {
        var response = await client
            .GetAsync($"/repos/{repo}/commits/{sha}/status", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.NotModified ? fallbackState : ParseStatusState(response.Body!);
    }

    internal static string RequireConfig(WidgetConfig widget, string key)
        => widget.Config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ConnectionException($"Widget {widget.Id} 缺少配置项 {key}。", ConnectionHealthState.Degraded);

    private static WidgetState BuildState(
        WidgetConfig widget,
        string connectionId,
        string repo,
        int openCount,
        IReadOnlyList<int> reviewFlagged,
        IReadOnlyList<int> redCi)
    {
        var severity = openCount == 0
            ? Severity.Success
            : reviewFlagged.Count > 0 || redCi.Count > 0 ? Severity.Warning : Severity.Info;

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = GitHubWidgetDescriptors.PullRequestsType,
            ConnectionId = connectionId,
            Severity = severity,
            Summary = BuildSummary(openCount, reviewFlagged.Count, redCi.Count),
            DetailUrl = $"https://github.com/{repo}/pulls",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["repo"] = repo,
                ["open_count"] = openCount.ToString(),
                ["flagged_count"] = reviewFlagged.Count.ToString(),
                ["flagged_numbers"] = string.Join(",", reviewFlagged),
                ["red_ci_count"] = redCi.Count.ToString(),
                ["red_ci_numbers"] = string.Join(",", redCi),
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    private static string BuildSummary(int openCount, int reviewCount, int redCiCount)
    {
        if (openCount == 0)
        {
            return "No open PRs";
        }

        var head = openCount == 1 ? "1 open PR" : $"{openCount} open PRs";
        var tails = new List<string>();
        if (reviewCount > 0)
        {
            tails.Add($"{reviewCount} review needed");
        }

        if (redCiCount > 0)
        {
            tails.Add($"{redCiCount} red ci");
        }

        return tails.Count == 0 ? head : $"{head} · {string.Join(" · ", tails)}";
    }

    private static string ParseStatusState(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("state", out var state)
                ? state.GetString() ?? "unknown"
                : "unknown";
        }
        catch (JsonException exception)
        {
            throw new ConnectionException(
                $"GitHub combined status 解析失败：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
    }

    private static IReadOnlyList<PullRequestSnapshot> ParsePulls(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return [.. document.RootElement.EnumerateArray().Select(pull => new PullRequestSnapshot(
                Number: pull.GetProperty("number").GetInt32(),
                Draft: pull.TryGetProperty("draft", out var draft) && draft.GetBoolean(),
                RequestedReviewers: pull.TryGetProperty("requested_reviewers", out var reviewers)
                    ? reviewers.GetArrayLength()
                    : 0,
                HeadSha: pull.TryGetProperty("head", out var head) && head.TryGetProperty("sha", out var sha)
                    ? sha.GetString()
                    : null))];
        }
        catch (JsonException exception)
        {
            throw new ConnectionException($"GitHub PR 响应解析失败：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
    }

    internal sealed record PullRequestSnapshot(int Number, bool Draft, int RequestedReviewers, string? HeadSha);

    /// <summary>红 CI 记忆：PR 号 → (head sha, combined state) + 上次计数（304 翻转时重建摘要用）。</summary>
    internal sealed class WidgetCiMemory
    {
        public Dictionary<int, (string Sha, string State)> Ci { get; set; } = [];
        public int LastOpenCount { get; set; }
        public List<int> LastReviewFlagged { get; set; } = [];
    }
}
