namespace Beacon.Core.Services;

/// <summary>SVG path 坐标点。</summary>
public readonly record struct SvgPoint(double X, double Y);

/// <summary>路径段（绝对坐标，相对命令已展开）。</summary>
public abstract record SvgSegment;

public sealed record SvgLine(SvgPoint To) : SvgSegment;

public sealed record SvgCubic(SvgPoint Control1, SvgPoint Control2, SvgPoint To) : SvgSegment;

public sealed record SvgQuadratic(SvgPoint Control, SvgPoint To) : SvgSegment;

/// <summary>椭圆弧（端点参数原样保留，渲染层映射 ArcSegment）。</summary>
public sealed record SvgArc(double RadiusX, double RadiusY, double Rotation, bool LargeArc, bool Sweep, SvgPoint To) : SvgSegment;

/// <summary>子路径：起点 + 段序列 + 是否闭合（Z）。</summary>
public sealed record SvgFigure(SvgPoint Start, bool Closed, IReadOnlyList<SvgSegment> Segments);

/// <summary>
/// SVG path d 数据解析器（W3C SVG 1.1 path 命令全集：M/L/H/V/C/S/Q/T/A/Z 及小写相对形式，
/// 含命令省略与 M→L 隐式参数组）。纯函数供单测；结果为中性几何模型，渲染层（WinUI PathGeometry 等）
/// 自行映射——不走 XamlReader：WinUI 3 的 XamlReader.Load 对 Geometry 元素文本的行为与 WPF/UWP 不一致，
/// 解析失败会炸掉整页构建（2026-10-08 设置窗口打不开的根因）。
/// </summary>
public static class SvgPathParser
{
    public static IReadOnlyList<SvgFigure> Parse(string pathData)
    {
        var figures = new List<SvgFigure>();
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return figures;
        }

        var reader = new TokenReader(pathData);
        var current = new SvgPoint(0, 0);
        var subpathStart = current;
        SvgPoint? lastCubicControl = null;
        SvgPoint? lastQuadraticControl = null;
        var segments = new List<SvgSegment>();
        var closed = false;

        while (true)
        {
            var command = reader.NextCommandOrThrow();
            if (command is '\0')
            {
                break; // 输入耗尽
            }
            var relative = char.IsLower(command);
            var kind = char.ToUpperInvariant(command);
            var implicitLine = false; // M/m 的隐式后续参数组按 L/l 语义处理

            do
            {
                if (kind == 'M' && !implicitLine)
                {
                    var to = reader.ReadPoint(relative, current);
                    if (segments.Count > 0)
                    {
                        figures.Add(new SvgFigure(subpathStart, closed, segments));
                        segments = [];
                        closed = false;
                    }
                    subpathStart = to;
                    current = to;
                    lastCubicControl = null;
                    lastQuadraticControl = null;
                }
                else
                {
                    switch (kind)
                    {
                        case 'M': // 隐式后续 = L
                        case 'L':
                        {
                            var to = reader.ReadPoint(relative, current);
                            segments.Add(new SvgLine(to));
                            current = to;
                            lastCubicControl = null;
                            lastQuadraticControl = null;
                            break;
                        }
                        case 'H':
                        {
                            var x = reader.ReadNumber() + (relative ? current.X : 0);
                            current = new SvgPoint(x, current.Y);
                            segments.Add(new SvgLine(current));
                            lastCubicControl = null;
                            lastQuadraticControl = null;
                            break;
                        }
                        case 'V':
                        {
                            var y = reader.ReadNumber() + (relative ? current.Y : 0);
                            current = new SvgPoint(current.X, y);
                            segments.Add(new SvgLine(current));
                            lastCubicControl = null;
                            lastQuadraticControl = null;
                            break;
                        }
                        case 'C':
                        {
                            var c1 = reader.ReadPoint(relative, current);
                            var c2 = reader.ReadPoint(relative, current);
                            var to = reader.ReadPoint(relative, current);
                            segments.Add(new SvgCubic(c1, c2, to));
                            current = to;
                            lastCubicControl = c2;
                            lastQuadraticControl = null;
                            break;
                        }
                        case 'S':
                        {
                            // 第一控制点 = 上一 C/S 第二控制点关于当前点的镜像；无上文则取当前点
                            var c1 = lastCubicControl is { } prev
                                ? new SvgPoint(2 * current.X - prev.X, 2 * current.Y - prev.Y)
                                : current;
                            var c2 = reader.ReadPoint(relative, current);
                            var to = reader.ReadPoint(relative, current);
                            segments.Add(new SvgCubic(c1, c2, to));
                            current = to;
                            lastCubicControl = c2;
                            lastQuadraticControl = null;
                            break;
                        }
                        case 'Q':
                        {
                            var control = reader.ReadPoint(relative, current);
                            var to = reader.ReadPoint(relative, current);
                            segments.Add(new SvgQuadratic(control, to));
                            current = to;
                            lastQuadraticControl = control;
                            lastCubicControl = null;
                            break;
                        }
                        case 'T':
                        {
                            var control = lastQuadraticControl is { } prevQuad
                                ? new SvgPoint(2 * current.X - prevQuad.X, 2 * current.Y - prevQuad.Y)
                                : current;
                            var to = reader.ReadPoint(relative, current);
                            segments.Add(new SvgQuadratic(control, to));
                            current = to;
                            lastQuadraticControl = control;
                            lastCubicControl = null;
                            break;
                        }
                        case 'A':
                        {
                            var rx = reader.ReadNumber();
                            var ry = reader.ReadNumber();
                            var rotation = reader.ReadNumber();
                            var largeArc = reader.ReadFlag();
                            var sweep = reader.ReadFlag();
                            var to = reader.ReadPoint(relative, current);
                            segments.Add(new SvgArc(rx, ry, rotation, largeArc, sweep, to));
                            current = to;
                            lastCubicControl = null;
                            lastQuadraticControl = null;
                            break;
                        }
                        case 'Z':
                            current = subpathStart;
                            closed = true;
                            break;
                        default:
                            throw new FormatException($"SVG path 命令不支持：{command}（位置 {reader.Position}）");
                    }
                }
                implicitLine = true;
            }
            while (kind != 'Z' && reader.HasMoreArguments());
        }

        if (segments.Count > 0)
        {
            figures.Add(new SvgFigure(subpathStart, closed, segments));
        }
        return figures;
    }

    /// <summary>数字 tokenizer：识别 SVG 数字语法（分隔符可省：'1-2'='1,-2'、'12.5.3'、'1e-5'）。</summary>
    private sealed class TokenReader(string text)
    {
        private int _position;

        public int Position => _position;

        public bool End => _position >= text.Length;

        /// <summary>跳过分隔符读命令字母；输入耗尽 → '\0'；遇非字母（残缺参数）→ 抛 FormatException。</summary>
        public char NextCommandOrThrow()
        {
            SkipSeparators();
            if (End)
            {
                return '\0';
            }
            var c = text[_position];
            if (!char.IsAsciiLetter(c))
            {
                throw new FormatException($"SVG path 语法残缺：期望命令字母，实际 '{c}'（位置 {_position}）");
            }
            _position++;
            return c;
        }

        /// <summary>探测是否还有同命令的下一参数组（不消费）：下一非分隔字符存在且不是命令字母。</summary>
        public bool HasMoreArguments()
        {
            var probe = _position;
            while (probe < text.Length && (char.IsWhiteSpace(text[probe]) || text[probe] == ','))
            {
                probe++;
            }
            return probe < text.Length && !char.IsAsciiLetter(text[probe]);
        }

        public SvgPoint ReadPoint(bool relative, SvgPoint current)
        {
            var x = ReadNumber();
            var y = ReadNumber();
            return relative ? new SvgPoint(current.X + x, current.Y + y) : new SvgPoint(x, y);
        }

        public bool ReadFlag()
        {
            SkipSeparators();
            if (End || text[_position] is not ('0' or '1'))
            {
                throw new FormatException($"SVG path 弧标志缺失/非法（位置 {_position}）");
            }
            var flag = text[_position] == '1';
            _position++;
            return flag;
        }

        public double ReadNumber()
        {
            SkipSeparators();
            var start = _position;
            if (!End && (text[_position] == '+' || text[_position] == '-'))
            {
                _position++;
            }
            var mantissaDigits = 0;
            while (!End && char.IsAsciiDigit(text[_position]))
            {
                _position++;
                mantissaDigits++;
            }
            if (!End && text[_position] == '.')
            {
                _position++;
                while (!End && char.IsAsciiDigit(text[_position]))
                {
                    _position++;
                    mantissaDigits++;
                }
            }
            if (mantissaDigits == 0)
            {
                throw new FormatException($"SVG path 数字缺失（位置 {start}）");
            }
            if (!End && text[_position] is 'e' or 'E')
            {
                var probe = _position + 1;
                if (probe < text.Length && (text[probe] == '+' || text[probe] == '-'))
                {
                    probe++;
                }
                var exponentDigits = 0;
                while (probe < text.Length && char.IsAsciiDigit(text[probe]))
                {
                    probe++;
                    exponentDigits++;
                }
                if (exponentDigits > 0)
                {
                    _position = probe; // 指数完整才消费，避免吞掉后续数字的 'E' 歧义
                }
            }
            var slice = text[start.._position];
            if (!double.TryParse(slice, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"SVG path 数字非法：{slice}（位置 {start}）");
            }
            return value;
        }

        private void SkipSeparators()
        {
            while (!End && (char.IsWhiteSpace(text[_position]) || text[_position] == ','))
            {
                _position++;
            }
        }
    }
}
