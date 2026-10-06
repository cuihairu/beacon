using System.Security.Cryptography;
using System.Text;
using Beacon.Core.Abstractions;
using Beacon.Core.Json;

namespace Beacon.Storage;

/// <summary>
/// DPAPI(CurrentUser) 密钥库（B-202，RFC §9.2）：secrets.bin，配置文件只存 credentialRef。
/// 用 credentialRef 派生熵，绑定密钥与用途；仅 Windows 可用。
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public DpapiSecretStore(string? rootDirectory = null)
    {
        _path = Path.Combine(
            rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon"),
            "secrets.bin");
    }

    public Task<string?> GetAsync(string credentialRef)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DpapiSecretStore requires Windows (DPAPI).");
        }
        lock (_gate)
        {
            var store = Load();
            if (!store.TryGetValue(credentialRef, out var protectedBase64))
            {
                return Task.FromResult<string?>(null);
            }
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedBase64),
                Entropy(credentialRef),
                DataProtectionScope.CurrentUser);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
        }
    }

    public Task SetAsync(string credentialRef, string secret)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DpapiSecretStore requires Windows (DPAPI).");
        }
        lock (_gate)
        {
            var store = Load();
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(secret),
                Entropy(credentialRef),
                DataProtectionScope.CurrentUser);
            store[credentialRef] = Convert.ToBase64String(protectedBytes);
            Save(store);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string credentialRef)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DpapiSecretStore requires Windows (DPAPI).");
        }
        lock (_gate)
        {
            var store = Load();
            if (store.Remove(credentialRef))
            {
                Save(store);
            }
        }
        return Task.CompletedTask;
    }

    private static byte[] Entropy(string credentialRef) => Encoding.UTF8.GetBytes("Beacon:" + credentialRef);

    private Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return BeaconJson.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));
            }
        }
        catch (Exception)
        {
            // 密钥库损坏：视为空（用户需重录凭据）
        }
        return [];
    }

    private void Save(Dictionary<string, string> store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, BeaconJson.Serialize(store, indented: false));
        File.Move(temporary, _path, overwrite: true);
    }
}
