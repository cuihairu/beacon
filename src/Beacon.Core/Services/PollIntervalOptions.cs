namespace Beacon.Core.Services;

/// <summary>
/// 全局检查频率档（SET-UX-01，2026-11-23 用户令原型落地批1）：预设五档 + 预设外合法旧值
/// （如 120 秒——AppConfig.PollIntervalSeconds 允许任意正整数秒）的「自定义」回显。
/// 此前下拉只有五档，120 秒旧配置在框里无档可选，且「改开机自启」经全字段回写把 120 洗回
/// 默认 0——修法=单项保存（App 侧每控件只写自己的字段）+ 非预设值按原值回显「自定义 · 2 分钟」，
/// 复用原存储与即时生效路径（AppConfig.PollIntervalSeconds 不迁移不新增字段）。
/// </summary>
public static class PollIntervalOptions
{
    /// <summary>（秒, 标签）预设档；0=按组件档位（各组件刷新档策略表）。展示序=列表序。</summary>
    public static IReadOnlyList<(int Seconds, string Label)> Presets { get; } =
    [
        (0, "按组件档位（默认）"),
        (15, "15 秒"),
        (30, "30 秒"),
        (60, "1 分钟"),
        (300, "5 分钟"),
    ];

    /// <summary>是否预设档（决定回显走预设标签还是自定义标签）。</summary>
    public static bool IsPreset(int seconds) => Presets.Any(p => p.Seconds == seconds);

    /// <summary>预设外合法旧值的回显标签：120→「自定义 · 2 分钟」、90→「自定义 · 1 分 30 秒」、
    /// 45→「自定义 · 45 秒」。分秒 60 进位；非正值不走此路径（0 由「按组件档位」预设承载）。</summary>
    public static string CustomLabel(int seconds)
    {
        if (seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "非正值不走自定义标签（预设 0 由「按组件档位」承载）");
        }
        var minutes = seconds / 60;
        var rest = seconds % 60;
        return (minutes, rest) switch
        {
            (_, 0) => $"自定义 · {minutes} 分钟",
            (0, _) => $"自定义 · {rest} 秒",
            _ => $"自定义 · {minutes} 分 {rest} 秒",
        };
    }
}
