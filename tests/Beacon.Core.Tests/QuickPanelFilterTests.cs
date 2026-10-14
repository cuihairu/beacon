using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>2026-10-14 用户单①：看板来源分组键与点击过滤判定（chip 标签与过滤同源）。</summary>
public class QuickPanelFilterTests
{
    [Theory]
    [InlineData("github.actions.runs", "CI")] // CI 运行单独成 chip（与 PR 计数区分）
    [InlineData("github.pull_requests", "GitHub")]
    [InlineData("bigmodel.usage", "智谱")]
    [InlineData("machine.cpu", "本机")]
    [InlineData("ark.usage", "火山方舟")]
    public void SourceKey_maps_by_type_prefix(string widgetType, string expected)
    {
        Assert.Equal(expected, QuickPanelFilter.SourceKey(widgetType));
    }

    [Fact]
    public void SourceKey_unknown_type_falls_back_to_tail()
    {
        Assert.Equal("quota", QuickPanelFilter.SourceKey("vendor.custom.quota"));
    }

    [Fact]
    public void Null_source_matches_all()
    {
        Assert.True(QuickPanelFilter.Matches(null, "github.actions.runs"));
        Assert.True(QuickPanelFilter.Matches(null, "bigmodel.usage"));
    }

    [Fact]
    public void Active_source_filters_other_types_out()
    {
        Assert.True(QuickPanelFilter.Matches("CI", "github.actions.runs"));
        Assert.False(QuickPanelFilter.Matches("CI", "github.pull_requests"));
        Assert.False(QuickPanelFilter.Matches("CI", "bigmodel.usage"));
    }

    [Fact]
    void SourceKey_matches_chip_label_source()
    {
        // 同一映射服务 chip 标签与过滤判定：拿 "CI" 过滤时，actions.runs 必命中、pull_requests 必排除
        Assert.Equal(
            QuickPanelFilter.Matches("CI", "github.actions.runs"),
            QuickPanelFilter.SourceKey("github.actions.runs") == "CI");
    }
}
