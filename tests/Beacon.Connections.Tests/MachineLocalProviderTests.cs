using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections.Tests;

/// <summary>
/// machine.local（本机资源，批3-补32 拍板回执「cpu 内存 磁盘空间」）：
/// /proc 解析（stat 首行 idle 口径 / meminfo MemAvailable 与 MemFree 兜底）、阈值口径（默认 90/越界 fail-loud/
/// 四舍五入后比线所见即所警）、CPU 差分（首刷无基线/计数回退重记基线）、超线亮黄、零容量 fail-loud、
/// 真实 /proc 与 DriveInfo 集成（Linux 跑，windows-latest 跳过）。
/// </summary>
public sealed class MachineLocalProviderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    // —— /proc 解析（纯函数，字符串进字符串出） ——

    [Fact]
    public void ParseProcStat_IdlePlusIowait_GarbageThrows()
    {
        var (idle, total) = MachineResourceSource.ParseProcStat("cpu  1 2 3 4 5 6 7 8 9 10");
        Assert.Equal(9UL, idle);   // idle(4) + iowait(5) 记闲
        Assert.Equal(36UL, total); // user..steal 前 8 段合计

        (idle, total) = MachineResourceSource.ParseProcStat("cpu  100 0 200 700 0 0 0 0");
        Assert.Equal(700UL, idle);
        Assert.Equal(1000UL, total);

        Assert.Throws<ConnectionException>(() => MachineResourceSource.ParseProcStat("cpu0 1 2")); // 单核行不冒充合计
        Assert.Throws<ConnectionException>(() => MachineResourceSource.ParseProcStat("cpu  1 2")); // 段数不足
    }

    [Fact]
    public void ParseProcMeminfo_AvailablePreferred_MemFreeFallback()
    {
        const string withAvailable = """
            MemTotal:        16384000 kB
            MemFree:          2048000 kB
            MemAvailable:     8192000 kB
            HugePages_Total:       0
            """;
        var (total, available) = MachineResourceSource.ParseProcMeminfo(withAvailable);
        Assert.Equal(16_384_000UL * 1024, total);
        Assert.Equal(8_192_000UL * 1024, available); // MemAvailable 优先（含可回收缓存口径）

        var (fallbackTotal, fallbackAvailable) = MachineResourceSource.ParseProcMeminfo("MemTotal: 1000 kB\nMemFree: 400 kB\n");
        Assert.Equal(1000UL * 1024, fallbackTotal);
        Assert.Equal(400UL * 1024, fallbackAvailable); // 老内核缺 MemAvailable 用 MemFree 兜底

        Assert.Throws<ConnectionException>(() => MachineResourceSource.ParseProcMeminfo("SwapTotal: 0 kB\n"));
    }

    // —— 阈值口径 ——

    [Fact]
    public void Threshold_DefaultCustomAndInvalid()
    {
        Assert.Equal(90d, MachineLocalProvider.Threshold(Widget(), "cpu_threshold")); // 空 = 默认 90

        var custom = Widget(("cpu_threshold", " 85.5 "));
        Assert.Equal(85.5, MachineLocalProvider.Threshold(custom, "cpu_threshold"));

        foreach (var bad in new[] { "abc", "0", "101", "-5" })
        {
            var widget = Widget(("mem_threshold", bad));
            var exception = Assert.Throws<ConnectionException>(() => MachineLocalProvider.Threshold(widget, "mem_threshold"));
            Assert.Equal(ConnectionHealthState.Degraded, exception.Health); // 配置错误 fail-loud，不静默回默认
        }
    }

    // —— Provider：CPU 差分与状态构建（假采样源，时间戳随样本走） ——

    private sealed class FakeSource(params MachineResourceSample[] samples) : IMachineResourceSource
    {
        private int _cursor;

        public MachineResourceSample Sample(string? diskPath)
        {
            if (_cursor >= samples.Length)
            {
                throw new InvalidOperationException("测试样本耗尽（多调了 Sample）");
            }
            return samples[_cursor++];
        }
    }

    private static MachineResourceSample Sample(DateTimeOffset at, ulong idle = 0, ulong total = 0,
        ulong memTotal = 16_000_000_000, ulong memAvailable = 8_000_000_000,
        string diskName = @"C:\", long diskFree = 300_000_000_000, long diskTotal = 1_000_000_000_000)
        => new(at, idle, total, memTotal, memAvailable, diskName, diskFree, diskTotal);

    private static WidgetConfig Widget(params (string Key, string Value)[] config)
    {
        var widget = new WidgetConfig { Id = "w1", Type = MachineWidgetDescriptors.LocalType, ConnectionId = "machine-local" };
        foreach (var (key, value) in config)
        {
            widget.Config[key] = value;
        }
        return widget;
    }

    private static ConnectionConfig Connection() => new() { Id = "machine-local", Type = "machine" };

    private static ConnectionContext Ctx() => new() { Secrets = new SecretStoreStub() };

    [Fact]
    public async Task FirstRefresh_NoCpuBaseline_MemDiskOnly()
    {
        var provider = new MachineLocalProvider(new FakeSource(Sample(T0)));
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(Severity.Info, state.Severity);
        Assert.Equal(LifecycleState.Success, state.Lifecycle);
        Assert.Equal("本机资源 · 内存 50% · C: 70%", state.Summary); // 无 CPU 段（基线未建）
        Assert.Equal("", state.Payload["cpu_percent"]);
        Assert.Equal("50", state.Payload["mem_percent"]);
        Assert.Equal("70", state.Payload["disk_percent"]);
        Assert.Equal(@"C:\", state.Payload["disk"]); // payload 存原始盘名；摘要才裁尾分隔符
    }

    [Fact]
    public async Task SecondRefresh_ComputesCpuFromTickDelta()
    {
        var provider = new MachineLocalProvider(new FakeSource(
            Sample(T0, idle: 700, total: 1000),
            Sample(T0.AddSeconds(15), idle: 760, total: 1100))); // busy = 100 - 60 = 40
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = (await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None))!;

        Assert.Equal("40", state.Payload["cpu_percent"]);
        Assert.Equal("本机资源 · CPU 40% · 内存 50% · C: 70%", state.Summary);
    }

    [Fact]
    public async Task Refresh_CounterRegression_RebaselinesWithoutCpu()
    {
        var provider = new MachineLocalProvider(new FakeSource(
            Sample(T0, idle: 700, total: 1000),
            Sample(T0.AddSeconds(15), idle: 0, total: 0))); // 重启后计数回退
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = (await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None))!;

        Assert.Equal("", state.Payload["cpu_percent"]); // 不出 CPU 不甩错，静默换基线
        Assert.DoesNotContain("CPU", state.Summary);
    }

    [Fact]
    public async Task OverThreshold_Warns()
    {
        // 内存 97%（可用 30 万 / 总 1000 万）超默认 90 → 黄
        var provider = new MachineLocalProvider(new FakeSource(Sample(T0, memTotal: 10_000_000, memAvailable: 300_000)));
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Warning, state!.Severity);
        Assert.Equal(LifecycleState.Running, state.Lifecycle);
        Assert.Equal("本机资源 · 内存 97% · C: 70%", state.Summary);

        // 磁盘 95%（剩 5G / 总 100G）超默认 90 → 黄（多项超线同为黄，Signal 不分级）
        var diskProvider = new MachineLocalProvider(new FakeSource(Sample(T0, diskFree: 5_000_000_000, diskTotal: 100_000_000_000)));
        var diskState = await diskProvider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        Assert.Equal(Severity.Warning, diskState!.Severity);
        Assert.Equal("本机资源 · 内存 50% · C: 95%", diskState.Summary);
    }

    [Fact]
    public async Task Threshold_BoundaryOnRoundedPercent()
    {
        // busy 894/1000 = 89.4% → 显示 89 → 不超 90
        var below = new MachineLocalProvider(new FakeSource(
            Sample(T0), Sample(T0.AddSeconds(15), idle: 106, total: 1000)));
        await below.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var belowState = (await below.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None))!;
        Assert.Equal(Severity.Info, belowState.Severity);
        Assert.Equal("89", belowState.Payload["cpu_percent"]);

        // busy 896/1000 = 89.6% → 显示 90（四舍五入）→ 超线亮黄：所见即所警
        var at = new MachineLocalProvider(new FakeSource(
            Sample(T0), Sample(T0.AddSeconds(15), idle: 104, total: 1000)));
        await at.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = (await at.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None))!;
        Assert.Equal(Severity.Warning, state.Severity);
        Assert.Equal("90", state.Payload["cpu_percent"]);
    }

    [Fact]
    public async Task ConfiguredThreshold_TakesEffect()
    {
        var provider = new MachineLocalProvider(new FakeSource(
            Sample(T0, idle: 700, total: 1000),
            Sample(T0.AddSeconds(15), idle: 760, total: 1100),  // CPU 40%
            Sample(T0.AddSeconds(30), idle: 860, total: 1250))); // CPU 50%
        var strict = Widget(("cpu_threshold", "20"));
        var lax = Widget(("cpu_threshold", "60"));
        await provider.GetStateAsync(strict, Connection(), Ctx(), CancellationToken.None); // 建基线
        var strictState = (await provider.GetStateAsync(strict, Connection(), Ctx(), CancellationToken.None))!; // 40% ≥ 20
        var laxState = (await provider.GetStateAsync(lax, Connection(), Ctx(), CancellationToken.None))!;       // 50% < 60
        Assert.Equal(Severity.Warning, strictState.Severity);
        Assert.Equal(Severity.Info, laxState.Severity);
    }

    [Fact]
    public async Task ZeroTotals_FailLoud()
    {
        var noMemory = new MachineLocalProvider(new FakeSource(Sample(T0, memTotal: 0)));
        var memoryException = await Assert.ThrowsAsync<ConnectionException>(() =>
            noMemory.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));
        Assert.Equal(ConnectionHealthState.Degraded, memoryException.Health);

        var noDisk = new MachineLocalProvider(new FakeSource(Sample(T0, diskTotal: 0)));
        var diskException = await Assert.ThrowsAsync<ConnectionException>(() =>
            noDisk.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None));
        Assert.Equal(ConnectionHealthState.Degraded, diskException.Health);
    }

    [Fact]
    public async Task Payload_CarriesRawNumbers()
    {
        var provider = new MachineLocalProvider(new FakeSource(
            Sample(T0, idle: 700, total: 1000, memTotal: 16_000_000_000, memAvailable: 4_000_000_000,
                diskFree: 250_000_000_000, diskTotal: 1_000_000_000_000),
            Sample(T0.AddSeconds(15), idle: 760, total: 1100, memTotal: 16_000_000_000, memAvailable: 4_000_000_000,
                diskFree: 250_000_000_000, diskTotal: 1_000_000_000_000)));
        await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);
        var state = (await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None))!;

        Assert.Equal("40", state.Payload["cpu_percent"]);
        Assert.Equal("75", state.Payload["mem_percent"]);
        Assert.Equal("12000000000", state.Payload["mem_used"]); // 原始字节（invariant 无分组符）
        Assert.Equal("16000000000", state.Payload["mem_total"]);
        Assert.Equal("250000000000", state.Payload["disk_free"]);
        Assert.Equal("1000000000000", state.Payload["disk_total"]);
        Assert.Equal(@"C:\", state.Payload["disk"]);
    }

    [Fact]
    public void Descriptor_CarriesFieldsAndTier()
    {
        var descriptor = MachineWidgetDescriptors.Local;
        Assert.Equal("machine.local", descriptor.Type);
        Assert.True(descriptor.PinSupported);
        Assert.True(descriptor.FloatingOptIn);
        Assert.Equal(RefreshTiers.Machine, descriptor.SuggestedTier); // 资源口随 machine 档 15s
        Assert.Contains(descriptor.Fields, field => field.Key == "disk_path");
        Assert.Contains(descriptor.Fields, field => field.Key == "cpu_threshold");
        Assert.Contains(MachineWidgetDescriptors.All, item => item.Type == MachineWidgetDescriptors.LocalType);
    }

    // —— 真实采样链路（Linux 跑 /proc + DriveInfo；windows-latest 无 /proc，跳过） ——

    [Fact]
    public void MachineResourceSource_RealProc_OnLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var sample = new MachineResourceSource().Sample(null);
        Assert.True(sample.CpuTotalTicks > 0); // 开机以来累计 jiffies
        Assert.True(sample.MemoryTotalBytes > 0);
        Assert.Equal("/", sample.DiskName); // 默认根分区
        Assert.True(sample.DiskTotalBytes > 0);
        Assert.True(sample.MemoryAvailableBytes < sample.MemoryTotalBytes);
    }

    [Fact]
    public void ResolveDrive_DefaultNamedAndInvalid_OnLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        Assert.Equal("/", MachineResourceSource.ResolveDrive(null).Name); // 空 = 根分区
        Assert.True(MachineResourceSource.ResolveDrive("/tmp").TotalSize > 0);
        Assert.Throws<ConnectionException>(() => MachineResourceSource.ResolveDrive("/no-such-mount-beacon")); // 点名不存在 fail-loud
        Assert.Throws<ConnectionException>(() => MachineResourceSource.ResolveDrive("data")); // 相对路径不是盘/挂载点
    }

    // —— 可选展示项（2026-10-12 用户令）：未选中的键不出 payload/Summary/数值位，也不参与超线判黄 ——

    [Fact]
    public async Task Show_CpuOnly_MemDiskAbsentAndNotGatingSeverity()
    {
        // 内存 97% 超默认阈值，但未选中——不亮黄、不进 payload/Summary/数值位
        var provider = new MachineLocalProvider(new FakeSource(
            Sample(T0, idle: 700, total: 1000, memTotal: 10_000_000, memAvailable: 300_000),
            Sample(T0.AddSeconds(15), idle: 760, total: 1100, memTotal: 10_000_000, memAvailable: 300_000)));
        await provider.GetStateAsync(Widget(("show", "cpu_percent")), Connection(), Ctx(), CancellationToken.None);
        var state = (await provider.GetStateAsync(Widget(("show", "cpu_percent")), Connection(), Ctx(), CancellationToken.None))!;

        Assert.Equal(Severity.Info, state.Severity); // 未选中项不参与超线判黄
        Assert.Equal("本机资源 · CPU 40%", state.Summary);
        Assert.Equal("CPU 40%", state.Payload["value_text"]); // 数值位与 Summary 段同源
        Assert.Equal("40", state.Payload["cpu_percent"]);
        Assert.False(state.Payload.ContainsKey("mem_percent")); // 未选中不占位
        Assert.False(state.Payload.ContainsKey("disk_percent"));
        Assert.True(state.Payload.ContainsKey("mem_used")); // 原始台账键不参与选择，恒留详情页
        Assert.True(state.Payload.ContainsKey("disk"));
    }

    [Fact]
    public async Task Show_CpuOnly_FirstRefresh_ValueTextDash()
    {
        // 只选 CPU 且首刷无基线：无段可拼 → Summary 退 head、数值位占位符
        var provider = new MachineLocalProvider(new FakeSource(Sample(T0)));
        var state = await provider.GetStateAsync(Widget(("show", "cpu_percent")), Connection(), Ctx(), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal("本机资源", state.Summary);
        Assert.Equal("—", state.Payload["value_text"]);
        Assert.Equal("", state.Payload["cpu_percent"]);
    }

    [Fact]
    public async Task Show_Default_ValueTextMatchesSummarySegments()
    {
        var provider = new MachineLocalProvider(new FakeSource(Sample(T0)));
        var state = await provider.GetStateAsync(Widget(), Connection(), Ctx(), CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal("内存 50% · C: 70%", state.Payload["value_text"]); // = Summary 去头
        Assert.Equal("本机资源 · 内存 50% · C: 70%", state.Summary);
        Assert.True(state.Payload.ContainsKey("cpu_percent")); // 默认全选
    }

    [Fact]
    public async Task Show_UnknownKey_FailsLoudDegraded()
    {
        var provider = new MachineLocalProvider(new FakeSource(Sample(T0)));
        var exception = await Assert.ThrowsAsync<ConnectionException>(() =>
            provider.GetStateAsync(Widget(("show", "mem_percent, nope")), Connection(), Ctx(), CancellationToken.None));
        Assert.Equal(ConnectionHealthState.Degraded, exception.Health);
    }
}
