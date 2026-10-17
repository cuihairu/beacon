using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>模板解析结果：Ok=false 时 Errors 给出可读诊断（调用面整面弃用回传统渲染）。</summary>
public sealed record HtmlTemplateParseResult(bool Ok, ComponentDescriptor? Root, IReadOnlyList<string> Errors);

/// <summary>
/// 受控 HTML/CSS 子集解析器（Phase 4，2026-10-17 用户裁定，决策全文
/// docs/design/phase4-html-ruling-2026-10-17.md）：htmltemplate 载体的模板文本 →
/// ComponentDescriptor 子树，交给既有声明式渲染器原生绘制——无 JavaScript、无 WebView2，
/// 词汇表即白名单：script/iframe/img/a/事件属性/外链协议在语法上不可表达（不是过滤，是词表外）。
/// 插值 {{state.x}} / {{payload.k}} / {{selector.n}} 脱糖为既有 ComponentBinding（引擎零改动，
/// 字段名/选择器名与引擎同一张白名单表）；样式只收令牌词（tone/pad/gap × 固定档位），
/// 字面色一律解析错——主题适配是语法保证而非约定。fail-closed：任何越词汇表输入 →
/// Ok=false + 首错诊断，调用面整面弃用回传统渲染，不炸常驻进程。
/// 静态纯函数（守已代决⑩，ComponentLayerInvariantTests 同表锚定）：无 IO、无运行时服务、无缓存。
/// </summary>
public static class HtmlTemplateParser
{
    /// <summary>载体节点类型键：根节点 Type 为此值时 Props.html 承载模板文本（OrdinalIgnoreCase）。</summary>
    public const string CarrierType = "htmltemplate";

    private const int MaxDepth = ComponentTreeValidator.MaxDepth;
    private const int MaxNodes = ComponentTreeValidator.MaxNodes;

    /// <summary>元素白名单 → 渲染注册表类型键（小写封闭集；渲染侧已知什么才允许声明什么）。</summary>
    private static readonly Dictionary<string, string> Elements = new(StringComparer.Ordinal)
    {
        ["card"] = "card",
        ["column"] = "column",
        ["row"] = "row",
        ["text"] = "text",
        ["section"] = "sectionlabel",
        ["metric"] = "metric",
        ["progress"] = "progress",
        ["dot"] = "statusdot",
        ["badge"] = "badge",
        ["kv"] = "keyvalue",
        ["divider"] = "divider",
        ["button"] = "actionbutton",
    };

    /// <summary>绑定槽（元素文本内容 → 绑定 Field）；不在表内的元素内容只能来自子元素，裸文本报错。</summary>
    private static readonly Dictionary<string, string> BindingSlots = new(StringComparer.Ordinal)
    {
        ["text"] = "text",
        ["section"] = "text",
        ["metric"] = "value",
        ["progress"] = "progress",
        ["dot"] = "severity",
        ["badge"] = "text",
        ["kv"] = "value",
        ["button"] = "text",
    };

    /// <summary>元素专有属性（class/when 之外）；不在表内的元素无专有属性，误挂报错。</summary>
    private static readonly Dictionary<string, string[]> OwnAttrs = new(StringComparer.Ordinal)
    {
        ["column"] = ["gap"],
        ["row"] = ["gap"],
        ["text"] = ["size"],
        ["metric"] = ["unit"],
        ["progress"] = ["width"],
        ["dot"] = ["size"],
        ["kv"] = ["key"],
        ["button"] = ["data-action"],
    };

    /// <summary>样式属性 → 允许的令牌词（封闭档位；#hex/rgb()/具名色进不了词表，模板无法脱离主题）。</summary>
    private static readonly Dictionary<string, string[]> StyleProps = new(StringComparer.Ordinal)
    {
        ["tone"] = ["info", "ok", "warn", "bad"],
        ["pad"] = ["sm", "md", "lg"],
        ["gap"] = ["sm", "md", "lg"],
    };

    public static HtmlTemplateParseResult Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new HtmlTemplateParseResult(false, null, ["template is empty"]);
        }
        try
        {
            var runner = new Runner(html.Trim());
            var root = runner.ParseDocument();
            return new HtmlTemplateParseResult(true, root, []);
        }
        catch (HtmlTemplateException e)
        {
            return new HtmlTemplateParseResult(false, null, [e.Message]);
        }
    }

    /// <summary>载体判定：根节点 Type 是否为 htmltemplate（OrdinalIgnoreCase，与渲染注册表同口径）。</summary>
    public static bool IsCarrier(ComponentDescriptor node)
        => string.Equals(node.Type, CarrierType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 载体展开（WidgetInstanceResolver / 加载净化共用）：非载体原样返回；载体要求 Props 只含
    /// html（可选 when）、无 Children、无 Bindings，模板解析成功 → 合成根（column wrapper，
    /// 透传载体 Id/when）；任何不合法 → null，调用面整面弃用回传统渲染。错误诊断由
    /// Parse().Errors 提供（加载侧主动 Parse 记 LoadErrors；解析侧 null 即 fail-closed）。
    /// </summary>
    public static ComponentDescriptor? ExpandCarrier(ComponentDescriptor root)
    {
        if (!IsCarrier(root))
        {
            return root;
        }
        foreach (var key in root.Props.Keys)
        {
            if (!key.Equals("html", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("when", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }
        var html = root.Props.GetValueOrDefault("html");
        if (root.Children.Count > 0 || root.Bindings.Count > 0 || string.IsNullOrWhiteSpace(html))
        {
            return null;
        }
        var parsed = Parse(html);
        if (!parsed.Ok || parsed.Root is null)
        {
            return null;
        }
        return new ComponentDescriptor
        {
            Id = root.Id,
            Type = parsed.Root.Type,
            Props = root.Props.TryGetValue("when", out var when)
                ? new Dictionary<string, string>(parsed.Root.Props, StringComparer.OrdinalIgnoreCase)
                    { ["when"] = when }
                : parsed.Root.Props,
            Children = parsed.Root.Children,
        };
    }

    /// <summary>解析期首错即中止（手写模板逐个修错；诊断只报第一处，避免级联噪声）。</summary>
    private sealed class HtmlTemplateException(string message) : Exception(message);

    private sealed class Runner(string s)
    {
        private int _i;
        private readonly List<ComponentDescriptor> _tops = [];
        private readonly Dictionary<string, Dictionary<string, string>> _styles = new(StringComparer.Ordinal);
        private int _nodes;
        private bool _sawElement;

        private string At => $"at offset {_i}";

        [DoesNotReturn]
        private void Fail(string message) => throw new HtmlTemplateException($"{At}: {message}");

        private bool End => _i >= s.Length;

        private bool Take(string token)
        {
            if (s.AsSpan(_i).StartsWith(token, StringComparison.Ordinal))
            {
                _i += token.Length;
                return true;
            }
            return false;
        }

        private void SkipWs()
        {
            while (!End && char.IsWhiteSpace(s[_i]))
            {
                _i++;
            }
        }

        /// <summary>文档层：style 块（必须先于其他元素）+ 顶层元素序列；裸文本/游离闭标签报错。</summary>
        public ComponentDescriptor ParseDocument()
        {
            while (true)
            {
                SkipWs();
                if (End)
                {
                    break;
                }
                if (Take("<!--"))
                {
                    SkipComment();
                    continue;
                }
                if (Peek('<'))
                {
                    var node = ParseNode(1);
                    if (node is not null)
                    {
                        _tops.Add(node);
                    }
                    continue;
                }
                Fail("bare text outside an element is not allowed (wrap it in <text>…</text>)");
            }
            if (_tops.Count == 0)
            {
                Fail("template has no visible elements");
            }
            return new ComponentDescriptor { Type = "column", Children = _tops };
        }

        private bool Peek(char c) => !End && s[_i] == c;

        private void SkipComment()
        {
            var close = s.IndexOf("-->", _i, StringComparison.Ordinal);
            if (close < 0)
            {
                Fail("unterminated comment");
            }
            _i = close + 3;
        }

        /// <summary>单节点：style 块返回 null（样式进类表），元素返回合成节点（text 混排返回行包装）。</summary>
        private ComponentDescriptor? ParseNode(int depth)
        {
            if (depth > MaxDepth)
            {
                Fail($"depth exceeds max {MaxDepth}");
            }
            _i++; // '<'
            var name = TakeName();
            if (name.Length == 0)
            {
                Fail($"expected an element name after '<'");
            }
            if (name == "style")
            {
                ParseStyleBlock();
                return null;
            }
            if (!Elements.TryGetValue(name, out var type))
            {
                Fail($"unknown element <{name}> (vocabulary is closed; see phase4-html-ruling §4)");
            }
            _sawElement = true;
            _nodes++;
            if (_nodes > MaxNodes)
            {
                Fail($"total nodes exceed max {MaxNodes}");
            }

            var attrs = ParseAttrs(name, out var selfClosing);
            var node = BuildNode(name, type, attrs);

            if (selfClosing)
            {
                return node;
            }
            var (children, text) = ParseContent(name, depth);
            ExpectClose(name);

            var slot = BindingSlots.GetValueOrDefault(name);
            if (children.Count > 0)
            {
                if (slot is not null || text.Trim().Length > 0)
                {
                    Fail($"<{name}> does not take both children and text content");
                }
                node.Children.AddRange(children);
                return node;
            }
            if (slot is null)
            {
                if (text.Trim().Length > 0)
                {
                    Fail($"bare text inside <{name}> is not allowed (wrap it in <text>…</text>)");
                }
                return node;
            }
            return ApplySlot(node, name, slot, text);
        }

        /// <summary>绑定槽内容：全字面 → Prop；整体单个插值 → 绑定；混排仅 <text> 允许 → 拆序列行包装（spacing 0）。</summary>
        private ComponentDescriptor ApplySlot(ComponentDescriptor node, string name, string slot, string text)
        {
            var collapsed = CollapseWs(text).Trim();
            var segments = SplitSegments(collapsed);
            if (segments.Count == 1 && !segments[0].IsInterp)
            {
                node.Props[slot] = collapsed;
                return node;
            }
            if (segments.Count == 1 && segments[0].IsInterp)
            {
                node.Bindings.Add(new ComponentBinding
                {
                    Field = slot,
                    Source = SourceOf(segments[0].Text),
                });
                return node;
            }
            if (name != "text")
            {
                Fail($"<{name}> content must be one literal or one interpolation (only <text> may mix)");
            }
            return WrapSequence(node, segments);
        }

        /// <summary>混排脱糖（裁定 §5）：行包装（spacing 0）承原节点属性（tone/when 照常生效），
        /// literal/绑定 text 节点成序列，专有属性 size 随子走。</summary>
        private ComponentDescriptor WrapSequence(ComponentDescriptor node, List<(bool IsInterp, string Text)> segments)
        {
            var wrapper = new ComponentDescriptor
            {
                Type = "row",
                Props = new Dictionary<string, string>(node.Props, StringComparer.OrdinalIgnoreCase)
                    { ["spacing"] = "0" },
                Bindings = node.Bindings,
            };
            node.Props.TryGetValue("size", out var size);
            foreach (var segment in segments)
            {
                if (segment.Text.Trim().Length == 0)
                {
                    continue;
                }
                var childProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (size is not null)
                {
                    childProps["size"] = size;
                }
                if (segment.IsInterp)
                {
                    childProps["text"] = "";
                    wrapper.Children.Add(new ComponentDescriptor
                    {
                        Type = "text",
                        Props = childProps,
                        Bindings =
                        [
                            new ComponentBinding { Field = "text", Source = SourceOf(segment.Text) },
                        ],
                    });
                }
                else
                {
                    childProps["text"] = segment.Text.Trim();
                    wrapper.Children.Add(new ComponentDescriptor { Type = "text", Props = childProps });
                }
            }
            if (wrapper.Children.Count == 0)
            {
                Fail("<text> has empty content");
            }
            return wrapper;
        }

        /// <summary>元素内容：子元素序列 + 直属文本（折叠空白）；文本里的 {{...}} 延后由 ApplySlot 处理。</summary>
        private (List<ComponentDescriptor> Children, string Text) ParseContent(string name, int depth)
        {
            var children = new List<ComponentDescriptor>();
            var text = new StringBuilder();
            while (true)
            {
                if (End)
                {
                    Fail($"missing </{name}>");
                }
                if (Take("<!--"))
                {
                    SkipComment();
                    continue;
                }
                if (Peek('<'))
                {
                    if (Take("</"))
                    {
                        _i -= 2;
                        return (children, text.ToString());
                    }
                    var child = ParseNode(depth + 1);
                    if (child is not null)
                    {
                        children.Add(child);
                    }
                    continue;
                }
                text.Append(s[_i]);
                _i++;
            }
        }

        private void ExpectClose(string name)
        {
            if (!Take("</"))
            {
                Fail($"expected </{name}>");
            }
            SkipWs();
            var close = TakeName();
            SkipWs();
            if (!Peek('>'))
            {
                Fail($"malformed close tag for <{name}>");
            }
            _i++;
            if (!string.Equals(close, name, StringComparison.Ordinal))
            {
                Fail($"close tag </{close}> does not match open <{name}>");
            }
        }

        /// <summary>属性区：双引号值封闭集；{{ 与 & 在属性值中一律报错（属性是配置面，无插值）。</summary>
        private Dictionary<string, string> ParseAttrs(string name, out bool selfClosing)
        {
            selfClosing = false;
            var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "class", "when" };
            if (OwnAttrs.TryGetValue(name, out var own))
            {
                foreach (var a in own)
                {
                    allowed.Add(a);
                }
            }
            while (true)
            {
                SkipWs();
                if (Peek('>'))
                {
                    _i++;
                    return attrs;
                }
                if (Take("/>"))
                {
                    selfClosing = true;
                    return attrs;
                }
                if (End)
                {
                    Fail($"malformed attributes on <{name}>");
                }
                var attr = TakeAttrName();
                if (attr.Length == 0)
                {
                    Fail($"malformed attribute name on <{name}>");
                }
                if (!allowed.Contains(attr))
                {
                    Fail($"unknown attribute '{attr}' on <{name}> (vocabulary is closed)");
                }
                if (attrs.ContainsKey(attr))
                {
                    Fail($"duplicate attribute '{attr}' on <{name}>");
                }
                SkipWs();
                if (!Peek('='))
                {
                    Fail($"attribute '{attr}' on <{name}> needs a double-quoted value");
                }
                _i++;
                SkipWs();
                if (!Peek('"'))
                {
                    Fail($"attribute '{attr}' on <{name}> needs a double-quoted value");
                }
                _i++;
                var value = TakeUntilQuote(name, attr);
                attrs[attr] = value;
            }
        }

        private string TakeUntilQuote(string name, string attr)
        {
            var start = _i;
            var sb = new StringBuilder();
            while (!End && s[_i] != '"')
            {
                if (s[_i] == '&')
                {
                    Fail($"attribute '{attr}' must not contain '&' (entities are not in the vocabulary)");
                }
                sb.Append(s[_i]);
                _i++;
            }
            if (End)
            {
                Fail($"unterminated attribute value for '{attr}' on <{name}>");
            }
            _i++; // closing quote
            var value = sb.ToString();
            if (value.Contains("{{", StringComparison.Ordinal))
            {
                Fail($"attribute '{attr}' does not take interpolation (only binding slots do; see ruling §5)");
            }
            return value;
        }

        private void ParseStyleBlock()
        {
            if (_sawElement)
            {
                Fail("<style> blocks must precede all other elements");
            }
            SkipWs();
            if (!Peek('>'))
            {
                Fail("<style> takes no attributes and no self-closing");
            }
            _i++;
            var start = _i;
            var close = s.IndexOf("</style>", start, StringComparison.Ordinal);
            if (close < 0)
            {
                Fail("missing </style>");
            }
            ParseStyles(s[start..close]);
            _i = close;
            ExpectClose("style");
        }

        /// <summary>样式表：.name { prop: token; … } 一类一规则；字面色/未知 prop/未知档位即解析错（裁定 §7）。</summary>
        private void ParseStyles(string css)
        {
            var i = 0;
            while (true)
            {
                while (i < css.Length && char.IsWhiteSpace(css[i]))
                {
                    i++;
                }
                if (i >= css.Length)
                {
                    return;
                }
                if (css[i] != '.')
                {
                    Fail($"style rules must be .class {{ … }} (at style offset {i})");
                }
                i++;
                var name = TakeStyleName(css, ref i);
                if (name.Length == 0 || !char.IsAsciiLetterLower(name[0]))
                {
                    Fail($"invalid class name in style block (at style offset {i})");
                }
                while (i < css.Length && char.IsWhiteSpace(css[i]))
                {
                    i++;
                }
                if (i >= css.Length || css[i] != '{')
                {
                    Fail($"expected '{{' after .{name} (at style offset {i})");
                }
                i++;
                var close = css.IndexOf('}', i);
                if (close < 0)
                {
                    Fail($"unterminated rule .{name}");
                }
                if (_styles.ContainsKey(name))
                {
                    Fail($"duplicate class definition .{name}");
                }
                var props = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var raw in css[i..close].Split(';'))
                {
                    var piece = raw.Trim();
                    if (piece.Length == 0)
                    {
                        continue;
                    }
                    var colon = piece.IndexOf(':');
                    if (colon <= 0)
                    {
                        Fail($"style declaration must be 'prop: token' (got '{piece}')");
                    }
                    var prop = piece[..colon].Trim();
                    var token = piece[(colon + 1)..].Trim();
                    if (!StyleProps.TryGetValue(prop, out var tokens))
                    {
                        Fail($"unknown style prop '{prop}' (vocabulary: tone/pad/gap)");
                    }
                    if (!tokens.Contains(token))
                    {
                        Fail($"invalid token '{token}' for style prop '{prop}' (literal colors are not in the vocabulary)");
                    }
                    props[prop] = token;
                }
                if (props.Count == 0)
                {
                    Fail($"empty rule .{name}");
                }
                _styles[name] = props;
                i = close + 1;
            }
        }

        /// <summary>节点装配：class → 令牌 props；专有属性逐个校验（data-action 静态字面、宽度正数…）。</summary>
        private ComponentDescriptor BuildNode(string name, string type, Dictionary<string, string> attrs)
        {
            var node = new ComponentDescriptor { Type = type };
            if (attrs.TryGetValue("class", out var classList))
            {
                foreach (var token in classList.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!_styles.TryGetValue(token, out var props))
                    {
                        Fail($"unknown class '{token}' on <{name}> (declare it in a <style> block first)");
                    }
                    foreach (var prop in props)
                    {
                        node.Props[prop.Key] = prop.Value;
                    }
                }
            }
            foreach (var attr in attrs)
            {
                switch (attr.Key)
                {
                    case "class":
                        break;
                    case "when":
                        if (attr.Value.Trim().Length == 0)
                        {
                            Fail("'when' needs a non-empty condition");
                        }
                        node.Props["when"] = attr.Value.Trim();
                        break;
                    case "gap":
                        if (!StyleProps["gap"]!.Contains(attr.Value))
                        {
                            Fail($"gap must be sm/md/lg (got '{attr.Value}')");
                        }
                        node.Props["gap"] = attr.Value;
                        break;
                    case "size" when name == "text":
                        if (attr.Value is not ("body" or "caption"))
                        {
                            Fail($"text size must be body/caption (got '{attr.Value}')");
                        }
                        node.Props["size"] = attr.Value;
                        break;
                    case "size":
                        if (!double.TryParse(attr.Value, CultureInfo.InvariantCulture, out var dotSize) || dotSize <= 0)
                        {
                            Fail($"dot size must be a positive number (got '{attr.Value}')");
                        }
                        node.Props["size"] = attr.Value;
                        break;
                    case "unit":
                    case "key":
                        if (attr.Value.Trim().Length == 0)
                        {
                            Fail($"'{attr.Key}' needs a non-empty value");
                        }
                        node.Props[attr.Key] = attr.Value.Trim();
                        break;
                    case "width":
                        if (!double.TryParse(attr.Value, CultureInfo.InvariantCulture, out var width) || width <= 0)
                        {
                            Fail($"width must be a positive number (got '{attr.Value}')");
                        }
                        node.Props["width"] = attr.Value;
                        break;
                    case "data-action":
                        if (attr.Value.Length == 0 || attr.Value.Any(char.IsWhiteSpace))
                        {
                            Fail("data-action must be a static action id (no whitespace, no interpolation)");
                        }
                        node.Props["data-action"] = attr.Value;
                        break;
                }
            }
            if (name == "button" && !node.Props.ContainsKey("data-action"))
            {
                Fail("<button> requires a data-action attribute (controlled bridge only)");
            }
            return node;
        }

        /// <summary>插值 → 绑定源：state 字段/payload 键/selector 名全部查引擎同一张白名单表（裁定 §5）。</summary>
        private static ComponentBindingSource SourceOf(string inner)
        {
            var dot = inner.IndexOf('.');
            var prefix = dot < 0 ? inner : inner[..dot];
            var rest = dot < 0 ? "" : inner[(dot + 1)..];
            switch (prefix)
            {
                case "state":
                    var fields = ComponentBindingEngine.StateFieldNames;
                    if (rest.Length == 0 || !fields.Contains(rest, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new HtmlTemplateException($"unsupported state field '{inner}' (closed field set; see binding engine)");
                    }
                    return new ComponentBindingSource { Kind = ComponentBindingSource.KindState, Ref = rest };
                case "payload":
                    if (rest.Length == 0 || rest.Contains(':'))
                    {
                        throw new HtmlTemplateException($"unsupported payload key '{inner}' (non-empty key, no colon)");
                    }
                    return new ComponentBindingSource { Kind = ComponentBindingSource.KindState, Ref = $"payload:{rest}" };
                case "selector":
                    var selectors = ComponentBindingEngine.SelectorNames;
                    if (rest.Length == 0 || !selectors.Contains(rest, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new HtmlTemplateException($"unknown selector '{inner}' (registry: value-hint/quota-windows)");
                    }
                    return new ComponentBindingSource { Kind = ComponentBindingSource.KindSelector, Ref = rest };
                default:
                    throw new HtmlTemplateException($"unsupported interpolation '{{{{{inner}}}}}' (state./payload./selector. only)");
            }
        }

        private List<(bool IsInterp, string Text)> SplitSegments(string text)
        {
            var segments = new List<(bool, string)>();
            var literal = new StringBuilder();
            for (var i = 0; i < text.Length;)
            {
                if (i + 1 < text.Length && text[i] == '{' && text[i + 1] == '{')
                {
                    if (literal.Length > 0)
                    {
                        segments.Add((false, literal.ToString()));
                        literal.Clear();
                    }
                    var close = text.IndexOf("}}", i + 2, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        Fail("unterminated interpolation '{{…}}'");
                    }
                    var inner = text[(i + 2)..close].Trim();
                    if (inner.Length == 0)
                    {
                        Fail("empty interpolation '{{}}'");
                    }
                    segments.Add((true, inner));
                    i = close + 2;
                    continue;
                }
                if (text[i] == '&')
                {
                    Fail("'&' is not in the vocabulary (entities and bare & both are errors)");
                }
                if (text[i] == '}')
                {
                    Fail("bare '}}' outside interpolation");
                }
                literal.Append(text[i]);
                i++;
            }
            if (literal.Length > 0)
            {
                segments.Add((false, literal.ToString()));
            }
            return segments;
        }

        private string TakeName()
        {
            var start = _i;
            while (!End && (char.IsAsciiLetterLower(s[_i]) || char.IsAsciiDigit(s[_i])))
            {
                _i++;
            }
            return s[start.._i];
        }

        private string TakeAttrName()
        {
            var start = _i;
            while (!End && (char.IsAsciiLetterLower(s[_i]) || s[_i] == '-'))
            {
                _i++;
            }
            return s[start.._i];
        }

        private static string TakeStyleName(string css, ref int i)
        {
            var start = i;
            while (i < css.Length && (char.IsAsciiLetterLower(css[i]) || char.IsAsciiDigit(css[i]) || css[i] == '-'))
            {
                i++;
            }
            return css[start..i];
        }

        /// <summary>折叠空白为单空格（文本槽语义；外缘 trim 在 ApplySlot）。</summary>
        private static string CollapseWs(string text)
            => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
