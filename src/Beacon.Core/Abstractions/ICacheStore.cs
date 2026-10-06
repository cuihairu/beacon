using Beacon.Core.Models;

namespace Beacon.Core.Abstractions;

/// <summary>Last-known-state 缓存：Beacon 打开永不白屏等网络（RFC §7.2 缓存优先启动）。</summary>
public interface ICacheStore
{
    IReadOnlyDictionary<string, WidgetState> LoadStates(string connectionId);

    void SaveState(string connectionId, WidgetState state);
}
