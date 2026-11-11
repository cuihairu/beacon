using System.Globalization;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>claude.usage（本机会话 JSONL 用量，positioning P0 #5）：行解析、去重聚合、窗口过滤、阈值升级、本地目录 IO。</summary>
public sealed class ClaudeUsageProviderTests
{
    // 固定“现在”（UTC 口径），夹具时间戳全部相对它构造
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private const string LineToday = """
        {"type":"assistant","message":{"id":"msg_1","usage":{"input_tokens":100,"output_tokens":50,"cache_read_input_tokens":2000,"cache_creation_input_tokens":300}},"costUSD":0.25,"timestamp":"2026-10-08T10:00:00Z"}
        """;

    private const string LineTodayDuplicate = """
        {"type":"assistant","message":{"id":"msg_1","usage":{"input_tokens":100,"output_tokens":50,"cache_read_input_tokens":2000,"cache_creation_input_tokens":300}},"costUSD":0.25,"timestamp":"2026-10-08T10:00:00Z"}
        """; // 仅供 Aggregate 去重用例

    private const string LineYesterday = """
        {"type":"assistant","message":{"id":"msg_2","usage":{"input_tokens":10,"output_tokens":5}},"costUSD":"0.75","timestamp":"2026-10-07T09:00:00Z"}
        """;

    private const string LineUser = """
        {"type":"user","message":{"role":"user"},"timestamp":"2026-10-08T10:00:00Z"}
        """;

    private const string LineNoUsage = """
        {"type":"assistant","message":{"id":"msg_3"},"timestamp":"2026-10-08T11:00:00Z"}
        """;

    private const string LineFuture = """
        {"type":"assistant","message":{"id":"msg_4","usage":{"input_tokens":999}},"timestamp":"2026-10-09T10:00:00Z"}
        """;

    private static ConnectionConfig Connection(string? endpoint) => new()
    {
        Id = "claude-local",
        Type = "claude",
        Endpoint = endpoint, // 可空 = 默认 ~/.claude/projects
    };

    private static WidgetConfig Widget(Dictionary<string, string>? config = null) => new()
    {
        Id = "w-claude",
        Type = ClaudeWidgetDescriptors.UsageType,
        ConnectionId = "claude-local",
        Config = config ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    // ---- 行解析 ----

    [Fact]
    public void ParseLine_AssistantUsage_ReadsAllFields()
    {
        var entry = ClaudeUsageProvider.ParseLine(LineToday)!;

        Assert.Equal("msg_1", entry.MessageId);
        Assert.Equal(100, entry.InputTokens);
        Assert.Equal(50, entry.OutputTokens);
        Assert.Equal(2000, entry.CacheReadTokens);
        Assert.Equal(300, entry.CacheCreateTokens);
        Assert.Equal(0.25, entry.Cost);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero), entry.Timestamp);
    }

    [Fact]
    public void ParseLine_StringCostUSD_ParsesToo()
    {
        var entry = ClaudeUsageProvider.ParseLine(LineYesterday)!;

        Assert.Equal(0.75, entry.Cost);
        Assert.Equal(10, entry.InputTokens);
    }

    [Theory]
    [InlineData(LineUser)] // 非 assistant 行
    [InlineData(LineNoUsage)] // 无 usage
    [InlineData("{not json")] // 坏行
    [InlineData("")] // 空行
    public void ParseLine_NonUsageLines_ReturnNull(string json)
    {
        Assert.Null(ClaudeUsageProvider.ParseLine(json));
    }

    // ---- 聚合 ----

    [Fact]
    public void Aggregate_DedupsSumsAndFiltersWindow()
    {
        var lines = new[] { LineToday, LineTodayDuplicate, LineYesterday, LineUser, "{not json", LineNoUsage, LineFuture };

        // 窗口 1 天：只计今天（msg_1），去重掉流式重放
        var today = ClaudeUsageProvider.Aggregate(lines, Now, 1);
        Assert.Equal(100, today.InputTokens);
        Assert.Equal(50, today.OutputTokens);
        Assert.Equal(2000, today.CacheReadTokens);
        Assert.Equal(300, today.CacheCreateTokens);
        Assert.Equal(0.25, today.Cost);
        Assert.True(today.HasCost);
        Assert.Equal(1, today.Sessions);
        Assert.Equal(1, today.TodaySessions);

        // 窗口 2 天：昨日（msg_2）也入账
        var twoDays = ClaudeUsageProvider.Aggregate(lines, Now, 2);
        Assert.Equal(110, twoDays.InputTokens);
        Assert.Equal(1.00, twoDays.Cost);
        Assert.Equal(2, twoDays.Sessions);
        Assert.Equal(1, twoDays.TodaySessions);
    }

    [Fact]
    public void Aggregate_NoCostEntries_HasCostFalse()
    {
        var summary = ClaudeUsageProvider.Aggregate([LineNoUsage], Now, 1);

        Assert.False(summary.HasCost);
        Assert.Equal(0, summary.Sessions);
    }

    // ---- 级别阈值 ----

    public static TheoryData<double?, double, double, double, Severity> SeverityCases => new()
    {
        { null, 35, 60, 90, Severity.Success }, // 无成本记录 → 纯信息基线
        { 10, 35, 60, 90, Severity.Success }, // ≈28.6%
        { 22, 35, 60, 90, Severity.Warning }, // ≈62.9%
        { 28, 35, 60, 90, Severity.Warning }, // =80%
        { 31.6, 35, 60, 90, Severity.Error }, // ≈90.3%（贴边不入边界，避免浮点抖动）
    };

    [Theory]
    [MemberData(nameof(SeverityCases))]
    public void MapSeverity_Thresholds(double? cost, double limit, double warn, double error, Severity expected)
    {
        Assert.Equal(expected, ClaudeUsageProvider.MapSeverity(cost, limit, warn, error));
    }

    // ---- 摘要 ----

    [Fact]
    public void Summarize_WithAndWithoutCost()
    {
        var withCost = ClaudeUsageProvider.Aggregate([LineToday, LineYesterday], Now, 2);
        Assert.Equal("Claude · 2天 · $1.00 · 2.5K tok · 2 条", ClaudeUsageProvider.Summarize("Claude", "2天", withCost));

        var noCost = new ClaudeUsageProvider.UsageSummary { InputTokens = 1500, OutputTokens = 500 };
        Assert.Equal("Claude · 今日 · 2K tok · 0 条", ClaudeUsageProvider.Summarize("Claude", "今日", noCost));
    }

    // ---- 目录解析与连接测试 ----

    [Theory]
    [InlineData("~/.claude/projects")]
    [InlineData("~\\.claude\\projects")] // Windows 分隔符写法同样归一化（CI 实证：Path.Combine 不归一化段内分隔符）
    [InlineData("~/.claude/projects/")]  // 尾分隔符不产生空段
    public void ResolveDir_TildeExpandsToProfile(string endpoint)
    {
        var resolved = ClaudeUsageProvider.ResolveDir(new ConnectionConfig { Id = "c", Type = "claude", Endpoint = endpoint });

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "projects");
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public async Task TestAsync_HealthyWhenDirExists_DegradedWhenMissing()
    {
        var provider = new ClaudeConnectionProvider();
        var temp = Path.Combine(Path.GetTempPath(), "beacon-claude-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            var healthy = (await provider.TestAsync(Connection(temp), Ctx(), CancellationToken.None)).Health;
            var missing = (await provider.TestAsync(Connection(Path.Combine(temp, "nope")), Ctx(), CancellationToken.None)).Health;

            Assert.Equal(ConnectionHealthState.Healthy, healthy);
            Assert.Equal(ConnectionHealthState.Degraded, missing);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    // ---- 端到端（真实目录 IO）----

    [Fact]
    public async Task GetStateAsync_CancellationRequested_StopsScanImmediately()
    {
        // P1 审核修复（ClaudeUsageProvider.EnumerateLines）：取消 token 逐文件逐行检查——
        // days 配大时扫描可被打断，不再刷完整目录。
        var dir = Path.Combine(Path.GetTempPath(), "beacon-claude-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var fresh = Path.Combine(dir, "fresh.jsonl");
            await File.WriteAllTextAsync(fresh, "{}");

            using var cancelled = new CancellationTokenSource(TimeSpan.Zero); // 进门即取消

            // 取消即抛 OCE（与 HTTP provider 同口径：token 交底层，取消上抛由 WidgetHost 兜）；
            // 关键是「立即」——扫描在第一个文件第一个读点就断，不把目录刷完。
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => new ClaudeUsageProvider().GetStateAsync(Widget(), Connection(dir), Ctx(), cancelled.Token));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_ReadsFreshFilesOnly_PayloadAndSummary()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beacon-claude-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            // 必须是真 UTC + 字面 Z：Now（本地）贴 Z 会被解析成 UTC，本地 16:00 后 ToLocalTime 落到明天 → ageDays=-1 全被过滤
            var todayStamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var fresh = Path.Combine(dir, "fresh.jsonl");
            await File.WriteAllLinesAsync(fresh,
            [
                $"{{\"type\":\"assistant\",\"message\":{{\"id\":\"m1\",\"usage\":{{\"input_tokens\":100,\"output_tokens\":50,\"cache_read_input_tokens\":2000,\"cache_creation_input_tokens\":300}}}},\"costUSD\":0.25,\"timestamp\":\"{todayStamp}\"}}",
                LineUser.Replace("2026-10-08T10:00:00Z", todayStamp), // 非 assistant 行跳过
            ]);
            var stale = Path.Combine(dir, "stale.jsonl");
            await File.WriteAllTextAsync(stale, LineYesterday);
            File.SetLastWriteTimeUtc(stale, DateTimeOffset.UtcNow.AddDays(-5).UtcDateTime); // mtime 粗筛直接排除

            var state = await new ClaudeUsageProvider().GetStateAsync(Widget(), Connection(dir), Ctx(), CancellationToken.None);

            Assert.NotNull(state);
            Assert.Equal(Severity.Success, state.Severity); // 未配上限 → 纯信息基线
            Assert.Equal(LifecycleState.Success, state.Lifecycle);
            Assert.Equal($"Claude · 今日 · $0.25 · 2.5K tok · 1 条", state.Summary);
            Assert.Equal(dir, state.Payload["dir"]);
            Assert.Equal("0.25", state.Payload["cost"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_CostLimitEscalatesSeverity()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beacon-claude-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var todayStamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", CultureInfo.InvariantCulture); // 真 UTC，同上
            await File.WriteAllTextAsync(Path.Combine(dir, "s.jsonl"),
                $"{{\"type\":\"assistant\",\"message\":{{\"id\":\"m1\",\"usage\":{{\"input_tokens\":1}}}},\"costUSD\":0.25,\"timestamp\":\"{todayStamp}\"}}");

            var warning = await new ClaudeUsageProvider().GetStateAsync(
                Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["daily_cost_limit"] = "0.3" }),
                Connection(dir), Ctx(), CancellationToken.None);
            var success = await new ClaudeUsageProvider().GetStateAsync(
                Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["daily_cost_limit"] = "35" }),
                Connection(dir), Ctx(), CancellationToken.None);

            Assert.Equal(Severity.Warning, warning!.Severity); // 0.25/0.3 ≈ 83%
            Assert.Equal(LifecycleState.Running, warning.Lifecycle);
            Assert.Equal(Severity.Success, success!.Severity);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_MissingDir_ThrowsDegraded()
    {
        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => new ClaudeUsageProvider().GetStateAsync(
                Widget(), Connection(Path.Combine(Path.GetTempPath(), "beacon-claude-missing-" + Guid.NewGuid().ToString("N"))), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // ---- 配置归一化（days / warn_percent / error_percent 分支补全）----

    /// <summary>写临时目录放一条「今天」会话行（真 UTC 时间戳；costUsd 为原始 JSON 数字字面量）。</summary>
    private static string FreshDirWithLine(string costUsd)
    {
        var dir = Path.Combine(Path.GetTempPath(), "beacon-claude-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        File.WriteAllText(Path.Combine(dir, "s.jsonl"),
            $"{{\"type\":\"assistant\",\"message\":{{\"id\":\"m1\",\"usage\":{{\"input_tokens\":1}}}},\"costUSD\":{costUsd},\"timestamp\":\"{stamp}\"}}");
        return dir;
    }

    private static string YesterdayStamp()
        => DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("abc")]  // 非数字 → TryParse 失败
    [InlineData("0")]    // 非正 → d > 0 失败
    [InlineData("-3")]   // 负数 → d > 0 失败
    public async Task GetStateAsync_InvalidDaysConfig_DefaultsToOneDayWindow(string daysRaw)
    {
        var dir = FreshDirWithLine("0.25");
        try
        {
            // 昨日行：窗口若被错误放宽会误入账；默认 1 天窗口必须只计今天
            await File.WriteAllTextAsync(Path.Combine(dir, "y.jsonl"),
                $"{{\"type\":\"assistant\",\"message\":{{\"id\":\"m2\",\"usage\":{{\"input_tokens\":1}}}},\"costUSD\":0.75,\"timestamp\":\"{YesterdayStamp()}\"}}");

            var state = await new ClaudeUsageProvider().GetStateAsync(
                Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["days"] = daysRaw }),
                Connection(dir), Ctx(), CancellationToken.None);

            Assert.NotNull(state);
            Assert.Contains("今日", state.Summary); // 无效 days → 回退 1 天窗口措辞
            Assert.EndsWith("1 条", state.Summary); // 昨日被排除
            Assert.Equal("1", state.Payload["sessions"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_ValidMultiDayWindow_CountsYesterdayAndLabels()
    {
        var dir = FreshDirWithLine("0.25");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "y.jsonl"),
                $"{{\"type\":\"assistant\",\"message\":{{\"id\":\"m2\",\"usage\":{{\"input_tokens\":1}}}},\"costUSD\":0.75,\"timestamp\":\"{YesterdayStamp()}\"}}");

            var state = await new ClaudeUsageProvider().GetStateAsync(
                Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["days"] = "2" }),
                Connection(dir), Ctx(), CancellationToken.None);

            Assert.Contains("2天", state!.Summary); // days != 1 → N 天措辞
            Assert.EndsWith("2 条", state.Summary); // 昨日入账
            Assert.Equal("2", state.Payload["sessions"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_ValidWarnPercent_LowersWarningThreshold()
    {
        var dir = FreshDirWithLine("0.5");
        try
        {
            // 用量 50%：默认 warn 60 → Success，配 warn 45 → Warning
            var state = await new ClaudeUsageProvider().GetStateAsync(
                Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["daily_cost_limit"] = "1", ["warn_percent"] = "45" }),
                Connection(dir), Ctx(), CancellationToken.None);

            Assert.Equal(Severity.Warning, state!.Severity);
            Assert.Equal(LifecycleState.Running, state.Lifecycle);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_ValidErrorPercent_LowersErrorThreshold()
    {
        var dir = FreshDirWithLine("0.5");
        try
        {
            // 用量 50%：默认 error 90 → Success，配 error 40 → Error
            var state = await new ClaudeUsageProvider().GetStateAsync(
                Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["daily_cost_limit"] = "1", ["error_percent"] = "40" }),
                Connection(dir), Ctx(), CancellationToken.None);

            Assert.Equal(Severity.Error, state!.Severity);
            Assert.Equal(LifecycleState.Failed, state.Lifecycle);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_InvalidWarnAndErrorPercent_FallBackToDefaultThresholds()
    {
        var dir = FreshDirWithLine("0.7");
        try
        {
            // 用量 70%：warn/error 均无效 → 回退默认 60/90 → Warning（70 ≥ 60，< 90）
            var state = await new ClaudeUsageProvider().GetStateAsync(
                Widget(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["daily_cost_limit"] = "1", ["warn_percent"] = "abc", ["error_percent"] = "xyz" }),
                Connection(dir), Ctx(), CancellationToken.None);

            Assert.Equal(Severity.Warning, state!.Severity);
            Assert.Equal(LifecycleState.Running, state.Lifecycle);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---- 端点归一化（Endpoint null / 空白 分支补全）----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveDir_NullOrEmptyEndpoint_ReturnsDefaultDir(string? endpoint)
    {
        var resolved = ClaudeUsageProvider.ResolveDir(new ConnectionConfig { Id = "c", Type = "claude", Endpoint = endpoint });

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "projects");
        Assert.Equal(expected, resolved);
    }

    // ---- costUSD 非数字形状（L165 分支补全）----

    [Fact]
    public void ParseLine_NonNumericStringCostUSD_CostIsNull()
    {
        var entry = ClaudeUsageProvider.ParseLine("""{"type":"assistant","message":{"id":"m","usage":{"input_tokens":5}},"costUSD":"abc","timestamp":"2026-10-08T10:00:00Z"}""")!;

        Assert.NotNull(entry);
        Assert.Null(entry.Cost); // 字符串数字解析失败 → 不估价
        Assert.Equal("m", entry.MessageId);
    }

    [Fact]
    public void ParseLine_NonScalarCostUSD_CostIsNull()
    {
        var entry = ClaudeUsageProvider.ParseLine("""{"type":"assistant","message":{"id":"m","usage":{"input_tokens":5}},"costUSD":{"amount":1},"timestamp":"2026-10-08T10:00:00Z"}""")!;

        Assert.NotNull(entry);
        Assert.Null(entry.Cost); // 非数字非字符串 → 不估价
    }

    // ---- 大数 token 格式化（FormatTokens M 臂补全）----

    [Fact]
    public void Summarize_MillionTokens_FormatsCompactM()
    {
        var summary = new ClaudeUsageProvider.UsageSummary { InputTokens = 1_500_000, OutputTokens = 0, Sessions = 1 };

        var text = ClaudeUsageProvider.Summarize("Claude", "今日", summary);

        Assert.Contains("1.5M tok", text);
    }
}
