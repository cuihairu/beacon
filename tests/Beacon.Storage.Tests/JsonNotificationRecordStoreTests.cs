using Beacon.Core.Models;
using Beacon.Storage;

namespace Beacon.Storage.Tests;

/// <summary>通知记录落盘（notifications.json，RFC §8）：往返 / 缺失当空 / 损坏回退备份。</summary>
public sealed class JsonNotificationRecordStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"beacon-notif-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static NotificationRecord Record(string id, bool read = false) => new()
    {
        Id = id,
        SourceWidgetId = "w-ci",
        WidgetType = "github.actions.runs",
        Severity = Severity.Error,
        Title = "ci.yml · failure",
        Message = read ? null : "Last update 10:00:00",
        Timestamp = new DateTimeOffset(2026, 10, 9, 10, 0, 0, TimeSpan.Zero),
        Read = read,
    };

    [Fact]
    public async Task SaveThenLoad_RoundTripsRecordsWithReadState()
    {
        var store = new JsonNotificationRecordStore(_directory);
        var records = new List<NotificationRecord> { Record("r-1"), Record("r-2", read: true) };

        await store.SaveAsync(records);
        var loaded = await store.LoadAsync();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("r-1", loaded[0].Id);
        Assert.False(loaded[0].Read);
        Assert.True(loaded[1].Read);
        Assert.Equal(Severity.Error, loaded[1].Severity);
        Assert.Equal("ci.yml · failure", loaded[1].Title);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsEmpty()
    {
        var loaded = await new JsonNotificationRecordStore(_directory).LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task Load_CorruptFile_RecoversFromBackup()
    {
        var store = new JsonNotificationRecordStore(_directory);
        await store.SaveAsync([Record("r-good")]); // 首存只建主文件
        await store.SaveAsync([Record("r-good"), Record("r-newer")]); // 二存起 File.Replace 滚出 .bak
        File.WriteAllText(Path.Combine(_directory, "notifications.json"), "{ not json"); // 主文件写坏

        var loaded = await store.LoadAsync();

        Assert.Single(loaded); // .bak 备份接管（上一份完整内容）
        Assert.Equal("r-good", loaded[0].Id);
    }

    [Fact]
    public async Task Load_CorruptWithoutBackup_ReturnsEmptyAndArchives()
    {
        var store = new JsonNotificationRecordStore(_directory);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "notifications.json"), "garbage");

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
        Assert.True(File.Exists(Path.Combine(_directory, "notifications.json.corrupt"))); // 损坏原件留档
    }
}
