using Beacon.Core.Models;

namespace Beacon.Actions.Tests;

public sealed class LocalCommandExecutorCoverageTests
{
    private static readonly bool IsWindows = OperatingSystem.IsWindows();

    private static ActionConfig Action(Dictionary<string, string> parameters) => new()
    {
        Id = "local/coverage.test",
        Type = "local.command",
        RequireConfirmation = false,
        Parameters = parameters,
    };

    [Fact]
    public async Task InvalidTimeout_FailsWithStartupMessage()
    {
        var executor = new LocalCommandExecutor(Path.Combine(Path.GetTempPath(), "beacon-local-logs"));

        // shell=exe 直启不存在的可执行：Windows/Linux 两侧 process.Start 均抛（文件找不到）→
        // 「无法启动命令」。旧配置 shell=powershell 在 Linux 上因 powershell 缺失才走到启动失败，
        // Windows 上 powershell 存在 → beacon-unused 运行时失败给「退出码」而非启动失败（CI 实证）。
        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["command"] = "beacon-unused-absolutely-missing",
                ["timeoutSeconds"] = "bad",
                ["shell"] = "exe",
            }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("无法启动命令", result.Message);
    }

    [Fact]
    public async Task RunsPowerShell_CapturesStreamsAndWritesSanitizedLogFile()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "beacon-local-coverage-" + Guid.NewGuid().ToString("N"));
        var executor = new LocalCommandExecutor(logDirectory);

        // shell=exe 直启可执行（规范调用）：Windows=powershell、Linux=/bin/sh，均不走 -Command 双包裹
        //（旧 shell=powershell + command=powershell 在 Windows 引号解析下双包裹失败，CI 实证）。
        // Windows 端补 exit 0：powershell.exe -Command 对未捕获 error record（Write-Error）恒回退出码 1
        //（$?=False → exit 1，CI 42539712896 实证），显式 exit 压回 0 才断言成功；stderr 照样带出记录文本。
        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["command"] = IsWindows ? "powershell" : "/bin/sh",
                ["args"] = IsWindows
                    ? "-NoProfile -NonInteractive -Command \"Write-Output beacon-out; Write-Error beacon-error; exit 0\""
                    : "-c \"printf beacon-out; printf beacon-error >&2\"",
                ["shell"] = "exe",
            }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.EndsWith(".log", result.OutputPath);
        var log = await File.ReadAllTextAsync(result.OutputPath!);
        Assert.Contains("beacon-out", log);
        Assert.Contains("beacon-error", log);
    }

    [Fact]
    public async Task Timeout_KillsLongRunningPowerShell()
    {
        var executor = new LocalCommandExecutor(Path.Combine(Path.GetTempPath(), "beacon-local-timeout-" + Guid.NewGuid().ToString("N")));

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["command"] = IsWindows ? "powershell" : "/bin/sh",
                ["args"] = IsWindows ? "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30" : "-c \"sleep 30\"",
                ["shell"] = IsWindows ? "powershell" : "exe",
                ["timeoutSeconds"] = "1",
            }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ActionOutcome.TimedOut, result.Outcome);
        Assert.Contains("超时", result.Message);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task ExternalCancellation_KillsProcessAndMarksCancelled()
    {
        var executor = new LocalCommandExecutor(Path.Combine(Path.GetTempPath(), "beacon-local-cancel-" + Guid.NewGuid().ToString("N")));
        using var cancellation = new CancellationTokenSource(500);

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["command"] = IsWindows ? "powershell" : "/bin/sh",
                ["args"] = IsWindows ? "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30" : "-c \"sleep 30\"",
                ["shell"] = IsWindows ? "powershell" : "exe",
                ["timeoutSeconds"] = "30",
            }),
            new ActionExecutionContext(),
            cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal(ActionOutcome.Cancelled, result.Outcome);
        Assert.Contains("已取消", result.Message);
    }

    [Fact]
    public async Task NonZeroExit_FailsWithExitCode()
    {
        var executor = new LocalCommandExecutor(Path.Combine(Path.GetTempPath(), "beacon-local-exit-" + Guid.NewGuid().ToString("N")));

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["command"] = IsWindows ? "powershell" : "/bin/sh",
                ["args"] = IsWindows ? "-NoProfile -NonInteractive -Command \"exit 7\"" : "-c \"exit 7\"",
                ["shell"] = "exe",
            }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("退出码 7", result.Message);
    }
}
