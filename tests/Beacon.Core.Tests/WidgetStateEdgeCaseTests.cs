using Beacon.Core.Models;

namespace Beacon.Core.Tests;

public sealed class WidgetStateEdgeCaseTests
{
    [Fact]
    public void Equals_NullAndDifferentPayloadAreNotEqual()
    {
        var left = State(new Dictionary<string, string> { ["Key"] = "Value" });

        Assert.False(left.Equals(null));
        Assert.False(left.Equals(State(new Dictionary<string, string> { ["Key"] = "Different" })));
        Assert.False(left.Equals(State(new Dictionary<string, string>())));
    }

    [Fact]
    public void GetHashCode_IncludesAllContentFields()
    {
        var baseState = State(new Dictionary<string, string>());

        var emptyPayload = new Dictionary<string, string>();
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, widgetId: "other").GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, widgetType: "other.type").GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, connectionId: "other").GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, severity: Severity.Warning).GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, summary: "other").GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, detailUrl: "https://example.test").GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, progress: 0.25).GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, lifecycle: LifecycleState.Running).GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, fetchedAt: DateTimeOffset.UnixEpoch).GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, isStale: true).GetHashCode());
        Assert.NotEqual(baseState.GetHashCode(), State(emptyPayload, connectionHealthy: false).GetHashCode());
    }

    [Fact]
    public void Equals_HasReferenceEqualityShortcut()
    {
        var state = State(new Dictionary<string, string>());

        Assert.True(state.Equals(state));
    }

    [Fact]
    public void Equals_DictionaryMissingKey_IsNotEqual()
    {
        var left = State(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
        var right = State(new Dictionary<string, string> { ["a"] = "1" });

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void GetHashCode_UsesDifferentPayloadCount()
    {
        var left = State(new Dictionary<string, string> { ["a"] = "1" });
        var right = State(new Dictionary<string, string>());

        Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
    }

    private static WidgetState State(
        IReadOnlyDictionary<string, string> payload,
        string widgetId = "w-1",
        string widgetType = "test.type",
        string connectionId = "conn-1",
        Severity severity = Severity.Success,
        string summary = "OK",
        string? detailUrl = null,
        double? progress = null,
        LifecycleState? lifecycle = null,
        DateTimeOffset? fetchedAt = null,
        bool isStale = false,
        bool connectionHealthy = true)
        => new()
        {
            WidgetId = widgetId,
            WidgetType = widgetType,
            ConnectionId = connectionId,
            Severity = severity,
            Summary = summary,
            DetailUrl = detailUrl,
            Progress = progress,
            Lifecycle = lifecycle,
            Payload = payload,
            FetchedAt = fetchedAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsStale = isStale,
            ConnectionHealthy = connectionHealthy,
        };
}
