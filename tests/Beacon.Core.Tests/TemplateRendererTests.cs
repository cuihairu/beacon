using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>B-402 验收：链接模板 {owner}/{repo}/{runId} 占位渲染。</summary>
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
}
