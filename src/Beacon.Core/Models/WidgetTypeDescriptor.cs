namespace Beacon.Core.Models;

public sealed record WidgetFieldDescriptor(string Key, string DisplayName, bool Required = false, string? Placeholder = null);

/// <summary>Widget 类型元数据：驱动 Settings 向导与 L0 准入（pinSupported，RFC §6.2.5）。</summary>
public sealed record WidgetTypeDescriptor
{
    public required string Type { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>变化敏感内容才可钉到桌面：CI 灯/PR 数/构建进度/Agent 状态。</summary>
    public bool PinSupported { get; init; }
    /// <summary>
    /// 信息密度够才配独立悬浮窗（趋势图/状态灯组）；数值/额度类默认 false——
    /// 产品拍板：纯数量 32 DIP 悬浮框无信息增益，钉选只进宿主面板。
    /// </summary>
    public bool FloatingSupported { get; init; }
    public string SuggestedTier { get; init; } = RefreshTiers.Default;
    public IReadOnlyList<WidgetFieldDescriptor> Fields { get; init; } = [];
}
