namespace Beacon.Core.Services;

/// <summary>
/// 岛收起态聚合事实（BEACON-ISLAND-AA-1 B2/B3）：从聚合计数字面采样，纯函数沉 Core——
/// App 侧只喂数不裁决（与 IslandDisplayPlan 同口径，Linux 本地可测）。
/// </summary>
public static class IslandAggregateFacts
{
    /// <summary>
    /// 健康份额：无数据（total==0）归 0 不再是 1.0——0 + 空轨道才是真值；
    /// 旧 1.0 兜底把零数据画成整圆/满条=造假（用户 2026-11-28 截图实证）。
    /// </summary>
    public static double OkShare(int ok, int warn, int error)
    {
        var total = ok + warn + error;
        return total == 0 ? 0.0 : (double)ok / total;
    }

    /// <summary>无数据标记：total==0 时进度面画空轨道、环心/条侧不留读数（与 OkShare=0 同拍）。</summary>
    public static bool NoData(int ok, int warn, int error) => ok + warn + error == 0;

    /// <summary>
    /// 离线横幅文案（B3）：最近一次拉取失败在 5 分钟内明示「（重试中）」给反馈，
    /// 否则保持静默旧值（冻结时间戳不再无解释常驻）。
    /// </summary>
    public static string OfflineBannerText(string timestamp, bool failedRecently)
        => failedRecently ? $"{timestamp}（重试中）" : timestamp;
}
