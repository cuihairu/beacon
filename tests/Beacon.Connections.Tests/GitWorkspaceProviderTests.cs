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
}
