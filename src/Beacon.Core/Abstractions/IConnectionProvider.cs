using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>Provider 执行上下文（密钥等）。</summary>
public sealed class ConnectionContext
{
    public required ISecretStore Secrets { get; init; }
}

/// <summary>连接 Provider：认证与连通性（RFC §5）。</summary>
public interface IConnectionProvider
{
    string ConnectionType { get; }

    Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken);
}

/// <summary>连接测试结果：健康态 + 失败原因（Detail 原样透出给设置页反馈——「降级（限流等）」这类
/// 通用文案吞掉真实原因（凭据格式/4xx 响应体/响应结构），用户无法判断「明明连上了却报降级」到底
/// 是哪一环。Healthy 时 Detail 为 null）。</summary>
public readonly record struct ConnectionTestResult(ConnectionHealthState Health, string? Detail)
{
    public static ConnectionTestResult Ok() => new(ConnectionHealthState.Healthy, null);
}

/// <summary>Widget Provider：把外部数据映射为统一 WidgetState（Severity/Lifecycle 唯一口径）。
/// 返回 null = 条件请求命中（如 ETag 304），内容未变化，宿主跳过发布/缓存。</summary>
public interface IWidgetProvider
{
    WidgetTypeDescriptor Descriptor { get; }

    Task<WidgetState?> GetStateAsync(WidgetConfig widget, ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken);
}

/// <summary>按 widget type 解析 Provider——Core 不感知具体 Provider（Provider First，RFC §5）。</summary>
public interface IWidgetProviderResolver
{
    IWidgetProvider? Resolve(string widgetType);
}
