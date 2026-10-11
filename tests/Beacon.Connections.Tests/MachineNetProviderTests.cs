using System.Net;
using System.Net.NetworkInformation;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// machine.net（本机网络，2026-10-11 用户拍板「ip 上行 下行速度 总的流量」）：
/// 选卡纯函数（点名精确匹配 / 自动优先有网关卡 / 回环与无 IPv4 剔除）、
/// 速率差分（首刷无基线 / dt&lt;1s 不出速率 / 计数回退重记基线 / 换卡重记基线）、
/// 无活动网卡软态 Warning、点名网卡不存在 fail-loud、速率与字节单位换算。
/// </summary>
public sealed class MachineNetProviderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    // —— 选卡：NicSnapshot 中性快照上的纯函数（NetworkInterface 抽象类无法构造） ——

    private static NicSnapshot Nic(string name, OperationalStatus status = OperationalStatus.Up,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet,
        string[]? unicast = null, string[]? gateways = null)
        => new(name, status, type,
            [.. (unicast ?? ["192.168.1.5"]).Select(IPAddress.Parse)],
            [.. (gateways ?? ["192.168.1.1"]).Select(IPAddress.Parse)]);

    [Fact]
    public void SelectIndex_ByNameExactCaseInsensitive()
    {
        var snapshots = new[] { Nic("Loopback"), Nic("以太网"), Nic("Wi-Fi") };
        Assert.Equal(1, NetworkInterfaceNetSource.SelectIndex(snapshots, "以太网"));
        Assert.Equal(2, NetworkInterfaceNetSource.SelectIndex(snapshots, " wi-fi ")); // 两侧空白裁掉 + 不区分大小写
    }

    [Fact]
    public void SelectIndex_ByNameMissing_ReturnsNegativeOne()
        => Assert.Equal(-1, NetworkInterfaceNetSource.SelectIndex([Nic("eth0")], "eth9"));

    [Fact]
    public void SelectIndex_Auto_SkipsLoopbackAndDownAndNoIpv4()
    {
        var snapshots = new[]
        {
            Nic("lo", type: NetworkInterfaceType.Loopback),                          // 回环剔除
            Nic("断网 Wi-Fi", status: OperationalStatus.Down),                       // Down 剔除
            Nic("仅 IPv6", unicast: ["fe80::1"]),                                    // 无非回环 IPv4 剔除
            Nic("eth0"),
        };
        Assert.Equal(3, NetworkInterfaceNetSource.SelectIndex(snapshots, null));
    }

    [Fact]
    public void SelectIndex_Auto_PrefersGatewayCard_KeepsOrderOnTie()
    {
        var snapshots = new[]
        {
            Nic("vpn0", gateways: []),      // 有 IPv4 无网关（VPN 虚卡）
            Nic("eth1", gateways: []),      // 同样无网关——同分保持枚举序取先者
            Nic("eth0"),                    // 有网关——后出现但优先
        };
        Assert.Equal(2, NetworkInterfaceNetSource.SelectIndex(snapshots, null));

        var noGatewayAtAll = new[] { Nic("vpn0", gateways: []), Nic("eth1", gateways: []) };
        Assert.Equal(0, NetworkInterfaceNetSource.SelectIndex(noGatewayAtAll, null));
    }

    // —— Provider：差分与状态构建（假采样源，时间戳随样本走） ——

    private sealed class FakeNetSource(params MachineNetSample[] samples) : IMachineNetSource
    {
        private int _cursor;

        public MachineNetSample Sample(string? interfaceName)
        {
            if (_cursor >= samples.Length)
            {
                throw new InvalidOperationException("测试样本耗尽（多调了 Sample）");
            }
            return samples[_cursor++];
        }
    }

    private static MachineNetSample Sample(DateTimeOffset at, string name = "eth0", string ip = "192.168.1.5",
        long sent = 1_000, long received = 10_000, bool up = true)
        => new(at, name, ip, sent, received, up);

    private static WidgetConfig Widget() => new() { Id = "w1", Type = MachineWidgetDescriptors.NetType, ConnectionId = "machine-local" };

    private static ConnectionConfig Connection() => new() { Id = "machine-local", Type = "machine" };

    [Fact]
    public async Task FirstRefresh_NoBaseline_IpAndTotalsOnly()
    {
        var provider = new MachineNetProvider(new FakeNetSource(Sample(T0, sent: 100, received: 200)));
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("本机网络 · 192.168.1.5", state.Summary); // 无速率段
        Assert.Equal("100", state.Payload["total_sent"]);
        Assert.Equal("200", state.Payload["total_received"]);
        Assert.Equal("", state.Payload["up_bps"]);
    }

    [Fact]
    public async Task SecondRefresh_DeltaOverSeconds_ShowsSpeeds()
    {
        var provider = new MachineNetProvider(new FakeNetSource(
            Sample(T0, sent: 1_000, received: 10_000),
            Sample(T0.AddSeconds(15), sent: 16_000, received: 1_210_000)));
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("1000", state.Payload["up_bps"]);   // (16000-1000)/15 = 1000 B/s
        Assert.Equal("80000", state.Payload["down_bps"]); // (1210000-10000)/15 = 80000 B/s
        Assert.Equal("本机网络 · 192.168.1.5 · ↓78.1 KB/s ↑1000 B/s", state.Summary);
    }

    [Fact]
    public async Task Refresh_LessThanOneSecondGap_NoSpeedSpike()
    {
        var provider = new MachineNetProvider(new FakeNetSource(
            Sample(T0, sent: 0, received: 0),
            Sample(T0.AddMilliseconds(200), sent: 1_000_000, received: 2_000_000))); // 手动连刷：0.2s 内暴涨→尖峰假速率
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("", state.Payload["up_bps"]);
        Assert.DoesNotContain("↓", state.Summary);
    }

    [Fact]
    public async Task Refresh_CounterReset_RestartsBaselineWithoutSpeed()
    {
        var provider = new MachineNetProvider(new FakeNetSource(
            Sample(T0, sent: 5_000_000_000, received: 9_000_000_000),
            Sample(T0.AddSeconds(15), sent: 100, received: 200))); // 网卡重启计数回退
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("", state.Payload["up_bps"]);
        Assert.Equal("100", state.Payload["total_sent"]); // 基线已换成新计数
    }

    [Fact]
    public async Task Refresh_InterfaceSwitched_RestartsBaselineWithoutSpeed()
    {
        var provider = new MachineNetProvider(new FakeNetSource(
            Sample(T0, name: "eth0", sent: 1_000, received: 10_000),
            Sample(T0.AddSeconds(15), name: "Wi-Fi", sent: 999_999, received: 888_888))); // 换卡：计数不可比
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("", state.Payload["up_bps"]);
        Assert.Equal("Wi-Fi", state.Payload["interface"]);
    }

    [Fact]
    public async Task NoActiveNic_WarningState()
    {
        var provider = new MachineNetProvider(new FakeNetSource(Sample(T0, name: null, ip: null, up: false)));
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal("本机网络 · 无活动网卡", state.Summary);
        Assert.Equal("", state.Payload["ip"]);
    }

    [Fact]
    public async Task LabelPrefixesSummary()
    {
        var config = new WidgetConfig { Id = "w1", Type = MachineWidgetDescriptors.NetType, ConnectionId = "machine-local", Config = new Dictionary<string, string> { ["label"] = "工位机" } };
        var provider = new MachineNetProvider(new FakeNetSource(Sample(T0)));
        var state = await provider.GetStateAsync(config, Connection(), Ctx(), CancellationToken.None);
        Assert.Equal("工位机 · 192.168.1.5", state!.Summary);
    }

    [Fact]
    public async Task NamedNicMissing_ExceptionPropagates()
    {
        var source = new ThrowingSource();
        var provider = new MachineNetProvider(source);
        var config = new WidgetConfig { Id = "w1", Type = MachineWidgetDescriptors.NetType, ConnectionId = "machine-local", Config = new Dictionary<string, string> { ["interface"] = "eth9" } };
        await Assert.ThrowsAsync<ConnectionException>(() =>
            provider.GetStateAsync(config, Connection(), Ctx(), CancellationToken.None));
    }

    private sealed class ThrowingSource : IMachineNetSource
    {
        public MachineNetSample Sample(string? interfaceName)
            => throw new ConnectionException("找不到网卡「eth9」。", ConnectionHealthState.Degraded);
    }

    // —— 单位换算 ——

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(1023, "1023 B/s")]
    [InlineData(1024, "1.0 KB/s")]
    [InlineData(80_000, "78.1 KB/s")]
    [InlineData(2_500_000, "2.4 MB/s")]
    [InlineData(5_500_000_000, "5.1 GB/s")]
    [InlineData(9_000_000_000_000, "8.2 TB/s")]
    public void FormatSpeed_Units(double bytesPerSecond, string expected)
        => Assert.Equal(expected, MachineNetProvider.FormatSpeed(bytesPerSecond));

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(5_368_709_120, "5.0 GB")]
    [InlineData(12_000_000_000_000, "10.9 TB")]
    public void FormatBytes_Units(long bytes, string expected)
        => Assert.Equal(expected, MachineNetProvider.FormatBytes(bytes));

    [Fact]
    public void MachineConnection_AlwaysHealthy()
    {
        var result = new MachineConnectionProvider()
            .TestAsync(Connection(), Ctx(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.Equal(ConnectionHealthState.Healthy, result.Health);
    }

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    [Fact]
    public void Descriptor_Net_CarriesFieldsAndTier()
    {
        var descriptor = MachineWidgetDescriptors.Net;
        Assert.Equal("machine.net", descriptor.Type);
        Assert.True(descriptor.PinSupported);
        Assert.True(descriptor.FloatingOptIn);
        Assert.Equal(RefreshTiers.Machine, descriptor.SuggestedTier);
        Assert.Contains(descriptor.Fields, field => field.Key == "interface");
    }
}
