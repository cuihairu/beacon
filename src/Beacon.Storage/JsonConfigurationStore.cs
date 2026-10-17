using Beacon.Core.Abstractions;
using Beacon.Core.Json;
using Beacon.Core.Models;
using Beacon.Core.Services;

namespace Beacon.Storage;

/// <summary>
/// 本地配置存储（B-201，RFC §9.1）：config/connections/widgets/pins/actions 五文件，
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
    private List<ActionConfig> _actions = [];

    /// <summary>LoadAll 的读取诊断：哪些文件读坏了、恢复走到哪一步（主文件→.bak→默认）。空列表 = 全部健康。</summary>
    public List<string> LoadErrors { get; } = [];

    public JsonConfigurationStore(string? rootDirectory = null)
    {
        _directory = rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon");
    }

    public AppConfig App { get { lock (_gate) return _app; } }

    public IReadOnlyList<ConnectionConfig> Connections { get { lock (_gate) return [.. _connections]; } }

    public IReadOnlyList<WidgetConfig> Widgets { get { lock (_gate) return [.. _widgets]; } }

    public PinsConfig Pins { get { lock (_gate) return _pins; } }

    public IReadOnlyList<ActionConfig> Actions { get { lock (_gate) return [.. _actions]; } }

    public void LoadAll()
    {
        lock (_gate)
        {
            LoadErrors.Clear();
            _app = LoadFile<AppConfig>("config.json") ?? new AppConfig();
            if (_app.ConfigVersion < 1)
            {
                // 2026-10-09 用户令：状态胶囊默认关闭——存量配置一次性拉平（含历史上以 true 落盘的），
                // 用户仍可在设置里手动打开；打开后的值随 version 1 落盘，不再被迁移覆盖。
                _app.ShowCapsule = false;
                _app.ConfigVersion = 1;
                SaveFile("config.json", _app);
            }
            _connections = LoadFile<List<ConnectionConfig>>("connections.json") ?? [];
            _widgets = LoadFile<List<WidgetConfig>>("widgets.json") ?? [];
            SanitizeWidgetComponents();
            SanitizeComponentSources();
            _pins = LoadFile<PinsConfig>("pins.json") ?? new PinsConfig();
            _actions = LoadFile<List<ActionConfig>>("actions.json") ?? [];
        }
    }

    /// <summary>
    /// 数据源聚合防呆（Phase 3 行 4）：声明非法的面整面弃用附加源（自身状态恒可用），诊断进
    /// LoadErrors——口径同组件树。null 内层视为无声明，不进诊断。加载与写入（UpsertWidget）共用。
    /// </summary>
    private void SanitizeComponentSources()
    {
        foreach (var widget in _widgets)
        {
            SanitizeWidgetSources(widget);
        }
    }

    /// <summary>
    /// 组件树防呆（Phase 3 行 1）：结构非法的树整树弃用回传统渲染，诊断进 LoadErrors——
    /// 常驻进程不因手坏 widgets.json 崩，也不静默裁剪成半棵。加载与写入（UpsertWidget）共用。
    /// </summary>
    private void SanitizeWidgetComponents()
    {
        foreach (var widget in _widgets)
        {
            SanitizeWidgetSurfaces(widget);
        }
    }

    /// <summary>写入面防呆（Phase 5 行 3，组件实例编辑）：UpsertWidget 与加载同一 fail-closed
    /// 口径——编辑器/手改落盘的坏树不再等下次重启才被清。入参 widget 是调用方私有对象，
    /// 尚未共享，净化无需持锁。Phase 4（2026-10-17 裁定）：htmltemplate 载体面在此展开后校验，
    /// 坏模板加载/写入即报错并整面弃用，不等首次渲染才发现。</summary>
    private void SanitizeWidgetSurfaces(WidgetConfig widget)
    {
        if (widget.Components is not { Count: > 0 } components)
        {
            return;
        }
        var invalid = new List<string>();
        foreach (var pair in components)
        {
            var result = ValidateSurfaceTree(pair.Value);
            if (result.Ok)
            {
                continue;
            }
            LoadErrors.Add($"widgets.json {widget.Id}/{pair.Key}: {string.Join("; ", result.Errors)}");
            invalid.Add(pair.Key);
        }
        foreach (var key in invalid)
        {
            components.Remove(key);
        }
    }

    /// <summary>面树校验：普通树走 ComponentTreeValidator；htmltemplate 载体面先展开
    /// （模板解析 + 载体合法性）再校验合成树——失败诊断带模板首错，可读可查。</summary>
    private static ComponentTreeValidation ValidateSurfaceTree(ComponentDescriptor declared)
    {
        var expanded = HtmlTemplateParser.ExpandCarrier(declared);
        if (expanded is not null)
        {
            return ComponentTreeValidator.Validate(expanded);
        }
        if (!HtmlTemplateParser.IsCarrier(declared))
        {
            // 不可达：非载体 ExpandCarrier 恒原样返回；防御性兜底与调用面同口径
            return new ComponentTreeValidation(false, ["surface tree is invalid"]);
        }
        var parsed = HtmlTemplateParser.Parse(declared.Props.GetValueOrDefault("html"));
        var reason = parsed.Errors.Count > 0
            ? parsed.Errors[0]
            : "carrier is invalid (children/bindings/props out of contract)";
        return new ComponentTreeValidation(false, [$"html template rejected: {reason}"]);
    }

    private void SanitizeWidgetSources(WidgetConfig widget)
    {
        if (widget.Sources is not { Count: > 0 } sources)
        {
            return;
        }
        var invalid = new List<string>();
        foreach (var pair in sources)
        {
            var result = ComponentSourcesValidator.Validate(pair.Value);
            if (result.Ok)
            {
                continue;
            }
            LoadErrors.Add($"widgets.json {widget.Id}/{pair.Key}: {string.Join("; ", result.Errors)}");
            invalid.Add(pair.Key);
        }
        foreach (var key in invalid)
        {
            sources.Remove(key);
        }
    }

    public void SaveApp() => SaveFile("config.json", _app);

    public void SaveConnections() => SaveFile("connections.json", _connections);

    public void SaveWidgets() => SaveFile("widgets.json", _widgets);

    public void SavePins() => SaveFile("pins.json", _pins);

    public void SaveActions() => SaveFile("actions.json", _actions);

    /// <summary>B-802 导入：五份配置整体替换并落盘（secrets 不随包走，credentialRef 原样保留待重录）。</summary>
    public void ReplaceAll(AppConfig app, List<ConnectionConfig> connections, List<WidgetConfig> widgets, PinsConfig pins, List<ActionConfig> actions)
    {
        lock (_gate)
        {
            _app = app;
            _connections = connections;
            _widgets = widgets;
            _pins = pins;
            _actions = actions;
        }
        SaveApp();
        SaveConnections();
        SaveWidgets();
        SavePins();
        SaveActions();
    }

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
        // 写入面防呆（Phase 5 行 3）：与加载同一 fail-closed 口径——坏树/坏源不落盘
        SanitizeWidgetSurfaces(widget);
        SanitizeWidgetSources(widget);
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

    public void UpsertAction(ActionConfig action)
    {
        lock (_gate)
        {
            var index = _actions.FindIndex(a => a.Id == action.Id);
            if (index >= 0)
            {
                _actions[index] = action;
            }
            else
            {
                _actions.Add(action);
            }
        }
        SaveActions();
    }

    public void RemoveAction(string actionId)
    {
        lock (_gate)
        {
            _actions.RemoveAll(a => a.Id == actionId);
        }
        SaveActions();
    }

    public ConnectionConfig? FindConnection(string? connectionId)
        => connectionId is null ? null : Connections.FirstOrDefault(c => c.Id == connectionId);

    public WidgetConfig? FindWidget(string widgetId) => Widgets.FirstOrDefault(w => w.Id == widgetId);

    public ActionConfig? FindAction(string actionId) => Actions.FirstOrDefault(a => a.Id == actionId);

    private T? LoadFile<T>(string fileName) where T : class
    {
        var path = Path.Combine(_directory, fileName);
        try
        {
            if (File.Exists(path))
            {
                return BeaconJson.Deserialize<T>(File.ReadAllText(path));
            }
            return null;
        }
        catch (Exception exception)
        {
            // 损坏原件先留档（*.corrupt 单份覆盖），绝不让后续落盘把坏文件直接冲掉——恢复的最小依据
            LoadErrors.Add($"{fileName}: {exception.Message}");
            try
            {
                File.Copy(path, path + ".corrupt", overwrite: true);
            }
            catch (Exception copyFailure)
            {
                LoadErrors.Add($"{fileName}: 留档 .corrupt 失败 {copyFailure.Message}");
            }
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    var recovered = BeaconJson.Deserialize<T>(File.ReadAllText(backup));
                    LoadErrors.Add($"{fileName}: 已从 .bak 备份恢复");
                    return recovered;
                }
                catch (Exception backupFailure)
                {
                    LoadErrors.Add($"{fileName}: .bak 备份也损坏 {backupFailure.Message}");
                }
            }
            return null;
        }
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
