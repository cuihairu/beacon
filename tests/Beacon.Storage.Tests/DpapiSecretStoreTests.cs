namespace Beacon.Storage.Tests;

/// <summary>
/// B-202 验收：set/get/delete 往返；磁盘文件不可读出明文。
/// 非 Windows 只验证 PlatformNotSupportedException 守卫；真实 DPAPI 往返在 windows-latest CI 上执行。
/// </summary>
public sealed class DpapiSecretStoreTests
{
    [Fact]
    public async Task NonWindows_ThrowsPlatformNotSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Windows CI 走完整往返用例
        }
        using var dir = new TempDir();
        var store = new DpapiSecretStore(dir.Path);

        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => store.GetAsync("k"));
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => store.SetAsync("k", "v"));
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => store.DeleteAsync("k"));
    }

    [Fact]
    public async Task SetGetDelete_RoundTrip_AndPlaintextNeverOnDisk()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // DPAPI 仅 Windows；CI = windows-latest
        }
        using var dir = new TempDir();
        var store = new DpapiSecretStore(dir.Path);
        var credentialRef = "test:" + Guid.NewGuid().ToString("N");
        const string secret = "ghp_supersecrettoken";

        await store.SetAsync(credentialRef, secret);
        Assert.Equal(secret, await store.GetAsync(credentialRef));

        var raw = await File.ReadAllTextAsync(System.IO.Path.Combine(dir.Path, "secrets.bin"));
        Assert.DoesNotContain(secret, raw); // 磁盘只有密文

        await store.DeleteAsync(credentialRef);
        Assert.Null(await store.GetAsync(credentialRef));
    }

    [Fact]
    public async Task DifferentCredentialRefs_CannotDecryptEachOther()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // 熵绑定 credentialRef 的校验依赖真实 DPAPI
        }
        using var dir = new TempDir();
        var store = new DpapiSecretStore(dir.Path);

        await store.SetAsync("ref-a", "secret-a");
        await store.SetAsync("ref-b", "secret-b");
        Assert.Equal("secret-a", await store.GetAsync("ref-a"));
        Assert.Equal("secret-b", await store.GetAsync("ref-b"));
    }

    [Fact]
    public async Task GetUnknownRef_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var dir = new TempDir();
        var store = new DpapiSecretStore(dir.Path);

        Assert.Null(await store.GetAsync("never-set"));
    }
}
