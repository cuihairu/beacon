using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;

namespace Beacon.Core.Services;

/// <summary>
/// Action 执行框架（B-401，RFC §13）：按 ActionType 路由 IActionExecutor；
/// RequireConfirmation 走注入的确认通道（无通道时保守拒绝）；统一超时；
/// 可选重试（Parameters retry_max_attempts/retry_backoff_seconds，默认 1 次 = 不重试；
/// 重试共享同一总超时预算，用户取消与超时不重试）；
/// 结果（成功/失败/取消）一律发 ActionExecuted 事件驱动 UI 反馈与通知。
/// </summary>
public sealed class ActionRunner
{
    private readonly IReadOnlyDictionary<string, IActionExecutor> _executors;
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly IConfigurationStore? _config;
    private readonly ISecretStore? _secrets;
    private readonly ILogger? _logger;

    /// <summary>单个 Action 的默认超时（executor 可用更短的自身超时）。</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>确认通道（App 侧接确认弹窗）。返回 false = 用户取消。</summary>
    public Func<ActionConfig, Task<bool>>? ConfirmationHandler { get; set; }

    public ActionRunner(
        IEnumerable<IActionExecutor> executors,
        IEventBus bus,
        IClock clock,
        IConfigurationStore? config = null,
        ISecretStore? secrets = null,
        ILogger? logger = null)
    {
        _executors = executors.ToDictionary(executor => executor.ActionType, StringComparer.OrdinalIgnoreCase);
        _bus = bus;
        _clock = clock;
        _config = config;
        _secrets = secrets;
        _logger = logger;
    }

    public async Task<ActionResult> ExecuteAsync(
        ActionConfig action,
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await RunCoreAsync(action, context, cancellationToken).ConfigureAwait(false);
        _bus.Publish(new ActionExecuted(result));
        return result;
    }

    private async Task<ActionResult> RunCoreAsync(
        ActionConfig action,
        ActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        context = EnrichContext(context);
        if (action.RequireConfirmation)
        {
            if (ConfirmationHandler is null)
            {
                _logger?.LogWarning("Action {ActionId} 需要确认但未配置确认通道，拒绝执行。", action.Id);
                return Cancelled(action.Id, "需要确认但无确认通道");
            }
            if (!await ConfirmationHandler(action).ConfigureAwait(false))
            {
                return Cancelled(action.Id, "已取消");
            }
        }

        if (!_executors.TryGetValue(action.Type, out var executor))
        {
            return Unsupported(action.Id, $"未知 Action 类型：{action.Type}"); // P5-01：未知动作不混作普通失败
        }

        // 重试参数（DSL actions.retry 落到 Parameters；手改配置非法值回退默认）。默认 1 次 = 不重试，
        // 与既有单次路径逐字节等价；重试共享同一总超时预算（timeoutSource 在循环外）。
        var maxAttempts = ReadRetryParam(action.Parameters, "retry_max_attempts", 1, 1, 10);
        var backoffSeconds = ReadRetryParam(action.Parameters, "retry_backoff_seconds", 5, 0, 300);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(Timeout);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                ActionResult result;
                try
                {
                    result = await executor.ExecuteAsync(action, context, timeoutSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    return TimedOut(action.Id, $"执行超时（>{Timeout.TotalSeconds:0}s）");
                }
                catch (OperationCanceledException)
                {
                    return Cancelled(action.Id, "已取消");
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Action {ActionId} 执行失败。", action.Id);
                    result = Fail(action.Id, exception.Message);
                }
                if (result.Success || cancellationToken.IsCancellationRequested || attempt >= maxAttempts)
                {
                    // 多次尝试后仍失败：标注尝试次数（单次路径不标注，消息与既有口径一致）
                    return !result.Success && maxAttempts > 1 && attempt > 1
                        ? Fail(action.Id, $"已重试 {attempt - 1} 次仍失败：{result.Message}")
                        : result;
                }
                _logger?.LogWarning(
                    "Action {ActionId} 第 {Attempt} 次执行失败：{Message}；{Backoff}s 后重试（最多 {Max} 次）。",
                    action.Id, attempt, result.Message, backoffSeconds, maxAttempts);
                if (backoffSeconds > 0)
                {
                    // timeoutSource 已链接 outer：总超时与用户取消都能打断退避（不挂起）
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), timeoutSource.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return TimedOut(action.Id, $"执行超时（>{Timeout.TotalSeconds:0}s）");
        }
        catch (OperationCanceledException)
        {
            return Cancelled(action.Id, "已取消");
        }
    }

    /// <summary>重试参数读取：可解析则钳位进范围，否则回退默认（DSL 层已 fail-loud 校验，这里兜手改配置）。</summary>
    private static int ReadRetryParam(IReadOnlyDictionary<string, string> parameters, string key, int fallback, int min, int max)
        => parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value)
            ? Math.Clamp(value, min, max)
            : fallback;

    /// <summary>远程 Action 需要 Connection/密钥：按 SourceState.ConnectionId 从配置解析注入。</summary>
    private ActionExecutionContext EnrichContext(ActionExecutionContext context)
    {
        if (context.Connection is not null || context.SourceState is null)
        {
            return context;
        }
        var connection = _config?.FindConnection(context.SourceState.ConnectionId);
        if (connection is null)
        {
            return context;
        }
        return new ActionExecutionContext
        {
            Vars = context.Vars,
            SourceState = context.SourceState,
            Connection = connection,
            ConnectionContext = _secrets is null ? null : new ConnectionContext { Secrets = _secrets },
        };
    }

    private ActionResult Cancelled(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: _clock.UtcNow)
        {
            Outcome = ActionOutcome.Cancelled,
        };

    private ActionResult TimedOut(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: _clock.UtcNow)
        {
            Outcome = ActionOutcome.TimedOut,
        };

    private ActionResult Unsupported(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: _clock.UtcNow)
        {
            Outcome = ActionOutcome.Unsupported,
        };

    private ActionResult Fail(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: _clock.UtcNow)
        {
            Outcome = ActionOutcome.Failed,
        };
}
