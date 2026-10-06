using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>本地配置存储（%AppData%\Beacon\*.json，Local First，RFC §9）。</summary>
public interface IConfigurationStore
{
    AppConfig App { get; }

    IReadOnlyList<ConnectionConfig> Connections { get; }

    IReadOnlyList<WidgetConfig> Widgets { get; }

    PinsConfig Pins { get; }

    void LoadAll();

    void SaveApp();

    void SaveConnections();

    void SaveWidgets();

    void SavePins();

    void UpsertConnection(ConnectionConfig connection);

    void RemoveConnection(string connectionId);

    void UpsertWidget(WidgetConfig widget);

    void RemoveWidget(string widgetId);

    ConnectionConfig? FindConnection(string? connectionId);

    WidgetConfig? FindWidget(string widgetId);
}
