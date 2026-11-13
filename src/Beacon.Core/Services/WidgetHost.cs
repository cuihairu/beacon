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
    /// <summary>生命周期串行闸（Codex 2026-11-12 收敛）：Start/Reload/ReloadRegistrations 的
    /// Clear→注册→启动序列与 KickConnection 批量 kick 互斥——kick 落在注册真空期会被调度器
    /// 静默丢掉（KickWidget no-op），复订唤醒丢失（AB2 修的正是这类丢唤醒）。</summary>
    private readonly object _lifecycleGate = new();
    /// <summary>真空期丢失的连接级 kick 记账（widgetId → 待补）：注册重建后由 DrainPendingKicks 补放，
    /// 唤醒意图穿越重建时序不丢失；kick 成功投递即出账，不产生重复拉取。</summary>
    private readonly HashSet<string> _pendingKicks = new(StringComparer.Ordinal);
    private readonly IDisposable _invalidationSubscription;
    private readonly IDisposable _actionSubscription;

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
        _actionSubscription = bus.Subscribe<ActionExecuted>(OnActionExecuted);
    }

    /// <summary>失效联动（P1-01）：删除/停用发布的 WidgetsInvalidated 同步清 Store 快照——
    /// 原先仅聚合器/面板订阅丢灯，Store 留着可被新消费者读出的幽灵快照。</summary>
    private void OnWidgetsInvalidated(WidgetsInvalidated evt)
    {
        _store.Invalidate(evt.WidgetIds);
    }

    /// <summary>只读状态查询（P3-03 快照-再-订阅口径的第一半）：新窗口/详情打开时先读 Store 最新值，
    /// 再靠事件订阅衔接后续变化，首次显示与订阅之间不漏更新；只读不暴露写入面——写入仍走刷新管线与失效联动。</summary>
    public WidgetState? GetState(string widgetId) => _store.Get(widgetId);

    /// <summary>动作后刷源（P5-02 请求接受与远端终态分离）：请求被接受不等于任务终态——
    /// 动作结算后立即拉一次来源 Widget，让「已接受→运行中→终态」由 Provider 权威状态驱动呈现，
    /// 不改权威状态制造成功假象；组件已删/连接停用由刷新链晚到保护拦截。</summary>
    private void OnActionExecuted(ActionExecuted evt)
    {
        if (evt.Result.SourceWidgetId is { } widgetId && _config.FindWidget(widgetId) is not null)
        {
            _ = RefreshWidgetAsync(widgetId);
        }
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

    /// <summary>注册全部 Widget 并启动调度；启动即水合缓存并 kick 第一轮拉取。</summary>
    public void Start()
    {
        lock (_lifecycleGate)
        {
            HydrateFromCache();

            _logger?.LogInformation("调度启动：全局检查频率 {PollSeconds}s（0=按档位），组件 {Count} 个。",
                _config.App.PollIntervalSeconds, _config.Widgets.Count);
            RegisterWidgets();
            _scheduler.Start(OnGroupRefreshAsync);
            DrainPendingKicks();
            KickAll();
        }
    }

    /// <summary>配置变化（Settings 增删 Widget/改查询参数）后重建注册并按需重拉（P3-02 受影响注册）：
    /// kickIds 内的组件各立即拉取一次，null = 全量（口径同旧 Reload）。不清缓存、不水合——
    /// 新鲜态不得被缓存陈旧视图盖回（ReloadRegistrations 同口径，见回归闸测试）。
    /// 走 TryRefreshOneAsync 直取而非 RefreshWidgetAsync：注册刚重建、节奏已归位，再 kick 会让
    /// 组循环立刻再拉一次（每组件双请求的另一半）；也不走 KickAll——旧实现全量重拉叠加调用方
    /// 对同一批组件的显式拉取，改一个组件 = 全体重拉，P3-02 由此收口。</summary>
    public void Reload(IEnumerable<string>? kickIds = null)
    {
        lock (_lifecycleGate)
        {
            _scheduler.Clear();
            RegisterWidgets();
            _scheduler.Start(OnGroupRefreshAsync);
            DrainPendingKicks(); // 复订唤醒意图穿越注册重建（Clear 会清掉调度侧已投递的 kick）
            foreach (var id in kickIds ?? _config.Widgets.Select(w => w.Id))
            {
                _ = TryRefreshOneAsync(id, CancellationToken.None);
            }
        }
    }

    /// <summary>展示面配置变化（布局/颜色/密度/树结构）后的注册重建（P3-02 分流）：
    /// 只重排调度注册，不 kick 不取数——展示保存零网络请求；检测间隔有变由注册周期自然接管。</summary>
    public void ReloadRegistrations()
    {
        lock (_lifecycleGate)
        {
            _scheduler.Clear();
            RegisterWidgets();
            DrainPendingKicks(); // 只重排不取数口径不变：补放的仅是此前真空期丢失的唤醒
        }
    }

    /// <summary>启动水合（P1-01）：磁盘缓存 → Store 先于首轮拉取——离线启动不等网络即亮旧数据与时间。
    /// 旧数据诚实标 IsStale（灰显+Last update）；首轮成功由新鲜态替换，未变化（304）由新鲜度恢复抬起。</summary>
    private void HydrateFromCache()
    {
        foreach (var widget in _config.Widgets)
        {
            var cached = _cache.LoadStates(widget.ConnectionId).GetValueOrDefault(widget.Id);
            if (cached is not null)
            {
                _store.Update(cached with { IsStale = true });
            }
        }
    }

    /// <summary>全局检查频率（设置「检查频率」，0=按档位策略表）：组件级检测间隔优先，未设组件级的按全局走。</summary>
    private void RegisterWidgets()
    {
        var globalPoll = _config.App.PollIntervalSeconds > 0
            ? TimeSpan.FromSeconds(_config.App.PollIntervalSeconds)
            : (TimeSpan?)null;
        foreach (var widget in _config.Widgets)
        {
            // 组件级检测间隔覆盖（设置页「检测间隔」）：null=按刷新档策略表
            TimeSpan? intervalOverride = widget.RefreshIntervalSeconds is { } seconds && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : globalPoll;
            _scheduler.Register(new WidgetRegistration(widget.Id, widget.ConnectionId, widget.RefreshTier, intervalOverride));
        }
    }

    private void KickAll()
    {
        foreach (var widget in _config.Widgets)
        {
            _scheduler.KickWidget(widget.Id);
        }
    }

    /// <summary>连接恢复（停用→启用）后唤醒该连接的组循环：打断退避等待，下一拍立即拉取。
    /// 停用期间组按 skip 退避越睡越久（30s/60s/...），仅翻 enabled 标志时循环仍要睡满当前段，
    /// 复订恢复刷新被推迟到退避段溢出（run 43134760840 场景 AB2 实证：判据窗只捞到 1 行）。
    /// 与手动刷新入口 RefreshWidgetAsync 的单组件 kick 同口径，这里是连接级批量 kick（不直取数，避免双请求）。
    /// kick 未投递（widget 恰处注册真空期）则记账，注册重建后补放——唤醒不因时序丢失。</summary>
    public void KickConnection(string connectionId)
    {
        lock (_lifecycleGate)
        {
            foreach (var widget in _config.Widgets.Where(w => w.ConnectionId == connectionId))
            {
                if (_scheduler.KickWidget(widget.Id))
                {
                    _pendingKicks.Remove(widget.Id);
                }
                else
                {
                    _pendingKicks.Add(widget.Id);
                }
            }
        }
    }

    /// <summary>补放真空期丢失的 kick（须持 _lifecycleGate 且注册已重建后调用）。</summary>
    private void DrainPendingKicks()
    {
        if (_pendingKicks.Count == 0)
        {
            return;
        }
        foreach (var id in _pendingKicks)
        {
            _scheduler.KickWidget(id); // 已注销的组件调度器 no-op，出账即可
        }
        _pendingKicks.Clear();
    }

    /// <summary>手动刷新入口（L2/L3 的 [Retry]，B-207）。</summary>
    public async Task<bool> RefreshWidgetAsync(string widgetId, CancellationToken cancellationToken = default)
    {
        var result = await TryRefreshOneAsync(widgetId, cancellationToken).ConfigureAwait(false);
        _scheduler.KickWidget(widgetId); // 让组循环恢复常规节奏
        return result != RefreshOutcome.Failed;
    }

    private async Task OnGroupRefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var failures = 0;
        var skipped = 0;
        foreach (var widgetId in request.WidgetIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await TryRefreshOneAsync(widgetId, cancellationToken).ConfigureAwait(false);
            if (outcome == RefreshOutcome.Failed)
            {
                failures++;
            }
            else if (outcome == RefreshOutcome.Skipped)
            {
                skipped++;
            }
        }
        if (failures == request.WidgetIds.Count)
        {
            // 整组失败 → 抛给调度器触发退避
            throw new ConnectionException(
                $"All {failures} widget(s) in group {request.ConnectionId}/{request.Tier} failed to refresh.",
                ConnectionHealthState.Offline);
        }
        if (skipped == request.WidgetIds.Count)
        {
            // 全组跳过（缺配置/缺 provider/连接停用）→ 抛给调度器退避一档
            // （审核修复 2026-11-12，P1：此前三种 skip 一律算成功，全组跳过时 failures==0，
            // 组循环按 15s 档全速空转刷日志）。
            throw new ConnectionException(
                $"All {skipped} widget(s) in group {request.ConnectionId}/{request.Tier} skipped (missing config/provider or disabled connection).",
                ConnectionHealthState.Offline);
        }
    }

    /// <summary>单组件刷新结果三分：刷新成（含 304 未变化）/ 跳过（配置暂不适用，不算失败）/ 失败（抛过异常）。</summary>
    private enum RefreshOutcome
    {
        Refreshed,
        Skipped,
        Failed,
    }

    private async Task<RefreshOutcome> TryRefreshOneAsync(string widgetId, CancellationToken cancellationToken)
    {
        var widget = _config.FindWidget(widgetId);
        var connection = _config.FindConnection(widget?.ConnectionId);
        if (widget is null || connection is null)
        {
            _logger?.LogWarning("Widget {WidgetId} or its connection is missing; skipped.", widgetId);
            return RefreshOutcome.Skipped;
        }
        var provider = _resolver.Resolve(widget.Type);
        if (provider is null)
        {
            _logger?.LogWarning("No provider for widget type {WidgetType}; skipped.", widget.Type);
            return RefreshOutcome.Skipped;
        }
        if (!connection.Enabled)
        {
            return RefreshOutcome.Skipped; // 模块停用：静默跳过（手动刷新不算失败；全组跳过走组级退避）
        }

        try
        {
            var state = await provider
                .GetStateAsync(widget, connection, new ConnectionContext { Secrets = _secrets }, cancellationToken)
                .ConfigureAwait(false);
            // 晚到结果闸门：await 期间组件/连接可能已被删停，写回前复核配置现值
            if (RefreshResultDropped(widgetId))
            {
                return RefreshOutcome.Skipped;
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
                return RefreshOutcome.Refreshed;
            }
            state = DebugInjection.OverrideSeverity(state); // debug 注入面：BEACON_DEBUG_SEVERITY=critical 时改判（缓存/事件/聚合全下游同判）
            _cache.SaveState(connection.Id, state);
            PublishHealth(connection.Id, ConnectionHealthState.Healthy);
            _store.Update(state);
            _logger?.LogInformation("Widget {WidgetId} refreshed ok: {Summary}", widgetId, state.Summary); // 逐跳诊断：拉取成功一行
            return RefreshOutcome.Refreshed;
        }
        catch (ConnectionException exception)
        {
            if (RefreshResultDropped(widgetId))
            {
                return RefreshOutcome.Skipped; // 失败结果同样晚到：不复活幽灵陈旧态
            }
            _logger?.LogWarning(exception, "Widget {WidgetId} refresh failed: {Health}.", widgetId, exception.Health);
            PublishHealth(connection.Id, exception.Health);
            PublishStale(widget, connection.Id, exception.Message); // 失败原因直显 tile（空白/静默吞错零容忍）
            return RefreshOutcome.Failed;
        }
        catch (Exception exception)
        {
            if (RefreshResultDropped(widgetId))
            {
                return RefreshOutcome.Skipped;
            }
            _logger?.LogError(exception, "Widget {WidgetId} refresh failed unexpectedly.", widgetId);
            PublishHealth(connection.Id, ConnectionHealthState.Degraded);
            PublishStale(widget, connection.Id, exception.Message);
            return RefreshOutcome.Failed;
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
        _actionSubscription.Dispose();
        _scheduler.Stop();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
