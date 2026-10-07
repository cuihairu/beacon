using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Beacon.App;

/// <summary>应用入口：单实例、托盘常驻、无主窗口启动（RFC §39 Startup）。</summary>
public partial class App : Application
{
    public static App Instance { get; private set; } = null!;

    public IServiceProvider Services { get; private set; } = null!;

    public DispatcherQueue Dispatcher { get; private set; } = null!;

    private ILogger<App> _logger = null!;
    private Windows.QuickPanelWindow? _quickPanel;
    private readonly Dictionary<string, Windows.DetailWindow> _detailWindows = [];

    public App()
    {
        Instance = this;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Dispatcher = DispatcherQueue.GetForCurrentThread();
        Services = BuildServices();
        _logger = Services.GetRequiredService<ILogger<App>>();

        ConfigureGlobalExceptionHandlers();

        // B-501：Toast 基础设施——激活路由要在任何窗口创建前注册（支持 Toast 冷启动回放）
        var toast = Services.GetRequiredService<ToastNotificationService>();
        toast.Initialize();
        toast.Activated += activation => Dispatcher.TryEnqueue(() => OpenToastTarget(activation));

        var guard = Services.GetRequiredService<SingleInstanceGuard>();
        if (!guard.IsPrimary)
        {
            // B-101 验收：二次启动不重复进程，并唤出既有实例
            guard.SignalExisting();
            _logger.LogInformation("Secondary instance exit.");
            Exit();
            return;
        }

        guard.OnActivate(() => Dispatcher.TryEnqueue(() =>
            Services.GetRequiredService<TrayIconService>().ShowBalloon("Beacon", "Beacon 已在运行。")));

        // B-504/B-602：运行时先行装配，L2 Quick Panel 直接消费事件流（缓存优先，打开零等待）
        var runtime = BeaconRuntime.Start(toast, _logger);
        var dispatcher = Services.GetRequiredService<IUiDispatcher>();
        var tray = Services.GetRequiredService<TrayIconService>();
        _quickPanel = new Windows.QuickPanelWindow(runtime, dispatcher);
        var quickPanel = _quickPanel;
        tray.OpenPanelRequested += () => quickPanel.Toggle();
        quickPanel.DetailRequested += state => OpenDetail(runtime, state); // B-603：L2 单击进 L3
        tray.SettingsRequested += () => { /* P8 接入 SettingsWindow */ };
        // B-503：Notifications 入口（P6 B-602 换成通知中心视图）；Refresh All 在 B-504 接运行时
        tray.NotificationsRequested += () => Dispatcher.TryEnqueue(quickPanel.Toggle);
        tray.ExitRequested += () => Exit();
        tray.Initialize();

        // B-504：胶囊/托盘接真实聚合
        var capsule = new Windows.CapsuleWindow(Services.GetRequiredService<ShellStateStore>());
        capsule.OpenPanelRequested += () => Dispatcher.TryEnqueue(quickPanel.Toggle);
        runtime.Bus.Subscribe<WidgetStateChanged>(evt => Dispatcher.TryEnqueue(() => capsule.NoteFetch(evt.State.FetchedAt)));
        runtime.Bus.Subscribe<AggregateStatusChanged>(status => Dispatcher.TryEnqueue(() =>
        {
            capsule.UpdateStatus(status);
            tray.SetSeverity(status.Overall);
            tray.SetTip(BuildTrayTip(status));
        }));
        tray.RefreshAllRequested += () => Dispatcher.TryEnqueue(runtime.RefreshAll);

        // 初值渲染（空集 = Success 基线），再启动调度（首轮拉取立即 kick）
        var snapshot = runtime.Aggregator.Snapshot();
        capsule.UpdateStatus(snapshot);
        tray.SetSeverity(snapshot.Overall);
        tray.SetTip(BuildTrayTip(snapshot));
        capsule.Activate();
        runtime.Host.Start();

        // B-103：全局热键（config.json 可改，B-801 提供设置 UI）
        var config = Services.GetRequiredService<Storage.AppConfigFile>().Load();

        // B-104：开机自启对齐（config.LaunchOnStartup 为准）
        Services.GetRequiredService<StartupService>().SyncWith(config.LaunchOnStartup);

        var hotkey = Services.GetRequiredService<HotkeyService>();
        hotkey.Triggered += () => quickPanel.Toggle();
        if (!hotkey.Register(string.IsNullOrWhiteSpace(config.Hotkey) ? "Ctrl+Alt+B" : config.Hotkey))
        {
            tray.ShowBalloon("Beacon", $"热键 {config.Hotkey} 注册失败（可能被占用），可在设置中更换。", Infrastructure.NativeMethods.NIIF_WARNING);
        }

        _logger.LogInformation("Beacon started (tray resident, no main window).");
    }

    /// <summary>L3 详情窗（B-603）：每 Widget 一窗，重复请求前置激活。</summary>
    private void OpenDetail(BeaconRuntime runtime, Beacon.Core.Models.WidgetState state)
    {
        if (_detailWindows.TryGetValue(state.WidgetId, out var existing))
        {
            existing.Activate();
            return;
        }
        var window = new Windows.DetailWindow(state, runtime, () => _detailWindows.Remove(state.WidgetId));
        _detailWindows[state.WidgetId] = window;
        window.Activate();
    }

    /// <summary>托盘提示文本（B-503）：分级计数 + 离线连接数。</summary>
    private static string BuildTrayTip(Beacon.Core.Events.AggregateStatusChanged status)
    {
        var ok = status.Counts.GetValueOrDefault(Beacon.Core.Models.Severity.Success) + status.Counts.GetValueOrDefault(Beacon.Core.Models.Severity.Info);
        var warn = status.Counts.GetValueOrDefault(Beacon.Core.Models.Severity.Warning);
        var error = status.Counts.GetValueOrDefault(Beacon.Core.Models.Severity.Error) + status.Counts.GetValueOrDefault(Beacon.Core.Models.Severity.Critical);
        var tip = $"Beacon · {ok} ok / {warn} warn / {error} error";
        return status.OfflineConnections > 0 ? $"{tip} · {status.OfflineConnections} offline" : tip;
    }

    /// <summary>Toast 点击激活路由（B-501）：有 DetailUrl 直接深链，L3 详情窗就绪（B-603）前回退浏览器；否则唤出 L2。</summary>
    private void OpenToastTarget(ToastActivation activation)
    {
        try
        {
            if (!string.IsNullOrEmpty(activation.DetailUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(activation.DetailUrl) { UseShellExecute = true });
                return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "打开 Toast 深链失败：{Url}", activation.DetailUrl);
        }
        _quickPanel?.Toggle();
    }

    /// <summary>崩溃兜底（B-105）：UI 异常标记 Handled 保持常驻，非 UI 线程异常落日志。</summary>
    private void ConfigureGlobalExceptionHandlers()
    {
        UnhandledException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "XAML unhandled exception: {Message}", e.Message);
            e.Handled = true; // 常驻应用不闪退（RFC §10 崩溃兜底）
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            _logger?.LogCritical(e.ExceptionObject as Exception, "Fatal (terminating={IsTerminating})", e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SingleInstanceGuard>();
        services.AddSingleton<Win32MessageWindow>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<ToastNotificationService>();
        services.AddSingleton<Core.Abstractions.INotificationSink>(sp => sp.GetRequiredService<ToastNotificationService>());
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<Storage.AppConfigFile>();
        services.AddSingleton<IUiDispatcher>(new UiDispatcher(Instance!.Dispatcher));
        services.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            })
            .AddProvider(new FileLoggerProvider()));
        return services.BuildServiceProvider();
    }
}
