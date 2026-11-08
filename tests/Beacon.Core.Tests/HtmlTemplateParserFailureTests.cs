using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

public sealed class HtmlTemplateParserFailureTests
{
    [Theory]
    [InlineData("<card nope=\"x\"/>", "unknown attribute 'nope'")]
    [InlineData("<card data-action=\"x\"/>", "unknown attribute 'data-action'")]
    [InlineData("<style><div>bad</div></style>", "style rules must be .class")]
    [InlineData("<style>. { pad: sm; }</style>", "invalid class name")]
    [InlineData("<style>.a { pad: sm;</style>", "unterminated rule .a")]
    [InlineData("<style>.a { pad sm; }</style>", "must be 'prop: token'")]
    [InlineData("<style>.a { empty: ; }</style>", "unknown style prop 'empty'")]
    [InlineData("<style>.a {; }</style>", "empty rule .a")]
    public void Parse_MalformedAttributeOrStyle_FailsClosed(string html, string expected)
    {
        var result = HtmlTemplateParser.Parse(html);

        Assert.False(result.Ok);
        Assert.Contains(expected, Assert.Single(result.Errors));
    }

    [Fact]
    public void Parse_UnterminatedComment_AtTopLevelAndContent_Fails()
    {
        Assert.Equal("at offset 4: unterminated comment", Assert.Single(HtmlTemplateParser.Parse("<!-- no close").Errors));
        Assert.Equal(
            "at offset 10: unterminated comment",
            Assert.Single(HtmlTemplateParser.Parse("<card><!-- no close").Errors));
    }

    [Fact]
    public void Parse_MalformedCloseAndMissingClose_Fails()
    {
        Assert.Contains("malformed attribute name", Assert.Single(HtmlTemplateParser.Parse("<card<text>x</text>").Errors));
        Assert.Contains("malformed close tag", Assert.Single(HtmlTemplateParser.Parse("<card><text>x</card bad>").Errors));
        Assert.Contains("missing </text>", Assert.Single(HtmlTemplateParser.Parse("<card><text>x").Errors));
    }

    [Fact]
    public void Parse_BareAmpersandOrCloseBraces_Fails()
    {
        Assert.Contains("'&' is not in the vocabulary", Assert.Single(HtmlTemplateParser.Parse("<text>a & b</text>").Errors));
        Assert.Contains("bare '}}'", Assert.Single(HtmlTemplateParser.Parse("<text>done}}</text>").Errors));
        Assert.Contains("empty interpolation", Assert.Single(HtmlTemplateParser.Parse("<text>{{}}</text>").Errors));
    }

    [Fact]
    public void Parse_UnsupportedInterpolationPrefixesAndSlots_Fail()
    {
        Assert.Contains("unsupported state field", Assert.Single(HtmlTemplateParser.Parse("<dot>{{state.bogus}}</dot>").Errors));
        Assert.Contains("unsupported payload key", Assert.Single(HtmlTemplateParser.Parse("<dot>{{payload.a:b}}</dot>").Errors));
        Assert.Contains("unknown selector", Assert.Single(HtmlTemplateParser.Parse("<dot>{{selector.bogus}}</dot>").Errors));
        Assert.Contains("unsupported interpolation", Assert.Single(HtmlTemplateParser.Parse("<dot>{{other.value}}</dot>").Errors));
        Assert.Contains("only <text> may mix", Assert.Single(HtmlTemplateParser.Parse("<dot>ok {{state.summary}}</dot>").Errors));
        Assert.Contains("only <text> may mix", Assert.Single(HtmlTemplateParser.Parse("<dot>{{nope}} {{also-nope}}</dot>").Errors));
    }

    [Fact]
    public void Parse_NodeAttributeValidation_Fails()
    {
        Assert.Contains("text size", Assert.Single(HtmlTemplateParser.Parse("<text size=\"huge\">x</text>").Errors));
        Assert.Contains("dot size", Assert.Single(HtmlTemplateParser.Parse("<dot size=\"0\">ok</dot>").Errors));
        Assert.Contains("needs a non-empty value", Assert.Single(HtmlTemplateParser.Parse("<metric unit=\"  \">1</metric>").Errors));
        Assert.Contains("needs a non-empty value", Assert.Single(HtmlTemplateParser.Parse("<kv key=\"  \">1</kv>").Errors));
        Assert.Contains("width", Assert.Single(HtmlTemplateParser.Parse("<progress width=\"zero\">1</progress>").Errors));
        Assert.Contains("'when' needs a non-empty condition", Assert.Single(HtmlTemplateParser.Parse("<card when=\" \"/>").Errors));
    }

    [Fact]
    public void ExpandCarrier_CarrierWithBindings_Fails()
    {
        var carrier = new ComponentDescriptor
        {
            Type = HtmlTemplateParser.CarrierType,
            Props = { ["html"] = "<text>shown</text>" },
            Bindings = [new ComponentBinding { Field = "text", Source = new ComponentBindingSource { Kind = ComponentBindingSource.KindState, Ref = "summary" } }],
        };

        Assert.Null(HtmlTemplateParser.ExpandCarrier(carrier));
    }
}
