using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;

namespace Beacon.Core.Services;

/// <summary>
/// Action 执行框架（B-401，RFC §13）：按 ActionType 路由 IActionExecutor；
/// RequireConfirmation 走注入的确认通道（无通道时保守拒绝）；统一超时；
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
            return Fail(action.Id, $"未知 Action 类型：{action.Type}");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(Timeout);
        try
        {
            return await executor.ExecuteAsync(action, context, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Fail(action.Id, $"执行超时（>{Timeout.TotalSeconds:0}s）");
        }
        catch (OperationCanceledException)
        {
            return Cancelled(action.Id, "已取消");
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Action {ActionId} 执行失败。", action.Id);
            return Fail(action.Id, exception.Message);
        }
    }

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
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: _clock.UtcNow);

    private ActionResult Fail(string actionId, string message)
        => new(actionId, Success: false, Message: message, OutputPath: null, CompletedAt: _clock.UtcNow);
}
