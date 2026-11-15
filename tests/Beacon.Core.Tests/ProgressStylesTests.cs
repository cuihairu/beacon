using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 额度显示方式三值解析（AQUA-001，2026-11-15 用户令「整行水族箱」）：bar 默认 / pool 显式 /
/// aquarium 显式；缺省/非法一律落 bar（pool 绝不做默认路径的 2026-10-11 纪律延伸到第三值）。
/// 波纹 off 档静止门：motion 显式 off = 静止（B-707 纪律），reduced/full/缺省放行。
/// </summary>
public sealed class ProgressStylesTests
{
    [Theory]
    [InlineData(null, "bar")]
    [InlineData("", "bar")]
    [InlineData("   ", "bar")]
    [InlineData("bar", "bar")]
    [InlineData("BAR", "bar")]
    [InlineData("pool", "pool")]
    [InlineData("POOL", "pool")]
    [InlineData("aquarium", "aquarium")]
    [InlineData("AQUARIUM", "aquarium")]
    [InlineData(" aquarium ", "aquarium")]
    [InlineData("circle", "bar")] // 非法值落 bar（fail-open）
    [InlineData("tank", "bar")]
    public void Resolve_ThreeValues_UnknownFallsBackToBar(string? raw, string expected)
        => Assert.Equal(expected, ProgressStyles.Resolve(raw));

    [Fact]
    public void Resolve_Constants_MatchCanonicalValues()
    {
        Assert.Equal("bar", ProgressStyles.Bar);
        Assert.Equal("pool", ProgressStyles.Pool);
        Assert.Equal("aquarium", ProgressStyles.Aquarium);
    }

    [Theory]
    [InlineData("off", false)]
    [InlineData("OFF", false)]
    [InlineData(" off ", false)]
    [InlineData("reduced", true)]
    [InlineData("full", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("unknown", true)] // 非法动效档不在 off 门上（Mode 解析侧已落 reduced）
    public void WavesAnimate_OffGate_MatchesB707Discipline(string? motionMode, bool expected)
        => Assert.Equal(expected, ProgressStyles.WavesAnimate(motionMode));
}
