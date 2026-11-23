using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 桌面状态条三档（SET-UX-02，2026-11-23 用户令原型落地批1）：off=关闭 / capsule=右下角胶囊 /
/// island=顶部悬浮岛。此前「常规页显示状态胶囊 + 组件中心岛勾选」两处开关分管两个旧字段，
/// 关岛可能露出右下角胶囊——统一为一个选择器，投影两个旧字段（零迁移）：
/// <list type="bullet">
/// <item>读投影：岛优先（CapsuleIsland=true → island）、胶囊其次（ShowCapsule=true → capsule）、
/// 否则 off。旧配置四组合（双关/胶囊/岛/双开）全部有唯一投影。</item>
/// <item>写成组保存：三档各自的两字段终态一次写齐——island→双开（窗由 ShowCapsule||CapsuleIsland
/// 建定，CapsuleIsland 定岛形态）、capsule→胶囊开岛关（同一胶囊窗回经典形态）、off→双关（窗关）。</item>
/// </list>
/// App 侧胶囊窗生命周期（EnsureCapsule 口径）不感知本档：字段即契约，广播 SettingsApplied 走既有链。
/// </summary>
public static class StatusBarMode
{
    public const string Off = "off";
    public const string Capsule = "capsule";
    public const string Island = "island";

    /// <summary>读投影（岛优先、胶囊其次）：CapsuleIsland 开即 island；否则 ShowCapsule 开=capsule；余 off。</summary>
    public static string Read(AppConfig app)
        => app.CapsuleIsland ? Island : (app.ShowCapsule ? Capsule : Off);

    /// <summary>写成组保存（主动选档才走）：两字段终态一次写齐，不读旧值拼接。
    /// 未知档炸响（接线错误不静默吞，同 CarrierCatalog.SetEnabled 口径）。</summary>
    public static void Apply(AppConfig app, string mode)
    {
        switch (mode)
        {
            case Island:
                app.ShowCapsule = true;
                app.CapsuleIsland = true;
                break;
            case Capsule:
                app.ShowCapsule = true;
                app.CapsuleIsland = false;
                break;
            case Off:
                app.ShowCapsule = false;
                app.CapsuleIsland = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知状态条档（接线错误须炸响，不静默吞）");
        }
    }
}
