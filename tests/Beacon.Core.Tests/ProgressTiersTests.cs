using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>进度档位色单一来源（tile 进度条与组件库共用）：≥98% Error、≥80% Warning、否则正常色。</summary>
public sealed class ProgressTiersTests
{
    [Theory]
    [InlineData(0.98)]
    [InlineData(0.985)]
    [InlineData(1.0)]
    public void Level_AtOrAboveFull_ReturnsError(double fraction)
        => Assert.Equal(Severity.Error, ProgressTiers.Level(fraction));

    [Theory]
    [InlineData(0.8)]
    [InlineData(0.9)]
    [InlineData(0.979)]
    public void Level_AtOrAboveNearFull_ReturnsWarning(double fraction)
        => Assert.Equal(Severity.Warning, ProgressTiers.Level(fraction));

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.79)]
    [InlineData(0.799)]
    public void Level_BelowNearFull_ReturnsNull(double fraction)
        => Assert.Null(ProgressTiers.Level(fraction));
}
