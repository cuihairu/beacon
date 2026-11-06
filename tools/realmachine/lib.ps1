# 视觉取证（2026-10-09 bug 批验收硬标准：没截图不算完，断言失败 = 红构建不发版）
#   场景 A 数量悬浮窗开：GLM 悬浮框必须在桌面（可见 "Beacon Tile" 窗）——全屏 + 6x 近景 +
#     进度条像素硬断言（底部 4 DIP 条带，2026-10-11 重设计后 bar 为默认）+「NN%」UIA 文本硬断言
#     （zhipu 连接指 mock /api/monitor/usage/quota/limit → 38% 健康链，不再赌公网/凭据）。
#   场景 B 数量悬浮窗关：桌面零残留窗——硬断言无可见 "Beacon Tile" / "Beacon Pinned"。
#   场景 C 浅色主题 icon 可辨度：白桌面 + theme=light，tile 近景暗像素硬断言——
#     近白 icon/文字叠浅底不可辨（用户实测「根本看不清」）在这里现形（修复=实色底+深前景）。
#   场景 D 五家 Provider 连接链（方舟/Kimi/MiMo/DeepSeek/千问）：本地 mock HTTP（127.0.0.1:18081 按
#     path 分发各响应）+ DPAPI 预置密钥 + --settings 启动 + UIA 驱动——逐家点模块行→点「测试」→
#     硬断言反馈含「连接正常」→ 展开组件「连接」下拉断言该连接在列→截图；
#     再 UIA 走一遍保存连接（save-flow），硬断言新连接**立即**出现在下拉（下拉不刷新回归在这里现形）；
#     组件向导「类型」下拉逐家断言非空且含本家条目（小米类型空回归在这里现形）；预置五张钉选
#     provider tile——UIA 读 tile 文本硬断言名字（火山方舟/DeepSeek/阿里千问）与数值（42.5/¥420.5/mimo 本机计数 128 次），
#     字段名（usage/quota）冒充名字在这里现形；「NN%」常驻百分比硬断言 + ark 进度条像素硬断言
#     （2026-10-11 重设计：bar 默认，底部 4 DIP 圆角条 + NN% 读数）；设置侧三断言：①config settingsSidebarWidth=400 启动恢复→左栏实测 400±8px（可拖
#     数据链）②设置搜索「额度」过滤 + 清词复原 ③SendInput 拖拽分隔条 +60px（不生效降级 warning，
#     手势转装机手验）；设置常规页改「检查频率」→15 秒——config.json 落盘
#     + 日志「检查频率变更为 15s」双硬断言（轮询节奏可配证据）。
# 断言失败不中途断：先收齐全部证据（失败现场截图），末尾统一 throw = 红构建。
# UIA/System.Security 需 Windows PowerShell 5.1（pwsh 缺 UIA 程序集）——非 5.1 自动自重启。
$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ne 5)
{
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath
    exit $LASTEXITCODE
}

# —— 总超时护栏（回归 run 41958504066/41963265872：脚本中途无进展时只能人肉盯着取消）——
# 分离 watchdog 进程倒计时，到点强杀本脚本进程（pid 定向）：步骤非 0 退出=红构建，
# 「挂满 job timeout / 数小时」从机制上不再可能。缺省 4800s=80min：本脚本含 15min 浸泡
# （场景 AD）与 40+ 场景，历史全程实测 ~70-90min（不能按 15min 设——会在浸泡中段误杀）；
# 早于 job timeout-minutes=90 触发，让 evidence 照常走 always() 上传步留证。
# 可用环境变量 BEACON_EVIDENCE_TIMEOUT_SEC 覆盖（本地调试缩短用）。
[int]$script:EvidenceTimeoutSec = 4800
if ($env:BEACON_EVIDENCE_TIMEOUT_SEC)
{
    [int]::TryParse($env:BEACON_EVIDENCE_TIMEOUT_SEC, [ref]$script:EvidenceTimeoutSec) | Out-Null
}
$evidenceWatchdogCmd = 'Start-Sleep -Seconds ' + $script:EvidenceTimeoutSec + '; $p = Get-Process -Id ' + $PID + ' -ErrorAction SilentlyContinue; if ($p) { Stop-Process -Id $p.Id -Force }'
$script:EvidenceWatchdog = Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @('-NoProfile', '-Command', $evidenceWatchdogCmd) -PassThru
Write-Host "总超时护栏已武装：watchdog pid=$($script:EvidenceWatchdog.Id)，${script:EvidenceTimeoutSec}s 后强杀本脚本（pid=$PID）；可用 BEACON_EVIDENCE_TIMEOUT_SEC 覆盖"

Set-Location $env:GITHUB_WORKSPACE

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Security
Add-Type -TypeDefinition @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class WinEnum
{
    private delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr h);
    public static List<IntPtr> VisibleHandlesByTitle(string title)
    {
        var found = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            if (IsWindowVisible(h))
            {
                var sb = new StringBuilder(512);
                GetWindowText(h, sb, 512);
                if (sb.ToString() == title) { found.Add(h); }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    // 顶层可见窗句柄全量枚举（EnumWindows 地面真值）：UIA 桌面根 Children 枚举漏 WinRT picker——
    // 38683714647 实证：失败截图里 Save As 对话框确在（File name 预填、Save/Cancel 齐全），而同一时刻
    // UIA 桌面清单只有 Beacon 设置/tiles/托盘/agent 控制台/Progman，picker 窗不在其中。故 picker 发现
    // 改走 EnumWindows（与 VisibleHandlesByClassAndPid 同一 P/Invoke 链，跨进程、与 UIA 桌面树无关）。
    public static List<IntPtr> VisibleTopLevelHandles()
    {
        var all = new List<IntPtr>();
        EnumWindows((h, l) => { if (IsWindowVisible(h)) { all.Add(h); } return true; }, IntPtr.Zero);
        return all;
    }
    public static string TitleOf(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
    // —— 场景 Z 纯 Win32 驱动（2026-10-15）：跨进程 UIA 查 WinRT picker 恒超时（GetFirstChild →
    // UIA_E_TIMEOUT 0x80131505，38689737249 实证），且该 picker 拒绝含 \ 与 : 的完整路径（shell 弹
    // 「Rename」校验错）——故控件定位走 EnumChildWindows，输入走 SetWindowText/WM_COMMAND，全程无 UIA。
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowTextW(IntPtr h, string t);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr h, int id);
    public static IntPtr DlgItemOf(IntPtr h, int id) { return GetDlgItem(h, id); }
    public static List<IntPtr> ChildrenOfClass(IntPtr parent, string cls)
    {
        var all = new List<IntPtr>();
        EnumChildWindows(parent, (h, l) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString() == cls) { all.Add(h); }
            return true;
        }, IntPtr.Zero);
        return all;
    }
    // 递归（直子优先）：经典 #32770 的文件名框是 ComboBox 内的 Edit，直子枚举够不着
    public static IntPtr DescendantsOfClass(IntPtr parent, string cls)
    {
        IntPtr direct = IntPtr.Zero;
        EnumChildWindows(parent, (h, l) =>
        {
            if (direct == IntPtr.Zero)
            {
                var sb = new StringBuilder(256);
                GetClassName(h, sb, 256);
                if (sb.ToString() == cls) { direct = h; }
            }
            return true;
        }, IntPtr.Zero);
        if (direct != IntPtr.Zero) { return direct; }
        return DescendantWalk(parent, cls);
    }
    private static IntPtr DescendantWalk(IntPtr parent, string cls)
    {
        IntPtr found = IntPtr.Zero;
        EnumChildWindows(parent, (h, l) =>
        {
            if (found == IntPtr.Zero)
            {
                var sb = new StringBuilder(256);
                GetClassName(h, sb, 256);
                if (sb.ToString() == cls) { found = h; }
                else { found = DescendantWalk(h, cls); }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static List<IntPtr> DescendantsOfClassAll(IntPtr parent, string cls)
    {
        var all = new List<IntPtr>();
        DescendantsScan(parent, cls, all);
        return all;
    }
    // EnumChildWindows 本身即「全部后代」遍历（非直子）——再往里递归会把每层子树重复计入：
    // 38709869802 实证 2 个 Edit 被列成 68 行（每个重复 34 遍 = 对话框树深度），证据被噪声淹没。
    private static void DescendantsScan(IntPtr parent, string cls, List<IntPtr> all)
    {
        EnumChildWindows(parent, (h, l) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString() == cls) { all.Add(h); }
            return true;
        }, IntPtr.Zero);
    }
    // 全部后代（不过滤类名）：对话框形态取证用——一次列全 class/id/可见/矩形/文本，
    // 判定「文件名框到底是 Win32 还是 DirectUI」不必再猜（38709869802 的教训）。
    public static List<IntPtr> Descendants(IntPtr parent)
    {
        var all = new List<IntPtr>();
        EnumChildWindows(parent, (h, l) => { all.Add(h); return true; }, IntPtr.Zero);
        return all;
    }
    public static void SetText(IntPtr h, string t) { SetWindowTextW(h, t); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    // CBE_SETEDITTEXT(0x1640)：Explorer 对话框的文件名框由 ComboBoxEx32 自绘，子 Edit 只是无绘制的缓冲区——
    // 38716060653 实证 SetWindowTextW 成功、GetWindowTextW 复核命中、截图裁框放大后框内仍空。写子 Edit 不动
    // 绘制面，提交时 IDOK 读到空名故 no-op。改走 ComboBoxEx 自己的编辑分支消息，lParam=LPWSTR 指针。
    public static bool SetComboBoxEditText(IntPtr h, string t)
    {
        if (h == IntPtr.Zero) { return false; }
        IntPtr p = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(t);
        try { return SendMessageW(h, 0x1640, IntPtr.Zero, p) != IntPtr.Zero; }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(p); }
    }
    public static bool Alive(IntPtr h) { return IsWindow(h); }
    public static bool Visible(IntPtr h) { return IsWindowVisible(h); }
    public static uint CtrlIdOf(IntPtr h) { return (uint)GetDlgCtrlID(h); }
    public static RECT RectOf(IntPtr h) { RECT r; GetWindowRect(h, out r); return r; }
    // DIP→物理像素缩放（GetDpiForWindow/96，与应用侧 DpiLayoutMath 同一换算口径）：
    // 断言期望值按窗实测 DPI 算，不写死 1.0——runner 换缩放不会假红（2026-10-14 缺陷单改造）。
    public static double DpiScaleOf(IntPtr h) { var d = GetDpiForWindow(h); return d == 0 ? 1.0 : d / 96.0; }
    public static RECT ClientRectOf(IntPtr h) { RECT r; GetClientRect(h, out r); return r; }
    // 客户区矩形换算到屏幕坐标：GetClientRect 原点是客户区自身（通常 0,0），New-Crop 走 CopyFromScreen
    // 要的是屏幕坐标，混用会把裁图挪到窗口原点（2026-10-14 进度条缺陷单断言改造踩坑）。
    public static RECT ClientScreenRectOf(IntPtr h)
    {
        RECT r; GetClientRect(h, out r);
        var w = r.Right - r.Left; var ht = r.Bottom - r.Top;
        POINT p; p.X = r.Left; p.Y = r.Top;
        ClientToScreen(h, ref p);
        return new RECT { Left = p.X, Top = p.Y, Right = p.X + w, Bottom = p.Y + ht };
    }
    // WM_NCHITTEST 探针（todo 行 65 空白穿透 CI 代验）：lParam 打包屏幕坐标直接问窗口命中裁决，
    // 返回 1=HTCLIENT（tile 行可交互）/ -1=HTTRANSPARENT（空白穿透到下层窗，Win32 对该点
    // 点击落点的官方语义即「落到下层」）——比注入真实点击确定性高，无 z 序/焦点竞态。
    public static int NcHitTest(IntPtr h, int x, int y)
    {
        var lp = (IntPtr)(((y & 0xFFFF) << 16) | (x & 0xFFFF));
        return unchecked((int)SendMessageW(h, 0x0084, IntPtr.Zero, lp));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SystemParametersInfo(uint action, uint param, string value, uint init);
    public static void SetWallpaper(string path) { SystemParametersInfo(20, 0, path, 3); }

    // 登录自启链代验（todo 行 96）：把注册表 Run 值**原样**交给 ShellExecute——登录时 explorer
    // 处理 Run 键走的就是这条 API。返回 >32 为成功，≤32 是失败码（2=文件未找到 / 5=拒绝访问 /
    // 31=无关联），随返回值带出便于断言定位。旧实现用 rundll32 shell32.dll,ShellExec_RunDll，
    // 该入口在新 shell32 仅序号导出、按名解析失败（38750239155 取证：RunDLL「Missing entry:
    // ShellExec_RunDll」对话框），故改直调文档化 API。
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr ShellExecuteW(IntPtr hwnd, string op, string file, string pars, string dir, int show);
    public static int ShellExec(string command) { return (int)ShellExecuteW(IntPtr.Zero, "open", command, null, null, 1); }

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    public static void SetForeground(IntPtr h) { SetForegroundWindow(h); }
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public static void MoveNoFocus(IntPtr h, int x, int y)
    {
        SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0004u | 0x0001u | 0x0010u); // SWP_NOZORDER|SWP_NOSIZE|SWP_NOACTIVATE——移位不改叠层不抢焦点
    }
    private const uint MouseDown = 0x0002;
    private const uint MouseUp = 0x0004;

    // 38044411443 实证：SetCursorPos+mouse_event 的合成拖拽在 CI 会话不生效（左栏 264→264）。
    // 换 SendInput 绝对坐标（归一化 0-65535）：INPUT 队列级注入，XAML 输入管线与真实鼠标同路径，
    // 且带 MOUSEEVENTF_VIRTUALDESK 多屏安全。struct 显式 LayoutKind.Sequential（PS5.1 Add-Type 无 unsafe）
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public INPUTUNION u; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    public static void DragMouse(int fromX, int fromY, int toX, int toY)
    {
        int vx = ToVirtual(fromX, 76, 78); int vy = ToVirtual(fromY, 77, 79);
        int vx2 = ToVirtual(toX, 76, 78); int vy2 = ToVirtual(toY, 77, 79);
        var move = new Func<int, int, INPUT>((x, y) => new INPUT
        {
            type = 0, // INPUT_MOUSE
            u = new INPUTUNION { mi = new MOUSEINPUT { dx = x, dy = y, dwFlags = 0x8001 } } // MOVE|ABSOLUTE|VIRTUALDESK
        });
        var down = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = 0x0002 } } };  // LEFTDOWN
        var up = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = 0x0004 } } };    // LEFTUP
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { move(vx, vy) }, size);
        System.Threading.Thread.Sleep(200);
        SendInput(1, new[] { down }, size);
        System.Threading.Thread.Sleep(200);
        for (int i = 1; i <= 12; i++)
        {
            SendInput(1, new[] { move(vx + (vx2 - vx) * i / 12, vy + (vy2 - vy) * i / 12) }, size);
            System.Threading.Thread.Sleep(40);
        }
        SendInput(1, new[] { up }, size);
        System.Threading.Thread.Sleep(400);
    }
    // 场景 X：合成右键（行级 ContextFlyout 触发）。同 DragMouse 的 SendInput 绝对坐标口径——
    // mouse_event/SetCursorPos 合成输入在 CI 会话不生效（38044411443），必须走 INPUT 队列。
    // RIGHTDOWN=0x0008 / RIGHTUP=0x0010
    public static void RightClick(int x, int y)
    {
        int vx = ToVirtual(x, 76, 78); int vy = ToVirtual(y, 77, 79);
        var move = new INPUT
        {
            type = 0, // INPUT_MOUSE
            u = new INPUTUNION { mi = new MOUSEINPUT { dx = vx, dy = vy, dwFlags = 0x8001 } } // MOVE|ABSOLUTE|VIRTUALDESK
        };
        var down = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = 0x0008 } } };  // RIGHTDOWN
        var up = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = 0x0010 } } };    // RIGHTUP
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { move }, size);
        System.Threading.Thread.Sleep(200);
        SendInput(1, new[] { down }, size);
        System.Threading.Thread.Sleep(120);
        SendInput(1, new[] { up }, size);
        System.Threading.Thread.Sleep(400);
    }
    // 物理像素 → 绝对归一化 [0,65535]：origin(76/77) 起算、span(78/79) 归一（VIRTUALDESK 语义）
    private static int ToVirtual(int px, int originMetric, int spanMetric)
    {
        int origin = GetSystemMetrics(originMetric);
        int span = GetSystemMetrics(spanMetric);
        if (span <= 0) { span = 1; }
        int v = (px - origin) * 65536 / span;
        if (v < 0) { v = 0; } if (v > 65535) { v = 65535; }
        return v;
    }
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);

    // —— 场景 E/F/G/H（2026-10-12 巡检点火批）新增：单实例/托盘/离线/调色/形态结构取证面 ——
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowW(string cls, string title);
    public static IntPtr FindByClass(string cls) { return FindWindowW(cls, null); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string cls, string title);
    public static IntPtr FindByClassMsgOnly(string cls) { return FindWindowExW((IntPtr)(-3), IntPtr.Zero, cls, null); } // HWND_MESSAGE=-3：message-only 窗口不在顶层枚举里
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int index);
    public static long ExStyleOf(IntPtr h) { return (long)(uint)GetWindowLong(h, -20); } // GWL_EXSTYLE=-20
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    public static IntPtr Foreground() { return GetForegroundWindow(); }
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    public static void Post(IntPtr h, uint msg, IntPtr w, IntPtr l) { PostMessageW(h, msg, w, l); }
    public static void SetCursor(int x, int y) { SetCursorPos(x, y); } // SetCursorPos import 复用第 69 行（拖拽批既有）
    public static void BringToFront(IntPtr h) { SetForegroundWindow(h); } // SetForegroundWindow 为 private import，PS 侧须经公开包装
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint a, uint b, bool attach);
    // PS 进程自身不是前台时 BringToFront 对任意窗口都静默失败（前台授予要求调用方即前台进程，
    // 38261911354 E2/E3 实证：BringToFront 后菜单仍不弹）。AttachThreadInput 挂到当前前台线程
    // 共享前台权后再切，是无输入注入依赖的经典取前台手法（键盘注入在本 CI 会话不可达，K1 实证）。
    public static void ForceForeground(IntPtr h)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == h) { SetForegroundWindow(h); return; }
        uint fgPid;
        var fgThread = GetWindowThreadProcessId(fg, out fgPid);
        var myThread = GetCurrentThreadId();
        if (fgThread == myThread) { SetForegroundWindow(h); return; }
        var attached = AttachThreadInput(myThread, fgThread, true);
        try { SetForegroundWindow(h); }
        finally { if (attached) { AttachThreadInput(myThread, fgThread, false); } }
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    public static uint PidOf(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    public static string ClassOf(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static List<IntPtr> VisibleHandlesByClassAndPid(string cls, uint pid)
    {
        var found = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            if (IsWindowVisible(h))
            {
                uint p; GetWindowThreadProcessId(h, out p);
                if (p == pid)
                {
                    var sb = new StringBuilder(256);
                    GetClassName(h, sb, 256);
                    if (sb.ToString() == cls) { found.Add(h); }
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    // 标题前缀匹配（L3 详情窗标题 Beacon · {summary} 动态，无法精确匹配）；prefix 由调用方传入
    public static List<IntPtr> VisibleHandlesByTitlePrefix(string prefix)
    {
        var found = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            if (IsWindowVisible(h))
            {
                var sb = new StringBuilder(512);
                GetWindowText(h, sb, 512);
                if (sb.ToString().StartsWith(prefix, StringComparison.Ordinal)) { found.Add(h); }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    // 键盘注入（托盘原生菜单导航）：dwFlags=0 按下 / 2 抬起
    // INPUT.type 必须为 1（INPUT_KEYBOARD）；0 是 INPUT_MOUSE，键事件会被丢弃
    public static void SendVirtualKey(ushort vk)
    {
        var down = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0000 } } };
        var up = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { down }, size);
        System.Threading.Thread.Sleep(90);
        SendInput(1, new[] { up }, size);
        System.Threading.Thread.Sleep(120);
    }
    // 单键按下/抬起分离（全局热键和弦用，如 Ctrl+Alt+B：MOD 修饰键按住期间敲主键）
    public static void SendKeyDown(ushort vk)
    {
        var down = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0000 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { down }, size);
        System.Threading.Thread.Sleep(60);
    }
    public static void SendKeyUp(ushort vk)
    {
        var up = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { up }, size);
        System.Threading.Thread.Sleep(60);
    }
    // 全局热键和弦（如 Ctrl+Alt+B）：修饰键按下 → 主键敲击 → 修饰键抬起。
    // modifier2 可选：注册热键 Ctrl+Alt+B 要两个修饰键——旧签名只发 Ctrl+B 与注册热键
    // 恒不匹配，K1 一直被 WM_HOTKEY 兜底带着走（38743014862 实证：type 修 1 后 Esc
    // 可达而和弦仍不触发，根因即和弦缺 Alt）。
    public static void SendChord(ushort modifier, ushort key, ushort modifier2 = 0)
    {
        SendKeyDown(modifier);
        if (modifier2 != 0) { SendKeyDown(modifier2); }
        SendKeyDown(key);
        SendKeyUp(key);
        if (modifier2 != 0) { SendKeyUp(modifier2); }
        SendKeyUp(modifier);
    }
    // 单击注入（前景不被打断断言用）：绝对坐标 move+down+up
    public static void ClickAt(int x, int y)
    {
        int vx = ToVirtual(x, 76, 78); int vy = ToVirtual(y, 77, 79);
        var move = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dx = vx, dy = vy, dwFlags = 0x8001 } } };
        var down = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = 0x0002 } } };
        var up = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = 0x0004 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { move }, size);
        System.Threading.Thread.Sleep(150);
        SendInput(1, new[] { down }, size);
        System.Threading.Thread.Sleep(90);
        SendInput(1, new[] { up }, size);
        System.Threading.Thread.Sleep(300);
    }
    // 键盘 UNICODE 注入：KEYEVENTF_UNICODE(0x0004)、wScan=码位——与键盘布局无关，文件名里的 - . 在
    // VK 注入下会错。type 必须是 INPUT_KEYBOARD(1)。CI 会话键盘注入可达性未证实（K1 注记），故仅作
    // CBE_SETEDITTEXT 之后可见框仍空时的兜底。
    public static void TypeText(string s)
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        foreach (char c in s)
        {
            var down = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = 0x0004 } } };
            var up = new INPUT { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wScan = c, dwFlags = 0x0006 } } };
            SendInput(1, new[] { down }, size);
            System.Threading.Thread.Sleep(25);
            SendInput(1, new[] { up }, size);
            System.Threading.Thread.Sleep(25);
        }
    }
}
"@

$configDir = Join-Path $env:APPDATA "Beacon"
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
$evidence = Join-Path $env:GITHUB_WORKSPACE "evidence"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$failures = New-Object System.Collections.Generic.List[string]

# —— 开场窗口基线（回归 run 41958504066/41963265872 排查面）：跑任何场景前把 Beacon 进程与
# 其顶层可见窗（class/title/rect）逐条落日志——UIA 阶段无进展时，这份基线能直接判定
# 「是否多建了置顶窗」（如胶囊窗懒建回归）或残留了上一场景的窗。
function Log-BeaconWindowInventory([string] $stage)
{
    $inventoryProcs = @(Get-Process Beacon.App -ErrorAction SilentlyContinue)
    if ($inventoryProcs.Count -eq 0)
    {
        Write-Host "窗口基线[$stage]：Beacon.App 进程不存在"
        return
    }
    foreach ($inventoryProc in $inventoryProcs)
    {
        Write-Host "窗口基线[$stage]：Beacon.App pid=$($inventoryProc.Id) 顶层可见窗："
        foreach ($h in [WinEnum]::VisibleTopLevelHandles())
        {
            if ([WinEnum]::PidOf($h) -eq $inventoryProc.Id)
            {
                $inventoryRect = [WinEnum]::RectOf($h)
                Write-Host ("    class={0} title='{1}' rect=({2},{3})-({4},{5})" -f [WinEnum]::ClassOf($h), [WinEnum]::TitleOf($h), $inventoryRect.Left, $inventoryRect.Top, $inventoryRect.Right, $inventoryRect.Bottom)
            }
        }
    }
}
Log-BeaconWindowInventory "script-start"

function Stop-Beacon
{
    Get-Process Beacon.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    # 收尸后残留取证：进程/窗没死干净立刻落基线（此前「零残留」断言失败只能靠场景报错倒推）
    if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -gt 0)
    {
        Write-Host "::warning::Stop-Beacon 后 Beacon.App 进程仍残留——窗口基线如下供定位"
        Log-BeaconWindowInventory "post-stop-residue"
    }
}

function Write-ScenarioConfig([bool] $numericFloating, [string] $theme = "dark")
{
    Stop-Beacon
    # numericFloatingResetDone 必须 true：否则启动迁移把开关强制回关，场景 A 直接失真
    $config = @'
{
  "configVersion": 1,
  "hotkey": "Ctrl+Alt+B",
  "launchOnStartup": false,
  "theme": "__THEME__",
  "pinDisplayMode": "floating",
  "numericFloatingEnabled": __NUM__,
  "numericFloatingResetDone": true,
  "showCapsule": false
}
'@.Replace("__THEME__", $theme).Replace("__NUM__", $numericFloating.ToString().ToLowerInvariant())
    Set-Content -Path (Join-Path $configDir "config.json") -Value $config -Encoding UTF8
    @'
[
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    # zhipu 指 mock（连接 Endpoint 是全 URL 语义）：GLM tile 确定性健康——此前无凭据真端点时好时坏
    # （38048738860 离线态、38044411443 健康），条带+NN% 断言不能赌公网
    @'
[
  {
    "id": "bigmodel.usage:GLM",
    "type": "bigmodel.usage",
    "connectionId": "zhipu",
    "refreshTier": "ci",
    "pinned": true,
    "config": { "label": "GLM" }
  }
]
'@ | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    '{ "tiles": [] }' | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8
    # 场景 C 的浅底前提：白桌面（tile 旧版透明底叠桌面，浅底才暴露近白 icon 不可辨）
    $wallpaper = Join-Path $env:TEMP "beacon-wallpaper.png"
    $bmp = New-Object System.Drawing.Bitmap 32, 32
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.Clear([System.Drawing.Color]::White)
    $gfx.Dispose()
    $bmp.Save($wallpaper, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [WinEnum]::SetWallpaper($wallpaper)
}

# —— 场景 D 配置：四家连接全指本地 mock + DPAPI 预置密钥（凭据只入 secrets.bin，配置零明文）——
function Write-ProviderConfig
{
    Stop-Beacon
    @'
{
  "configVersion": 1,
  "hotkey": "Ctrl+Alt+B",
  "launchOnStartup": false,
  "theme": "dark",
  "pinDisplayMode": "floating",
  "numericFloatingEnabled": true,
  "numericFloatingResetDone": true,
  "showCapsule": false,
  "settingsSidebarWidth": 400
}
'@ | Set-Content -Path (Join-Path $configDir "config.json") -Encoding UTF8
    # numericFloating 必须 true：五张 tile 全是 FloatingOptIn 数值类——总闸关着悬浮/面板两路都不建窗
    # （WidgetDisplayPolicy.PanelCarries 数值抑制口径，桌面零残留拍板），tile 名字/数值断言直接无窗可断
    # settingsSidebarWidth=400：确定性「可拖」证据链——绕过合成鼠标（38044411443 两次不生效），
    # 直接证 config→启动恢复→左栏实测宽 400px；拖拽手势另证（SendInput，失败降级 warning 不拦 nightly）
    @'
[
  { "id": "ark-main", "type": "ark", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ark-main", "enabled": true },
  { "id": "kimi-main", "type": "kimi", "endpoint": "http://127.0.0.1:18081/coding/v1/usages", "credentialRef": "conn:kimi-main", "enabled": true },
  { "id": "mimo-main", "type": "mimo", "endpoint": "http://127.0.0.1:18081/v1", "credentialRef": "conn:mimo-main", "enabled": true, "settings": { "usage_endpoint": "http://127.0.0.1:18081/mimo/usage" } },
  { "id": "ds-main", "type": "deepseek", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ds-main", "enabled": true },
  { "id": "qwen-main", "type": "qwen", "endpoint": "http://127.0.0.1:18081/compatible-mode/v1", "credentialRef": "conn:qwen-main", "enabled": true, "settings": { "usage_endpoint": "http://127.0.0.1:18081/qwen/usage" } }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    # 五张钉选 provider tile：名字全靠 WidgetTypeNames 兜底（无 label 配置——字段名冒充名字在这里现形）
    @'
[
  { "id": "ark.usage:ark", "type": "ark.usage", "connectionId": "ark-main", "refreshTier": "ci", "pinned": true, "config": {} },
  { "id": "deepseek.balance:ds", "type": "deepseek.balance", "connectionId": "ds-main", "refreshTier": "ci", "pinned": true, "config": {} },
  { "id": "qwen.usage:qwen", "type": "qwen.usage", "connectionId": "qwen-main", "refreshTier": "ci", "pinned": true, "config": {} },
  { "id": "kimi.coding:kimi", "type": "kimi.coding", "connectionId": "kimi-main", "refreshTier": "ci", "pinned": true, "config": {} },
  { "id": "mimo.usage:mimo", "type": "mimo.usage", "connectionId": "mimo-main", "refreshTier": "ci", "pinned": true, "config": {} }
]
'@ | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    '{ "tiles": [] }' | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8

    # secrets.bin = JSON 字典 credentialRef → base64(DPAPI(CurrentUser, entropy="Beacon:"+ref))，
    # 与 DpapiSecretStore 逐字段对齐（应用可解 = provider 真实走通鉴权头）
    $secretsPath = Join-Path $configDir "secrets.bin"
    $store = @{}
    if (Test-Path $secretsPath)
    {
        (ConvertFrom-Json (Get-Content $secretsPath -Raw)).PSObject.Properties | ForEach-Object { $store[$_.Name] = $_.Value }
    }
    foreach ($pair in @(
        @("conn:ark-main", "AKTESTKEY:SKTESTSECRET"),
        @("conn:kimi-main", "sk-kimi-mock"),
        @("conn:mimo-main", "sk-mimo-mock"),
        @("conn:ds-main", "sk-ds-mock"),
        @("conn:qwen-main", "sk-sp-qwen-mock")))
    {
        $entropy = [System.Text.Encoding]::UTF8.GetBytes("Beacon:" + $pair[0])
        $protectedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
            [System.Text.Encoding]::UTF8.GetBytes($pair[1]), $entropy,
            [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
        $store[$pair[0]] = [Convert]::ToBase64String($protectedBytes)
    }
    # WriteAllText = UTF-8 无 BOM——Set-Content UTF8 的 BOM 会让 JsonDocument.Parse 拒读（before 轮实证）
    [System.IO.File]::WriteAllText($secretsPath, ($store | ConvertTo-Json -Compress))
    Write-Host "预置四家连接 + DPAPI 密钥完成（secrets.bin）"
}

function Start-MockServer
{
    # netsh 尽力而为（runner 已提权，通常不需要）；失败不阻断
    netsh http add urlacl url=http://127.0.0.1:18081/ user=Everyone 2>$null | Out-Null
    Remove-Item (Join-Path $env:TEMP "beacon-mock-stop.txt") -Force -ErrorAction SilentlyContinue # 清残留停止标记（防新作业首轮即自停）
    $script:mockJob = Start-Job -ScriptBlock {
        $listener = New-Object System.Net.HttpListener
        $listener.Prefixes.Add("http://127.0.0.1:18081/")
        $listener.Start()
        $ark = '{"ResponseMetadata":{"RequestId":"mock-1","Action":"GetCodingPlanUsage"},"Result":{"Status":"Running","QuotaUsage":[{"Level":"session","Percent":42.5,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":8,"ResetTimestamp":1772000000},{"Level":"monthly","Percent":"92","ResetTimestamp":0}]}}'
        # bigmodel 监控响应（quota/limit 结构：limits[] 按 type/unit/number 归类——5h 滚动窗 unit=3/number=5）。
        # 健康链口径（2026-10-13 最差窗规则）：级别取已用最高窗——三窗必须全部低于 warn(60)，且 5h(38) 须为
        # 最高窗（月 20<38），摘要才以「5h 窗口已用 38%」打头，S1 的「已用 38%」事件行断言才成立。
        # （38390521909 实测：月 61 ≥ 60 → GLM 翻 Warning，G2/L/O1/Q1/S1 六连环红）
        $bigmodel = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":38,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":15,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":20,"nextResetTime":0}]}}'
        # 档位翻转态（G 场景状态变化/动效断言）：主窗 percentage 95 ≥ error_percent(90) → severity Error
        $bigmodelError = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":95,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":15,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":61,"nextResetTime":0}]}}'
        # 四态渲染档（场景 AE 验收③）：p0=0%（percent 选择器出「0%」，0 是有效值）/ p52=52%（中位）/
        # p100=100%（满格，Progress=1.0 →「100%」）；三窗最高窗取主窗值，severity 走各自档
        $bigmodelP0 = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":0,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":0,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":0,"nextResetTime":0}]}}'
        $bigmodelP52 = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":52,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":10,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":20,"nextResetTime":0}]}}'
        $bigmodelP100 = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":100,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":15,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":20,"nextResetTime":0}]}}'
        # 档位文件：内容 "error"=95% / "p0"=0% / "p52"=52% / "p100"=100%，空/缺 = 38% 态（场景侧 Set-Content / Remove-Item 切换）
        $modeFile = Join-Path $env:TEMP "beacon-mock-mode.txt"
        $kimi = '{"usage":{"limit":1000,"used":300,"remaining":700,"resetTime":"2026-10-13"},"limits":[{"window":{"duration":5,"timeUnit":"HOUR"},"detail":{"limit":200,"remaining":150,"resetTime":"2026-10-09T18:00"}}],"user":{"membership":{"level":"pro"}}}'
        $deepseek = '{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"420.50","granted_balance":"20.00","topped_up_balance":"400.50"}]}'
        $mimo = '{"data":[{"id":"mimo-v1"},{"id":"mimo-mini"}]}'
        $mimoUsage = '{"total_calls":128,"window_calls":6,"window_minutes":60}'
        $qwenModels = '{"object":"list","data":[{"id":"qwen3-coder-plus"},{"id":"qwen3-max"}]}'
        $qwenUsage = '{"percent":42.5,"remaining_credits":1150,"total_credits":2000,"reset_at":"2026-10-19T00:00:00+08:00"}'
        # GitHub Actions 固定失败 run（场景 U L3 动作链）：id 12345 → rerun POST 路径精确可断
        $ghRuns = '{"total_count":1,"workflow_runs":[{"id":12345,"name":"ci","status":"completed","conclusion":"failure","head_branch":"main","html_url":"https://github.com/cuihairu/beacon/actions/runs/12345","run_started_at":"2026-10-13T10:00:00Z","updated_at":"2026-10-13T10:05:00Z"}]}'
        $stopFile = Join-Path $env:TEMP "beacon-mock-stop.txt"
        while ($listener.IsListening)
        {
            if (Test-Path $stopFile)
            {
                Remove-Item $stopFile -Force -ErrorAction SilentlyContinue
                $listener.Stop() # 显式停监听=HTTP.SYS 注册即时释放，不靠 GC 兜底（悬空注册=后续 Start 撞 existing registration）
                break
            }
            $ctx = $null
            try
            {
                $ctx = $listener.GetContext()
                $path = $ctx.Request.Url.AbsolutePath
                switch ($path)
                {
                    "/coding/v1/usages" { $body = $kimi }
                    "/api/monitor/usage/quota/limit" {
                        $mode = ""
                        try { $mode = (Get-Content $modeFile -Raw -ErrorAction Stop).Trim() } catch { }
                        # 38% 健康链 / 95% Error 链 / 0%-52%-100% 四态链按档位文件翻转
                        $body = if ($mode -eq "error") { $bigmodelError }
                            elseif ($mode -eq "p0") { $bigmodelP0 }
                            elseif ($mode -eq "p52") { $bigmodelP52 }
                            elseif ($mode -eq "p100") { $bigmodelP100 }
                            else { $bigmodel }
                    }
                    "/user/balance" { $body = $deepseek }
                    "/v1/models" { $body = $mimo }
                    "/mimo/usage" { $body = $mimoUsage }
                    "/compatible-mode/v1/models" { $body = $qwenModels }
                    "/qwen/usage" { $body = $qwenUsage }
                    "/repos/cuihairu/beacon/actions/workflows/ci.yml/runs" { $body = $ghRuns }
                    "/repos/cuihairu/beacon/actions/runs/12345/rerun-failed-jobs" {
                        # POST 回执留痕（场景 U）：marker 文件 = 服务端真收到重跑请求的硬证据
                        Set-Content -Path (Join-Path $env:TEMP "beacon-gh-rerun-hit.txt") -Value ("rerun " + (Get-Date -Format "HH:mm:ss")) -Encoding ASCII
                        $rerunMode = ""
                        try { $rerunMode = (Get-Content (Join-Path $env:TEMP "beacon-gh-rerun-mode.txt") -Raw -ErrorAction Stop).Trim() } catch { }
                        if ($rerunMode -eq "fail") { $ctx.Response.StatusCode = 500 } # 失败链：executor 收 ConnectionException → ✗
                        $body = "{}"
                    }
                    default { $body = $ark } # POST /?Action=GetCodingPlanUsage
                }
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
                $ctx.Response.ContentType = "application/json; charset=utf-8"
                $ctx.Response.ContentLength64 = $bytes.Length
                $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                $ctx.Response.OutputStream.Close()
            }
            catch
            {
                # GetContext 自身异常也吞掉续循环——作业线程死掉=监听悬空，后续 Start 必撞端口
                if ($ctx -ne $null) { try { $ctx.Response.Abort() } catch { } }
                Start-Sleep -Milliseconds 100
            }
        }
    }
    Start-Sleep -Seconds 2
    $alive = Get-Job -Id $script:mockJob.Id -ErrorAction SilentlyContinue
    if (!$alive -or $script:mockJob.State -ne "Running")
    {
        Receive-Job $script:mockJob -Keep | Write-Host
        throw "mock HTTP server 启动失败（127.0.0.1:18081）"
    }
    Write-Host "mock HTTP server 已监听 127.0.0.1:18081（各响应按 path 分发）"
}

function Stop-MockServer
{
    if (Get-Variable mockJob -Scope Script -ErrorAction SilentlyContinue)
    {
        # 停止标记：作业内循环见标即 Stop() 显式释放注册（GC 兜底太慢=悬空注册，
        # 后续 Start 撞 existing registration——38440205547 U/V 连红根因）
        Set-Content -Path (Join-Path $env:TEMP "beacon-mock-stop.txt") -Value "stop" -Encoding ASCII
        Stop-Job $script:mockJob -ErrorAction SilentlyContinue
        Remove-Job $script:mockJob -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $env:TEMP "beacon-mock-stop.txt") -Force -ErrorAction SilentlyContinue
    }
    # Stop-Job 只终止作业线程，HttpListener 的 urlacl/端口释放是异步的：紧接着的
    # Start-MockServer 会撞「Failed to listen on prefix … conflicts with an existing
    # registration」（38440205547 的 U/V 连红根因）。这里轮询到端口真不可达才返回。
    $releaseDeadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $releaseDeadline)
    {
        $stillUp = $null
        try { $stillUp = (Invoke-WebRequest -Uri "http://127.0.0.1:18081/api/monitor/usage/quota/limit" -TimeoutSeconds 2).StatusCode } catch { }
        if (!$stillUp) { return }
        Start-Sleep -Milliseconds 500
    }
}

function Ensure-MockServer
{
    # 幂等拉起：先探针（重试 3 次吸收瞬断/单线程作业卡顿），仍败则经 Stop-MockServer 收净
    # （内含端口释放轮询）再拉起；Start 撞 existing registration 时退避重试——38440205547
    # U/V 连红根因=探针失败后盲目 Start 撞未释注册。全场景统一走本助手，不再裸调 Start-MockServer 自愈。
    for ($i = 0; $i -lt 3; $i++)
    {
        $probeStatus = $null
        try { $probeStatus = (Invoke-WebRequest -Uri "http://127.0.0.1:18081/api/monitor/usage/quota/limit" -TimeoutSeconds 3).StatusCode } catch { }
        if ($probeStatus) { return }
        Start-Sleep -Seconds 1
    }
    Stop-MockServer
    $started = $false
    for ($i = 0; $i -lt 5; $i++)
    {
        try { Start-MockServer; $started = $true; break }
        catch { Start-Sleep -Seconds 2 }
    }
    if (!$started) { throw "mock HTTP server 拉起失败（Stop 后重试 5 次仍失败）" }
}

# AUMID 注册在案判定：HKCU\Software\Classes\AppUserModelId 下存在带 CustomActivator 值的键。
# toolkit 7.1.3 的「AUMID 自愈」= Initialize() 首次触碰 ToastNotificationManagerCompat 时写此键
# （Show() 只调 CreateToastNotifier().Show()，全程不建 .lnk——源证 ToastContentBuilder.cs:430）。
function Test-AumidKeyPresent
{
    $root = "HKCU:\Software\Classes\AppUserModelId"
    if (!(Test-Path $root)) { return $false }
    foreach ($k in @(Get-ChildItem $root -ErrorAction SilentlyContinue))
    {
        $props = Get-ItemProperty $k.PSPath -ErrorAction SilentlyContinue
        if ($props -and $props.CustomActivator) { return $true }
    }
    return $false
}

# —— 场景 E/F/G/H 取证助手（2026-10-12 巡检点火批：需装机清单 CI 代验转化） ——

# 颜色像素计数：与目标 RGB 三通道各差 ≤tol 的像素数（状态灯/条带档位色硬断言）
function Count-ColorPixels([System.Drawing.Bitmap] $bmp, [int] $r, [int] $g, [int] $b, [int] $tol)
{
    $count = 0
    for ($y = 0; $y -lt $bmp.Height; $y++)
    {
        for ($x = 0; $x -lt $bmp.Width; $x++)
        {
            $p = $bmp.GetPixel($x, $y)
            if ([Math]::Abs($p.R - $r) -le $tol -and [Math]::Abs($p.G - $g) -le $tol -and [Math]::Abs($p.B - $b) -le $tol) { $count++ }
        }
    }
    return $count
}

# 双帧差分像素计数：任一通道差 >tol 的像素数（呼吸动效在帧间现形；off 档应≈0）
function Count-FrameDiff([System.Drawing.Bitmap] $a, [System.Drawing.Bitmap] $b, [int] $tol)
{
    $count = 0
    $w = [Math]::Min($a.Width, $b.Width)
    $h = [Math]::Min($a.Height, $b.Height)
    for ($y = 0; $y -lt $h; $y++)
    {
        for ($x = 0; $x -lt $w; $x++)
        {
            $p1 = $a.GetPixel($x, $y)
            $p2 = $b.GetPixel($x, $y)
            if ([Math]::Abs($p1.R - $p2.R) -gt $tol -or [Math]::Abs($p1.G - $p2.G) -gt $tol -or [Math]::Abs($p1.B - $p2.B) -gt $tol) { $count++ }
        }
    }
    return $count
}

# 今日应用日志全文（UTF-8 无 BOM——PS 5.1 默认 ANSI 读中文必乱码，见场景 D 同款教训）
function Get-TodayLog
{
    $logPath = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    if (!(Test-Path $logPath)) { return "" }
    return (Get-Content $logPath -Raw -Encoding UTF8)
}

# HKCU Run\Beacon 自启值（键/值缺失或读失败 → $null；场景 M 开机自启注册表对齐断言）
function Get-BeaconRunValue
{
    try { return (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Beacon' -ErrorAction Stop).Beacon } catch { return $null }
}

# 合成托盘右键菜单驱动：直发托盘回调消息（WM_APP_TRAYICON, IconId=1, WM_RBUTTONUP）绕过真实图标点击，
# TrackPopupMenu 原生菜单（#32768）在应用消息线程模态弹出后键盘导航（DOWN×n + RETURN）选中。
# 返回 $false = 菜单未弹出（调用方按降级口径处理，参考分隔条拖拽先例）。
# 菜单项序：Open Beacon(1) Keep Panel Open(2) Notifications(3) Refresh All(4) Settings(5) ── Exit(6)，分隔符跳过。
function Invoke-TrayMenu([int] $downCount, [string] $expect)
{
    # 残留菜单先收掉（Esc），避免 #32768 命中旧窗
    if ([WinEnum]::FindByClass("#32768") -ne [IntPtr]::Zero)
    {
        [WinEnum]::SendVirtualKey(0x1B)
        Start-Sleep -Milliseconds 400
    }
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero)
    {
        $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") # 托盘消息窗是 message-only（HWND_MESSAGE），FindWindowW 搜顶层搜不到（38254521518 实证）
    }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "未找到 BeaconMessageWindow（托盘消息窗，托盘未初始化？）" }
    # TrackPopupMenu 需要 owner 线程在前台（否则菜单不显示，38256993295/38261911354 实证）：
    # PS 自身非前台时 BringToFront 静默失败（前台授予要求调用方即前台进程），改走
    # AttachThreadInput 共享前台权（ForceForeground）。app 内部 SetForegroundWindow(消息窗)
    # 本就必败（message-only 窗不可前台），菜单显示只依赖调用线程的前台归属。
    $fgFeed = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($fgFeed.Count -eq 0) { $fgFeed = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板") }
    if ($fgFeed.Count -gt 0)
    {
        [WinEnum]::ForceForeground($fgFeed[0])
        Start-Sleep -Milliseconds 300
    }
    [WinEnum]::SetCursor(300, 300) # 菜单弹在光标处，避开右上角 tile 级联区
    Start-Sleep -Milliseconds 200
    [WinEnum]::Post($msgWindow, 0x8100, [IntPtr]1, [IntPtr]0x0202) # WM_APP_TRAYICON=0x8100, IconId=1, WM_RBUTTONUP=0x0202
    $menu = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(6)
    while ((Get-Date) -lt $deadline)
    {
        $menu = [WinEnum]::FindByClass("#32768")
        if ($menu -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 300
    }
    if ($menu -eq [IntPtr]::Zero) { return $false }
    Save-FullScreenshot "tray-menu-open.png"
    Write-Host "托盘菜单已弹出（#32768）：DOWN×${downCount} → RETURN 选中「$expect」"
    for ($i = 0; $i -lt $downCount; $i++) { [WinEnum]::SendVirtualKey(0x28) } # VK_DOWN
    [WinEnum]::SendVirtualKey(0x0D) # VK_RETURN
    Start-Sleep -Milliseconds 800
    return $true
}

# G 场景配置：GLM（bigmodel 38% 健康）+ DeepSeek（420.5）双 tile；GLM 可带 colorOverride（合法/非法两态）
# $showCapsule：胶囊窗显示开关（场景 Q 胶囊计数/离线横幅断言用；默认 false 维持各场景零残留口径）
function Write-GConfig([string] $glmColorOverride = "", [string] $motionMode = "off", [double] $motionIntensity = 1.0, [bool] $launchOnStartup = $false, [bool] $showCapsule = $false)
{
    Stop-Beacon
    $appearance = '"appearance": { "motion": { "mode": "' + $motionMode + '", "intensity": ' + $motionIntensity + ' } }'
    @'
{
  "configVersion": 1,
  "hotkey": "Ctrl+Alt+B",
  "launchOnStartup": __STARTUP__,
  "theme": "dark",
  "pinDisplayMode": "floating",
  "numericFloatingEnabled": true,
  "numericFloatingResetDone": true,
  "showCapsule": __CAPSULE__,
  "settingsSidebarWidth": 400,
  __APPEARANCE__
}
'@.Replace("__STARTUP__", $launchOnStartup.ToString().ToLowerInvariant()).Replace("__APPEARANCE__", $appearance).Replace("__CAPSULE__", $showCapsule.ToString().ToLowerInvariant()) | Set-Content -Path (Join-Path $configDir "config.json") -Encoding UTF8
    @'
[
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": true },
  { "id": "ds-main", "type": "deepseek", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ds-main", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    $glmOverride = ""
    if ($glmColorOverride) { $glmOverride = ", `"colorOverride`": `"$glmColorOverride`"" }
    $widgets = @"
[
  { "id": "bigmodel.usage:GLM", "type": "bigmodel.usage", "connectionId": "zhipu", "refreshTier": "ci", "pinned": true, "config": { "label": "GLM" }$glmOverride },
  { "id": "deepseek.balance:ds", "type": "deepseek.balance", "connectionId": "ds-main", "refreshTier": "ci", "pinned": true, "config": {} }
]
"@
    Set-Content -Path (Join-Path $configDir "widgets.json") -Value $widgets -Encoding UTF8
    '{ "tiles": [] }' | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8
    # deepseek 密钥预置（420.5 健康链）
    $secretsPath = Join-Path $configDir "secrets.bin"
    $store = @{}
    if (Test-Path $secretsPath)
    {
        (ConvertFrom-Json (Get-Content $secretsPath -Raw)).PSObject.Properties | ForEach-Object { $store[$_.Name] = $_.Value }
    }
    $entropy = [System.Text.Encoding]::UTF8.GetBytes("Beacon:conn:ds-main")
    $protected = [System.Security.Cryptography.ProtectedData]::Protect(
        [System.Text.Encoding]::UTF8.GetBytes("sk-ds-mock"), $entropy,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $store["conn:ds-main"] = [Convert]::ToBase64String($protected)
    [System.IO.File]::WriteAllText($secretsPath, ($store | ConvertTo-Json -Compress))
}

# H 场景配置：双 tile 带预置悬浮位置（pinLayout.floatingX/Y，DIP=px @96dpi），panel 模式切位用
# $collapsed：pins.json/widgets.json 的 layout.collapsed 双写（场景 P 收起态还原用）
function Write-HConfig([string] $displayMode = "floating", [bool] $collapsed = $false)
{
    Stop-Beacon
    $collapsedJson = $collapsed.ToString().ToLowerInvariant()
    $config = @'
{
  "configVersion": 1,
  "hotkey": "Ctrl+Alt+B",
  "launchOnStartup": false,
  "theme": "dark",
  "pinDisplayMode": "__MODE__",
  "numericFloatingEnabled": true,
  "numericFloatingResetDone": true,
  "showCapsule": false
}
'@.Replace("__MODE__", $displayMode)
    Set-Content -Path (Join-Path $configDir "config.json") -Value $config -Encoding UTF8
    @'
[
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": true },
  { "id": "ds-main", "type": "deepseek", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ds-main", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    # pinLayout 权威双写：widgets.json 便利引用 + pins.json（EnsurePlaced 读 widget.PinLayout）
    @'
[
  { "id": "bigmodel.usage:GLM", "type": "bigmodel.usage", "connectionId": "zhipu", "refreshTier": "ci", "pinned": true, "config": { "label": "GLM" },
    "pinLayout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": __COLLAPSED__, "floatingX": 120, "floatingY": 120 } },
  { "id": "deepseek.balance:ds", "type": "deepseek.balance", "connectionId": "ds-main", "refreshTier": "ci", "pinned": true, "config": {},
    "pinLayout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": __COLLAPSED__, "floatingX": 260, "floatingY": 220 } }
]
'@.Replace("__COLLAPSED__", $collapsedJson) | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    @'
{
  "tiles": [
    { "widgetId": "bigmodel.usage:GLM", "layout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": __COLLAPSED__, "floatingX": 120, "floatingY": 120 } },
    { "widgetId": "deepseek.balance:ds", "layout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": __COLLAPSED__, "floatingX": 260, "floatingY": 220 } }
  ]
}
'@.Replace("__COLLAPSED__", $collapsedJson) | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8
    $secretsPath = Join-Path $configDir "secrets.bin"
    $store = @{}
    if (Test-Path $secretsPath)
    {
        (ConvertFrom-Json (Get-Content $secretsPath -Raw)).PSObject.Properties | ForEach-Object { $store[$_.Name] = $_.Value }
    }
    $entropy = [System.Text.Encoding]::UTF8.GetBytes("Beacon:conn:ds-main")
    $protected = [System.Security.Cryptography.ProtectedData]::Protect(
        [System.Text.Encoding]::UTF8.GetBytes("sk-ds-mock"), $entropy,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    $store["conn:ds-main"] = [Convert]::ToBase64String($protected)
    [System.IO.File]::WriteAllText($secretsPath, ($store | ConvertTo-Json -Compress))
}

function Save-FullScreenshot([string] $name)
{
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen($vs.X, $vs.Y, 0, 0, $bmp.Size)
    $graphics.Dispose()
    $bmp.Save((Join-Path $evidence $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "截图已存：evidence/$name"
}

function New-Crop([WinEnum+RECT] $rect, [int] $margin)
{
    $w = $rect.Right - $rect.Left + 2 * $margin
    $h = $rect.Bottom - $rect.Top + 2 * $margin
    $crop = New-Object System.Drawing.Bitmap $w, $h
    $g1 = [System.Drawing.Graphics]::FromImage($crop)
    $g1.CopyFromScreen($rect.Left - $margin, $rect.Top - $margin, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $g1.Dispose()
    return $crop
}

# 状态灯条带裁图（20×18 @ tile 内偏移 x6/y10）：G2 工件实测灯 bbox x13-20/y15-22，留边避 icon/文字；
# 360px 小图单帧扫描 ~5ms，支撑 ~15ms 周期连拍——过渡帧取证（场景 L）依赖高帧率小裁图
function New-LightStrip([WinEnum+RECT] $rect)
{
    $crop = New-Object System.Drawing.Bitmap 20, 18
    $g1 = [System.Drawing.Graphics]::FromImage($crop)
    $g1.CopyFromScreen($rect.Left + 6, $rect.Top + 10, 0, 0, (New-Object System.Drawing.Size 20, 18))
    $g1.Dispose()
    return $crop
}

function Save-Zoom([System.Drawing.Bitmap] $crop, [string] $name, [int] $zoom)
{
    $big = New-Object System.Drawing.Bitmap ($crop.Width * $zoom), ($crop.Height * $zoom)
    $g2 = [System.Drawing.Graphics]::FromImage($big)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g2.DrawImage($crop, 0, 0, $big.Width, $big.Height)
    $g2.Dispose()
    $big.Save((Join-Path $evidence $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $big.Dispose()
    Write-Host "近景截图已存：evidence/$name"
}

function Count-DarkPixels([System.Drawing.Bitmap] $bmp, [int] $luminanceBelow)
{
    $count = 0
    for ($y = 0; $y -lt $bmp.Height; $y++)
    {
        for ($x = 0; $x -lt $bmp.Width; $x++)
        {
            $p = $bmp.GetPixel($x, $y)
            if ((0.2126 * $p.R + 0.7152 * $p.G + 0.0722 * $p.B) -lt $luminanceBelow) { $count++ }
        }
    }
    return $count
}

# 屏幕矩形内「文字墨迹」像素数：唯一不撒谎的写入判据。窗口文本会骗人——38716060653 实证 GetWindowTextW
# 复核命中、截图裁框放大后框内仍空（子 Edit 是 ComboBoxEx 自绘面的无绘制缓冲区）。裁框内亮背景上的暗像素
# 数量即用户看得见的内容：空白框≈0（光标竖线至多数十），有文字则成排出现。
function Count-BoxInk([WinEnum+RECT] $rect)
{
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    if ($w -le 0 -or $h -le 0) { return 0 }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $g.Dispose()
    $n = Count-DarkPixels $bmp 120
    $bmp.Dispose()
    return $n
}

# 期望客户区高度（px）：设计 DIP × 该窗实测 DPI。取整口径与应用侧 DpiLayoutMath.ToPx 一致
# （MidpointRounding.AwayFromZero），100% 缩放下 32 DIP = 32px，runner 换缩放期望值跟着走。
function ExpectedRowPx([IntPtr] $hwnd, [double] $dips = 32)
{
    return [int][Math]::Round($dips * [WinEnum]::DpiScaleOf($hwnd), [MidpointRounding]::AwayFromZero)
}

# 客户区高度硬断言（2026-10-14 进度条缺陷单回归守卫）：AppWindow.Resize 设的是窗口外框，
# 客户区需求尺寸（tile 32 DIP@100%=32px）直接喂 Resize 会丢非客户区差值（~6px resize 边框），
# 底部 4 DIP 进度条整条落进不可见区——缺陷形态=客户区实测 26px、条带零像素（run 38488861749 取证：
# A 场景 GLM tile 26px 无条、D 场景 5 窗全 26px、H 场景面板末行 tile 26px 且首行完整 32px）。
# 客户区高必须等于设计值 ±1（亚像素阈值口径同 todo 决策记录：状态灯 8px 下限同理）。
function Assert-ClientHeight([IntPtr] $hwnd, [int] $expectedPx, [string] $label)
{
    $client = [WinEnum]::ClientRectOf($hwnd)
    $frame = [WinEnum]::RectOf($hwnd)
    $clientH = $client.Bottom - $client.Top
    $frameH = $frame.Bottom - $frame.Top
    if ([Math]::Abs($clientH - $expectedPx) -gt 1)
    {
        throw "断言失败（$label）：客户区高 ${clientH}px（期望 ${expectedPx}px ±1，外框 ${frameH}px）——非客户区差值未补回，底部进度条落进不可见区（缺陷 2026-10-14 回归）"
    }
    Write-Host "$label 客户区断言通过：client ${clientH}px / frame ${frameH}px（chrome $($frameH - $clientH)px）"
}

# 进度条像素硬断言助手（2026-10-10 修「进度条看不到」配套，2026-10-11 重设计后 bar 为默认路径；
# 2026-10-14 缺陷单改客户区锚定）：入参必须是客户区裁图（ClientRectOf，非外框 RectOf）——
# 旧实现裁外框、底部 6 行落在窗框外的桌面区，「465/507 像素」全是桌面边缘（vacuous 通过实证）。
# 扫底部 6 物理像素行、中部 60% 宽（避开 2 DIP 圆角端头与 NN% 读数列），统计与暗底色
# (15,23,42) 任一通道差 >25 的像素。
# bar 缺席=0（CI 截图实证过的旧病）；bar 在场：track(≈62,72,92)+fill(档位色) 盖满 4px 高带 → 数百，
# 阈值 120 ≈ 期望值的 30%，容忍圆角/窗口边 1-2 行的毛边。
# 只对 dark 主题（A/D 场景）成立；浅主题底色不同不适用（C 场景走暗像素断言）。
function Count-BarPixels([System.Drawing.Bitmap] $bmp)
{
    $count = 0
    $xStart = [int]($bmp.Width * 0.2)
    $xEnd = [int]($bmp.Width * 0.8)
    for ($y = [Math]::Max(0, $bmp.Height - 6); $y -lt $bmp.Height; $y++)
    {
        for ($x = $xStart; $x -lt $xEnd; $x++)
        {
            $p = $bmp.GetPixel($x, $y)
            if ([Math]::Abs($p.R - 15) -gt 25 -or [Math]::Abs($p.G - 23) -gt 25 -or [Math]::Abs($p.B - 42) -gt 25) { $count++ }
        }
    }
    return $count
}

# —— UIA 助手（Windows PowerShell 5.1 程序集） ——
function Find-UiaById([System.Windows.Automation.AutomationElement] $root, [string] $automationId, [int] $timeoutSec = 12)
{
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline)
    {
        $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 400
    }
    return $null
}

# 按 Name 找元素（场景 U ContentDialog 按钮无 x:Name，AutomationId 不可用）——与 Find-UiaById 同款超时轮询
function Find-UiaByName([System.Windows.Automation.AutomationElement] $root, [string] $name, [int] $timeoutSec = 10)
{
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline)
    {
        $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 400
    }
    return $null
}

function Invoke-Uia([System.Windows.Automation.AutomationElement] $element)
{
    ($element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
}

function Find-SettingsWindow([int] $timeoutSec = 30)
{
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Beacon 设置")
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline)
    {
        $found = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Wait-Feedback([System.Windows.Automation.AutomationElement] $settings, [string] $contains, [int] $timeoutSec = 25)
{
    $feedback = Find-UiaById $settings "conn-feedback" 10
    if (!$feedback) { throw "UIA 未找到 conn-feedback 文本（设置页结构变化？）" }
    $name = ""
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline)
    {
        $name = ""
        try { $name = $feedback.Current.Name } catch { }
        if ($name -like "*$contains*") { return $name }
        Start-Sleep -Milliseconds 500
    }
    throw "连接测试反馈 25s 内未出现「$contains」（实际：$name）"
}

function Expand-ConnectionDropdown([System.Windows.Automation.AutomationElement] $settings)
{
    $combo = Find-UiaById $settings "widget-connection-box" 10
    if (!$combo) { throw "UIA 未找到 widget-connection-box（组件向导下拉）" }
    ($combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand()
    Start-Sleep -Seconds 1
    return $combo
}

function Collapse-ConnectionDropdown([System.Windows.Automation.AutomationElement] $combo)
{
    try { ($combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Collapse() } catch { }
    Start-Sleep -Milliseconds 400
}

function Get-WindowUiaTexts([IntPtr] $handle)
{
    $element = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    $found = $element.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $texts = @()
    foreach ($item in $found)
    {
        try { if ($item.Current.Name) { $texts += $item.Current.Name } } catch { }
    }
    return $texts
}

# 结构化树遍历：按 x:Name→AutomationId 锚定单个列表，只收其子树行文本（滤除纯 HH:mm:ss 时间串）
# ——事件行与状态行同出 MakeStateRow（点+时间+摘要），平铺 Name 列表分不开两区；场景 S 依此分行取证
function Get-UiaListSummaries([System.Windows.Automation.AutomationElement] $root, [string] $listId)
{
    $list = Find-UiaById $root $listId 5
    if (!$list) { return $null } # 收起整块可能不在 UIA 树（StatusPanel Collapsed）——调用方按「取不到=无行」解读
    $found = $list.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $summaries = @()
    foreach ($item in $found)
    {
        try
        {
            $name = $item.Current.Name
            if ($name -and $name -notmatch '^\d{2}:\d{2}:\d{2}$') { $summaries += $name }
        }
        catch { }
    }
    return ,@($summaries)
}

# 场景 X 用：按摘要文本锚定 RecentList 事件行，返回右键落点元素。命中元素沿控制视图向上找 ListItem
# 祖先取整行矩形；无 ListItem（InvokeableListItem 自定义 peer，不报 ListItem ControlType——run
# 38571373088 实证 ControlType 过滤 40s 零命中）时停在命中元素自身，绝不越界到 ListView 整框。
function Find-PanelRow([System.Windows.Automation.AutomationElement] $root, [string] $nameRegex)
{
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    foreach ($item in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition))
    {
        try
        {
            if ($item.Current.Name -notmatch $nameRegex) { continue }
            $rowAnchor = $item
            $node = $item
            for ($depth = 0; $depth -lt 8; $depth++)
            {
                $parent = $walker.GetParent($node)
                if ($null -eq $parent) { break }
                if ($parent.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { $rowAnchor = $parent; break }
                $node = $parent
            }
            return $rowAnchor
        }
        catch { }
    }
    return $null
}

# AF 用：按摘要文本锚定行后取「支持 InvokePattern 的最近祖先」＝InvokeableListItem 行本体。
# 不走 ControlType==ListItem 过滤（X 实证该口径在 RecentList 零命中；且行内容为 Grid 时行自身
# Name 为空，「ListItem 元素 Name 匹配」两坑叠加——run 40887021700 面板明明渲染 GLM 行却 60s 零命中）。
# Name 命中沿用 Get-UiaListSummaries 的 TrueCondition 口径（子 TextBlock 即可命中），再沿控制视图
# 向上找首个带 Invoke 的元素——行 peer 挂 Invoke（InvokeableListItemPeer），ListView 容器不挂，
# 向上第一站即行本体，不越界到整框。
function Find-InvokableRowByName([System.Windows.Automation.AutomationElement] $root, [string] $nameRegex)
{
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    foreach ($item in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition))
    {
        try
        {
            if ($item.Current.Name -notmatch $nameRegex) { continue }
            $node = $item
            for ($depth = 0; $depth -lt 8; $depth++)
            {
                try
                {
                    $pattern = $node.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
                    if ($pattern) { return $node }
                }
                catch { }
                $parent = $walker.GetParent($node)
                if ($null -eq $parent) { break }
                $node = $parent
            }
        }
        catch { }
    }
    return $null
}

# UIA 点击落点解析（X1 行右键/菜单点击、W3 chip 点击共用）：38619441591 X 红实证——RecentList 行被
# 虚拟化或刷新 tick 重建中途时，UIA BoundingRectangle 返 NaN 矩形，[int] 转换直接炸（"Cannot convert
# value NaN to Int32"），无任何断言消息。优先 GetClickablePoint（元素不可点时抛 PointUnavailableException
# 而非返 NaN），退回矩形中心；NaN/空/解析失败一律返 $null，由调用方判失败或整链重试。
function Get-ClickPoint([System.Windows.Automation.AutomationElement] $element)
{
    try
    {
        $pt = $element.GetClickablePoint()
        if (-not [double]::IsNaN($pt.X) -and -not [double]::IsNaN($pt.Y)) { return @( [int]$pt.X, [int]$pt.Y ) }
    }
    catch { }
    $r = $null
    try { $r = $element.Current.BoundingRectangle } catch { }
    if ($null -eq $r -or $r.IsEmpty -or [double]::IsNaN($r.X) -or [double]::IsNaN($r.Y) -or [double]::IsNaN($r.Width) -or [double]::IsNaN($r.Height)) { return $null }
    return @( [int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2) )
}

# 组件中心窗定位（场景 AE）：SampleGalleryWindow Title 恒「Beacon · 组件中心」（与 Find-SettingsWindow 同款）
function Find-GalleryWindow([int] $timeoutSec = 20)
{
    $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Beacon · 组件中心")
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline)
    {
        $found = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

# 样式条目点击（场景 AE）：坐标点击 + 选中态回读 + 表单头回读 + 重试。两轮红跑演进：
# ① 40547402881（c4b0358 修）：点击落偏静默失败 → 补 IsSelected 回读 + 三连重试；
# ② 40560895375：IsSelected=true 但表单仍旧模板——WinUI ItemClick 依赖完整指针语义，合成点击只翻
#    选中位不触发事件（选中高亮≠切换生效）。故回读升级为语义级：给 $expectText（表单头「样式 · 数字卡」
#    之类），IsSelected 通过后还要窗口文本里等到它；产品侧已补 SelectionChanged 兜底驱动切换，双保险。
# 三连未达预期才红，红前留全屏取证。
function Invoke-StyleItemClick([IntPtr] $hwnd, [string] $itemId, [string] $expectText)
{
    foreach ($attempt in 1..3)
    {
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
        $item = Find-UiaById $root $itemId 8
        if (!$item) { throw "断言失败（场景 AE）：UIA 未找到 $itemId（第 $attempt 次重找仍无）" }
        [WinEnum]::ForceForeground($hwnd)
        Start-Sleep -Milliseconds 400
        $point = Get-ClickPoint $item
        if (!$point) { Save-FullScreenshot "AE-FAIL-no-point-$itemId-$attempt.png"; throw "断言失败（场景 AE）：$itemId 取不到可点坐标（第 $attempt 次）" }
        [WinEnum]::ClickAt($point[0], $point[1])
        $deadline = (Get-Date).AddSeconds(6)
        while ((Get-Date) -lt $deadline)
        {
            $fresh = Find-UiaById ([System.Windows.Automation.AutomationElement]::FromHandle($hwnd)) $itemId 3
            if ($fresh)
            {
                try
                {
                    $sel = $fresh.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
                    if ($sel.Current.IsSelected)
                    {
                        if (!$expectText) { return }
                        $texts = Get-WindowUiaTexts $hwnd
                        if (@($texts | Where-Object { $_ -eq $expectText }).Count -gt 0) { return }
                    }
                }
                catch { }
            }
            Start-Sleep -Milliseconds 400
        }
    }
    Save-FullScreenshot "AE-FAIL-click-$itemId.png"
    throw "断言失败（场景 AE）：$itemId 坐标点击三连后表单头未见「$expectText」（选中翻但切换未生效，或点偏——见 AE-FAIL-click 截图）"
}

function Select-ComboItemById([System.Windows.Automation.AutomationElement] $settings, [System.Windows.Automation.AutomationElement] $combo, [string] $itemId)
{
    ($combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand()
    Start-Sleep -Seconds 1
    $item = Find-UiaById $settings $itemId 8
    if (!$item)
    {
        # 展开弹层可能是独立 HWND：退回桌面根子树找
        $item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $itemId)))
    }
    if (!$item) { throw "UIA 未找到下拉选项 $itemId（展开后仍未见）" }
    ($item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    Start-Sleep -Milliseconds 400
}

# 下拉当前选中项文本（SelectionPattern 首 selection 的 Name）——密度档回读断言用（场景 AA）
function Get-ComboSelectionName([System.Windows.Automation.AutomationElement] $combo)
{
    $sel = ($combo.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)).Current.GetSelection()
    if ($sel.Length -eq 0) { return "" }
    try { return $sel[0].Current.Name } catch { return "" }
}

# 按选项完整 Name 选中（Select-ComboItemById 的 AutomationId 口径不可得时用 Name；
# WinUI 弹层可能独立 HWND——combo 子树找不到时退桌面根子树找，同款双域兜底）
function Select-ComboItemByName([System.Windows.Automation.AutomationElement] $combo, [string] $name)
{
    ($combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand()
    Start-Sleep -Seconds 1
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $item = $combo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
    if (!$item)
    {
        $item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
    }
    if (!$item) { throw "UIA 未找到下拉选项「$name」（展开后仍未见）" }
    ($item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    Start-Sleep -Milliseconds 400
}


function Assert-DropdownHasItem([System.Windows.Automation.AutomationElement] $settings, [string] $itemId, [string] $what)
{
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $itemId)
    $found = $settings.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
    if (!$found)
    {
        # 展开弹层可能是独立 HWND：退回桌面根子树找
        $found = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCondition)
    }
    if (!$found) { throw "断言失败：组件「连接」下拉中未见 $itemId（$what）——下拉未列出可选项" }
    Write-Host "下拉断言通过：$what 含 $itemId"
}

function Save-UiaTreeDump([System.Windows.Automation.AutomationElement] $root, [string] $fileName)
{
    $lines = New-Object System.Collections.Generic.List[string]
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $queue = New-Object System.Collections.Queue
    $queue.Enqueue(@($root, 0))
    $count = 0
    while ($queue.Count -gt 0 -and $count -lt 300)
    {
        $item = $queue.Dequeue()
        $el = $item[0]
        $depth = $item[1]
        try
        {
            $lines.Add(("  " * $depth) + $el.Current.ControlType.ProgrammaticName + " id=[" + $el.Current.AutomationId + "] name=[" + $el.Current.Name + "]")
        }
        catch { }
        $count++
        if ($depth -lt 6)
        {
            # 走树调用也须入 try：跨进程 provider 不响应时 GetFirstChild 抛 UIA_E_TIMEOUT（0x80131505），
            # 38689737249 实证异常从 dump 逃逸把整个场景带红——诊断面不得反杀场景。
            try
            {
                $child = $walker.GetFirstChild($el)
                while ($child)
                {
                    $queue.Enqueue(@($child, ($depth + 1)))
                    $child = $walker.GetNextSibling($child)
                }
            }
            catch { }
        }
    }
    [System.IO.File]::WriteAllLines((Join-Path $evidence $fileName), $lines)
}

# 顶层窗句柄快照（EnumWindows 地面真值，跨进程）：WinRT picker 由外部进程承载（PickerHost 等），
# 按 class=#32770 + pid(beacon) 找必空（38678998804 实证）；38683714647 再证 UIA 桌面根 Children 枚举
# 也漏它——截图里「Save As」对话框确在（File name 预填、Save/Cancel 齐全），同时刻 UIA 桌面清单无此窗。
# 故「点前快照 → 点后差集」全程走 EnumWindows（Win32 层，与 UIA 桌面树/provider 注册无关）。
function Get-DesktopWindowKeys
{
    $keys = New-Object System.Collections.Generic.List[string]
    foreach ($h in [WinEnum]::VisibleTopLevelHandles())
    {
        $keys.Add([string]$h)
    }
    return $keys
}

# 桌面顶层窗清单 dump（诊断面）：EnumWindows 枚举，Win32 title/class/pid 恒可得，再挂 UIA 视角
# （AutomationElement.FromHandle → ControlType/Name）——若 picker 有 handle 但 uia=<unavailable>，
# 即「UIA 桌面树漏窗 + 无 UIA provider」双失效，驱动只能转纯 Win32（SetWindowText+SendInput）。
function Save-DesktopWindowDump([string] $fileName)
{
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($h in [WinEnum]::VisibleTopLevelHandles())
    {
        $head = "hwnd=[" + [string]$h + "] title=[" + [WinEnum]::TitleOf($h) + "] class=[" + [WinEnum]::ClassOf($h) + "] pid=[" + [WinEnum]::PidOf($h) + "]"
        try
        {
            $el = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$h)
            $lines.Add($head + " uia=[" + $el.Current.ControlType.ProgrammaticName + " name=[" + $el.Current.Name + "]]")
        }
        catch { $lines.Add($head + " uia=<unavailable>") }
    }
    [System.IO.File]::WriteAllLines((Join-Path $evidence $fileName), $lines)
}

# 等新顶层对话框窗（纯 Win32）：EnumWindows 全量枚举取「点前快照之外」的新窗，标题命中
# titleHints（*like*）优先，无命中取首个新窗；返回 hwnd（IntPtr.Zero=超时未现）。跨进程、与
# window class / UIA 桌面树 / provider 响应性全部无关（38683714647/38689737249 实证 UIA 全盲）。
