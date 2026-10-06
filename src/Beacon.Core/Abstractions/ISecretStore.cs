namespace Beacon.Core.Abstractions;

/// <summary>密钥存储抽象：默认 DPAPI(CurrentUser) 实现（RFC §9.2）。
/// 配置文件只允许出现 credentialRef，严禁明文 token。</summary>
public interface ISecretStore
{
    Task<string?> GetAsync(string credentialRef);

    Task SetAsync(string credentialRef, string secret);

    Task DeleteAsync(string credentialRef);
}
