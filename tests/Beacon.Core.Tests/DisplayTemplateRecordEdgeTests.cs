using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 展示模板记录面收口（Batch 5）：Describe 词表与回原词、TemplateParameter 默认值、
/// TemplateCompatibility Yes/No、模板注册表基本盘面（含 RequiresProgress 两向）。
/// </summary>
public sealed class DisplayTemplateRecordEdgeTests
{
    [Fact]
    public void Describe_KnownKind_IsChinese_Unknown_Passthrough()
    {
        Assert.Equal("百分比", DisplayTemplateKinds.Describe(DisplayTemplateKinds.Progress));
        Assert.Equal("ring", DisplayTemplateKinds.Describe("ring"));
        Assert.Equal("size", DisplayTemplateKinds.Describe("size"));
    }

    [Fact]
    public void TemplateParameter_Defaults_AreZeroToOne()
    {
        var p = new TemplateParameter("k", "label", "text", "default");
        Assert.Equal("k", p.Key);
        Assert.Equal(0, p.Min, 6);
        Assert.Equal(1, p.Max, 6);
    }

    [Fact]
    public void Compatibility_YesAndNo_AreStable()
    {
        Assert.True(TemplateCompatibility.Yes.Ok);
        Assert.Null(TemplateCompatibility.Yes.Reason);
        var no = TemplateCompatibility.No("needs progress");
        Assert.False(no.Ok);
        Assert.Equal("needs progress", no.Reason);
    }

    [Fact]
    public void Registry_ContainsTemplates_BothProgressFlavors()
    {
        Assert.NotEmpty(DisplayTemplates.All);
        Assert.Contains(DisplayTemplates.All, t => t.RequiresProgress);
        Assert.Contains(DisplayTemplates.All, t => !t.RequiresProgress);
    }
}
