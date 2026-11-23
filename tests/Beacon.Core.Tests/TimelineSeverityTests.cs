using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// 历史条目级别口径（2026-11-23 用户令）：
/// tray = 当前最差窗口；历史行 = 按快照自身百分比重算那一刻的级别。
/// 快照 Severity 是「当时最差窗」（可能被周/月抬高），历史行必须按快照
/// Payload 里的主窗口百分比（或三窗最大值）重算，且不随后续刷新漂移。
/// </summary>
public sealed class TimelineSeverityTests
{
    private static WidgetState State(Dictionary<string, string>? payload, Severity severity = Severity.Warning)
        => new()
        {
            WidgetId = "w1",
            WidgetType = "ark.usage",
            ConnectionId = "c1",
            Severity = severity,
            Summary = "s",
            Payload = payload ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            FetchedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void Percent_below_warn_resolves_success_even_if_snapshot_says_warning()
    {
        // 快照 Severity 是当时最差窗（周 65%）给的 Warning，但 5h 主窗只有 10%：
        // 历史行按快照 percent=10 重算 → Success（那一刻 5h 状态是绿的）
        var snapshot = State(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = "10",
        }, Severity.Warning);

        var resolved = TimelineSeverity.Resolve(snapshot, warnPercent: 60, errorPercent: 90);
        Assert.Equal(Severity.Success, resolved);
    }

    [Fact]
    public void Percent_at_error_resolves_error()
    {
        var snapshot = State(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = "92",
        }, Severity.Warning);

        Assert.Equal(Severity.Error, TimelineSeverity.Resolve(snapshot, 60, 90));
    }

    [Fact]
    public void Percent_at_warn_resolves_warning()
    {
        var snapshot = State(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = "60",
        }, Severity.Success);

        Assert.Equal(Severity.Warning, TimelineSeverity.Resolve(snapshot, 60, 90));
    }

    [Fact]
    public void Missing_main_percent_falls_back_to_largest_window_percent()
    {
        // 主 percent 键空：三窗取最大（该快照自己的窗值，不是当前值）
        var snapshot = State(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = "",
            ["rolling_percent"] = "12",
            ["weekly_percent"] = "70",
            ["monthly_percent"] = "30",
        }, Severity.Warning);

        Assert.Equal(Severity.Warning, TimelineSeverity.Resolve(snapshot, 60, 90));
    }

    [Fact]
    public void No_percent_payload_returns_snapshot_severity()
    {
        // 非额度组件（无任何百分比键）：快照级别即时刻级别
        var snapshot = State(null, Severity.Error);
        Assert.Equal(Severity.Error, TimelineSeverity.Resolve(snapshot, 60, 90));
    }

    [Fact]
    public void Resolve_is_pure_and_ignores_current_state()
    {
        // 两次 Resolve 同一快照结果一致——历史级别不随"当前"漂移（用户令核心断言）
        var snapshot = State(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = "20",
        }, Severity.Error); // 当前已涨红

        var first = TimelineSeverity.Resolve(snapshot, 60, 90);
        var second = TimelineSeverity.Resolve(snapshot, 60, 90);
        Assert.Equal(Severity.Success, first);
        Assert.Equal(first, second);
    }
}
