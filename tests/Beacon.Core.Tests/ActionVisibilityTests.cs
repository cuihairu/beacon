using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>动作显隐纯函数（B-603 + 批3-补42 DSL 动作）：widgetType 逗号分隔多值、connection_id 限定、无限定全局可见。</summary>
public sealed class ActionVisibilityTests
{
    [Fact]
    public void NoScopes_VisibleEverywhere()
    {
        Assert.True(ActionVisibility.IsVisible(null, "http.status", "c1"));
        Assert.True(ActionVisibility.IsVisible(new Dictionary<string, string>(), "http.status", "c1"));
        Assert.True(ActionVisibility.IsVisible(
            new Dictionary<string, string> { ["url"] = "https://a" }, "http.status", "c1"));
    }

    [Theory]
    [InlineData("http.status", true)]
    [InlineData("HTTP.STATUS", true)] // 大小写不敏感
    [InlineData("HTTP.QUOTA", false)]
    [InlineData("jenkins.build", false)]
    public void WidgetTypeScope_SingleValue(string widgetType, bool expected)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["widgetType"] = "http.status" };

        Assert.Equal(expected, ActionVisibility.IsVisible(parameters, widgetType, "c1"));
    }

    [Fact]
    public void WidgetTypeScope_CommaSeparated_ListMatchesAny()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["widgetType"] = " http.status , jenkins.build ",
        };

        Assert.True(ActionVisibility.IsVisible(parameters, "jenkins.build", "c1"));
        Assert.True(ActionVisibility.IsVisible(parameters, "http.status", "c1"));
        Assert.False(ActionVisibility.IsVisible(parameters, "http.quota", "c1"));
    }

    [Fact]
    public void ConnectionIdScope_RestrictsToConnection()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["connection_id"] = "packer-a" };

        Assert.True(ActionVisibility.IsVisible(parameters, "http.status", "packer-a"));
        Assert.True(ActionVisibility.IsVisible(parameters, "http.status", "PACKER-A")); // 大小写不敏感
        Assert.False(ActionVisibility.IsVisible(parameters, "http.status", "packer-b"));
    }

    [Fact]
    public void BothScopes_BothMustPass()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["widgetType"] = "http.status,http.quota",
            ["connection_id"] = "packer-a",
        };

        Assert.True(ActionVisibility.IsVisible(parameters, "http.quota", "packer-a"));
        Assert.False(ActionVisibility.IsVisible(parameters, "jenkins.build", "packer-a"));
        Assert.False(ActionVisibility.IsVisible(parameters, "http.quota", "packer-b"));
    }
}
