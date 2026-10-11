using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>
/// YAML provider DSL（positioning §6 原设计口径落地，批3-补42）：per-provider `mapping` 声明式字段映射
/// + `actions`（含 retry）动作配置。**可选声明层**：连接 Settings 不配 `dsl_path` 即零变化（既有
/// http.status/http.quota 的 widget.Config 路径逐字节等价）；配了则以 DSL mapping 作为缺省值，
/// widget.Config 实例值仍优先（声明层不夺实例覆盖权）。
/// 零第三方依赖：内置极小 YAML 子集解析器（缩进映射 / `- ` 块列表 / 引号标量 / 行内注释），
/// 支持外的语法（flow、锚点、块标量、Tab 缩进等）fail-loud 带行号——配置错误要人管，不静默猜。
/// mapping 只管**数据提取**（路径/词表/单位）；label/icon/阈值仍属组件实例，不进 DSL。
/// </summary>
public static class ProviderDsl
{
    /// <summary>连接 Settings 键：指向 DSL YAML 文件路径（空/缺省 = 不启用声明层）。</summary>
    public const string SettingKey = "dsl_path";

    /// <summary>mapping 允许的键 = http.status/http.quota 的数据提取键并集（未知键 fail-loud，防拼错静默失效）。</summary>
    public static readonly IReadOnlyList<string> KnownMappingKeys =
    [
        "status_path", "summary_path", "url_path",
        "success_values", "warning_values", "error_values",
        "total_path", "used_path", "remaining_path", "reset_path", "unit",
    ];

    /// <summary>actions 允许的类型 = 现有执行器链路里适合声明式配置的三种（gh.workflow_* 需要 runId 语义，不开放）。</summary>
    public static readonly IReadOnlyList<string> KnownActionTypes = ["open.url", "http", "webhook"];

    public static ProviderDslDocument Load(string text)
    {
        var root = YamlLite.Parse(text);
        if (!root.IsMap)
        {
            throw YamlLite.Fail(1, "顶层必须是映射（provider/mapping/actions）");
        }
        foreach (var key in root.Map.Keys)
        {
            if (key is not ("provider" or "mapping" or "actions"))
            {
                throw YamlLite.Fail(1, $"未知顶层键「{key}」（允许 provider/mapping/actions）");
            }
        }
        var provider = root.Text("provider");
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw YamlLite.Fail(1, "缺少 provider 名称（日志与校验用）");
        }

        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var mappingNode = root.Child("mapping");
        if (mappingNode is { IsMap: true })
        {
            foreach (var (key, value) in mappingNode.Map)
            {
                if (!KnownMappingKeys.Contains(key, StringComparer.Ordinal))
                {
                    throw YamlLite.Fail(1, $"未知 mapping 键「{key}」（允许：{string.Join("、", KnownMappingKeys)}）");
                }
                if (!value.IsScalar || value.Scalar.Length == 0)
                {
                    throw YamlLite.Fail(1, $"mapping「{key}」的值必须是标量");
                }
                mapping[key] = value.Scalar;
            }
        }
        else if (mappingNode is { IsMap: false })
        {
            throw YamlLite.Fail(1, "mapping 必须是映射");
        }

        var actions = new List<DslAction>();
        var actionsNode = root.Child("actions");
        if (actionsNode is { IsList: true })
        {
            foreach (var item in actionsNode.List)
            {
                actions.Add(ParseAction(item));
            }
        }
        else if (actionsNode is { IsList: false })
        {
            throw YamlLite.Fail(1, "actions 必须是列表（- id: …）");
        }
        return new ProviderDslDocument(provider, mapping, actions);
    }

    private static DslAction ParseAction(YamlNode item)
    {
        if (!item.IsMap)
        {
            throw YamlLite.Fail(1, "actions 列表项必须是映射（- id: …）");
        }
        var id = item.Text("id");
        var type = item.Text("type");
        if (string.IsNullOrWhiteSpace(id))
        {
            throw YamlLite.Fail(1, "action 缺少 id");
        }
        if (string.IsNullOrWhiteSpace(type))
        {
            throw YamlLite.Fail(1, $"action「{id}」缺少 type");
        }
        if (!KnownActionTypes.Contains(type, StringComparer.Ordinal))
        {
            throw YamlLite.Fail(1, $"action「{id}」类型「{type}」不支持（允许：{string.Join("、", KnownActionTypes)}）");
        }
        var url = item.Text("url");
        if (string.IsNullOrWhiteSpace(url))
        {
            throw YamlLite.Fail(1, $"action「{id}」缺少 url（模板支持 {{var}} 占位）");
        }
        var confirmText = item.Text("confirm");
        if (confirmText is not null && confirmText is not ("true" or "false"))
        {
            throw YamlLite.Fail(1, $"action「{id}」confirm 只接受 true/false");
        }
        // 确认默认：open.url 只开浏览器无副作用默认免确认；http/webhook 触发远端动作默认要确认
        var confirm = confirmText is { Length: > 0 } ? confirmText == "true" : type != "open.url";

        var retryNode = item.Child("retry");
        if (retryNode is { IsMap: false })
        {
            throw YamlLite.Fail(1, $"action「{id}」retry 必须是映射");
        }
        var attempts = ReadInt(retryNode, "max_attempts", 1, 1, 10);
        var backoff = ReadInt(retryNode, "backoff_seconds", 5, 0, 300);
        return new DslAction(
            id, type!, item.Text("name"), url!, item.Text("method"), item.Text("body"),
            confirm, attempts, backoff);
    }

    private static int ReadInt(YamlNode? node, string key, int fallback, int min, int max)
    {
        var text = node?.Text(key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }
        if (!int.TryParse(text, out var value) || value < min || value > max)
        {
            throw YamlLite.Fail(1, $"{key} 必须是 {min}–{max} 的整数（得到「{text}」）");
        }
        return value;
    }

    /// <summary>DSL 动作 → 既有 Action 执行器链路的 ActionConfig（Id 带连接前缀防撞；重试经参数进 Runner）。</summary>
    public static ActionConfig ToActionConfig(this DslAction action, string connectionId)
    {
        var config = new ActionConfig
        {
            Id = $"dsl.{connectionId}.{action.Id}",
            Type = action.Type,
            Name = action.Name ?? action.Id,
            RequireConfirmation = action.RequireConfirmation,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["url"] = action.Url,
                ["connection_id"] = connectionId, // 只在该连接的组件详情页显示
                ["retry_max_attempts"] = action.RetryMaxAttempts.ToString("0"),
                ["retry_backoff_seconds"] = action.RetryBackoffSeconds.ToString("0"),
            },
        };
        if (action.Method is { Length: > 0 })
        {
            config.Parameters["method"] = action.Method;
        }
        if (action.Body is { Length: > 0 })
        {
            config.Parameters["body"] = action.Body;
        }
        return config;
    }

    // —— 文件加载与缓存（mtime+size 变了才重解析；坏文件 fail-loud 由调用方落卡面） ——

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (DateTimeOffset Modified, long Size, ProviderDslDocument Document)> Cache = new(StringComparer.Ordinal);

    public static ProviderDslDocument LoadFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new ConnectionException($"DSL 文件不存在：{path}（连接 Settings dsl_path 指向的声明文件）", ConnectionHealthState.Degraded);
        }
        lock (Gate)
        {
            if (Cache.TryGetValue(path, out var cached)
                && cached.Modified == info.LastWriteTimeUtc && cached.Size == info.Length)
            {
                return cached.Document;
            }
        }
        ProviderDslDocument document;
        try
        {
            document = Load(File.ReadAllText(path));
        }
        catch (YamlLiteException exception)
        {
            throw new ConnectionException($"DSL 文件解析失败（{Path.GetFileName(path)}）：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }
        lock (Gate)
        {
            Cache[path] = (info.LastWriteTimeUtc, info.Length, document);
        }
        return document;
    }

    /// <summary>声明层取值：widget.Config 实例值优先，其次 DSL mapping（dsl_path 未配返回 null——默认路径零变化）。</summary>
    public static string? MappingValue(WidgetConfig widget, ConnectionConfig connection, string key)
    {
        var own = widget.Config.GetValueOrDefault(key);
        if (own is { Length: > 0 })
        {
            return own;
        }
        var path = connection.Settings.TryGetValue(SettingKey, out var settingsPath)
            && !string.IsNullOrWhiteSpace(settingsPath)
                ? settingsPath
                : null;
        if (path is null)
        {
            return null;
        }
        return LoadFile(path).Mapping.GetValueOrDefault(key);
    }

    /// <summary>
    /// 生效配置视图：dsl_path 未配 → 原样返回 widget.Config（零变化路径）；配了 → DSL mapping 为底、
    /// 实例 Config 逐键覆盖（声明层不夺实例覆盖权）。每次检测加载一次（LoadFile 有 mtime+size 缓存）。
    /// </summary>
    public static IReadOnlyDictionary<string, string> EffectiveConfig(WidgetConfig widget, ConnectionConfig connection)
    {
        var path = connection.Settings.TryGetValue(SettingKey, out var settingsPath)
            && !string.IsNullOrWhiteSpace(settingsPath)
                ? settingsPath
                : null;
        if (path is null)
        {
            return widget.Config;
        }
        var mapping = LoadFile(path).Mapping;
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in mapping)
        {
            merged[key] = value;
        }
        foreach (var (key, value) in widget.Config)
        {
            merged[key] = value;
        }
        return merged;
    }
}

/// <summary>DSL 文档：provider 名称 + 数据提取映射 + 声明式动作（含 retry）。</summary>
public sealed record ProviderDslDocument(
    string Provider,
    IReadOnlyDictionary<string, string> Mapping,
    IReadOnlyList<DslAction> Actions);

/// <summary>DSL 声明的单个动作（url 支持模板占位，执行时走既有 TemplateRenderer）。</summary>
public sealed record DslAction(
    string Id,
    string Type,
    string? Name,
    string Url,
    string? Method,
    string? Body,
    bool RequireConfirmation,
    int RetryMaxAttempts,
    int RetryBackoffSeconds);

/// <summary>极小 YAML 子集解析异常（带行号，由 ProviderDsl.LoadFile 包成 Degraded）。</summary>
public sealed class YamlLiteException(string message) : Exception(message)
{
    public static YamlLiteException At(int line, string message) => new($"第 {line} 行：{message}");
}

/// <summary>
/// 零依赖极小 YAML 子集（只服务 provider DSL，刻意不支持完整 YAML）：
/// 缩进映射 / `- ` 块列表（列表项为映射）/ 引号标量（"…" 与 '…'，不处理转义）/ 行内注释（引号外 ` #`）。
/// 遇到 Tab 缩进、flow（{…}/[…]）、锚点别名（&amp;/\*）、块标量（|/>）、文档分隔符（---）即 fail-loud。
/// </summary>
public static class YamlLite
{
    public static YamlLiteException Fail(int line, string message) => YamlLiteException.At(line, message);

    private sealed record Line(int No, int Indent, string Content);

    public static YamlNode Parse(string text)
    {
        var lines = new List<Line>();
        var raw = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < raw.Length; i++)
        {
            var source = raw[i];
            var indent = CountIndent(source, i + 1);
            var content = StripComment(source, i + 1).Trim();
            if (content.Length == 0)
            {
                continue;
            }
            if (content is "---" or "...")
            {
                throw Fail(i + 1, "不支持多文档（---）");
            }
            lines.Add(new Line(i + 1, indent, content));
        }
        if (lines.Count == 0)
        {
            throw Fail(1, "空文档");
        }
        var pos = 0;
        var node = ParseBlock(lines, ref pos, lines[0].Indent);
        if (pos != lines.Count)
        {
            throw Fail(lines[pos].No, "缩进层级不一致（未对齐任何外层）");
        }
        return node;
    }

    private static int CountIndent(string line, int no)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
            {
                count++;
                continue;
            }
            if (ch == '\t')
            {
                throw Fail(no, "缩进用了 Tab（只允许空格）");
            }
            break;
        }
        return count;
    }

    private static YamlNode ParseBlock(List<Line> lines, ref int pos, int indent)
        => lines[pos].Content.StartsWith('-') ? ParseSequence(lines, ref pos, indent) : ParseMapping(lines, ref pos, indent);

    private static YamlNode ParseMapping(List<Line> lines, ref int pos, int indent)
    {
        var map = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        while (pos < lines.Count && lines[pos].Indent == indent)
        {
            var content = lines[pos].Content;
            if (content.StartsWith('-'))
            {
                break; // 同层混入列表项：交回上层报「缩进不一致」
            }
            var colon = SplitKeyColon(content, lines[pos].No);
            var key = content[..colon].Trim();
            ValidateKey(key, lines[pos].No);
            if (map.ContainsKey(key))
            {
                throw Fail(lines[pos].No, $"重复键「{key}」");
            }
            var rest = content[(colon + 1)..].Trim();
            pos++;
            if (rest.Length > 0)
            {
                map[key] = new YamlNode(Scalar(rest, lines[pos - 1].No));
                continue;
            }
            // 键后无值：下一行更深缩进 = 子块；否则空标量
            if (pos < lines.Count && lines[pos].Indent > indent)
            {
                map[key] = ParseBlock(lines, ref pos, lines[pos].Indent);
            }
            else
            {
                map[key] = new YamlNode("");
            }
        }
        return new YamlNode(map);
    }

    private static YamlNode ParseSequence(List<Line> lines, ref int pos, int indent)
    {
        var items = new List<YamlNode>();
        while (pos < lines.Count && lines[pos].Indent == indent && lines[pos].Content.StartsWith('-'))
        {
            var content = lines[pos].Content;
            // 只认「- key: value」（dash + 恰好一个空格）：键的列号 = indent + 2，同项后续键对齐到该列
            if (content.Length < 3 || content[1] != ' ' || content[2] == ' ')
            {
                throw Fail(lines[pos].No, "列表项必须是「- key: value」（dash 后恰好一个空格）");
            }
            var rest = content[2..];
            if (TrySplitKeyColon(rest) < 0)
            {
                throw Fail(lines[pos].No, "列表项必须是「- key: value」（不支持纯标量列表）");
            }
            var itemIndent = indent + 2;
            lines[pos] = lines[pos] with { Indent = itemIndent, Content = rest };
            items.Add(ParseMapping(lines, ref pos, itemIndent));
        }
        return new YamlNode(items);
    }

    private static int SplitKeyColon(string content, int no)
        => TrySplitKeyColon(content) is var at && at >= 0
            ? at
            : throw Fail(no, $"缺少「key:」冒号：{content}");

    /// <summary>引号感知地找「: 」（冒号后跟空格或行尾）；没有返回 -1。</summary>
    private static int TrySplitKeyColon(string content)
    {
        var inQuote = '\0';
        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (inQuote != '\0')
            {
                if (ch == inQuote)
                {
                    inQuote = '\0';
                }
                continue;
            }
            if (ch is '"' or '\'')
            {
                inQuote = ch;
                continue;
            }
            if (ch == ':' && (i + 1 == content.Length || content[i + 1] == ' '))
            {
                return i;
            }
        }
        return -1;
    }

    private static void ValidateKey(string key, int no)
    {
        if (key.Length == 0 || key.Contains(' '))
        {
            throw Fail(no, $"键不合法：「{key}」");
        }
    }

    /// <summary>引号外 ` #` 起为注释（行首 # 也算）；引号内 # 保留。</summary>
    private static string StripComment(string line, int no)
    {
        var inQuote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuote != '\0')
            {
                if (ch == inQuote)
                {
                    inQuote = '\0';
                }
                continue;
            }
            if (ch is '"' or '\'')
            {
                inQuote = ch;
                continue;
            }
            if (ch == '#' && (i == 0 || line[i - 1] == ' '))
            {
                return line[..i];
            }
        }
        if (inQuote != '\0')
        {
            throw Fail(no, "引号未闭合");
        }
        return line;
    }

    private static string Scalar(string text, int no)
    {
        // 引号优先剥除：引号内是字面量（JSON body 等以 { 开头合法），不做 flow/块标量检查
        if (text.Length >= 2 && (text[0] == '"' && text[^1] == '"' || text[0] == '\'' && text[^1] == '\''))
        {
            return text[1..^1];
        }
        if (text.StartsWith('{') || text.StartsWith('['))
        {
            throw Fail(no, "不支持 flow 语法（{…}/[…]）：字面量字符串请加引号，结构请用缩进块");
        }
        if (text.StartsWith('|') || text.StartsWith('>'))
        {
            throw Fail(no, "不支持块标量（|/>）");
        }
        return text;
    }
}

/// <summary>解析产物：映射 / 列表 / 标量 三态节点（按 Kind 取用，取错抛 InvalidOperationException 由测试暴露）。</summary>
public sealed class YamlNode
{
    private YamlNode(
        IReadOnlyDictionary<string, YamlNode>? map,
        IReadOnlyList<YamlNode>? list,
        string scalar)
    {
        Map = map ?? new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        List = list ?? [];
        Scalar = scalar;
    }

    public YamlNode(string scalar) : this(null, null, scalar) { }
    public YamlNode(IReadOnlyDictionary<string, YamlNode> map) : this(map, null, "") { }
    public YamlNode(IReadOnlyList<YamlNode> list) : this(null, list, "") { }

    public bool IsMap => Map.Count > 0;
    public bool IsList => List.Count > 0;
    public bool IsScalar => Map.Count == 0 && List.Count == 0;

    public IReadOnlyDictionary<string, YamlNode> Map { get; }
    public IReadOnlyList<YamlNode> List { get; }
    public string Scalar { get; }

    public YamlNode? Child(string key) => Map.TryGetValue(key, out var value) ? value : null;
    public string? Text(string key) => Map.TryGetValue(key, out var value) && value.IsScalar ? value.Scalar : null;
}
