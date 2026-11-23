using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 桌面状态条三档投影（SET-UX-02，2026-11-23 用户令原型落地批1）：统一选择器投影两个旧字段
/// （CapsuleIsland/ShowCapsule，零迁移）。行为锁：
/// ① 读投影四组合各有唯一档（岛优先、胶囊其次）——旧配置升级零迁移可读；
/// ② 写成组保存两字段终态一次写齐（off→双关 / capsule→胶囊开岛关 / island→双开）；
/// ③ 三档往返（off→capsule→island→off）字段链与 App 侧建窗口径（ShowCapsule||CapsuleIsland）
/// 一致：任意档位组合都决定唯一窗口存在性+形态；
/// ④ 未知档炸响（接线错误不静默吞）；
/// ⑤ 同一 config.json 往返保留（持久化向后兼容）。
/// </summary>
public sealed class StatusBarModeTests
{
    // —— 读：旧字段四组合 → 唯一投影（岛优先、胶囊其次） ——

    [Fact]
    public void Read_BothOff_ProjectsOff()
    {
        var app = new AppConfig { ShowCapsule = false, CapsuleIsland = false };
        Assert.Equal(StatusBarMode.Off, StatusBarMode.Read(app));
    }

    [Fact]
    public void Read_CapsuleOnly_ProjectsCapsule()
    {
        var app = new AppConfig { ShowCapsule = true, CapsuleIsland = false };
        Assert.Equal(StatusBarMode.Capsule, StatusBarMode.Read(app));
    }

    [Fact]
    public void Read_IslandOnly_ProjectsIsland()
    {
        // 岛优先：showCapsule=false 旧组合（只开岛，2026-11-06 用户实测过的真机状态）投影 island
        var app = new AppConfig { ShowCapsule = false, CapsuleIsland = true };
        Assert.Equal(StatusBarMode.Island, StatusBarMode.Read(app));
    }

    [Fact]
    public void Read_BothOn_ProjectsIsland_IslandWins()
    {
        // 岛优先：双开=岛形态（同一胶囊窗岛让经典让位，SetIslandMode 既有语义）
        var app = new AppConfig { ShowCapsule = true, CapsuleIsland = true };
        Assert.Equal(StatusBarMode.Island, StatusBarMode.Read(app));
    }

    // —— 写：主动选档 → 两字段终态一次写齐（不读旧值拼接） ——

    [Fact]
    public void Apply_Off_ClearsBothLegacyFields()
    {
        var app = new AppConfig { ShowCapsule = true, CapsuleIsland = true };
        StatusBarMode.Apply(app, StatusBarMode.Off);
        Assert.False(app.ShowCapsule);
        Assert.False(app.CapsuleIsland);
    }

    [Fact]
    public void Apply_Capsule_EnablesCapsule_DisablesIsland()
    {
        // 从岛切经典：CapsuleIsland 翻关、ShowCapsule 保开（同一窗回经典形态，窗不闪关）
        var app = new AppConfig { ShowCapsule = false, CapsuleIsland = true };
        StatusBarMode.Apply(app, StatusBarMode.Capsule);
        Assert.True(app.ShowCapsule);
        Assert.False(app.CapsuleIsland);
    }

    [Fact]
    public void Apply_Island_EnablesBoth()
    {
        var app = new AppConfig { ShowCapsule = false, CapsuleIsland = false };
        StatusBarMode.Apply(app, StatusBarMode.Island);
        Assert.True(app.ShowCapsule);
        Assert.True(app.CapsuleIsland);
    }

    // —— 往返：三档循环字段链稳定（App 建窗口径 ShowCapsule||CapsuleIsland 全程有解） ——

    [Fact]
    public void RoundTrip_Off_Capsule_Island_Off_FieldsChainConsistently()
    {
        var app = new AppConfig();
        Assert.Equal(StatusBarMode.Off, StatusBarMode.Read(app));
        StatusBarMode.Apply(app, StatusBarMode.Capsule);
        Assert.Equal(StatusBarMode.Capsule, StatusBarMode.Read(app));
        StatusBarMode.Apply(app, StatusBarMode.Island);
        Assert.Equal(StatusBarMode.Island, StatusBarMode.Read(app));
        StatusBarMode.Apply(app, StatusBarMode.Off);
        Assert.Equal(StatusBarMode.Off, StatusBarMode.Read(app));
    }

    [Fact]
    public void Apply_UnknownMode_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => StatusBarMode.Apply(new AppConfig(), "sidebar"));

    // —— 持久化：同一 config.json，往返保留 ——

    [Fact]
    public void Config_RoundTrip_PersistsProjectedMode()
    {
        var app = new AppConfig();
        StatusBarMode.Apply(app, StatusBarMode.Island);
        var restored = BeaconJson.RoundTrip(app);
        Assert.Equal(StatusBarMode.Island, StatusBarMode.Read(restored));
    }
}
