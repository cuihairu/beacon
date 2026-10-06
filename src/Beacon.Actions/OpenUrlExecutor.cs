using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Beacon.Core.Services;
using System.Diagnostics;

namespace Beacon.Actions;

/// <summary>
/// open.url（B-402）：系统默认浏览器打开链接。Parameters.url 支持模板占位符
/// （Vars + SourceState.Payload 合并取值，如 {owner}/{repo}/{runId}）。
/// opener 可注入（测试记录目标 URL）。
/// </summary>
public sealed class OpenUrlExecutor : IActionExecutor
{
    public static readonly string DefaultType = "open.url";

    private readonly Func<string, Task>? opener;

    public OpenUrlExecutor(Func<string, Task>? opener = null) => this.opener = opener;

    public string ActionType => DefaultType;

    public async Task<ActionResult> ExecuteAsync(
        ActionConfig action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!action.Parameters.TryGetValue("url", out var template) || string.IsNullOrWhiteSpace(template))
        {
            return Fail(action.Id, "缺少参数 url。");
        }

        string target;
        try
        {
            target = TemplateRenderer.Render(template, ActionVars.Merge(context));
        }
        catch (ArgumentException exception)
        {
            return Fail(action.Id, exception.Message);
        }

        if (opener is not null)
        {
            await opener(target).ConfigureAwait(false);
        }
        else
        {
            using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        return new ActionResult(action.Id, Success: true, Message: target, OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);
    }

    private static ActionResult Fail(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: DateTimeOffset.UtcNow);
}
