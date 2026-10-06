namespace Beacon.Core.Models;

public sealed record WidgetFieldDescriptor(string Key, string DisplayName, bool Required = false, string? Placeholder = null);

/// <summary>Widget 类型元数据：驱动 Settings 向导与 L0 准入（pinSupported，RFC §6.2.5）。</summary>
public sealed record WidgetTypeDescriptor
{
    public required string Type { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>变化敏感内容才可钉到桌面：CI 灯/PR 数/构建进度/Agent 状态。</summary>
    public bool PinSupported { get; init; }
    public string SuggestedTier { get; init; } = RefreshTiers.Default;
    public IReadOnlyList<WidgetFieldDescriptor> Fields { get; init; } = [];
}
