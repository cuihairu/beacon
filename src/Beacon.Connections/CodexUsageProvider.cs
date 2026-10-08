using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>codex（OpenAI Codex CLI）Widget 类型元数据（positioning P0 #5：AI Usage 七家之一）。</summary>
public static class CodexWidgetDescriptors
{
    public const string UsageType = "codex.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "OpenAI Codex 用量（本机）",
        PinSupported = true,
        FloatingOptIn = true, // 数值/额度类：设置「数量悬浮窗」开关放行（默认关）
        SuggestedTier = RefreshTiers.Static, // 本地文件统计：手动/打开面板时重算即可
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Codex"),
            new WidgetFieldDescriptor("days", "统计窗口（天）", Placeholder: "默认 7（近一周）"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// codex.usage（positioning P0 #5）：读本机 Codex CLI 会话记录（~/.codex/sessions/**\/rollout-*.jsonl）
/// 聚合 token——零凭据零网络。**三档探测后落第三档「本地统计」**（2026-10-08 实证）：
/// ①官方用量 API：ChatGPT OAuth 侧 wham/usage 需 OAuth 凭据封装（与 Beacon「连接+凭据」模型不同构，
/// 本机亦无 platform.openai.com 计费 Key）；②控制台口复用：同需登录态；③本地统计：rollout 文件可读 → 落此档。
/// 口径如实标注「本地统计，非官方口径」（payload source=local），不编造官方数字。
/// token 口径：rollout 内 token_count 事件的 total_token_usage 是**会话内累计值**——每文件取最后一条
/// 求和；无 token 事件的会话计 0 token 但计入会话数（如实反映「有会话无计数」的旧版本文件）。
/// 连接 Endpoint 可填自定义会话目录（可空 = 默认 ~/.codex/sessions）。
/// </summary>
public sealed class CodexUsageProvider : IWidgetProvider
{
    private static string DefaultDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");

    public WidgetTypeDescriptor Descriptor => CodexWidgetDescriptors.Usage;

    public Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var dir = ResolveDir(connection);
        if (!Directory.Exists(dir))
        {
            throw new ConnectionException($"Codex 会话目录不存在：{dir}", ConnectionHealthState.Degraded);
        }

        var days = widget.Config.TryGetValue("days", out var daysRaw) && int.TryParse(daysRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : 7;
        var fromUtc = DateTimeOffset.UtcNow - TimeSpan.FromDays(days);
        var usage = Aggregate(EnumerateSessions(dir, fromUtc));

        var head = widget.Config.GetValueOrDefault("label") ?? "Codex";
        var window = days == 1 ? "今日" : $"{days}天";
        return Task.FromResult<WidgetState?>(new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = CodexWidgetDescriptors.UsageType,
            ConnectionId = connection.Id,
            Severity = Severity.Info, // 本地统计纯信息基线：无官方上限可比，不升级别（不造假红）
            Lifecycle = LifecycleState.Success,
            Summary = Summarize(head, window, usage),
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tokens_in"] = usage.InputTokens.ToString("0", CultureInfo.InvariantCulture),
                ["tokens_out"] = usage.OutputTokens.ToString("0", CultureInfo.InvariantCulture),
                ["tokens_reasoning"] = usage.ReasoningTokens.ToString("0", CultureInfo.InvariantCulture),
                ["tokens_cached"] = usage.CachedInputTokens.ToString("0", CultureInfo.InvariantCulture),
                ["sessions"] = usage.Sessions.ToString("0", CultureInfo.InvariantCulture),
                ["counted_sessions"] = usage.CountedSessions.ToString("0", CultureInfo.InvariantCulture),
                ["source"] = "local", // 如实口径：本地统计，非官方口径
                ["dir"] = dir,
            },
            FetchedAt = DateTimeOffset.UtcNow,
        });
    }

    internal static string ResolveDir(ConnectionConfig connection)
    {
        var configured = connection.Endpoint?.Trim();
        if (string.IsNullOrEmpty(configured))
        {
            return DefaultDir();
        }
        // 允许 ~ 起头的家目录写法；按目录段拆分再 Combine——Path.Combine 不归一化段内分隔符（CI 实证）
        return configured.StartsWith('~')
            ? Path.Combine([Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), .. configured[1..].Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)])
            : configured;
    }

    /// <summary>窗口内最近修改的会话文件（LastWriteTime 粗筛——mtime 用 UTC 比较，本地时间贴字面 Z 的教训）。</summary>
    internal static IEnumerable<string> EnumerateSessions(string dir, DateTimeOffset fromUtc)
        => Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories)
            .Where(file => File.GetLastWriteTimeUtc(file) >= fromUtc.ToUniversalTime());

    /// <summary>
    /// 单行解析（纯函数供单测）：只认 payload.type=token_count 且 info.total_token_usage 完整的行；
    /// info 为 null（部分事件占位）返回 null。
    /// </summary>
    internal static TokenTotal? ParseLine(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object
                || !payload.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String
                || typeElement.GetString() != "token_count"
                || !payload.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object
                || !info.TryGetProperty("total_token_usage", out var total) || total.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            return new TokenTotal(
                Input: total.TryGetProperty("input_tokens", out var input) && input.TryGetInt64(out var i) ? i : 0,
                Cached: total.TryGetProperty("cached_input_tokens", out var cached) && cached.TryGetInt64(out var c) ? c : 0,
                Output: total.TryGetProperty("output_tokens", out var output) && output.TryGetInt64(out var o) ? o : 0,
                Reasoning: total.TryGetProperty("reasoning_tokens", out var reasoning) && reasoning.TryGetInt64(out var r) ? r : 0);
        }
    }

    /// <summary>聚合（纯函数供单测）：每文件取最后一条累计值求和（会话内 total 是累积口径）；
    /// 文件计入会话数，无 token 事件的文件只占会话数不加 token。</summary>
    internal static UsageSummary Aggregate(IEnumerable<string> files)
    {
        var summary = new UsageSummary();
        foreach (var file in files)
        {
            summary.Sessions++;
            TokenTotal? last = null;
            string? line;
            using var reader = new StreamReader(file);
            while ((line = reader.ReadLine()) is not null)
            {
                var parsed = ParseLine(line);
                if (parsed is not null)
                {
                    last = parsed;
                }
            }
            if (last is not { } total)
            {
                continue;
            }
            summary.CountedSessions++;
            summary.InputTokens += total.Input;
            summary.CachedInputTokens += total.Cached;
            summary.OutputTokens += total.Output;
            summary.ReasoningTokens += total.Reasoning;
        }
        return summary;
    }

    internal static string Summarize(string head, string window, UsageSummary usage)
        => $"{head} · {window} · {usage.Sessions} 会话 · {FormatTokens(usage.InputTokens + usage.OutputTokens)} tok · 本地统计";

    private static string FormatTokens(long value) => value switch
    {
        >= 1_000_000 => $"{value / 1_000_000.0:0.#}M",
        >= 1_000 => $"{value / 1_000.0:0.#}K",
        _ => value.ToString("0", CultureInfo.InvariantCulture),
    };

    internal sealed record TokenTotal(long Input, long Cached, long Output, long Reasoning);

    internal sealed class UsageSummary
    {
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long CachedInputTokens { get; set; }
        public long ReasoningTokens { get; set; }
        public int Sessions { get; set; }
        public int CountedSessions { get; set; }
    }
}

/// <summary>codex 连接（本机统计）：零网络零凭据——会话目录存在即 Healthy（同 claude 口径）。</summary>
public sealed class CodexConnectionProvider : IConnectionProvider
{
    public string ConnectionType => "codex";

    public Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
        => Task.FromResult(Directory.Exists(CodexUsageProvider.ResolveDir(connection))
            ? ConnectionHealthState.Healthy
            : ConnectionHealthState.Degraded);
}
