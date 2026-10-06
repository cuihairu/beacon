namespace Beacon.Core.Abstractions;

/// <summary>时钟抽象：退避/冷却等时间逻辑一律可注入（RFC §15 测试策略）。</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
