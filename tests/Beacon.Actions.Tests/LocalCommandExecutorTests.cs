using Beacon.Core.Models;

namespace Beacon.Actions.Tests;

/// <summary>B-404 验收：启动信息构造跨平台单测；真实进程/超时用例在 Windows CI 执行。</summary>
public sealed class LocalCommandExecutorTests
{
    private static ActionConfig Action(Dictionary<string, string> parameters) => new()
    {
        Id = "cmd-1",
        Type = "local.command",
        RequireConfirmation = false,
        Parameters = parameters,
    };

    [Fact]
    public void BuildStartInfo_Powershell_WrapsCommand()
    {
        var startInfo = LocalCommandExecutor.BuildStartInfo(
            "powershell", "Get-Process", "-Name dotnet",
            Action(new Dictionary<string, string> { ["command"] = "Get-Process" }));

        Assert.Equal("powershell", startInfo.FileName);
        Assert.Equal("-NoProfile -NonInteractive -Command Get-Process -Name dotnet", startInfo.Arguments);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_Cmd_UsesSlashC()
    {
        var startInfo = LocalCommandExecutor.BuildStartInfo(
            "cmd", "echo", "hello",
            Action(new Dictionary<string, string> { ["command"] = "echo" }));

        Assert.Equal("cmd", startInfo.FileName);
        Assert.Equal("/C echo hello", startInfo.Arguments);
    }

    [Fact]
    public void BuildStartInfo_Exe_RunsDirectly()
    {
        var startInfo = LocalCommandExecutor.BuildStartInfo(
            "exe", "git", "status --short",
            Action(new Dictionary<string, string> { ["command"] = "git", ["shell"] = "exe" }));

        Assert.Equal("git", startInfo.FileName);
        Assert.Equal("status --short", startInfo.Arguments);
    }

    [Fact]
    public void BuildStartInfo_UnknownShell_Throws()
    {
        Assert.Throws<ArgumentException>(() => LocalCommandExecutor.BuildStartInfo(
            "bash", "ls", null, Action(new Dictionary<string, string> { ["command"] = "ls" })));
    }

    [Fact]
    public void BuildStartInfo_AppliesEnvAndWorkingDirectory()
    {
        var startInfo = LocalCommandExecutor.BuildStartInfo(
            "exe", "tool", null,
            Action(new Dictionary<string, string>
            {
                ["command"] = "tool",
                ["shell"] = "exe",
                ["workingDirectory"] = "/tmp/build",
                ["env.BEACON_MODE"] = "ci",
            }));

        Assert.Equal("/tmp/build", startInfo.WorkingDirectory);
        Assert.Equal("ci", startInfo.Environment["BEACON_MODE"]);
    }

    [Fact]
    public async Task MissingCommand_Fails()
    {
        var executor = new LocalCommandExecutor(logDirectory: Path.Combine(Path.GetTempPath(), "beacon-act-test"));

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["shell"] = "exe" }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("command", result.Message);
    }

    [Fact]
    public async Task RunsRealCommand_CapturesOutput_WritesLogFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // 真实进程用例在 Windows CI 执行
        }
        var logDirectory = Path.Combine(Path.GetTempPath(), "beacon-act-test-" + Guid.NewGuid().ToString("N"));
        var executor = new LocalCommandExecutor(logDirectory);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["command"] = "echo", ["args"] = "hello-beacon", ["shell"] = "cmd" }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Contains("hello-beacon", await File.ReadAllTextAsync(result.OutputPath));
    }

    [Fact]
    public async Task Timeout_KillsProcess_AndStillWritesLog()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // 真实进程用例在 Windows CI 执行
        }
        var logDirectory = Path.Combine(Path.GetTempPath(), "beacon-act-test-" + Guid.NewGuid().ToString("N"));
        var executor = new LocalCommandExecutor(logDirectory);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["command"] = "Start-Sleep",
                ["args"] = "-Seconds 30",
                ["shell"] = "powershell",
                ["timeoutSeconds"] = "1",
            }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("超时", result.Message);
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath)); // 部分输出也落盘
    }
}
