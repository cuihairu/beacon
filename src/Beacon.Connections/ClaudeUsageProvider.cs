using System.Globalization;
using System.Text.Json;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>claude（Claude Code 本机用量）Widget 类型元数据（positioning P0 #5：AI Usage 一级概念）。</summary>
public static class ClaudeWidgetDescriptors
{
    public const string UsageType = "claude.usage";

    public static WidgetTypeDescriptor Usage { get; } = new()
    {
        Type = UsageType,
        DisplayName = "Claude Code 用量（本机）",
        PinSupported = true,
        SuggestedTier = RefreshTiers.Static, // 本地文件统计：手动/打开面板时重算即可
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "Claude"),
            new WidgetFieldDescriptor("days", "统计窗口（天）", Placeholder: "默认 1（今天）"),
            new WidgetFieldDescriptor("daily_cost_limit", "成本上限（美元/窗口，可选）", Placeholder: "如 35"),
            new WidgetFieldDescriptor("warn_percent", "告警阈值（%）", Placeholder: "默认 60"),
            new WidgetFieldDescriptor("error_percent", "错误阈值（%）", Placeholder: "默认 90"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Usage];
}

/// <summary>
/// claude.usage（positioning P0 #5）：读本机 Claude Code 会话记录（~/.claude/projects/**\/*.jsonl，
/// ccusage 同源数据），聚合窗口内 token 与成本——零凭据零网络。连接 Endpoint 可填自定义会话目录
///（可空 = 默认 ~/.claude/projects）。costUSD 字段存在才计成本（不自行估价，模型价目漂移快）；
/// 有 daily_cost_limit 才按用量过阈值升级别，否则恒为 Success（纯信息基线）。
/// </summary>
public sealed class ClaudeUsageProvider : IWidgetProvider
{
    private static string DefaultDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    public WidgetTypeDescriptor Descriptor => ClaudeWidgetDescriptors.Usage;

    public Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var dir = ResolveDir(connection);
        if (!Directory.Exists(dir))
        {
            throw new ConnectionException($"Claude 会话目录不存在：{dir}", ConnectionHealthState.Degraded);
        }

        var days = widget.Config.TryGetValue("days", out var daysRaw) && int.TryParse(daysRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : 1;
        var nowLocal = DateTimeOffset.Now;
        var usage = Aggregate(EnumerateLines(dir, nowLocal - TimeSpan.FromDays(days)), nowLocal, days);

        double? limit = widget.Config.TryGetValue("daily_cost_limit", out var limitRaw) && double.TryParse(limitRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var l) && l > 0 ? l : null;
        var warnPercent = widget.Config.TryGetValue("warn_percent", out var warnRaw) && double.TryParse(warnRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 60;
        var errorPercent = widget.Config.TryGetValue("error_percent", out var errorRaw) && double.TryParse(errorRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var e) ? e : 90;
        var severity = limit is { } bound ? MapSeverity(usage.Cost, bound, warnPercent, errorPercent) : Severity.Success;

        var head = widget.Config.GetValueOrDefault("label") ?? "Claude";
        var window = days == 1 ? "今日" : $"{days}天";
        return Task.FromResult<WidgetState?>(new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Error ? LifecycleState.Failed
                : severity == Severity.Warning ? LifecycleState.Running
                : LifecycleState.Success,
            Progress = null,
            Summary = Summarize(head, window, usage),
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cost"] = usage.HasCost ? usage.Cost.ToString("0.00", CultureInfo.InvariantCulture) : "",
                ["tokens_in"] = usage.InputTokens.ToString("0", CultureInfo.InvariantCulture),
                ["tokens_out"] = usage.OutputTokens.ToString("0", CultureInfo.InvariantCulture),
                ["tokens_cache_read"] = usage.CacheReadTokens.ToString("0", CultureInfo.InvariantCulture),
                ["sessions"] = usage.Sessions.ToString("0", CultureInfo.InvariantCulture),
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
        // 允许 ~ 起头的家目录写法
        return configured.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), configured[1..].TrimStart('/', '\\'))
            : configured;
    }

    /// <summary>窗口内最近修改的会话文件逐行产出（LastWriteTime 粗筛——超大目录不整读）。</summary>
    internal static IEnumerable<string> EnumerateLines(string dir, DateTimeOffset fromLocal)
    {
        var fromUtc = fromLocal.ToUniversalTime();
        var files = Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories)
            .Where(file => File.GetLastWriteTimeUtc(file) >= fromUtc);
        foreach (var file in files)
        {
            string? line;
            using var reader = new StreamReader(file);
            while ((line = reader.ReadLine()) is not null)
            {
                yield return line;
            }
        }
    }

    /// <summary>会话行归一化（纯函数供单测）：只认 assistant 行的 usage；costUSD 兼容数字与字符串。</summary>
    internal static UsageEntry? ParseLine(string json)
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
                || (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String || typeElement.GetString() != "assistant"))
            {
                return null;
            }
            if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var entry = new UsageEntry
            {
                Timestamp = root.TryGetProperty("timestamp", out var timestampElement) && timestampElement.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(timestampElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
                    ? timestamp
                    : null,
                MessageId = message.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : null,
                InputTokens = usageElement.TryGetProperty("input_tokens", out var inElement) && inElement.TryGetInt64(out var input) ? input : 0,
                OutputTokens = usageElement.TryGetProperty("output_tokens", out var outElement) && outElement.TryGetInt64(out var output) ? output : 0,
                CacheReadTokens = usageElement.TryGetProperty("cache_read_input_tokens", out var cacheReadElement) && cacheReadElement.TryGetInt64(out var cacheRead) ? cacheRead : 0,
                CacheCreateTokens = usageElement.TryGetProperty("cache_creation_input_tokens", out var cacheCreateElement) && cacheCreateElement.TryGetInt64(out var cacheCreate) ? cacheCreate : 0,
            };
            if (root.TryGetProperty("costUSD", out var costElement))
            {
                entry.Cost = costElement.ValueKind == JsonValueKind.Number && costElement.TryGetDouble(out var cost)
                    ? cost
                    : costElement.ValueKind == JsonValueKind.String && double.TryParse(costElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCost)
                        ? parsedCost
                        : null;
            }
            return entry;
        }
    }

    /// <summary>窗口聚合（纯函数供单测）：本地时区日期分桶，ageDays ∈ [0, days) 才计；按 message.id 去重（流式重放同条会重复）。</summary>
    internal static UsageSummary Aggregate(IEnumerable<string> lines, DateTimeOffset nowLocal, int days)
    {
        var summary = new UsageSummary();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var entry = ParseLine(line);
            if (entry is null)
            {
                continue;
            }
            if (entry.MessageId is { } id && !seen.Add(id))
            {
                continue;
            }
            if (entry.Timestamp is not { } timestamp)
            {
                continue;
            }
            var local = timestamp.ToLocalTime();
            var ageDays = (nowLocal.Date - local.Date).Days;
            if (ageDays < 0 || ageDays >= days)
            {
                continue; // 未来时间戳（时钟漂移）与窗口外条目不计
            }
            summary.InputTokens += entry.InputTokens;
            summary.OutputTokens += entry.OutputTokens;
            summary.CacheReadTokens += entry.CacheReadTokens;
            summary.CacheCreateTokens += entry.CacheCreateTokens;
            summary.Cost += entry.Cost ?? 0;
            summary.HasCost |= entry.Cost is not null;
            summary.Sessions++;
            if (ageDays == 0)
            {
                summary.TodaySessions++;
            }
        }
        return summary;
    }

    /// <summary>级别映射（纯函数供单测）：用量占上限比过阈值；无上限恒 Success。</summary>
    internal static Severity MapSeverity(double? cost, double limit, double warnPercent, double errorPercent)
        => cost is not { } used ? Severity.Success
            : used / limit * 100 >= errorPercent ? Severity.Error
            : used / limit * 100 >= warnPercent ? Severity.Warning
            : Severity.Success;

    /// <summary>摘要：有成本展示美元，无成本只报 token（不自行估价）。</summary>
    internal static string Summarize(string head, string window, UsageSummary usage)
    {
        var total = usage.InputTokens + usage.OutputTokens + usage.CacheReadTokens + usage.CacheCreateTokens;
        var parts = new List<string>();
        if (usage.HasCost)
        {
            parts.Add($"${usage.Cost.ToString("0.00", CultureInfo.InvariantCulture)}");
        }
        parts.Add($"{FormatTokens(total)} tok");
        parts.Add($"{usage.Sessions} 条");
        return $"{head} · {window} · " + string.Join(" · ", parts);
    }

    private static string FormatTokens(long value) => value switch
    {
        >= 1_000_000 => $"{value / 1_000_000.0:0.#}M",
        >= 1_000 => $"{value / 1_000.0:0.#}K",
        _ => value.ToString("0", CultureInfo.InvariantCulture),
    };

    internal sealed class UsageEntry
    {
        public DateTimeOffset? Timestamp { get; init; }
        public string? MessageId { get; init; }
        public long InputTokens { get; init; }
        public long OutputTokens { get; init; }
        public long CacheReadTokens { get; init; }
        public long CacheCreateTokens { get; init; }
        public double? Cost { get; set; }
    }

    internal sealed class UsageSummary
    {
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long CacheReadTokens { get; set; }
        public long CacheCreateTokens { get; set; }
        public double Cost { get; set; }
        public bool HasCost { get; set; }
        public int Sessions { get; set; }
        public int TodaySessions { get; set; }
    }
}
