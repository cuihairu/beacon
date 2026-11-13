using System.Text.Json;
using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage;

/// <summary>config.json 最小读写（B-103 热键持久化所需）。
/// B-201 的 JsonConfigurationStore 将以完整 IConfigurationStore 覆盖配置面，本类保留为薄封装。
/// .bak 备份 + 损坏恢复（*.corrupt 留档）与 JsonConfigurationStore 同口径。</summary>
public sealed class AppConfigFile
{
    private readonly string _directoryPath;
    private Func<string>? _readerOverride;

    /// <summary>可注入根目录（测试隔离；与 JsonConfigurationStore 同口径），默认 ApplicationData\Beacon。</summary>
    public AppConfigFile(string? rootDirectory = null)
    {
        _directoryPath = rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon");
    }

    private string DirectoryPath => _directoryPath;

    private string FilePath => Path.Combine(DirectoryPath, "config.json");

    /// <summary>读入口（测试注入 IO 故障用；生产恒真读盘）。</summary>
    internal Func<string> Reader
    {
        get => _readerOverride ?? (() => File.ReadAllText(FilePath));
        set => _readerOverride = value;
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppConfig();
            }
            return BeaconJson.Deserialize<AppConfig>(ReadConfigText());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            // 仅 JSON 损坏进恢复链（Codex 2026-11-12 M 收敛）：此前 catch 全量把 IO 故障也送进来——
            // 文件被占用/权限问题时好文件被 *.corrupt 留档顶掉真损坏档、会话配置静默归默认。
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

    /// <summary>读 config.json，IO 故障带一次瞬时重试：杀软/索引器短暂锁文件被重试吸收；
    /// 仍失败向上抛（fail loud，不冒充损坏进恢复链）。</summary>
    private string ReadConfigText()
    {
        try
        {
            return Reader();
        }
        catch (IOException)
        {
            Thread.Sleep(150);
            return Reader();
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
