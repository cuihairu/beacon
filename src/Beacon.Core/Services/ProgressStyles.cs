namespace Beacon.Core.Services;

/// <summary>
/// 额度显示方式三值单源（AQUA-001，2026-11-15 用户令「整行水族箱」）：bar（默认，底部线性条）/
/// pool（圆池注水，旧特效仅显式选择）/ aquarium（整行水族箱：文字行下方通栏活水，水位=进度）。
/// 缺省/非法值一律落 bar（与 AppConfig.ProgressStyle 默认同口径——pool 绝不做默认路径的纪律不变，
/// aquarium 同样仅显式选择）。组件级 widgets.json config.progressStyle 覆盖外观设置时同走本解析。
/// </summary>
public static class ProgressStyles
{
    public const string Bar = "bar";

    public const string Pool = "pool";

    public const string Aquarium = "aquarium";

    /// <summary>解析显示方式：缺省/空白/非法一律回 bar（fail-open 同 UiDensity 口径——
    /// 配置打错不构成换默认渲染路径的理由）。</summary>
    public static string Resolve(string? raw)
        => (raw?.Trim().ToLowerInvariant()) switch
        {
            "pool" => Pool,
            "aquarium" => Aquarium,
            _ => Bar,
        };

    /// <summary>装饰性循环动画是否放行（水族箱波纹的 off 档静止门，B-707 同一纪律）：
    /// motion mode 显式 "off" = 静止；reduced/full/缺省 = 放行（波纹是常驻装饰，不是 full 档专属）。</summary>
    public static bool WavesAnimate(string? motionMode)
        => !string.Equals(motionMode?.Trim().ToLowerInvariant(), "off", StringComparison.Ordinal);
}
