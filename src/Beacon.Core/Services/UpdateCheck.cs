using System.Text.Json;

namespace Beacon.Core.Services;

/// <summary>
/// 版本比较与「检查更新」判定的纯函数（B-805）。
/// 只做解析与比较，不碰网络；App 侧负责调 GitHub API 与 UI。
/// 口径：tag 形如 v0.1.2 / 2026.10.9（nightly 日期口径），允许 v/V 前缀、
/// 预发布号（-beta.1）与构建元数据（+sha）。比较规则遵循 SemVer 2.0.0。
/// </summary>
public static class UpdateCheck
{
    /// <summary>解析后的版本号：主/次/修订三元组 + 预发布段（无预发布 = 正式版，永远高于预发布）。</summary>
    public readonly record struct ReleaseVersion(
        int Major, int Minor, int Patch, string[] Prerelease)
    {
        /// <summary>展示口径：恢复 v 前缀与预发布号（v0.1.2 / v2026.10.9-beta.1）。</summary>
        public override string ToString()
        {
            var core = $"v{Major}.{Minor}.{Patch}";
            return Prerelease.Length == 0 ? core : $"{core}-{string.Join(".", Prerelease)}";
        }
    }

    /// <summary>检查结果：已是最新 / 有新版本 / 无法比较（本地或远程 tag 解析失败）。</summary>
    public enum Verdict
    {
        /// <summary>本地版本不低于远程最新正式发布。</summary>
        UpToDate,

        /// <summary>远程最新正式发布高于本地版本。</summary>
        UpdateAvailable,

        /// <summary>任一侧版本无法解析，不能下结论。</summary>
        Incomparable,
    }

    /// <summary>
    /// 解析版本号：剥 v/V 前缀 → 切掉 + 构建元数据 → 切出 - 预发布段 →
    /// 主干按 '.' 分段，必须 1–3 段全数字（不足补 0）。任何越轨返回 null。
    /// 例："v0.1.2"→0.1.2；"2026.10.9+abc"→2026.10.9；"1.0-beta.2"→1.0.0-beta/2。
    /// </summary>
    public static ReleaseVersion? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.Length > 1 && (value[0] is 'v' or 'V'))
        {
            value = value[1..];
        }

        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        string[] prerelease = [];
        var dash = value.IndexOf('-');
        if (dash >= 0)
        {
            var preText = value[(dash + 1)..];
            value = value[..dash];
            prerelease = preText.Split('.');
            if (prerelease.Any(string.IsNullOrWhiteSpace))
            {
                return null;
            }
        }

        var parts = value.Split('.');
        if (parts is { Length: < 1 } or { Length: > 3 } || parts.Any(p => p.Length == 0))
        {
            return null;
        }

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var n) || n < 0 || parts[i].Length != n.ToString().Length)
            {
                return null;
            }
            numbers[i] = n;
        }

        return new ReleaseVersion(numbers[0], numbers[1], numbers[2], prerelease);
    }

    /// <summary>SemVer 比较：先比主干三元组；同主干时正式版 &gt; 预发布；
    /// 预发布段逐段比——数字段按数值、字母数字段按序数、数字段 &lt; 字母数字段、前缀相等时段多者大。</summary>
    public static int Compare(ReleaseVersion left, ReleaseVersion right)
    {
        var core = (left.Major, left.Minor, left.Patch).CompareTo((right.Major, right.Minor, right.Patch));
        if (core != 0)
        {
            return core;
        }

        if (left.Prerelease.Length == 0 || right.Prerelease.Length == 0)
        {
            return right.Prerelease.Length.CompareTo(left.Prerelease.Length);
        }

        var shared = Math.Min(left.Prerelease.Length, right.Prerelease.Length);
        for (var i = 0; i < shared; i++)
        {
            var lNum = int.TryParse(left.Prerelease[i], out var ln);
            var rNum = int.TryParse(right.Prerelease[i], out var rn);
            if (lNum && rNum)
            {
                if (ln != rn)
                {
                    return ln.CompareTo(rn);
                }
            }
            else if (lNum != rNum)
            {
                return lNum ? -1 : 1; // 数字段 < 字母数字段
            }
            else
            {
                var byName = string.CompareOrdinal(left.Prerelease[i], right.Prerelease[i]);
                if (byName != 0)
                {
                    return byName;
                }
            }
        }

        return left.Prerelease.Length.CompareTo(right.Prerelease.Length);
    }

    /// <summary>判定：任一侧解析失败 → Incomparable；远程 &gt; 本地 → UpdateAvailable；否则 UpToDate。</summary>
    public static Verdict Evaluate(string? localVersion, string? remoteTag)
    {
        var local = Parse(localVersion);
        var remote = Parse(remoteTag);
        if (local is null || remote is null)
        {
            return Verdict.Incomparable;
        }

        var order = Compare(local.Value, remote.Value);
        return order < 0 ? Verdict.UpdateAvailable : Verdict.UpToDate;
    }

    /// <summary>从 GitHub releases/latest 响应 JSON 里取 tag_name；任何畸形输入（限流 body、非 JSON）返回 null。</summary>
    public static string? ExtractTagName(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("tag_name", out var tag)
                && tag.ValueKind == JsonValueKind.String
                    ? tag.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
