using Beacon.Core.Abstractions;
using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage;

/// <summary>Last-known-state 缓存（B-206，RFC §7.2）：cache/{connectionId}/states.json，
/// 缓存优先启动——Beacon 打开永不白屏等网络。</summary>
public sealed class JsonCacheStore : ICacheStore
{
    private readonly string _cacheDirectory;
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, WidgetState>> _memory = [];

    public JsonCacheStore(string? rootDirectory = null)
    {
        _cacheDirectory = Path.Combine(
            rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon"),
            "cache");
    }

    public IReadOnlyDictionary<string, WidgetState> LoadStates(string connectionId)
    {
        lock (_gate)
        {
            return EnsureLoaded(connectionId);
        }
    }

    public void SaveState(string connectionId, WidgetState state)
    {
        lock (_gate)
        {
            var states = EnsureLoaded(connectionId);
            states[state.WidgetId] = state;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatesPath(connectionId))!);
                var temporary = StatesPath(connectionId) + ".tmp";
                File.WriteAllText(temporary, BeaconJson.Serialize(states, indented: false));
                File.Move(temporary, StatesPath(connectionId), overwrite: true);
            }
            catch (IOException)
            {
                // 缓存写失败不影响主流程
            }
        }
    }

    private Dictionary<string, WidgetState> EnsureLoaded(string connectionId)
    {
        if (_memory.TryGetValue(connectionId, out var cached))
        {
            return cached;
        }
        var path = StatesPath(connectionId);
        try
        {
            if (File.Exists(path))
            {
                var states = BeaconJson.Deserialize<Dictionary<string, WidgetState>>(File.ReadAllText(path));
                _memory[connectionId] = states;
                return states;
            }
        }
        catch (Exception)
        {
            // 缓存损坏当作无缓存
        }
        var empty = new Dictionary<string, WidgetState>();
        _memory[connectionId] = empty;
        return empty;
    }

    private string StatesPath(string connectionId) => Path.Combine(_cacheDirectory, connectionId, "states.json");
}
