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
/// public：作为 public 窗口类（QuickPanelWindow）构造器参数，可及性须一致（CS0051）。
/// </summary>
public sealed class BeaconRuntime : IAsyncDisposable
{
    public IEventBus Bus { get; }
    public JsonConfigurationStore Config { get; }
    public JsonCacheStore Cache { get; }
    public IWidgetProviderResolver Resolver { get; }
    public WidgetHost Host { get; }
    public StatusAggregator Aggregator { get; }
    public NotificationEngine Notifications { get; }
    public ActionRunner Actions { get; }
    /// <summary>密钥存储（B-801 Settings 录入 token：credentialRef → DPAPI，严禁明文 JSON）。</summary>
    public DpapiSecretStore Secrets { get; }
    /// <summary>连接 Provider 注册表（B-801 测试连接）：类型标识 → Provider。</summary>
    public IReadOnlyDictionary<string, IConnectionProvider> ConnectionProviders { get; }

    private readonly RefreshScheduler _scheduler;

    private BeaconRuntime(
        IEventBus bus,
        JsonConfigurationStore config,
        JsonCacheStore cache,
        IWidgetProviderResolver resolver,
        RefreshScheduler scheduler,
        WidgetHost host,
        StatusAggregator aggregator,
        NotificationEngine notifications,
        ActionRunner actions,
        DpapiSecretStore secrets,
        IReadOnlyDictionary<string, IConnectionProvider> connectionProviders)
    {
        Bus = bus;
        Config = config;
        Cache = cache;
        Resolver = resolver;
        _scheduler = scheduler;
        Host = host;
        Aggregator = aggregator;
        Notifications = notifications;
        Actions = actions;
        Secrets = secrets;
        ConnectionProviders = connectionProviders;
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
            [HttpWidgetDescriptors.StatusType] = new HttpStatusProvider(), // positioning P0 #3：Generic HTTP
            [HttpWidgetDescriptors.QuotaType] = new HttpQuotaProvider(), // 验收反馈：通用额度数值卡（字段映射可配）
            [BigModelWidgetDescriptors.UsageType] = new BigModelUsageProvider(), // positioning P0 #5：GLM 套餐额度
            [ClaudeWidgetDescriptors.UsageType] = new ClaudeUsageProvider(), // positioning P0 #5：Claude 本机用量（读会话 JSONL）
            [DeepSeekWidgetDescriptors.BalanceType] = new DeepSeekBalanceProvider(), // positioning P0 #5：DeepSeek 余额
            [KimiWidgetDescriptors.CodingType] = new KimiCodingUsageProvider(), // positioning P0 #5：Kimi Coding 套餐余量
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

        var connectionProviders = new Dictionary<string, IConnectionProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["github"] = new GitHubConnectionProvider(),
            ["http"] = new HttpConnectionProvider(),
            ["bigmodel"] = new BigModelConnectionProvider(),
            ["claude"] = new ClaudeConnectionProvider(),
            ["deepseek"] = new DeepSeekConnectionProvider(),
            ["kimi"] = new KimiConnectionProvider(),
        };

        return new BeaconRuntime(bus, config, cache, resolver, scheduler, host, aggregator, notifications, actions, secrets, connectionProviders);
    }

    /// <summary>手动全量刷新（托盘 Refresh All）：逐 Widget 立即拉取，随后组循环恢复常规节奏。</summary>
    public void RefreshAll()
    {
        foreach (var widget in Config.Widgets)
        {
            _ = Host.RefreshWidgetAsync(widget.Id);
        }
    }

    /// <summary>
    /// 模块启停（PowerToys 形态配置中心）：按连接类型整体开关。停用即失活——宿主按 Enabled 跳过刷新，
    /// 已有状态经 WidgetsInvalidated 从聚合器/面板丢弃（不留旧灯）。
    /// </summary>
    public void SetConnectionTypeEnabled(string connectionType, bool enabled)
    {
        var affected = new List<string>();
        foreach (var connection in Config.Connections.Where(c => c.Type == connectionType && c.Enabled != enabled))
        {
            // ConnectionConfig 是 class 且 Enabled init-only：重建替换（连接是不可变值语义）
            Config.UpsertConnection(new ConnectionConfig
            {
                Id = connection.Id,
                Type = connection.Type,
                Endpoint = connection.Endpoint,
                CredentialRef = connection.CredentialRef,
                Enabled = enabled,
                Settings = connection.Settings,
            });
            if (!enabled)
            {
                affected.AddRange(Config.Widgets.Where(w => w.ConnectionId == connection.Id).Select(w => w.Id));
            }
        }
        if (affected.Count > 0)
        {
            Bus.Publish(new WidgetsInvalidated(affected));
        }
    }

    /// <summary>组件删除后的状态清理：聚合器与面板丢弃残留（不刷新周期也能即时生效）。</summary>
    public void InvalidateWidgets(IEnumerable<string> widgetIds)
    {
        var ids = widgetIds.ToList();
        if (ids.Count > 0)
        {
            Bus.Publish(new WidgetsInvalidated(ids));
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
