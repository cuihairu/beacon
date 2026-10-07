using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage;

/// <summary>导出包结构（B-802）：四份配置合一单文件；secrets 绝不入包，connections 只带 credentialRef 引用。</summary>
public sealed class ConfigBundle
{
    public int Version { get; set; } = ImportExport.CurrentVersion;
    public DateTimeOffset ExportedAt { get; set; }
    public AppConfig App { get; set; } = new();
    public List<ConnectionConfig> Connections { get; set; } = [];
    public List<WidgetConfig> Widgets { get; set; } = [];
    public PinsConfig Pins { get; set; } = new();
}

/// <summary>导入结果：credentialRef 列表供 UI 逐个提示重录密钥（新机器 DPAPI 里没有对应明文）。</summary>
public sealed record ImportResult(int Connections, int Widgets, int Pins, IReadOnlyList<string> CredentialRefs);

/// <summary>
/// 配置导入导出（B-802）：config/connections/widgets/pins 打包成单 JSON。
/// 红线（RFC §13）：导出文件全文无 token——密钥只存 DPAPI，包内仅 credentialRef；
/// 导入后须按返回的 credentialRef 逐个提示用户重录密钥，行为才与原机一致。
/// </summary>
public sealed class ImportExport(JsonConfigurationStore config)
{
    public const int CurrentVersion = 1;

    public void Export(string filePath)
    {
        var bundle = new ConfigBundle
        {
            ExportedAt = DateTimeOffset.Now,
            App = config.App,
            Connections = [.. config.Connections],
            Widgets = [.. config.Widgets],
            Pins = config.Pins,
        };
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, BeaconJson.Serialize(bundle));
    }

    public ImportResult Import(string filePath)
    {
        var bundle = BeaconJson.Deserialize<ConfigBundle>(File.ReadAllText(filePath));
        if (bundle.Version != CurrentVersion)
        {
            throw new InvalidOperationException($"不支持的导出包版本 {bundle.Version}（当前支持 {CurrentVersion}）。");
        }
        config.ReplaceAll(bundle.App, bundle.Connections, bundle.Widgets, bundle.Pins);
        var credentialRefs = bundle.Connections
            .Where(connection => !string.IsNullOrWhiteSpace(connection.CredentialRef))
            .Select(connection => connection.CredentialRef!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new ImportResult(bundle.Connections.Count, bundle.Widgets.Count, bundle.Pins.Tiles.Count, credentialRefs);
    }
}
