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

    Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken);
}

/// <summary>Widget Provider：把外部数据映射为统一 WidgetState（Severity/Lifecycle 唯一口径）。</summary>
public interface IWidgetProvider
{
    WidgetTypeDescriptor Descriptor { get; }

    Task<WidgetState> GetStateAsync(WidgetConfig widget, ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken);
}

/// <summary>按 widget type 解析 Provider——Core 不感知具体 Provider（Provider First，RFC §5）。</summary>
public interface IWidgetProviderResolver
{
    IWidgetProvider? Resolve(string widgetType);
}
