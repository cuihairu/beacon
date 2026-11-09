using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// SVG path 读取器命令面收口（Batch 5）：T/t 反射二次贝塞尔（有/无前驱）、非法命令字母、
/// 弧标志缺失、科学计数法、非法数字、同命令隐式重复参数组。均走 public Parse 纯函数。
/// </summary>
public sealed class SvgPathReaderEdgeTests
{
    private static SvgPoint P(double x, double y) => new(x, y);

    [Fact]
    public void Parse_TCommand_ReflectsPreviousQuadratic()
    {
        var figure = Assert.Single(SvgPathParser.Parse("M0 0 Q2 4 4 0 T8 0"));
        Assert.Equal(P(0, 0), figure.Start);
        Assert.Equal(2, figure.Segments.Count);
        var quad = Assert.IsType<SvgQuadratic>(figure.Segments[1]);
        Assert.Equal(P(6, -4), quad.Control); // 2*(4,0) - (2,4)
        Assert.Equal(P(8, 0), quad.To);
    }

    [Fact]
    public void Parse_TWithoutPreviousQuadratic_UsesCurrentPoint()
    {
        var figure = Assert.Single(SvgPathParser.Parse("M0 0 L4 0 T8 4"));
        var quad = Assert.IsType<SvgQuadratic>(figure.Segments[1]);
        Assert.Equal(P(4, 0), quad.Control);
        Assert.Equal(P(8, 4), quad.To);
    }

    [Fact]
    public void Parse_RelativeT_ReflectsAndOffsets()
    {
        var figure = Assert.Single(SvgPathParser.Parse("M0 0 Q2 2 4 0 t4 0"));
        var quad = Assert.IsType<SvgQuadratic>(figure.Segments[1]);
        Assert.Equal(P(6, -2), quad.Control);
        Assert.Equal(P(8, 0), quad.To);
    }

    [Fact]
    public void Parse_InvalidCommandLetter_Throws()
    {
        // 首位即数字：非命令字母处解析器报语法残缺（M 的隐式重复参数组是合法后续，故不在此类）
        var ex = Assert.Throws<FormatException>(() => SvgPathParser.Parse("5 5"));
        Assert.Contains("命令字母", ex.Message);
    }

    [Fact]
    public void Parse_ArcFlagMissingOrInvalid_Throws()
    {
        Assert.Throws<FormatException>(() => SvgPathParser.Parse("M0 0 A4 4 0 5 1 8 8"));
    }

    [Fact]
    public void Parse_ScientificNotation_ParsedAsNumber()
    {
        var figure = Assert.Single(SvgPathParser.Parse("M1e2 2 L1.5e1 3"));
        Assert.Equal(P(100, 2), figure.Start);
        Assert.Equal(P(15, 3), Assert.IsType<SvgLine>(figure.Segments[0]).To);
    }

    [Fact]
    public void Parse_SignedExponent_ConsumesSignThenDigits()
    {
        // L273 指数带符号真臂：e+/e- 后的符号位被 probe 消费，数字整体当一个 token 读
        var figure = Assert.Single(SvgPathParser.Parse("M1e+2 2 L1e-1 3"));
        Assert.Equal(P(100, 2), figure.Start);
        Assert.Equal(P(0.1, 3), Assert.IsType<SvgLine>(figure.Segments[0]).To);
    }

    // 注：L289「数字非法」臂经分析为死码——ReadNumber 只产出语法合法 double，
    // .NET Core 3.0+ double.TryParse 对溢出（1e999）返回 true（+∞）而非 false，
    // 故不写测试（见 todo 覆盖率收口条目）。

    [Fact]
    public void Parse_InvalidNumber_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => SvgPathParser.Parse("Mx0 0"));
        Assert.Contains("数字", ex.Message); // 数字缺失（非数字非法——后者只在 TryParse 失败时）
    }

    [Fact]
    public void Parse_ImplicitRepeatedParams_SameCommand()
    {
        var figure = Assert.Single(SvgPathParser.Parse("M0 0 L2 2 4 4 6 6"));
        Assert.Equal(3, figure.Segments.Count);
    }
}
