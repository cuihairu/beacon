using System.Globalization;
using Beacon.Core.Models;
using Beacon.Core.Selectors;

namespace Beacon.Core.Services;

/// <summary>
/// 绑定引擎（Phase 3 行 3）：组件属性 ← 状态/选择器/字面量的纯函数解析。
/// 无 IO、无 Provider 依赖、无缓存——状态来自调用面传入的快照，因此改布局只重解析、
/// 不触发数据请求（Phase 3 行 6 不变量的构造保证）。取不到值一律空串（fail-closed）：
/// 未知源名、未知选择器、缺失 payload 键都不抛异常。
/// </summary>
public static class ComponentBindingEngine
{
    private static readonly Dictionary<string, Func<WidgetState, string>> StatePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["summary"] = state => state.Summary,
        ["severity"] = state => state.Severity.ToString(),
        ["progress"] = state => state.Progress?.ToString("R", CultureInfo.InvariantCulture) ?? "",
        ["detailUrl"] = state => state.DetailUrl ?? "",
        ["lifecycle"] = state => state.Lifecycle?.ToString() ?? "",
        ["connectionId"] = state => state.ConnectionId,
        ["widgetId"] = state => state.WidgetId,
        ["widgetType"] = state => state.WidgetType,
    };

    /// <summary>选择器源注册表（现行：值位提示与多窗口额度，均为 Core 既有纯函数选择器）。</summary>
    private static readonly Dictionary<string, Func<WidgetState, IReadOnlyDictionary<string, string>, string?>> Selectors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["value-hint"] = (state, _) => WidgetValueHint.Of(state),
            ["quota-windows"] = (state, _) => WidgetValueHint.MultiWindow(state.Payload),
        };

    /// <summary>受支持的状态字段名（Phase 4 模板解析期白名单校验同一张表，不另写第二份）。</summary>
    public static IReadOnlyCollection<string> StateFieldNames { get; } = StatePaths.Keys.ToArray();

    /// <summary>选择器注册表名（Phase 4 模板插值 {{selector.x}} 解析期白名单校验用）。</summary>
    public static IReadOnlyCollection<string> SelectorNames { get; } = Selectors.Keys.ToArray();

    /// <summary>整体解析：树内每个绑定节点的 Field 值，键 = "{节点路径}.{字段}"（路径与校验诊断同口径）。</summary>
    public static IReadOnlyDictionary<string, string> Resolve(WidgetInstance instance, WidgetState state)
        => Resolve(instance, new Dictionary<string, WidgetState> { [ComponentBindingSource.OwnStateKey] = state });

    /// <summary>
    /// 带数据源映射的解析：SourceName 按精确源名取映射键（"" = 自身状态），映射由
    /// WidgetInstanceResolver.ResolveSources 按 WidgetInstance.Sources 声明从状态快照构建。
    /// </summary>
    public static IReadOnlyDictionary<string, string> Resolve(
        WidgetInstance instance,
        IReadOnlyDictionary<string, WidgetState> sources)
    {
        var values = new Dictionary<string, string>();
        Collect(instance.Root, null, 0, sources, values);
        return values;
    }

    /// <summary>单条绑定解析：取不到值回空串，不抛。</summary>
    public static string ResolveBinding(
        ComponentBinding binding,
        WidgetState? ownState,
        IReadOnlyDictionary<string, WidgetState>? sources = null)
    {
        if (string.IsNullOrWhiteSpace(binding.Field))
        {
            return "";
        }
        var source = binding.Source;
        if (source.Kind == ComponentBindingSource.KindConst)
        {
            return source.Args is { } args && args.TryGetValue("value", out var value) ? value : "";
        }
        var state = LookupState(source.SourceName, ownState, sources);
        if (state is null)
        {
            return "";
        }
        return source.Kind == ComponentBindingSource.KindSelector
            ? ResolveSelector(source.Ref, state, source.Args)
            : ResolveStatePath(state, source.Ref);
    }

    private static WidgetState? LookupState(
        string sourceName,
        WidgetState? ownState,
        IReadOnlyDictionary<string, WidgetState>? sources)
        => string.IsNullOrEmpty(sourceName)
            ? ownState
            : (sources is not null && sources.TryGetValue(sourceName, out var state) ? state : null);

    /// <summary>状态路径单源求值（受支持字段集封闭 + payload:key）；条件显示（ComponentVisibility）
    /// 复用同一张字段表，两个消费面不各写一遍。</summary>
    public static string ResolveStatePath(WidgetState state, string path)
    {
        var colon = path.IndexOf(':');
        if (colon >= 0
            && path[..colon] == "payload"
            && colon + 1 < path.Length
            && state.Payload.TryGetValue(path[(colon + 1)..], out var value))
        {
            return value;
        }
        return StatePaths.TryGetValue(path, out var resolve) ? resolve(state) : "";
    }

    private static string ResolveSelector(
        string name,
        WidgetState state,
        IReadOnlyDictionary<string, string>? args)
    {
        if (!Selectors.TryGetValue(name, out var selector))
        {
            return "";
        }
        return selector(state, args ?? EmptyArgs) ?? "";
    }

    private static readonly Dictionary<string, string> EmptyArgs = new(StringComparer.OrdinalIgnoreCase);

    private static void Collect(
        ComponentDescriptor node,
        string? parentPath,
        int index,
        IReadOnlyDictionary<string, WidgetState>? sources,
        Dictionary<string, string> values)
    {
        var name = string.IsNullOrEmpty(node.Id) ? $"child{index}" : node.Id;
        var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
        var own = sources is not null
                  && sources.TryGetValue(ComponentBindingSource.OwnStateKey, out var ownState)
                  ? ownState
                  : null;
        foreach (var binding in node.Bindings)
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.Field))
            {
                continue;
            }
            values[$"{path}.{binding.Field}"] = ResolveBinding(binding, own, sources);
        }
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child is not null)
            {
                Collect(child, path, i, sources, values);
            }
        }
    }
}
