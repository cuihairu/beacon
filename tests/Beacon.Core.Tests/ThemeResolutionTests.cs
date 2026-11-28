using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>BEACON-THEME-001：主题三态解析纯函数 + 实时单源（IsDark 即时翻转、同值不重发）。</summary>
public sealed class ThemeResolutionTests
{
    [Theory]
    [InlineData("light", false, false)]
    [InlineData("light", true, false)] // light 绝对覆盖：系统再暗也是浅色
    [InlineData("dark", false, true)]
    [InlineData("dark", true, true)]
    [InlineData("system", false, false)]
    [InlineData("system", true, true)] // 跟随系统：取系统口径
    [InlineData("", true, true)] // 空/未知/缺失 = 跟随系统（App 构造器同口径）
    [InlineData("bogus", true, true)]
    [InlineData("bogus", false, false)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    public void ThreeStateResolution_FollowsConfigOrSystem(string? theme, bool systemIsDark, bool expected)
    {
        Assert.Equal(expected, ThemeResolution.IsDark(theme, systemIsDark));
    }

    [Theory]
    [InlineData(" DARK ", true)]
    [InlineData("Dark", true)]
    [InlineData(" Light ", false)]
    [InlineData("light", false)] // App 启动统一 trim+lower 归一，解析层容错同口径
    public void InputIsNormalized_TrimAndLower(string theme, bool expected)
    {
        Assert.Equal(expected, ThemeResolution.IsDark(theme, systemIsDark: false));
    }

    [Fact]
    public void Initialize_SeedsWithoutBroadcasting()
    {
        ThemeRuntime.ResetForTests();
        var raised = 0;
        void Record() => raised++;
        ThemeRuntime.Changed += Record;
        try
        {
            ThemeRuntime.Initialize("dark", systemIsDark: false);
            Assert.True(ThemeRuntime.IsDark);
            Assert.Equal(0, raised); // 启动播种不广播（无窗在场，广播无意义）
        }
        finally
        {
            ThemeRuntime.Changed -= Record; // local function 才能真正退订（lambda 不等引用）
            ThemeRuntime.ResetForTests();
        }
    }

    [Fact]
    public void Apply_FlipRaisesOnce_SameValueSilent()
    {
        ThemeRuntime.ResetForTests();
        var raised = 0;
        void Record() => raised++;
        ThemeRuntime.Changed += Record;
        try
        {
            ThemeRuntime.Apply("dark", systemIsDark: false); // false→true 翻转
            Assert.True(ThemeRuntime.IsDark);
            Assert.Equal(1, raised);

            ThemeRuntime.Apply("dark", systemIsDark: false); // 同值幂等：不翻转不广播
            ThemeRuntime.Apply("DARK  ", systemIsDark: false); // 归一后同值亦静默
            Assert.Equal(1, raised);

            ThemeRuntime.Apply("light", systemIsDark: true); // 绝对覆盖再翻回（true→false）
            Assert.False(ThemeRuntime.IsDark);
            ThemeRuntime.Apply("light", systemIsDark: false); // 绝对值不受系统值影响：仍 false，静默
            Assert.Equal(2, raised);
            ThemeRuntime.Apply("system", systemIsDark: true); // 切跟随系统且系统=暗：false→true 翻转广播
            Assert.True(ThemeRuntime.IsDark);
            Assert.Equal(3, raised);
        }
        finally
        {
            ThemeRuntime.Changed -= Record;
            ThemeRuntime.ResetForTests();
        }
    }
}
