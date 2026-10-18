using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// 2026-10-18 外部评审 P1：展示模板注册表——纯函数生成器、能力筛选与树合法性。
/// 每份模板产物都必须过 ComponentTreeValidator（与保存链前置校验同一张口径），
/// 保证模板产物可直接落盘；percent 选择器无进度时回空串，不编造确定数字。
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
    public void Number_uses_percent_selector_for_metric_value()
    {
        var root = Template("number").Build();
        var metric = Assert.Single(root.Children, child => child.Type == "metric");
        var binding = Assert.Single(metric.Bindings);
        Assert.Equal(("value", ComponentBindingSource.KindSelector, "percent"),
            (binding.Field, binding.Source.Kind, binding.Source.Ref));
        Assert.Equal("%", metric.Props["unit"]);
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
        Assert.Equal("50%", ComponentBindingEngine.ResolveBinding(binding, State(0.5)));
        Assert.Equal("75%", ComponentBindingEngine.ResolveBinding(binding, State(0.75)));
        Assert.Equal("100%", ComponentBindingEngine.ResolveBinding(binding, State(0.999)));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(binding, State(null)));
        Assert.Equal("", ComponentBindingEngine.ResolveBinding(binding, null));
    }

    private static DisplayTemplates.DisplayTemplate Template(string id)
    {
        var matches = DisplayTemplates.Match(id);
        Assert.Single(matches);
        return matches[0];
    }
}
