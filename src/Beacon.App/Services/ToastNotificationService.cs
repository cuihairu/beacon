using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;

namespace Beacon.App.Services;

/// <summary>一次 Toast 点击激活的载荷（深链：widget → L3 详情）。</summary>
public sealed record ToastActivation(string? WidgetId, string? DetailUrl);

/// <summary>
/// Toast 通知出口（B-501，unpackaged）：Microsoft.Toolkit.Uwp.Notifications 的
/// ToastNotificationManagerCompat 兼容非打包应用——首次触碰（Initialize）时写
/// HKCU\Software\Classes\AppUserModelId\&lt;aumid&gt; 注册键（含 CustomActivator）绑定 AUMID，
/// 缺失自愈（RFC §8/§16 风险 2）。注意：toolkit 7.1.3 不创建开始菜单快捷方式，
/// Show() 只调 CreateToastNotifier().Show()（源证 ToastContentBuilder.cs:430）。
/// 点击激活通过 OnActivated 深链路由（widget → L3，L3 就绪前回退打开 DetailUrl）。
/// </summary>
internal sealed class ToastNotificationService : INotificationSink
{
    private const string ArgWidgetId = "widgetId";
    private const string ArgDetailUrl = "detailUrl";

    private readonly ILogger<ToastNotificationService> _logger;

    /// <summary>Toast 点击激活（已解析参数；回调在任意 COM 线程触发，消费方负责切 UI 线程）。</summary>
    public event Action<ToastActivation>? Activated;

    public ToastNotificationService(ILogger<ToastNotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>进程启动即调用：注册激活路由（含 Toast 冷启动回放）+ AUMID 注册自检。</summary>
    public void Initialize()
    {
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        CheckAumidHealth();
    }

    public void Show(NotificationRecord notification, NotificationDelivery delivery)
    {
        if (!delivery.Toast)
        {
            return;
        }
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(notification.Title)
                .AddText(BuildBody(notification))
                .AddArgument(ArgWidgetId, notification.SourceWidgetId);
            if (!string.IsNullOrEmpty(notification.DetailUrl))
            {
                builder.AddArgument(ArgDetailUrl, notification.DetailUrl);
            }
            builder.AddAudio(new ToastAudio { Silent = !delivery.Sound });
            builder.Show(); // AUMID 注册在 Initialize() 完成；Show 只发通知，不建快捷方式
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Toast 发送失败（{NotificationId}）。", notification.Id);
        }
    }

    /// <summary>
    /// AUMID 注册自检：toolkit 7.1.3 的「自愈」是注册表机制而非开始菜单快捷方式——
    /// 首次触碰 ToastNotificationManagerCompat（上方 OnActivated 挂接）即由 Initialize()
    /// 写 HKCU\Software\Classes\AppUserModelId\&lt;aumid&gt; 键并置 CustomActivator；
    /// Show() 不建 .lnk。缺失告警入应用日志（下次启动由 toolkit 重建自愈），不阻塞启动。
    /// Release 构建无调试器附挂时 Debug.WriteLine 是空操作，走应用日志才有取证面。
    /// </summary>
    private void CheckAumidHealth()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Classes\AppUserModelId");
            if (root is null)
            {
                _logger.LogWarning("AUMID 注册缺失（HKCU\\Software\\Classes\\AppUserModelId 不存在）——下次启动由 toolkit 重建（AUMID 自愈）。");
                return;
            }

            foreach (var name in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(name);
                if (sub?.GetValue("CustomActivator") is not null)
                {
                    return; // 注册在案
                }
            }

            _logger.LogWarning("AUMID 注册缺失（AppUserModelId 下无 CustomActivator 键）——下次启动由 toolkit 重建（AUMID 自愈）。");
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "AUMID 注册自检失败。");
        }
    }

    private static string BuildBody(NotificationRecord notification)
    {
        if (!string.IsNullOrEmpty(notification.Message))
        {
            return notification.Message;
        }
        return notification.WidgetType is { } type ? $"{type} · {notification.Timestamp:HH:mm:ss}" : notification.Timestamp.ToString("HH:mm:ss");
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat args)
    {
        ToastActivation activation;
        try
        {
            var arguments = ToastArguments.Parse(args.Argument);
            arguments.TryGetValue(ArgWidgetId, out string? widgetId);
            arguments.TryGetValue(ArgDetailUrl, out string? detailUrl);
            activation = new ToastActivation(widgetId, detailUrl);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Toast 激活参数解析失败：{Argument}", args.Argument);
            return;
        }
        try
        {
            Activated?.Invoke(activation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Toast 激活处理异常。");
        }
    }
}
