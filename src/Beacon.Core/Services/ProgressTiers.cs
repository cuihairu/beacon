using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 进度档位色单一来源（组件化重构 Phase 2，redesign-plan §6）：tile 进度条（PinnedHost）与
/// 组件库进度件共用同一档位口径——≥98% 满格 Error 红、≥80% NearFull 起 Warning、否则正常色
/// （2026-10-11 用户令「满了就变成另一颜色」）。阈值原为 PinnedHostWindow 私有常量，下沉 Core 供双端引用。
/// </summary>
public static class ProgressTiers
{
    public const double NearFull = 0.8;
    public const double Full = 0.98;

    /// <summary>档位判定：null = 正常色（不换档）。</summary>
    public static Severity? Level(double fraction)
        => fraction >= Full ? Severity.Error
        : fraction >= NearFull ? Severity.Warning
        : null;
}
