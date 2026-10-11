using System.Globalization;
using System.Net.NetworkInformation;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>machine 组件类型元数据（本机资源，零网络零凭据）：驱动 Settings 向导与 L0 准入。</summary>
public static class MachineWidgetDescriptors
{
    public const string NetType = "machine.net";

    public static WidgetTypeDescriptor Net { get; } = new()
    {
        Type = NetType,
        DisplayName = "本机网络（IP · 速率 · 流量）",
        PinSupported = true,
        FloatingOptIn = true, // 数值类：设置「数量悬浮窗」开关放行（2026-10-11 用户令默认开）
        SuggestedTier = RefreshTiers.Machine,
        Fields =
        [
            new WidgetFieldDescriptor("label", "显示名", Placeholder: "本机网络"),
            new WidgetFieldDescriptor("interface", "网卡名（空 = 自动选活动网卡）", Placeholder: "以太网 / Wi-Fi / eth0"),
        ],
    };

    public static IReadOnlyList<WidgetTypeDescriptor> All { get; } = [Net];
}

/// <summary>
/// 网卡采样快照：收发计数是 OS 自开机累计口径，速率由 Provider 按相邻两次差分算出；
/// 时间戳随样本走（源注入时间，Provider 不持时钟——测试确定性的关键）。
/// </summary>
public readonly record struct MachineNetSample(
    DateTimeOffset At,
    string? InterfaceName,
    string? Address,
    long SentBytes,
    long ReceivedBytes,
    bool Up);

/// <summary>采样抽象：生产走 System.Net.NetworkInformation，测试注入假样本（Linux 全程可测，不碰真实网卡）。</summary>
public interface IMachineNetSource
{
    MachineNetSample Sample(string? interfaceName);
}

/// <summary>
/// 网卡中性快照：NetworkInterface 是抽象类无法构造，选卡逻辑要在纯数据上做才可测（Linux 单测不碰真实网卡）。
/// </summary>
internal readonly record struct NicSnapshot(
    string Name,
    OperationalStatus Status,
    NetworkInterfaceType Type,
    IReadOnlyList<System.Net.IPAddress> Unicast,
    IReadOnlyList<System.Net.IPAddress> Gateways)
{
    internal static NicSnapshot From(NetworkInterface nic)
    {
        var properties = nic.GetIPProperties();
        return new NicSnapshot(
            nic.Name,
            nic.OperationalStatus,
            nic.NetworkInterfaceType,
            [.. properties.UnicastAddresses.Select(unicast => unicast.Address)],
            [.. properties.GatewayAddresses.Select(gateway => gateway.Address)]);
    }

    internal bool HasLanIpv4(IReadOnlyList<System.Net.IPAddress> addresses)
        => addresses.Any(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && !System.Net.IPAddress.Loopback.Equals(address));
}

/// <summary>真实采样：NetworkInterface 全量枚举 → 选卡 → 首个非回环 IPv4 + IPv4 收发计数。</summary>
public sealed class NetworkInterfaceNetSource : IMachineNetSource
{
    public MachineNetSample Sample(string? interfaceName)
    {
        var nics = NetworkInterface.GetAllNetworkInterfaces();
        var index = SelectIndex([.. nics.Select(NicSnapshot.From)], interfaceName);
        if (index < 0)
        {
            if (!string.IsNullOrWhiteSpace(interfaceName))
            {
                // 点名网卡不存在是配置错误（fail-loud，与 Jenkins job_path 同口径）；自动模式无活动卡是常态（软态 Warning）
                throw new ConnectionException($"找不到网卡「{interfaceName.Trim()}」。", ConnectionHealthState.Degraded);
            }
            return new MachineNetSample(DateTimeOffset.UtcNow, null, null, 0, 0, Up: false);
        }

        var nic = nics[index];
        var address = NicSnapshot.From(nic).Unicast.FirstOrDefault(IsLanIpv4);
        var statistics = nic.GetIPv4Statistics();
        return new MachineNetSample(
            DateTimeOffset.UtcNow,
            nic.Name,
            address?.ToString(),
            statistics.BytesSent,
            statistics.BytesReceived,
            Up: nic.OperationalStatus == OperationalStatus.Up);
    }

    /// <summary>
    /// 选卡（纯函数供单测，返回下标 -1 = 无卡）：点名网卡按名精确匹配（不区分大小写，不筛状态——
    /// 拔线也要报「无活动」而非「找不到」）；自动模式取「Up + 非回环 + 有非回环 IPv4」，
    /// 有网关者优先（默认路由所在卡才是真出口），同分保持枚举序。
    /// </summary>
    internal static int SelectIndex(IReadOnlyList<NicSnapshot> snapshots, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            for (var i = 0; i < snapshots.Count; i++)
            {
                if (string.Equals(snapshots[i].Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        var best = -1;
        var bestHasGateway = false;
        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (snapshot.Status != OperationalStatus.Up
                || snapshot.Type == NetworkInterfaceType.Loopback
                || !snapshot.HasLanIpv4(snapshot.Unicast))
            {
                continue;
            }

            var hasGateway = snapshot.HasLanIpv4(snapshot.Gateways);
            if (best < 0 || (hasGateway && !bestHasGateway))
            {
                best = i;
                bestHasGateway = hasGateway;
            }
        }
        return best;
    }

    private static bool IsLanIpv4(System.Net.IPAddress address)
        => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && !System.Net.IPAddress.Loopback.Equals(address);
}

/// <summary>
/// machine.net（本机网络，用户拍板 2026-10-11「网络资源比如ip 上行 下行速度 总的流量」）：
/// IP / 上行下行速率 / 累计流量。速率 = 相邻两次采样计数差 ÷ 时间差（machine 档 15s，首次刷新无基线只出
/// IP 与累计值）；计数器回退（网卡重启/换卡）丢弃差分重记基线。累计流量进 Payload 走 L3 详情，
/// tile 一行摘要只放 IP + 实时速率（32 DIP 装不下四个数）。连接语义沿用 claude/codex 零凭据先例。
/// </summary>
public sealed class MachineNetProvider : IWidgetProvider
{
    private readonly IMachineNetSource _source;
    private readonly object _gate = new();
    private readonly Dictionary<string, MachineNetSample> _last = new(StringComparer.Ordinal); // widget.Id → 基线样本

    public MachineNetProvider() : this(new NetworkInterfaceNetSource()) { }

    public MachineNetProvider(IMachineNetSource source) => _source = source;

    public WidgetTypeDescriptor Descriptor => MachineWidgetDescriptors.Net;

    public Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var sample = _source.Sample(widget.Config.GetValueOrDefault("interface"));

        double? upSpeed = null;
        double? downSpeed = null;
        lock (_gate)
        {
            if (_last.TryGetValue(widget.Id, out var baseline)
                && sample.Up
                && baseline.Up
                && baseline.InterfaceName == sample.InterfaceName
                && sample.At > baseline.At)
            {
                var seconds = (sample.At - baseline.At).TotalSeconds;
                // dt ≥ 1s 防手动连刷出尖峰假速率；计数回退 = 网卡重启，不出速率只换基线
                if (seconds >= 1 && sample.SentBytes >= baseline.SentBytes && sample.ReceivedBytes >= baseline.ReceivedBytes)
                {
                    upSpeed = (sample.SentBytes - baseline.SentBytes) / seconds;
                    downSpeed = (sample.ReceivedBytes - baseline.ReceivedBytes) / seconds;
                }
            }
            _last[widget.Id] = sample;
        }

        var head = widget.Config.GetValueOrDefault("label") is { Length: > 0 } label ? label : "本机网络";
        Severity severity;
        string summary;
        if (!sample.Up)
        {
            severity = Severity.Warning;
            summary = $"{head} · 无活动网卡";
        }
        else
        {
            severity = Severity.Info;
            var speeds = downSpeed is { } down && upSpeed is { } up
                ? $" · ↓{FormatRate(down, "/s")} ↑{FormatRate(up, "/s")}"
                : "";
            summary = $"{head} · {sample.Address}{speeds}";
        }

        return Task.FromResult<WidgetState?>(new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Warning ? LifecycleState.Running : LifecycleState.Success,
            Progress = null,
            Summary = summary,
            DetailUrl = null,
            Payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ip"] = sample.Address ?? "",
                ["interface"] = sample.InterfaceName ?? "",
                ["up_bps"] = upSpeed?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["down_bps"] = downSpeed?.ToString("0", CultureInfo.InvariantCulture) ?? "",
                ["total_sent"] = sample.SentBytes.ToString(CultureInfo.InvariantCulture),
                ["total_received"] = sample.ReceivedBytes.ToString(CultureInfo.InvariantCulture),
            },
            FetchedAt = DateTimeOffset.UtcNow,
        });
    }

    internal static string FormatSpeed(double bytesPerSecond) => FormatRate(bytesPerSecond, "/s");

    internal static string FormatBytes(long bytes) => FormatRate(bytes, "");

    private static string FormatRate(double bytes, string suffix)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value.ToString(index == 0 ? "0" : "0.0", CultureInfo.InvariantCulture)} {units[index]}{suffix}";
    }
}

/// <summary>machine 连接：本机数据源（零网络零凭据）。无端点无凭据，恒通——本机即本机。</summary>
public sealed class MachineConnectionProvider : IConnectionProvider
{
    public string ConnectionType => "machine";

    public Task<ConnectionTestResult> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken cancellationToken)
        => Task.FromResult(ConnectionTestResult.Ok());
}
