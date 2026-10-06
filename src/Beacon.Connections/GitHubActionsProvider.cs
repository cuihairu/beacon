using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>
/// github.actions.runs（B-303）：workflow×branch 最新 run → LifecycleState + Severity。
/// 映射表：Failed/StartupFailure→Error，TimedOut→Critical，Cancelled→Warning，
/// Running→Info（超 15 分钟→Warning），Queued→Queued/Info，Success/Skipped→Success。
/// </summary>
public sealed class GitHubActionsProvider : IWidgetProvider
{
    /// <summary>Running 超过此时长视为疑似卡住 → Warning（RFC B-303）。</summary>
    public static readonly TimeSpan StuckRunningThreshold = TimeSpan.FromMinutes(15);

    public WidgetTypeDescriptor Descriptor => GitHubWidgetDescriptors.ActionsRuns;

    public async Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var repo = GitHubPullRequestsProvider.RequireConfig(widget, "repo");
        var workflow = GitHubPullRequestsProvider.RequireConfig(widget, "workflow");
        var branch = widget.Config.GetValueOrDefault("branch");
        var client = GitHubClientCache.Get(connection, context);

        var query = "per_page=1" + (string.IsNullOrWhiteSpace(branch) ? "" : $"&branch={Uri.EscapeDataString(branch.Trim())}");
        var response = await client
            .GetAsync($"/repos/{repo}/actions/workflows/{Uri.EscapeDataString(workflow.Trim())}/runs", query, cancellationToken)
            .ConfigureAwait(false);
        if (response.NotModified)
        {
            return null;
        }

        var run = ParseLatestRun(response.Body!);
        TimeSpan? elapsed = run.StartedAt is { } started
            ? (run.CompletedAt ?? DateTimeOffset.UtcNow) - started
            : null;
        var (lifecycle, severity) = MapRun(run.Status, run.Conclusion, elapsed);

        return new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = lifecycle,
            Progress = null, // GitHub API 无进度百分比 → UI 按 Lifecycle 显示不确定进度
            Summary = Describe(run, workflow, elapsed),
            DetailUrl = run.HtmlUrl,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["repo"] = repo,
                ["workflow"] = workflow,
                ["run_id"] = run.Id.ToString(),
                ["branch"] = run.Branch ?? branch ?? "",
                ["conclusion"] = run.Conclusion ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>状态映射（表驱动，纯函数供单测）。elapsed 仅 Running 分支消费。</summary>
    internal static (LifecycleState Lifecycle, Severity Severity) MapRun(string status, string? conclusion, TimeSpan? elapsed)
        => status switch
        {
            "queued" or "waiting" or "pending" => (LifecycleState.Queued, Severity.Info),
            "in_progress" => (LifecycleState.Running, elapsed is { } e && e > StuckRunningThreshold ? Severity.Warning : Severity.Info),
            "completed" => conclusion switch
            {
                "success" => (LifecycleState.Success, Severity.Success),
                "skipped" => (LifecycleState.Skipped, Severity.Success),
                "neutral" => (LifecycleState.Success, Severity.Info),
                "cancelled" or "canceled" => (LifecycleState.Cancelled, Severity.Warning),
                "timed_out" => (LifecycleState.Failed, Severity.Critical),
                "failure" or "startup_failure" or "action_required" or null => (LifecycleState.Failed, Severity.Error),
                _ => (LifecycleState.Unknown, Severity.Warning),
            },
            _ => (LifecycleState.Unknown, Severity.Warning),
        };

    private static string Describe(RunSnapshot run, string workflow, TimeSpan? elapsed)
    {
        var phase = run.Status switch
        {
            "completed" => run.Conclusion ?? "completed",
            _ => run.Status,
        };
        return elapsed is { } e && run.Status == "completed"
            ? $"{workflow} · {phase} · {FormatDuration(e)}"
            : $"{workflow} · {phase}";
    }

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalMinutes >= 1 ? $"{(int)duration.TotalMinutes}m{duration.Seconds:00}s" : $"{duration.Seconds}s";

    private static RunSnapshot ParseLatestRun(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var runs = document.RootElement.GetProperty("workflow_runs");
            if (runs.GetArrayLength() == 0)
            {
                return RunSnapshot.None;
            }
            var run = runs[0];
            return new RunSnapshot(
                Id: run.GetProperty("id").GetInt64(),
                Status: run.GetProperty("status").GetString() ?? "unknown",
                Conclusion: run.TryGetProperty("conclusion", out var conclusion) && conclusion.ValueKind == JsonValueKind.String
                    ? conclusion.GetString()
                    : null,
                Branch: run.TryGetProperty("head_branch", out var branch) && branch.ValueKind == JsonValueKind.String
                    ? branch.GetString()
                    : null,
                HtmlUrl: run.TryGetProperty("html_url", out var url) && url.ValueKind == JsonValueKind.String
                    ? url.GetString()
                    : null,
                StartedAt: TryGetTime(run, "run_started_at") ?? TryGetTime(run, "created_at"),
                CompletedAt: TryGetTime(run, "updated_at"));
        }
        catch (JsonException exception)
        {
            throw new ConnectionException($"GitHub Actions 响应解析失败：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
        catch (KeyNotFoundException exception)
        {
            throw new ConnectionException($"GitHub Actions 响应缺少字段：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
    }

    private static DateTimeOffset? TryGetTime(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    internal sealed record RunSnapshot(
        long Id,
        string Status,
        string? Conclusion,
        string? Branch,
        string? HtmlUrl,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt)
    {
        public static readonly RunSnapshot None = new(0, "unknown", null, null, null, null, null);
    }
}
