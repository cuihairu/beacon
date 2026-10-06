using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;

namespace Beacon.Core.Services;

/// <summary>
/// Widget 数据管线（B-206/B-207）：调度器回调 → Provider 拉取 → 缓存落盘 → 事件发布。
/// 连接级失败转 ConnectionHealthChanged + 陈旧状态重发布（灰灯 + Last update，不甩异常，RFC §6.2.6/§12）。
/// </summary>
public sealed class WidgetHost : IAsyncDisposable
{
    private readonly IEventBus _bus;
    private readonly IClock _clock;
    private readonly RefreshScheduler _scheduler;
    private readonly ICacheStore _cache;
    private readonly IConfigurationStore _config;
    private readonly ISecretStore _secrets;
    private readonly IWidgetProviderResolver _resolver;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, ConnectionHealthState> _health = [];

    public WidgetHost(
        IEventBus bus,
        IClock clock,
        RefreshScheduler scheduler,
        ICacheStore cache,
        IConfigurationStore config,
        ISecretStore secrets,
        IWidgetProviderResolver resolver,
        ILogger? logger = null)
    {
        _bus = bus;
        _clock = clock;
        _scheduler = scheduler;
        _cache = cache;
        _config = config;
        _secrets = secrets;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>注册全部 Widget 并启动调度；启动即 kick 第一轮拉取。</summary>
    public void Start()
    {
        foreach (var widget in _config.Widgets)
        {
            _scheduler.Register(new WidgetRegistration(widget.Id, widget.ConnectionId, widget.RefreshTier));
        }
        _scheduler.Start(OnGroupRefreshAsync);
        foreach (var widget in _config.Widgets)
        {
            _scheduler.KickWidget(widget.Id);
        }
    }

    /// <summary>配置变化（Settings 增删 Widget）后重建注册。</summary>
    public void Reload()
    {
        _scheduler.Clear();
        Start();
    }

    /// <summary>手动刷新入口（L2/L3 的 [Retry]，B-207）。</summary>
    public async Task<bool> RefreshWidgetAsync(string widgetId, CancellationToken cancellationToken = default)
    {
        var result = await TryRefreshOneAsync(widgetId, cancellationToken).ConfigureAwait(false);
        _scheduler.KickWidget(widgetId); // 让组循环恢复常规节奏
        return result;
    }

    private async Task OnGroupRefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var failures = 0;
        foreach (var widgetId in request.WidgetIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await TryRefreshOneAsync(widgetId, cancellationToken).ConfigureAwait(false))
            {
                failures++;
            }
        }
        if (failures == request.WidgetIds.Count)
        {
            // 整组失败 → 抛给调度器触发退避
            throw new ConnectionException(
                $"All {failures} widget(s) in group {request.ConnectionId}/{request.Tier} failed to refresh.",
                ConnectionHealthState.Offline);
        }
    }

    private async Task<bool> TryRefreshOneAsync(string widgetId, CancellationToken cancellationToken)
    {
        var widget = _config.FindWidget(widgetId);
        var connection = _config.FindConnection(widget?.ConnectionId);
        if (widget is null || connection is null)
        {
            _logger?.LogWarning("Widget {WidgetId} or its connection is missing; skipped.", widgetId);
            return true;
        }
        var provider = _resolver.Resolve(widget.Type);
        if (provider is null)
        {
            _logger?.LogWarning("No provider for widget type {WidgetType}; skipped.", widget.Type);
            return true;
        }

        try
        {
            var state = await provider
                .GetStateAsync(widget, connection, new ConnectionContext { Secrets = _secrets }, cancellationToken)
                .ConfigureAwait(false);
            if (state is null)
            {
                // 条件请求未变化（ETag 304）：连接健康，但内容无新值，不重发不重写缓存
                PublishHealth(connection.Id, ConnectionHealthState.Healthy);
                return true;
            }
            _cache.SaveState(connection.Id, state);
            PublishHealth(connection.Id, ConnectionHealthState.Healthy);
            _bus.Publish(new WidgetStateChanged(state));
            return true;
        }
        catch (ConnectionException exception)
        {
            _logger?.LogWarning(exception, "Widget {WidgetId} refresh failed: {Health}.", widgetId, exception.Health);
            PublishHealth(connection.Id, exception.Health);
            PublishStale(widget, connection.Id);
            return false;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Widget {WidgetId} refresh failed unexpectedly.", widgetId);
            PublishHealth(connection.Id, ConnectionHealthState.Degraded);
            PublishStale(widget, connection.Id);
            return false;
        }
    }

    private void PublishHealth(string connectionId, ConnectionHealthState state)
    {
        lock (_gate)
        {
            if (_health.TryGetValue(connectionId, out var current) && current == state)
            {
                return;
            }
            _health[connectionId] = state;
        }
        _bus.Publish(new ConnectionHealthChanged(connectionId, state, _clock.UtcNow));
    }

    private void PublishStale(WidgetConfig widget, string connectionId)
    {
        var cached = _cache.LoadStates(connectionId).GetValueOrDefault(widget.Id);
        var state = cached is null
            ? new WidgetState
            {
                WidgetId = widget.Id,
                WidgetType = widget.Type,
                ConnectionId = connectionId,
                Severity = Severity.Warning,
                Summary = "Unable to refresh",
                FetchedAt = _clock.UtcNow,
                IsStale = true,
                ConnectionHealthy = false,
            }
            : cached with { IsStale = true, ConnectionHealthy = false };
        _bus.Publish(new WidgetStateChanged(state));
    }

    public async ValueTask DisposeAsync()
    {
        _scheduler.Stop();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
