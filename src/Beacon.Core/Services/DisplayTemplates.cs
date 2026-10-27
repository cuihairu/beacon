using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>数据能力词表（类型级稳定声明，评审 P1 语义描述）："progress" 百分比。
/// 持有方是 WidgetTypeDescriptor.DataKinds；词表封闭，渲染/筛选只认这些键。</summary>
public static class DisplayTemplateKinds
{
    public const string Progress = "progress";

    public static IReadOnlySet<string> None { get; } = new HashSet<string>(StringComparer.Ordinal);

    public static IReadOnlySet<string> ProgressOnly { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Progress,
    };

    /// <summary>能力词中文名（诊断/反馈文案用；未认词表回原词）。</summary>
    public static string Describe(string kind)
        => kind == Progress ? "百分比" : kind;
}

/// <summary>模板可调参数声明（评审 P1 参数表单）：Key 即覆盖到生成树同名 Props 键的参数，
/// Kind 词表沿组件中心表单（text/size/tone/severity/spacing/slider）。</summary>
public sealed record TemplateParameter(
    string Key,
    string Label,
    string Kind,
    string Default,
    double Min = 0,
    double Max = 1);

/// <summary>兼容性判定结果：Ok + 不可用原因（评审 P1 能力匹配——筛选不只给真假，给理由）。</summary>
public sealed record TemplateCompatibility(bool Ok, string? Reason)
{
    public static TemplateCompatibility Yes { get; } = new(true, null);

    public static TemplateCompatibility No(string reason) => new(false, reason);
}

/// <summary>
/// 展示模板注册表（2026-10-18 外部评审 P1，2026-10-22 评审 P0/P1 能力闭环升级）：
/// 「数据显示成什么样」的成品样式单源。每个模板 = 一份声明式树生成器（纯函数，动态值全部经
/// 绑定注入，状态到了才显值——fail-closed 口径与渲染链一致）+ 能力门槛 Requires（类型级稳定
/// 数据能力，经 WidgetTypeDescriptor.DataKinds 判断，不看当前值——「这次没值」≠「不支持进度」）。
/// 类型词汇只用渲染注册表已知键（ring / pool 与本批 App 侧工厂同落地）；百分比数值取数走
/// percent 选择器（ComponentBindingEngine）。高级组合（手工拼树）不经过本表——树编辑器口径不变。
/// 模板身份可从已落盘树反解（Identify），恢复选择不再默认横条；尺寸下限（MinTileHeightDips）
/// 供 tile 宿主按模板放大行高防裁切（评审 P2）。
/// </summary>
public static class DisplayTemplates
{
    private const string State = ComponentBindingSource.KindState;
    private const string Selector = ComponentBindingSource.KindSelector;

    /// <summary>模板声明：Id 稳定键 / Name 中文名 / Desc 一句话说明 / Requires 数据能力门槛 /
    /// Parameters 可调参数（生成器的参数覆盖表）/ MinTileHeightDips tile 面最小高 /
    /// Builder 生成树根（每次调用新实例，参数覆盖在返回前就地生效）。</summary>
    public sealed record DisplayTemplate(
        string Id,
        string Name,
        string Desc,
        IReadOnlySet<string> Requires,
        IReadOnlyDictionary<string, TemplateParameter> Parameters,
        double MinTileHeightDips,
        Func<IReadOnlyDictionary<string, string>?, ComponentDescriptor> Builder)
    {
        /// <summary>旧口径兼容位（测试/调用面）：RequiresProgress = 含 progress 能力。</summary>
        public bool RequiresProgress => Requires.Contains(DisplayTemplateKinds.Progress);

        /// <summary>生成树根（无参便捷口；参数覆盖走带覆盖重载）。</summary>
        public ComponentDescriptor Build() => Builder(null);

        /// <summary>带参数覆盖生成：键在模板声明参数内且落树内同名 Props 才生效，slider 越界钳位
        /// （调用面表单已钳，此处兜底防手改 JSON 绕过）。</summary>
        public ComponentDescriptor Build(IReadOnlyDictionary<string, string> overrides)
        {
            var tree = Builder(overrides);
            if (overrides is { Count: > 0 })
            {
                ClampSliders(tree, Parameters);
            }
            return tree;
        }

        private static void ClampSliders(
            ComponentDescriptor node,
            IReadOnlyDictionary<string, TemplateParameter> schema)
        {
            foreach (var (key, value) in node.Props)
            {
                if (schema.TryGetValue(key, out var spec)
                    && string.Equals(spec.Kind, "slider", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var number))
                {
                    node.Props[key] = Math.Clamp(number, spec.Min, spec.Max)
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            foreach (var child in node.Children)
            {
                if (child is not null)
                {
                    ClampSliders(child, schema);
                }
            }
        }
    }

    public static IReadOnlyList<DisplayTemplate> All { get; } =
    [
        new("bar", "横条", "摘要一行 + 底部进度条（默认档）",
            DisplayTemplateKinds.ProgressOnly,
            ParamMap(new TemplateParameter("width", "条宽", "slider", "220", 40, 320)),
            32,
            static overrides => WithParamOverrides(
                Group("column", Props(("gap", "sm")),
                    Leaf("text", null, Bind("text", State, "summary")),
                    Leaf("progress", Props(("width", "220")), Bind("progress", State, "progress"))),
                "width", overrides)),
        new("ring", "圆环", "环形进度 + 摘要一行",
            DisplayTemplateKinds.ProgressOnly,
            ParamMap(
                new TemplateParameter("size", "环径", "slider", "30", 16, 96),
                new TemplateParameter("thickness", "环粗", "slider", "3", 1, 8)),
            34,
            static overrides => WithParamOverrides(
                Group("row", Props(("gap", "md")),
                    Leaf("ring", Props(("size", "30"), ("thickness", "3")), Bind("progress", State, "progress")),
                    Leaf("text", null, Bind("text", State, "summary"))),
                "size", overrides, "thickness")),
        new("number", "数字卡", "百分比大数字（percent 选择器取数）+ 摘要",
            DisplayTemplateKinds.ProgressOnly,
            ParamMap(new TemplateParameter("fontSize", "字号", "slider", "22", 12, 40)),
            48,
            static overrides => WithParamOverrides(
                Group("column", Props(("gap", "sm")),
                    Leaf("metric", Props(("fontSize", "22")), Bind("value", Selector, "percent")),
                    Leaf("text", Props(("size", "caption")), Bind("text", State, "summary"))),
                "fontSize", overrides)),
        new("pool", "水位", "圆池注水 + 摘要一行",
            DisplayTemplateKinds.ProgressOnly,
            ParamMap(
                new TemplateParameter("width", "池宽", "slider", "30", 16, 96),
                new TemplateParameter("height", "池高", "slider", "30", 16, 96)),
            34,
            static overrides => WithParamOverrides(
                Group("row", Props(("gap", "md")),
                    Leaf("pool", Props(("width", "30"), ("height", "30")), Bind("progress", State, "progress")),
                    Leaf("text", null, Bind("text", State, "summary"))),
                "width", overrides, "height")),
        new("statusline", "状态行", "状态灯 + 摘要（无百分比数据也兼容）",
            DisplayTemplateKinds.None,
            ParamMap(new TemplateParameter("size", "灯径", "slider", "10", 4, 24)),
            32,
            static overrides => WithParamOverrides(
                Group("row", Props(("gap", "sm")),
                    Leaf("statusdot", Props(("size", "10")), Bind("severity", State, "severity")),
                    Leaf("text", null, Bind("text", State, "summary"))),
                "size", overrides)),
    ];

    /// <summary>按当前值筛选（旧口径回退面）：无状态或无百分比 → 只给不要求进度的模板。
    /// 请优先用带 dataKinds 的重载（能力级判断，空状态也可按类型能力列全）。</summary>
    public static IReadOnlyList<DisplayTemplate> Compatible(WidgetState? state)
        => Compatible(null, state);

    /// <summary>能力级筛选（评审 P1）：类型声明能力为准（dataKinds 缺位才回退当前值推断）；
    /// 空状态/离线只要类型产百分比，横条/圆环/数字卡/水位照列——不把「这次没值」等同「不支持」。</summary>
    public static IReadOnlyList<DisplayTemplate> Compatible(IReadOnlySet<string>? dataKinds, WidgetState? state)
        => All.Where(template => CanApply(template, dataKinds, "tile", state).Ok).ToArray();

    /// <summary>兼容性判定（评审 P1 能力匹配）：面词汇封闭校验 + 能力门槛 + 缺能力给中文原因。</summary>
    public static TemplateCompatibility CanApply(
        DisplayTemplate template,
        IReadOnlySet<string>? dataKinds,
        string surface,
        WidgetState? state)
    {
        if (surface is not ("tile" or "row" or "detail"))
        {
            return TemplateCompatibility.No($"不支持的面：{surface}（仅 tile / row / detail）");
        }
        var missing = template.Requires
            .Where(required => dataKinds is null
                ? !(state is { Progress: { } }) // 能力未声明：回退当前值推断（旧口径）
                : !dataKinds.Contains(required))
            .Select(DisplayTemplateKinds.Describe)
            .ToArray();
        return missing.Length == 0
            ? TemplateCompatibility.Yes
            : TemplateCompatibility.No($"该组件无{missing[0]}数据（不给虚假的确定进度）");
    }

    /// <summary>模板身份反解（评审 P0 样式恢复）：已落盘树结构（含绑定/子件）与某模板默认产物
    /// 全等即该模板 Id（比对跳过参数键——参数已改仍是该模板）；自定义树/无树回 null。</summary>
    public static string? Identify(ComponentDescriptor? root)
    {
        if (root is null)
        {
            return null;
        }
        foreach (var template in All)
        {
            if (StructureEquals(template.Build(), root,
                new HashSet<string>(template.Parameters.Keys, StringComparer.OrdinalIgnoreCase)))
            {
                return template.Id;
            }
        }
        return null;
    }

    /// <summary>该模板在 tile 面所需最小高（无匹配模板按常规档下限 0，不放大行高）。</summary>
    public static double MinTileHeightDipsOf(ComponentDescriptor? root)
    {
        var id = Identify(root);
        if (id is null)
        {
            return 0;
        }
        return All.First(template => template.Id == id).MinTileHeightDips;
    }

    /// <summary>取模板定义（未知 Id 返回空列表，调用方按空处理）。</summary>
    public static IReadOnlyList<DisplayTemplate> Match(string id)
        => All.Where(template => template.Id == id).ToArray();

    // —— 生成与比对（内部纯函数）——

    private static Dictionary<string, TemplateParameter> ParamMap(params TemplateParameter[] items)
    {
        var map = new Dictionary<string, TemplateParameter>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            map[item.Key] = item;
        }
        return map;
    }

    /// <summary>参数覆盖：树内同名 Props 键就地覆盖（slider 词钳到声明区间，非法值回默认——
    /// 调用面表单已钳，此处兜底防手改 JSON 绕过）。覆盖键不在任何节点上 = 无效果（不猜）。</summary>
    private static ComponentDescriptor WithParamOverrides(
        ComponentDescriptor root,
        string firstKey,
        IReadOnlyDictionary<string, string>? overrides,
        params string[] otherKeys)
    {
        if (overrides is not { Count: > 0 })
        {
            return root;
        }
        var keys = new List<string> { firstKey };
        keys.AddRange(otherKeys);
        WalkProps(root, keys, overrides);
        return root;
    }

    private static void WalkProps(
        ComponentDescriptor node,
        IReadOnlyList<string> keys,
        IReadOnlyDictionary<string, string> overrides)
    {
        foreach (var key in keys)
        {
            if (overrides.TryGetValue(key, out var value) && node.Props.ContainsKey(key))
            {
                node.Props[key] = value;
            }
        }
        foreach (var child in node.Children)
        {
            if (child is not null)
            {
                WalkProps(child, keys, overrides);
            }
        }
    }

    /// <summary>结构全等（Identify 用）：Type/子件数/绑定序与值/Props（跳过 skipKeys 参数键）逐项比。</summary>
    private static bool StructureEquals(
        ComponentDescriptor left,
        ComponentDescriptor right,
        IReadOnlySet<string> skipKeys)
    {
        if (!string.Equals(left.Type, right.Type, StringComparison.Ordinal))
        {
            return false;
        }
        if (!string.Equals(left.Id, right.Id, StringComparison.Ordinal))
        {
            return false;
        }
        if (!PropsEqual(left.Props, right.Props, skipKeys))
        {
            return false;
        }
        if (left.Bindings.Count != right.Bindings.Count
            || !left.Bindings.Zip(right.Bindings).All(pair => BindingEquals(pair.First, pair.Second)))
        {
            return false;
        }
        return left.Children.Count == right.Children.Count
            && left.Children.Zip(right.Children).All(pair => StructureEquals(pair.First, pair.Second, skipKeys));
    }

    private static bool BindingEquals(ComponentBinding? left, ComponentBinding? right)
        => left is not null && right is not null
            && string.Equals(left.Field, right.Field, StringComparison.Ordinal)
            && left.Source is not null && right.Source is not null
            && string.Equals(left.Source.Kind, right.Source.Kind, StringComparison.Ordinal)
            && string.Equals(left.Source.Ref, right.Source.Ref, StringComparison.Ordinal)
            && ArgsEqual(left.Source.Args, right.Source.Args);

    private static bool ArgsEqual(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var value)
                && string.Equals(value, pair.Value, StringComparison.Ordinal));
    }

    private static bool PropsEqual(
        Dictionary<string, string> left,
        Dictionary<string, string> right,
        IReadOnlySet<string> skipKeys)
    {
        var leftKept = left.Keys.Where(key => !skipKeys.Contains(key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightKept = right.Keys.Where(key => !skipKeys.Contains(key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!leftKept.SetEquals(rightKept))
        {
            return false;
        }
        return leftKept.All(key =>
            right.TryGetValue(key, out var value)
            && string.Equals(value, left[key], StringComparison.Ordinal));
    }

    private static ComponentDescriptor Leaf(string type, Dictionary<string, string>? props = null, params ComponentBinding[] bindings)
        => new()
        {
            Type = type,
            Props = props ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Bindings = [.. bindings],
        };

    private static ComponentDescriptor Group(string type, Dictionary<string, string>? props, params ComponentDescriptor[] children)
        => new()
        {
            Type = type,
            Props = props ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Children = [.. children],
        };

    private static Dictionary<string, string> Props(params (string Key, string Value)[] items)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in items)
        {
            map[key] = value;
        }
        return map;
    }

    private static ComponentBinding Bind(string field, string kind, string reference)
        => new()
        {
            Field = field,
            Source = new ComponentBindingSource { Kind = kind, Ref = reference },
        };
}
