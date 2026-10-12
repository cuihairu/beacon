using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// machine 可选展示项（2026-10-12 用户令「本地组件可选展示项」）与按值行推算的 tile 宽度：
/// show 解析（缺省 = 默认全选 / 大小写不敏感去重按规范序 / 未知键 fail-loud / 选空 fail-loud /
/// ResolveOrAll 宽松退默认）、宽度模型（非 machine 维持 168 / local·net 默认宽逐字符可复算 /
/// 窄选择落 168 下限 / disk_path 加长算进宽 / 标签配置算进宽）。
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

    // —— show 解析 ——

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

    // —— tile 宽度（模型逐字符可复算：ASCII=7、CJK=11、空格=4、%=10、箭头/·=11、标签 ASCII=8/CJK=12，
    //    固定件 74（padding 20 + 灯 8 + icon 16 + 列间距 30）+ 取整余量 4，下限 168） ——

    [Fact]
    public void ValueTextWidth_ModelPerCharacter()
        => Assert.Equal(56, MachineTileSizing.ValueTextWidthDips("CPU 100%")); // 21+4+(3×7+10)

    [Fact]
    public void TileWidth_NonMachine_KeepsLegacyFloor()
        => Assert.Equal(168, MachineTileSizing.TileWidthDips(Widget("http.status")));

    [Fact]
    public void TileWidth_LocalDefault_FitsWorstValueLine()
    {
        // 值行 = "CPU 100% · 内存 100% · C: 100%" = 56+19+57+19+49 = 200；标签「本机」= 24
        Assert.Equal(302, MachineTileSizing.TileWidthDips(Widget(MachineWidgetDescriptors.LocalType)));
    }

    [Fact]
    public void TileWidth_NetDefault_RatesFormIsTheWorst()
    {
        // 值行形态二选一取宽者：速率行 "↓999.9 MB/s ↑999.9 MB/s" = 78+4+78 = 160（IP 行 105 不并排出现）
        Assert.Equal(262, MachineTileSizing.TileWidthDips(Widget(MachineWidgetDescriptors.NetType)));
    }

    [Fact]
    public void TileWidth_NarrowSelection_LandsOnFloor()
    {
        // 只留 CPU：「CPU 100%」= 56，74+4+24+56 = 158 < 168 → 下限兜底
        Assert.Equal(168, MachineTileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.LocalType, ("show", "cpu_percent"))));
    }

    [Fact]
    public void TileWidth_LongDiskPath_CountsIntoWidth()
    {
        // disk_path 进磁盘段前缀（与 Provider 的 diskHead 同构）："/mnt/data" = 9×7 = 63
        Assert.Equal(351, MachineTileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.LocalType, ("disk_path", "/mnt/data"))));
    }

    [Fact]
    public void TileWidth_LabelConfig_CountsIntoWidth()
    {
        // 标签配置进宽（标签列不再挨裁）：「工位机」= 3×12 = 36
        Assert.Equal(314, MachineTileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.LocalType, ("label", "工位机"))));
    }

    [Fact]
    public void TileWidth_NetIpOnly_FitsAddress()
    {
        Assert.Equal(207, MachineTileSizing.TileWidthDips(Widget(
            MachineWidgetDescriptors.NetType, ("show", "ip"))));
    }
}
