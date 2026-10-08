using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// BrandIcons：品牌 path 字典（Simple Icons CC0 内置）。放 Core 的原因即这些测试——
/// 留在 App 层时 Linux 测不到，静态初始化器自引用（["uptimekuma"] = Paths[...]）只在
/// 运行时首次点设置才 NullReferenceException（2026-10-08 设置窗口打不开实证）。
/// </summary>
public sealed class BrandIconsTests
{
    [Theory]
    [InlineData("github")]
    [InlineData("gitlab")]
    [InlineData("jenkins")]
    [InlineData("docker")]
    [InlineData("sonarqube")]
    [InlineData("claude")]
    [InlineData("kimi")]
    [InlineData("deepseek")]
    [InlineData("uptime-kuma")]
    [InlineData("bigmodel")] // 智谱（Simple Icons 无 slug，自绘 Z 字母——用户指名）
    [InlineData("ark")] // 火山方舟（Simple Icons 无 slug，自绘火山剪影）
    [InlineData("xiaomi")] // 小米（Simple Icons slug xiaomi）
    public void TryGet_KnownBrand_ReturnsNonEmptyPath(string brand)
    {
        Assert.True(BrandIcons.TryGet(brand, out var pathData));
        Assert.False(string.IsNullOrWhiteSpace(pathData));
    }

    [Fact]
    public void TryGet_AliasUptimeKuma_ResolvesCanonicalEntry()
    {
        // 别名归一发生在查表时（初始化器内自引用会 NRE）——两种拼写都命中同一条 path
        Assert.True(BrandIcons.TryGet("uptimekuma", out var viaAlias));
        Assert.True(BrandIcons.TryGet("uptime-kuma", out var canonical));
        Assert.Equal(canonical, viaAlias);
    }

    [Theory]
    [InlineData("unknown-brand")]
    [InlineData("")]
    public void TryGet_UnknownBrand_ReturnsFalse(string brand)
    {
        Assert.False(BrandIcons.TryGet(brand, out var pathData));
        Assert.Null(pathData);
    }

    [Fact]
    public void AllBrandPaths_AreParseableBySvgPathParser()
    {
        // 全量 path 必须过 SvgPathParser（渲染前置层）——字符串坏一块这里就红，
        // 不必等装机点设置才发现
        var reflection = typeof(BrandIcons);
        var pathsField = reflection.GetField("Paths",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(pathsField);
        var paths = Assert.IsType<Dictionary<string, string>>(pathsField.GetValue(null));

        Assert.NotEmpty(paths);
        foreach (var (brand, pathData) in paths)
        {
            var figures = SvgPathParser.Parse(pathData);
            Assert.True(figures.Count > 0, $"{brand} 的 path 解析不出任何图形");
        }
    }
}
