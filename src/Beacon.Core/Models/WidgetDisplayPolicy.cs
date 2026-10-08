namespace Beacon.Core.Models;

/// <summary>
/// 组件悬浮显示策略：数值/额度类（FloatingOptIn）由 AppConfig.NumericFloatingEnabled 总闸
/// （默认关 = 桌面零残留，拍板 2026-10-08）；信息密集类（FloatingSupported，趋势图/灯组）
/// 与普通钉选不受闸。钉选宿主面板与独立悬浮窗两形态共用此判定——只闸悬浮窗一条路径时，
/// 钉选面板照画数值卡（「改了还弹」的第二条渲染路径，2026-10-08 实证）。
/// </summary>
public static class WidgetDisplayPolicy
{
    public static bool IsNumericSuppressed(WidgetTypeDescriptor? descriptor, bool numericFloatingEnabled)
        => descriptor is { FloatingOptIn: true } && !numericFloatingEnabled;
}
