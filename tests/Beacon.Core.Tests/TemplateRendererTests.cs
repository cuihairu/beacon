using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-402 验收：链接模板 {owner}/{repo}/{runId} 占位渲染 + 字符集/单趟展开/字面花括号/缺键清单口径。</summary>
public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_ReplacesAllPlaceholders()
    {
        var vars = new Dictionary<string, string> { ["owner"] = "cuihairu", ["repo"] = "beacon", ["runId"] = "987" };

        var rendered = TemplateRenderer.Render("https://github.com/{owner}/{repo}/actions/runs/{runId}", vars);

        Assert.Equal("https://github.com/cuihairu/beacon/actions/runs/987", rendered);
    }

    [Fact]
    public void Render_IsCaseInsensitiveOnKeys()
    {
        var vars = new Dictionary<string, string> { ["Repo"] = "beacon" };

        Assert.Equal("r/beacon", TemplateRenderer.Render("r/{repo}", vars));
    }

    [Fact]
    public void Render_KeepsTextWithoutPlaceholders()
    {
        Assert.Equal("plain/path", TemplateRenderer.Render("plain/path", new Dictionary<string, string>()));
    }

    [Fact]
    public void Render_MissingVariable_ThrowsListingKeys()
    {
        var vars = new Dictionary<string, string> { ["owner"] = "o" };

        var exception = Assert.Throws<ArgumentException>(
            () => TemplateRenderer.Render("{owner}/{repo}/{runId}", vars));

        Assert.Contains("repo", exception.Message);
        Assert.Contains("runId", exception.Message);
    }

    [Theory]
    [InlineData("https://x/{owner}/a/{owner}", "owner", "o", "https://x/o/a/o")] // 同键重复占位全替换
    [InlineData("s/{run.id}/e", "run.id", "7", "s/7/e")] // 键字符集含点（A-Za-z0-9_.-）
    [InlineData("s/{run-id}/e", "run-id", "7", "s/7/e")] // 键字符集含连字符
    [InlineData("s/{owner}/e", "OWNER", "o", "s/o/e")] // 模板大小写不敏感
    public void Render_TableDriven(string template, string key, string value, string expected)
        => Assert.Equal(expected, TemplateRenderer.Render(template, new Dictionary<string, string> { [key] = value }));

    [Fact]
    public void Render_ValueContainingBraces_NotReExpanded()
    {
        // 替换值不再二次展开（单趟正则）：防注入递归，也保住带 { 的原样输出
        var vars = new Dictionary<string, string> { ["v"] = "{owner}" };

        Assert.Equal("{owner}", TemplateRenderer.Render("{v}", vars));
    }

    [Fact]
    public void Render_NonKeyBraces_KeptLiterally()
    {
        // 键字符集外的花括号（空体/含引号空格）不匹配占位符、原样保留且不抛——JSON body 模板的实况
        var vars = new Dictionary<string, string> { ["owner"] = "o" };

        Assert.Equal("""{"ref":"main","owner":"o"}""", TemplateRenderer.Render("""{"ref":"main","owner":"{owner}"}""", vars));
        Assert.Equal("{} {a b}", TemplateRenderer.Render("{} {a b}", vars));
    }

    [Fact]
    public void Render_DuplicateMissingKeys_ListedOnce()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TemplateRenderer.Render("{repo} and {repo} and {owner}", new Dictionary<string, string>()));

        var ownerIndex = exception.Message.IndexOf("owner", StringComparison.Ordinal);
        var repoIndex = exception.Message.IndexOf("repo", StringComparison.Ordinal);
        Assert.True(ownerIndex >= 0 && repoIndex >= 0);
        Assert.True(exception.Message.IndexOf("repo", repoIndex + 1, StringComparison.Ordinal) < 0); // repo 只列一次
    }
}
