using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;

namespace Beacon.App.Services;

/// <summary>
/// UI 级别色解析（B-706，RFC §4.1/§6.2.8）：每次渲染实时读 config.json 的 appearance.SeverityColors，
/// 设置改色即刻生效、无需重启；优先级 Widget &gt; 级别 &gt; 默认，非法值回退默认（PaletteResolver 纯函数，Core.Tests 覆盖）。
/// </summary>
internal sealed class UiPalette
{
    private readonly JsonConfigurationStore _config;

    public UiPalette(JsonConfigurationStore config) => _config = config;

    /// <summary>解析状态灯最终色；widgetOverride 为该 Widget 的 colorOverride（可空）。</summary>
    public global::Windows.UI.Color SeverityColor(Severity severity, bool offline = false, string? widgetOverride = null)
    {
        var argb = PaletteResolver.Resolve(_config.App.Appearance.SeverityColors, widgetOverride, severity, offline);
        return new global::Windows.UI.Color
        {
            A = (byte)(argb >> 24),
            R = (byte)(argb >> 16),
            G = (byte)(argb >> 8),
            B = (byte)argb,
        };
    }
}
