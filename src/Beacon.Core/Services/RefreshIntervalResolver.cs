using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>刷新频率解析（SET-UX-10，2026-11-24 用户令批3）：此前「检查频率」（全局）/「刷新档」
/// （组件）/「检测间隔」（组件覆盖）三个名字各说各话，继承优先级全靠猜。本类归一命名——全局=
/// 「默认刷新间隔」、组件覆盖=「此组件刷新频率」、未设=「刷新档 X」、static=「手动刷新」——并把
/// 注册优先级（组件覆盖 > 全局间隔 > 档位策略表）收成单一实现：WidgetHost.RegisterWidgets 直接
/// 消费 EffectiveOverrideSeconds（与旧内联逻辑逐分支等价，调度器对档位默认的解析不变）。
/// Describe 只服务设置页文案：档位默认读静态 Defaults 表（设置页拿不到调度器注入的自定义策略，
/// 展示层与调度层同表即一致）。「手动刷新」与「运行期退避」在文案上分开——退避是失败重试行为，
/// 不是配置频率（UI 提示语承接，本类不管运行期）。</summary>
public static class RefreshIntervalResolver
{
    /// <summary>继承来源（封闭词表，UI 与测试同锚）。</summary>
    public const string SourceComponent = "component"; // 此组件刷新频率（组件覆盖，RefreshIntervalSeconds>0）
    public const string SourceGlobal = "global";       // 默认刷新间隔（全局覆盖，PollIntervalSeconds>0）
    public const string SourceTier = "tier";           // 刷新档默认（组件未设、全局按档位）
    public const string SourceManual = "manual";       // 手动刷新（static 档，不自动拉取）

    /// <summary>注册覆盖间隔秒数（null=调度器按档位策略表走）。优先级与 WidgetHost.RegisterWidgets
    /// 旧内联逻辑逐分支等价：组件 RefreshIntervalSeconds>0 优先；其次全局 PollIntervalSeconds>0；
    /// 都未设返回 null（manual 档调度器解析成 Infinite=不自动刷新）。</summary>
    public static int? EffectiveOverrideSeconds(int? widgetIntervalSeconds, int pollIntervalSeconds)
        => widgetIntervalSeconds is { } seconds && seconds > 0 ? seconds
        : pollIntervalSeconds > 0 ? pollIntervalSeconds
        : null;

    /// <summary>展示口径的生效间隔秒数（null=手动档不自动）：EffectiveOverrideSeconds 之外把档位默认
    /// 也解析出来（读静态 Defaults 表，未知档回落 default），供设置页显示完整周期。</summary>
    public static int? EffectiveDisplaySeconds(int? widgetIntervalSeconds, int pollIntervalSeconds, string tier)
        => EffectiveOverrideSeconds(widgetIntervalSeconds, pollIntervalSeconds)
        ?? TierSeconds(tier);

    /// <summary>继承来源判定（与 EffectiveOverrideSeconds 同优先级；未设再看档位是否手动）。</summary>
    public static string SourceOf(int? widgetIntervalSeconds, int pollIntervalSeconds, string tier)
        => EffectiveOverrideSeconds(widgetIntervalSeconds, pollIntervalSeconds) is { }
            ? (widgetIntervalSeconds is { } seconds && seconds > 0 ? SourceComponent : SourceGlobal)
            : TierSeconds(tier) is null ? SourceManual : SourceTier;

    /// <summary>人话标签：「每 15 秒 · 刷新档 ci」「每 2 分钟 · 此组件刷新频率」「每 5 分钟 · 默认刷新间隔」
    /// 「手动刷新 · 不自动拉取」。组件行/向导共用，文案与调度行为一致。</summary>
    public static string Describe(int? widgetIntervalSeconds, int pollIntervalSeconds, string tier)
    {
        var source = SourceOf(widgetIntervalSeconds, pollIntervalSeconds, tier);
        if (source == SourceManual)
        {
            return "手动刷新 · 不自动拉取";
        }
        var period = FormatPeriod(EffectiveDisplaySeconds(widgetIntervalSeconds, pollIntervalSeconds, tier)!.Value);
        return source switch
        {
            SourceComponent => $"每 {period} · 此组件刷新频率",
            SourceGlobal => $"每 {period} · 默认刷新间隔",
            _ => $"每 {period} · 刷新档 {tier}",
        };
    }

    /// <summary>周期格式化：120→「2 分钟」、90→「1 分 30 秒」、45→「45 秒」（PollIntervalOptions 同口径）。</summary>
    public static string FormatPeriod(int seconds)
    {
        var minutes = seconds / 60;
        var rest = seconds % 60;
        return (minutes, rest) switch
        {
            (_, 0) => $"{minutes} 分钟",
            (0, _) => $"{rest} 秒",
            _ => $"{minutes} 分 {rest} 秒",
        };
    }

    /// <summary>档位默认秒数（读静态策略表；手动档=Infinite 返 null；未知档回落 default）。</summary>
    private static int? TierSeconds(string tier)
    {
        var policy = RefreshTiers.Defaults.GetValueOrDefault(tier, RefreshTiers.Defaults[RefreshTiers.Default]);
        return policy.Interval == Timeout.InfiniteTimeSpan ? null : (int)policy.Interval.TotalSeconds;
    }
}
