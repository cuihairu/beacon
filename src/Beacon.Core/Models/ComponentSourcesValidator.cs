namespace Beacon.Core.Models;

/// <summary>数据源聚合校验结果：Ok=false 时 Errors 给出可读诊断（调用面整面弃用 + 进诊断日志）。</summary>
public sealed record ComponentSourcesValidation(bool Ok, IReadOnlyList<string> Errors);

/// <summary>
/// 数据源聚合防呆（Phase 3 行 4）：面级声明「源名 → widgetId」。源名必须非空且不得使用保留键
/// ""（实例自身状态，ComponentBindingSource.OwnStateKey）；widgetId 非空；每面源数带上限。
/// 常驻进程铁律同组件树：声明坏不炸进程、不静默裁剪——非法整面弃用（附加源全丢，自身状态
/// 恒可用），诊断进加载日志。
/// </summary>
public static class ComponentSourcesValidator
{
    public const int MaxSourcesPerSurface = 16;

    /// <summary>无附加源声明 = 无错（实例只用自身状态）。</summary>
    public static ComponentSourcesValidation None { get; } = new(true, []);

    public static ComponentSourcesValidation Validate(IReadOnlyDictionary<string, string>? surface)
    {
        if (surface is null)
        {
            return None;
        }
        var errors = new List<string>();
        if (surface.Count > MaxSourcesPerSurface)
        {
            errors.Add($"source count {surface.Count} exceeds max {MaxSourcesPerSurface}");
        }
        foreach (var pair in surface)
        {
            if (pair.Key == ComponentBindingSource.OwnStateKey || string.IsNullOrWhiteSpace(pair.Key))
            {
                errors.Add($"source name '{pair.Key}' is empty or reserved");
                continue;
            }
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                errors.Add($"source '{pair.Key}': widget id is empty");
            }
        }
        return errors.Count == 0 ? None : new(false, [.. errors]);
    }
}
