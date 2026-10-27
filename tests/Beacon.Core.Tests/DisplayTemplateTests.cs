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
        Assert.Equal(5, DisplayTemplates.All.Count);
        Assert.Equal(5, DisplayTemplates.All.Select(template => template.Id).Distinct().Count());
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
        var ids = DisplayTemplates.Compatible(State(0.52)).Select(template => template.Id).ToHashSet();
        Assert.Equal(5, ids.Count);
        Assert.Contains("ring", ids);
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
    public void StatusLine_is_the_only_template_without_progress_requirement()
    {
        Assert.Equal(1, DisplayTemplates.All.Count(template => !template.RequiresProgress));
        var root = Template("statusline").Build();
        var dotBinding = Assert.Single(root.Children[0].Bindings);
        Assert.Equal("severity", dotBinding.Field);
        Assert.Equal("severity", dotBinding.Source.Ref);
    }

    [Fact]
    public void Percent_is_a_registered_selector_name()
        => Assert.Contains("percent", ComponentBindingEngine.SelectorNames);

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
    public void Every_template_has_a_parameter_schema_matching_its_props()
    {
        foreach (var template in DisplayTemplates.All)
        {
            var tree = template.Build();
            Assert.NotEmpty(template.Parameters);
            foreach (var (key, spec) in template.Parameters)
            {
                // 参数键必须落在生成树某节点 Props 上（覆盖才有目标）
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
}
