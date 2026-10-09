using Beacon.Core.Abstractions;
using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage;

/// <summary>
/// 通知记录落盘（RFC §8，notifications.json）：滚动记录 + 已读态跨重启。
/// 与 JsonConfigurationStore 同一套原子写（临时文件 + Replace + .bak），损坏回退备份、再失败当空。
/// 记录非用户配置，不需要 LoadErrors 诊断面。
/// </summary>
public sealed class JsonNotificationRecordStore : INotificationRecordStore
{
    private readonly string _directory;
    private readonly object _gate = new();

    public JsonNotificationRecordStore(string? rootDirectory = null)
    {
        _directory = rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon");
    }

    public Task<IReadOnlyList<NotificationRecord>> LoadAsync()
    {
        var path = Path.Combine(_directory, "notifications.json");
        try
        {
            if (!File.Exists(path))
            {
                return Task.FromResult<IReadOnlyList<NotificationRecord>>([]);
            }
            var records = BeaconJson.Deserialize<List<NotificationRecord>>(File.ReadAllText(path));
            return Task.FromResult<IReadOnlyList<NotificationRecord>>(records ?? []);
        }
        catch (Exception)
        {
            try
            {
                File.Copy(path, path + ".corrupt", overwrite: true);
            }
            catch (Exception)
            {
                // 留档失败也当空——通知记录不值得阻断启动
            }
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    var recovered = BeaconJson.Deserialize<List<NotificationRecord>>(File.ReadAllText(backup));
                    if (recovered is not null)
                    {
                        return Task.FromResult<IReadOnlyList<NotificationRecord>>(recovered);
                    }
                }
                catch (Exception)
                {
                    // 备份也坏 → 当空
                }
            }
            return Task.FromResult<IReadOnlyList<NotificationRecord>>([]);
        }
    }

    public Task SaveAsync(IReadOnlyList<NotificationRecord> records)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "notifications.json");
        var temporary = path + ".tmp";
        var backup = path + ".bak";
        lock (_gate)
        {
            File.WriteAllText(temporary, BeaconJson.Serialize(records));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, backup);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        return Task.CompletedTask;
    }
}
