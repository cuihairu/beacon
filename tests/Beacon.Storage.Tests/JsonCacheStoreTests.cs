using Beacon.Core.Models;

namespace Beacon.Storage.Tests;

/// <summary>B-206 验收：last-known-state 按 Connection 落盘；缓存损坏当作无缓存（离线启动不白屏的前提）。</summary>
public sealed class JsonCacheStoreTests
{
    private static WidgetState State(string widgetId, string summary) => new()
    {
        WidgetId = widgetId,
        WidgetType = "github.pr.list",
        ConnectionId = "conn-1",
        Severity = Severity.Info,
        Summary = summary,
        FetchedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void SaveThenLoad_RoundTripsAcrossInstances()
    {
        using var dir = new TempDir();
        var first = new JsonCacheStore(dir.Path);
        first.SaveState("conn-1", State("w-1", "3 open PRs"));

        var second = new JsonCacheStore(dir.Path);
        var states = second.LoadStates("conn-1");

        var state = Assert.Single(states.Values);
        Assert.Equal("w-1", state.WidgetId);
        Assert.Equal("3 open PRs", state.Summary);
        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), state.FetchedAt);
    }

    [Fact]
    public void LoadUnknownConnection_ReturnsEmpty()
    {
        using var dir = new TempDir();
        var store = new JsonCacheStore(dir.Path);

        Assert.Empty(store.LoadStates("nope"));
    }

    [Fact]
    public void CorruptCacheFile_TreatedAsEmpty()
    {
        using var dir = new TempDir();
        var cacheDir = System.IO.Path.Combine(dir.Path, "cache", "conn-1");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(System.IO.Path.Combine(cacheDir, "states.json"), "}{ corrupt");

        var store = new JsonCacheStore(dir.Path);

        Assert.Empty(store.LoadStates("conn-1"));
    }

    [Fact]
    public void SaveState_OverwritesSameWidget()
    {
        using var dir = new TempDir();
        var store = new JsonCacheStore(dir.Path);
        store.SaveState("conn-1", State("w-1", "old"));
        store.SaveState("conn-1", State("w-1", "new"));

        var states = store.LoadStates("conn-1");

        Assert.Single(states);
        Assert.Equal("new", states["w-1"].Summary);
    }

    [Fact]
    public void StatesAreIsolatedPerConnection()
    {
        using var dir = new TempDir();
        var store = new JsonCacheStore(dir.Path);
        store.SaveState("conn-a", State("w-1", "from-a"));

        Assert.Empty(store.LoadStates("conn-b"));
        Assert.Equal("from-a", store.LoadStates("conn-a")["w-1"].Summary);
    }
}
