using System.Diagnostics;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>machine.git 组件类型元数据（本地 Git 工作区，零凭据零网络）：驱动 Settings 向导与 L0 准入。</summary>
public static class GitWorkspaceDescriptors
{
    public const string Type = "machine.git";

    public static WidgetTypeDescriptor Git { get; } = new()
    {
        Type = Type,
        DisplayName = "Git 仓库状态（↑↓ · 改 · stash）",
        PinSupported = true,
        FloatingOptIn = true, // 数值类：设置「数量悬浮窗」开关放行（2026-10-11 用户令默认开）
        SuggestedTier = RefreshTiers.Default, // 每刷 3 次 git 子进程，60s 档够用（15s 档纯浪费）
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "beacon"),
            new WidgetFieldDescriptor("repo_path", "仓库路径", Required: true, Placeholder: "C:\\code\\beacon"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Git];
}

/// <summary>Git 工作区快照：一次 Read 全量产出（分支/上下游/差量/变更/stash），Provider 只做映射不做 IO。</summary>
public readonly record struct GitWorkspaceSnapshot(
    string? Branch,
    string? Upstream,
    bool HasUpstream,
    int Ahead,
    int Behind,
    int Dirty,
    int Staged,
    int Untracked,
    int Stash,
    string? DirtyFilesPreview);

/// <summary>采样抽象：生产走 git CLI 子进程，测试注入假快照（纯逻辑可测）+ 真仓库集成测试护 CLI 链路。</summary>
public interface IGitSource
{
    GitWorkspaceSnapshot Read(string repoPath);
}

/// <summary>
/// 真实采样：git CLI 三连（status --porcelain -b / rev-list --left-right --count / stash list）。
/// 输出走 porcelain 文本协议（路径非 ASCII 由 git octal 转义，ASCII 安全，无编码坑）；
/// 无上游分支时跳过 rev-list（打了必报错）。未配置 git 或目录缺失按 Degraded fail-loud。
/// </summary>
public sealed class GitCliSource : IGitSource
{
    public GitWorkspaceSnapshot Read(string repoPath)
    {
        if (!Directory.Exists(repoPath))
        {
            throw new ConnectionException($"仓库路径不存在：{repoPath}", ConnectionHealthState.Degraded);
        }

        var status = Git("status --porcelain=v1 -b", repoPath);
        var (branch, upstream, hasUpstream, dirty, staged, untracked, preview) = ParseStatus(status);

        var ahead = 0;
        var behind = 0;
        if (hasUpstream)
        {
            (behind, ahead) = ParseAheadBehind(Git("rev-list --left-right --count @{u}...HEAD", repoPath));
        }

        var stash = Git("stash list", repoPath);
        var stashCount = 0;
        foreach (var line in SplitLines(stash))
        {
            if (line.Length > 0)
            {
                stashCount++;
            }
        }

        return new GitWorkspaceSnapshot(branch, upstream, hasUpstream, ahead, behind, dirty, staged, untracked, stashCount, preview);
    }

    /// <summary>
    /// 解析 `git status --porcelain=v1 -b`：首行 `## &lt;branch&gt;...&lt;upstream&gt; [ahead N, behind M]`，
    /// 其余每行一个变更条目（XY path）。分支行三态：常规（带/不带 upstream）、`## No commits yet on main`（未出生）、
    /// `## HEAD (no branch)`（detached）。dirty=全部条目；staged=X 位非空格非 ?；untracked=`??`。
    /// </summary>
    internal static (string? Branch, string? Upstream, bool HasUpstream, int Dirty, int Staged, int Untracked, string? Preview)
        ParseStatus(string output)
    {
        string? branch = null;
        string? upstream = null;
        var hasUpstream = false;
        var dirty = 0;
        var staged = 0;
        var untracked = 0;
        List<string>? previewFiles = null;

        var first = true;
        foreach (var line in SplitLines(output))
        {
            if (line.Length == 0)
            {
                continue;
            }
            if (first)
            {
                first = false;
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    (branch, upstream, hasUpstream) = ParseBranchLine(line[3..]);
                }
                continue;
            }

            dirty++;
            var x = line[0];
            var marker = line.Length < 3 ? line : line[..2];
            if (marker == "??")
            {
                untracked++;
            }
            else if (x is not (' ' or '?'))
            {
                staged++;
            }
            previewFiles ??= [];
            if (previewFiles.Count < 5)
            {
                previewFiles.Add(PreviewPath(line));
            }
        }

        var preview = previewFiles is { Count: > 0 } ? string.Join("、", previewFiles) : null;
        return (branch, upstream, hasUpstream, dirty, staged, untracked, preview);
    }

    private static (string? Branch, string? Upstream, bool HasUpstream) ParseBranchLine(string text)
    {
        if (text.StartsWith("No commits yet on ", StringComparison.Ordinal))
        {
            return (text["No commits yet on ".Length..], null, false);
        }
        if (text.StartsWith("HEAD (no branch)", StringComparison.Ordinal))
        {
            return ("HEAD", null, false); // detached：无 upstream，不上板 ↑↓
        }
        var dots = text.IndexOf("...", StringComparison.Ordinal);
        if (dots < 0)
        {
            return (text, null, false);
        }
        var upstreamText = text[(dots + 3)..];
        var bracket = upstreamText.IndexOf('[', StringComparison.Ordinal); // 尾巴 "[ahead 1, behind 2]" 不用——差量走 rev-list
        if (bracket >= 0)
        {
            upstreamText = upstreamText[..bracket].Trim();
        }
        return (text[..dots], upstreamText.Length > 0 ? upstreamText : null, upstreamText.Length > 0);
    }

    /// <summary>解析 `rev-list --left-right --count @{u}...HEAD`：`behind\tahead`（左=上游独有=behind）。解析不动按 0/0（不因输出抖动甩错）。</summary>
    internal static (int Behind, int Ahead) ParseAheadBehind(string output)
    {
        var parts = output.Trim().Split('\t');
        if (parts.Length == 2
            && int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var behind)
            && int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var ahead))
        {
            return (behind, ahead);
        }
        return (0, 0);
    }

    private static string PreviewPath(string entry)
    {
        var path = entry.Length >= 3 ? entry[3..] : entry;
        var arrow = path.IndexOf(" -> ", StringComparison.Ordinal); // 重命名 "R  old -> new" 取新路径
        if (arrow >= 0)
        {
            path = path[(arrow + 4)..];
        }
        return path;
    }

    private static IEnumerable<string> SplitLines(string text)
        => text.Replace("\r\n", "\n").Split('\n');

    private static string Git(string arguments, string repoPath)
    {
        var safePath = repoPath.Replace("\"", ""); // Windows 路径本就容不下引号；Unix 防注入式写坏参数
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{safePath}\" {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new ConnectionException($"找不到 git 命令（需安装 Git 并加入 PATH）：{exception.Message}", ConnectionHealthState.Degraded, exception);
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10_000))
        {
            try { process.Kill(); } catch (InvalidOperationException) { } // 已退出即无事
            throw new ConnectionException($"git {arguments.Split(' ')[0]} 超时（>10s）", ConnectionHealthState.Degraded);
        }
        if (process.ExitCode != 0)
        {
            var reason = FirstLine(error) ?? $"exit {process.ExitCode}";
            throw new ConnectionException($"git {arguments.Split(' ')[0]} 失败：{reason}", ConnectionHealthState.Degraded);
        }
        return output;
    }

    private static string? FirstLine(string text)
    {
        var line = text.Replace("\r\n", "\n").Split('\n')[0].Trim();
        return line.Length > 0 ? line : null;
    }
}

/// <summary>
/// machine.git（本地 Git 工作区，2026-10-11 用户拍板「git 都搞」）：每仓库一张卡——
/// `beacon ↑2↓5 · 改 7 · stash 1`。↑=ahead（待推）↓=behind（远端有新提交，Warning 提醒拉取）；
/// behind&gt;0 或分叉 → Warning（Signal 不是 Dashboard：需要动手才亮黄）。变更文件列表只进 L3
/// 详情（payload 前 5 条预览，32 DIP 装不下也不做分页——用户拍板「展示多少页」按一卡四数收口）。
/// 零凭据零网络：git CLI 只读本机仓库。连接复用 machine（本机即本机）。
/// </summary>
public sealed class GitWorkspaceProvider : IWidgetProvider
{
    private readonly IGitSource _source;

    public GitWorkspaceProvider() : this(new GitCliSource()) { }

    public GitWorkspaceProvider(IGitSource source) => _source = source;

    public WidgetTypeDescriptor Descriptor => GitWorkspaceDescriptors.Git;

    public Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var repoPath = widget.Config.GetValueOrDefault("repo_path");
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            throw new ConnectionException("组件缺少 repo_path（本地仓库路径）。", ConnectionHealthState.Degraded);
        }

        var snapshot = _source.Read(repoPath);
        var severity = snapshot.Behind > 0 ? Severity.Warning : Severity.Info; // 分叉（前后都有）同样落在 behind>0 分支

        var head = widget.Config.GetValueOrDefault("label") is { Length: > 0 } label ? label : snapshot.Branch ?? "Git";
        var segments = new List<string> { head };
        if (snapshot.HasUpstream)
        {
            segments.Add($"↑{snapshot.Ahead}↓{snapshot.Behind}");
        }
        segments.Add($"改 {snapshot.Dirty}");
        if (snapshot.Stash > 0)
        {
            segments.Add($"stash {snapshot.Stash}");
        }

        return Task.FromResult<WidgetState?>(new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Warning ? LifecycleState.Running : LifecycleState.Success,
            Progress = null,
            Summary = string.Join(" · ", segments),
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["branch"] = snapshot.Branch ?? "",
                ["upstream"] = snapshot.Upstream ?? "",
                ["ahead"] = snapshot.Ahead.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["behind"] = snapshot.Behind.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["dirty"] = snapshot.Dirty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["staged"] = snapshot.Staged.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["untracked"] = snapshot.Untracked.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["stash"] = snapshot.Stash.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["files"] = snapshot.DirtyFilesPreview ?? "",
            },
            FetchedAt = DateTimeOffset.UtcNow,
        });
    }
}
