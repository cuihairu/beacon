using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// codex.usage（OpenAI Codex 本机统计，第三档兜底口径）：token_count 解析、会话内累计取末值、
/// 无计数事件会话只计数不计 token、Info 卡 source=local 如实标注、目录缺失 Degraded。
/// </summary>
public sealed class CodexUsageProviderTests
{
    private const string TokenLine = """
        {"timestamp":"2026-10-07T10:00:00Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":1000,"cached_input_tokens":400,"output_tokens":200,"reasoning_tokens":50,"total_tokens":1200}}}}
        """;

    private const string TokenLine2 = """
        {"timestamp":"2026-10-07T10:05:00Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":3000,"cached_input_tokens":900,"output_tokens":700,"reasoning_tokens":150,"total_tokens":3700}}}}
        """;

    private static ConnectionConfig Connection(string endpoint) => new()
    {
        Id = "codex-local",
        Type = "codex",
        Endpoint = endpoint,
    };

    private static WidgetConfig Widget() => new()
    {
        Id = "w-codex",
        Type = CodexWidgetDescriptors.UsageType,
        ConnectionId = "codex-local",
        Config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    };

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    private static string NewSessionDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beacon-codex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ---- 行解析 ----

    [Fact]
    public void ParseLine_TokenCountLine_MapsTotals()
    {
        var total = CodexUsageProvider.ParseLine(TokenLine);

        Assert.NotNull(total);
        Assert.Equal(1000, total!.Input);
        Assert.Equal(400, total.Cached);
        Assert.Equal(200, total.Output);
        Assert.Equal(50, total.Reasoning);
    }

    [Theory]
    [InlineData("""{"type":"event_msg","payload":{"type":"agent_message","message":"hi"}}""")] // 非 token 事件
    [InlineData("""{"type":"event_msg","payload":{"type":"token_count","info":null}}""")] // info 占位为 null
    [InlineData("""{"type":"event_msg","payload":{"type":"token_count","info":{}}}""")] // 缺 total_token_usage
    [InlineData("not json")]
    public void ParseLine_NonTokenOrMalformed_ReturnsNull(string line)
        => Assert.Null(CodexUsageProvider.ParseLine(line));

    // ---- 聚合口径 ----

    [Fact]
    public void Aggregate_TakesLastCumulativePerSession()
    {
        var dir = NewSessionDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "rollout-a.jsonl"), TokenLine + "\n" + TokenLine2 + "\n");

            var summary = CodexUsageProvider.Aggregate(CodexUsageProvider.EnumerateSessions(dir, DateTimeOffset.UtcNow.AddDays(-1)));

            // 会话内 total 是累计值：只取最后一条（3000+700），不叠加
            Assert.Equal(1, summary.Sessions);
            Assert.Equal(1, summary.CountedSessions);
            Assert.Equal(3000, summary.InputTokens);
            Assert.Equal(700, summary.OutputTokens);
            Assert.Equal(900, summary.CachedInputTokens);
            Assert.Equal(150, summary.ReasoningTokens);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Aggregate_SessionWithoutTokenEvents_CountedButNoTokens()
    {
        var dir = NewSessionDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "rollout-old.jsonl"),
                "{\"type\":\"session_meta\",\"payload\":{\"id\":\"x\"}}\n"); // 旧版本会话无 token_count

            var summary = CodexUsageProvider.Aggregate(CodexUsageProvider.EnumerateSessions(dir, DateTimeOffset.UtcNow.AddDays(-1)));

            Assert.Equal(1, summary.Sessions); // 如实计入会话数
            Assert.Equal(0, summary.CountedSessions);
            Assert.Equal(0, summary.InputTokens + summary.OutputTokens);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnumerateSessions_SkipsFilesOlderThanWindow()
    {
        var dir = NewSessionDir();
        try
        {
            var old = Path.Combine(dir, "rollout-old.jsonl");
            File.WriteAllText(old, TokenLine);
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-10));

            var found = CodexUsageProvider.EnumerateSessions(dir, DateTimeOffset.UtcNow.AddDays(-1)).ToList();

            Assert.Empty(found);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---- 卡片渲染 ----

    [Fact]
    public async Task GetStateAsync_HonestLocalStatCard()
    {
        var dir = NewSessionDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "rollout-a.jsonl"), TokenLine + "\n" + TokenLine2 + "\n");
            var provider = new CodexUsageProvider();

            var state = await provider.GetStateAsync(Widget(), Connection(dir), Ctx(), CancellationToken.None);

            Assert.NotNull(state);
            Assert.Equal(Severity.Info, state!.Severity); // 本地统计纯信息：无官方上限不升级别
            Assert.Contains("本地统计", state.Summary); // 口径明示（用户令：不许编官方数字）
            Assert.Equal("local", state.Payload["source"]);
            Assert.Equal("3000", state.Payload["tokens_in"]);
            Assert.Equal("700", state.Payload["tokens_out"]);
            Assert.Equal("1", state.Payload["sessions"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStateAsync_MissingDir_ThrowsDegraded()
    {
        var provider = new CodexUsageProvider();

        var exception = await Assert.ThrowsAsync<ConnectionException>(
            () => provider.GetStateAsync(Widget(), Connection(Path.Combine(Path.GetTempPath(), "beacon-nope-" + Guid.NewGuid().ToString("N"))), Ctx(), CancellationToken.None));

        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }

    // ---- 连接测试（零网络） ----

    [Fact]
    public async Task ConnectionTest_DirExists_Healthy_Missing_Degraded()
    {
        var provider = new CodexConnectionProvider();
        var dir = NewSessionDir();
        try
        {
            Assert.Equal(ConnectionHealthState.Healthy, (await provider.TestAsync(Connection(dir), Ctx(), CancellationToken.None)).Health);
            Assert.Equal(ConnectionHealthState.Degraded, (await provider.TestAsync(
                Connection(Path.Combine(Path.GetTempPath(), "beacon-nope-" + Guid.NewGuid().ToString("N"))), Ctx(), CancellationToken.None)).Health);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
