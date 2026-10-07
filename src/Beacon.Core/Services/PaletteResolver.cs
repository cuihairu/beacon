using Beacon.Core.Models;

namespace Beacon.Core.Services;

/// <summary>
/// 级别色解析（B-706，RFC §4.1/§6.2.8）：Widget 覆盖 &gt; 级别全局覆盖 &gt; 默认色表；
/// 非法/缺省键一律回退默认，绝不因配置写坏而崩溃。纯函数，Core.Tests 覆盖。
/// </summary>
public static class PaletteResolver
{
    public const string OfflineKey = "offline";
    public const string DefaultOfflineHex = "#8b949e";

    /// <summary>默认色表（§4.1 五级，GitHub 语义色系）。</summary>
    public static readonly IReadOnlyDictionary<Severity, string> Defaults = new Dictionary<Severity, string>
    {
        [Severity.Success] = "#3fb950",
        [Severity.Info] = "#58a6ff",
        [Severity.Warning] = "#d29922",
        [Severity.Error] = "#f85149",
        [Severity.Critical] = "#ff3b30",
    };

    /// <summary>解析最终色（ARGB）。offline=true 用 offline 键（§4.1 派生态）。</summary>
    public static uint Resolve(IReadOnlyDictionary<string, string>? levelOverrides, string? widgetOverride, Severity severity, bool offline)
    {
        if (TryParseHex(widgetOverride, out var argb))
        {
            return argb;
        }
        var key = offline ? OfflineKey : KeyOf(severity);
        if (levelOverrides is not null
            && levelOverrides.TryGetValue(key, out var hex)
            && TryParseHex(hex, out argb))
        {
            return argb;
        }
        TryParseHex(offline ? DefaultOfflineHex : Defaults[severity], out argb);
        return argb;
    }

    /// <summary>接受 #RRGGBB / #AARRGGBB（可有可无 #），其余判非法。</summary>
    public static bool TryParseHex(string? hex, out uint argb)
    {
        argb = 0;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }
        var body = hex.Trim().TrimStart('#');
        if (body.Length is not (6 or 8) || !IsHex(body))
        {
            return false;
        }
        var value = uint.Parse(body, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        argb = body.Length == 8 ? value : 0xff000000u | value;
        return true;
    }

    public static string KeyOf(Severity severity) => severity switch
    {
        Severity.Success => "success",
        Severity.Info => "info",
        Severity.Warning => "warning",
        Severity.Error => "error",
        Severity.Critical => "critical",
        _ => "info",
    };

    private static bool IsHex(string text)
    {
        foreach (var c in text)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }
        return true;
    }
}
