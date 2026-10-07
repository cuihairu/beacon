namespace Beacon.Core.Models;

/// <summary>外部服务连接。凭据只存 credentialRef，严禁明文 token（RFC §4.2 / §9）。</summary>
public sealed class ConnectionConfig
{
    public required string Id { get; init; }
    /// <summary>"github" | "rest" | ...（Provider 类型标识）。</summary>
    public required string Type { get; init; }
    public string? Endpoint { get; init; }
    public string? CredentialRef { get; init; }
    /// <summary>模块启停（PowerToys 形态配置中心）：false 时宿主跳过刷新、UI 丢弃其状态。</summary>
    public bool Enabled { get; init; } = true;
    public Dictionary<string, string> Settings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
