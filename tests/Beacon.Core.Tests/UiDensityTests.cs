using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// Phase 5 行 5（主题、密度与可访问性完善）密度刻度：紧凑/常规/宽松三档的解析与间距缩放
/// （设计令牌 redesign-plan §6.1「尺寸：紧凑、标准、宽松三种密度」）。
/// 常规 = 恒等；紧凑 = 0.75、宽松 = 1.25（设计未定值，取与紧凑对称）并取整（AwayFromZero）。
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
    [InlineData("spacious")]
    [InlineData("SPACIOUS")]
    [InlineData("COZY")]
    public void Parse_KnownTiers_OthersFallBackToRegular(string? raw)
        => Assert.Equal(raw?.Trim().ToLowerInvariant() switch
            {
                "compact" => UiDensityKind.Compact,
                "spacious" => UiDensityKind.Spacious,
                _ => UiDensityKind.Regular,
            },
            UiDensity.Parse(raw));

    [Fact]
    public void Parse_UnknownValue_IsRegularNotThrow()
        => Assert.Equal(UiDensityKind.Regular, UiDensity.Parse("dense"));

    [Theory]
    [InlineData(UiDensityKind.Regular, 1.0)]
    [InlineData(UiDensityKind.Compact, 0.75)]
    [InlineData(UiDensityKind.Spacious, 1.25)]
    public void ScaleOf_ThreeTiers(UiDensityKind kind, double expected)
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
    [InlineData(2, UiDensityKind.Spacious, 3)]   // 2.5 进位（AwayFromZero，不 banker）
    [InlineData(4, UiDensityKind.Spacious, 5)]
    [InlineData(6, UiDensityKind.Spacious, 8)]   // 7.5 进位
    [InlineData(8, UiDensityKind.Spacious, 10)]
    [InlineData(10, UiDensityKind.Spacious, 13)] // 12.5 进位（banker 会给 12）
    [InlineData(16, UiDensityKind.Spacious, 20)]
    public void Spacing_ScalesAndRounds(double space, UiDensityKind kind, double expected)
        => Assert.Equal(expected, UiDensity.Spacing(space, kind));

    [Fact]
    public void Spacing_Regular_IsIdentity()
        => Assert.Equal(7, UiDensity.Spacing(7, UiDensityKind.Regular));
}
