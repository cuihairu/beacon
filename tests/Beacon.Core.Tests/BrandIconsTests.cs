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
    [InlineData("openai")] // OpenAI（Simple Icons slug openai，Codex 卡）
    public void TryGet_KnownBrand_ReturnsNonEmptyPath(string brand)
    {
        Assert.True(BrandIcons.TryGet(brand, out var pathData));
        Assert.False(string.IsNullOrWhiteSpace(pathData));
    }

    [Theory]
    [InlineData("uptimekuma", "uptime-kuma")] // Simple Icons slug 带连字符
    [InlineData("mimo", "xiaomi")]            // MiMo 走小米品牌标
    [InlineData("codex", "openai")]           // Codex 属 OpenAI
    [InlineData("copilot", "githubcopilot")]  // Copilot slug 无连字符
    public void TryGet_ConnectionTypeAlias_ResolvesCanonicalEntry(string connectionType, string canonical)
    {
        // 别名归一发生在查表时（初始化器内自引用会 NRE）——连接类型名与品牌 slug 不一致的必须归一到同一条 path
        Assert.True(BrandIcons.TryGet(connectionType, out var viaAlias));
        Assert.True(BrandIcons.TryGet(canonical, out var direct));
        Assert.Equal(direct, viaAlias);
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

    [Fact]
    public void PickerKeys_AllResolve_NoDuplicates_LeadsWithGenericSet()
    {
        // 自选图标集（icon 字段下拉）：每个键必须可渲染、不重复；通用集（box/bolt/…）在前——
        // 打包机等自定义组件第一眼看到的是泛型图标而非品牌标
        var keys = BrandIcons.PickerKeys;
        Assert.True(keys.Count >= 16, $"选择器键过少：{keys.Count}");
        Assert.Equal(keys.Count, keys.Distinct().Count());
        foreach (var key in keys)
        {
            Assert.True(BrandIcons.TryGet(key, out var path), $"选择器键 {key} 无对应 path");
            Assert.False(string.IsNullOrWhiteSpace(path));
        }
        Assert.Equal("box", keys[0]); // 通用集打头
        Assert.Contains("github", keys); // 品牌标仍可选
    }
}
