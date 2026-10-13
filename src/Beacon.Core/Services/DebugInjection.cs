using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// debug 注入面（todo 行 71「Critical 与崩溃注入机制」，环境变量门控，缺省零行为）：
/// <c>BEACON_DEBUG_SEVERITY=critical</c> 时把刷新成功的 state 改判 Critical——L0 渲染 Critical
/// 默认色（#ff3b30）+ Critical 脉冲（reduced 起），供 CI 视觉取证（行 55/61 Critical 半）与装机演练；
/// 值不认识/缺省一律原样返回。进程内首次读取缓存（注入值场景启动前设好，运行期不回读）。
/// 与 App 侧 <c>BEACON_DEBUG_CRASH</c>（XAML 派发线程延迟抛未处理异常，走三兜底保进程）同族，均为
/// 只进 debug 的观测钩子：不进设置 UI、不落配置文件、不改变任何生产路径行为。
/// </summary>
public static class DebugInjection
{
    public const string SeverityVariable = "BEACON_DEBUG_SEVERITY";

    private static readonly string? Configured = Environment.GetEnvironmentVariable(SeverityVariable);

    /// <summary>应用注入（纯函数供单测）：critical → Critical/Failed（摘要/负载不动，只改级别）；其余原样。</summary>
    public static WidgetState OverrideSeverity(WidgetState state, string? value = null)
    {
        var effective = value ?? Configured;
        return string.Equals(effective, "critical", StringComparison.OrdinalIgnoreCase)
            ? state with { Severity = Severity.Critical, Lifecycle = LifecycleState.Failed }
            : state;
    }
}
