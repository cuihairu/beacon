using Beacon.App.Infrastructure;
using Beacon.App.Services;
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

        var tray = Services.GetRequiredService<TrayIconService>();
        var quickPanel = new Windows.QuickPanelWindow();
        tray.OpenPanelRequested += () => quickPanel.Toggle();
        tray.SettingsRequested += () => { /* P8 接入 SettingsWindow */ };
        tray.ExitRequested += () => Exit();
        tray.Initialize();

        // B-102：L1 胶囊常驻（占位聚合，B-504 接入真实状态）
        var capsule = new Windows.CapsuleWindow(Services.GetRequiredService<ShellStateStore>());
        capsule.Activate();

        // B-103：全局热键（config.json 可改，B-801 提供设置 UI）
        var config = Services.GetRequiredService<Storage.AppConfigFile>().Load();
        var hotkey = Services.GetRequiredService<HotkeyService>();
        hotkey.Triggered += () => quickPanel.Toggle();
        if (!hotkey.Register(string.IsNullOrWhiteSpace(config.Hotkey) ? "Ctrl+Alt+B" : config.Hotkey))
        {
            tray.ShowBalloon("Beacon", $"热键 {config.Hotkey} 注册失败（可能被占用），可在设置中更换。", Infrastructure.NativeMethods.NIIF_WARNING);
        }

        _logger.LogInformation("Beacon started (tray resident, no main window).");
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SingleInstanceGuard>();
        services.AddSingleton<Win32MessageWindow>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<Storage.AppConfigFile>();
        services.AddSingleton<IUiDispatcher>(new UiDispatcher(Instance!.Dispatcher));
        services.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            }));
        return services.BuildServiceProvider();
    }
}
