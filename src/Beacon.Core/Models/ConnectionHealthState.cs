namespace Beacon.Core.Models;

/// <summary>连接健康状态（RFC B-207）：错误只影响对应连接的 Widget，绝不让 Beacon 崩溃（RFC §12）。</summary>
public enum ConnectionHealthState
{
    Healthy,
    Degraded,
    Offline,
    Unauthorized,
}

/// <summary>连接级异常（认证失败/网络不可达/限流等）：Provider 抛出，WidgetHost 转换为连接健康事件。</summary>
public sealed class ConnectionException(string message, ConnectionHealthState health, Exception? innerException = null)
    : Exception(message, innerException)
{
    public ConnectionHealthState Health { get; } = health;
}
