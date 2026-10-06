using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage;

/// <summary>config.json 最小读写（B-103 热键持久化所需）。
/// B-201 的 JsonConfigurationStore 将以完整 IConfigurationStore 覆盖配置面，本类保留为薄封装。</summary>
public sealed class AppConfigFile
{
    private static string DirectoryPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon");

    private static string FilePath => Path.Combine(DirectoryPath, "config.json");

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppConfig();
            }
            return BeaconJson.Deserialize<AppConfig>(File.ReadAllText(FilePath));
        }
        catch (Exception)
        {
            // 配置损坏：回退默认值（B-201 会加上 .bak 备份恢复）
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, BeaconJson.Serialize(config));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
