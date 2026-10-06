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
        tray.OpenPanelRequested += () => { /* P6 接入 QuickPanel */ };
        tray.SettingsRequested += () => { /* P8 接入 SettingsWindow */ };
        tray.ExitRequested += () => Exit();
        tray.Initialize();

        _logger.LogInformation("Beacon started (tray resident, no main window).");
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SingleInstanceGuard>();
        services.AddSingleton<Win32MessageWindow>();
        services.AddSingleton<TrayIconService>();
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
