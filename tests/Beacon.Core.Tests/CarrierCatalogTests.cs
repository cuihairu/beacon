using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 展示载体统一选择（ISLAND-RING-1 扩围②，2026-11-07 用户令）：载体控制此前散在四处
/// （岛形态开关/数量悬浮窗开关/悬浮形态下拉/托盘零散入口）——收编为组件中心「展示载体」区三项：
/// 悬浮岛/悬浮框/看板。持久化向后兼容：旧 config 字段继续用（CapsuleIsland/NumericFloatingEnabled），
/// 不造新格式；看板此前无常驻开关，按 HiddenComponents「增量可选字段」先例补 boardEnabled
/// （缺键=true=旧配置行为不变）。行为锁：载体勾选变化 → 对应字段翻转（App 侧据此建/关窗）。
/// 真实共存规则（无互斥面，不许编）：三者是不同窗口任意组合共存；唯一形态互斥=岛↔经典胶囊
/// （同一胶囊窗两种形态，岛勾选时经典让位——SetIslandMode 既有语义）；看板是唤起式面板，
/// 关=不可唤起且已开即收，与岛/悬浮框共存。
/// </summary>
public sealed class CarrierCatalogTests
{
    // —— 读：默认值=旧配置行为（向后兼容锁） ——

    [Fact]
    public void Defaults_IslandOff_FloatingOn_BoardOn()
    {
        var app = new AppConfig();
        Assert.False(CarrierCatalog.IslandEnabled(app)); // CapsuleIsland 默认关=历史口径（岛形态开关默认关）
        Assert.True(CarrierCatalog.FloatingEnabled(app)); // 数量悬浮窗默认开（2026-10-11 用户令）
        Assert.True(CarrierCatalog.BoardEnabled(app)); // 看板此前无开关恒可用：缺省 true=行为不变
    }

    // —— 写：勾选变化 → 旧字段翻转（岛/悬浮框持久化零迁移） ——

    [Fact]
    public void SetIsland_TogglesLegacyCapsuleIslandField()
    {
        var app = new AppConfig();
        CarrierCatalog.SetIsland(app, enabled: true);
        Assert.True(app.CapsuleIsland); // 旧字段继续用：岛勾选写 CapsuleIsland，不新增载体格式
        CarrierCatalog.SetIsland(app, enabled: false);
        Assert.False(app.CapsuleIsland);
    }

    [Fact]
    public void SetFloating_TogglesLegacyNumericFloatingField()
    {
        var app = new AppConfig();
        CarrierCatalog.SetFloating(app, enabled: false);
        Assert.False(app.NumericFloatingEnabled); // 旧字段继续用：悬浮框勾选写 NumericFloatingEnabled
        CarrierCatalog.SetFloating(app, enabled: true);
        Assert.True(app.NumericFloatingEnabled);
    }

    [Fact]
    public void SetBoard_TogglesBoardEnabledField()
    {
        var app = new AppConfig();
        CarrierCatalog.SetBoard(app, enabled: false);
        Assert.False(app.BoardEnabled);
        CarrierCatalog.SetBoard(app, enabled: true);
        Assert.True(app.BoardEnabled);
    }

    [Fact]
    public void SetCarrier_GenericEntry_MatchesPerCarrierWriters()
    {
        var app = new AppConfig();
        CarrierCatalog.SetEnabled(app, CarrierCatalog.CarrierIslandId, enabled: true);
        CarrierCatalog.SetEnabled(app, CarrierCatalog.CarrierFloatingId, enabled: false);
        CarrierCatalog.SetEnabled(app, CarrierCatalog.CarrierBoardId, enabled: false);
        Assert.True(CarrierCatalog.IslandEnabled(app));
        Assert.False(CarrierCatalog.FloatingEnabled(app));
        Assert.False(CarrierCatalog.BoardEnabled(app));
    }

    [Fact]
    public void SetEnabled_UnknownCarrierId_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => CarrierCatalog.SetEnabled(new AppConfig(), "carrier.unknown", enabled: true));

    // —— 持久化：同一 config.json，往返保留 ——

    [Fact]
    public void Config_RoundTrip_PersistsCarrierStates()
    {
        var app = new AppConfig();
        CarrierCatalog.SetIsland(app, enabled: true);
        CarrierCatalog.SetFloating(app, enabled: false);
        CarrierCatalog.SetBoard(app, enabled: false);
        var restored = BeaconJson.RoundTrip(app);
        Assert.True(CarrierCatalog.IslandEnabled(restored));
        Assert.False(CarrierCatalog.FloatingEnabled(restored));
        Assert.False(CarrierCatalog.BoardEnabled(restored));
    }

    // —— 载体清单：组件中心「展示载体」区三项固定在册 ——

    [Fact]
    public void Carriers_ListsIslandFloatingBoard()
        => Assert.Equal(
            [CarrierCatalog.CarrierIslandId, CarrierCatalog.CarrierFloatingId, CarrierCatalog.CarrierBoardId],
            CarrierCatalog.Carriers.Select(c => c.Id).ToArray());
}
