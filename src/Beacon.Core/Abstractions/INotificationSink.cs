using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>通知出口（App 侧实现：Windows Toast + 声音 + 托盘着色）。
/// 同步入队，投递由实现内部异步完成。</summary>
public interface INotificationSink
{
    void Show(NotificationRecord notification, NotificationDelivery delivery);
}

/// <summary>通知记录落盘（RFC §8：记录滚动保留 + 已读/未读跨重启）。默认实现 Storage 的 notifications.json。</summary>
public interface INotificationRecordStore
{
    Task<IReadOnlyList<NotificationRecord>> LoadAsync();

    Task SaveAsync(IReadOnlyList<NotificationRecord> records);
}
