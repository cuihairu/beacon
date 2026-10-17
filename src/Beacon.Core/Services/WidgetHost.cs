using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;

namespace Beacon.Core.Services;

/// <summary>
/// Widget 数据管线（B-206/B-207）：调度器回调 → Provider 拉取 → 缓存落盘 → 状态发布。
/// 连接级失败转 ConnectionHealthChanged + 陈旧状态重发布（灰灯 + Last update，不甩异常，RFC §6.2.6/§12）。
/// 状态发布点经 IStateStore（Phase 1：发布职责移入 Store，事件契约不变；默认直通，去重启用待拍板）。
/// </summary>
public sealed class WidgetHost : IAsyncDisposable
{
    private readonly IEventBus _bus;
    private readonly IStateStore _store;
    private readonly IClock _clock;
    private readonly RefreshScheduler _scheduler;
    private readonly ICacheStore _cache;
    private readonly IConfigurationStore _config;
    private readonly ISecretStore _secrets;
    private readonly IWidgetProviderResolver _resolver;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, ConnectionHealthState> _health = [];
    private readonly IDisposable _invalidationSubscription;

    public WidgetHost(
        IEventBus bus,
        IClock clock,
        RefreshScheduler scheduler,
        ICacheStore cache,
        IConfigurationStore config,
        ISecretStore secrets,
        IWidgetProviderResolver resolver,
        ILogger? logger = null,
        IStateStore? stateStore = null)
    {
        _bus = bus;
        _store = stateStore ?? new StateStore(bus);
        _clock = clock;
        _scheduler = scheduler;
        _cache = cache;
        _config = config;
        _secrets = secrets;
        _resolver = resolver;
        _logger = logger;
        _invalidationSubscription = bus.Subscribe<WidgetsInvalidated>(OnWidgetsInvalidated);
    }

    /// <summary>失效联动（P1-01）：删除/停用发布的 WidgetsInvalidated 同步清 Store 快照——
    /// 原先仅聚合器/面板订阅丢灯，Store 留着可被新消费者读出的幽灵快照。</summary>
    private void OnWidgetsInvalidated(WidgetsInvalidated evt)
    {
        _store.Invalidate(evt.WidgetIds);
    }

    /// <summary>晚到结果保护（P1-01）：取数 await 期间组件被删或连接停用/删除——缓存/状态/健康/陈旧提示一律不写回。</summary>
    private bool RefreshResultDropped(string widgetId)
    {
        var live = _config.FindWidget(widgetId);
        if (live is null)
        {
            _logger?.LogWarning("Widget {WidgetId} disappeared during refresh; late result dropped.", widgetId);
            return true;
        }
        var connection = _config.FindConnection(live.ConnectionId);
        if (connection is null || !connection.Enabled)
        {
            _logger?.LogWarning("Connection {ConnectionId} for widget {WidgetId} gone or disabled during refresh; late result dropped.", live.ConnectionId, widgetId);
            return true;
        }
        return false;
    }

    /// <summary>注册全部 Widget 并启动调度；启动即 kick 第一轮拉取。</summary>
    public void Start()
    {
        // 启动水合（P1-01）：磁盘缓存 → Store 先于首轮拉取——离线启动不等网络即亮旧数据与时间。
        // 旧数据诚实标 IsStale（灰显+Last update）；首轮成功由新鲜态替换，未变化（304）由新鲜度恢复抬起。
        foreach (var widget in _config.Widgets)
        {
            var cached = _cache.LoadStates(widget.ConnectionId).GetValueOrDefault(widget.Id);
            if (cached is not null)
            {
                _store.Update(cached with { IsStale = true });
            }
        }

        // 全局检查频率（设置「检查频率」，0=按档位策略表）：组件级检测间隔优先，未设组件级的按全局走
        var globalPoll = _config.App.PollIntervalSeconds > 0
            ? TimeSpan.FromSeconds(_config.App.PollIntervalSeconds)
            : (TimeSpan?)null;
        _logger?.LogInformation("调度启动：全局检查频率 {PollSeconds}s（0=按档位），组件 {Count} 个。",
            _config.App.PollIntervalSeconds, _config.Widgets.Count);
        foreach (var widget in _config.Widgets)
        {
            // 组件级检测间隔覆盖（设置页「检测间隔」）：null=按刷新档策略表
            TimeSpan? intervalOverride = widget.RefreshIntervalSeconds is { } seconds && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : globalPoll;
            _scheduler.Register(new WidgetRegistration(widget.Id, widget.ConnectionId, widget.RefreshTier, intervalOverride));
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
        if (!connection.Enabled)
        {
            return true; // 模块停用：静默跳过（不算失败，不触发退避）
        }

        try
        {
            var state = await provider
                .GetStateAsync(widget, connection, new ConnectionContext { Secrets = _secrets }, cancellationToken)
                .ConfigureAwait(false);
            // 晚到结果闸门：await 期间组件/连接可能已被删停，写回前复核配置现值
            if (RefreshResultDropped(widgetId))
            {
                return true;
            }
            if (state is null)
            {
                // 条件请求未变化（ETag 304）：连接健康，但内容无新值，不重发不重写缓存。
                // 新鲜度恢复（P1-01）：失败期落进 Store 的陈旧视图在此抬回——IsStale/ConnectionHealthy
                // 翻转属内容键变化，去重仍会发布一次，恢复后不再一直灰显；缓存内容/FetchedAt/摘要全冻结。
                PublishHealth(connection.Id, ConnectionHealthState.Healthy);
                var cached = _cache.LoadStates(connection.Id).GetValueOrDefault(widgetId);
                var current = _store.Get(widgetId);
                if (cached is not null && current is not null && (current.IsStale || !current.ConnectionHealthy))
                {
                    _store.Update(cached with { IsStale = false, ConnectionHealthy = true });
                }
                return true;
            }
            state = DebugInjection.OverrideSeverity(state); // debug 注入面：BEACON_DEBUG_SEVERITY=critical 时改判（缓存/事件/聚合全下游同判）
            _cache.SaveState(connection.Id, state);
            PublishHealth(connection.Id, ConnectionHealthState.Healthy);
            _store.Update(state);
            _logger?.LogInformation("Widget {WidgetId} refreshed ok: {Summary}", widgetId, state.Summary); // 逐跳诊断：拉取成功一行
            return true;
        }
        catch (ConnectionException exception)
        {
            if (RefreshResultDropped(widgetId))
            {
                return true; // 失败结果同样晚到：不复活幽灵陈旧态
            }
            _logger?.LogWarning(exception, "Widget {WidgetId} refresh failed: {Health}.", widgetId, exception.Health);
            PublishHealth(connection.Id, exception.Health);
            PublishStale(widget, connection.Id, exception.Message); // 失败原因直显 tile（空白/静默吞错零容忍）
            return false;
        }
        catch (Exception exception)
        {
            if (RefreshResultDropped(widgetId))
            {
                return true;
            }
            _logger?.LogError(exception, "Widget {WidgetId} refresh failed unexpectedly.", widgetId);
            PublishHealth(connection.Id, ConnectionHealthState.Degraded);
            PublishStale(widget, connection.Id, exception.Message);
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

    private void PublishStale(WidgetConfig widget, string connectionId, string reason)
    {
        var cached = _cache.LoadStates(connectionId).GetValueOrDefault(widget.Id);
        var state = cached is null
            ? new WidgetState
            {
                WidgetId = widget.Id,
                WidgetType = widget.Type,
                ConnectionId = connectionId,
                Severity = Severity.Warning,
                Summary = $"拉取失败：{reason}", // 首错直显（tile 值位/L2 摘要可查，替代原 "Unable to refresh" 零信息）
                FetchedAt = _clock.UtcNow,
                IsStale = true,
                ConnectionHealthy = false,
            }
            // 陈旧至少抬到 Warning（与无缓存路径一致）：缓存命中时 severity 沿用 Success 的话，
            // builtin.connection-degraded 在真正的「连接降级」（有数据后断网）路径永不开火
            // （场景 F 首跑实证）。tile 灰显由 ConnectionHealthy 驱动，不受 severity 抬升影响。
            : cached with { IsStale = true, ConnectionHealthy = false, Severity = cached.Severity.Max(Severity.Warning) };
        _store.Update(state);
    }

    public async ValueTask DisposeAsync()
    {
        _invalidationSubscription.Dispose();
        _scheduler.Stop();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
