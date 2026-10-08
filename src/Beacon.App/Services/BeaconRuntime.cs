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
    /// <summary>诊断日志（文件通道，%APPDATA%\Beacon\logs）——UI 层（设置/面板）逐跳打点共用。</summary>
    public ILogger? Logger { get; }

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
        IReadOnlyDictionary<string, IConnectionProvider> connectionProviders,
        ILogger? logger = null)
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
        Logger = logger;
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

        return new BeaconRuntime(bus, config, cache, resolver, scheduler, host, aggregator, notifications, actions, secrets, connectionProviders, logger);
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
    /// 组件增删/钉选后的全链初始化（白板三连根因收口）：以前 UpsertWidget 只落盘，
    /// 调度器全然不知（Host.Reload 无人调用）——新组件进不了组循环，tile 空等到重启；
    /// 托盘强刷（RefreshAll 直调 RefreshWidgetAsync）是唯一出数路径，即「强刷才出数据」。
    /// 现在任何组件变更走这里：重建注册 + 立即拉取 + 事件驱动重渲，一次开钉即到位。
    /// </summary>
    public void ReloadWidgets(IEnumerable<string>? kickIds = null)
    {
        Host.Reload();
        var ids = (kickIds ?? Config.Widgets.Select(w => w.Id)).ToList();
        foreach (var id in ids)
        {
            _ = Host.RefreshWidgetAsync(id);
        }
        Logger?.LogInformation("组件注册已重建并立即拉取 {Count} 个：{Ids}", ids.Count, string.Join(", ", ids));
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
