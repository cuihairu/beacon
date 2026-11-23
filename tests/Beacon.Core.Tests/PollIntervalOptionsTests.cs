using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 全局检查频率档（SET-UX-01，2026-11-23 用户令原型落地批1）：预设五档 + 预设外合法旧值
/// 「自定义」回显。行为锁：
/// ① 预设档固定五档（0=按组件档位 / 15 / 30 / 60 / 300）——下拉标签与 App 构造一致；
/// ② 非预设正整数判自定义（120=旧配置真实卡过用户的值）；
/// ③ 自定义标签 60 进位分秒（120→「2 分钟」、90→「1 分 30 秒」、45→「45 秒」）；
/// ④ 非正值炸响（0 由预设承载，不走自定义路径）。
/// </summary>
public sealed class PollIntervalOptionsTests
{
    // —— 预设档：五档固定（0=按组件档位在册，既有下拉口径不变） ——

    [Fact]
    public void Presets_ContainsFiveTiers_WithZeroDefault()
    {
        Assert.Equal(
            [(0, "按组件档位（默认）"), (15, "15 秒"), (30, "30 秒"), (60, "1 分钟"), (300, "5 分钟")],
            PollIntervalOptions.Presets.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(300)]
    public void IsPreset_KnownTiers_True(int seconds)
        => Assert.True(PollIntervalOptions.IsPreset(seconds));

    [Theory]
    [InlineData(120)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(3600)]
    public void IsPreset_LegalNonPreset_False(int seconds)
        => Assert.False(PollIntervalOptions.IsPreset(seconds));

    // —— 自定义标签：分秒 60 进位 ——

    [Theory]
    [InlineData(120, "自定义 · 2 分钟")]
    [InlineData(3600, "自定义 · 60 分钟")]
    [InlineData(45, "自定义 · 45 秒")]
    [InlineData(90, "自定义 · 1 分 30 秒")]
    [InlineData(75, "自定义 · 1 分 15 秒")]
    public void CustomLabel_FormatsMinutesAndSeconds(int seconds, string expected)
        => Assert.Equal(expected, PollIntervalOptions.CustomLabel(seconds));

    [Fact]
    public void CustomLabel_NonPositive_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PollIntervalOptions.CustomLabel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PollIntervalOptions.CustomLabel(-30));
    }
}
