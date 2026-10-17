using Xunit;

namespace Beacon.Core.Tests;

/// <summary>
/// MVP 验收行「动效不发声」常青门：全仓 C# 源不得引用任何发声 API——常驻应用铁律
/// （RFC §6.2.7「动效永不抢焦点、不发声、不产生弹窗」）。此前证据是巡检 grep 审计
/// （零命中），本测试把审计固化为随编译跑的门：新增代码引入声音面（_sound 等词法
/// 命中或真实 API 类型名）即红。词表收 .NET/WinRT 常见发声入口，宁窄勿宽——只拦
/// 确凿的发声面，不给普通命名（如 Speaker 概念类）留误报。
/// </summary>
public sealed class AudioApiAbsenceTests
{
    /// <summary>确凿发声面：Windows 音频栈的经典入口。按类型名精确词匹配（词边界），
    /// 不匹配子串——避免把注释里的普通英文词误判成 API。</summary>
    private static readonly string[] ForbiddenTokens =
    [
        "SoundPlayer",
        "SystemSounds",
        "MediaPlayer",
        "MediaElement",
        "PlaySound",
        "sndPlaySound",
        "IAudioEndpointVolume",
        "AudioClient",
        "XAudio2",
        "PlayBackgroundNoise",
    ];

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }
        return dir is null
            ? throw new InvalidOperationException($"repo root not found from {AppContext.BaseDirectory}")
            : Path.Combine(dir.FullName, "src");
    }

    public static IEnumerable<object[]> SourceFiles()
    {
        var root = SourceRoot();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            yield return [file];
        }
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void Source_CarriesNoAudioApis(string path)
    {
        var text = File.ReadAllText(path);
        foreach (var token in ForbiddenTokens)
        {
            Assert.False(ContainsWord(text, token),
                $"{Path.GetRelativePath(SourceRoot(), path)} references audio API '{token}' — resident app must stay silent (RFC §6.2.7)");
        }
    }

    /// <summary>词边界匹配：前后不得是字母/数字（"PlaySound" 命中、"PlaySoundEx" 不误放行、
    /// "PlaySoundSomething" 命中——宁可误报不可漏放，发声面新增必然起名近似）。</summary>
    private static bool ContainsWord(string text, string token)
    {
        var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var before = index == 0 ? '\0' : text[index - 1];
            var after = index + token.Length >= text.Length ? '\0' : text[index + token.Length];
            if (!char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after))
            {
                return true;
            }
            index = text.IndexOf(token, index + token.Length, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
