using Beacon.Core.Json;
using Beacon.Core.Models;

namespace Beacon.Storage;

/// <summary>导出包结构（B-802）：五份配置合一单文件；secrets 绝不入包，connections 只带 credentialRef 引用。
/// Actions 字段为后加（2026-10-10）：旧包无此字段，反序列化得空列表，导入即清空动作库——向后兼容不升版本号。</summary>
public sealed class ConfigBundle
{
    public int Version { get; set; } = ImportExport.CurrentVersion;
    public DateTimeOffset ExportedAt { get; set; }
    public AppConfig App { get; set; } = new();
    public List<ConnectionConfig> Connections { get; set; } = [];
    public List<WidgetConfig> Widgets { get; set; } = [];
    public PinsConfig Pins { get; set; } = new();
    public List<ActionConfig> Actions { get; set; } = [];
}

/// <summary>导入结果：credentialRef 列表供 UI 逐个提示重录密钥（新机器 DPAPI 里没有对应明文）。</summary>
public sealed record ImportResult(int Connections, int Widgets, int Pins, int Actions, IReadOnlyList<string> CredentialRefs);

/// <summary>
/// 配置导入导出（B-802）：config/connections/widgets/pins/actions 打包成单 JSON。
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
            Actions = [.. config.Actions],
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
        config.ReplaceAll(bundle.App, bundle.Connections, bundle.Widgets, bundle.Pins, bundle.Actions);
        var credentialRefs = bundle.Connections
            .Where(connection => !string.IsNullOrWhiteSpace(connection.CredentialRef))
            .Select(connection => connection.CredentialRef!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new ImportResult(bundle.Connections.Count, bundle.Widgets.Count, bundle.Pins.Tiles.Count, bundle.Actions.Count, credentialRefs);
    }
}
