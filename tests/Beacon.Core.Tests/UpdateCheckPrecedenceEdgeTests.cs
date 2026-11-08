using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// UpdateCheck 预发布段逐段比较收口（Batch 5）：数字段 vs 字母数字段两个方向的序数判定
/// （既有理论只覆盖「字母 > 数字」单向，此处补反向与等核长短段）。
/// </summary>
public sealed class UpdateCheckPrecedenceEdgeTests
{
    private static int SignOf(string left, string right)
        => Math.Sign(UpdateCheck.Compare(UpdateCheck.Parse(left)!.Value, UpdateCheck.Parse(right)!.Value));

    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-1", 1)]   // 字母段 > 数字段（反向：1 在前）
    [InlineData("1.0.0-1", "1.0.0-alpha", -1)]  // 数字段 < 字母数字段
    [InlineData("1.0.0-rc", "1.0.0-rc.0", -1)]  // 前缀相等，段多者大
    [InlineData("1.0.0-rc.0", "1.0.0-rc", 1)]   // 段多者大（反向）
    public void Compare_MixedNumericAlnum_SignFollowsSemver(string left, string right, int expectedSign)
        => Assert.Equal(expectedSign, SignOf(left, right));
}
