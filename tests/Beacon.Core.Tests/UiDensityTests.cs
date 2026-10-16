using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 5 行 5（主题、密度与可访问性完善）密度刻度：紧凑/常规两档的解析与间距缩放。
/// 常规 = 恒等；紧凑 = 0.75 并取整（2→2、4→3、6→5（4.5 进位）、8→6、10→8、16→12）。
/// 配置字段是字符串：缺省/未知值回退常规（fail-open——密度是增强不是正确性）。
/// </summary>
public sealed class UiDensityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("regular")]
    [InlineData("REGULAR")]
    [InlineData("compact")]
    [InlineData("COZY")]
    public void Parse_KnownCompact_OthersFallBackToRegular(string? raw)
        => Assert.Equal(raw is not null && raw.Trim().ToLowerInvariant() == "compact"
                ? UiDensityKind.Compact
                : UiDensityKind.Regular,
            UiDensity.Parse(raw));

    [Fact]
    public void Parse_UnknownValue_IsRegularNotThrow()
        => Assert.Equal(UiDensityKind.Regular, UiDensity.Parse("dense"));

    [Theory]
    [InlineData(UiDensityKind.Regular, 1.0)]
    [InlineData(UiDensityKind.Compact, 0.75)]
    public void ScaleOf_TwoTiers(UiDensityKind kind, double expected)
        => Assert.Equal(expected, UiDensity.ScaleOf(kind));

    [Theory]
    [InlineData(2, UiDensityKind.Regular, 2)]
    [InlineData(2, UiDensityKind.Compact, 2)]    // 1.5 进位
    [InlineData(4, UiDensityKind.Compact, 3)]
    [InlineData(6, UiDensityKind.Compact, 5)]    // 4.5 进位（AwayFromZero，不 banker）
    [InlineData(8, UiDensityKind.Compact, 6)]
    [InlineData(10, UiDensityKind.Compact, 8)]
    [InlineData(12, UiDensityKind.Compact, 9)]
    [InlineData(16, UiDensityKind.Compact, 12)]
    [InlineData(18, UiDensityKind.Compact, 14)]  // 13.5 进位
    public void Spacing_ScalesAndRounds(double space, UiDensityKind kind, double expected)
        => Assert.Equal(expected, UiDensity.Spacing(space, kind));

    [Fact]
    public void Spacing_Regular_IsIdentity()
        => Assert.Equal(7, UiDensity.Spacing(7, UiDensityKind.Regular));
}
