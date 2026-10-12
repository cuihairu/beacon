using System.Globalization;
using System.Runtime.InteropServices;
using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Connections;

/// <summary>
/// 本机资源采样快照：CPU 是自开机累计 tick（idle/total，速率口径同 machine.net 由 Provider 差分），
/// 内存与磁盘是即时值；时间戳随样本走（源注入时间，Provider 不持时钟——测试确定性的关键）。
/// </summary>
public readonly record struct MachineResourceSample(
    DateTimeOffset At,
    ulong CpuIdleTicks,
    ulong CpuTotalTicks,
    ulong MemoryTotalBytes,
    ulong MemoryAvailableBytes,
    string? DiskName,
    long DiskFreeBytes,
    long DiskTotalBytes);

/// <summary>采样抽象：生产按 OS 取数（Windows API / /proc），测试注入假样本（Linux 全程可测，不碰真实硬件计数）。</summary>
public interface IMachineResourceSource
{
    MachineResourceSample Sample(string? diskPath);
}

/// <summary>
/// 真实采样：CPU——Linux 读 /proc/stat 首行累计 jiffies（idle+iowait 记闲），Windows 走 GetSystemTimes
/// （kernel 段已含 idle）；内存——Linux 读 /proc/meminfo MemTotal/MemAvailable，Windows 走
/// GlobalMemoryStatusEx；磁盘统一 DriveInfo（跨平台）。默认盘：Windows 取系统盘，其余取 /。
/// 点名盘不存在/未就绪 fail-loud（与 machine.net 点名网卡同口径）。
/// </summary>
public sealed class MachineResourceSource : IMachineResourceSource
{
    public MachineResourceSample Sample(string? diskPath)
    {
        var (idle, total) = ReadCpu();
        var (memoryTotal, memoryAvailable) = ReadMemory();
        var drive = ResolveDrive(diskPath);
        return new MachineResourceSample(
            DateTimeOffset.UtcNow,
            idle,
            total,
            memoryTotal,
            memoryAvailable,
            drive.Name,
            drive.AvailableFreeSpace,
            drive.TotalSize);
    }

    /// <summary>解析磁盘：空 = 系统盘（Windows）/ 根分区；点名不存在、非根路径、未就绪都按 Degraded 甩错。</summary>
    internal static DriveInfo ResolveDrive(string? diskPath)
    {
        var path = diskPath?.Trim();
        DriveInfo drive;
        if (string.IsNullOrEmpty(path))
        {
            drive = new DriveInfo(OperatingSystem.IsWindows() ? Environment.SystemDirectory : "/");
        }
        else
        {
            try
            {
                drive = new DriveInfo(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                throw new ConnectionException($"磁盘路径「{path}」不是盘符或根路径（如 C:\\ 或 /mnt/data）。", ConnectionHealthState.Degraded);
            }
        }
        if (!drive.IsReady)
        {
            throw new ConnectionException($"磁盘「{drive.Name}」不存在或未就绪。", ConnectionHealthState.Degraded);
        }
        return drive;
    }

    private static (ulong Idle, ulong Total) ReadCpu()
        => OperatingSystem.IsWindows() ? ReadWindowsCpu() : ParseProcStat(ProcStatLine());

    private static (ulong Total, ulong Available) ReadMemory()
        => OperatingSystem.IsWindows() ? ReadWindowsMemory() : ParseProcMeminfo(File.ReadAllText("/proc/meminfo"));

    private static string ProcStatLine()
    {
        // 首行 "cpu  …" 即全核合计；文件恒在，缺行视为环境异常（fail-loud 不静默）
        var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
        return line ?? throw new ConnectionException("/proc/stat 缺 cpu 合计行（环境异常）。", ConnectionHealthState.Degraded);
    }

    /// <summary>解析 /proc/stat 首行：`cpu  user nice system idle iowait irq softirq steal …`（jiffies）；idle = idle+iowait。</summary>
    internal static (ulong Idle, ulong Total) ParseProcStat(string line)
    {
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // fields[0] = "cpu"；口径取前 8 段（user..steal），guest 已含在 user 里不重算
        if (fields.Length < 9 || fields[0] != "cpu")
        {
            throw new ConnectionException("/proc/stat cpu 行格式不符（段数不足）。", ConnectionHealthState.Degraded);
        }
        var ticks = fields[1..9].Select(field => ulong.Parse(field, CultureInfo.InvariantCulture)).ToArray();
        ulong total = 0;
        foreach (var tick in ticks)
        {
            total += tick; // Enumerable.Sum 无 ulong 重载，手写累加
        }
        return (ticks[3] + ticks[4], total);
    }

    /// <summary>解析 /proc/meminfo 的 MemTotal / MemAvailable（kB 单位 → 字节；老内核缺 MemAvailable 用 MemFree 兜底）。</summary>
    internal static (ulong Total, ulong Available) ParseProcMeminfo(string content)
    {
        ulong total = 0;
        ulong available = 0;
        ulong free = 0;
        foreach (var line in content.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            var value = ParseKilobytes(line[(colon + 1)..]);
            switch (line[..colon])
            {
                case "MemTotal": total = value; break;
                case "MemAvailable": available = value; break;
                case "MemFree": free = value; break;
            }
        }
        if (total == 0)
        {
            throw new ConnectionException("/proc/meminfo 缺 MemTotal（环境异常）。", ConnectionHealthState.Degraded);
        }
        return (total * 1024, (available > 0 ? available : free) * 1024);
    }

    private static ulong ParseKilobytes(string text)
    {
        var fields = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 0 && ulong.TryParse(fields[0], CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    // —— Windows 侧（P/Invoke 声明跨平台可编译，仅 Windows 运行路径触达） ——

    private static (ulong Idle, ulong Total) ReadWindowsCpu()
    {
        // GetSystemTimes：kernel 段已含 idle，total = kernel + user
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            throw new ConnectionException("GetSystemTimes 采样失败。", ConnectionHealthState.Degraded);
        }
        return (idle, kernel + user);
    }

    private static (ulong Total, ulong Available) ReadWindowsMemory()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new ConnectionException("GlobalMemoryStatusEx 采样失败。", ConnectionHealthState.Degraded);
        }
        return (status.ullTotalPhys, status.ullAvailPhys);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}

/// <summary>
/// machine.local（本机资源，批3-补32 拍板回执「本机的cpu 内存 磁盘空间 这个倒是一个低成本的显示」）：
/// CPU/内存/磁盘三数一行。CPU = 相邻两次累计 tick 差分（首刷无基线不出 CPU，同速率口径；计数回退重记基线）；
/// 内存/磁盘即时占比。阈值超线亮黄（Signal 不是 Dashboard：黄灯提醒动手，不做分级红）——阈值所见即所得，
/// 按展示用的四舍五入整数比线。三数进 payload 走 L3，32 DIP 一行只放三个百分比。连接复用 machine（零凭据）。
/// </summary>
public sealed class MachineLocalProvider : IWidgetProvider
{
    internal const double DefaultThresholdPercent = 90;

    private readonly IMachineResourceSource _source;
    private readonly object _gate = new();
    private readonly Dictionary<string, MachineResourceSample> _last = new(StringComparer.Ordinal); // widget.Id → CPU 基线

    public MachineLocalProvider() : this(new MachineResourceSource()) { }

    public MachineLocalProvider(IMachineResourceSource source) => _source = source;

    public WidgetTypeDescriptor Descriptor => MachineWidgetDescriptors.Local;

    public Task<WidgetState?> GetStateAsync(
        WidgetConfig widget,
        ConnectionConfig connection,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var cpuThreshold = Threshold(widget, "cpu_threshold");
        var memThreshold = Threshold(widget, "mem_threshold");
        var diskThreshold = Threshold(widget, "disk_threshold");
        // 可选展示项（2026-10-12 用户令）：未选中的键不出 payload/Summary/数值位，阈值校验仍全量
        var show = new HashSet<string>(MachineShowOptions.Resolve(Descriptor.Type, widget), StringComparer.Ordinal);
        var sample = _source.Sample(widget.Config.GetValueOrDefault("disk_path"));

        if (sample.MemoryTotalBytes == 0)
        {
            throw new ConnectionException("内存总量采样为 0（源异常）。", ConnectionHealthState.Degraded);
        }
        if (string.IsNullOrEmpty(sample.DiskName) || sample.DiskTotalBytes <= 0)
        {
            throw new ConnectionException("磁盘不可用或容量为 0。", ConnectionHealthState.Degraded);
        }

        double? cpuPercent = null;
        lock (_gate)
        {
            // 首刷无基线不出 CPU；total 回退/原地不动（重启瞬间）不出 CPU 只换基线
            if (_last.TryGetValue(widget.Id, out var baseline)
                && sample.At > baseline.At
                && sample.CpuTotalTicks > baseline.CpuTotalTicks
                && sample.CpuIdleTicks >= baseline.CpuIdleTicks)
            {
                var totalDelta = sample.CpuTotalTicks - baseline.CpuTotalTicks;
                var busyDelta = totalDelta - (sample.CpuIdleTicks - baseline.CpuIdleTicks);
                cpuPercent = Math.Clamp(100.0 * busyDelta / totalDelta, 0, 100);
            }
            _last[widget.Id] = sample;
        }

        // 阈值与摘要都用四舍五入后的整数：所见即所警（89.6 显示 90 就按 90 比线，不搞显示绿灯超线的错位）
        double? cpuShown = cpuPercent is { } cpu ? Math.Round(cpu) : null;
        var memShown = Math.Round(100.0 * (sample.MemoryTotalBytes - Math.Min(sample.MemoryAvailableBytes, sample.MemoryTotalBytes)) / sample.MemoryTotalBytes);
        var diskShown = Math.Round(100.0 * (sample.DiskTotalBytes - Math.Min(sample.DiskFreeBytes, sample.DiskTotalBytes)) / sample.DiskTotalBytes);

        var severity = (show.Contains("cpu_percent") && cpuShown >= cpuThreshold)
            || (show.Contains("mem_percent") && memShown >= memThreshold)
            || (show.Contains("disk_percent") && diskShown >= diskThreshold)
            ? Severity.Warning
            : Severity.Info;
        var head = widget.Config.GetValueOrDefault("label") is { Length: > 0 } label ? label : "本机资源";
        var diskHead = sample.DiskName == "/" ? "/" : sample.DiskName!.TrimEnd('\\', '/'); // C:\ → C:；根分区保持 /
        var segments = new List<string>();
        if (show.Contains("cpu_percent") && cpuShown is { } shownCpu)
        {
            segments.Add($"CPU {shownCpu.ToString("0", CultureInfo.InvariantCulture)}%");
        }
        if (show.Contains("mem_percent"))
        {
            segments.Add($"内存 {memShown.ToString("0", CultureInfo.InvariantCulture)}%");
        }
        if (show.Contains("disk_percent"))
        {
            segments.Add($"{diskHead} {diskShown.ToString("0", CultureInfo.InvariantCulture)}%");
        }
        // 数值位短文本（WidgetValueHint 首优先键）：与 Summary 段同源——所选段拼行，三处口径一致的根
        var valueText = string.Join(" · ", segments);

        var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mem_used"] = (sample.MemoryTotalBytes - sample.MemoryAvailableBytes).ToString(CultureInfo.InvariantCulture),
            ["mem_total"] = sample.MemoryTotalBytes.ToString(CultureInfo.InvariantCulture),
            ["disk_free"] = sample.DiskFreeBytes.ToString(CultureInfo.InvariantCulture),
            ["disk_total"] = sample.DiskTotalBytes.ToString(CultureInfo.InvariantCulture),
            ["disk"] = sample.DiskName,
            ["value_text"] = segments.Count > 0 ? valueText : "—",
        };
        if (show.Contains("cpu_percent"))
        {
            payload["cpu_percent"] = cpuShown?.ToString("0", CultureInfo.InvariantCulture) ?? "";
        }
        if (show.Contains("mem_percent"))
        {
            payload["mem_percent"] = memShown.ToString("0", CultureInfo.InvariantCulture);
        }
        if (show.Contains("disk_percent"))
        {
            payload["disk_percent"] = diskShown.ToString("0", CultureInfo.InvariantCulture);
        }

        return Task.FromResult<WidgetState?>(new WidgetState
        {
            WidgetId = widget.Id,
            WidgetType = Descriptor.Type,
            ConnectionId = connection.Id,
            Severity = severity,
            Lifecycle = severity == Severity.Warning ? LifecycleState.Running : LifecycleState.Success,
            Progress = null,
            Summary = segments.Count > 0 ? $"{head} · {valueText}" : head,
            DetailUrl = null,
            Payload = payload,
            FetchedAt = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>阈值口径：空 = 默认 90；非数字或越界 1-100 按配置错误 fail-loud（与 herald status 筛选同口径）。</summary>
    internal static double Threshold(WidgetConfig widget, string key)
    {
        var raw = widget.Config.GetValueOrDefault(key)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return DefaultThresholdPercent;
        }
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value is < 1 or > 100)
        {
            throw new ConnectionException($"{key} 阈值只认 1-100 的数字，填了「{raw}」。", ConnectionHealthState.Degraded);
        }
        return value;
    }
}
