namespace Beacon.Core.Services;

/// <summary>嵌套额度窗口层：5 小时 ⊂ 周 ⊂ 月。</summary>
public enum QuotaWindow
{
    FiveHour,
    Weekly,
    Monthly,
}

/// <summary>
/// 嵌套窗口判定结果。
/// <see cref="Exhausted"/> 非空 = 最外层已耗尽的窗（**最高级别**，内层必然随之耗尽）；
/// <see cref="Display"/> = 正常态展示窗（5h 优先，缺失退周、月）。
/// </summary>
public sealed record QuotaWindowDecision(QuotaWindow? Exhausted, double? ExhaustedPercent, QuotaWindow? Display, double? DisplayPercent)
{
    /// <summary>三窗全无数据（既有「无数据兜底」口径：级别 Warning、不画进度条）。</summary>
    public bool NoData => Exhausted is null && Display is null;

    /// <summary>真实耗尽原因（不伪装成内层耗尽）；正常态 null。</summary>
    public string? ExhaustedReason => Exhausted switch
    {
        QuotaWindow.Monthly => "月度额度已耗尽（月 ⊇ 周 ⊇ 5h：周与 5h 随之归零）",
        QuotaWindow.Weekly => "周度额度已耗尽（周 ⊇ 5h：5h 随之归零）",
        QuotaWindow.FiveHour => "5 小时额度已耗尽",
        _ => null,
    };
}

/// <summary>
/// 嵌套额度窗口判定（2026-10-15 用户令，唯一口径）。
///
/// 三个窗口是**嵌套**关系而非并列：月 ⊇ 周 ⊇ 5 小时。外层耗尽 ⇒ 内层必然耗尽，
/// 故耗尽判定必须**外层优先**，不能用「已用最高」的最大值比较——三窗同时 100% 时并列，
/// 旧口径（并列取先，迭代序 5h→周→月）会命中 5h 窗，把「月度额度已耗尽」报成「5h 已用」，
/// 界面便显示内层还有余额（本次缺陷核心）。正常态（三窗均有余额）5h 窗是限流档位
/// （Z.ai 5h 耗尽触发 300s 降级），故展示 5h。
/// </summary>
public static class QuotaWindows
{
    /// <summary>已耗尽判定：已用占比 ≥100 ⇔ 剩余量 ≤0（两口径等价；接口只给已用 % 时用本式）。</summary>
    public static bool IsExhausted(double? usedPercent) => usedPercent is { } percent && percent >= 100;

    /// <summary>
    /// 嵌套窗口判定：月耗尽 → 月（最高级别，不再看周/5h）；否则周耗尽 → 周；否则 5h 耗尽 → 5h；
    /// 否则正常态 → 展示 5h（有额度时看 5h）。入参为各窗已用占比（0-100），null = 该窗无数据。
    /// </summary>
    public static QuotaWindowDecision Select(double? fiveHourUsedPercent, double? weeklyUsedPercent, double? monthlyUsedPercent)
    {
        if (IsExhausted(monthlyUsedPercent))
        {
            return new QuotaWindowDecision(QuotaWindow.Monthly, monthlyUsedPercent, null, null);
        }
        if (IsExhausted(weeklyUsedPercent))
        {
            return new QuotaWindowDecision(QuotaWindow.Weekly, weeklyUsedPercent, null, null);
        }
        if (IsExhausted(fiveHourUsedPercent))
        {
            return new QuotaWindowDecision(QuotaWindow.FiveHour, fiveHourUsedPercent, null, null);
        }

        // 正常态：5h 是限流档位故优先展示，缺失退周、月（该退让只影响展示，不影响耗尽判定）。
        if (fiveHourUsedPercent is { } rolling)
        {
            return new QuotaWindowDecision(null, null, QuotaWindow.FiveHour, rolling);
        }
        if (weeklyUsedPercent is { } weekly)
        {
            return new QuotaWindowDecision(null, null, QuotaWindow.Weekly, weekly);
        }
        if (monthlyUsedPercent is { } monthly)
        {
            return new QuotaWindowDecision(null, null, QuotaWindow.Monthly, monthly);
        }
        return new QuotaWindowDecision(null, null, null, null);
    }

    /// <summary>窗标（摘要/主数值文案口径：5h / 周 / 月）。</summary>
    public static string Label(QuotaWindow window) => window switch
    {
        QuotaWindow.FiveHour => "5h",
        QuotaWindow.Weekly => "周",
        QuotaWindow.Monthly => "月",
        _ => "",
    };
}
