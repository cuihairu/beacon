namespace Beacon.Core.Models;

/// <summary>
/// 组件描述符（Phase 3 契约，契约.md §3.2 增量可选字段）：组件树的声明式描述——
/// App 渲染段与 Core 绑定引擎之间的纯数据。Id 仅绑定目标需要（空=匿名节点）；
/// Props 一律字符串（同 WidgetConfig.Config / ActionConfig.Parameters 口径，类型换算在渲染期）；
/// Children 允许嵌套组合，防爆半径见 ComponentTreeValidator。
/// </summary>
public sealed class ComponentDescriptor
{
    /// <summary>节点标识：绑定目标定位用；空=匿名节点（允许多个）。</summary>
    public string Id { get; init; } = "";

    /// <summary>组件类型键（渲染注册表解析，Phase 3 绑定引擎落分发）。</summary>
    public required string Type { get; init; }

    /// <summary>静态属性：键值均为字符串，OrdinalIgnoreCase（与 Config/Parameters 同口径）。</summary>
    public Dictionary<string, string> Props { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>子节点（嵌套组合）。</summary>
    public List<ComponentDescriptor> Children { get; init; } = [];
}

/// <summary>组件树校验结果：Ok=false 时 Errors 给出可读诊断（调用面整树弃用 + 进诊断日志）。</summary>
public sealed record ComponentTreeValidation(bool Ok, IReadOnlyList<string> Errors);

/// <summary>
/// 组件树结构防呆校验。常驻进程铁律：配置坏不炸进程、不静默裁剪——非法树整树弃用回传统渲染。
/// 上限是手写 widgets.json 的防爆半径：8 层深 × 32 子 × 256 总节点。
/// </summary>
public static class ComponentTreeValidator
{
    public const int MaxDepth = 8;
    public const int MaxChildrenPerNode = 32;
    public const int MaxNodes = 256;

    /// <summary>无树 = 无错（无组件树的组件走传统渲染）。</summary>
    public static ComponentTreeValidation None { get; } = new(true, []);

    public static ComponentTreeValidation Validate(ComponentDescriptor? root)
    {
        if (root is null)
        {
            return None;
        }
        var nodes = CountNodes(root);
        var errors = new List<string>();
        if (nodes > MaxNodes)
        {
            errors.Add($"tree: total nodes {nodes} exceeds max {MaxNodes}");
            return new ComponentTreeValidation(false, [.. errors]);
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Walk(root, null, 0, 1, ids, errors);
        return errors.Count == 0 ? None : new ComponentTreeValidation(false, [.. errors]);
    }

    private static int CountNodes(ComponentDescriptor node)
    {
        var total = 1;
        foreach (var child in node.Children)
        {
            if (child is not null)
            {
                total += CountNodes(child);
            }
        }
        return total;
    }

    private static void Walk(
        ComponentDescriptor node,
        string? parentPath,
        int index,
        int depth,
        HashSet<string> ids,
        List<string> errors)
    {
        var name = string.IsNullOrEmpty(node.Id) ? $"child{index}" : node.Id;
        var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";

        if (string.IsNullOrWhiteSpace(node.Type))
        {
            errors.Add($"{path}: type is empty");
        }
        if (depth > MaxDepth)
        {
            errors.Add($"{path}: depth {depth} exceeds max {MaxDepth}");
        }
        if (node.Children.Count > MaxChildrenPerNode)
        {
            errors.Add($"{path}: children {node.Children.Count} exceeds max {MaxChildrenPerNode}");
        }
        if (!string.IsNullOrEmpty(node.Id) && !ids.Add(node.Id))
        {
            errors.Add($"{path}: duplicate id '{node.Id}'");
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child is null)
            {
                errors.Add($"{path}/child{i}: null child");
                continue;
            }
            Walk(child, path, i, depth + 1, ids, errors);
        }
    }
}
