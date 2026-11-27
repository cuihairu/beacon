using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 岛收起态聚合事实（BEACON-ISLAND-AA-1 B2/B3）：无数据份额归 0（旧 1.0 兜底把零数据画成
/// 整圆/满条=造假）、NoData 标记与离线横幅「（重试中）」文案裁决。纯函数沉 Core 同口径可本地测。
/// </summary>
public sealed class IslandAggregateFactsTests
{
    [Fact]
    public void OkShare_NoTotal_ReturnsZeroNotFakeFull()
    {
        // AA-1 B2：total==0 时旧实现兜底 1.0 → 环画整圆/条画满=无数据显示 100% 健康（造假）；
        // 现在归 0，空轨道才是真值
        Assert.Equal(0.0, IslandAggregateFacts.OkShare(0, 0, 0));
    }

    [Theory]
    [InlineData(3, 0, 0, 1.0)] // 全 ok
    [InlineData(2, 1, 1, 0.5)] // 混合：ok 占比
    [InlineData(0, 2, 2, 0.0)] // 全异常
    public void OkShare_WithTotal_ReturnsOkRatio(int ok, int warn, int error, double expected)
        => Assert.Equal(expected, IslandAggregateFacts.OkShare(ok, warn, error));

    [Fact]
    public void NoData_MatchesTotalZeroOnly()
    {
        Assert.True(IslandAggregateFacts.NoData(0, 0, 0));
        Assert.False(IslandAggregateFacts.NoData(1, 0, 0));
        Assert.False(IslandAggregateFacts.NoData(0, 0, 1));
    }

    [Fact]
    public void OfflineBannerText_FreshFailure_AppendsRetrying()
    {
        // AA-1 B3：断连 fresh（5 分钟内）明示重试反馈
        Assert.Equal("Last update 09:00（重试中）",
            IslandAggregateFacts.OfflineBannerText("Last update 09:00", failedRecently: true));
    }

    [Fact]
    public void OfflineBannerText_StaleOutage_KeepsSilentTimestamp()
    {
        // AA-1 B3：久断退回静默旧值，不再无解释常驻
        Assert.Equal("Last update 09:00",
            IslandAggregateFacts.OfflineBannerText("Last update 09:00", failedRecently: false));
    }
}
