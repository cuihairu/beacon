using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// machine.git（本地 Git 工作区，2026-10-11 用户拍板「git 都搞」）：
/// porcelain 解析（分支行三态/计数口径/重命名预览）、rev-list behind\tahead 解析、
/// Provider 映射（↑↓ 只在有上游时上板、behind&gt;0 亮黄、stash&gt;0 才显段、缺 repo_path fail-loud）、
/// 真仓库集成测试（git init/commit/stash 护 CLI 链路）。
/// </summary>
public sealed class GitWorkspaceProviderTests
{
    // —— porcelain 解析（纯函数） ——

    [Fact]
    public void ParseStatus_BranchLineVariants()
    {
        var (branch, upstream, hasUpstream, _, _, _, _) = GitCliSource.ParseStatus("## main...origin/main [ahead 1, behind 2]\n");
        Assert.Equal("main", branch);
        Assert.Equal("origin/main", upstream);
        Assert.True(hasUpstream);

        (branch, upstream, hasUpstream, _, _, _, _) = GitCliSource.ParseStatus("## feature/x\n");
        Assert.Equal("feature/x", branch);
        Assert.Null(upstream);
        Assert.False(hasUpstream);

        (branch, upstream, hasUpstream, _, _, _, _) = GitCliSource.ParseStatus("## No commits yet on main\n");
        Assert.Equal("main", branch);
        Assert.False(hasUpstream);

        (branch, upstream, hasUpstream, _, _, _, _) = GitCliSource.ParseStatus("## HEAD (no branch)\n");
        Assert.Equal("HEAD", branch);
        Assert.False(hasUpstream);
    }

    [Fact]
    public void ParseStatus_CountsDirtyStagedUntracked()
    {
        var output = string.Join("\n",
            "## main...origin/main",
            "M  staged-only.cs",      // X=M：staged
            "MM staged-and-mod.cs",   // X=M：staged（Y 也算，dirty 一条）
            " M worktree-only.cs",    // X=空格：非 staged
            "?? new-file.cs",         // untracked
            "?? new-file2.cs",
            "");
        var (_, _, _, dirty, staged, untracked, _) = GitCliSource.ParseStatus(output);
        Assert.Equal(5, dirty);
        Assert.Equal(2, staged);
        Assert.Equal(2, untracked);
    }

    [Fact]
    public void ParseStatus_PreviewTakesFirstFiveAndNewSideOfRename()
    {
        var lines = new List<string> { "## dev", "R  old-name.cs -> new-name.cs" };
        for (var i = 1; i <= 7; i++)
        {
            lines.Add($" M file{i}.cs");
        }
        var (_, _, _, _, _, _, preview) = GitCliSource.ParseStatus(string.Join("\n", lines));
        Assert.NotNull(preview);
        Assert.StartsWith("new-name.cs", preview); // rename 条目在首位，取 -> 右侧新路径
        Assert.Equal(5, preview.Split('、').Length); // 只留前 5 条，32 DIP 与 L3 都不搞分页
    }

    [Fact]
    public void ParseStatus_EmptyOutput_NoBranch()
    {
        var (branch, _, hasUpstream, dirty, _, _, preview) = GitCliSource.ParseStatus("");
        Assert.Null(branch);
        Assert.False(hasUpstream);
        Assert.Equal(0, dirty);
        Assert.Null(preview);
    }

    [Fact]
    public void ParseAheadBehind_TabSeparated()
    {
        Assert.Equal((5, 2), GitCliSource.ParseAheadBehind("5\t2\n"));   // 左=上游独有=behind
        Assert.Equal((0, 0), GitCliSource.ParseAheadBehind("0\t0"));
        Assert.Equal((0, 0), GitCliSource.ParseAheadBehind("garbage")); // 解析不动按 0/0
    }

    // —— Provider 映射（假采样源） ——

    private sealed class FakeGitSource(GitWorkspaceSnapshot snapshot) : IGitSource
    {
        public GitWorkspaceSnapshot Read(string repoPath) => snapshot;
    }

    private static GitWorkspaceSnapshot Snapshot(
        string branch = "main", string? upstream = "origin/main", bool hasUpstream = true,
        int ahead = 0, int behind = 0, int dirty = 0, int staged = 0, int untracked = 0,
        int stash = 0, string? preview = null)
        => new(branch, upstream, hasUpstream, ahead, behind, dirty, staged, untracked, stash, preview);

    private static WidgetConfig Widget(string label = "", string repoPath = @"C:\code\beacon")
    {
        var config = new Dictionary<string, string>();
        if (label.Length > 0)
        {
            config["label"] = label;
        }
        config["repo_path"] = repoPath;
        return new WidgetConfig { Id = "w1", Type = GitWorkspaceDescriptors.Type, ConnectionId = "machine-local", Config = config };
    }

    private static ConnectionConfig Connection() => new() { Id = "machine-local", Type = "machine" };

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    [Fact]
    public async Task UpstreamSegment_OnlyWhenPresent()
    {
        var provider = new GitWorkspaceProvider(new FakeGitSource(Snapshot(ahead: 2, behind: 0, dirty: 7)));
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("main · ↑2↓0 · 改 7", state!.Summary);
        Assert.Equal(Severity.Info, state.Severity); // 只有待推（ahead）不算告警

        var noUpstream = await new GitWorkspaceProvider(new FakeGitSource(Snapshot(hasUpstream: false, upstream: null, dirty: 3)))
            .GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("main · 改 3", noUpstream!.Summary); // 无 upstream 不上板 ↑↓
    }

    [Fact]
    public async Task BehindOrDiverged_Warning()
    {
        var behind = await new GitWorkspaceProvider(new FakeGitSource(Snapshot(behind: 3, dirty: 0)))
            .GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Warning, behind!.Severity); // 远端有新提交 → 提醒拉取

        var diverged = await new GitWorkspaceProvider(new FakeGitSource(Snapshot(ahead: 2, behind: 1)))
            .GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Warning, diverged!.Severity); // 分叉 → 提醒 rebase/merge
        Assert.Equal(LifecycleState.Running, diverged.Lifecycle);
    }

    [Fact]
    public async Task StashSegment_OnlyWhenNonZero()
    {
        var with = await new GitWorkspaceProvider(new FakeGitSource(Snapshot(stash: 1, dirty: 0)))
            .GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("main · ↑0↓0 · 改 0 · stash 1", with!.Summary);
        Assert.Equal("↑0↓0 · 改 0 · stash 1", with.Payload["value_text"]); // 数值位 = Summary 计数段

        var without = await new GitWorkspaceProvider(new FakeGitSource(Snapshot(stash: 0, dirty: 0)))
            .GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("main · ↑0↓0 · 改 0", without!.Summary);
        Assert.Equal("↑0↓0 · 改 0", without.Payload["value_text"]);
    }

    [Fact]
    public async Task LabelOverridesHead_PayloadCarriesNumbers()
    {
        var provider = new GitWorkspaceProvider(new FakeGitSource(Snapshot(ahead: 1, behind: 0, dirty: 4, staged: 1, untracked: 2, stash: 2, preview: "a.cs、b.cs")));
        var state = await provider.GetStateAsync(Widget(label: "beacon"), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("beacon · ↑1↓0 · 改 4 · stash 2", state!.Summary);
        Assert.Equal("↑1↓0 · 改 4 · stash 2", state.Payload["value_text"]);
        Assert.Equal("1", state.Payload["ahead"]);
        Assert.Equal("0", state.Payload["behind"]);
        Assert.Equal("4", state.Payload["dirty"]);
        Assert.Equal("1", state.Payload["staged"]);
        Assert.Equal("2", state.Payload["untracked"]);
        Assert.Equal("2", state.Payload["stash"]);
        Assert.Equal("a.cs、b.cs", state.Payload["files"]);
        Assert.Equal("main", state.Payload["branch"]);
    }

    [Fact]
    public async Task MissingRepoPath_FailLoud()
    {
        var config = new WidgetConfig { Id = "w1", Type = GitWorkspaceDescriptors.Type, ConnectionId = "machine-local" };
        var provider = new GitWorkspaceProvider(new FakeGitSource(Snapshot()));
        await Assert.ThrowsAsync<ConnectionException>(() =>
            provider.GetStateAsync(config, Connection(), Ctx(), CancellationToken.None));
    }

    // —— 真仓库集成（护 CLI 链路：参数引号/退出码/porcelain 实协议；Linux 与 windows-latest 均有 git） ——

    [Fact]
    public async Task GitCliSource_RealRepo_EndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), $"beacon-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var source = new GitCliSource();
            var git = (string args) =>
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = $"-C \"{root}\" {args}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10_000);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"git {args} 失败");
                }
                return output;
            };

            git("init -b main");
            git("-c user.name=test -c user.email=test@example.com commit --allow-empty -m init");
            var snapshot = source.Read(root);
            Assert.Equal("main", snapshot.Branch);
            Assert.False(snapshot.HasUpstream); // 无远端：不上板 ↑↓
            Assert.Equal(0, snapshot.Dirty);
            Assert.Equal(0, snapshot.Stash);

            File.WriteAllText(Path.Combine(root, "untracked.txt"), "x");
            snapshot = source.Read(root);
            Assert.Equal(1, snapshot.Dirty);
            Assert.Equal(1, snapshot.Untracked);
            Assert.NotNull(snapshot.DirtyFilesPreview);
            Assert.Contains("untracked.txt", snapshot.DirtyFilesPreview);

            git("stash push --include-untracked");
            snapshot = source.Read(root);
            Assert.Equal(1, snapshot.Stash);
            Assert.Equal(0, snapshot.Dirty);

            // 空目录非仓库 → fail-loud（建在仓库外：建在仓库内 git 会向上找到父仓库，测不出）
            var plain = Path.Combine(Path.GetTempPath(), $"beacon-plain-{Guid.NewGuid():N}");
            Directory.CreateDirectory(plain);
            await Assert.ThrowsAsync<ConnectionException>(() =>
                new GitWorkspaceProvider(source).GetStateAsync(
                    Widget(repoPath: plain), Connection(), Ctx(), CancellationToken.None));

            // 路径不存在 → fail-loud
            await Assert.ThrowsAsync<ConnectionException>(() =>
                new GitWorkspaceProvider(source).GetStateAsync(
                    Widget(repoPath: Path.Combine(root, "no-such-dir")), Connection(), Ctx(), CancellationToken.None));

            // 状态映射走真快照：干净仓库 Info（上一步 stash push 后 dirty=0、stash=1 都在卡上）
            var state = await new GitWorkspaceProvider(source).GetStateAsync(
                Widget(repoPath: root, label: "it"), Connection(), Ctx(), CancellationToken.None);
            Assert.Equal(Severity.Info, state!.Severity);
            Assert.Equal("it · 改 0 · stash 1", state.Summary);
        }
        finally
        {
            // Windows 上 .git 对象文件只读，递归删除会 UnauthorizedAccessException——清理尽力而为
            try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void Descriptor_Git_CarriesFieldsAndTier()
    {
        var descriptor = GitWorkspaceDescriptors.Git;
        Assert.Equal("machine.git", descriptor.Type);
        Assert.True(descriptor.PinSupported);
        Assert.True(descriptor.FloatingOptIn);
        Assert.Equal(RefreshTiers.Default, descriptor.SuggestedTier);
        Assert.Contains(descriptor.Fields, field => field.Key == "repo_path");
    }

    // —— CLI 错误/边界分支（真实子进程：有上游差量 / git 缺失 / 超时） ——

    [Fact]
    public async Task GitCliSource_RealRepo_WithUpstream_CountsAheadBehind()
    {
        // 有上游才走 rev-list 差量分支：push -u 设 upstream → status 出 `## main...origin/main`，
        // 本地再提交 → ahead 1；第二克隆推一提交 → 工作区 fetch 后分叉 ↑1↓1 → 亮黄
        var root = Path.Combine(Path.GetTempPath(), $"beacon-git-up-{Guid.NewGuid():N}");
        var bare = Path.Combine(Path.GetTempPath(), $"beacon-git-up-origin-{Guid.NewGuid():N}");
        var clone = Path.Combine(Path.GetTempPath(), $"beacon-git-up-clone-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var source = new GitCliSource();
            static void RunGit(string repo, string args)
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = $"-C \"{repo}\" {args}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit(30_000);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"git {args} 失败");
                }
            }

            const string Identity = "-c user.name=test -c user.email=test@example.com";
            RunGit(root, "init -b main");
            RunGit(root, $"{Identity} commit --allow-empty -m init");
            RunGit(root, $"init --bare -b main \"{bare}\"");
            RunGit(root, $"remote add origin \"{bare}\"");
            RunGit(root, "push -u origin main");
            RunGit(root, $"{Identity} commit --allow-empty -m second"); // 本地领先 → ahead 1

            var snapshot = source.Read(root);
            Assert.True(snapshot.HasUpstream); // 有上游才进入 rev-list 分支
            Assert.Equal("main", snapshot.Branch);
            Assert.Equal("origin/main", snapshot.Upstream);
            Assert.Equal(1, snapshot.Ahead);
            Assert.Equal(0, snapshot.Behind);
            var aheadState = await new GitWorkspaceProvider(source).GetStateAsync(
                Widget(repoPath: root), Connection(), Ctx(), CancellationToken.None);
            Assert.Equal("main · ↑1↓0 · 改 0", aheadState!.Summary);

            RunGit(root, $"clone \"{bare}\" \"{clone}\"");
            RunGit(clone, $"{Identity} commit --allow-empty -m third");
            RunGit(clone, "push origin main");
            RunGit(root, "fetch origin"); // 上游再进一提交 → 工作区分叉

            snapshot = source.Read(root);
            Assert.Equal(1, snapshot.Ahead);
            Assert.Equal(1, snapshot.Behind);
            var diverged = await new GitWorkspaceProvider(source).GetStateAsync(
                Widget(repoPath: root), Connection(), Ctx(), CancellationToken.None);
            Assert.Equal(Severity.Warning, diverged!.Severity); // 分叉（前后都有）→ 提醒拉取/合并
            Assert.Equal("main · ↑1↓1 · 改 0", diverged.Summary);
        }
        finally
        {
            foreach (var dir in new[] { root, bare, clone })
            {
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    [Fact]
    public void GitCliSource_GitMissing_FailLoudDegraded()
    {
        // git 不在 PATH → Process.Start 抛 Win32Exception → fail-loud（Windows 与 Linux 同一路径）
        var repo = Path.Combine(Path.GetTempPath(), $"beacon-nogit-repo-{Guid.NewGuid():N}");
        var emptyBin = Path.Combine(Path.GetTempPath(), $"beacon-nogit-bin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(emptyBin);
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", emptyBin);
            var exception = Assert.Throws<ConnectionException>(() => { new GitCliSource().Read(repo); });
            Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
            Assert.Contains("找不到 git 命令", exception.Message);
            Assert.IsType<System.ComponentModel.Win32Exception>(exception.InnerException); // 底层是进程启动失败
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void GitCliSource_Timeout_KillsAndFailsDegraded()
    {
        if (!OperatingSystem.IsLinux())
        {
            return; // 假 git 用 shebang + 关 fd 的 shell 脚本，仅 Linux 语义（windows-latest 无）
        }
        var repo = Path.Combine(Path.GetTempPath(), $"beacon-timeout-repo-{Guid.NewGuid():N}");
        var bin = Path.Combine(Path.GetTempPath(), $"beacon-timeout-bin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(bin);
        var fakeGit = Path.Combine(bin, "git");
        File.WriteAllText(fakeGit, "#!/bin/sh\nexec 1>&- 2>&-\nexec sleep 30\n"); // 关输出后挂起 >10s → 命中超时
        File.SetUnixFileMode(fakeGit, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            // 假 git 前插 PATH（优先命中），保留原 PATH 供脚本里的 sleep 解析
            Environment.SetEnvironmentVariable("PATH", bin + Path.PathSeparator + originalPath);
            var exception = Assert.Throws<ConnectionException>(() => { new GitCliSource().Read(repo); });
            Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
            Assert.Contains("超时", exception.Message);
            Assert.Contains("status", exception.Message); // 超时信息携带首个 git 子命令名
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
}
