using System.Text;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// HtmlTemplateParser 分支补齐（2026-11-09 覆盖率 98% 收口）：codecov 把「行命中但非全分支」计为未覆盖，
/// 本文件专打剩余 partial 臂——空/空白输入、载体展开属性组合、深度/节点上限、自闭合、slot 冲突、
/// 单插值、混排、插值边界（空/未闭合/裸括号）、& 词表外、button 缺 data-action、gap 非法档位。
/// </summary>
public sealed class HtmlTemplateParserCoverageTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \n\t")]
    public void Parse_EmptyOrWhitespace_FailsEmpty(string html)
    {
        // L84 `IsNullOrWhiteSpace` 真臂：现有套件只测过合法模板，从未喂空/空白
        var result = HtmlTemplateParser.Parse(html);
        Assert.False(result.Ok);
        Assert.Equal("template is empty", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_LeadingWhitespace_StillParses()
    {
        // L176 SkipWs 循环「消费后遇非空白退出」臂
        var result = HtmlTemplateParser.Parse("  \n\t <text>hi</text>");
        Assert.True(result.Ok);
        Assert.Equal("column", result.Root!.Type);
        Assert.Single(result.Root.Children);
    }

    [Fact]
    public void Parse_TopLevelComment_ThenElement_Ok()
    {
        // L192 Take("<!--") 真臂（顶层注释）
        var result = HtmlTemplateParser.Parse("<!-- c --><text>hi</text>");
        Assert.True(result.Ok);
        Assert.Single(result.Root!.Children);
    }

    [Fact]
    public void Parse_MultipleTopLevelElements_AllWrappedInSyntheticColumn()
    {
        // L185 文档层 while 回边真臂：顶层元素连排（循环第二次迭代）→ 全部进合成 column 根
        var result = HtmlTemplateParser.Parse("<text>a</text><badge>b</badge>");
        Assert.True(result.Ok);
        Assert.Equal("column", result.Root!.Type);
        Assert.Equal(2, result.Root.Children.Count);
        Assert.Equal("text", result.Root.Children[0].Type);
        Assert.Equal("badge", result.Root.Children[1].Type);
    }

    [Fact]
    public void Parse_CommentOnly_NoVisibleElements_Fails()
    {
        // L208 _tops.Count==0 真臂
        var result = HtmlTemplateParser.Parse("<!-- only a comment -->");
        Assert.False(result.Ok);
        Assert.Contains("template has no visible elements", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_BareTextAtTop_Fails()
    {
        // L197 Peek('<') 假臂（裸文本）
        var result = HtmlTemplateParser.Parse("hello");
        Assert.False(result.Ok);
        Assert.Contains("bare text outside an element", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_DepthBeyondMax_Fails()
    {
        // L230 depth > MaxDepth(8) 真臂
        var html = new StringBuilder();
        for (var i = 0; i < 10; i++)
        {
            html.Append("<column>");
        }
        html.Append("<text>hi</text>");
        for (var i = 0; i < 10; i++)
        {
            html.Append("</column>");
        }
        var result = HtmlTemplateParser.Parse(html.ToString());
        Assert.False(result.Ok);
        Assert.Contains("depth exceeds max", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_EmptyElementName_Fails()
    {
        // L236 name.Length==0 真臂
        var result = HtmlTemplateParser.Parse("<>");
        Assert.False(result.Ok);
        Assert.Contains("expected an element name", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_NodesBeyondMax_Fails()
    {
        // L251 _nodes > MaxNodes(256) 真臂：1 column + 258 自闭合 divider
        var html = new StringBuilder("<column>");
        for (var i = 0; i < 258; i++)
        {
            html.Append("<divider/>");
        }
        html.Append("</column>");
        var result = HtmlTemplateParser.Parse(html.ToString());
        Assert.False(result.Ok);
        Assert.Contains("total nodes exceed max", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_SelfClosingElement_ReturnsEarly()
    {
        // L259 selfClosing 真臂
        var result = HtmlTemplateParser.Parse("<divider/>");
        Assert.True(result.Ok);
        Assert.Equal("divider", result.Root!.Children.Single().Type);
    }

    [Theory]
    [InlineData("<text><divider/></text>", "does not take both children and text")]
    [InlineData("<card>hi<text>x</text></card>", "does not take both children and text")]
    public void Parse_SlotWithChildrenConflict_Fails(string html, string expected)
    {
        // L269 `slot is not null || text.Trim().Length > 0` 两个真臂
        var result = HtmlTemplateParser.Parse(html);
        Assert.False(result.Ok);
        Assert.Contains(expected, Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_NoSlotElementWithChildren_Ok()
    {
        // L276 slot is null 真臂
        var result = HtmlTemplateParser.Parse("<column><text>hi</text></column>");
        Assert.True(result.Ok);
        Assert.Single(result.Root!.Children);
    }

    [Fact]
    public void Parse_NoSlotEmptyElement_Ok()
    {
        // L278 `text.Trim().Length > 0` 假臂：无子无文本的空元素
        var result = HtmlTemplateParser.Parse("<column></column>");
        Assert.True(result.Ok);
        Assert.Empty(result.Root!.Children.Single().Children);
    }

    [Fact]
    public void Parse_NoSlotElementWithBareText_Fails()
    {
        // L278 text.Trim().Length > 0 真臂
        var result = HtmlTemplateParser.Parse("<card>hello</card>");
        Assert.False(result.Ok);
        Assert.Contains("bare text inside <card>", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_SingleInterpolation_BecomesBinding()
    {
        // L297 单插值真臂 + L689/690 state 合法臂
        var result = HtmlTemplateParser.Parse("<text>{{state.summary}}</text>");
        Assert.True(result.Ok);
        var text = result.Root!.Children.Single();
        var binding = Assert.Single(text.Bindings);
        Assert.Equal("text", binding.Field);
        Assert.Equal(ComponentBindingSource.KindState, binding.Source.Kind);
        Assert.Equal("summary", binding.Source.Ref);
    }

    [Fact]
    public void Parse_TextWhitespaceOnly_HasEmptyContent_Fails()
    {
        // L355 wrapper.Children.Count==0 真臂（空白段全部跳过）
        var result = HtmlTemplateParser.Parse("<text>   </text>");
        Assert.False(result.Ok);
        Assert.Contains("<text> has empty content", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_ContentComment_Ok()
    {
        // L373 内容层 Take("<!--") 真臂
        var result = HtmlTemplateParser.Parse("<column><!-- c --><text>hi</text></column>");
        Assert.True(result.Ok);
        Assert.Single(result.Root!.Children);
    }

    [Fact]
    public void Parse_MismatchedCloseTag_Fails()
    {
        // L411 close != name 真臂
        var result = HtmlTemplateParser.Parse("<text>hi</div>");
        Assert.False(result.Ok);
        Assert.Contains("does not match open <text>", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_InvalidGapToken_Fails()
    {
        // L634 gap 非法档位真臂
        var result = HtmlTemplateParser.Parse("<row gap=\"xl\"><text>hi</text></row>");
        Assert.False(result.Ok);
        Assert.Contains("gap must be sm/md/lg", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_ValidGapToken_Ok()
    {
        // L634 gap 合法档位假臂
        var result = HtmlTemplateParser.Parse("<row gap=\"lg\"><text>hi</text></row>");
        Assert.True(result.Ok);
        Assert.Equal("lg", result.Root!.Children.Single().Props["gap"]);
    }

    [Fact]
    public void Parse_ButtonWithoutAction_Fails()
    {
        // L678 button 缺 data-action 真臂
        var result = HtmlTemplateParser.Parse("<button>Go</button>");
        Assert.False(result.Ok);
        Assert.Contains("requires a data-action", Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_ButtonWithAction_Ok()
    {
        // L678 假臂（有 data-action）
        var result = HtmlTemplateParser.Parse("<button data-action=\"open\">Go</button>");
        Assert.True(result.Ok);
        Assert.Equal("open", result.Root!.Children.Single().Props["data-action"]);
    }

    [Fact]
    public void Parse_StyleAndClass_AppliesProps()
    {
        // L240 style 元素臂 + L606 class 真臂
        var result = HtmlTemplateParser.Parse("<style>.a { tone: ok; }</style><badge class=\"a\">S</badge>");
        Assert.True(result.Ok);
        Assert.Equal("ok", result.Root!.Children.Single().Props["tone"]);
    }

    [Theory]
    [InlineData("<text>{{state}}</text>", "unsupported state field")]
    [InlineData("<text>{{state.bogus}}</text>", "unsupported state field")]
    [InlineData("<text>{{payload}}</text>", "unsupported payload key")]
    [InlineData("<text>{{payload.a:b}}</text>", "unsupported payload key")]
    [InlineData("<text>{{state.summary</text>", "unterminated interpolation")]
    [InlineData("<text>{{}}</text>", "empty interpolation")]
    [InlineData("<text>a & b</text>", "'&' is not in the vocabulary")]
    [InlineData("<text>a }}</text>", "bare '}}' outside interpolation")]
    public void Parse_InterpolationBoundaries_Fail(string html, string expected)
    {
        // L689/690/695/732/737/745/749 边界真臂
        var result = HtmlTemplateParser.Parse(html);
        Assert.False(result.Ok);
        Assert.Contains(expected, Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_PayloadInterpolation_BecomesPayloadBinding()
    {
        // L695 payload 合法臂（Ref 带 payload: 前缀）
        var result = HtmlTemplateParser.Parse("<text>{{payload.cpu}}</text>");
        Assert.True(result.Ok);
        var binding = Assert.Single(result.Root!.Children.Single().Bindings);
        Assert.Equal("payload:cpu", binding.Source.Ref);
    }

    [Fact]
    public void Parse_MixedContent_WrapsSequence()
    {
        // L726 字面量缓冲遇 {{ 的落段臂 + WrapSequence 混排
        var result = HtmlTemplateParser.Parse("<text>up {{state.summary}} down</text>");
        Assert.True(result.Ok);
        var row = result.Root!.Children.Single();
        Assert.Equal("row", row.Type);
        Assert.Equal(3, row.Children.Count); // "up" / 绑定段 / "down"
    }

    // ---- 载体展开（ExpandCarrier）分支 ----

    private static ComponentDescriptor Carrier(Dictionary<string, string> props, List<ComponentDescriptor>? children = null, List<ComponentBinding>? bindings = null) => new()
    {
        Id = "w-tpl",
        Type = HtmlTemplateParser.CarrierType,
        Props = props,
        Children = children ?? [],
        Bindings = bindings ?? [],
    };

    [Fact]
    public void ExpandCarrier_NonCarrier_ReturnsSelf()
    {
        var node = new ComponentDescriptor { Id = "w", Type = "card", Props = new(StringComparer.OrdinalIgnoreCase) };
        Assert.Same(node, HtmlTemplateParser.ExpandCarrier(node));
    }

    [Fact]
    public void ExpandCarrier_PropsWithUnknownKey_ReturnsNull()
    {
        // L118 未知键（非 html 非 when）真臂
        Assert.Null(HtmlTemplateParser.ExpandCarrier(Carrier(new(StringComparer.OrdinalIgnoreCase) { ["html"] = "<text>hi</text>", ["bad"] = "x" })));
    }

    [Fact]
    public void ExpandCarrier_PropsWithWhenOnly_ReturnsNull()
    {
        // L125 空 html（whitespace）真臂
        Assert.Null(HtmlTemplateParser.ExpandCarrier(Carrier(new(StringComparer.OrdinalIgnoreCase) { ["when"] = "x" })));
    }

    [Fact]
    public void ExpandCarrier_WithChildren_ReturnsNull()
    {
        // L125 Children.Count > 0 真臂
        Assert.Null(HtmlTemplateParser.ExpandCarrier(Carrier(
            new(StringComparer.OrdinalIgnoreCase) { ["html"] = "<text>hi</text>" },
            children: [new ComponentDescriptor { Type = "text", Props = new(StringComparer.OrdinalIgnoreCase) }])));
    }

    [Fact]
    public void ExpandCarrier_WithBindings_ReturnsNull()
    {
        // L125 Bindings.Count > 0 真臂
        Assert.Null(HtmlTemplateParser.ExpandCarrier(Carrier(
            new(StringComparer.OrdinalIgnoreCase) { ["html"] = "<text>hi</text>" },
            bindings: [new ComponentBinding { Field = "text", Source = new ComponentBindingSource { Kind = ComponentBindingSource.KindState, Ref = "summary" } }])));
    }

    [Fact]
    public void ExpandCarrier_ValidHtml_TransfersWhen()
    {
        // L134 返回合成根 + L138 when 透传真臂
        var expanded = HtmlTemplateParser.ExpandCarrier(Carrier(
            new(StringComparer.OrdinalIgnoreCase) { ["html"] = "<badge>GO</badge>", ["when"] = "w" }))!;
        Assert.Equal("column", expanded.Type); // 合成根（列包装）
        Assert.Equal("badge", expanded.Children.Single().Type);
        Assert.Equal("w", expanded.Props["when"]); // when 透传到合成根
        Assert.Equal("w-tpl", expanded.Id);
    }

    [Fact]
    public void ExpandCarrier_ValidHtmlNoWhen_KeepsProps()
    {
        // L138 假臂（无 when → 原样 Props）
        var expanded = HtmlTemplateParser.ExpandCarrier(Carrier(
            new(StringComparer.OrdinalIgnoreCase) { ["html"] = "<badge>GO</badge>" }))!;
        Assert.Equal("column", expanded.Type);
        Assert.Equal("badge", expanded.Children.Single().Type);
        Assert.False(expanded.Props.ContainsKey("when"));
    }
}
