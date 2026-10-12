using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>版本解析与检查更新判定：v 前缀、构建元数据、预发布 SemVer 规则、限流降级体。</summary>
public sealed class UpdateCheckTests
{
    // ---------- Parse ----------

    [Theory]
    [InlineData("v0.1.2", 0, 1, 2)]
    [InlineData("V0.1.2", 0, 1, 2)]
    [InlineData("0.1.2", 0, 1, 2)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("7", 7, 0, 0)]
    [InlineData("2026.10.9", 2026, 10, 9)]
    [InlineData(" v0.1.2 ", 0, 1, 2)]
    [InlineData("0.1.2+abc123", 0, 1, 2)]
    public void Parse_CommonForms_MatchTriple(string tag, int major, int minor, int patch)
    {
        var parsed = UpdateCheck.Parse(tag);
        Assert.NotNull(parsed);
        Assert.Equal((major, minor, patch), (parsed!.Value.Major, parsed.Value.Minor, parsed.Value.Patch));
        Assert.Empty(parsed.Value.Prerelease);
    }

    [Fact]
    public void Parse_Prerelease_SplitIntoSegments()
    {
        var parsed = UpdateCheck.Parse("v0.1.2-beta.1");
        Assert.NotNull(parsed);
        Assert.Equal(new[] { "beta", "1" }, parsed!.Value.Prerelease);
        Assert.Equal("v0.1.2-beta.1", parsed.Value.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("V")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    [InlineData("0.-1.2")]
    [InlineData("1..2")]
    [InlineData("1.0-")]
    [InlineData("1.0-beta.")]
    [InlineData("1.0.x")]
    [InlineData("0.1.0 2")]
    [InlineData("01.2.3")]
    [InlineData("+1.2.3")]
    public void Parse_RejectsGarbage(string? tag)
    {
        Assert.Null(UpdateCheck.Parse(tag));
    }

    [Fact]
    public void Parse_TrimsOuterWhitespaceOnly()
    {
        // 外层空白剥掉后合法；段内符号/空白属于非法字符。
        Assert.NotNull(UpdateCheck.Parse(" 1.2.3 "));
        Assert.Null(UpdateCheck.Parse("1. 2.3"));
        Assert.Null(UpdateCheck.Parse("1 .2.3"));
    }

    // ---------- Compare ----------

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("0.1.2", "0.1.2", 0)]
    [InlineData("1.0", "1.0.0", 0)]
    [InlineData("2.0.0", "10.0.0", -1)]
    [InlineData("1.0.0-alpha", "1.0.0", -1)]   // 正式版 > 预发布
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    [InlineData("1.0.0-beta", "1.0.0-alpha.1", 1)] // 字母段 > 数字段
    [InlineData("1.0.0-alpha.2", "1.0.0-alpha.10", -1)] // 数字段按数值比
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]    // 前缀相等时段多者大
    [InlineData("1.0.0-rc.1", "1.0.0-beta.2", 1)]
    public void Compare_FollowsSemverRules(string left, string right, int expectedSign)
    {
        var l = UpdateCheck.Parse(left)!.Value;
        var r = UpdateCheck.Parse(right)!.Value;
        var order = Math.Sign(UpdateCheck.Compare(l, r));
        Assert.Equal(expectedSign, order);
    }

    // ---------- Evaluate ----------

    [Theory]
    [InlineData("0.1.2", "v0.1.2", UpdateCheck.Verdict.UpToDate)]
    [InlineData("0.1.2", "v0.1.1", UpdateCheck.Verdict.UpToDate)]
    [InlineData("0.1.1", "v0.1.2", UpdateCheck.Verdict.UpdateAvailable)]
    [InlineData("0.1.0", "v0.1.2", UpdateCheck.Verdict.UpdateAvailable)]
    [InlineData("2026.10.9+8f3a2b1", "v0.1.2", UpdateCheck.Verdict.UpToDate)] // nightly 日期口径高于 0.1.x
    [InlineData("0.1.2-beta.1", "v0.1.2", UpdateCheck.Verdict.UpdateAvailable)]
    public void Evaluate_ComparesLocalAgainstRemoteTag(
        string local, string remote, UpdateCheck.Verdict expected)
    {
        Assert.Equal(expected, UpdateCheck.Evaluate(local, remote));
    }

    [Theory]
    [InlineData(null, "v0.1.2")]
    [InlineData("0.1.2", null)]
    [InlineData("", "v0.1.2")]
    [InlineData("dev-build", "v0.1.2")]
    public void Evaluate_UnparsableSide_IsIncomparable(string? local, string? remote)
    {
        Assert.Equal(UpdateCheck.Verdict.Incomparable, UpdateCheck.Evaluate(local, remote));
    }

    // ---------- ExtractTagName ----------

    [Fact]
    public void ExtractTagName_ReleaseResponse_ReturnsTag()
    {
        const string json = """{"url":"https://api.github.com/...","tag_name":"v0.1.2","name":"v0.1.2","prerelease":false}""";
        Assert.Equal("v0.1.2", UpdateCheck.ExtractTagName(json));
    }

    [Fact]
    public void ExtractTagName_RateLimitOrGarbage_ReturnsNull()
    {
        // 本机实证的限流降级体：有 message 无 tag_name。
        const string rateLimit = """{"message":"API rate limit exceeded for 87.120.204.149.","documentation_url":"https://docs.github.com/"}""";
        Assert.Null(UpdateCheck.ExtractTagName(rateLimit));
        Assert.Null(UpdateCheck.ExtractTagName("not json {"));
        Assert.Null(UpdateCheck.ExtractTagName("[1,2,3]"));
        Assert.Null(UpdateCheck.ExtractTagName("""{"tag_name":123}"""));
        Assert.Null(UpdateCheck.ExtractTagName(null));
        Assert.Null(UpdateCheck.ExtractTagName(""));
    }
}
