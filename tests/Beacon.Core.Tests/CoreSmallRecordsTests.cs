using Beacon.Core.Models;

namespace Beacon.Core.Tests;

/// <summary>核心小记录面收口（Batch 5）：布局/字段描述记录构造与默认，钉住持久化形状。</summary>
public sealed class CoreSmallRecordsTests
{
    [Fact]
    public void CapsuleIslandLayout_RoundTripsRequiredProps()
    {
        var layout = new CapsuleIslandLayout
        {
            Monitor = "DISPLAY1",
            OffsetDips = new PinOffset(12, 34),
        };
        Assert.Equal("DISPLAY1", layout.Monitor);
        Assert.Equal(new PinOffset(12, 34), layout.OffsetDips);
    }

    [Fact]
    public void QuickPanelLayout_RoundTripsAllProps()
    {
        var layout = new QuickPanelLayout
        {
            Monitor = "DISPLAY1",
            Anchor = PinAnchor.TopRight,
            OffsetDips = new PinOffset(24, 96),
            Width = 720,
            Height = 560,
        };
        Assert.Equal(PinAnchor.TopRight, layout.Anchor);
        Assert.Equal(720, layout.Width, 6);
        Assert.Equal(560, layout.Height, 6);
    }

    [Fact]
    public void WidgetFieldDescriptor_Defaults_AreOptionalFreeText()
    {
        var field = new WidgetFieldDescriptor("key", "名称");
        Assert.Equal("key", field.Key);
        Assert.Equal("名称", field.DisplayName);
        Assert.False(field.Required);
        Assert.Null(field.Placeholder);
        Assert.Null(field.Choices);
    }

    [Fact]
    public void WidgetFieldDescriptor_WithChoices_CarriesList()
    {
        var field = new WidgetFieldDescriptor("icon", "图标", Placeholder: "选择", Choices: ["one", "two"]);
        Assert.Equal(["one", "two"], field.Choices);
    }
}
