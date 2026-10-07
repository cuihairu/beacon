using System.Text.Json;

namespace Beacon.Connections;

/// <summary>
/// 极简 JSON 点路径（positioning §3 Generic HTTP）：`$.status`、`$.items[0].state`、`$.a.b.c`。
/// 只读提取标量；路径不存在/中间不是对象或数组一律返回 null——映射配错不炸刷新（RFC §6.2.6 离线降级口径）。
/// </summary>
public static class HttpJsonPath
{
    public static string? Select(JsonElement root, string? path)
    {
        if (path is null)
        {
            return null;
        }
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }
        if (trimmed.StartsWith("$", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..]; // 允许带不带 $ 前缀
        }

        var current = (JsonElement?)root;
        foreach (var raw in trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Length == 0)
            {
                continue;
            }
            var open = token.IndexOf('[');
            var name = open >= 0 ? token[..open].Trim() : token;
            if (name.Length > 0)
            {
                current = Property(current, name);
                if (current is null)
                {
                    return null;
                }
            }
            while (open >= 0)
            {
                var close = token.IndexOf(']', open);
                if (close < open + 1 || !int.TryParse(token[(open + 1)..close].Trim(), out var index))
                {
                    return null;
                }
                current = Index(current, index);
                if (current is null)
                {
                    return null;
                }
                open = token.IndexOf('[', close);
            }
        }
        return current is { } element ? Scalar(element) : null;
    }

    private static JsonElement? Property(JsonElement? element, string name)
        => element is { } e && e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var found) ? found : null;

    private static JsonElement? Index(JsonElement? element, int index)
        => element is { } e && e.ValueKind == JsonValueKind.Array && (uint)index < (uint)e.GetArrayLength() ? e[index] : null;

    private static string? Scalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null, // null/对象/数组不映射为字符串
    };
}
