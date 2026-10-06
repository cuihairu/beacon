using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>
/// github.pull_requests（B-302）：指定仓库 open PR 数与列表。
/// Severity：0 → Success；有 review-requested（可配置）→ Warning；其余 >0 → Info。
/// </summary>
public sealed class GitHubPullRequestsProvider : IWidgetProvider
{
    public WidgetTypeDescriptor Descriptor => GitHubWidgetDescriptors.PullRequests;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var repo = RequireConfig(widget, "repo");
        var warnOnReviewRequested = widget.Config.GetValueOrDefault("warnOnReviewRequested", "true") != "false";
        var client = GitHubClientCache.Get(connection, context);

        var response = await client
            .GetAsync($"/repos/{repo}/pulls", "state=open&per_page=100", cancellationToken)
            .ConfigureAwait(false);
        if (response.NotModified)
        {
            return null;
        }

        var pulls = ParsePulls(response.Body!);
        var flagged = pulls.Where(p => warnOnReviewRequested && p.RequestedReviewers > 0).ToList();
        var severity = pulls.Count == 0
            ? Severity.Success
            : flagged.Count > 0 ? Severity.Warning : Severity.Info;

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Summary = pulls.Count switch
            {
                0 => "No open PRs",
                1 when flagged.Count > 0 => "1 open PR · 1 review needed",
                1 => "1 open PR",
                _ when flagged.Count > 0 => $"{pulls.Count} open PRs · {flagged.Count} review needed",
                _ => $"{pulls.Count} open PRs",
            },
            DetailUrl = $"https://github.com/{repo}/pulls",
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["repo"] = repo,
                ["open_count"] = pulls.Count.ToString(),
                ["flagged_count"] = flagged.Count.ToString(),
                ["flagged_numbers"] = string.Join(",", flagged.Select(p => p.Number)),
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    internal static string RequireConfig(WidgetConfig widget, string key)
        => widget.Config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ConnectionException($"Widget {widget.Id} 缺少配置项 {key}。", ConnectionHealthState.Degraded);

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
                    : 0))];
        }
        catch (JsonException exception)
        {
            throw new ConnectionException($"GitHub PR 响应解析失败：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
    }

    internal sealed record PullRequestSnapshot(int Number, bool Draft, int RequestedReviewers);
}
