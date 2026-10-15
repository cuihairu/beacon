namespace Beacon.Core.Models;

/// <summary>
/// 组件悬浮显示策略：数值/额度类（FloatingOptIn）由 AppConfig.NumericFloatingEnabled 总闸
/// （2026-10-11 用户令改默认开）；信息密集类（FloatingSupported，趋势图/灯组）
/// 与普通钉选不受闸。钉选宿主面板与独立悬浮窗两形态共用此判定——只闸悬浮窗一条路径时，
/// 钉选面板照画数值卡（「改了还弹」的第二条渲染路径，2026-10-08 实证）。
/// </summary>
public static class WidgetDisplayPolicy
{
    public static bool IsNumericSuppressed(WidgetTypeDescriptor? descriptor, bool numericFloatingEnabled)
        => descriptor is { FloatingOptIn: true } && !numericFloatingEnabled;

    /// <summary>钉入口可见性（B-702 两入口一致性，todo 行 59/60）：设置页钉开关与 L2 面板行右键
    /// 菜单共用此一条判定，避免只闸一条入口时另一条留死链——面板侧曾恒挂 Pin 菜单项、
    /// 非准入类型点击后 PinWidget 静默返回（看着能点、按了没反应）。</summary>
    public static bool PinEntryVisible(WidgetTypeDescriptor? descriptor) => descriptor is { PinSupported: true };

    /// <summary>钉选面板是否承载该组件（App 的启动建板/设置后重建/空板自关复活三处判定合一）：
    /// 前提 PinSupported 且不被数值总闸抑制；悬浮形态下悬浮窗已展示的组件不重复上面板。
    /// panel 形态恒承载（面板是该形态唯一 L0，悬浮门不参与）。</summary>
    public static bool PanelCarries(WidgetTypeDescriptor? descriptor, bool numericFloatingEnabled, bool floatingMode, bool floatingEligible)
        => PinEntryVisible(descriptor)
           && !IsNumericSuppressed(descriptor, numericFloatingEnabled)
           && (!floatingMode || !floatingEligible);
}
