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

    /// <summary>缓存写失败（连接目录槽位被同名文件占住 → IOException）不影响主流程：内存态仍在，磁盘未落。</summary>
    [Fact]
    public void SaveState_WriteFailure_KeepsMemoryStateWithoutDiskWrite()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(System.IO.Path.Combine(dir.Path, "cache"));
        File.WriteAllText(System.IO.Path.Combine(dir.Path, "cache", "conn-1"), "not a directory"); // 占住连接目录槽位
        var store = new JsonCacheStore(dir.Path);

        store.SaveState("conn-1", State("w-1", "memory-only")); // IOException 被吞，不抛

        Assert.Equal("memory-only", store.LoadStates("conn-1")["w-1"].Summary); // 内存态兜底可读
        Assert.False(File.Exists(System.IO.Path.Combine(dir.Path, "cache", "conn-1", "states.json"))); // 磁盘未落
        Assert.True(File.Exists(System.IO.Path.Combine(dir.Path, "cache", "conn-1"))); // 占位文件原样（未被目录替换）
    }
}
