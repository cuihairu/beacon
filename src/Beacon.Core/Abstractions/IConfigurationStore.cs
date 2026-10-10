using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>本地配置存储（%AppData%\Beacon\*.json，Local First，RFC §9）。</summary>
public interface IConfigurationStore
{
    AppConfig App { get; }

    IReadOnlyList<ConnectionConfig> Connections { get; }

    IReadOnlyList<WidgetConfig> Widgets { get; }

    PinsConfig Pins { get; }

    /// <summary>自定义动作库（actions.json）：用户在设置里定义的通用动作（HTTP/Webhook/本地命令）。</summary>
    IReadOnlyList<ActionConfig> Actions { get; }

    void LoadAll();

    void SaveApp();

    void SaveConnections();

    void SaveWidgets();

    void SavePins();

    void SaveActions();

    void UpsertConnection(ConnectionConfig connection);

    void RemoveConnection(string connectionId);

    void UpsertWidget(WidgetConfig widget);

    void RemoveWidget(string widgetId);

    void UpsertAction(ActionConfig action);

    void RemoveAction(string actionId);

    ConnectionConfig? FindConnection(string? connectionId);

    WidgetConfig? FindWidget(string widgetId);

    ActionConfig? FindAction(string actionId);
}
