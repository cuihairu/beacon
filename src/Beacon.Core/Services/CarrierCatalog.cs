using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 展示载体统一选择（ISLAND-RING-1 扩围②，2026-11-07 用户令）：此前载体控制散在四处
/// （岛形态开关/数量悬浮窗开关/悬浮形态下拉/托盘零散入口），是「一个页面决定显示什么+
/// 用什么显示」的反面。收编为组件中心「展示载体」区三项，读写 Core 单源：
/// <list type="bullet">
/// <item>悬浮岛 → 旧字段 CapsuleIsland（岛形态开=胶囊窗顶中贴屏；取消=岛退场——「显示状态胶囊」
/// 仍开着则回右下角经典胶囊，否则胶囊窗整体关闭；岛↔经典是同一窗的两种形态，唯一互斥面）</item>
/// <item>悬浮框 → 旧字段 NumericFloatingEnabled（数值/额度类悬浮窗总闸；关=桌面零残留即时生效）</item>
/// <item>看板 → boardEnabled（此前无常驻开关，唤起式面板恒可用——按 HiddenComponents「增量可选字段」
/// 先例补字段，缺键=true 旧配置行为不变；关=热键/岛点击不可唤起且已开即收）</item>
/// </list>
/// 共存规则（按真实窗口架构，无编造）：三者是不同窗口，任意组合共存；信息密集组件的悬浮形态
/// （PinDisplayMode panel|floating）是悬浮框的组件级补充档，不属于本三项。
/// </summary>
public static class CarrierCatalog
{
    public const string CarrierIslandId = "carrier.island";
    public const string CarrierFloatingId = "carrier.floating";
    public const string CarrierBoardId = "carrier.board";

    /// <summary>组件中心「展示载体」区在册三项（顺序=面板展示序）。</summary>
    public static IReadOnlyList<CarrierItem> Carriers { get; } =
    [
        new(CarrierIslandId, "悬浮岛", "顶部居中刘海形态的状态岛（取消则按「显示状态胶囊」回经典胶囊或整体关闭）"),
        new(CarrierFloatingId, "悬浮框", "数值/额度类独立悬浮窗（关=桌面零残留即时生效）"),
        new(CarrierBoardId, "看板", "事件时间线看板（唤起式：岛点击/热键唤起；关闭=不可唤起且已开即收）"),
    ];

    public static bool IslandEnabled(AppConfig app) => app.CapsuleIsland;

    public static bool FloatingEnabled(AppConfig app) => app.NumericFloatingEnabled;

    public static bool BoardEnabled(AppConfig app) => app.BoardEnabled;

    /// <summary>写入口（设置页载体勾选唯一写路径）：岛/悬浮框写旧字段零迁移，看板写 boardEnabled。</summary>
    public static void SetEnabled(AppConfig app, string carrierId, bool enabled)
    {
        switch (carrierId)
        {
            case CarrierIslandId:
                SetIsland(app, enabled);
                break;
            case CarrierFloatingId:
                SetFloating(app, enabled);
                break;
            case CarrierBoardId:
                SetBoard(app, enabled);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(carrierId), carrierId, "未知载体 Id（接线错误须炸响，不静默吞）");
        }
    }

    public static void SetIsland(AppConfig app, bool enabled) => app.CapsuleIsland = enabled;

    public static void SetFloating(AppConfig app, bool enabled) => app.NumericFloatingEnabled = enabled;

    public static void SetBoard(AppConfig app, bool enabled) => app.BoardEnabled = enabled;
}

/// <summary>展示载体条目：组件中心「展示载体」区一行的投影（Id=勾选落库键、Hint=真实能力说明）。</summary>
public sealed record CarrierItem(string Id, string Label, string Hint);
