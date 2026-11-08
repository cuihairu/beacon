using System.Net;
using System.Net.NetworkInformation;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

public sealed class MachineNetProviderSelectionTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static NicSnapshot Nic(
        string name,
        OperationalStatus status = OperationalStatus.Up,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet,
        string[]? unicast = null,
        string[]? gateways = null)
        => new(
            name,
            status,
            type,
            [.. (unicast ?? ["192.168.1.5"]).Select(IPAddress.Parse)],
            [.. (gateways ?? ["192.168.1.1"]).Select(IPAddress.Parse)]);

    [Fact]
    public void SelectIndex_ReturnsNoNicWhenOnlyLoopbackIsUp()
    {
        var snapshots = new[] { Nic("lo", type: NetworkInterfaceType.Loopback, gateways: []) };

        Assert.Equal(-1, NetworkInterfaceNetSource.SelectIndex(snapshots, null));
    }

    [Fact]
    public void SelectIndex_Auto_DoesNotReplaceNoGatewayCardWithAnotherNoGatewayCard()
    {
        var snapshots = new[]
        {
            Nic("first", gateways: []),
            Nic("second", gateways: []),
        };

        Assert.Equal(0, NetworkInterfaceNetSource.SelectIndex(snapshots, null));
    }

    [Fact]
    public async Task Provider_IgnoresSpeedWhenSampleTimeIsNotNewer()
    {
        var provider = new MachineNetProvider(new FixedSource(
            Sample(Base, sent: 1_000, received: 10_000),
            Sample(Base, sent: 9_000, received: 90_000)));
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("", state!.Payload["up_bps"]);
        Assert.Equal("", state.Payload["down_bps"]);
        Assert.Equal("9000", state.Payload["total_sent"]);
    }

    [Fact]
    public async Task Provider_AllSelections_InCanonicalSummaryOrder()
    {
        var provider = new MachineNetProvider(new FixedSource(
            Sample(Base, sent: 1_000, received: 10_000),
            Sample(Base.AddSeconds(10), sent: 21_000, received: 210_000)));
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        var state = await provider.GetStateAsync(Widget(("show", "up_bps, ip, down_bps")), Connection(), Ctx(), CancellationToken.None);

        Assert.Equal("本机网络 · 192.168.1.5 · ↓19.5 KB/s ↑2.0 KB/s", state!.Summary);
        Assert.Equal("192.168.1.5 · ↓19.5 KB/s ↑2.0 KB/s", state.Payload["value_text"]);
    }

    private static MachineNetSample Sample(
        DateTimeOffset at,
        string name = "eth0",
        string ip = "192.168.1.5",
        long sent = 1_000,
        long received = 10_000,
        bool up = true)
        => new(at, name, ip, sent, received, up);

    private static WidgetConfig Widget(params (string Key, string Value)[] config)
    {
        var widget = new WidgetConfig
        {
            Id = "w-net",
            Type = MachineWidgetDescriptors.NetType,
            ConnectionId = "machine-local",
        };
        foreach (var (key, value) in config)
        {
            widget.Config[key] = value;
        }
        return widget;
    }

    private static ConnectionConfig Connection() => new()
    {
        Id = "machine-local",
        Type = "machine",
        Settings = [],
    };

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    private sealed class FixedSource(params MachineNetSample[] samples) : IMachineNetSource
    {
        private int cursor;

        public MachineNetSample Sample(string? interfaceName)
        {
            if (cursor >= samples.Length)
            {
                throw new InvalidOperationException("No more test samples");
            }
            return samples[cursor++];
        }
    }
}
