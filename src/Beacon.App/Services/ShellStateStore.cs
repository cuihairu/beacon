using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.App.Services;

/// <summary>Shell 级 UI 状态（窗口位置等），与用户配置（config/widgets/…）分开存储。
/// 位置复用 PinLayout（显示器 + 锚点角 + DIP 偏移），与 L0 布局同构（RFC §6.2.3）。</summary>
public sealed class ShellStateStore
{
    private sealed class ShellStateFile
    {
        public PinLayout? CapsuleLayout { get; set; }

        /// <summary>悬浮岛布局（display-modes-analysis §7 Phase 2）：与经典胶囊布局分开存——
        /// 两形态各有默认位（岛=顶中、胶囊=右下），互不覆盖，切形态各回各的记忆位。</summary>
        public CapsuleIslandLayout? IslandLayout { get; set; }
    }

    private static string DirectoryPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon");

    private static string FilePath => Path.Combine(DirectoryPath, "shell-state.json");

    /// <summary>读取胶囊布局；损坏/缺失返回 null（用默认位置）。</summary>
    public PinLayout? LoadCapsuleLayout()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }
            return BeaconJson.Deserialize<ShellStateFile>(File.ReadAllText(FilePath)).CapsuleLayout;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void SaveCapsuleLayout(PinLayout? layout)
    {
        Directory.CreateDirectory(DirectoryPath);
        var state = new ShellStateFile { CapsuleLayout = layout };
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, BeaconJson.Serialize(state));
        File.Move(temporary, FilePath, overwrite: true);
    }

    /// <summary>读取悬浮岛布局；损坏/缺失返回 null（用顶部居中默认位，CapsuleIslandMath.DefaultOffset）。</summary>
    public CapsuleIslandLayout? LoadIslandLayout()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }
            return BeaconJson.Deserialize<ShellStateFile>(File.ReadAllText(FilePath)).IslandLayout;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>悬浮岛布局落盘（原子写与 SaveCapsuleLayout 同口径），读改写保留 CapsuleLayout 段。</summary>
    public void SaveIslandLayout(CapsuleIslandLayout? layout)
    {
        Directory.CreateDirectory(DirectoryPath);
        var state = new ShellStateFile { CapsuleLayout = LoadCapsuleLayout(), IslandLayout = layout };
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, BeaconJson.Serialize(state));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
