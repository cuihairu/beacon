namespace Beacon.Core.Services;

/// <summary>
/// 组件类型 → 短显示名（tile 标签/L2 分组 chip 共用，纯函数 Linux 可单测）。
/// 修「悬浮框名字显示 usage」：PinTile 标签兜底此前取类型尾段（ark.usage → "usage"），
/// 字段名冒充了显示名；现按类型前缀映射为连接类型短名（与设置侧栏 ModuleDef 同源口径），
/// 未知前缀仍退类型尾段（不抛，命名缺失不阻断渲染）。
/// </summary>
public static class WidgetTypeNames
{
    public static string Of(string widgetType)
    {
        var prefix = widgetType.Split('.')[0].ToLowerInvariant();
        return prefix switch
        {
            "github" => "GitHub",
            "http" => "HTTP",
            "bigmodel" => "智谱",
            "ark" => "火山方舟",
            "claude" => "Claude",
            "deepseek" => "DeepSeek",
            "kimi" => "Kimi",
            "moonshot" => "Moonshot",
            "mimo" => "小米 MiMo",
            "qwen" => "阿里千问",
            "codex" => "Codex",
            "copilot" => "Copilot",
            "opencode" => "OpenCode",
            _ => widgetType.Split('.')[^1], // 未知类型：退类型尾段（旧行为兜底）
        };
    }
}
