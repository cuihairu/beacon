using System.Text.Json;
using System.Text.Json.Serialization;

namespace Beacon.Core.Json;

/// <summary>全局 JSON 约定（RFC §4.2）：camelCase、枚举存字符串、缩进便于人读。</summary>
public static class BeaconJson
{
    public static JsonSerializerOptions Options { get; } = Create(writeIndented: true);

    public static JsonSerializerOptions CompactOptions { get; } = Create(writeIndented: false);

    public static string Serialize<T>(T value, bool indented = true)
        => JsonSerializer.Serialize(value, indented ? Options : CompactOptions);

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options)
           ?? throw new InvalidOperationException($"JSON deserialized to null for {typeof(T).Name}.");

    /// <summary>测试便利：序列化后立即反序列化。</summary>
    public static T RoundTrip<T>(T value) => Deserialize<T>(Serialize(value));

    private static JsonSerializerOptions Create(bool writeIndented) => new()
    {
        WriteIndented = writeIndented,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // payload 字典键保持原样，不做大小写改写
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
