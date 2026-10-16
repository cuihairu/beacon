using System.Globalization;
using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 条件显示判定（Phase 5「更丰富的布局与条件显示」）：把「这个区段/节点此时要不要画」
/// 从散落各窗口的写死 if 收编为按状态字段求值的纯函数声明。语法（封闭集）：
/// <list type="bullet">
/// <item>空/空白 = 无条件 = 可见（现行为的缺省面）</item>
/// <item><c>stale</c> / <c>offline</c> 标志位（offline 与 WidgetFeedback 四态同口径：IsStale 或连接不健康）</item>
/// <item><c>has:字段</c> 值非空（<c>has:details</c> 特例 = WidgetFeedback.HasDetailRows，数值位提示键不算）</item>
/// <item><c>字段 op 值</c>，op ∈ &gt;= / &lt;= / != / &gt; / &lt; / =：
/// severity 按枚举序比较（Critical &gt; Error &gt; Warning &gt; Info &gt; Success，值大小写不敏感）；
/// progress 按不变文化双精度比较；其余字段 = / != 按忽略大小写字符串比较、关系运算不可求值为 false</item>
/// <item>前缀 <c>!</c> 取反（只作用于可识别形态）</item>
/// </list>
/// 字段名走 ComponentBindingEngine.ResolveStatePath 的同一张受支持字段集（含 payload:key），
/// 不另立词汇。缺省=可见；声明解析不出（未知字段/未知运算/操作数解析失败）= 不满足 = 隐藏
/// （与绑定引擎「未知选择器→空串」同型：结构缺省回退现行为，声明错误不给半真值）。
/// 静态无状态、不接收运行时服务——改条件只重求值，不触发数据请求（Phase 3 行 6 不变量同守）。
/// </summary>
public static class ComponentVisibility
{
    /// <summary>判定条件是否成立（条件为空 = 无条件 = 可见）。</summary>
    public static bool Evaluate(string? condition, WidgetState state)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        var negated = condition.StartsWith('!');
        var body = negated ? condition[1..].Trim() : condition.Trim();
        // 取反只作用于可识别形态：空体/未知形态直接按「不满足」隐藏，不让 ! 把声明错误翻成可见
        return TryEvaluateKnown(body, state, out var satisfied) ? (negated ? !satisfied : satisfied) : false;
    }

    private static bool TryEvaluateKnown(string body, WidgetState state, out bool satisfied)
    {
        satisfied = false;
        switch (body)
        {
            case "stale":
                satisfied = state.IsStale;
                return true;
            case "offline":
                satisfied = state.IsStale || !state.ConnectionHealthy;
                return true;
        }

        if (body.StartsWith("has:", StringComparison.Ordinal))
        {
            var hasPath = body["has:".Length..].Trim();
            satisfied = hasPath.Length > 0 && HasValue(hasPath, state);
            return true;
        }

        if (TrySplitComparison(body, out var path, out var op, out var value))
        {
            satisfied = Compare(path, op, value, state);
            return true;
        }
        return false;
    }

    private static bool HasValue(string path, WidgetState state)
        => path.Equals("details", StringComparison.OrdinalIgnoreCase)
            ? WidgetFeedback.HasDetailRows(state.Payload)
            : !string.IsNullOrWhiteSpace(ComponentBindingEngine.ResolveStatePath(state, path));

    private static readonly string[] Operators = [">=", "<=", "!=", ">", "<", "="];

    private static bool TrySplitComparison(string body, out string path, out string op, out string value)
    {
        path = "";
        op = "";
        value = "";
        for (var index = 0; index < body.Length; index++)
        {
            // 双字符运算符先于单字符（> 在 >= 之前截断会把 ">=5" 拆成 ">"、"=5"）
            foreach (var candidate in Operators)
            {
                if (index + candidate.Length <= body.Length && body.Substring(index, candidate.Length) == candidate)
                {
                    path = body[..index].Trim();
                    value = body[(index + candidate.Length)..].Trim();
                    if (path.Length > 0 && value.Length > 0)
                    {
                        op = candidate;
                        return true;
                    }
                    return false;
                }
            }
        }
        return false;
    }

    private static bool Compare(string path, string op, string value, WidgetState state)
    {
        if (path.Equals("severity", StringComparison.OrdinalIgnoreCase))
        {
            return Enum.TryParse<Severity>(value, true, out var operand)
                   && CompareOrdinal((int)state.Severity, (int)operand, op);
        }
        if (path.Equals("progress", StringComparison.OrdinalIgnoreCase))
        {
            return state.Progress is { } progress
                   && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var operand)
                   && CompareOrdinal(progress.CompareTo(operand), op);
        }

        var text = ComponentBindingEngine.ResolveStatePath(state, path);
        return op is "=" or "!="
            ? (string.Equals(text, value, StringComparison.OrdinalIgnoreCase) == (op == "="))
            : false; // 非数值字段的关系运算不可求值
    }

    private static bool CompareOrdinal(int left, int right, string op)
        => op switch
        {
            ">=" => left >= right,
            "<=" => left <= right,
            "!=" => left != right,
            ">" => left > right,
            "<" => left < right,
            _ => left == right,
        };

    private static bool CompareOrdinal(int comparison, string op)
        => op switch
        {
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            "!=" => comparison != 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            _ => comparison == 0,
        };
}
