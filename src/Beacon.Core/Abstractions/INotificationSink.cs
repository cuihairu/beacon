using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>通知出口（App 侧实现：Windows Toast + 声音 + 托盘着色）。
/// 同步入队，投递由实现内部异步完成。</summary>
public interface INotificationSink
{
    void Show(NotificationRecord notification, NotificationDelivery delivery);
}
