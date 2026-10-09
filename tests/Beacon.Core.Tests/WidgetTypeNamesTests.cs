using Beacon.Core.Services;

namespace Beacon.Core.Tests;

/// <summary>
/// 组件类型短名表（tile 标签/L2 chip 共用）：全前缀映射 + 未知前缀退类型尾段。
/// 背景：PinTile 标签旧兜底取类型尾段，ark.usage tile 显示成「usage」——字段名冒充显示名。
/// </summary>
public sealed class WidgetTypeNamesTests
{
    [Theory]
    [InlineData("ark.usage", "火山方舟")]
    [InlineData("bigmodel.usage", "智谱")]
    [InlineData("mimo.usage", "小米 MiMo")]
    [InlineData("qwen.usage", "阿里千问")]
    [InlineData("deepseek.balance", "DeepSeek")]
    [InlineData("kimi.coding", "Kimi")]
    [InlineData("github.pull_requests", "GitHub")]
    [InlineData("github.actions.runs", "GitHub")]
    [InlineData("http.quota", "HTTP")]
    [InlineData("claude.usage", "Claude")]
    [InlineData("codex.usage", "Codex")]
    [InlineData("copilot.usage", "Copilot")]
    [InlineData("opencode.usage", "OpenCode")]
    public void Of_KnownPrefixes_MapToConnectionTypeShortName(string widgetType, string expected)
        => Assert.Equal(expected, WidgetTypeNames.Of(widgetType));

    [Theory]
    [InlineData("unknown.type", "type")] // 未知前缀退尾段（命名缺失不阻断渲染）
    [InlineData("unknown", "unknown")]
    [InlineData("ARK.USAGE", "火山方舟")] // 前缀大小写不敏感
    public void Of_UnknownOrCased_FallsBackSafely(string widgetType, string expected)
        => Assert.Equal(expected, WidgetTypeNames.Of(widgetType));
}
