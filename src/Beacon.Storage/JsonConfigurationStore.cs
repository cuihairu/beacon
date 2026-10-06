using Beacon.Core.Abstractions;
using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage;

/// <summary>
/// 本地配置存储（B-201，RFC §9.1）：config/connections/widgets/pins 四文件，
/// 临时文件 + 原子替换 + .bak 备份；读取失败回退备份，再失败用默认值。
/// Upsert/Remove 自动保存。
/// </summary>
public sealed class JsonConfigurationStore : IConfigurationStore
{
    private readonly string _directory;
    private readonly object _gate = new();
    private AppConfig _app = new();
    private List<ConnectionConfig> _connections = [];
    private List<WidgetConfig> _widgets = [];
    private PinsConfig _pins = new();

    public JsonConfigurationStore(string? rootDirectory = null)
    {
        _directory = rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon");
    }

    public AppConfig App { get { lock (_gate) return _app; } }

    public IReadOnlyList<ConnectionConfig> Connections { get { lock (_gate) return [.. _connections]; } }

    public IReadOnlyList<WidgetConfig> Widgets { get { lock (_gate) return [.. _widgets]; } }

    public PinsConfig Pins { get { lock (_gate) return _pins; } }

    public void LoadAll()
    {
        lock (_gate)
        {
            _app = LoadFile<AppConfig>("config.json") ?? new AppConfig();
            _connections = LoadFile<List<ConnectionConfig>>("connections.json") ?? [];
            _widgets = LoadFile<List<WidgetConfig>>("widgets.json") ?? [];
            _pins = LoadFile<PinsConfig>("pins.json") ?? new PinsConfig();
        }
    }

    public void SaveApp() => SaveFile("config.json", _app);

    public void SaveConnections() => SaveFile("connections.json", _connections);

    public void SaveWidgets() => SaveFile("widgets.json", _widgets);

    public void SavePins() => SaveFile("pins.json", _pins);

    public void UpsertConnection(ConnectionConfig connection)
    {
        lock (_gate)
        {
            var index = _connections.FindIndex(c => c.Id == connection.Id);
            if (index >= 0)
            {
                _connections[index] = connection;
            }
            else
            {
                _connections.Add(connection);
            }
        }
        SaveConnections();
    }

    public void RemoveConnection(string connectionId)
    {
        lock (_gate)
        {
            _connections.RemoveAll(c => c.Id == connectionId);
        }
        SaveConnections();
    }

    public void UpsertWidget(WidgetConfig widget)
    {
        lock (_gate)
        {
            var index = _widgets.FindIndex(w => w.Id == widget.Id);
            if (index >= 0)
            {
                _widgets[index] = widget;
            }
            else
            {
                _widgets.Add(widget);
            }
        }
        SaveWidgets();
    }

    public void RemoveWidget(string widgetId)
    {
        lock (_gate)
        {
            _widgets.RemoveAll(w => w.Id == widgetId);
        }
        SaveWidgets();
    }

    public ConnectionConfig? FindConnection(string? connectionId)
        => connectionId is null ? null : Connections.FirstOrDefault(c => c.Id == connectionId);

    public WidgetConfig? FindWidget(string widgetId) => Widgets.FirstOrDefault(w => w.Id == widgetId);

    private T? LoadFile<T>(string fileName) where T : class
    {
        var path = Path.Combine(_directory, fileName);
        try
        {
            if (File.Exists(path))
            {
                return BeaconJson.Deserialize<T>(File.ReadAllText(path));
            }
        }
        catch (Exception)
        {
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    return BeaconJson.Deserialize<T>(File.ReadAllText(backup));
                }
                catch (Exception)
                {
                    // 备份也坏：用默认值
                }
            }
        }
        return null;
    }

    private void SaveFile<T>(string fileName, T value)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, fileName);
        var temporary = path + ".tmp";
        var backup = path + ".bak";
        lock (_gate)
        {
            File.WriteAllText(temporary, BeaconJson.Serialize(value));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, backup);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
    }
}
