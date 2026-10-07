using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-706 验收：Widget &gt; 级别 &gt; 默认优先级、非法值回退、offline 键、六级默认色。</summary>
public sealed class PaletteResolverTests
{
    [Fact]
    public void Resolve_NoOverrides_ReturnsDefaultForEverySeverity()
    {
        foreach (var (severity, hex) in PaletteResolver.Defaults)
        {
            Assert.Equal(Parse(hex), PaletteResolver.Resolve(null, null, severity, offline: false));
        }
        Assert.Equal(Parse(PaletteResolver.DefaultOfflineHex), PaletteResolver.Resolve(null, null, Severity.Warning, offline: true));
    }

    [Fact]
    public void Resolve_LevelOverride_BeatsDefault()
    {
        var levels = new Dictionary<string, string> { ["warning"] = "#112233" };
        Assert.Equal(0xff112233u, PaletteResolver.Resolve(levels, null, Severity.Warning, offline: false));
    }

    [Fact]
    public void Resolve_WidgetOverride_BeatsLevelOverride()
    {
        var levels = new Dictionary<string, string> { ["error"] = "#112233" };
        Assert.Equal(0xff445566u, PaletteResolver.Resolve(levels, "#445566", Severity.Error, offline: false));
    }

    [Theory]
    [InlineData("not-a-color", "#112233")] // Widget 坏值 → 级别覆盖
    [InlineData("", "#112233")]
    [InlineData("#12345", "#112233")]      // 长度不对
    public void Resolve_InvalidWidgetOverride_FallsBackToLevel(string bad, string expect)
    {
        var levels = new Dictionary<string, string> { ["error"] = expect };
        Assert.Equal(Parse(expect), PaletteResolver.Resolve(levels, bad, Severity.Error, offline: false));
    }

    [Fact]
    public void Resolve_InvalidLevelOverride_FallsBackToDefault()
    {
        var levels = new Dictionary<string, string> { ["critical"] = "zzz" };
        Assert.Equal(Parse(PaletteResolver.Defaults[Severity.Critical]),
            PaletteResolver.Resolve(levels, null, Severity.Critical, offline: false));
    }

    [Fact]
    public void Resolve_OfflineKey_IndependentOfSeverity()
    {
        var levels = new Dictionary<string, string> { ["offline"] = "#010203" };
        Assert.Equal(0xff010203u, PaletteResolver.Resolve(levels, null, Severity.Success, offline: true));
    }

    [Fact]
    public void Keys_AreCaseInsensitive_WhenDictionaryIsConfigShaped()
    {
        // config.json 反序列化为 OrdinalIgnoreCase 字典（AppearanceConfig.SeverityColors）
        var levels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["WARNING"] = "#abcdef" };
        Assert.Equal(0xffabcdefu, PaletteResolver.Resolve(levels, null, Severity.Warning, offline: false));
    }

    [Theory]
    [InlineData("#3fb950", true)]
    [InlineData("3fb950", true)]           // 无 # 也接受
    [InlineData("#80ff3fb950", false)]     // 10 位不存在
    [InlineData("#80ff3fb9", true)]        // AARRGGBB 8 位
    [InlineData("#fff", false)]            // 3 位简写不接受（配置只走 6/8）
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParseHex_AcceptsConfigFormatsOnly(string? hex, bool ok)
    {
        Assert.Equal(ok, PaletteResolver.TryParseHex(hex, out _));
    }

    private static uint Parse(string hex)
    {
        Assert.True(PaletteResolver.TryParseHex(hex, out var argb));
        return argb;
    }
}
