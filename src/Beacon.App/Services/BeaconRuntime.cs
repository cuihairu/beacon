using Beacon.Actions;
using Beacon.Connections;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
using Beacon.Core.Models;
using Beacon.Core.Services;
using Beacon.Storage;
using Microsoft.Extensions.Logging;

namespace Beacon.App.Services;

/// <summary>App 侧 Provider 解析：widget 类型 → Provider 实例（新连接类型在此注册，Core 零改动）。</summary>
internal sealed class DictionaryWidgetResolver(IReadOnlyDictionary<string, IWidgetProvider> providers) : IWidgetProviderResolver
{
    public IWidgetProvider? Resolve(string widgetType) => providers.GetValueOrDefault(widgetType);
}

/// <summary>
/// 运行时装配（B-504）：配置/密钥/缓存 + GitHub Providers + 调度/宿主/聚合/通知/动作
/// 组装成一条数据流（Provider → WidgetState → Event Bus → 聚合/通知/UI，RFC §3）。
/// </summary>
internal sealed class BeaconRuntime : IAsyncDisposable
{
    public IEventBus Bus { get; }
    public JsonConfigurationStore Config { get; }
    public JsonCacheStore Cache { get; }
    public WidgetHost Host { get; }
    public StatusAggregator Aggregator { get; }
    public NotificationEngine Notifications { get; }
    public ActionRunner Actions { get; }

    private readonly RefreshScheduler _scheduler;

    private BeaconRuntime(
        IEventBus bus,
        JsonConfigurationStore config,
        JsonCacheStore cache,
        RefreshScheduler scheduler,
        WidgetHost host,
        StatusAggregator aggregator,
        NotificationEngine notifications,
        ActionRunner actions)
    {
        Bus = bus;
        Config = config;
        Cache = cache;
        _scheduler = scheduler;
        Host = host;
        Aggregator = aggregator;
        Notifications = notifications;
        Actions = actions;
    }

    public static BeaconRuntime Start(INotificationSink sink, ILogger? logger = null)
    {
        var bus = new EventBus(logger);
        var config = new JsonConfigurationStore();
        var secrets = new DpapiSecretStore();
        var cache = new JsonCacheStore();
        var clock = new SystemClock();

        var resolver = new DictionaryWidgetResolver(new Dictionary<string, IWidgetProvider>(StringComparer.Ordinal)
        {
            [GitHubWidgetDescriptors.PullRequestsType] = new GitHubPullRequestsProvider(),
            [GitHubWidgetDescriptors.ActionsRunsType] = new GitHubActionsProvider(),
        });

        var scheduler = new RefreshScheduler(logger: logger);
        var host = new WidgetHost(bus, clock, scheduler, cache, config, secrets, resolver, logger);
        var aggregator = new StatusAggregator(bus);
        var notifications = new NotificationEngine(bus, sink, clock, logger: logger);
        var actions = new ActionRunner(
            [
                new OpenUrlExecutor(),
                new LocalCommandExecutor(),
                new HttpExecutor(),
                new GitHubWorkflowDispatchExecutor(),
                new GitHubWorkflowRerunExecutor(),
                new GitHubWorkflowCancelExecutor(),
            ],
            bus, clock, config, secrets, logger);

        return new BeaconRuntime(bus, config, cache, scheduler, host, aggregator, notifications, actions);
    }

    /// <summary>手动全量刷新（托盘 Refresh All）：逐 Widget 立即拉取，随后组循环恢复常规节奏。</summary>
    public void RefreshAll()
    {
        foreach (var widget in Config.Widgets)
        {
            _ = Host.RefreshWidgetAsync(widget.Id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync().ConfigureAwait(false);
        await _scheduler.DisposeAsync().ConfigureAwait(false);
        Aggregator.Dispose();
        Notifications.Dispose();
    }
}
