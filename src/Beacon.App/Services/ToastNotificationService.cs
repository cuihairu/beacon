using Beacon.Core.Abstractions;
using Beacon.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Beacon.App.Services;

/// <summary>一次 Toast 点击激活的载荷（深链：widget → L3 详情）。</summary>
public sealed record ToastActivation(string? WidgetId, string? DetailUrl);

/// <summary>
/// Toast 通知出口（B-501，unpackaged）：Microsoft.Toolkit.Uwp.Notifications 的
/// ToastNotificationManagerCompat 兼容非打包应用——首次发送时自动创建/修复
/// 开始菜单快捷方式并绑定 AUMID（缺失自愈，RFC §8/§16 风险 2）。
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

    /// <summary>进程启动即调用：注册激活路由（含 Toast 冷启动回放）+ 快捷方式自检。</summary>
    public void Initialize()
    {
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        CheckShortcutHealth();
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
            builder.Audio(new ToastAudio { Silent = !delivery.Sound });
            builder.Show(); // 首次调用自动创建开始菜单快捷方式（AUMID 自愈）
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Toast 发送失败（{NotificationId}）。", notification.Id);
        }
    }

    /// <summary>快捷方式自检：缺失只告警（下次发送时由 toolkit 修复），不阻塞启动。</summary>
    private static void CheckShortcutHealth()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs",
            Path.ChangeExtension(AppDomain.CurrentDomain.FriendlyName, ".lnk"));
        if (!File.Exists(expected))
        {
            // 自愈路径：Toolkit 在下一次 Toast 发送时重建快捷方式
            System.Diagnostics.Debug.WriteLine($"Toast shortcut missing: {expected}");
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
