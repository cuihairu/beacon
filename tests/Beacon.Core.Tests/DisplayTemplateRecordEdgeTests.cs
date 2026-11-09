using Beacon.Core.Models;
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

    // ---- 收口：记录面合成成员（行 29 TemplateParameter Default / 行 61-62 DisplayTemplate Name/Desc）----

    [Fact]
    public void TemplateParameter_RecordSurface_EqualityWalksThroughDefault()
    {
        // 合成 Equals 逐字段比：前 3 字段相等才走到 Default（行 29），不同 → 判不等
        var p1 = new TemplateParameter("k", "label", "slider", "d1", 40, 320);
        var p2 = new TemplateParameter("k", "label", "slider", "d2", 40, 320);

        Assert.NotEqual(p1, p2);
        Assert.False(p1 == p2);
        Assert.True(p1 != p2);
        Assert.False(p1.Equals(null));
        Assert.Equal(p1, p1 with { });                       // with 克隆值相等（合成克隆 + Equals）
        Assert.Equal(p1.GetHashCode(), (p1 with { }).GetHashCode()); // 合成 GetHashCode：值相等 → 哈希一致

        var (key, label, kind, defaultValue, min, max) = p1; // 合成 Deconstruct
        Assert.Equal(("k", "label", "slider", "d1", 40d, 320d), (key, label, kind, defaultValue, min, max));
        Assert.Contains("d1", p1.ToString());                 // PrintMembers
    }

    [Fact]
    public void DisplayTemplate_RecordSurface_EqualityWalksNameAndDesc()
    {
        // 合成 Equals 逐字段比：Id 相等 → Name（行 61）→ Desc（行 62），不同即判不等
        ComponentDescriptor Leaf() => new() { Type = "text" };
        var emptyParams = new Dictionary<string, TemplateParameter>();
        var t1 = new DisplayTemplates.DisplayTemplate("x", "名称甲", "描述一", DisplayTemplateKinds.None, emptyParams, 32, _ => Leaf());
        var t2 = new DisplayTemplates.DisplayTemplate("x", "名称乙", "描述一", DisplayTemplateKinds.None, emptyParams, 32, _ => Leaf());
        var t3 = new DisplayTemplates.DisplayTemplate("x", "名称甲", "描述二", DisplayTemplateKinds.None, emptyParams, 32, _ => Leaf());

        Assert.NotEqual(t1, t2); // Id 相等 → 走到 Name 比较才判不等（行 61）
        Assert.NotEqual(t1, t3); // Id/Name 相等 → 走到 Desc 比较才判不等（行 62）
        Assert.Equal(t1, t1 with { });
        Assert.Equal(t1.GetHashCode(), (t1 with { }).GetHashCode());

        var (id, name, desc, requires, parameters, minHeight, builder) = t1; // 合成 Deconstruct
        Assert.Equal(("x", "名称甲", "描述一"), (id, name, desc));
        Assert.Same(DisplayTemplateKinds.None, requires);
        Assert.Same(emptyParams, parameters);
        Assert.Equal(32d, minHeight);
        Assert.NotNull(builder);
        Assert.Equal("text", builder(null)!.Type); // Builder 透传可用
        Assert.Contains("名称甲", t1.ToString());   // PrintMembers
    }
}
