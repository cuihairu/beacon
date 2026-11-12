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
            // 损坏回退 .bak（审核修复 2026-11-12 P1）：此前静默回默认——config.json 在落盘窗口
            // 被读时热键/主题/自启悄悄归零。与 JsonConfigurationStore.LoadFile 同口径：
            // *.corrupt 留档 + .bak 恢复，坏文件不被后续落盘直接冲掉。
            try
            {
                File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
            }
            catch (Exception)
            {
                // 留档失败不挡恢复
            }
            if (File.Exists(FilePath + ".bak"))
            {
                try
                {
                    return BeaconJson.Deserialize<AppConfig>(File.ReadAllText(FilePath + ".bak"));
                }
                catch (Exception)
                {
                    // 备份也坏 → 默认值
                }
            }
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, BeaconJson.Serialize(config));
        // 原子替换 + .bak（审核修复 2026-11-12 P1）：此前 File.Move(overwrite) 冲掉旧文件不留备份
        if (File.Exists(FilePath))
        {
            File.Replace(temporary, FilePath, FilePath + ".bak");
        }
        else
        {
            File.Move(temporary, FilePath);
        }
    }
}
