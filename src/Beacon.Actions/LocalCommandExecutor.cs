using System.Diagnostics;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Actions;

/// <summary>
/// local.command（B-404）：powershell/cmd/exe 本地命令；WorkingDirectory/环境变量/超时；
/// stdout+stderr 落盘供「Open Logs」；默认经 ActionRunner 确认（RequireConfirmation 默认 true）。
/// </summary>
public sealed class LocalCommandExecutor : IActionExecutor
{
    public static readonly string DefaultType = "local.command";

    private readonly string logDirectory;

    public LocalCommandExecutor(string? logDirectory = null)
        => this.logDirectory = logDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon", "action-logs");

    public string ActionType => DefaultType;

    public async Task<ActionResult> ExecuteAsync(
        ActionConfig action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!action.Parameters.TryGetValue("command", out var command) || string.IsNullOrWhiteSpace(command))
        {
            return Fail(action.Id, "缺少参数 command。");
        }
        action.Parameters.TryGetValue("args", out var args);
        var shell = action.Parameters.GetValueOrDefault("shell", "powershell").ToLowerInvariant();
        var timeout = TimeSpan.FromSeconds(
            double.TryParse(action.Parameters.GetValueOrDefault("timeoutSeconds", "30"), out var seconds) ? seconds : 30);

        ProcessStartInfo startInfo;
        try
        {
            startInfo = BuildStartInfo(shell, command, args, action);
        }
        catch (ArgumentException exception)
        {
            return Fail(action.Id, exception.Message);
        }

        var outputPath = Path.Combine(logDirectory, $"{Sanitize(action.Id)}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                var output = await SafeRead(outputTask).ConfigureAwait(false);
                var error = await SafeRead(errorTask).ConfigureAwait(false);
                WriteLog(outputPath, output, error);
                var reason = cancellationToken.IsCancellationRequested ? "已取消" : $"执行超时（>{timeout.TotalSeconds:0}s），进程已终止";
                return new ActionResult(action.Id, Success: false, Message: reason, OutputPath: outputPath, CompletedAt: DateTimeOffset.UtcNow);
            }

            var stdout = await SafeRead(outputTask).ConfigureAwait(false);
            var stderr = await SafeRead(errorTask).ConfigureAwait(false);
            WriteLog(outputPath, stdout, stderr);
            var success = process.ExitCode == 0;
            return new ActionResult(
                action.Id,
                success,
                success ? $"退出码 0 · {Path.GetFileName(outputPath)}" : $"退出码 {process.ExitCode} · {Path.GetFileName(outputPath)}",
                outputPath,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Fail(action.Id, $"无法启动命令：{exception.Message}");
        }
    }

    /// <summary>构造启动信息（纯函数，跨平台可测）。</summary>
    internal static ProcessStartInfo BuildStartInfo(string shell, string command, string? args, ActionConfig action)
    {
        var (fileName, arguments) = shell switch
        {
            "powershell" => ("powershell", $"-NoProfile -NonInteractive -Command {command} {args}".TrimEnd()),
            "cmd" => ("cmd", $"/C {command} {args}".TrimEnd()),
            "exe" => (command, args ?? ""),
            _ => throw new ArgumentException($"未知 shell：{shell}（支持 powershell/cmd/exe）。"),
        };
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var (key, value) in action.Parameters.Where(kv => kv.Key.StartsWith("env.", StringComparison.OrdinalIgnoreCase)))
        {
            startInfo.Environment[key["env.".Length..]] = value;
        }
        if (action.Parameters.TryGetValue("workingDirectory", out var workingDirectory) && !string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }
        return startInfo;
    }

    private void WriteLog(string outputPath, string stdout, string stderr)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            File.WriteAllText(outputPath, $"[stdout]\n{stdout}\n[stderr]\n{stderr}");
        }
        catch (IOException)
        {
            // 日志写失败不影响结果
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // 进程可能已退出
        }
    }

    private static async Task<string> SafeRead(Task<string> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string Sanitize(string id)
        => new([.. id.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')]);

    private static ActionResult Fail(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);
}
