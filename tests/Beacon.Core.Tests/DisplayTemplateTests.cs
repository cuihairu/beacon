using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// 2026-10-18 外部评审 P1：展示模板注册表——纯函数生成器、能力筛选与树合法性。
/// 每份模板产物都必须过 ComponentTreeValidator（与保存链前置校验同一张口径），
/// 保证模板产物可直接落盘；percent 选择器无进度时回空串，不编造确定数字。
/// 2026-10-27 评审 P0/P1 能力闭环升级：能力级筛选（dataKinds）不把「这次没值」等同
/// 「不支持进度」；模板身份反解（Identify）供样式恢复；参数覆盖（Build(overrides)）
/// 供实时参数表单；数字卡去双百分号（percent 选择器自带 %）。
/// </summary>
public sealed class DisplayTemplateTests
{
    private static WidgetState State(double? progress) => new()
    {
        WidgetId = "w1",
        WidgetType = "glm-quota",
        ConnectionId = "glm",
        Severity = Severity.Warning,
        Summary = "GLM · ¥110.00",
        Progress = progress,
    };

    [Fact]
    public void Templates_have_unique_ids_with_default_first()
    {
        Assert.Equal(7, DisplayTemplates.All.Count); // bar/ring/number/pool/statusline/quotaline/metricgrid（SET-UX-07 起）
        Assert.Equal(7, DisplayTemplates.All.Select(template => template.Id).Distinct().Count());
        Assert.Equal("bar", DisplayTemplates.All[0].Id); // 默认档在首，展示面默认选中项可稳定取
    }

    [Fact]
    public void Every_template_builds_a_validator_clean_tree()
    {
        foreach (var template in DisplayTemplates.All)
        {
            var validation = ComponentTreeValidator.Validate(template.Build());
            Assert.True(validation.Ok, $"{template.Id}: {string.Join("; ", validation.Errors)}");
        }
    }

    [Fact]
    public void Templates_build_independent_instances()
    {
        var template = Template("bar");
        Assert.NotSame(template.Build(), template.Build());
    }

    [Fact]
    public void Compatible_returns_all_when_progress_is_present()
    {
        // dataKinds 未声明（null）回退当前值推断（旧口径）：有进度即全表放行——含 quotaline/metricgrid
        // （旧口径不区分窗口能力，声明过的类型才按声明锁面，见 Capability_* 系列）。
        var ids = DisplayTemplates.Compatible(State(0.52)).Select(template => template.Id).ToHashSet();
        Assert.Equal(7, ids.Count);
        Assert.Contains("ring", ids);
        Assert.Contains("quotaline", ids);
        Assert.Contains("metricgrid", ids);
    }

    [Fact]
    public void Compatible_excludes_progress_templates_without_progress()
    {
        foreach (var state in new WidgetState?[] { null, State(null) })
        {
            var ids = DisplayTemplates.Compatible(state).Select(template => template.Id).ToHashSet();
            Assert.Single(ids);
            Assert.Contains("statusline", ids);
        }
    }

    [Fact]
    public void Bar_binds_summary_text_and_progress_value()
    {
        var root = Template("bar").Build();
        Assert.Equal("column", root.Type);
        Assert.Equal(2, root.Children.Count);
        Assert.Empty(root.Bindings); // 容器不声明取数槽
        var textBinding = Assert.Single(root.Children[0].Bindings);
        Assert.Equal(("text", ComponentBindingSource.KindState, "summary"),
            (textBinding.Field, textBinding.Source.Kind, textBinding.Source.Ref));
        var progressBinding = Assert.Single(root.Children[1].Bindings);
        Assert.Equal(("progress", ComponentBindingSource.KindState, "progress"),
            (progressBinding.Field, progressBinding.Source.Kind, progressBinding.Source.Ref));
        Assert.Equal("220", root.Children[1].Props["width"]);
    }

    [Fact]
    public void Number_uses_percent_selector_without_duplicate_unit()
    {
        // 2026-10-27 评审 P0 52%%：percent 选择器已自带「%」，模板不再声明 unit="%"——
        // 数字卡只显示一个百分号（组件渲染链 metric 值位 + 单位小字）。
        var root = Template("number").Build();
        var metric = Assert.Single(root.Children, child => child.Type == "metric");
        var binding = Assert.Single(metric.Bindings);
        Assert.Equal(("value", ComponentBindingSource.KindSelector, "percent"),
            (binding.Field, binding.Source.Kind, binding.Source.Ref));
        Assert.False(metric.Props.ContainsKey("unit"));
        Assert.Equal("22", metric.Props["fontSize"]);
    }

    [Fact]
    public void StatusLine_and_metricgrid_are_templates_without_progress_requirement()
    {
        // SET-UX-07：metricgrid 走 payload 百分比键直读（percent-of），不依赖 state.Progress——
        // 与 statusline 同为无进度门槛模板（WindowsOnly 能力门槛）。
        Assert.Equal(2, DisplayTemplates.All.Count(template => !template.RequiresProgress));
        var root = Template("statusline").Build();
        var dotBinding = Assert.Single(root.Children[0].Bindings);
        Assert.Equal("severity", dotBinding.Field);
        Assert.Equal("severity", dotBinding.Source.Ref);
    }

    [Fact]
    public void Percent_is_a_registered_selector_name()
        => Assert.Contains("percent", ComponentBindingEngine.SelectorNames);

    // —— SET-UX-07 ①：percent-of 选择器（payload 指定键直读） ——

    private static ComponentBinding PercentOf(string key) => new()
    {
        Field = "value",
        Source = new ComponentBindingSource
        {
            Kind = ComponentBindingSource.KindSelector,
            Ref = "percent-of",
            Args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["key"] = key },
        },
    };

    [Fact]
    public void PercentOf_is_a_registered_selector_name()
        => Assert.Contains("percent-of", ComponentBindingEngine.SelectorNames);

    private static WidgetState StateWithPayload(params (string Key, string Value)[] items)
        => State(0.9) with
        {
            Payload = new Dictionary<string, string>(
                items.ToDictionary(item => item.Key, item => item.Value), StringComparer.OrdinalIgnoreCase),
        };

    [Fact]
    public void PercentOf_reads_payload_key_and_formats_percent()
    {
        var state = StateWithPayload(("cpu_percent", "12"), ("rolling_percent", "65.4"));
        Assert.Equal("12%", ComponentBindingEngine.ResolveBinding(PercentOf("cpu_percent"), state));
        Assert.Equal("65.4%", ComponentBindingEngine.ResolveBinding(PercentOf("rolling_percent"), state));
    }

    [Fact]
    public void PercentOf_fails_closed_on_missing_or_bad_keys()
    {
        var state = StateWithPayload(("junk_percent", "not-a-number"));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(PercentOf("absent_key"), state)); // 缺键=空串
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(PercentOf("junk_percent"), state)); // 非数值=空串
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(PercentOf("cpu_percent"), null)); // 无状态=空串
        var noKey = new ComponentBinding
        {
            Field = "value",
            Source = new ComponentBindingSource { Kind = ComponentBindingSource.KindSelector, Ref = "percent-of" },
        };
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(noKey, state)); // 未声明键=空串（不猜主键）
    }

    [Fact]
    public void QuotaLine_builds_main_bar_plus_windows_caption()
    {
        var root = Template("quotaline").Build();
        Assert.Equal("column", root.Type);
        Assert.Equal(3, root.Children.Count);
        var summary = Assert.Single(root.Children[0].Bindings);
        Assert.Equal(("text", ComponentBindingSource.KindState, "summary"),
            (summary.Field, summary.Source.Kind, summary.Source.Ref));
        var progress = Assert.Single(root.Children[1].Bindings);
        Assert.Equal("progress", progress.Field);
        var windows = Assert.Single(root.Children[2].Bindings);
        Assert.Equal(("text", ComponentBindingSource.KindSelector, "quota-windows"),
            (windows.Field, windows.Source.Kind, windows.Source.Ref)); // 次窗口读数=quota-windows 聚合（单窗=空行不占位）
    }

    [Fact]
    public void QuotaLine_gate_and_identify()
    {
        var template = Template("quotaline");
        Assert.True(DisplayTemplates.CanApply(template, DisplayTemplateKinds.ProgressAndWindows, "tile", null).Ok);
        Assert.False(DisplayTemplates.CanApply(template, DisplayTemplateKinds.ProgressOnly, "tile", null).Ok);
        Assert.Contains("多窗口读数", DisplayTemplates.CanApply(template, DisplayTemplateKinds.ProgressOnly, "tile", null).Reason);
        Assert.Equal("quotaline", DisplayTemplates.Identify(template.Build()));
    }

    [Fact]
    public void MetricGrid_builds_three_percent_of_rows_with_missing_key_hiding()
    {
        var root = Template("metricgrid").Build();
        Assert.Equal("column", root.Type);
        Assert.Equal(3, root.Children.Count);
        var keys = new[] { "cpu_percent", "mem_percent", "disk_percent" };
        for (var index = 0; index < 3; index++)
        {
            var row = root.Children[index];
            Assert.Equal("row", row.Type);
            Assert.Equal($"has:payload.{keys[index]}", row.Props["when"]); // 缺键整行隐藏（各来源分别处理缺失）
            var label = Assert.Single(row.Children[0].Bindings);
            Assert.Equal(ComponentBindingSource.KindConst, label.Source.Kind); // 标签=字面量
            var value = Assert.Single(row.Children[1].Bindings);
            Assert.Equal(("value", ComponentBindingSource.KindSelector, "percent-of"),
                (value.Field, value.Source.Kind, value.Source.Ref));
            Assert.Equal(keys[index], value.Source.Args!["key"]); // 键由模板声明（行序=CPU/内存/磁盘）
        }
    }

    [Fact]
    public void MetricGrid_gate_and_identify()
    {
        var template = Template("metricgrid");
        Assert.True(DisplayTemplates.CanApply(template, DisplayTemplateKinds.WindowsOnly, "tile", null).Ok);
        Assert.False(DisplayTemplates.CanApply(template, DisplayTemplateKinds.StatusOnly, "tile", null).Ok);
        Assert.Equal("metricgrid", DisplayTemplates.Identify(template.Build()));
        var overridden = template.Build(new Dictionary<string, string> { ["width"] = "99" }); // 未知参数不猜
        Assert.Equal("metricgrid", DisplayTemplates.Identify(overridden));
    }

    [Fact]
    public void Percent_formats_progress_and_fails_closed_without_it()
    {
        var binding = new ComponentBinding
        {
            Field = "value",
            Source = new ComponentBindingSource { Kind = ComponentBindingSource.KindSelector, Ref = "percent" },
        };
        Assert.Equal("0%", ComponentBindingEngine.ResolveBinding(binding, State(0)));
        Assert.Equal("50%", ComponentBindingEngine.ResolveBinding(binding, State(0.5)));
        Assert.Equal("75%", ComponentBindingEngine.ResolveBinding(binding, State(0.75)));
        Assert.Equal("100%", ComponentBindingEngine.ResolveBinding(binding, State(0.999)));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(binding, State(null)));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(binding, null));
    }

    // —— 2026-10-27 能力闭环升级（评审 P0/P1）——

    [Fact]
    public void Capability_compatible_lists_progress_templates_even_when_state_null()
    {
        // G01/G06 核心：类型声明产百分比（dataKinds），当前值缺失（空状态/首取数前）仍列四种进度样式。
        var ids = DisplayTemplates.Compatible(DisplayTemplateKinds.ProgressOnly, null)
            .Select(template => template.Id).ToHashSet();
        Assert.Equal(5, ids.Count);
        Assert.Contains("ring", ids);
        Assert.Contains("number", ids);
    }

    [Fact]
    public void Capability_compatible_without_progress_kind_lists_only_statusline()
    {
        var ids = DisplayTemplates.Compatible(DisplayTemplateKinds.None, State(0.52))
            .Select(template => template.Id).ToHashSet();
        Assert.Single(ids);
        Assert.Contains("statusline", ids);
    }

    [Fact]
    public void CanApply_returns_ok_with_reason_for_missing_capability()
    {
        var bar = Template("bar");
        var noKinds = DisplayTemplates.CanApply(bar, DisplayTemplateKinds.None, "tile", State(0.52));
        Assert.False(noKinds.Ok);
        Assert.Contains("百分比", noKinds.Reason);
        var withKinds = DisplayTemplates.CanApply(bar, DisplayTemplateKinds.ProgressOnly, "tile", State(null));
        Assert.True(withKinds.Ok);
        Assert.Null(withKinds.Reason);
    }

    [Fact]
    public void CanApply_rejects_unknown_surface_with_reason()
    {
        var result = DisplayTemplates.CanApply(Template("bar"), DisplayTemplateKinds.ProgressOnly, "canvas", State(0.5));
        Assert.False(result.Ok);
        Assert.Contains("canvas", result.Reason);
    }

    [Fact]
    public void CanApply_falls_back_to_current_value_when_capability_not_declared()
    {
        // 能力未声明（dataKinds=null）：回退当前值推断（旧口径），不给虚假进度。
        Assert.True(DisplayTemplates.CanApply(Template("bar"), null, "tile", State(0.52)).Ok);
        Assert.False(DisplayTemplates.CanApply(Template("bar"), null, "tile", State(null)).Ok);
    }

    [Fact]
    public void Identify_roundtrips_every_template_and_rejects_custom()
    {
        foreach (var template in DisplayTemplates.All)
        {
            Assert.Equal(template.Id, DisplayTemplates.Identify(template.Build()));
        }
        Assert.Null(DisplayTemplates.Identify(null));
        // 自定义树（非任何模板）：未知类型 → 反解 null
        var custom = new ComponentDescriptor
        {
            Type = "text",
            Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["text"] = "自定义" },
        };
        Assert.Null(DisplayTemplates.Identify(custom));
    }

    [Fact]
    public void Identify_survives_parameter_overrides()
    {
        // 参数已改仍是该模板（比对跳过参数键）
        var overridden = Template("bar").Build(new Dictionary<string, string> { ["width"] = "260" });
        Assert.Equal("bar", DisplayTemplates.Identify(overridden));
        var sizedRing = Template("ring").Build(new Dictionary<string, string> { ["size"] = "48", ["thickness"] = "6" });
        Assert.Equal("ring", DisplayTemplates.Identify(sizedRing));
    }

    [Fact]
    public void Build_with_overrides_applies_parameter_and_clamps_slider()
    {
        var bar = Template("bar").Build(new Dictionary<string, string> { ["width"] = "260" });
        Assert.Equal("260", bar.Children[1].Props["width"]);
        // slider 越界钳到声明区间（调用面表单已钳，此处兜底防手改）
        var clamped = Template("bar").Build(new Dictionary<string, string> { ["width"] = "9999" });
        Assert.Equal("320", clamped.Children[1].Props["width"]);
        // 未知参数键不猜（无效果）
        var noop = Template("bar").Build(new Dictionary<string, string> { ["nope"] = "1" });
        Assert.Equal("220", noop.Children[1].Props["width"]);
    }

    [Fact]
    public void Every_template_declares_min_tile_height_within_sane_range()
    {
        foreach (var template in DisplayTemplates.All)
        {
            Assert.True(template.MinTileHeightDips >= 32 && template.MinTileHeightDips <= 64,
                $"{template.Id}: MinTileHeightDips={template.MinTileHeightDips}");
        }
        // 数字卡竖排大字+摘要最占高；状态行最省
        Assert.True(Template("number").MinTileHeightDips > Template("statusline").MinTileHeightDips);
    }

    [Fact]
    public void MinTileHeightDipsOf_resolves_from_tree_and_zero_for_custom()
    {
        Assert.Equal(Template("number").MinTileHeightDips, DisplayTemplates.MinTileHeightDipsOf(Template("number").Build()));
        Assert.Equal(0, DisplayTemplates.MinTileHeightDipsOf(new ComponentDescriptor { Type = "text" }));
        Assert.Equal(0, DisplayTemplates.MinTileHeightDipsOf(null));
    }

    [Fact]
    public void Identify_DetectsIdPropsBindingsArgsAndStructuralDifferences()
    {
        var actual = Template("bar").Build();

        Assert.True(IdentifyMatches(actual, differentId: true));
        Assert.True(IdentifyMatches(actual, extraProp: true));
        Assert.True(Template("bar").RequiresProgress);
        Assert.False(Template("statusline").RequiresProgress);
        Assert.False(IdentifyMatches(actual, withoutBindings: true));
        Assert.True(IdentifyMatches(actual, withoutChildren: true));
    }

    [Fact]
    public void Identify_MatchesBindingsWithOptionalArguments()
    {
        var bar = Template("bar").Build();
        var summaryBinding = bar.Children[0].Bindings.Single();
        var withArgs = new ComponentDescriptor
        {
            Type = bar.Type,
            Props = new Dictionary<string, string>(bar.Props, StringComparer.OrdinalIgnoreCase),
            Children = [.. bar.Children],
            Bindings = [WithArgs(summaryBinding, "weekly")],
        };
        Assert.Null(DisplayTemplates.Identify(withArgs));

        var matchingArgs = new ComponentDescriptor
        {
            Type = bar.Type,
            Props = new Dictionary<string, string>(bar.Props, StringComparer.OrdinalIgnoreCase),
            Children = [.. bar.Children],
            Bindings = [],
        };
        Assert.Equal("bar", DisplayTemplates.Identify(matchingArgs));
    }

    [Fact]
    public void Every_template_has_a_parameter_schema_matching_its_props()
    {
        foreach (var template in DisplayTemplates.All)
        {
            var tree = template.Build();
            // 空参数表合法（SET-UX-07 metricgrid：定构多指标行，无旋钮）；已声明参数必须落到树内 Props。
            foreach (var (key, spec) in template.Parameters)
            {
                Assert.True(ContainsProp(tree, key), $"{template.Id}: 参数 {key} 未落到生成树任何 Props");
                Assert.False(string.IsNullOrWhiteSpace(spec.Label));
            }
        }
    }

    private static bool ContainsProp(ComponentDescriptor node, string key)
        => node.Props.ContainsKey(key) || node.Children.Any(child => child is not null && ContainsProp(child, key));

    private static DisplayTemplates.DisplayTemplate Template(string id)
    {
        var matches = DisplayTemplates.Match(id);
        Assert.Single(matches);
        return matches[0];
    }

    private static bool IdentifyMatches(
        ComponentDescriptor actual,
        bool differentId = false,
        bool extraProp = false,
        bool withoutBindings = false,
        bool withoutChildren = false,
        bool modifiedBindingArgs = false)
    {
        var candidate = new ComponentDescriptor
        {
            Id = differentId ? "different" : string.Empty,
            Type = actual.Type,
            Props = new Dictionary<string, string>(actual.Props, StringComparer.OrdinalIgnoreCase),
            Bindings = withoutBindings
                ? []
                : [.. actual.Bindings.Select(binding => modifiedBindingArgs ? WithArgs(binding, "monthly") : CloneBinding(binding))],
            Children = withoutChildren ? [] : [.. actual.Children],
        };
        if (extraProp)
        {
            candidate.Props["extra"] = "1";
        }
        return DisplayTemplates.Identify(candidate) is null;
    }

    private static ComponentBinding CloneBinding(ComponentBinding binding) => new()
    {
        Field = binding.Field,
        Source = binding.Source,
    };


    private static ComponentBinding WithArgs(ComponentBinding binding, string? window) => new()
    {
        Field = binding.Field,
        Source = new ComponentBindingSource
        {
            Kind = binding.Source!.Kind,
            Ref = binding.Source.Ref,
            Args = window is null ? null : new Dictionary<string, string> { ["window"] = window },
        },
    };
}
