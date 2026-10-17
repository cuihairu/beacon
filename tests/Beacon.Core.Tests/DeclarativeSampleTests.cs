using System.Text.Json;
using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// P3-04 纵样例结构门（常青）：docs/samples/declarative-vertical-sample.json 必须随引擎演进
/// 保持合法——树/源校验通过、附加源闭环指向包内组件、全文无密钥语义。运行时观感与真机
/// 闭环另行装机验收，这里保证「可导入」不是一句空话：样例改坏即红。
/// </summary>
public sealed class DeclarativeSampleTests
{
    private const string SampleRelativePath = "docs/samples/declarative-vertical-sample.json";

    private static string SamplePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, SampleRelativePath)))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException($"repo root not found from {AppContext.BaseDirectory}")
            : Path.Combine(dir.FullName, SampleRelativePath);
    }

    private static (List<WidgetConfig> Widgets, string Json) Load()
    {
        var json = File.ReadAllText(SamplePath());
        using var doc = JsonDocument.Parse(json);
        var widgets = BeaconJson.Deserialize<List<WidgetConfig>>(
            doc.RootElement.GetProperty("widgets").GetRawText());
        return (widgets!, json);
    }

    [Fact]
    public void Sample_TreesAndSources_PassValidators()
    {
        var (widgets, _) = Load();
        Assert.True(widgets.Count >= 2, "sample must bundle both the CI widget and its quota source");
        foreach (var widget in widgets)
        {
            foreach (var pair in widget.Components ?? [])
            {
                Assert.True(ComponentTreeValidator.Validate(pair.Value).Ok,
                    $"{widget.Id}/{pair.Key} tree invalid");
            }
            foreach (var pair in widget.Sources ?? [])
            {
                Assert.True(ComponentSourcesValidator.Validate(pair.Value).Ok,
                    $"{widget.Id}/{pair.Key} sources invalid");
            }
        }
    }

    [Fact]
    public void Sample_AdditionalSources_CloseOverBundledWidgets()
    {
        var (widgets, _) = Load();
        var ids = widgets.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var widget in widgets)
        {
            foreach (var sources in (widget.Sources ?? []).Values)
            {
                foreach (var target in sources.Values)
                {
                    Assert.Contains(target, ids); // 附加源指向包内组件：样例自包含，导入即闭环
                }
            }
        }

        // 组合真实存在：detail 面声明了跨源绑定（两源独立更新/缺源显示的演示前提）
        var ci = Assert.Single(widgets, w => w.Id == "w-sample-ci");
        Assert.NotEmpty(ci.Sources!["detail"]);
    }

    [Fact]
    public void Sample_CarriesNoSecrets()
    {
        // RFC §13 口径：包内只有 credentialRef 引用；密钥语义词一个不许出现
        var (_, json) = Load();
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ghp_", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-", json, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer", json, StringComparison.OrdinalIgnoreCase);
    }
}
