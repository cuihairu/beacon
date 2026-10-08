using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>SvgPathParser：SVG path d → 中性几何（品牌图标渲染前置层，2026-10-08 设置窗口打不开根因的替代实现）。</summary>
public sealed class SvgPathParserTests
{
    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        Assert.Empty(SvgPathParser.Parse(""));
        Assert.Empty(SvgPathParser.Parse("   "));
    }

    [Fact]
    public void Parse_LineClose()
    {
        var figures = SvgPathParser.Parse("M0 0 L10 0 Z");

        var figure = Assert.Single(figures);
        Assert.Equal(new SvgPoint(0, 0), figure.Start);
        Assert.True(figure.Closed);
        var line = Assert.IsType<SvgLine>(Assert.Single(figure.Segments));
        Assert.Equal(new SvgPoint(10, 0), line.To);
    }

    [Fact]
    public void Parse_RelativeCubic_GithubPrefix()
    {
        // GitHub 图标开头：相对 c 命令 + 省分隔符数字（"-12 5.373-12 12"）
        var figures = SvgPathParser.Parse("M12 .297c-6.63 0-12 5.373-12 12");

        var figure = Assert.Single(figures);
        Assert.Equal(new SvgPoint(12, 0.297), figure.Start);
        var cubic = Assert.IsType<SvgCubic>(Assert.Single(figure.Segments));
        Assert.Equal(new SvgPoint(5.37, 0.297), cubic.Control1);   // 12-6.63, 0.297+0
        Assert.Equal(new SvgPoint(0, 5.67), cubic.Control2);       // 12-12, 0.297+5.373
        Assert.Equal(new SvgPoint(0, 12.297), cubic.To);           // 12-12, 0.297+12
    }

    [Fact]
    public void Parse_HorizontalVertical()
    {
        var figure = SvgPathParser.Parse("M1 2H5V8").Single();

        Assert.Collection(
            figure.Segments,
            segment => Assert.Equal(new SvgPoint(5, 2), Assert.IsType<SvgLine>(segment).To),
            segment => Assert.Equal(new SvgPoint(5, 8), Assert.IsType<SvgLine>(segment).To));
    }

    [Fact]
    public void Parse_ImplicitLineAfterMove()
    {
        // M 后隐式参数组按 L 处理
        var figure = SvgPathParser.Parse("M0 0 10 10 20 0").Single();

        Assert.Equal(2, figure.Segments.Count); // 不含移动本身
        Assert.All(figure.Segments, segment => Assert.IsType<SvgLine>(segment));
    }

    [Fact]
    public void Parse_SmoothCubic_ReflectsControl()
    {
        // S 第一控制点 = 上一 C 第二控制点 (2,1) 关于当前点 (3,0) 的镜像 = (4,-1)
        var figure = SvgPathParser.Parse("M0 0 C1 1 2 1 3 0 S5 -1 6 0").Single();

        Assert.Equal(2, figure.Segments.Count);
        var smooth = Assert.IsType<SvgCubic>(figure.Segments[1]);
        Assert.Equal(new SvgPoint(4, -1), smooth.Control1);
        Assert.Equal(new SvgPoint(5, -1), smooth.Control2);
        Assert.Equal(new SvgPoint(6, 0), smooth.To);
    }

    [Fact]
    public void Parse_MultipleSubpaths()
    {
        var figures = SvgPathParser.Parse("M0 0 L1 1 M5 5 L6 6");

        Assert.Equal(2, figures.Count);
        Assert.Equal(new SvgPoint(0, 0), figures[0].Start);
        Assert.Equal(new SvgPoint(5, 5), figures[1].Start);
    }

    [Fact]
    public void Parse_Arc()
    {
        var segment = Assert.IsType<SvgArc>(SvgPathParser.Parse("M0 0A5 5 0 0 1 10 10").Single().Segments.Single());

        Assert.Equal(5, segment.RadiusX);
        Assert.Equal(5, segment.RadiusY);
        Assert.Equal(0, segment.Rotation);
        Assert.False(segment.LargeArc);
        Assert.True(segment.Sweep);
        Assert.Equal(new SvgPoint(10, 10), segment.To);
    }

    [Fact]
    public void Parse_CloseReturnsToSubpathStart()
    {
        // Z 后 current 回子路径起点，后续 L 从那里出发
        var figure = SvgPathParser.Parse("M2 2 L4 4 Z L9 9").Single();

        Assert.True(figure.Closed);
        Assert.Equal(2, figure.Segments.Count);
        Assert.Equal(new SvgPoint(9, 9), Assert.IsType<SvgLine>(figure.Segments[1]).To);
    }

    [Theory]
    [InlineData("M1-2l0 0", 1, -2)]       // '-' 作分隔
    [InlineData("M12.5.3l0 0", 12.5, 0.3)] // '.' 作分隔
    [InlineData("M1e2 2l0 0", 100, 2)]    // 科学计数
    [InlineData("M 1 , 2l0 0", 1, 2)]     // 逗号/空白混合
    public void Parse_NumberSyntax(string pathData, double x, double y)
    {
        // M 后补零长线段：M-only 无绘制段本就不产生 figure（无可渲染内容）
        Assert.Equal(new SvgPoint(x, y), SvgPathParser.Parse(pathData).Single().Start);
    }

    [Fact]
    public void Parse_InvalidCommand_Throws()
    {
        Assert.Throws<FormatException>(() => SvgPathParser.Parse("X0 0"));
    }

    [Fact]
    public void Parse_TruncatedNumber_Throws()
    {
        Assert.Throws<FormatException>(() => SvgPathParser.Parse("M0 0 L"));
    }

    // ---- 真实品牌 path 端到端（Simple Icons 提取，小体积两个代表）——全部命令可解析且坐标落在合理范围 ----

    public static TheoryData<string, string> BrandPaths => new()
    {
        {
            "gitlab",
            "m23.6004 9.5927-.0337-.0862L20.3.9814a.851.851 0 0 0-.3362-.405.8748.8748 0 0 0-.9997.0539.8748.8748 0 0 0-.3237.4259l-1.3665 4.1469h-9.7807l-1.3665-4.1469a.8748.8748 0 0 0-.3237-.4259.8748.8748 0 0 0-.9997-.0539.851.851 0 0 0-.3362.405l-3.2662 8.5252c-.5015 1.2983.5611 2.6879 1.9471 2.6879h5.0539l2.0355 5.0361c.1225.3116.4225.5164.7565.5164.334 0 .634-.2048.7565-.5164l2.0355-5.0361h5.0538c1.386 0 2.4486-1.3896 1.9471-2.6879z"
        },
        {
            "kimi",
            "M11.971 3.304c-.57-.678-1.69-.278-1.687.632l.033 8.047-2.345-2.426a.896.896 0 0 0-1.29 0 .94.94 0 0 0-.266.656c0 .246.096.482.266.656l4.255 4.404a.896.896 0 0 0 1.29 0l4.255-4.404a.94.94 0 0 0 .266-.656.94.94 0 0 0-.266-.656.896.896 0 0 0-1.29 0l-2.37 2.452V3.94c0-.24-.09-.47-.251-.636z"
        },
    };

    [Theory]
    [MemberData(nameof(BrandPaths))]
    public void Parse_RealBrandPaths_AllSegmentsInBounds(string brand, string pathData)
    {
        var figures = SvgPathParser.Parse(pathData);

        Assert.NotEmpty(figures);
        foreach (var figure in figures)
        {
            AssertPointInBounds(figure.Start);
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case SvgLine line: AssertPointInBounds(line.To); break;
                    case SvgCubic cubic:
                        AssertPointInBounds(cubic.Control1);
                        AssertPointInBounds(cubic.Control2);
                        AssertPointInBounds(cubic.To);
                        break;
                    case SvgQuadratic quadratic:
                        AssertPointInBounds(quadratic.Control);
                        AssertPointInBounds(quadratic.To);
                        break;
                    case SvgArc arc:
                        Assert.True(arc.RadiusX > 0 && arc.RadiusY > 0, $"{brand}: 弧半径非法");
                        AssertPointInBounds(arc.To);
                        break;
                }
            }
        }
    }

    /// <summary>Simple Icons 均 24×24 viewBox：允许控制点轻微越界（曲线外凸），不允许数量级跑飞。</summary>
    private static void AssertPointInBounds(SvgPoint point)
    {
        Assert.InRange(point.X, -2, 26);
        Assert.InRange(point.Y, -2, 26);
    }
}
