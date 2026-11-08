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

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string> { ["command"] = "beacon-unused", ["timeoutSeconds"] = "bad" }),
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

        var result = await executor.ExecuteAsync(
            Action(new Dictionary<string, string>
            {
                ["command"] = IsWindows ? "powershell" : "/bin/sh",
                ["args"] = IsWindows
                    ? "-NoProfile -NonInteractive -Command \"Write-Output beacon-out; Write-Error beacon-error\""
                    : "-c \"printf beacon-out; printf beacon-error >&2\"",
                ["shell"] = IsWindows ? "powershell" : "exe",
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
                ["shell"] = IsWindows ? "powershell" : "exe",
            }),
            new ActionExecutionContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("退出码 7", result.Message);
    }
}
