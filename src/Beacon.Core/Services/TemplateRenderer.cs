namespace Beacon.Core.Services;

/// <summary>
/// 模板渲染（RFC §13）：{key} 占位符替换（OrdinalIgnoreCase）。
/// 缺失 key 抛 ArgumentException（fail-closed：避免把占位符字面量发到外部系统）。
/// </summary>
public static class TemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> vars)
    {
        var lookup = new Dictionary<string, string>(vars, StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        var rendered = new System.Text.RegularExpressions.Regex(@"\{([A-Za-z0-9_.-]+)\}")
            .Replace(template, match =>
            {
                var key = match.Groups[1].Value;
                if (lookup.TryGetValue(key, out var value))
                {
                    return value;
                }
                missing.Add(key);
                return match.Value;
            });
        if (missing.Count > 0)
        {
            throw new ArgumentException($"模板缺少变量：{string.Join(", ", missing.Distinct())}");
        }
        return rendered;
    }
}
