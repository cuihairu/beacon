using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// machine 可选展示项（2026-10-12 用户令「本地组件可选展示项」）：
/// show 解析（缺省 = 默认全选 / 大小写不敏感去重按规范序 / 未知键 fail-loud / 选空 fail-loud /
/// ResolveOrAll 宽松退默认）。tile 宽度测试在 TileSizingTests（全类型同一路径）。
/// </summary>
public sealed class MachineShowOptionsTests
{
    private static WidgetConfig Widget(string type, params (string Key, string Value)[] config)
    {
        var widget = new WidgetConfig { Id = "w1", Type = type, ConnectionId = "machine-local" };
        foreach (var (key, value) in config)
        {
            widget.Config[key] = value;
        }
        return widget;
    }

    [Fact]
    public void Resolve_MissingOrBlank_DefaultsToAll()
    {
        Assert.Equal(MachineShowOptions.LocalKeys, MachineShowOptions.Resolve(MachineWidgetDescriptors.LocalType, Widget(MachineWidgetDescriptors.LocalType)));
        Assert.Equal(MachineShowOptions.NetKeys, MachineShowOptions.Resolve(MachineWidgetDescriptors.NetType, Widget(MachineWidgetDescriptors.NetType)));
        Assert.Equal(MachineShowOptions.NetKeys, MachineShowOptions.Resolve(MachineWidgetDescriptors.NetType, Widget(MachineWidgetDescriptors.NetType, ("show", "   "))));
    }

    [Fact]
    public void Resolve_CaseInsensitiveDedupedCanonicalOrder()
    {
        // 大小写不敏感、去重、输出按规范序（与书写顺序无关）
        var resolved = MachineShowOptions.Resolve(MachineWidgetDescriptors.LocalType,
            Widget(MachineWidgetDescriptors.LocalType, ("show", " Disk_Percent, cpu_percent, CPU_PERCENT ")));
        Assert.Equal(["cpu_percent", "disk_percent"], resolved);
    }

    [Fact]
    public void Resolve_UnknownKey_FailsLoudDegraded()
    {
        var exception = Assert.Throws<ConnectionException>(() => MachineShowOptions.Resolve(
            MachineWidgetDescriptors.NetType, Widget(MachineWidgetDescriptors.NetType, ("show", "down_bps, gpu_load"))));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health); // 配置错误不静默（与阈值校验同口径）
        Assert.Contains("gpu_load", exception.Message);
        Assert.Contains("down_bps / up_bps / ip", exception.Message);
    }

    [Fact]
    public void Resolve_EmptySelection_FailsLoud()
    {
        // ",,," 清掉空段后一项不剩——明确报错而不是静默全显
        var exception = Assert.Throws<ConnectionException>(() => MachineShowOptions.Resolve(
            MachineWidgetDescriptors.LocalType, Widget(MachineWidgetDescriptors.LocalType, ("show", ",,,"))));
        Assert.Contains("至少要选一项", exception.Message);
    }

    [Fact]
    public void ResolveOrAll_InvalidConfig_FallsBackToDefaults()
    {
        // sizing 在 UI 侧不能炸：未知键退默认全选（全选是最宽口径，按它算宽恒安全）
        Assert.Equal(MachineShowOptions.NetKeys, MachineShowOptions.ResolveOrAll(
            MachineWidgetDescriptors.NetType, Widget(MachineWidgetDescriptors.NetType, ("show", "nonsense"))));
    }
}
