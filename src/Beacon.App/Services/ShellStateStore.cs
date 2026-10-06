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
}
