using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 4 受控 HTML/CSS 子集解析器（2026-10-17 裁定，docs/design/phase4-html-ruling-2026-10-17.md）：
/// 六行验收的 Core 半证据——模板加载 / 受控数据桥接 / Action ID 桥接 / 安全与错误回退 /
/// 主题令牌 / 增量更新（绑定重求值不动模板树）。安全面的核心断言：词汇表封闭即白名单，
/// script/iframe/img/a/事件属性/实体/字面色全部解析错（不是过滤，是词表外）。
/// </summary>
public sealed class HtmlTemplateParserTests
{
    private static ComponentDescriptor Ok(string html)
    {
        var result = HtmlTemplateParser.Parse(html);
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        return result.Root!;
    }

    private static string Fail(string html)
    {
        var result = HtmlTemplateParser.Parse(html);
        Assert.False(result.Ok, "expected parse failure");
        return Assert.Single(result.Errors);
    }

    private static ComponentDescriptor Find(ComponentDescriptor root, string type)
        => TryFind(root, type)
           ?? throw new Xunit.Sdk.XunitException($"no node of type {type}");

    private static ComponentDescriptor? TryFind(ComponentDescriptor root, string type)
    {
        if (string.Equals(root.Type, type, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }
        foreach (var child in root.Children)
        {
            var found = TryFind(child, type);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static ComponentBinding BindingOf(ComponentDescriptor node, string field)
        => node.Bindings.Single(b => string.Equals(b.Field, field, StringComparison.OrdinalIgnoreCase));

    // ---------- 模板加载（行 174） ----------

    [Fact]
    public void Parse_WrapsInColumn_AndMapsElements()
    {
        var root = Ok("<card><text>hello</text></card>");
        Assert.Equal("column", root.Type);
        var card = Assert.Single(root.Children);
        Assert.Equal("card", card.Type);
        var text = Assert.Single(card.Children);
        Assert.Equal("text", text.Type);
        Assert.Equal("hello", text.Props["text"]);
    }

    [Theory]
    [InlineData("card", "<card/>")]
    [InlineData("column", "<column/>")]
    [InlineData("row", "<row/>")]
    [InlineData("text", "<text>hi</text>")]
    [InlineData("sectionlabel", "<section>title</section>")]
    [InlineData("metric", "<metric>42</metric>")]
    [InlineData("progress", "<progress>0.5</progress>")]
    [InlineData("statusdot", "<dot>ok</dot>")]
    [InlineData("badge", "<badge>3</badge>")]
    [InlineData("keyvalue", "<kv key=\"runs\">12</kv>")]
    [InlineData("divider", "<divider/>")]
    [InlineData("actionbutton", "<button data-action=\"a\">Go</button>")]
    public void Parse_MapsEveryVocabularyElement(string expectedType, string html)
    {
        Assert.Equal(expectedType, Ok(html).Children[0].Type);
    }

    [Fact]
    public void Parse_CarriesAttributesThrough()
    {
        var root = Ok(
            "<column gap=\"md\" when=\"severity=bad\">" +
            "<metric unit=\"ms\">120</metric>" +
            "<progress width=\"72\">0.5</progress>" +
            "<dot size=\"10\">ok</dot>" +
            "<kv key=\"runs\">12</kv>" +
            "<text size=\"caption\">cap</text>" +
            "</column>");
        var column = root.Children[0];
        Assert.Equal("md", column.Props["gap"]);
        Assert.Equal("severity=bad", column.Props["when"]);
        Assert.Equal("ms", column.Children[0].Props["unit"]);
        Assert.Equal("72", column.Children[1].Props["width"]);
        Assert.Equal("10", column.Children[2].Props["size"]);
        Assert.Equal("runs", column.Children[3].Props["key"]);
        Assert.Equal("caption", column.Children[4].Props["size"]);
    }

    [Fact]
    public void Parse_SkipsComments_AndAllowsMultipleTopElements()
    {
        var root = Ok("<!-- header --><divider/><!-- x --><divider/>");
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Parse_EmptyTemplate_Fails()
    {
        Assert.Contains("empty", Fail(""));
        Assert.Contains("empty", Fail("   "));
    }

    [Fact]
    public void Parse_BareTextAtTopLevel_Fails()
    {
        Assert.Contains("bare text", Fail("hello"));
    }

    [Fact]
    public void Parse_CloseMismatch_Fails()
    {
        Assert.Contains("does not match", Fail("<card><text>hi</card></text>"));
    }

    // ---------- 受控数据桥接（行 175） ----------

    [Fact]
    public void Parse_StateInterpolation_BecomesBinding()
    {
        var text = Find(Ok("<text>{{state.summary}}</text>"), "text");
        var binding = BindingOf(text, "text");
        Assert.Equal(ComponentBindingSource.KindState, binding.Source.Kind);
        Assert.Equal("summary", binding.Source.Ref);
        Assert.False(text.Props.ContainsKey("text"));
    }

    [Fact]
    public void Parse_PayloadInterpolation_MapsToColonRef()
    {
        var metric = Find(Ok("<metric>{{payload.run_id}}</metric>"), "metric");
        var binding = BindingOf(metric, "value");
        Assert.Equal("payload:run_id", binding.Source.Ref);
    }

    [Fact]
    public void Parse_SelectorInterpolation_BecomesSelectorSource()
    {
        var text = Find(Ok("<text>{{selector.value-hint}}</text>"), "text");
        Assert.Equal(ComponentBindingSource.KindSelector, BindingOf(text, "text").Source.Kind);
    }

    [Fact]
    public void Parse_MixedTextContent_DesugarsToSequence()
    {
        var wrapper = Find(Ok("<text size=\"caption\">Runtime: {{state.summary}} ms</text>"), "row");
        Assert.Equal("0", wrapper.Props["spacing"]);
        Assert.Equal("caption", wrapper.Props["size"]);
        Assert.Equal(3, wrapper.Children.Count);
        Assert.Equal("Runtime:", wrapper.Children[0].Props["text"]);
        Assert.Equal("state", BindingOf(wrapper.Children[1], "text").Source.Kind);
        Assert.Equal("ms", wrapper.Children[2].Props["text"]);
    }

    [Fact]
    public void Parse_MixedContentOutsideText_Fails()
    {
        Assert.Contains("only <text> may mix", Fail("<metric>used: {{state.progress}}</metric>"));
    }

    [Theory]
    [InlineData("{{state.bogus}}")]
    [InlineData("{{selector.bogus}}")]
    [InlineData("{{evil.x}}")]
    [InlineData("{{payload.}}")]
    [InlineData("{{state}}")]
    [InlineData("{{}}")]
    public void Parse_UnsupportedInterpolation_Fails(string interp)
    {
        Assert.NotEmpty(Fail($"<text>{interp}</text>"));
    }

    [Fact]
    public void Parse_InterpolationInAttribute_Fails()
    {
        Assert.Contains("does not take interpolation", Fail("<column gap=\"{{state.summary}}\"/>"));
    }

    [Fact]
    public void Parse_UnterminatedInterpolation_Fails()
    {
        Assert.Contains("unterminated interpolation", Fail("<text>{{state.summary</text>"));
    }

    // ---------- Action ID 桥接（行 176 Core 半） ----------

    [Fact]
    public void Parse_ButtonBridgesStaticActionId()
    {
        var button = Find(Ok("<button data-action=\"rerun-failed\">Retry</button>"), "actionbutton");
        Assert.Equal("rerun-failed", button.Props["data-action"]);
        Assert.Equal("Retry", button.Props["text"]);
    }

    [Fact]
    public void Parse_ButtonWithoutAction_Fails()
    {
        Assert.Contains("data-action", Fail("<button>Go</button>"));
    }

    [Fact]
    public void Parse_ActionIdWithWhitespace_Fails()
    {
        Assert.Contains("static action id", Fail("<button data-action=\"rerun failed\">Go</button>"));
    }

    // ---------- 安全与错误回退（行 177 Core 半） ----------

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<iframe src=\"https://x\"/>")]
    [InlineData("<img src=\"file:///c:/x.png\"/>")]
    [InlineData("<a href=\"https://x\">go</a>")]
    [InlineData("<text onclick=\"alert(1)\">x</text>")]
    [InlineData("<text>&amp;</text>")]
    [InlineData("<text>a & b</text>")]
    [InlineData("<foo>x</foo>")]
    [InlineData("<card><text>x</text></badge></card>")]
    [InlineData("<text class=\"x\" class=\"y\">t</text>")]
    [InlineData("<TEXT>x</TEXT>")]
    public void Parse_OutOfVocabulary_Fails(string html)
    {
        Assert.NotEmpty(Fail(html));
    }

    [Fact]
    public void Parse_DepthAndNodeCaps_Fail()
    {
        Assert.NotEmpty(Fail(string.Concat(Enumerable.Repeat("<column>", 10)) + "<text>x</text>"
            + string.Concat(Enumerable.Repeat("</column>", 10))));
        Assert.NotEmpty(Fail("<column>" + string.Concat(Enumerable.Repeat("<divider/>", 300)) + "</column>"));
    }

    [Fact]
    public void ExpandCarrier_NonCarrier_PassesThrough()
    {
        var node = new ComponentDescriptor { Type = "text", Props = { ["text"] = "x" } };
        Assert.Same(node, HtmlTemplateParser.ExpandCarrier(node));
    }

    [Fact]
    public void ExpandCarrier_GoodTemplate_SynthesizesTreeWithCarrierIdentity()
    {
        var carrier = new ComponentDescriptor
        {
            Id = "tpl",
            Type = HtmlTemplateParser.CarrierType,
            Props = { ["html"] = "<text>hi</text>", ["when"] = "severity=bad" },
        };
        var expanded = HtmlTemplateParser.ExpandCarrier(carrier);
        Assert.NotNull(expanded);
        Assert.Equal("tpl", expanded!.Id);
        Assert.Equal("column", expanded.Type);
        Assert.Equal("severity=bad", expanded.Props["when"]);
        Assert.Equal("text", Assert.Single(expanded.Children).Type);
    }

    [Fact]
    public void ExpandCarrier_CarrierWithChildren_Fails()
    {
        var carrier = new ComponentDescriptor
        {
            Type = HtmlTemplateParser.CarrierType,
            Props = { ["html"] = "<column><text>x</text></column>" },
            Children = [new ComponentDescriptor { Type = "text" }],
        };
        Assert.Null(HtmlTemplateParser.ExpandCarrier(carrier));
    }

    [Fact]
    public void ExpandCarrier_CarrierWithUnknownProp_Fails()
    {
        var carrier = new ComponentDescriptor
        {
            Type = HtmlTemplateParser.CarrierType,
            Props = { ["html"] = "<text>x</text>", ["html2"] = "y" },
        };
        Assert.Null(HtmlTemplateParser.ExpandCarrier(carrier));
    }

    [Fact]
    public void ExpandCarrier_EmptyOrBrokenTemplate_Fails()
    {
        Assert.Null(HtmlTemplateParser.ExpandCarrier(new ComponentDescriptor
        {
            Type = HtmlTemplateParser.CarrierType,
            Props = { ["html"] = "  " },
        }));
        Assert.Null(HtmlTemplateParser.ExpandCarrier(new ComponentDescriptor
        {
            Type = HtmlTemplateParser.CarrierType,
            Props = { ["html"] = "<script>x</script>" },
        }));
        Assert.Null(HtmlTemplateParser.ExpandCarrier(new ComponentDescriptor
        {
            Type = HtmlTemplateParser.CarrierType,
        }));
    }

    // ---------- 主题令牌（行 178 Core 半） ----------

    [Fact]
    public void Parse_StyleClasses_ApplyTokenProps()
    {
        var root = Ok(
            "<style>.hot { tone: bad; } .box { pad: md; } .flow { gap: lg; }</style>" +
            "<card class=\"box\"><text class=\"hot\">down</text><column class=\"flow\"/></card>");
        var text = Find(root, "text");
        Assert.Equal("bad", text.Props["tone"]);
        var card = Find(root, "card");
        Assert.Equal("md", card.Props["pad"]);
        Assert.Equal("lg", Find(card, "column").Props["gap"]);
    }

    [Fact]
    public void Parse_AttrOverridesStyleToken()
    {
        var root = Ok("<style>.flow { gap: lg; }</style><row class=\"flow\" gap=\"sm\"/>");
        Assert.Equal("sm", root.Children[0].Props["gap"]);
    }

    [Fact]
    public void Parse_LiteralColors_Fail()
    {
        Assert.Contains("invalid token", Fail("<style>.x { tone: #ff0000; }</style><text class=\"x\">t</text>"));
        Assert.Contains("unknown style prop", Fail("<style>.x { color: red; }</style><text class=\"x\">t</text>"));
        Assert.Contains("invalid token", Fail("<style>.x { pad: xl; }</style><text class=\"x\">t</text>"));
    }

    [Fact]
    public void Parse_UnknownOrDuplicateClass_Fails()
    {
        Assert.Contains("unknown class", Fail("<text class=\"nope\">t</text>"));
        Assert.Contains("duplicate class", Fail("<style>.a { pad: sm; } .a { pad: md; }</style><text class=\"a\">t</text>"));
    }

    [Fact]
    public void Parse_StyleAfterElement_Fails()
    {
        Assert.Contains("must precede", Fail("<divider/><style>.a { pad: sm; }</style>"));
    }

    // ---------- 增量更新（行 179 Core 半） ----------

    private static WidgetState State(string summary, double progress)
        => new()
        {
            WidgetId = "w-1",
            WidgetType = "github.actions.runs",
            ConnectionId = "conn-1",
            Severity = Severity.Info,
            Summary = summary,
            Progress = progress,
            FetchedAt = new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero),
        };

    [Fact]
    public void Incremental_SameTree_ReevaluatedPerStateSnapshot()
    {
        var root = Ok("<text>Runtime: {{state.summary}} ms</text>");
        var instance = new WidgetInstance
        {
            Id = "w-1",
            Type = "github.actions.runs",
            Surface = "detail",
            Config = new Dictionary<string, string>(),
            Root = root,
            Sources = new Dictionary<string, string>(),
        };

        var first = ComponentBindingEngine.Resolve(instance, State("run A", 0.25));
        var second = ComponentBindingEngine.Resolve(instance, State("run B", 0.5));
        // 树内唯一绑定：键随引擎路径口径派生，值随状态快照变化；合成树对象不变——
        // 数据更新走结构化绑定重求值，不触碰模板文本（裁定 §8）
        var key = Assert.Single(first.Keys);
        Assert.Equal(first.Keys, second.Keys);
        Assert.Equal("run A", first[key]);
        Assert.Equal("run B", second[key]);
        Assert.Same(root, instance.Root);
    }

    [Fact]
    public void EngineExposures_MatchEngineTables()
    {
        // 模板白名单与引擎同一张表（不另写第二份）——抽样锚定
        Assert.Contains("summary", ComponentBindingEngine.StateFieldNames);
        Assert.Contains("value-hint", ComponentBindingEngine.SelectorNames);
        Assert.DoesNotContain("value-hint", ComponentBindingEngine.StateFieldNames);
    }
}
