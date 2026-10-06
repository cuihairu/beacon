using Beacon.Core.Models;

namespace Beacon.Core.Tests;

public class SeverityTests
{
    [Fact]
    public void Rank_orders_five_levels()
    {
        Assert.True(Severity.Success.Rank() < Severity.Info.Rank());
        Assert.True(Severity.Info.Rank() < Severity.Warning.Rank());
        Assert.True(Severity.Warning.Rank() < Severity.Error.Rank());
        Assert.True(Severity.Error.Rank() < Severity.Critical.Rank());
    }

    [Fact]
    public void IsAtLeast_checks_threshold()
    {
        Assert.True(Severity.Error.IsAtLeast(Severity.Warning));
        Assert.False(Severity.Warning.IsAtLeast(Severity.Error));
        Assert.True(Severity.Critical.IsAtLeast(Severity.Critical));
    }

    [Fact]
    public void Max_returns_more_severe_side()
    {
        Assert.Equal(Severity.Error, Severity.Success.Max(Severity.Error));
        Assert.Equal(Severity.Critical, Severity.Critical.Max(Severity.Error));
        Assert.Equal(Severity.Warning, Severity.Warning.Max(Severity.Warning));
    }
}
