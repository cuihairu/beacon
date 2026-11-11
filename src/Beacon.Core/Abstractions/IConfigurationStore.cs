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

    /// <summary>批量 upsert（审核修复 2026-11-12）：防呆口径同 UpsertWidget，锁与落盘各一次——
    /// 拖动落点等一次改多个 widget 的场景不再放大成 N 次全量 widgets.json 原子写。</summary>
    void UpsertWidgets(IReadOnlyList<WidgetConfig> widgets);

    void RemoveWidget(string widgetId);

    void UpsertAction(ActionConfig action);

    void RemoveAction(string actionId);

    ConnectionConfig? FindConnection(string? connectionId);

    WidgetConfig? FindWidget(string widgetId);

    ActionConfig? FindAction(string actionId);
}
