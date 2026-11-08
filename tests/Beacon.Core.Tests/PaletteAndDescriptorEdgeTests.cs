using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// PaletteResolver 与 ComponentTreeValidator 边界收口（Batch 5）：未识别 severity 回默认键、
/// 非十六进制覆盖值回退默认色表、空 state 路径判定不支持、非法绑定组合逐条报错。
/// </summary>
public sealed class PaletteAndDescriptorEdgeTests
{
    // ---------- PaletteResolver ----------

    [Fact]
    public void Palette_KeyOf_UnknownSeverity_FallsBackToInfo()
    {
        Assert.Equal("info", PaletteResolver.KeyOf((Severity)999));
        Assert.Equal("critical", PaletteResolver.KeyOf(Severity.Critical));
    }

    [Fact]
    public void Palette_TryParseHex_RejectsNonHex()
    {
        Assert.False(PaletteResolver.TryParseHex("#xyz123", out _));
        Assert.False(PaletteResolver.TryParseHex("#12345", out _));
        Assert.False(PaletteResolver.TryParseHex("", out _));
        Assert.True(PaletteResolver.TryParseHex("#3fb950", out var argb));
        Assert.Equal(0xff3fb950u, argb);
        Assert.True(PaletteResolver.TryParseHex("ff3fb950", out var argb8));
        Assert.Equal(0xff3fb950u, argb8); // 8 位 = 带 A 直取
    }

    [Fact]
    public void Palette_Resolve_NonHexOverride_FallsBackToLevelOverrides()
    {
        var argb = PaletteResolver.Resolve(
            new Dictionary<string, string> { ["warning"] = "#d29922" },
            widgetOverride: "not-a-color",
            Severity.Warning,
            offline: false);
        Assert.Equal(0xffd29922u, argb);
    }

    [Fact]
    public void Palette_Resolve_InvalidLevelHex_FallsBackToDefault()
    {
        var argb = PaletteResolver.Resolve(
            new Dictionary<string, string> { ["warning"] = "zzz" },
            widgetOverride: null,
            Severity.Warning,
            offline: false);
        Assert.Equal(0xffd29922u, argb);
    }

    // ---------- ComponentTreeValidator ----------

    [Fact]
    public void Validate_EmptyStatePath_ReportsUnsupported()
    {
        var tree = new ComponentDescriptor
        {
            Id = "root",
            Type = "card",
            Bindings =
            [
                new ComponentBinding { Field = "title", Source = new ComponentBindingSource { Kind = "state", Ref = "" } },
            ],
        };

        var result = ComponentTreeValidator.Validate(tree);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, error => error.Contains("unsupported state path"));
    }

    [Fact]
    public void Validate_SelectorKind_AcceptedAtValidatorLayer()
    {
        // 选择器白名单在 Phase 4 解析期（SelectorNames）执行；组件树校验只认 kind 词表。
        var tree = new ComponentDescriptor
        {
            Id = "root",
            Type = "card",
            Bindings =
            [
                new ComponentBinding { Field = "text", Source = new ComponentBindingSource { Kind = "selector", Ref = "percent" } },
            ],
        };

        var result = ComponentTreeValidator.Validate(tree);

        Assert.True(result.Ok);
    }
}
