using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// HtmlTemplateParser 词表/属性/样式块边缘收口（Batch 5）：属性区各失败臂（缺引号/缺等号/重复/
/// 含 &/未闭合）、样式块各失败臂（带属性/缺闭合/非点规则/非法类名/缺花括号/未终止规则/
/// 重复定义/空规则/未知 prop/非法档位）、slot 冲突臂（子元素+文本、card 内裸文本）、
/// 载体 when 透传与大小写不敏感判定、混排 size 随子走。全部 fail-closed 纯函数。
/// </summary>
public sealed class HtmlTemplateParserVocabEdgeTests
{
    private static string FirstError(string html)
        => Assert.Single(HtmlTemplateParser.Parse(html).Errors);

    // ---------- 属性区失败臂 ----------

    [Fact]
    public void Attrs_EofInsideAttrs_Fails()
        => Assert.Contains("malformed attributes", FirstError("""<card class="a" """));

    [Fact]
    public void Attrs_MissingEquals_Fails()
        => Assert.Contains("needs a double-quoted value", FirstError("""<card class when="ok"/>"""));

    [Fact]
    public void Attrs_UnquotedValue_Fails()
        => Assert.Contains("needs a double-quoted value", FirstError("""<card when=ok/>"""));

    [Fact]
    public void Attrs_AmpersandInValue_Fails()
        => Assert.Contains("must not contain '&'", FirstError("""<card class="a&b"/>"""));

    [Fact]
    public void Attrs_DuplicateAttribute_Fails()
        => Assert.Contains("duplicate attribute", FirstError("""<card when="a" when="b"/>"""));

    [Fact]
    public void Attrs_UnterminatedValue_Fails()
        => Assert.Contains("unterminated attribute value", FirstError("""<card class="abc"""));

    // ---------- 节点/slot 失败臂 ----------

    [Fact]
    public void Node_MissingElementName_Fails()
        => Assert.Contains("expected an element name", FirstError("<card><</card>"));

    [Fact]
    public void Slot_ChildrenPlusText_Fails()
        => Assert.Contains("does not take both children and text content", FirstError("<card>hi<text>x</text></card>"));

    [Fact]
    public void Slot_BareTextInsideNonSlotElement_Fails()
        => Assert.Contains("bare text inside", FirstError("<card>hi</card>"));

    [Fact]
    public void Close_MissingRootClose_Fails()
        => Assert.Contains("missing </card>", FirstError("<card><text>x</text>"));

    // ---------- 样式块失败臂 ----------

    [Fact]
    public void Style_WithAttribute_Fails()
        => Assert.Contains("takes no attributes", FirstError("""<style media="x"></style>"""));

    [Fact]
    public void Style_MissingCloseTag_Fails()
        => Assert.Contains("missing </style>", FirstError("<style>.a{tone:info}</card>"));

    [Fact]
    public void Style_NonDotRule_Fails()
        => Assert.Contains("style rules must be .class", FirstError("<style>color: red</style>"));

    [Fact]
    public void Style_ClassNameNotLetter_Fails()
        => Assert.Contains("invalid class name", FirstError("<style>.1x{tone:info}</style>"));

    [Fact]
    public void Style_MissingBrace_Fails()
        => Assert.Contains("expected '{'", FirstError("<style>.a tone:info</style>"));

    [Fact]
    public void Style_UnterminatedRule_Fails()
        => Assert.Contains("unterminated rule", FirstError("<style>.a{tone:info</style>"));

    [Fact]
    public void Style_DuplicateDefinition_Fails()
        => Assert.Contains("duplicate class definition", FirstError("""<style>.a{tone:info}.a{gap:sm}</style><card class="a"/>"""));

    [Fact]
    public void Style_EmptyRule_Fails()
        => Assert.Contains("empty rule", FirstError("<style>.a{}</style>"));

    [Fact]
    public void Style_UnknownProp_Fails()
        => Assert.Contains("unknown style prop", FirstError("""<style>.a{color:info}</style><card class="a"/>"""));

    [Fact]
    public void Style_InvalidToken_Fails()
        => Assert.Contains("invalid token 'huge'", FirstError("""<style>.a{gap:huge}</style><card class="a"/>"""));

    // ---------- 正路径边缘 ----------

    [Fact]
    public void Style_MultipleTokens_SameClass_Applied()
    {
        var result = HtmlTemplateParser.Parse("""<style>.a{tone:info;pad:lg}</style><card class="a"/>""");
        Assert.True(result.Ok);
        var card = Assert.Single(result.Root!.Children);
        Assert.Equal("info", card.Props["tone"]);
        Assert.Equal("lg", card.Props["pad"]);
    }

    [Fact]
    public void TextMixed_SizeFollowsChildren_IntoWrapper()
    {
        var result = HtmlTemplateParser.Parse("""<text size="caption">{{state.summary}} done</text>""");
        Assert.True(result.Ok);
        var wrapper = Assert.Single(result.Root!.Children);
        Assert.Equal("row", wrapper.Type);
        Assert.Equal("0", wrapper.Props["spacing"]);
        Assert.All(wrapper.Children, child => Assert.Equal("caption", child.Props["size"]));
    }

    [Fact]
    public void IsCarrier_CaseInsensitive()
    {
        Assert.True(HtmlTemplateParser.IsCarrier(new ComponentDescriptor { Type = "HTMLTEMPLATE" }));
        Assert.False(HtmlTemplateParser.IsCarrier(new ComponentDescriptor { Type = "card" }));
    }

    [Fact]
    public void ExpandCarrier_WhenProp_PassthroughsWithCarrierIdentity()
    {
        var carrier = new ComponentDescriptor
        {
            Id = "tpl-1",
            Type = HtmlTemplateParser.CarrierType,
            Props = new Dictionary<string, string>
            {
                ["html"] = "<text>{{state.summary}}</text>",
                ["when"] = "stale",
            },
        };

        var expanded = HtmlTemplateParser.ExpandCarrier(carrier);

        Assert.NotNull(expanded);
        Assert.Equal("tpl-1", expanded!.Id);
        Assert.Equal("stale", expanded.Props["when"]);
        Assert.Equal("column", expanded.Type);
    }
}
