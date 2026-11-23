namespace Beacon.Core.Services;

/// <summary>
/// 历史条目级别（2026-11-23 用户令）：历史行的颜色反映**历史时刻的状态**，
/// 不随当前最差窗口漂移。额度类快照的 Severity 由"当前最差窗口"决定
/// （tray 口径），但快照 Payload 里带着**该时刻自己的百分比**
/// （rolling/weekly/monthly_percent / percent）——渲染历史行时按快照
/// 自身百分比 + 组件阈值重算那一刻的级别；非额度组件（Payload 无百分比）
/// 退回快照原 Severity（那一刻本来就是按自身状态定的）。
/// </summary>
public static class TimelineSeverity
{
    /// <summary>从快照重算历史级别：percent 键优先（该快照主窗口的已用 %），
    /// 缺失退三窗里最大的；全无 → 原 Severity。</summary>
    public static Models.Severity Resolve(
        Models.WidgetState snapshot, double warnPercent, double errorPercent)
    {
        var percent = PercentOf(snapshot.Payload);
        if (percent is null)
        {
            return snapshot.Severity; // 非额度组件：快照级别即时刻级别
        }

        var value = percent.Value;
        if (value >= errorPercent)
        {
            return Models.Severity.Error;
        }

        return value >= warnPercent ? Models.Severity.Warning : Models.Severity.Success;
    }

    private static double? PercentOf(IReadOnlyDictionary<string, string> payload)
    {
        if (payload.TryGetValue("percent", out var main)
            && double.TryParse(main, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        double? worst = null;
        foreach (var key in (string[])["rolling_percent", "weekly_percent", "monthly_percent"])
        {
            if (payload.TryGetValue(key, out var raw)
                && double.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var window))
            {
                worst = worst is null ? window : System.Math.Max(worst.Value, window);
            }
        }
        return worst;
    }
}
