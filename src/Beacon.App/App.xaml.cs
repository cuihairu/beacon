using Beacon.App.Infrastructure;
using Beacon.App.Services;
using Beacon.Core.Abstractions;
using Beacon.Core.Events;
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
    private Windows.PinnedHostWindow? _pinnedHost;
    private readonly Dictionary<string, Windows.DetailWindow> _detailWindows = [];

    public App()
    {
        Instance = this;
        // B-105 崩溃兜底必须在一切之前挂上：托盘常驻无主窗，异常被吞=用户眼里「点了没反应」。
        // 这三个处理器原先在 OnLaunched 中段才注册，此前（构造器读配置等）的异常零痕迹静默死亡。
        UnhandledException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "XAML unhandled exception: {Message}", e.Message);
            CrashLog.Write("XAML 未处理异常：" + e.Message, e.Exception);
            e.Handled = true; // 常驻应用不闪退（RFC §10 崩溃兜底）
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            _logger?.LogCritical(e.ExceptionObject as Exception, "Fatal (terminating={IsTerminating})", e.IsTerminating);
            CrashLog.Write($"进程级未处理异常（terminating={e.IsTerminating}）", e.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "Unobserved task exception");
            CrashLog.Write("未观察 Task 异常（已观察，不影响进程）", e.Exception);
            e.SetObserved();
        };
        InitializeComponent();
        try
        {
            // B-801 外观主题：RequestedTheme 只能在 App 构造器设置——重启生效口径（浅色/深色，缺省跟随系统）
            var theme = new Storage.AppConfigFile().Load().Theme?.Trim().ToLowerInvariant();
            if (theme == "light")
            {
                RequestedTheme = ApplicationTheme.Light;
            }
            else if (theme == "dark")
            {
                RequestedTheme = ApplicationTheme.Dark;
            }
        }
        catch
        {
            // 主题读不出来（配置损坏等）就跟随系统，不因外观配置阻断启动
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Startup();
        }
        catch (Exception exception)
        {
            // 启动失败必须可见：弹窗给出异常与日志路径（XAML 兜底 Handled=true 只保进程，
            // 用户侧仍是「没反应」——所以 OnLaunched 单独兜，亮明错误再退）。
            _logger?.LogCritical(exception, "Beacon 启动失败");
            CrashLog.Alert("Beacon 启动失败", exception);
            Exit();
        }
    }

    private void Startup()
    {
        Dispatcher = DispatcherQueue.GetForCurrentThread();
        Services = BuildServices();
        _logger = Services.GetRequiredService<ILogger<App>>();

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
        tray.Palette = new UiPalette(runtime.Config); // B-706：托盘圆点按 appearance.SeverityColors 运行时绘制
        _quickPanel = new Windows.QuickPanelWindow(runtime, dispatcher);
        var quickPanel = _quickPanel;
        tray.OpenPanelRequested += () => quickPanel.Toggle();
        quickPanel.DetailRequested += state => OpenDetail(runtime, state); // B-603：L2 单击进 L3
        quickPanel.PinsChanged += () => Dispatcher.TryEnqueue(() => _pinnedHost?.ReloadTiles()); // B-702：Pin 入口联动 L0
        // B-503：Notifications 入口（P6 B-602 换成通知中心视图）；Refresh All 在 B-504 接运行时
        tray.NotificationsRequested += () => Dispatcher.TryEnqueue(quickPanel.Toggle);
        tray.ExitRequested += () => Exit();
        tray.Initialize();
        // 托盘常驻无主窗口：启动即给可见反馈，避免被当成「点了没反应」
        tray.ShowBalloon("Beacon", "已启动并常驻托盘——状态胶囊在屏幕右下角，点胶囊或托盘图标打开面板。");

        // B-504：胶囊/托盘接真实聚合
        var capsule = new Windows.CapsuleWindow(Services.GetRequiredService<ShellStateStore>(), runtime.Config);
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
        if (!runtime.Config.App.ShowCapsule)
        {
            capsule.StartHidden(); // B-801：showCapsule=false 时注册但不显示（避免先显示后隐藏的闪烁）
        }
        capsule.Activate();
        capsule.ApplyOpacity(runtime.Config.App.UiOpacity); // B-801：界面透明度启动即生效
        runtime.Host.Start();

        // B-701：L0 悬浮组件宿主（主屏单实例；多显示器分配随 B-703 位置持久化）
        if (Infrastructure.MonitorService.Primary() is { } primaryMonitor)
        {
            _pinnedHost = new Windows.PinnedHostWindow(runtime, primaryMonitor);
            _pinnedHost.TileActivated += () => Dispatcher.TryEnqueue(quickPanel.Toggle); // L0 点击下钻 L2（RFC §6.2.7）
            _pinnedHost.Initialize();
        }

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

        // B-801：Settings 窗口（托盘入口，单例）。常规项落库即存 config.json，经 ApplySettingsEffects
        // 重挂热键/对齐自启/胶囊显隐与透明度（主题走 App 构造器，重启生效）；组件增删与钉选重建 L0。
        void ApplySettingsEffects()
        {
            var appConfig = runtime.Config.App;
            if (!string.IsNullOrWhiteSpace(appConfig.Hotkey) && appConfig.Hotkey != hotkey.Current)
            {
                hotkey.Unregister();
                if (!hotkey.Register(appConfig.Hotkey))
                {
                    tray.ShowBalloon("Beacon", $"热键 {appConfig.Hotkey} 注册失败（可能被占用），可在设置中更换。", Infrastructure.NativeMethods.NIIF_WARNING);
                }
            }
            Services.GetRequiredService<StartupService>().SyncWith(appConfig.LaunchOnStartup);
            capsule.SetVisible(appConfig.ShowCapsule);
            capsule.ApplyOpacity(appConfig.UiOpacity);
            // B-805：调色即时生效——托盘/胶囊/L0 用最新快照按新色表重渲染（UiPalette 实时读 config）
            var snapshot = runtime.Aggregator.Snapshot();
            capsule.UpdateStatus(snapshot);
            tray.SetSeverity(snapshot.Overall);
            tray.SetTip(BuildTrayTip(snapshot));
            _pinnedHost?.ReloadTiles();
        }

        Windows.SettingsWindow? settingsWindow = null;
        tray.SettingsRequested += () => Dispatcher.TryEnqueue(() =>
        {
            if (settingsWindow is { } open)
            {
                open.Activate();
                return;
            }
            settingsWindow = new Windows.SettingsWindow(runtime);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
            settingsWindow.SettingsApplied += ApplySettingsEffects;
            settingsWindow.PinsChanged += () => Dispatcher.TryEnqueue(() => _pinnedHost?.ReloadTiles());
            settingsWindow.Activate();
        });

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
        services.AddSingleton<ShellStateStore>();
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
