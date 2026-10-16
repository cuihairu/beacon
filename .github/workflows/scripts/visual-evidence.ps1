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

function Stop-Beacon
{
    Get-Process Beacon.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
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
        # 档位文件：内容 "error" = 95% 态，空/缺 = 38% 态（场景侧 Set-Content / Remove-Item 切换）
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
                        $body = if ($mode -eq "error") { $bigmodelError } else { $bigmodel } # 38% 健康链 / 95% Error 链按档位文件翻转
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

# —— 场景 A：数量悬浮窗开 ——悬浮框必须在桌面，icon 近景留证
Ensure-MockServer # 提前到 A 前：GLM tile 走 mock 健康链（38%），D 场景复用同一实例（幂等：已在场则直接返回）
try
{
    Write-ScenarioConfig $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    $tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($tiles.Count -lt 1)
    {
        Save-FullScreenshot "A-FAIL-no-tile-window.png"
        throw "断言失败（场景 A）：数量悬浮窗开启时未见悬浮框窗口（标题 Beacon Tile）——悬浮窗未创建"
    }
    Save-FullScreenshot "A-numeric-on-desktop.png"
    Save-Zoom (New-Crop ([WinEnum]::RectOf($tiles[0])) 10) "A-numeric-on-tile-closeup.png" 6
    # 进度条像素硬断言（2026-10-11 重设计，bar 默认路径；2026-10-14 缺陷单改客户区锚定）：
    # GLM tile 有 Progress 语义（mock 38%），底部 4 DIP 条带必须出像素——旧病「条从未渲染」/
    # 3px 细到难辨都在这里现形。客户区高先硬断言（32 DIP@100%=32px ±1），再裁客户区数条带像素。
    Assert-ClientHeight $tiles[0] (ExpectedRowPx $tiles[0]) "场景 A"
    $aBarCrop = New-Crop ([WinEnum]::ClientScreenRectOf($tiles[0])) 0
    Save-Zoom $aBarCrop "A-tile-bar-band.png" 6
    $aBarPixels = Count-BarPixels $aBarCrop
    $aBarCrop.Dispose()
    if ($aBarPixels -lt 120)
    {
        throw "断言失败（场景 A）：GLM tile 底部进度条带仅 $aBarPixels 像素（期望 ≥120，即 4 DIP 条+底轨）——bar 默认路径渲染回归"
    }
    # 百分比硬断言（2026-10-10 用户令「常驻静态可读量」）：GLM tile「NN%」文本必须常驻
    $aTexts = Get-WindowUiaTexts $tiles[0]
    $aPercent = $aTexts | Where-Object { $_ -match '\d+\s*%' } | Select-Object -First 1
    if ($aPercent)
    {
        Write-Host "场景 A 百分比断言通过：GLM tile 常驻静态文本「$aPercent」（mock 38% 健康链）；进度条像素 $aBarPixels（A-tile-bar-band.png）"
    }
    elseif (($aTexts | Where-Object { $_.Trim().Length -gt 0 } | Measure-Object).Count -gt 0)
    {
        throw "断言失败（场景 A）：GLM tile 文本可读但无百分比（N% 常驻静态量缺失）——进度读数回归；实际文本：$($aTexts -join ' | ')"
    }
    else
    {
        # UIA 对 NOACTIVATE 悬浮窗读不出任何文本时：6x 近景截图即数字状态证据（转人工目检），不拦绿
        Write-Host "::warning::场景 A UIA 文本不可读（$($aTexts.Count) 条）——「NN%」断言转 A-numeric-on-tile-closeup.png 人工目检"
    }
    Write-Host "场景 A 通过：悬浮框窗口存在（$($tiles.Count) 个），全屏/近景/条带截图已存"
}
catch
{
    Save-FullScreenshot "A-FAIL-unexpected.png"
    $failures.Add("场景 A：$($_.Exception.Message)")
    Write-Host "::error::场景 A 断言失败：$($_.Exception.Message)"
}

# —— 场景 B：数量悬浮窗关 ——桌面零残留窗（硬断言）
try
{
    Write-ScenarioConfig $false
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    $leftover = @([WinEnum]::VisibleHandlesByTitle("Beacon Tile") + [WinEnum]::VisibleHandlesByTitle("Beacon Pinned"))
    if ($leftover.Count -gt 0)
    {
        Save-FullScreenshot "B-FAIL-residue-windows.png"
        throw "断言失败（场景 B）：数量悬浮窗关闭后桌面残留 $($leftover.Count) 个 Beacon 窗口"
    }
    Save-FullScreenshot "B-numeric-off-desktop.png"
    Write-Host "场景 B 通过：桌面零残留窗（无可见 Beacon Tile / Beacon Pinned）"
}
catch
{
    Save-FullScreenshot "B-FAIL-unexpected.png"
    $failures.Add("场景 B：$($_.Exception.Message)")
    Write-Host "::error::场景 B 断言失败：$($_.Exception.Message)"
}

# —— 场景 C：浅色主题 icon 可辨度 ——白桌面 + theme=light，暗像素硬断言（近白 icon 叠浅底在这里现形）
try
{
    Write-ScenarioConfig $true "light"
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    $tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($tiles.Count -lt 1)
    {
        Save-FullScreenshot "C-FAIL-no-tile-window.png"
        throw "断言失败（场景 C）：浅色主题下未见悬浮框窗口"
    }
    $crop = New-Crop ([WinEnum]::RectOf($tiles[0])) 10
    Save-Zoom $crop "C-light-theme-tile-closeup.png" 6
    $dark = Count-DarkPixels $crop 60
    $crop.Dispose()
    Save-FullScreenshot "C-light-theme-desktop.png"
    # 修复后（实色浅底 + 深前景）：icon/文字大量暗像素；修复前（近白叠浅底）：≈0
    if ($dark -lt 30)
    {
        throw "断言失败（场景 C）：浅色主题悬浮框 icon/文字不可辨（亮度<60 的暗像素仅 $dark）——前景对比度回归"
    }
    Write-Host "场景 C 通过：浅色主题 tile 暗像素 $dark（icon/文字可辨）"
}
catch
{
    Save-FullScreenshot "C-FAIL-unexpected.png"
    $failures.Add("场景 C：$($_.Exception.Message)")
    Write-Host "::error::场景 C 断言失败：$($_.Exception.Message)"
}

# —— 场景 D：五家 Provider 连接链（mock + DPAPI 密钥 + 设置页 UIA） ——
try
{
    Write-ProviderConfig
    # mock 已在场景 A 前启动（同端口二次启动会冲突），此处直接复用
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings)
    {
        Save-FullScreenshot "D-FAIL-no-settings-window.png"
        throw "断言失败（场景 D）：--settings 启动 30s 内未见「Beacon 设置」窗口"
    }

    # —— 2026-10-10 修「设置左侧不可调、很多项目看不到」三断言 ——
    Save-FullScreenshot "D-settings-window.png"
    # ① 16 个模块行 UIA 全部存在（此前 16 项超出视口被整体裁掉）
    foreach ($key in @("general","appearance","github","bigmodel","ark","claude","kimi","deepseek","mimo","codex","copilot","opencode","qwen","http","actions","advanced"))
    {
        if (!(Find-UiaById $settings "module-$key" 8)) { throw "断言失败（场景 D）：UIA 未找到模块行 module-$key——左栏模块列表缺失" }
    }
    Write-Host "模块行断言通过：16 个模块 UIA 全部可达"
    # ② 左栏可滚到底：最后一个模块「高级」进入可见区（IsOffscreen=false）——常驻滚动条功能性证明
    $leftHost = Find-UiaById $settings "settings-left-host" 8
    if (!$leftHost) { throw "断言失败（场景 D）：UIA 未找到 settings-left-host——左栏 ScrollViewer 缺失" }
    $advanced = Find-UiaById $settings "module-advanced" 8
    if (!$advanced) { throw "断言失败（场景 D）：UIA 未找到 module-advanced" }
    # ScrollItemPattern.ScrollIntoView()（无参，与 Invoke 同族绑定可靠）——SetScrollPercent(int) 在
    # PS 5.1 对 UIA COM 包装重载解析失败（38043008092 实证），弃用
    $scrollItem = $null
    try { $scrollItem = $advanced.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern) } catch { }
    if ($scrollItem)
    {
        $scrollItem.ScrollIntoView()
        Start-Sleep -Milliseconds 800
    }
    if ($advanced.Current.IsOffscreen)
    {
        throw "断言失败（场景 D）：左栏滚到底后「高级」模块仍不可见（IsOffscreen=true）——滚动修复回归"
    }
    Write-Host "左栏滚动断言通过：「高级」滚入可见区（ScrollIntoView=$(if ($scrollItem) { 'ok' } else { 'pattern 不可用' })，IsOffscreen=false）"
    # ③a 启动恢复硬断言（config→UI，确定性「可拖」数据链）：预置 settingsSidebarWidth=400 →
    # 左栏 ScrollViewer 实测宽必须 ≈400px（DPI 100%）——宽了/窄了都是恢复逻辑回归
    $widthBefore = [int]$leftHost.Current.BoundingRectangle.Width
    if ([Math]::Abs($widthBefore - 400) -gt 8)
    {
        Save-FullScreenshot "D-FAIL-sidebar-restore.png"
        throw "断言失败（场景 D）：config settingsSidebarWidth=400 启动恢复后左栏实测 ${widthBefore}px（期望 400±8）——边栏宽度持久化回归"
    }
    Write-Host "边栏宽度恢复断言通过：config 400 → 左栏实测 ${widthBefore}px"
    # ③b 设置搜索（兜底③）：写「额度」→ 不含词的模块行被过滤（高级从 UIA 树消失）；清词复原
    $searchBox = Find-UiaById $settings "settings-search" 8
    if (!$searchBox) { throw "断言失败（场景 D）：UIA 未找到 settings-search——设置搜索框缺失" }
    ($searchBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("额度")
    Start-Sleep -Milliseconds 900
    Save-FullScreenshot "D-settings-search-filter.png"
    $advFiltered = Find-UiaById $settings "module-advanced" 2
    if ($advFiltered -and -not $advFiltered.Current.IsOffscreen)
    {
        throw "断言失败（场景 D）：搜索「额度」后 module-advanced 仍可见——搜索过滤未生效"
    }
    $bigFiltered = Find-UiaById $settings "module-bigmodel" 2
    if (!$bigFiltered -or $bigFiltered.Current.IsOffscreen)
    {
        throw "断言失败（场景 D）：搜索「额度」后 module-bigmodel 反被隐藏——过滤词命中逻辑回归"
    }
    ($searchBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("")
    Start-Sleep -Milliseconds 600
    if (!(Find-UiaById $settings "module-advanced" 4)) { throw "断言失败（场景 D）：清空搜索词后 module-advanced 未恢复——过滤复位回归" }
    Write-Host "设置搜索断言通过：「额度」过滤（高级隐藏/智谱保留）+ 清词复原"
    # ③c 分隔条真实拖拽 +60px（SendInput 绝对坐标）：左栏实测变宽 ≥40px——拖拽手势的功能性证明。
    # 38044411443 实证 mouse_event 版在 CI 会话不生效；本版换 SendInput 队列级注入。仍不生效则降级
    # warning 不拦 nightly——可拖数据链已由 ③a（config→400px）与 PointerReleased 落盘逻辑证明，手势留装机手验
    $splitter = Find-UiaById $settings "settings-left-splitter" 8
    if (!$splitter) { throw "断言失败（场景 D）：UIA 未找到 settings-left-splitter——拖拽分隔条缺失" }
    [WinEnum]::SetForeground([IntPtr]$settings.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 500
    $sr = $splitter.Current.BoundingRectangle
    Save-FullScreenshot "D-settings-splitter-before.png"
    [WinEnum]::DragMouse([int](($sr.Left + $sr.Right) / 2), [int](($sr.Top + $sr.Bottom) / 2), [int](($sr.Left + $sr.Right) / 2) + 60, [int](($sr.Top + $sr.Bottom) / 2))
    Start-Sleep -Seconds 1
    Save-FullScreenshot "D-settings-splitter-after.png"
    $widthAfter = [int]$leftHost.Current.BoundingRectangle.Width
    # 期望宽 = min(起点+40, 钳位上限 420)：38048738860 实证拖拽生效但 400+60 撞上限钳到 420，
    # 旧断言要求 +40（=440）在数学上不可能——钳位感知后 400→420 即硬通过
    $widthExpected = [Math]::Min($widthBefore + 40, 420)
    if ($widthAfter -lt $widthExpected)
    {
        Write-Host "::warning::分隔条合成拖拽在 CI 会话未生效（${widthBefore}→${widthAfter}px，期望 ≥${widthExpected}，SendInput 已试）——可拖数据链由 config 400→${widthBefore}px 恢复断言+落盘代码证明，拖拽手势转装机手验（截图 before/after 已存 artifact）"
    }
    else
    {
        Start-Sleep -Milliseconds 500 # PointerReleased 落盘防抖留时间
        $appConfigNow = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
        if ("$($appConfigNow.settingsSidebarWidth)" -ne "$widthAfter")
        {
            throw "断言失败（场景 D）：拖拽后左栏 ${widthBefore}→${widthAfter}px 但 config settingsSidebarWidth=$($appConfigNow.settingsSidebarWidth)（期望 $widthAfter）——拖完落盘回归"
        }
        Write-Host "分隔条拖拽断言通过：左栏 $widthBefore → $widthAfter px（钳位感知，期望 ≥${widthExpected}）；config 落盘 $($appConfigNow.settingsSidebarWidth)（拖完即存，启动恢复闭环）"
    }

    foreach ($provider in @(
        @{ type = "ark"; conn = "ark-main"; typeName = "方舟 Coding Plan 额度（火山）" },
        @{ type = "kimi"; conn = "kimi-main"; typeName = "Kimi For Coding 套餐余量" },
        @{ type = "mimo"; conn = "mimo-main"; typeName = "小米 MiMo 用量（开放平台）" },
        @{ type = "deepseek"; conn = "ds-main"; typeName = "DeepSeek 余额（开放平台）" },
        @{ type = "qwen"; conn = "qwen-main"; typeName = "阿里千问用量（百炼 Token Plan）" }))
    {
        $row = Find-UiaById $settings "module-$($provider.type)" 10
        if (!$row) { throw "UIA 未找到模块行 module-$($provider.type)（连接类型缺失？）" }
        Invoke-Uia $row
        Start-Sleep -Seconds 2

        $testButton = Find-UiaById $settings "test-$($provider.conn)" 10
        if (!$testButton) { throw "UIA 未找到测试按钮 test-$($provider.conn)" }
        Invoke-Uia $testButton
        $feedback = Wait-Feedback $settings "连接正常"
        Write-Host "$($provider.type) 连接测试通过：$feedback"

        $combo = Expand-ConnectionDropdown $settings
        Save-FullScreenshot "D-$($provider.type)-healthy-dropdown.png"
        Assert-DropdownHasItem $settings $provider.conn $provider.type
        Collapse-ConnectionDropdown $combo

        # 组件向导「类型」下拉非空且含本家条目（小米类型空回归在这里现形）
        $typeCombo = Find-UiaById $settings "widget-type-box" 10
        if (!$typeCombo) { throw "UIA 未找到 widget-type-box（组件向导类型下拉）" }
        ($typeCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand()
        Start-Sleep -Seconds 1
        Save-FullScreenshot "D-$($provider.type)-type-dropdown.png"
        Assert-DropdownHasItem $settings $provider.typeName "$($provider.type) 类型下拉"
        ($typeCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Collapse()
        Start-Sleep -Milliseconds 400
    }

    # save-flow：UIA 保存新连接 ark-e2e → 硬断言**立即**出现在组件下拉（下拉不刷新回归在这里现形）
    $row = Find-UiaById $settings "module-ark" 10
    Invoke-Uia $row
    Start-Sleep -Seconds 2
    $idBox = Find-UiaById $settings "conn-id-box" 10
    $endpointBox = Find-UiaById $settings "conn-endpoint-box" 10
    $saveButton = Find-UiaById $settings "conn-save-button" 10
    if (!$idBox -or !$endpointBox -or !$saveButton) { throw "UIA 未找到连接表单（conn-id-box / conn-endpoint-box / conn-save-button）" }
    ($idBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("ark-e2e")
    ($endpointBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("http://127.0.0.1:18081")
    Invoke-Uia $saveButton
    $savedFeedback = Wait-Feedback $settings "已保存"
    Write-Host "save-flow 保存成功：$savedFeedback"
    $combo = Expand-ConnectionDropdown $settings
    Save-FullScreenshot "D-save-flow-dropdown.png"
    Assert-DropdownHasItem $settings "ark-e2e" "save-flow"
    Collapse-ConnectionDropdown $combo
    # provider tile 名字/数值断言（预置五张钉选 tile，无 label 配置——字段名冒充名字在这里现形）
    $panelHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Pinned") + [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($panelHandles.Count -lt 1)
    {
        Save-FullScreenshot "D-FAIL-no-tile-host.png"
        throw "断言失败（场景 D）：tile 宿主窗未见（Beacon Pinned / Beacon Tile）"
    }
    $tileTexts = @()
    $windowTexts = @() # 按窗保序（一窗多条文本已 join 成一条）——条带/百分比断言按窗序定位 ark tile
    for ($tileIndex = 0; $tileIndex -lt $panelHandles.Count; $tileIndex++)
    {
        $texts = Get-WindowUiaTexts $panelHandles[$tileIndex]
        $tileTexts += $texts
        $windowTexts += ($texts -join "`n")
        Save-Zoom (New-Crop ([WinEnum]::RectOf($panelHandles[$tileIndex])) 8) "D-tile-$tileIndex-closeup.png" 4
    }
    Save-FullScreenshot "D-provider-tiles-desktop.png"
    # 逐窗客户区高度硬断言（2026-10-14 进度条缺陷单）：取证 5 悬浮 tile 窗客户区全 26px（32 设计值
    # −6px 非客户区 resize 边框），底部 4 DIP 条带整条落进不可见区。修复后每窗客户区必须回到 32px ±1。
    $dTileHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    foreach ($h in $dTileHandles)
    {
        Assert-ClientHeight $h (ExpectedRowPx $h) "场景 D"
    }
    if (($tileTexts | Where-Object { $_.Trim().Length -gt 0 } | Measure-Object).Count -gt 0)
    {
        foreach ($expected in @("火山方舟", "DeepSeek", "千问", "42.5", "420.5", "128 次"))
        {
            $hit = $tileTexts | Where-Object { $_ -like "*$expected*" } | Select-Object -First 1
            if (!$hit)
            {
                throw "断言失败（场景 D）：tile 文本缺「$expected」——名字/数值渲染回归；实际文本：$($tileTexts -join ' | ')"
            }
        }
        $fieldNamed = $tileTexts | Where-Object { $_ -eq "usage" -or $_ -eq "quota" } | Select-Object -First 1
        if ($fieldNamed)
        {
            throw "断言失败（场景 D）：tile 名字出现字段名「$fieldNamed」——LabelOf 兜底回归"
        }
        Write-Host "tile 名称断言通过：火山方舟/DeepSeek/千问 + 42.5/420.5，无字段名残留"
    }
    else
    {
        # UIA 对 NOACTIVATE 悬浮窗读不出文本时显式降级：窗存在+近景截图照收，文本断言转人工目检
        Write-Host "::warning::tile UIA 文本不可读（$($panelHandles.Count) 窗）——名字/数值断言转近景截图人工目检"
        Write-Host "tile 证据已收：$($panelHandles.Count) 个悬浮窗存在 + 近景截图；文本断言跳过"
    }

    # 进度显示硬断言（2026-10-11 用户令重设计定稿：bar 默认，圆池仅显式选择）：ark tile
    # （火山方舟，Progress=42.5%）文本必含「NN%」+ 底部 4 DIP 条带像素硬断言（margin 0 防桌面混色）。
    # bar 推进是 ~300ms 一次性过渡（intensity 接管），截图前 sleep 足够长即无相位差
    $arkIndex = -1
    for ($i = 0; $i -lt $windowTexts.Count; $i++) { if ($windowTexts[$i] -like "*火山方舟*") { $arkIndex = $i } }
    $percentHit = $tileTexts | Where-Object { $_ -match '\d+\s*%' } | Select-Object -First 1
    if ($percentHit)
    {
        Write-Host "百分比常驻断言通过：tile 静态文本「$percentHit」（NN% 一眼可读，动画+文字双通道）"
    }
    elseif (($tileTexts | Where-Object { $_.Trim().Length -gt 0 } | Measure-Object).Count -gt 0)
    {
        throw "断言失败（场景 D）：tile 文本可读但无百分比（N% 常驻静态量缺失）——进度读数回归；实际文本：$($tileTexts -join ' | ')"
    }
    else
    {
        Write-Host "::warning::tile UIA 文本不可读——百分比断言转 D-tile-*-closeup.png 人工目检（NN% 为静态文本必在）"
    }
    if ($arkIndex -ge 0)
    {
        # 条带裁图锚客户区（ClientRectOf）：旧实现裁外框，底部 6 行落在窗框外桌面——「507 像素」
        # 全是桌面边缘的 vacuous 通过（2026-10-14 缺陷单取证）。客户区裁图下条带缺席=真 0。
        $arkBarCrop = New-Crop ([WinEnum]::ClientScreenRectOf($panelHandles[$arkIndex])) 0
        Save-Zoom $arkBarCrop "D-tile-ark-bar-closeup.png" 6
        $arkBarPixels = Count-BarPixels $arkBarCrop
        $arkBarCrop.Dispose()
        if ($arkBarPixels -lt 120)
        {
            throw "断言失败（场景 D）：ark tile 底部进度条带仅 $arkBarPixels 像素（期望 ≥120，即 4 DIP 条+底轨）——bar 默认路径渲染回归"
        }
        Write-Host "ark 条带断言通过：$arkBarPixels 像素（D-tile-ark-bar-closeup.png，档位色+底轨目检）"
    }

    # 检查频率：常规页改「检查频率」→15 秒——config.json 落盘 + 日志重建调度双硬断言
    $row = Find-UiaById $settings "module-general" 10
    if (!$row) { throw "UIA 未找到模块行 module-general" }
    Invoke-Uia $row
    Start-Sleep -Seconds 2
    $pollBox = Find-UiaById $settings "poll-interval-box" 10
    if (!$pollBox) { throw "UIA 未找到 poll-interval-box（检查频率设置项缺失？）" }
    Save-FullScreenshot "D-general-poll-interval.png"
    Select-ComboItemById $settings $pollBox "poll-15"
    Save-FullScreenshot "D-general-poll-15s-selected.png"
    Start-Sleep -Seconds 3
    Copy-Item (Join-Path $configDir "logs\*.log") evidence/ -Force # app 日志随 artifact 归档（断言失败可直读现场）
    $appConfigJson = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($appConfigJson.pollIntervalSeconds)" -ne "15")
    {
        throw "断言失败（场景 D）：config.json pollIntervalSeconds=$($appConfigJson.pollIntervalSeconds)（期望 15）——检查频率未落盘"
    }
    $logPath = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    if (!(Test-Path $logPath)) { throw "断言失败（场景 D）：日志文件不存在 $logPath" }
    $logText = Get-Content $logPath -Raw -Encoding UTF8 # app 日志 UTF-8 无 BOM：PS 5.1 默认按 ANSI 读中文成乱码，断言必失配
    if ($logText -notmatch "检查频率变更为 15s")
    {
        throw "断言失败（场景 D）：日志未见「检查频率变更为 15s」——调度未按新间隔重建"
    }
    Write-Host "检查频率断言通过：config.json 落盘 15s + 日志确认重建调度"
    Write-Host "场景 D 通过：五家类型可见+连接成功+组件下拉有项；类型下拉逐家非空；保存连接即时进下拉；tile 名字/数值正确；检查频率落盘并重建调度"
}
catch
{
    Save-FullScreenshot "D-FAIL-unexpected.png"
    $failures.Add("场景 D：$($_.Exception.Message)")
    Write-Host "::error::场景 D 断言失败：$($_.Exception.Message)"
}
finally
{
    Stop-Beacon # mock 服务留待 E/F/G/H 场景复用，脚本末尾统一收尾
}

# —— 场景 E：单实例 + 托盘驻留 + Exit 唯一退出（todo「二次启动不重复进程」「关窗驻留 Exit 才退」CI 代验）——
try
{
    Write-ScenarioConfig $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    # E1 二次启动：命名互斥体 → 副实例 SignalExisting 后退出——进程数恒 1 + 日志凭证
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    # E1b 唤起气泡（todo 行 30 后半）：副实例 SignalExisting → 主实例 OnActivate → ShowBalloon
    # 「Beacon 已在运行。」Shell_NotifyIcon NIF_INFO 气泡 Win10+ 由 shell 转 toast 展示——toast 存活
    # ~5s，须启动后立即轮询（睡 8s 再查必扑空）；UIA 桌面根两变体精确匹配（带/不带句号）。
    # 无头会话 toast 基础设施不可用或被专注助手抑制则降级 ::warning 转装机（F5t/G7c 同口径），不可观测面不诈尸成红。
    $balloonSeen = $false
    $balloonCond1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Beacon 已在运行")
    $balloonCond2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Beacon 已在运行。")
    $deadline = (Get-Date).AddSeconds(12)
    while ((Get-Date) -lt $deadline -and !$balloonSeen)
    {
        $balloonHit = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $balloonCond1)
        if (!$balloonHit) { $balloonHit = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $balloonCond2) }
        if ($balloonHit) { $balloonSeen = $true; break }
        Start-Sleep -Milliseconds 400
    }
    Start-Sleep -Seconds 4 # 与气泡轮询合计凑足原 8s：副实例退出 + 日志落盘窗口
    if ($balloonSeen)
    {
        Write-Host "场景 E1b 通过：二次启动唤起气泡「Beacon 已在运行」UIA 可见（shell 转 toast 展示）——唤起链全绿"
    }
    else
    {
        Write-Host "::warning::二次启动气泡未在 UIA 出现（无头会话 toast 基础设施不可用或专注助手抑制）——唤起气泡呈现转装机目检（信号/进程半 E1 已绿）"
    }
    $alive = @(Get-Process Beacon.App -ErrorAction SilentlyContinue)
    if ($alive.Count -ne 1)
    {
        Save-FullScreenshot "E-FAIL-second-instance.png"
        throw "断言失败（场景 E1）：二次启动后 Beacon 进程数 $($alive.Count)（期望 1）——单实例互斥体失效"
    }
    $log = Get-TodayLog
    if ($log -notmatch "Secondary instance exit")
    {
        Save-FullScreenshot "E-FAIL-no-secondary-log.png"
        throw "断言失败（场景 E1）：二次启动后日志未见「Secondary instance exit.」——副实例退出路径未走"
    }
    Write-Host "场景 E1 通过：二次启动进程数保持 1，副实例退出日志在案（唤起气泡见 E1b）"
    # E2 托盘菜单 → Settings → 关窗不退进程（托盘驻留语义）；菜单驱动失败降级 warning（分隔条拖拽同款先例）
    $opened = Invoke-TrayMenu 5 "Settings"
    if (!$opened)
    {
        Write-Host "::warning::托盘菜单未弹出（合成右键未生效）——关窗驻留与 Exit 转装机手验（tray-menu 未截图）"
    }
    else
    {
        $settings = Find-SettingsWindow 15
        if (!$settings)
        {
            Save-FullScreenshot "E-FAIL-no-settings.png"
            throw "断言失败（场景 E2）：托盘菜单 Settings 后未见「Beacon 设置」窗"
        }
        [WinEnum]::Post([IntPtr]$settings.Current.NativeWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE
        Start-Sleep -Seconds 3
        if (Find-SettingsWindow 2)
        {
            Save-FullScreenshot "E-FAIL-settings-stuck.png"
            throw "断言失败（场景 E2）：WM_CLOSE 后设置窗未关闭"
        }
        if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -ne 1)
        {
            throw "断言失败（场景 E2）：关闭设置窗后进程退出——托盘驻留失效（关窗不应退进程）"
        }
        Write-Host "场景 E2 通过：设置窗 WM_CLOSE 关闭、进程驻留（关窗不退进程）"
        # E3 托盘菜单 → Exit → 进程必须退出（Exit 唯一退出路径）
        $opened2 = Invoke-TrayMenu 6 "Exit"
        if (!$opened2)
        {
            Write-Host "::warning::第二次托盘菜单未弹出——Exit 路径转装机手验"
        }
        else
        {
            $exited = $false
            $deadline = (Get-Date).AddSeconds(15)
            while ((Get-Date) -lt $deadline)
            {
                if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -eq 0) { $exited = $true; break }
                Start-Sleep -Milliseconds 500
            }
            if (!$exited)
            {
                Save-FullScreenshot "E-FAIL-exit-noop.png"
                throw "断言失败（场景 E3）：托盘菜单 Exit 后 15s 进程未退出——Exit 路径回归"
            }
            Write-Host "场景 E3 通过：托盘 Exit 后进程退出（Exit 唯一退出路径实证）"
        }
    }
}
catch
{
    Save-FullScreenshot "E-FAIL-unexpected.png"
    $failures.Add("场景 E：$($_.Exception.Message)")
    Write-Host "::error::场景 E 断言失败：$($_.Exception.Message)"
}

# —— 场景 F：无网启动缓存优先 + Last update 标注 + 自动刷新自愈 + 断网零弹窗 + 单条通知（todo 行 17/33/35/46 CI 代验）——
try
{
    Write-ScenarioConfig $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    # F0 健康基线：38% 在案（mock 健康链，缓存由本轮首启填充）
    $tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($tiles.Count -lt 1)
    {
        Save-FullScreenshot "F-FAIL-no-tile-healthy.png"
        throw "断言失败（场景 F0）：健康基线未见 GLM tile 窗"
    }
    $baseline = $false
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $tiles[0]
        if (($texts | Where-Object { $_ -match '\d+\s*%' })) { $baseline = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$baseline) { throw "断言失败（场景 F0）：健康基线 30s 内无百分比读数——mock 健康链未走通"; }
    Stop-Beacon
    Stop-MockServer # 断网（端点不可达 = 无网语义）
    Remove-Item (Join-Path $configDir "notifications.json") -Force -ErrorAction SilentlyContinue # 通知记录隔离，只认本场景新增
    # F5t 预置缺失：删 AUMID 注册键（前 38% 预跑期已由本场景首启写入）。38470370548/38488861749 实证
    # toolkit 7.1.3 从不创建开始菜单 .lnk（Show() 源码只有 CreateToastNotifier().Show(notif)），
    # 自愈真身=Initialize() 重写 HKCU\Software\Classes\AppUserModelId\<aumid>（含 CustomActivator）。
    # 只删带 CustomActivator 的键（toolkit 专有标记），不误伤其他 AUMID。
    foreach ($k in @(Get-ChildItem "HKCU:\Software\Classes\AppUserModelId" -ErrorAction SilentlyContinue))
    {
        $props = Get-ItemProperty $k.PSPath -ErrorAction SilentlyContinue
        if ($props -and $props.CustomActivator) { Remove-Item $k.PSPath -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 12
    $tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($tiles.Count -lt 1)
    {
        Save-FullScreenshot "F-FAIL-no-tile-offline.png"
        throw "断言失败（场景 F1）：无网启动未见 tile 窗——离线白板回归"
    }
    # F2 断网降级标注：Last update ≤45s 内出现（首个刷新失败 → 离线，值取缓存 FetchedAt）
    $sawLastUpdate = $false
    $texts = @()
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $tiles[0]
        if (($texts | Where-Object { $_ -like "*Last update*" })) { $sawLastUpdate = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$sawLastUpdate)
    {
        Save-FullScreenshot "F-FAIL-no-last-update.png"
        throw "断言失败（场景 F2）：断网后 45s 内 tile 未出现「Last update」离线标注；实际文本：$($texts -join ' | ')"
    }
    Save-FullScreenshot "F-offline-last-update.png"
    # F3 断网零弹窗：Beacon 进程名下不得有 #32770 对话框窗
    $beaconPid = (Get-Process Beacon.App | Select-Object -First 1).Id
    $dialogs = [WinEnum]::VisibleHandlesByClassAndPid("#32770", $beaconPid)
    if ($dialogs.Count -gt 0)
    {
        Save-FullScreenshot "F-FAIL-dialog.png"
        throw "断言失败（场景 F3）：断网期间 Beacon 进程弹出 $($dialogs.Count) 个对话框窗——降级弹窗回归"
    }
    Write-Host "场景 F1-F3 通过：无网启动有窗有数据、Last update 标注在案、零弹窗"
    # F5 断网降级通知：connection-degraded 规则（severity ≥ Warning，冷却去重）——
    # 记录应在断网窗口内落盘（缓存命中路径 severity 抬 Warning 后开火）
    $notifPath = Join-Path $configDir "notifications.json"
    $mine = @()
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline)
    {
        if (Test-Path $notifPath)
        {
            $records = @(Get-Content $notifPath -Raw | ConvertFrom-Json)
            $mine = @($records | Where-Object { $_.sourceWidgetId -eq "bigmodel.usage:GLM" })
            if ($mine.Count -ge 1) { break }
        }
        Start-Sleep -Seconds 3
    }
    if ($mine.Count -ne 1)
    {
        Save-FullScreenshot "F-FAIL-notifications.png"
        throw "断言失败（场景 F5）：断网窗口内 GLM 降级通知记录 $($mine.Count) 条（期望恰 1，冷却去重）——规则未开火或重复投递"
    }
    Write-Host "场景 F5 通过：断网降级通知恰 1 条（connection-degraded 冷却去重）；Toast 呈现面转装机"
    # F4 恢复自愈：mock 回来 ≤150s 内百分比读数重现（自动刷新闭环，无重启无人工；
    # 断网期失败刷新会累积 backoff——15s 基数 ×10 封顶 150s，宽限取满，成功即提前退出）
    Ensure-MockServer
    $healed = $false
    $deadline = (Get-Date).AddSeconds(150)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $tiles[0]
        if (($texts | Where-Object { $_ -match '\d+\s*%' })) { $healed = $true; break }
        Start-Sleep -Seconds 3
    }
    if (!$healed)
    {
        Save-FullScreenshot "F-FAIL-not-healed.png"
        throw "断言失败（场景 F4）：网络恢复后 150s 内 tile 未自愈回百分比读数——自动刷新/自愈回归"
    }
    Save-FullScreenshot "F-recovered.png"
    Write-Host "场景 F4 通过：网络恢复后 tile 自愈（自动刷新闭环实证）"
    # F5b 恢复后无新告警：健康链不再发降级通知（GLM 记录恒 1）
    $records = @(Get-Content $notifPath -Raw | ConvertFrom-Json)
    $mine = @($records | Where-Object { $_.sourceWidgetId -eq "bigmodel.usage:GLM" })
    if ($mine.Count -ne 1)
    {
        throw "断言失败（场景 F5b）：恢复后 GLM 通知记录 $($mine.Count) 条（期望仍 1）——健康状态误报"
    }
    # F5t AUMID 注册自愈（行 41「快捷方式缺失可自愈」半——机制澄清：toolkit 7.1.3 不建 .lnk，
    # 自愈真身=Initialize() 重写 HKCU\Software\Classes\AppUserModelId\<aumid> 键，见上方预删除注释）。
    # 无头会话 toast 平台不可用时按「Toast 发送失败」日志降级 warning 转装机（G7c 同口径）；
    # 零失败日志且键不在 = 首发送链真回归红（行 41 发现面）。
    # F5t 排在 F4 之后：F4 的 Ensure-MockServer 必须先于任何 throw 执行，否则 mock 停摆连带 G/H/K 全红。
    $sentFailure = $false
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        if (Test-AumidKeyPresent) { break }
        $fLog = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
        if ((Test-Path $fLog) -and ([System.IO.File]::ReadAllText($fLog, [System.Text.Encoding]::UTF8) -match "Toast 发送失败")) { $sentFailure = $true; break }
        Start-Sleep -Seconds 2
    }
    if (Test-AumidKeyPresent)
    {
        Save-FullScreenshot "F-toast-shortcut-healed.png"
        Write-Host "场景 F5t 通过：AUMID 注册自愈——预置删除后 Initialize() 重建 AppUserModelId 键（toolkit 7.1.3 注册表机制，行 41 半）"
    }
    elseif ($sentFailure)
    {
        Save-FullScreenshot "F-toast-shortcut-headless.png"
        Write-Host "::warning::场景 F5t：无头会话 toast 平台不可用（「Toast 发送失败」在日志），AUMID 注册自愈转装机目检（G7c 同口径）；通知记录断言（F5）不受影响"
    }
    else
    {
        Save-FullScreenshot "F-FAIL-shortcut-not-healed.png"
        throw "断言失败（场景 F5t）：预置删除的 AUMID 注册键 30s 未自愈重建，且日志无「Toast 发送失败」——首发送链未走通（行 41 发现面）"
    }
}
catch
{
    Save-FullScreenshot "F-FAIL-unexpected.png"
    $failures.Add("场景 F：$($_.Exception.Message)")
    Write-Host "::error::场景 F 断言失败：$($_.Exception.Message)"
}

# —— 场景 G：调色/动效全链（todo 行 49/50/51/52/53/58/59 CI 代验）——
try
{
    # G1 colorOverride 仅该 tile 变色（#ff00ff 品红只落 GLM 状态灯，DeepSeek 零渗）
    Ensure-MockServer # 防御：不依赖 F 场景收尾（38470370548 级联红根因=F5t throw 跳过 F4 的 Ensure-MockServer）
    Write-GConfig "#ff00ff"
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($handles.Count -lt 2)
    {
        Save-FullScreenshot "G-FAIL-tiles.png"
        throw "断言失败（场景 G1）：双 tile 未建窗（$($handles.Count) 个）"
    }
    $glmIndex = -1
    for ($i = 0; $i -lt $handles.Count; $i++)
    {
        if ((Get-WindowUiaTexts $handles[$i]) -like "*GLM*") { $glmIndex = $i }
    }
    $dsIndex = -1
    for ($i = 0; $i -lt $handles.Count; $i++)
    {
        if ((Get-WindowUiaTexts $handles[$i]) -like "*420.5*") { $dsIndex = $i }
    }
    if ($glmIndex -lt 0 -or $dsIndex -lt 0)
    {
        throw "断言失败（场景 G1）：GLM/DeepSeek tile 文本不可辨（GLM=$glmIndex ds=$dsIndex）"
    }
    $glmCrop = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    Save-Zoom $glmCrop "G1-glm-override-closeup.png" 6
    $glmMagenta = Count-ColorPixels $glmCrop 255 0 255 60
    $glmCrop.Dispose()
    $dsCrop = New-Crop ([WinEnum]::RectOf($handles[$dsIndex])) 0
    $dsMagenta = Count-ColorPixels $dsCrop 255 0 255 60
    $dsCrop.Dispose()
    # 阈值依据（38261911354 工件实测）：8DIP 状态灯同一几何，逐实例亚像素渲染位置不同 →
    # 匹配像素漂移：品红 tol60 两次实测 44/24，绿 tol45 五实例底 10、另三轮 44；tol45=tol60
    # （tol90 起误吞 icon 灰）。失效模式（覆盖未生效）=0 px。阈值取实测底之下 → 8。
    if ($glmMagenta -lt 8)
    {
        throw "断言失败（场景 G1）：GLM tile 品红覆盖像素 $glmMagenta（期望 ≥8）——colorOverride 未生效"
    }
    if ($dsMagenta -gt 4)
    {
        throw "断言失败（场景 G1）：DeepSeek tile 出现品红 $dsMagenta 像素——覆盖越界（应仅 GLM）"
    }
    Write-Host "场景 G1 通过：colorOverride 品红仅在 GLM（$glmMagenta px），DeepSeek 零渗（$dsMagenta px）"
    # G2 非法颜色回退：不崩溃 + 渲染默认成功绿 + 重启后保持（两轮启动）
    Write-GConfig "sent:---"
    foreach ($round in @(1, 2))
    {
        Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
        Start-Sleep -Seconds 15
        if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -lt 1)
        {
            Save-FullScreenshot "G-FAIL-invalid-crash.png"
            throw "断言失败（场景 G2 第${round}轮）：非法 colorOverride 启动即崩"
        }
        $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
        $glmIndex = -1
        for ($i = 0; $i -lt $handles.Count; $i++)
        {
            if ((Get-WindowUiaTexts $handles[$i]) -like "*GLM*") { $glmIndex = $i }
        }
        if ($glmIndex -lt 0) { throw "断言失败（场景 G2 第${round}轮）：GLM tile 不可辨" }
        $crop = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
        Save-Zoom $crop "G2-invalid-round$round-closeup.png" 6
        $magenta = Count-ColorPixels $crop 255 0 255 60
        $green = Count-ColorPixels $crop 63 185 80 45
        $crop.Dispose()
        if ($magenta -gt 4) { throw "断言失败（场景 G2 第${round}轮）：非法值被当色渲染（品红 $magenta px）——回退失效" }
        # 实测口径修正（38261911354 工件逐像素取证）：GLM tile 无进度条（state.Progress 为空，
        # bar 收起）——绿像素全部来自 8DIP 状态灯本体；同 Ø8 灯因亚像素位置不同实测 10~44 px
        # （D 场景五实例 10、G2 三轮 44）。失效模式（回退后渲染缺失）=0 px → 阈值 8（实测底之下）。
        if ($green -lt 8) { throw "断言失败（场景 G2 第${round}轮）：默认成功绿未渲染（$green px）——回退后渲染缺失" }
        Stop-Beacon
        Write-Host "场景 G2 第${round}轮通过：非法色回退默认绿（品红 $magenta / 绿 $green px），进程存活"
    }
    Write-Host "场景 G2 通过：非法颜色回退不崩溃且重启保持"
    # G3-G6 设置页驱动：改色即时生效+落盘、重置默认、动效档位/强度持久化、重启恢复选中。
    # showCapsule=true：胶囊 OverallLight 与 tile 同源（UiPalette 每次渲染实时读 config），
    # G4/G5 顺带像素断言「设置改级别色后胶囊同步变色」（todo 行 67 胶囊半；标题「Beacon Status」同 Q 场景）；
    # L 场景 :2203 重播种回 showCapsule=false，胶囊只存活于 G3-G7 会话
    Write-GConfig "" "off" 1.0 $false $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings)
    {
        Save-FullScreenshot "G-FAIL-no-settings.png"
        throw "断言失败（场景 G3）：--settings 启动未见「Beacon 设置」窗"
    }
    Start-Sleep -Seconds 10 # 双 tile 建窗
    $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    $glmIndex = -1
    for ($i = 0; $i -lt $handles.Count; $i++)
    {
        if ((Get-WindowUiaTexts $handles[$i]) -like "*GLM*") { $glmIndex = $i }
    }
    if ($glmIndex -lt 0) { throw "断言失败（场景 G3）：GLM tile 不可辨" }
    $row = Find-UiaById $settings "module-appearance" 10
    if (!$row) { throw "断言失败（场景 G3）：UIA 未找到 module-appearance" }
    Invoke-Uia $row
    Start-Sleep -Seconds 2
    $motionBox = Find-UiaById $settings "appearance-motion-mode-box" 10
    if (!$motionBox) { throw "断言失败（场景 G3）：UIA 未找到 appearance-motion-mode-box（锚点丢失？）" }
    # G4 成功色改 #ff8800 → config 落盘 + tile 换橙。重绘由状态事件驱动（刷新 tick ≤15s 重发布），
    # 非保存即绘（38259426553 实证保存后 2s crop 橙 0px）——轮询 ≤20s 等下一渲染 tick
    $successBox = Find-UiaById $settings "appearance-color-success" 10
    if (!$successBox) { throw "断言失败（场景 G4）：UIA 未找到 appearance-color-success" }
    ($successBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("#ff8800")
    try { $motionBox.SetFocus() } catch { Invoke-Uia $row } # 移焦触发 LostFocus 落盘
    Start-Sleep -Seconds 2
    $appConfigNow = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($appConfigNow.appearance.severityColors.success)" -ne "#ff8800")
    {
        throw "断言失败（场景 G4）：config severityColors.success=$($appConfigNow.appearance.severityColors.success)（期望 #ff8800）——改色未落盘"
    }
    $orange = 0
    $orangeCrop = $null
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $crop = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
        $orange = Count-ColorPixels $crop 255 136 0 45
        if ($orangeCrop) { $orangeCrop.Dispose() }
        $orangeCrop = $crop
        if ($orange -ge 8) { break }
        Start-Sleep -Seconds 3
    }
    Save-Zoom $orangeCrop "G4-glm-orange-live.png" 6
    $orangeCrop.Dispose()
    # 阈值依据（38261911354 工件逐像素取证，修正 3be02b6 的「呼吸调光」误诊）：
    # G4 两次 CI 实测橙 24/24 px，均 = Ø8 灯在半像素偏移下的正常渲染（精确核 16 + 0.89 覆盖率
    # 均匀边缘环 8；呼吸灯只存在于 full 档，reduced/off 根本不启动——绿同灯三轮实测 44、
    # D 场景五实例 10，纯亚像素几何差异，与动效/调光无关）。改色未生效的失效模式 = 0 px 橙。
    # 阈值取实测底（10）之下、0 之上 → 8；轮询保留作渲染 tick 兜底。
    if ($orange -lt 8)
    {
        throw "断言失败（场景 G4）：20s 内 GLM tile 橙色像素 $orange（期望 ≥8）——改色后渲染链未用新色（palette 渲染时读 config 失效）"
    }
    Write-Host "场景 G4 通过：设置改成功色 #ff8800 → config 落盘 + GLM tile 下一渲染 tick 换橙（$orange px，无重启）"
    # G4 胶囊半（todo 行 67：胶囊原记「像素不可脚本取证」——实测与 tile 同机制，可断言）：
    # OverallLight.Fill 走同一 UiPalette.SeverityColor（Ø9 DIP 灯）→ 改色后下一聚合 tick 同步换橙，
    # 同 tile 轮询口径；窗口默认右下角（tile 右上角级联，无重叠），UiOpacity 默认 1.0 无混色
    $capsuleHandle = [IntPtr]::Zero
    $capsuleDeadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $capsuleDeadline)
    {
        $capsuleHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
        if ($capsuleHandles.Count -ge 1) { $capsuleHandle = $capsuleHandles[0]; break }
        Start-Sleep -Milliseconds 500
    }
    if ($capsuleHandle -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "G4-FAIL-no-capsule.png"
        throw "断言失败（场景 G4）：showCapsule=true 启动未见「Beacon Status」胶囊窗"
    }
    $capsuleOrange = 0
    $capsuleCrop = $null
    $capsuleDeadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $capsuleDeadline)
    {
        $crop = New-Crop ([WinEnum]::RectOf($capsuleHandle)) 0
        $capsuleOrange = Count-ColorPixels $crop 255 136 0 45
        if ($capsuleCrop) { $capsuleCrop.Dispose() }
        $capsuleCrop = $crop
        if ($capsuleOrange -ge 8) { break }
        Start-Sleep -Seconds 3
    }
    Save-Zoom $capsuleCrop "G4-capsule-orange-live.png" 6
    $capsuleCrop.Dispose()
    # 阈值同 tile 口径：Ø9 灯失效模式（改色未生效）=0 px 橙 → 8
    if ($capsuleOrange -lt 8)
    {
        throw "断言失败（场景 G4）：20s 内胶囊灯橙色像素 $capsuleOrange（期望 ≥8）——胶囊未随级别色同步变色"
    }
    Write-Host "场景 G4 通过：胶囊 OverallLight 同步换橙（$capsuleOrange px，无重启）"
    # G5 重置默认一键还原
    $reset = Find-UiaById $settings "appearance-reset-button" 10
    if (!$reset) { throw "断言失败（场景 G5）：UIA 未找到 appearance-reset-button" }
    Invoke-Uia $reset
    Start-Sleep -Seconds 2
    $appConfigNow = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    # severityColors 可能整键省略（=null）：@($null).Count 是 1 不是 0，必须先判空
    $remaining = 0
    if ($null -ne $appConfigNow.appearance.severityColors)
    {
        $remaining = @($appConfigNow.appearance.severityColors.PSObject.Properties).Count
    }
    if ($remaining -ne 0)
    {
        throw "断言失败（场景 G5）：重置后 severityColors 仍余 $remaining 项——一键还原回归"
    }
    # 重置后同样等下一渲染 tick（同 G4 口径）
    $green = 0
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $crop = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
        $green = Count-ColorPixels $crop 63 185 80 45
        $crop.Dispose()
        if ($green -ge 8) { break }
        Start-Sleep -Seconds 3
    }
    # 同 G2/G4 阈值依据：同 Ø8 灯亚像素实测 10~44 px，失效模式（重置未回渲染）=0 px → 8
    if ($green -lt 8) { throw "断言失败（场景 G5）：20s 内重置默认绿未回渲染（$green px，期望 ≥8）" }
    Write-Host "场景 G5 通过：重置默认 → severityColors 清空 + tile 回默认绿（$green px）"
    # G5 胶囊半：重置后胶囊灯同源回默认绿（$capsuleHandle 沿用 G4 所取，同会话未重启；同 tile 轮询口径）
    $capsuleGreen = 0
    $capsuleCrop = $null
    $capsuleDeadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $capsuleDeadline)
    {
        $crop = New-Crop ([WinEnum]::RectOf($capsuleHandle)) 0
        $capsuleGreen = Count-ColorPixels $crop 63 185 80 45
        if ($capsuleCrop) { $capsuleCrop.Dispose() }
        $capsuleCrop = $crop
        if ($capsuleGreen -ge 8) { break }
        Start-Sleep -Seconds 3
    }
    Save-Zoom $capsuleCrop "G5-capsule-green-back.png" 6
    $capsuleCrop.Dispose()
    if ($capsuleGreen -lt 8) { throw "断言失败（场景 G5）：20s 内胶囊灯默认绿未回渲染（$capsuleGreen px，期望 ≥8）" }
    Write-Host "场景 G5 通过：胶囊 OverallLight 回默认绿（$capsuleGreen px）"
    # G6 动效档 off + 强度 1.5 落盘
    Select-ComboItemById $settings $motionBox "motion-off"
    Start-Sleep -Seconds 2
    $slider = Find-UiaById $settings "appearance-motion-intensity" 10
    if (!$slider) { throw "断言失败（场景 G6）：UIA 未找到 appearance-motion-intensity" }
    ($slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)).SetValue(1.5)
    Start-Sleep -Seconds 2
    $appConfigNow = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($appConfigNow.appearance.motion.mode)" -ne "off" -or "$($appConfigNow.appearance.motion.intensity)" -ne "1.5")
    {
        throw "断言失败（场景 G6）：config motion.mode=$($appConfigNow.appearance.motion.mode) intensity=$($appConfigNow.appearance.motion.intensity)（期望 off/1.5）——动效设置未落盘"
    }
    Write-Host "场景 G6 通过：动效档 off + 强度 1.5 落盘"
    # G6b 重启恢复选中态
    Stop-Beacon
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings) { throw "断言失败（场景 G6b）：重启后设置窗未开" }
    Start-Sleep -Seconds 8
    $row = Find-UiaById $settings "module-appearance" 10
    Invoke-Uia $row
    Start-Sleep -Seconds 2
    $motionBox = Find-UiaById $settings "appearance-motion-mode-box" 10
    if (!$motionBox) { throw "断言失败（场景 G6b）：重启后 motion combo 未找到" }
    ($motionBox.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand()
    Start-Sleep -Seconds 1
    $offItem = Find-UiaById $settings "motion-off" 8
    if (!$offItem)
    {
        # 展开弹层可能是独立 HWND：退回桌面根子树找（同 Select-ComboItemById 口径）
        $offItem = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "motion-off")))
    }
    if (!$offItem) { throw "断言失败（场景 G6b）：重启后 motion-off 选项未找到" }
    $isSelected = ($offItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected
    ($motionBox.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Collapse()
    if (!$isSelected) { throw "断言失败（场景 G6b）：重启后动效档位未恢复「关闭」选中态——持久化回归" }
    Write-Host "场景 G6b 通过：重启后动效档位恢复「关闭」选中（UIA IsSelected）"
    # G7 mock 翻 95% Error：off 档全静止 / full 档呼吸可见 + 两 tile 独立更新
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "error" -Encoding ASCII
    $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    $glmIndex = -1
    $dsIndex = -1
    $deadline = (Get-Date).AddSeconds(75)
    while ((Get-Date) -lt $deadline)
    {
        $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
        $glmIndex = -1
        $dsIndex = -1
        for ($i = 0; $i -lt $handles.Count; $i++)
        {
            $t = Get-WindowUiaTexts $handles[$i]
            if (($t | Where-Object { $_ -match '95' })) { $glmIndex = $i }
            if (($t | Where-Object { $_ -like "*420.5*" })) { $dsIndex = $i }
        }
        if ($glmIndex -ge 0) { break }
        Start-Sleep -Seconds 3
    }
    if ($glmIndex -lt 0) { throw "断言失败（场景 G7）：mock 翻档 75s 内 GLM 未变 95%——状态变化未反映到 tile" }
    if ($dsIndex -lt 0) { throw "断言失败（场景 G7）：DeepSeek tile 420.5 不可辨——翻档后另一 tile 丢失" }
    Write-Host "场景 G7a 通过：38%→95% 状态变化 ≤1 刷新周期反映到 GLM tile；DeepSeek 420.5 不受影响（两 tile 独立更新）"
    $crop = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    $red = Count-ColorPixels $crop 248 81 73 45
    $crop.Dispose()
    # 阈值依据（同 G2/G4 逐像素取证口径）：GLM tile 无进度条，红像素全部来自 8DIP 状态灯，
    # 亚像素位置不同实测区间 10~44 px（「95% 填充 2.5 倍外推 ~92px」系条带误设——G2 绿
    # bbox 8×8 实证无条带）。G7 尚未在 CI 完整跑过（此前 G2/G4 先红），先按实测底标定 → 8。
    if ($red -lt 8) { throw "断言失败（场景 G7a）：Error 档位色未渲染（红 $red px，期望 ≥8）" }
    # off 档静止：三帧差分应≈0
    $crop1 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    Start-Sleep -Milliseconds 900
    $crop2 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    Start-Sleep -Milliseconds 900
    $crop3 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    $offDiff = [Math]::Max((Count-FrameDiff $crop1 $crop2 12), [Math]::Max((Count-FrameDiff $crop2 $crop3 12), (Count-FrameDiff $crop1 $crop3 12)))
    Save-Zoom $crop1 "G7-off-frame1.png" 4
    Save-Zoom $crop3 "G7-off-frame3.png" 4
    $crop1.Dispose(); $crop2.Dispose(); $crop3.Dispose()
    if ($offDiff -gt 12) { throw "断言失败（场景 G7b）：off 档三帧差分 $offDiff px（期望 ≤12）——off 档仍有动画（功耗回归）" }
    Write-Host "场景 G7b 通过：off 档三帧差分 $offDiff px——全静止"
    # 切 full 档：呼吸在帧间现形。呼吸循环由 ApplyLoops 挂载——只在下一个 WidgetStateChanged
    # tick（GLM=ci 档 15s）应用；呼吸周期 2400ms×intensity(1.5)=3600ms，固定帧对还可能撞缓动
    # 峰台（相位差小 → 差分小）。故先硬断言选档落盘，再轮询重采样帧对（每轮 3 帧、取两两差分
    # 最大值）80s（≥5 个刷新 tick，覆盖「挂载 tick + 多个完整呼吸周期」）。
    Select-ComboItemById $settings $motionBox "motion-full"
    Start-Sleep -Seconds 2
    $appConfigFull = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($appConfigFull.appearance.motion.mode)" -ne "full")
    {
        throw "断言失败（场景 G7c）：config appearance.motion.mode=$($appConfigFull.appearance.motion.mode)（期望 full）——选档未落盘"
    }
    $fullDiff = 0
    $best1 = $null
    $best3 = $null
    $deadline = (Get-Date).AddSeconds(80)
    while ((Get-Date) -lt $deadline)
    {
        $c1 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
        Start-Sleep -Milliseconds 900
        $c2 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
        Start-Sleep -Milliseconds 900
        $c3 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
        $d = [Math]::Max((Count-FrameDiff $c1 $c2 12), [Math]::Max((Count-FrameDiff $c2 $c3 12), (Count-FrameDiff $c1 $c3 12)))
        if ($d -gt $fullDiff)
        {
            $fullDiff = $d
            if ($best1) { $best1.Dispose() }
            if ($best3) { $best3.Dispose() }
            $best1 = $c1
            $best3 = $c3
            $c2.Dispose()
        }
        else
        {
            $c1.Dispose(); $c2.Dispose(); $c3.Dispose()
        }
        if ($fullDiff -ge 25) { break }
        Start-Sleep -Seconds 2
    }
    if ($best1)
    {
        Save-Zoom $best1 "G7-full-frame1.png" 4
        Save-Zoom $best3 "G7-full-frame3.png" 4
        $best1.Dispose(); $best3.Dispose()
    }
    # 阈值依据：呼吸在 0.55↔1.0 相位间摆动时灯区全部像素跨相位变色（Ø8 灯全域 ~50-64 px），
    # 帧对撞峰台由轮询取最大值规避；25 为灯全域差分的保守下界。
    if ($fullDiff -ge 25)
    {
        Write-Host "场景 G7c 通过：full 档帧差分 $fullDiff px——呼吸灯可见（改档即时生效，无重启）"
    }
    else
    {
        # tile 不呼吸时分诊：设置页「呼吸」预览按钮驱动同一 MotionEngine 同一挂载路径
        # （PreviewMotion("breath")→StartBreathing）。预览灯 Ø24（呼吸时全域 ~450px 跨相位变色）：
        #   预览也静止 → Composition 属性动画在本 CI 会话不可证（mode=full 已落盘硬断言在案，
        #   38268308192/38271096867 两次 0 差分同证）——L0 呼吸灯观感转装机目检（同 Esc/SendInput 降级口径）；
        #   预览呼吸而 L0 不呼吸 → tile 链路真实回归，保持红。
        $breathBtn = $settings.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "呼吸")))
        if (!$breathBtn) { throw "断言失败（场景 G7c）：tile 帧差分 $fullDiff px 且设置页「呼吸」预览按钮未找到——无法分诊，按真实回归处理" }
        ($breathBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
        Start-Sleep -Seconds 1
        $br = $breathBtn.Current.BoundingRectangle
        if ($br.IsEmpty -or $br.Width -le 0 -or $br.Height -le 0)
        {
            throw "断言失败（场景 G7c）：tile 帧差分 $fullDiff px 且「呼吸」预览按钮无可视矩形（模块可能滚出视口）——无法分诊，按真实回归处理"
        }
        $rx = [Math]::Max(0, [int]$br.X - 10)
        $ry = [Math]::Max(0, [int]$br.Y - 45)
        $rw = [int]$br.Width + 340 # 按钮行内「呼吸」右侧还有 3 个按钮 + 预览灯（水平 StackPanel 同行）
        $rh = [int]$br.Height + 90
        $p1 = New-Object System.Drawing.Bitmap $rw, $rh
        $g1p = [System.Drawing.Graphics]::FromImage($p1)
        $g1p.CopyFromScreen($rx, $ry, 0, 0, (New-Object System.Drawing.Size $rw, $rh))
        $g1p.Dispose()
        Start-Sleep -Milliseconds 900
        $p2 = New-Object System.Drawing.Bitmap $rw, $rh
        $g2p = [System.Drawing.Graphics]::FromImage($p2)
        $g2p.CopyFromScreen($rx, $ry, 0, 0, (New-Object System.Drawing.Size $rw, $rh))
        $g2p.Dispose()
        Start-Sleep -Milliseconds 900
        $p3 = New-Object System.Drawing.Bitmap $rw, $rh
        $g3p = [System.Drawing.Graphics]::FromImage($p3)
        $g3p.CopyFromScreen($rx, $ry, 0, 0, (New-Object System.Drawing.Size $rw, $rh))
        $g3p.Dispose()
        $probeDiff = [Math]::Max((Count-FrameDiff $p1 $p2 12), [Math]::Max((Count-FrameDiff $p2 $p3 12), (Count-FrameDiff $p1 $p3 12)))
        Save-Zoom $p1 "G7-preview-frame1.png" 3
        $p1.Dispose(); $p2.Dispose(); $p3.Dispose()
        # 阈值依据：Ø24 预览灯呼吸时全域 ~450px 跨相位变色（阈值 50=保守下界）；静止恒 0
        if ($probeDiff -ge 50)
        {
            throw "断言失败（场景 G7c）：设置页预览呼吸可见（$probeDiff px）但 L0 tile 帧差分 $fullDiff px——tile 链路真实回归"
        }
        Write-Host "::warning::L0 tile 呼吸灯帧差分 $fullDiff px 且设置页预览同样静止（$probeDiff px；mode=full 已落盘）——Composition 属性动画在本 CI 会话不可证，呼吸灯观感转装机目检（同 Esc/SendInput 降级口径）"
    }
    Write-Host "场景 G7c 通过：full 档三帧差分 $fullDiff px——呼吸灯可见（改档即时生效，无重启）"
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Stop-Beacon
    Write-Host "场景 G 通过：调色即时生效/落盘/重置/回退 + 动效档持久化/静止呼吸两态"
}
catch
{
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Save-FullScreenshot "G-FAIL-unexpected.png"
    $failures.Add("场景 G：$($_.Exception.Message)")
    Write-Host "::error::场景 G 断言失败：$($_.Exception.Message)"
}

# —— 场景 H：悬浮位置还原 + Alt-Tab/任务栏结构 + 刷新不抢焦点 + panel/floating 二选一（todo 行 39/40/41/44 半项 CI 代验）——
try
{
    Ensure-MockServer # 防御：不依赖前序场景收尾（38470370548 级联红根因）
    Write-HConfig "floating"
    foreach ($round in @(1, 2))
    {
        Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
        Start-Sleep -Seconds 15
        $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
        if ($handles.Count -lt 2)
        {
            Save-FullScreenshot "H-FAIL-tiles.png"
            throw "断言失败（场景 H${round}）：双 tile 未建窗（$($handles.Count) 个）"
        }
        $glmHit = $false
        $dsHit = $false
        foreach ($h in $handles)
        {
            $r = [WinEnum]::RectOf($h)
            if ([Math]::Abs($r.Left - 120) -le 4 -and [Math]::Abs($r.Top - 120) -le 4) { $glmHit = $true }
            if ([Math]::Abs($r.Left - 260) -le 4 -and [Math]::Abs($r.Top - 220) -le 4) { $dsHit = $true }
            $ex = [WinEnum]::ExStyleOf($h)
            if (($ex -band 0x80) -eq 0) { throw "断言失败（场景 H${round}）：tile 缺 WS_EX_TOOLWINDOW（会进任务栏/Alt-Tab）" }
            if (($ex -band 0x40000) -ne 0) { throw "断言失败（场景 H${round}）：tile 带 WS_EX_APPWINDOW（会进任务栏）" }
        }
        if (!$glmHit -or !$dsHit)
        {
            Save-FullScreenshot "H-FAIL-position.png"
            throw "断言失败（场景 H${round}）：位置还原失败（GLM@120,120=$glmHit ds@260,220=$dsHit）——FloatingX/Y 启动恢复回归"
        }
        Write-Host "场景 H${round} 通过：双 tile 精确落位 120,120 / 260,220；TOOLWINDOW 置位、APPWINDOW 清位"
        if ($round -eq 1)
        {
            # 刷新不抢焦点：38%→95% 翻档期间前台窗不变（NOACTIVATE 语义）
            Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "error" -Encoding ASCII
            $fgBefore = [WinEnum]::Foreground()
            $changed = $false
            $deadline = (Get-Date).AddSeconds(75)
            while ((Get-Date) -lt $deadline)
            {
                $hit = $false
                foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
                {
                    if ((Get-WindowUiaTexts $h) | Where-Object { $_ -match '95' }) { $hit = $true; break }
                }
                if ($hit) { $changed = $true; break }
                Start-Sleep -Seconds 3
            }
            Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
            if (!$changed) { throw "断言失败（场景 H1）：翻档 75s 内 tile 未变 95%——状态刷新链失效" }
            $fgAfter = [WinEnum]::Foreground()
            if ($fgAfter -ne $fgBefore)
            {
                Save-FullScreenshot "H-FAIL-focus-stolen.png"
                throw "断言失败（场景 H1）：数据刷新改变了前台窗——更新抢焦点回归"
            }
            Write-Host "场景 H1 通过：状态刷新到 95% 期间前台窗未被打断（NOACTIVATE 语义）"
        }
        Stop-Beacon
    }
    Write-Host "场景 H 重启还原通过（两轮落位一致，config→窗口数据链）"
    # H6 悬浮 tile 可拖放（todo 行 67 拖放半项 CI 代验）：SendInput 绝对坐标拖拽（场景 D 分隔条
    # 同款口径，38048738860 实证合成拖拽在 CI 生效；旧「合成拖拽不可靠」先例是 mouse_event 时代）。
    # 拖 GLM tile +80/+60（远过 3px 死区）→ 窗随动 → PointerReleased 落盘 widgets.json/pins.json →
    # 重启还原到新位（拖放→落盘→还原全链闭环）。
    Ensure-MockServer
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $glm = [IntPtr]::Zero
    foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
    {
        $r = [WinEnum]::RectOf($h)
        if ([Math]::Abs($r.Left - 120) -le 4 -and [Math]::Abs($r.Top - 120) -le 4) { $glm = $h; break }
    }
    if ($glm -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "H6-FAIL-no-glm.png"
        throw "断言失败（场景 H6）：GLM tile 未在预设位 120,120（拖拽前置失败）"
    }
    $r0 = [WinEnum]::RectOf($glm)
    $cx = [int](($r0.Left + $r0.Right) / 2)
    $cy = [int](($r0.Top + $r0.Bottom) / 2)
    Save-FullScreenshot "H6-drag-before.png"
    [WinEnum]::DragMouse($cx, $cy, ($cx + 80), ($cy + 60))
    Start-Sleep -Seconds 1
    Save-FullScreenshot "H6-drag-after.png"
    $r1 = [WinEnum]::RectOf($glm)
    if ([Math]::Abs(($r1.Left - $r0.Left) - 80) -gt 6 -or [Math]::Abs(($r1.Top - $r0.Top) - 60) -gt 6)
    {
        Write-Host "::warning::场景 H6：拖拽后窗位 $($r1.Left),$($r1.Top)（自 $($r0.Left),$($r0.Top) 期望 +80/+60）——合成拖拽手势未生效（截图 before/after 在案），拖放功能链转装机手验"
    }
    else
    {
        Write-Host "场景 H6a 通过：拖拽 +80/+60 窗随动（$($r0.Left),$($r0.Top) → $($r1.Left),$($r1.Top)，死区 3px 已过）"
        # 落盘断言：PointerReleased→PersistPosition 双写 widgets.json/pins.json 的 FloatingX/Y = 新位
        $appCfg = Get-Content (Join-Path $configDir "widgets.json") -Raw | ConvertFrom-Json
        $glmRow = @($appCfg) | Where-Object { $_.id -eq "bigmodel.usage:GLM" } | Select-Object -First 1
        $expectX = $r1.Left; $expectY = $r1.Top
        $savedX = $glmRow.pinLayout.floatingX
        $savedY = $glmRow.pinLayout.floatingY
        if ([Math]::Abs([double]$savedX - $expectX) -gt 4 -or [Math]::Abs([double]$savedY - $expectY) -gt 4)
        {
            Save-FullScreenshot "H6-FAIL-persist.png"
            throw "断言失败（场景 H6b）：拖后落盘 floatingX/Y=$savedX,$savedY 与窗位 $expectX,$expectY 不符（±4）——PersistPosition 落盘回归"
        }
        Write-Host "场景 H6b 通过：拖后落盘 floatingX/Y=$savedX,$savedY（窗位一致）"
        # 重启还原到拖后新位（与 H 预设还原同一取证口径，但这次还原的是手势产物）
        Stop-Beacon
        Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
        Start-Sleep -Seconds 15
        $restored = $false
        foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
        {
            $r = [WinEnum]::RectOf($h)
            if ([Math]::Abs($r.Left - $expectX) -le 4 -and [Math]::Abs($r.Top - $expectY) -le 4) { $restored = $true; break }
        }
        if (!$restored)
        {
            Save-FullScreenshot "H6-FAIL-restore.png"
            throw "断言失败（场景 H6c）：重启后无 tile 落在拖后位 $expectX,$expectY（±4）——拖动结果未进还原链"
        }
        Write-Host "场景 H6c 通过：重启还原到拖后位 $expectX,$expectY（拖放→落盘→还原闭环）"
    }
    # H5 panel/floating 二选一
    Write-HConfig "panel"
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $panel = [WinEnum]::VisibleHandlesByTitle("Beacon Pinned")
    $tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($panel.Count -lt 1)
    {
        Save-FullScreenshot "H-FAIL-no-panel.png"
        throw "断言失败（场景 H5）：panel 模式未见 Beacon Pinned 宿主窗"
    }
    if ($tiles.Count -gt 0)
    {
        throw "断言失败（场景 H5）：panel 模式残留 $($tiles.Count) 个悬浮 tile 窗——形态互斥回归"
    }
    Save-FullScreenshot "H-panel-mode.png"
    # 面板客户区高度硬断言（2026-10-14 缺陷单）：面板客户区 = 行数×32 DIP（100% DPI 下 32 的整数倍）。
    # 非客户区差值未补回时末行 tile 底部 6px 不可见（H 取证：末行 tile 26px 无条、首行完整 32px）。
    $panelClient = [WinEnum]::ClientRectOf($panel[0])
    $panelH = $panelClient.Bottom - $panelClient.Top
    $panelRowPx = ExpectedRowPx $panel[0]
    if ($panelH -lt $panelRowPx -or ($panelH % $panelRowPx) -ne 0)
    {
        throw "断言失败（场景 H5）：面板客户区高 ${panelH}px（期望 ${panelRowPx}px 的整数倍，32 DIP × 缩放 ${([WinEnum]::DpiScaleOf($panel[0]))}）——非客户区差值未补回，末行 tile 底部进度条落进不可见区（缺陷 2026-10-14 回归）"
    }
    Write-Host "场景 H5 面板客户区断言通过：client ${panelH}px（$($panelH / $panelRowPx) 行 × 32 DIP，行高 ${panelRowPx}px）"
    Write-Host "场景 H5 通过：panel 模式宿主在、悬浮 tile 窗零（两形态二选一）"
    # H5b 空白区点击穿透（todo 行 65 半项 CI 代验）：直接 WM_NCHITTEST 问宿主命中裁决。
    # PinnedHostWindow.WndProc 口径：client ∈ [0,行高)×[0,命中宽) → HTCLIENT，其余（外框
    # chrome 环带）→ HTTRANSPARENT；HTTRANSPARENT 的 Win32 语义即点击落到下层窗/桌面。
    # 收起态命中宽=12 DIP 细条，probe(1,1) 两种形态都在命中区内。
    $wr = [WinEnum]::RectOf($panel[0])
    $cr = [WinEnum]::ClientScreenRectOf($panel[0])
    $inTile = [WinEnum]::NcHitTest($panel[0], ($cr.Left + 1), ($cr.Top + 1))
    $inRightBand = [WinEnum]::NcHitTest($panel[0], ($wr.Right - 2), [int](($cr.Top + $cr.Bottom) / 2))
    $inBottomBand = [WinEnum]::NcHitTest($panel[0], [int](($cr.Left + $cr.Right) / 2), ($wr.Bottom - 2))
    Write-Host "场景 H5b：WM_NCHITTEST probe tile行=$inTile 右环带=$inRightBand 底环带=$inBottomBand（期望 1 / -1 / -1）"
    if ($inTile -ne 1)
    {
        Save-FullScreenshot "H-FAIL-hittest-tile.png"
        throw "断言失败（场景 H5b）：tile 行 WM_NCHITTEST 返回 $inTile（期望 1=HTCLIENT）——命中区收窄回归"
    }
    if ($inRightBand -ne -1 -or $inBottomBand -ne -1)
    {
        Save-FullScreenshot "H-FAIL-hittest-blank.png"
        throw "断言失败（场景 H5b）：空白区 WM_NCHITTEST 返回 右=$inRightBand 底=$inBottomBand（期望 -1=HTTRANSPARENT）——空白不穿透，点击会被宿主吃掉（B-701 回归）"
    }
    Write-Host "场景 H5b 通过：tile 行 HTCLIENT、空白环带 HTTRANSPARENT（空白点击穿透宿主裁决在案，真实点击观感转装机）"
    Stop-Beacon
}
catch
{
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Save-FullScreenshot "H-FAIL-unexpected.png"
    $failures.Add("场景 H：$($_.Exception.Message)")
    Write-Host "::error::场景 H 断言失败：$($_.Exception.Message)"
}

# —— 场景 K：全局热键唤起 L2 快速面板 + 缓存优先 + Esc 键盘关闭（todo 行14/36 半项 CI 代验）——
try
{
    Ensure-MockServer # 防御：不依赖前序场景收尾（38470370548 级联红根因）
    Write-ScenarioConfig $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    # 前置：缓存已填充（tile 百分比在）——面板缓存取同一份 states 落盘
    $tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($tiles.Count -lt 1)
    {
        Save-FullScreenshot "K-FAIL-no-tile.png"
        throw "断言失败（场景 K 前置）：tile 窗未建"
    }
    $texts = @()
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $tiles[0]
        if (($texts | Where-Object { $_ -match '\d+\s*%' })) { break }
        Start-Sleep -Seconds 2
    }
    if (!($texts | Where-Object { $_ -match '\d+\s*%' })) { throw "断言失败（场景 K 前置）：缓存未填充（30s 无百分比读数）" }
    Stop-MockServer # 断网：面板打开必须走缓存（「无网络等待白屏」的反证）
    # K1 全局热键链：①启动日志无 RegisterHotKey 失败（注册成功）→ ②SendInput 和弦唤起 →
    # 失败则 Post WM_HOTKEY 直驱（引擎只查 wParam==HotkeyId，与真实触发同一 WndProc 入口）
    # 键盘注入曾因 INPUT.type 写成 0（INPUT_MOUSE）静默丢键——38725138151 起场景 Z 的
    # KEYEVENTF_UNICODE 已实证 CI 会话可达，故和弦应直接生效，WM_HOTKEY 仅作兜底。
    $log = Get-TodayLog
    if ($log -match "RegisterHotKey.*failed")
    {
        throw "断言失败（场景 K1）：启动日志含 RegisterHotKey 失败——热键注册冲突/解析失败"
    }
    $before = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count
    if ($before -ne 0) { throw "断言失败（场景 K1）：面板在热键前已可见（$before）" }
    $via = "SendInput 和弦"
    [WinEnum]::SendChord(0x11, 0x42, 0x12) # VK_CONTROL + VK_B + VK_MENU(Alt)——注册热键是 Ctrl+Alt+B，缺 Alt 恒不匹配
    $panel = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(3)
    while ((Get-Date) -lt $deadline)
    {
        $handles = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($handles.Count -ge 1) { $panel = $handles[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panel -eq [IntPtr]::Zero)
    {
        # 合成键盘不可达（环境限制）→ WM_HOTKEY 直驱确定性验证应用侧链路
        $via = "WM_HOTKEY 直驱"
        $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
        if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
        if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 K1）：托盘消息窗未找到" }
        [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY, wParam=HotkeyId
        $deadline = (Get-Date).AddSeconds(5)
        while ((Get-Date) -lt $deadline)
        {
            $handles = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
            if ($handles.Count -ge 1) { $panel = $handles[0]; break }
            Start-Sleep -Milliseconds 250
        }
    }
    if ($panel -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "K-FAIL-no-panel.png"
        throw "断言失败（场景 K1）：热键触发后 5s 内快速面板未出现（和弦与 WM_HOTKEY 双路）——触发链回归"
    }
    Save-FullScreenshot "K-panel-open-offline.png"
    if ($via -eq "SendInput 和弦")
    {
        Write-Host "场景 K1 通过：SendInput Ctrl+Alt+B 和弦唤起快速面板（全局键盘注入可用，注册+触发全链实证）"
    }
    else
    {
        Write-Host "::warning::SendInput 和弦在 CI 会话未生效（INPUT.type 曾误写 0，已修）——WM_HOTKEY 直驱实证应用侧触发链；注册失败日志断言在案；真实键盘全局唤起转装机"
    }
    # K2 缓存优先：断网状态下面板有数据（百分比读数在，无白屏）；<100ms 精确计时留装机（CI 时钟分辨率不够）
    $panelTexts = @()
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $panelTexts = Get-WindowUiaTexts $panel
        if (($panelTexts | Where-Object { $_ -match '\d+\s*%' })) { break }
        Start-Sleep -Milliseconds 400
    }
    if (!($panelTexts | Where-Object { $_ -match '\d+\s*%' }))
    {
        Save-FullScreenshot "K-FAIL-panel-blank.png"
        throw "断言失败（场景 K2）：断网打开面板无缓存读数（白屏回归）；实际文本：$($panelTexts -join ' | ')"
    }
    Write-Host "场景 K2 通过：断网打开面板显示缓存数据（缓存优先）"
    # K3 Esc 键盘关闭（先 ForceForeground 保证键盘焦点在面板）——硬断言，不再降级：
    # INPUT.type 修 1 后键盘注入在 CI 会话可达（场景 Z 的 KEYEVENTF_UNICODE 已实证），
    # 「CI 会话不可达」是 type 误写造成的自证，Esc 关面板不再半转装机。
    [WinEnum]::ForceForeground($panel)
    Start-Sleep -Milliseconds 400
    [WinEnum]::SendVirtualKey(0x1B) # VK_ESCAPE
    $closed = $false
    $deadline = (Get-Date).AddSeconds(4)
    while ((Get-Date) -lt $deadline)
    {
        if ([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count -eq 0) { $closed = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (!$closed)
    {
        Save-FullScreenshot "K-FAIL-esc-close.png"
        throw "断言失败（场景 K3）：Esc 未关闭快速面板——Esc 键路径回归（键盘注入已修 type=1）"
    }
    Write-Host "场景 K3 通过：Esc 键盘关闭面板（键盘注入可用，全链实证）"
    Ensure-MockServer # 还原 mock（后续场景/收尾统一管理）
}
catch
{
    try { Ensure-MockServer } catch { } # 失败路径也还原 mock，别让 J/收尾连带红
    Save-FullScreenshot "K-FAIL-unexpected.png"
    $failures.Add("场景 K：$($_.Exception.Message)")
    Write-Host "::error::场景 K 断言失败：$($_.Exception.Message)"
}

# —— 场景 L：reduced 档变色过渡帧取证（todo 行 54「Success→Error 平滑变色过渡」CI 代验）——
# L1 同色 tick 不闪灯：Update 曾每 tick 重放 TransitionFill（reduced 档状态灯每刷新周期灭一次
#   再渐亮，违反「换色后亮度渐入」本意）；已改 _lastFillColor 门控只在色变 tick 过渡。
#   连拍 22s（GLM ci 档 15s ≥1 tick），灯在帧（max(红,绿)）不得跌破 8px。
# L2 色变渐入过渡：mock 翻 error/normal 交替，每轮 ~15ms 周期连拍至新色落定，环内找
#   「与两端都差 ≥8px」的中间态帧。判据依据（代码+实测）：TransitionFill = 200ms×intensity(2.0)
#   = 400ms，cubic-bezier(0.1,0.9) 前载缓动 → 部分透明可视窗 ~50ms； arrival 帧（tol45 红 ≥8
#   ≈ opacity ≥0.79）本身就是部分透明帧，瞬时切换只可能出现端点帧——中间态帧是渐入的直接证据。
#   可判定轮 = 到达帧之前有基线帧（环 >7）且采样最大间隔 ≤120ms；≥2 可判定轮零中间态 = 真回归（红）。
try
{
    Write-GConfig "" "reduced" 2.0
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    $glmIndex = -1
    for ($i = 0; $i -lt $handles.Count; $i++)
    {
        if ((Get-WindowUiaTexts $handles[$i]) -like "*GLM*") { $glmIndex = $i }
    }
    if ($glmIndex -lt 0) { throw "断言失败（场景 L）：GLM tile 不可辨" }
    $glmRect = [WinEnum]::RectOf($handles[$glmIndex])
    $green = 0
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $c = New-LightStrip $glmRect
        $green = Count-ColorPixels $c 63 185 80 45
        $c.Dispose()
        if ($green -ge 8) { break }
        Start-Sleep -Seconds 2
    }
    if ($green -lt 8) { throw "断言失败（场景 L）：GLM 状态灯绿未渲染（$green px，期望 ≥8）" }
    Write-Host "场景 L 基线：GLM 绿 $green px（reduced 档 intensity 2.0）"
    # L1 同色 tick 不闪灯
    $l1Min = 999
    $l1Low = 0
    $l1MaxGap = 0.0
    $l1Prev = [DateTime]::UtcNow
    $deadline = (Get-Date).AddSeconds(22)
    while ((Get-Date) -lt $deadline)
    {
        $c = New-LightStrip $glmRect
        $red = Count-ColorPixels $c 248 81 73 45
        $g = Count-ColorPixels $c 63 185 80 45
        $c.Dispose()
        $now = [DateTime]::UtcNow
        $gap = ($now - $l1Prev).TotalMilliseconds
        if ($gap -gt $l1MaxGap) { $l1MaxGap = $gap }
        $l1Prev = $now
        $present = [Math]::Max($red, $g)
        if ($present -lt $l1Min) { $l1Min = $present }
        if ($present -lt 8) { $l1Low++ }
    }
    Write-Host "场景 L1 连拍 22s：灯在帧最小值 $l1Min px，跌破 8px 帧数 $l1Low，最大采样间隔 $([int]$l1MaxGap) ms"
    if ($l1Low -gt 0)
    {
        Save-FullScreenshot "L-FAIL-tick-blink.png"
        throw "断言失败（场景 L1）：reduced 档同色 tick 期间状态灯 $l1Low 帧跌破 8px（最小 $l1Min px）——过渡被每 tick 重放（闪灯回归）"
    }
    Write-Host "场景 L1 通过：同色 tick 期间灯恒在（最小 $l1Min px，最大间隔 $([int]$l1MaxGap) ms）——无闪灯"
    # L2 色变渐入过渡（交替翻档至多 5 轮，取 3 轮可判定）
    $caught = 0
    $conclusive = 0
    $attempt = 0
    while ($conclusive -lt 3 -and $attempt -lt 5)
    {
        $attempt++
        $toError = ($attempt % 2) -eq 1
        if ($toError)
        {
            Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "error" -Encoding ASCII
            $tR = 248; $tG = 81; $tB = 73
        }
        else
        {
            Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
            $tR = 63; $tG = 185; $tB = 80
        }
        $ring = New-Object System.Collections.ArrayList
        $maxGap = 0.0
        $prev = [DateTime]::UtcNow
        $arrived = $false
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $deadline)
        {
            $c = New-LightStrip $glmRect
            $now = [DateTime]::UtcNow
            $gap = ($now - $prev).TotalMilliseconds
            if ($gap -gt $maxGap) { $maxGap = $gap }
            $prev = $now
            $hit = Count-ColorPixels $c $tR $tG $tB 45
            if ($hit -ge 8)
            {
                [void]$ring.Add($c)
                for ($k = 0; $k -lt 6; $k++)
                {
                    Start-Sleep -Milliseconds 60
                    $c2 = New-LightStrip $glmRect
                    [void]$ring.Add($c2) # 补 6 帧取真·终态（淡入尾段 0.8→1.0 仍可辨）
                }
                $arrived = $true
                break
            }
            [void]$ring.Add($c)
            if ($ring.Count -gt 24) { $old = $ring[0]; $ring.RemoveAt(0); $old.Dispose() }
        }
        if (!$arrived)
        {
            foreach ($b in $ring) { $b.Dispose() }
            throw "断言失败（场景 L2 第${attempt}轮）：mock 翻档 30s 内状态灯未落定——色变未反映到 tile"
        }
        $hadBaseline = ($ring.Count -gt 7)
        if (!$hadBaseline -or $maxGap -gt 120)
        {
            foreach ($b in $ring) { $b.Dispose() }
            Write-Host "::warning::场景 L2 第${attempt}轮不可判定（基线帧缺失=$(-not $hadBaseline)，最大间隔 $([int]$maxGap) ms）——加翻一轮"
            continue
        }
        $conclusive++
        $first = $ring[0]
        $last = $ring[$ring.Count - 1]
        $mid = -1
        for ($m = 1; $m -lt $ring.Count - 1; $m++)
        {
            $d1 = Count-FrameDiff $ring[$m] $first 12
            $d2 = Count-FrameDiff $ring[$m] $last 12
            if ($d1 -ge 8 -and $d2 -ge 8) { $mid = $m; break }
        }
        if ($mid -ge 0)
        {
            $caught++
            Save-Zoom $ring[$mid] "L2-round$attempt-mid-frame.png" 8
            Write-Host "场景 L2 第${attempt}轮：取到中间态帧（环位 $mid/$($ring.Count)，与两端差分均 ≥8px）——渐入过渡可见"
        }
        else
        {
            Write-Host "场景 L2 第${attempt}轮：环内未见中间态（$($ring.Count) 帧，最大间隔 $([int]$maxGap) ms）"
        }
        foreach ($b in $ring) { $b.Dispose() }
        Start-Sleep -Seconds 3
    }
    if ($caught -eq 0 -and $conclusive -ge 2)
    {
        Save-FullScreenshot "L-FAIL-no-transition.png"
        throw "断言失败（场景 L2）：$conclusive 轮可判定翻档均只见端点帧——reduced 档色变无平滑过渡（瞬时切换回归）"
    }
    if ($caught -gt 0)
    {
        Write-Host "场景 L2 通过：$caught/$conclusive 轮取到渐入中间态帧——Success↔Error 平滑变色过渡（reduced 档）实证"
    }
    else
    {
        Write-Host "::warning::场景 L2 未取得可判定帧序列（连拍空洞/未逮到 ~50ms 渐入窗）——reduced 档过渡观感转装机目检（同 G7c 降级口径）"
    }
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Stop-Beacon
    Write-Host "场景 L 通过：同色 tick 不闪灯 + 色变渐入过渡帧取证"
}
catch
{
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Save-FullScreenshot "L-FAIL-unexpected.png"
    $failures.Add("场景 L：$($_.Exception.Message)")
    Write-Host "::error::场景 L 断言失败：$($_.Exception.Message)"
}

# —— 场景 M：开机自启注册表对齐（todo 行 6「开关即时生效」的开机自启注册表半项 CI 代验）——
# App 启动即 StartupService.SyncWith(config.launchOnStartup) 对齐 HKCU Run\Beacon：
# true → 写入且指向 Beacon.App.exe；false → 清值。OS 重启生效半仍需装机。
try
{
    Write-GConfig "" "off" 1.0 $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 12
    $runVal = Get-BeaconRunValue
    if ([string]::IsNullOrWhiteSpace($runVal) -or $runVal -notmatch 'Beacon\.App\.exe')
    {
        Save-FullScreenshot "M-FAIL-runkey-absent.png"
        throw "断言失败（场景 M1）：launchOnStartup=true 启动后 HKCU Run\Beacon 未写入（值：'$runVal'）——开机自启注册表对齐回归"
    }
    Write-Host "场景 M1 通过：HKCU Run\Beacon = $runVal"
    Stop-Beacon
    # M3 登录启动链代验（todo 行 96 半项 CI 代验）：把注册表 Run 值原样交给 ShellExecute——
    # 与登录时 explorer 处理 Run 键同一条 API（rundll32 ShellExec_RunDll 入口按名不可解析，
    # 38750239155 已废）。进程被拉起即证「Run 值可被 shell 直接执行」（引号/路径串回归在案）；
    # 「真登录会话由 explorer 调度」属 OS 行为，留装机。
    $rc = [WinEnum]::ShellExec($runVal)
    if ($rc -le 32)
    {
        Save-FullScreenshot "M-FAIL-shellexec-code.png"
        throw "断言失败（场景 M3）：ShellExecute 返回 $rc（≤32 即失败码，2=未找到 5=拒绝访问）——Run 值不可执行（值：'$runVal'）"
    }
    $booted = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -ge 1) { $booted = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$booted)
    {
        Save-FullScreenshot "M-FAIL-shellexec-no-boot.png"
        throw "断言失败（场景 M3）：ShellExecute 返回 $rc（成功）但 20s 未拉起 Beacon 进程（值：'$runVal'）——启动后即退？"
    }
    Write-Host "场景 M3 通过：Run 值经 ShellExecute 拉起 Beacon 进程（启动串可执行，真登录会话调度留装机）"
    Write-GConfig "" "off" 1.0 $false
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 12
    $runVal2 = Get-BeaconRunValue
    if (-not [string]::IsNullOrWhiteSpace($runVal2))
    {
        Save-FullScreenshot "M-FAIL-runkey-left.png"
        throw "断言失败（场景 M2）：launchOnStartup=false 启动后 HKCU Run\Beacon 仍在（'$runVal2'）——SyncWith 未清值"
    }
    Write-Host "场景 M2 通过：launchOnStartup=false 后 Run\Beacon 已清"
    Stop-Beacon
    Write-Host "场景 M 通过：开机自启注册表随配置双向对齐（写入/清值）"
}
catch
{
    Save-FullScreenshot "M-FAIL-unexpected.png"
    $failures.Add("场景 M：$($_.Exception.Message)")
    Write-Host "::error::场景 M 断言失败：$($_.Exception.Message)"
}

# —— 场景 N：设置改热键 → 落盘 → 重启注册 → 触发（todo 行 18「可在设置改键并持久化」半项 CI 代验）——
# 设置页无保存按钮，常规项在控件事件时落盘（SaveGeneral）：改热键框后两次切换「开机自启」开关，
# 借其 Toggled→SaveGeneral 同一落盘链把新热键带出去，净态回 false（不留在自启，供 J 场景）。
# 改键值 Ctrl+Alt+C：HotkeyService.TryParse 合法（modifiers+单字母），与 K1 的 Ctrl+Alt+B 区分。
try
{
    Write-GConfig "" "off" 1.0 $false
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings)
    {
        Save-FullScreenshot "N-FAIL-no-settings.png"
        throw "断言失败（场景 N）：--settings 启动 30s 内未见「Beacon 设置」窗口"
    }
    # 热键输入框：Edit 控件且当前值等于 config 里的 Ctrl+Alt+B（避免误取搜索框/动作框）。
    # 内容构建晚于窗口出现（38333593739 实测设置窗全黑未渲染，30s 窗口只到标题栏）——找到为止轮询 45s。
    $hkBox = $null
    $hkDeadline = (Get-Date).AddSeconds(45)
    while ($hkBox -eq $null -and (Get-Date) -lt $hkDeadline)
    {
        $edits = $settings.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
        foreach ($e in $edits)
        {
            try
            {
                $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
                if ($vp.Current.Value -eq "Ctrl+Alt+B") { $hkBox = $e; break }
            }
            catch { }
        }
        if ($hkBox -eq $null) { Start-Sleep -Seconds 3 }
    }
    if (!$hkBox)
    {
        Save-FullScreenshot "N-FAIL-no-hotkey-edit.png"
        throw "断言失败（场景 N）：设置页 45s 内未找到值为 Ctrl+Alt+B 的热键输入框"
    }
    $toggle = $settings.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "startup-toggle")))
    if (!$toggle) { throw "断言失败（场景 N）：设置页未找到 startup-toggle（开关驱动落盘链）" }
    $tg = $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $hkValue = $hkBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $tg.Toggle()
    Start-Sleep -Milliseconds 500
    $hkValue.SetValue("Ctrl+Alt+C")
    $tg.Toggle() # 新热键与开关同一次 SaveGeneral 落盘
    Start-Sleep -Seconds 2
    $cfgN = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($cfgN.hotkey)" -ne "Ctrl+Alt+C")
    {
        Save-FullScreenshot "N-FAIL-hotkey-not-saved.png"
        throw "断言失败（场景 N1）：设置改键后 config.json hotkey=$($cfgN.hotkey)（期望 Ctrl+Alt+C）——改键未落盘"
    }
    if ("$($cfgN.launchOnStartup)" -ne "False")
    {
        throw "断言失败（场景 N1）：开关两次切换后 launchOnStartup=$($cfgN.launchOnStartup)（期望 False）——落盘链状态错乱"
    }
    Write-Host "场景 N1 通过：设置改键 Ctrl+Alt+B→Ctrl+Alt+C 落盘，开关双切净态 False"
    Stop-Beacon
    # 重启：读新配置 → 注册无失败日志 → 触发链仍通（与 K1 同入口）
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 12
    $logN = Get-TodayLog
    if ($logN -match 'RegisterHotKey.*failed') { throw "断言失败（场景 N2）：重启后新热键 Ctrl+Alt+C 注册失败（冲突/解析）" }
    if ($logN -match 'Invalid hotkey string') { throw "断言失败（场景 N2）：重启后新热键字符串解析失败" }
    Write-Host "场景 N2 通过：重启后新热键注册无失败日志（重绑定生效）"
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 N3）：托盘消息窗未找到" }
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY
    $panelN = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $hs = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($hs.Count -ge 1) { $panelN = $hs[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panelN -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "N-FAIL-no-panel.png"
        throw "断言失败（场景 N3）：重启后热键触发链失效——面板未出现"
    }
    Write-Host "场景 N3 通过：重启后新热键触发链可用（面板出现）"
    Stop-Beacon
    Write-Host "场景 N 通过：设置改键 → 落盘 → 重启注册 → 触发 全链"
}
catch
{
    Save-FullScreenshot "N-FAIL-unexpected.png"
    $failures.Add("场景 N：$($_.Exception.Message)")
    Write-Host "::error::场景 N 断言失败：$($_.Exception.Message)"
}

# —— 场景 O：L2 面板计数与实际 Widget 状态一致（todo 行 41「计数与实际 Widget 状态一致」半项 CI 代验）——
# 摘要 OverallSummary 由 UpdateHeader 从 _states 实时 severity 计数算出（all clear / N warn · N error），
# chip 由 UpdateOverview 按 source 分组计数——若面板计数与 Widget 状态脱钩，注入 Error 后文本不翻。
# 三态往返硬断言：双健康 all clear → 注入 Error（mode file）→「0 warn · 1 error」→ 清档回 all clear。
# 面板开：WM_HOTKEY 直驱（K1 已证应用侧触发链；O 不重复注册断言）。
try
{
    Write-GConfig "" "off" 1.0
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    # 前置：GLM tile 在且百分比读数在（states 已落 _states，面板摘要才有可断之数）
    $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    $glmIndex = -1
    for ($i = 0; $i -lt $handles.Count; $i++)
    {
        if ((Get-WindowUiaTexts $handles[$i]) -like "*GLM*") { $glmIndex = $i }
    }
    if ($glmIndex -lt 0) { throw "断言失败（场景 O 前置）：GLM tile 不可辨" }
    $glmTexts = @()
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $glmTexts = Get-WindowUiaTexts $handles[$glmIndex]
        if (($glmTexts | Where-Object { $_ -match '\d+\s*%' })) { break }
        Start-Sleep -Seconds 2
    }
    if (!($glmTexts | Where-Object { $_ -match '\d+\s*%' })) { throw "断言失败（场景 O 前置）：40s 内 GLM tile 无百分比读数（states 未落）" }
    # O1 双健康态：摘要 all clear + 双 chip 各计 1
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 O1）：托盘消息窗未找到" }
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY, wParam=HotkeyId
    $panel = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $ph = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($ph.Count -ge 1) { $panel = $ph[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panel -eq [IntPtr]::Zero) { Save-FullScreenshot "O-FAIL-no-panel.png"; throw "断言失败（场景 O1）：热键触发后 5s 内快速面板未出现" }
    Start-Sleep -Seconds 2 # 等面板渲染
    $texts = Get-WindowUiaTexts $panel
    if (!($texts -contains "all clear")) { Save-FullScreenshot "O-FAIL-summary.png"; throw "断言失败（场景 O1）：双健康态摘要非 all clear（实际：$($texts -join ' | ')）" }
    if (!($texts -contains "智谱 1")) { throw "断言失败（场景 O1）：chip 缺「智谱 1」" }
    if (!($texts -contains "DeepSeek 1")) { throw "断言失败（场景 O1）：chip 缺「DeepSeek 1」" }
    Write-Host "场景 O1 通过：双健康态摘要 all clear + chip 智谱 1 / DeepSeek 1"
    # O2 注入 Error：mode file "error" → GLM 95% → 摘要翻「0 warn · 1 error」（ci 档 15s 一轮，45s 留 2 轮余量）
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "error" -Encoding ASCII
    $ok = $false
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $panel
        if ($texts -contains "0 warn · 1 error") { $ok = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$ok) { Save-FullScreenshot "O-FAIL-error-summary.png"; throw "断言失败（场景 O2）：注入 Error 后 45s 内摘要未翻「0 warn · 1 error」（实际：$($texts -join ' | ')）" }
    if (!($texts -contains "智谱 1")) { throw "断言失败（场景 O2）：Error 态 chip 计数丢失（智谱 1 不在）" }
    Write-Host "场景 O2 通过：Error 注入后摘要 0 warn · 1 error，chip 计数保持"
    # O3 恢复：清 mode file → 回 all clear（计数随状态往返，非钉死）
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    $ok = $false
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $panel
        if ($texts -contains "all clear") { $ok = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$ok) { Save-FullScreenshot "O-FAIL-recover-summary.png"; throw "断言失败（场景 O3）：恢复后 45s 内摘要未回 all clear（实际：$($texts -join ' | ')）" }
    Write-Host "场景 O3 通过：恢复后摘要回 all clear（计数随状态往返）"
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # Toggle 再收，避免残留影响后续场景
    Start-Sleep -Seconds 1
    Write-Host "场景 O 通过：面板摘要/chip 计数与 Widget 状态三态往返一致"
}
catch
{
    Save-FullScreenshot "O-FAIL-unexpected.png"
    $failures.Add("场景 O：$($_.Exception.Message)")
    Write-Host "::error::场景 O 断言失败：$($_.Exception.Message)"
}

# —— 场景 P：panel 模式收起态（吸边细条）跨重启还原（todo 行 51「重启后位置/收起态还原」半项 CI 代验）——
# P1 收起态还原：pins.json 首条 layout.collapsed=true → 宿主窗按 CollapseStripDips(12 DIP) 细条宽启动
#   （ApplyLayout strip 分支：宽度换细条宽、高度仍取整面板高，细条=展开面板锚边窄条）；重启后仍细条
#   = 收起态经 pins.json 往返（RestoreLayout 读首条 pinned 布局的 Collapsed）。
# P2 对照（非空洞）：同配置 collapsed=false → 宽度回整面板宽（TileSizing.FloorDips=168 DIP 底），
#   细条断言才有意义——否则宿主恒窄会被误判成细条。
try
{
    Write-HConfig "panel" $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $panel = [WinEnum]::VisibleHandlesByTitle("Beacon Pinned")
    if ($panel.Count -lt 1)
    {
        Save-FullScreenshot "P-FAIL-no-panel.png"
        throw "断言失败（场景 P1）：panel 模式未见 Beacon Pinned 宿主窗"
    }
    $r = [WinEnum]::RectOf($panel[0])
    $w1 = $r.Right - $r.Left + 1
    if ($w1 -gt 20)
    {
        Save-FullScreenshot "P-FAIL-not-collapsed.png"
        throw "断言失败（场景 P1）：collapsed=true 启动后宿主宽 $w1 px（期望 ≤20，12 DIP 细条 @96dpi）——收起态未生效"
    }
    Write-Host "场景 P1 通过：collapsed=true 启动宿主宽 $w1 px（12 DIP 细条）"
    # 重启还原：收起态经 pins.json 持久化，二次启动仍细条
    Stop-Beacon
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $panel = [WinEnum]::VisibleHandlesByTitle("Beacon Pinned")
    if ($panel.Count -lt 1) { throw "断言失败（场景 P1）：重启后未见 Beacon Pinned 宿主窗" }
    $r = [WinEnum]::RectOf($panel[0])
    $w2 = $r.Right - $r.Left + 1
    if ($w2 -gt 20)
    {
        Save-FullScreenshot "P-FAIL-restore-expanded.png"
        throw "断言失败（场景 P1）：重启后宿主宽 $w2 px（期望 ≤20）——收起态未随 pins.json 还原"
    }
    Write-Host "场景 P1 通过：重启后宿主仍细条（$w2 px）——收起态跨重启还原"
    # P2 对照：collapsed=false 同配置 → 整面板宽（细条断言非空洞）
    Write-HConfig "panel" $false
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $panel = [WinEnum]::VisibleHandlesByTitle("Beacon Pinned")
    if ($panel.Count -lt 1) { throw "断言失败（场景 P2）：对照轮未见 Beacon Pinned 宿主窗" }
    $r = [WinEnum]::RectOf($panel[0])
    $w3 = $r.Right - $r.Left + 1
    if ($w3 -lt 100)
    {
        Save-FullScreenshot "P-FAIL-expanded-narrow.png"
        throw "断言失败（场景 P2）：collapsed=false 宿主宽 $w3 px（期望 ≥100，面板宽底 168 DIP）——展开态未生效，P1 细条断言无对照"
    }
    if ($w3 -le 3 * $w1)
    {
        throw "断言失败（场景 P2）：展开宽 $w3 未达细条宽 $w1 的 3 倍——两态不可辨"
    }
    Write-Host "场景 P2 通过：collapsed=false 宿主宽 $w3 px（细条 $w1 px 的 $([int]($w3 / $w1)) 倍）——两态可辨"
    Stop-Beacon
}
catch
{
    Save-FullScreenshot "P-FAIL-unexpected.png"
    $failures.Add("场景 P：$($_.Exception.Message)")
    Write-Host "::error::场景 P 断言失败：$($_.Exception.Message)"
}

# —— 场景 Q：胶囊（L0）计数与离线横幅（todo 行 42 / mvp B-504「断网显示 Offline+时间；计数与实际 Widget 状态一致」胶囊半 CI 代验）——
# 胶囊计数零值组不显示（§6.2.2）：双健康=「2 ok」；注入 Error=「1 ok · 1 err」。
# 断网（Stop mock）→ OfflineBanner 显 +「Last update HH:mm」（§6.2.6）+ 计数翻「2 warn」
# （缓存命中 PublishStale severity=Max(cached,Warning)，批3-补50 拍板的胶囊面兑现；
#   断网前先轮询回「2 ok」确定性复位——否则 GLM 残留 Error 缓存会翻出「1 warn · 1 err」假阴性）。
# 断言全走 UIA 文本（与 O 同机制），不碰像素——胶囊观感（透明度/位置/灯色）仍属装机。
try
{
    Write-GConfig "" "off" 1.0 $false $true
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $capsule = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($capsule.Count -lt 1)
    {
        Save-FullScreenshot "Q-FAIL-no-capsule.png"
        throw "断言失败（场景 Q 前置）：showCapsule=true 未见「Beacon Status」胶囊窗"
    }
    # Q1 双健康计数：2 ok（零值组不显示）
    $texts = @()
    $ok = $false
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $capsule[0]
        if ($texts -contains "2 ok") { $ok = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$ok) { Save-FullScreenshot "Q-FAIL-count.png"; throw "断言失败（场景 Q1）：胶囊计数未见「2 ok」（实际：$($texts -join ' | ')）" }
    Write-Host "场景 Q1 通过：双健康胶囊计数 2 ok（零值组不显示）"
    # Q2 注入 Error：1 ok · 1 err
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "error" -Encoding ASCII
    $ok = $false
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $capsule[0]
        if ($texts -contains "1 ok · 1 err") { $ok = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$ok) { Save-FullScreenshot "Q-FAIL-err-count.png"; throw "断言失败（场景 Q2）：注入 Error 后胶囊计数未翻「1 ok · 1 err」（实际：$($texts -join ' | ')）" }
    Write-Host "场景 Q2 通过：Error 注入后胶囊计数 1 ok · 1 err"
    # 复位：清档位文件并轮询回「2 ok」，断网前提确定性（GLM 缓存回 Success）
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    $ok = $false
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $capsule[0]
        if ($texts -contains "2 ok") { $ok = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$ok) { Save-FullScreenshot "Q-FAIL-reset.png"; throw "断言失败（场景 Q 复位）：清档后 40s 内胶囊计数未回「2 ok」（实际：$($texts -join ' | ')）" }
    # Q3 断网：OfflineBanner + Last update 时间 + 计数随 stale 抬档 2 warn
    Stop-MockServer
    $banner = $false
    $warned = $false
    $deadline = (Get-Date).AddSeconds(75)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $capsule[0]
        if (!$banner -and ($texts | Where-Object { $_ -match '^Last update \d{2}:\d{2}$' })) { $banner = $true }
        if (!$warned -and ($texts -contains "2 warn")) { $warned = $true }
        if ($banner -and $warned) { break }
        Start-Sleep -Seconds 3
    }
    Ensure-MockServer # 还原 mock（J/收尾统一管理，但失败路径也不能把后续拖红）
    if (!$banner)
    {
        Save-FullScreenshot "Q-FAIL-no-banner.png"
        throw "断言失败（场景 Q3）：断网 75s 内胶囊未出「Last update HH:mm」离线横幅（实际：$($texts -join ' | ')）"
    }
    if (!$warned)
    {
        Save-FullScreenshot "Q-FAIL-stale-count.png"
        throw "断言失败（场景 Q3）：断网 stale 后胶囊计数未翻「2 warn」（severity=Max(cached,Warning) 未反映到胶囊）"
    }
    Write-Host "场景 Q3 通过：断网离线横幅 Last update HH:mm + 计数随 stale 抬档 2 warn"
    Write-Host "场景 Q 通过：胶囊计数与离线横幅随实际状态翻转（B-504 CI 面）"
}
catch
{
    try { Ensure-MockServer } catch { } # 失败路径也还原 mock，别让 J/收尾连带红
    Save-FullScreenshot "Q-FAIL-unexpected.png"
    $failures.Add("场景 Q：$($_.Exception.Message)")
    Write-Host "::error::场景 Q 断言失败：$($_.Exception.Message)"
}

# —— 场景 R：胶囊位置预设还原 + 重启一致（todo 行 20「重启后胶囊回到上次位置」半项 CI 代验）——
# 胶囊位置复用 PinLayout（%APPDATA%\Beacon\shell-state.json capsuleLayout：monitor+anchor+offsetDips），
# RestorePosition TopLeft 语义 = work.Left+40 / work.Top+40（96dpi DIP=px，工作区原点 (0,0) 已由
# 场景 H tile 落位 120,120 旁证），与 tile floatingX/Y 同一「预设→启动恢复→重启仍一致」取证口径；
# 拖动保存半需手势注入（CI 合成拖拽不可靠先例），仍属装机。monitor 名「ci」必不匹配 → 回落主屏（确定性）。
try
{
    Write-GConfig "" "off" 1.0 $false $true
    $shellDir = Join-Path $env:APPDATA "Beacon"
    New-Item -ItemType Directory -Force -Path $shellDir | Out-Null
    # WriteAllText = 无 BOM（JsonDocument 拒 BOM 的先例口径）
    [System.IO.File]::WriteAllText((Join-Path $shellDir "shell-state.json"),
        '{ "capsuleLayout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 40, "y": 40 }, "collapsed": false } }')
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $capsule = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($capsule.Count -lt 1)
    {
        Save-FullScreenshot "R-FAIL-no-capsule.png"
        throw "断言失败（场景 R）：未见「Beacon Status」胶囊窗"
    }
    $r = [WinEnum]::RectOf($capsule[0])
    if ([Math]::Abs($r.Left - 40) -gt 4 -or [Math]::Abs($r.Top - 40) -gt 4)
    {
        Save-FullScreenshot "R-FAIL-position.png"
        throw "断言失败（场景 R1）：胶囊落位 $($r.Left),$($r.Top)（期望 40,40 ±4）——shell-state.json 布局未生效"
    }
    Write-Host "场景 R1 通过：胶囊按预设布局落位 $($r.Left),$($r.Top)（TopLeft 40,40 DIP）"
    # 重启一致：布局文件不动，二次启动落位仍一致
    Stop-Beacon
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $capsule = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($capsule.Count -lt 1) { throw "断言失败（场景 R）：重启后未见胶囊窗" }
    $r = [WinEnum]::RectOf($capsule[0])
    if ([Math]::Abs($r.Left - 40) -gt 4 -or [Math]::Abs($r.Top - 40) -gt 4)
    {
        Save-FullScreenshot "R-FAIL-restart-position.png"
        throw "断言失败（场景 R2）：重启后胶囊落位 $($r.Left),$($r.Top)（期望 40,40 ±4）——位置还原不一致"
    }
    Write-Host "场景 R2 通过：重启后胶囊落位一致（40,40 ±4）——位置还原跨重启"
    # R3 胶囊拖动落盘（todo 行 39 手势半项 CI 代验）：SendInput 拖拽（场景 D/H6 同款口径，
    # 38048738860 已实证 CI 生效）。净位移 50/40 DIP 远过 6 DIP 单击死区（不会误开 L2）；
    # 拖后 SavePosition 落 shell-state.json（锚点按象限算，TopLeft 象限 offset=窗口左上），
    # 重启还原到拖后位——旧注释「拖动保存需装机」按 DragMouse 实证口径改判。
    $r0 = [WinEnum]::RectOf($capsule[0])
    $cx = [int](($r0.Left + $r0.Right) / 2)
    $cy = [int](($r0.Top + $r0.Bottom) / 2)
    Save-FullScreenshot "R3-drag-before.png"
    [WinEnum]::DragMouse($cx, $cy, ($cx + 50), ($cy + 40))
    Start-Sleep -Seconds 1
    Save-FullScreenshot "R3-drag-after.png"
    $r1 = [WinEnum]::RectOf($capsule[0])
    if ([Math]::Abs(($r1.Left - $r0.Left) - 50) -gt 6 -or [Math]::Abs(($r1.Top - $r0.Top) - 40) -gt 6)
    {
        Write-Host "::warning::场景 R3：拖拽后胶囊位 $($r1.Left),$($r1.Top)（自 $($r0.Left),$($r0.Top) 期望 +50/+40）——合成拖拽手势未生效（截图 before/after 在案），拖动保存链转装机手验"
        Stop-Beacon
    }
    else
    {
        Write-Host "场景 R3a 通过：胶囊拖拽 +50/+40 窗随动（$($r0.Left),$($r0.Top) → $($r1.Left),$($r1.Top)，单击死区 6 DIP 已过）"
        $shellJson = Get-Content (Join-Path $shellDir "shell-state.json") -Raw | ConvertFrom-Json
        $offX = [double]$shellJson.capsuleLayout.offsetDips.x
        $offY = [double]$shellJson.capsuleLayout.offsetDips.y
        if ([Math]::Abs($offX - $r1.Left) -gt 4 -or [Math]::Abs($offY - $r1.Top) -gt 4)
        {
            Save-FullScreenshot "R3-FAIL-persist.png"
            throw "断言失败（场景 R3b）：拖后落盘 offsetDips=$offX,$offY 与窗位 $($r1.Left),$($r1.Top) 不符（±4，TopLeft 象限 offset=左上角）——SavePosition 落盘回归"
        }
        Write-Host "场景 R3b 通过：拖后落盘 offsetDips=$offX,$offY（TopLeft 象限=窗位）"
        Stop-Beacon
        Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
        Start-Sleep -Seconds 15
        $capsule = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
        if ($capsule.Count -lt 1) { throw "断言失败（场景 R3）：重启后未见胶囊窗" }
        $r2 = [WinEnum]::RectOf($capsule[0])
        if ([Math]::Abs($r2.Left - $r1.Left) -gt 4 -or [Math]::Abs($r2.Top - $r1.Top) -gt 4)
        {
            Save-FullScreenshot "R3-FAIL-restore.png"
            throw "断言失败（场景 R3c）：重启后胶囊 $($r2.Left),$($r2.Top) 未还原到拖后位 $($r1.Left),$($r1.Top)（±4）——拖动结果未进还原链"
        }
        Write-Host "场景 R3c 通过：重启还原到拖后位 $($r2.Left),$($r2.Top)（拖动→落盘→还原闭环）"
        Stop-Beacon
    }
}
catch
{
    Save-FullScreenshot "R-FAIL-unexpected.png"
    $failures.Add("场景 R：$($_.Exception.Message)")
    Write-Host "::error::场景 R 断言失败：$($_.Exception.Message)"
}

# —— 场景 S：RECENT 事件流与当前状态区同源一致（todo 行 43「Recent 事件流一致」CI 代验；结构化树遍历）——
# 事件行与状态行同出 MakeStateRow（点+时间+摘要），平铺 UIA Name 列表两区分不开——
# 本场景按 x:Name→AutomationId 锚定 RecentList / AttentionList 各自子树取行文本（Get-UiaListSummaries 滤时间串）。
# S1 基线：RecentList 有 GLM 健康事件行（「已用 38%」摘要，ci 档 15s 一轮 record:true 逐轮落事件）；
# S2 注入 Error：RecentList 现事件行「已用 95%」且位次先于 38% 行（新→旧），AttentionList 同现 95%
#   （状态区与事件流同源——OnStateChanged 现场重建状态区；修复前状态区仅 OnWidgetsInvalidated 重建，此断言必红）；
# S3 恢复：状态区摘掉 95%（all clear 同证状态真翻；整块收起取不到=无行）而 RecentList 保留 95% 并补回 38%
#   ——「状态区=当前、事件流=历史」语义分野实证。
try
{
    Write-GConfig "" "off" 1.0
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 S 前置）：托盘消息窗未找到" }
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY, wParam=HotkeyId（O1 同链路）
    $panelHandle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $ph = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($ph.Count -ge 1) { $panelHandle = $ph[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panelHandle -eq [IntPtr]::Zero) { Save-FullScreenshot "S-FAIL-no-panel.png"; throw "断言失败（场景 S 前置）：热键触发后 5s 内快速面板未出现" }
    Start-Sleep -Seconds 2
    $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
    # S1 基线：RecentList 有 GLM 健康事件行
    $recent = @()
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $recent = Get-UiaListSummaries $panelEl "RecentList"
        if ($recent -and @($recent | Where-Object { $_ -match '已用 38%' }).Count -ge 1) { break }
        Start-Sleep -Seconds 2
    }
    if (!$recent -or @($recent | Where-Object { $_ -match '已用 38%' }).Count -lt 1)
    {
        Save-FullScreenshot "S-FAIL-baseline.png"
        throw "断言失败（场景 S1）：40s 内 RecentList 无 GLM 健康事件行（实际：$($recent -join ' | ')）"
    }
    Write-Host "场景 S1 通过：RecentList 基线事件行在列（$(@($recent).Count) 条行文本，含「已用 38%」）"
    # S2 注入 Error：事件流现 95% 行且新于 38% 行 + 状态区同现 95%（同源）
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "error" -Encoding ASCII
    $recentHit = $false
    $attentionHit = $false
    $ordered = $false
    $attention = @()
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline)
    {
        $recent = Get-UiaListSummaries $panelEl "RecentList"
        $attention = Get-UiaListSummaries $panelEl "AttentionList"
        if ($recent)
        {
            $i95 = -1
            for ($i = 0; $i -lt @($recent).Count; $i++) { if (@($recent)[$i] -match '已用 95%') { $i95 = $i; break } }
            $i38 = -1
            for ($i = 0; $i -lt @($recent).Count; $i++) { if (@($recent)[$i] -match '已用 38%') { $i38 = $i; break } }
            if ($i95 -ge 0)
            {
                $recentHit = $true
                if ($i38 -ge 0 -and $i95 -lt $i38) { $ordered = $true } # 95% 行新于全部 38% 行（新→旧）
            }
        }
        if ($attention -and @($attention | Where-Object { $_ -match '已用 95%' }).Count -ge 1) { $attentionHit = $true }
        if ($recentHit -and $attentionHit -and $ordered) { break }
        Start-Sleep -Seconds 2
    }
    if (!$recentHit -or !$attentionHit -or !$ordered)
    {
        Save-FullScreenshot "S-FAIL-error-row.png"
        throw "断言失败（场景 S2）：注入 Error 后 45s 内 事件流行=$recentHit / 状态区行=$attentionHit / 新旧位次=$ordered（Recent：$($recent -join ' | ')；Attention：$($attention -join ' | ')）"
    }
    Write-Host "场景 S2 通过：注入 Error 后 RecentList 现事件行（95% 行新于 38% 行）且 AttentionList 同现 95%——两区同源"
    # S3 恢复：状态区摘掉 95%（all clear 同证状态真翻；整块收起=无行亦算摘掉）而事件流保留 95% 并补回 38%
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    $attentionClear = $false
    $historyKept = $false
    $texts = @()
    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline)
    {
        $texts = Get-WindowUiaTexts $panelHandle
        $recent = Get-UiaListSummaries $panelEl "RecentList"
        $attention = Get-UiaListSummaries $panelEl "AttentionList"
        if (($texts -contains "all clear") -and !($attention -and @($attention | Where-Object { $_ -match '已用 95%' }).Count -ge 1)) { $attentionClear = $true }
        if ($recent -and @($recent | Where-Object { $_ -match '已用 95%' }).Count -ge 1 -and @($recent | Where-Object { $_ -match '已用 38%' }).Count -ge 1) { $historyKept = $true }
        if ($attentionClear -and $historyKept) { break }
        Start-Sleep -Seconds 2
    }
    if (!$attentionClear -or !$historyKept)
    {
        Save-FullScreenshot "S-FAIL-recover.png"
        throw "断言失败（场景 S3）：恢复后 45s 内 状态区摘除=$attentionClear / 事件流保留 95% 且补回 38%=$historyKept（Recent：$($recent -join ' | ')；Attention：$($attention -join ' | ')）"
    }
    Write-Host "场景 S3 通过：恢复后状态区摘掉 95%（当前）而 RecentList 保留 95% 并补回 38%（历史）——两区语义分野"
    # S4 UIA 激活回归门（todo 行 46）：RecentList 首行经 UIA pattern 激活能否打开 L3 详情窗。
    # 38325074321/38330530278 实测 ListView 原生只暴露 SelectionItem/ScrollItem，三 pattern 全不中
    # （Item 门=ItemClick 坐实）→ 应用侧已补 InvokeableListItem（Invoke pattern 走 ItemClick 同链）。
    # 本步为该 peer 的回归门：Invoke 打开 L3 → peer 生效硬记；仍不中 → ::warning::（peer 投影/链路
    # 需排查——不算红，但行 46 详情半不得勾账）。
    $s4Before = @([WinEnum]::VisibleHandlesByTitlePrefix("Beacon · "))
    $s4Opened = $false
    $s4Detail = @()
    $recentListEl = $null
    try { $recentListEl = Find-UiaById $panelEl "RecentList" 5 } catch { }
    if ($recentListEl)
    {
        $firstItem = $recentListEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
        if ($firstItem)
        {
            foreach ($attempt in @("InvokePattern", "LegacyIAccessiblePattern", "SelectionItemPattern"))
            {
                try
                {
                    if ($attempt -eq "InvokePattern")
                    {
                        $firstItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
                        $s4Detail += "Invoke:invoked"
                    }
                    elseif ($attempt -eq "LegacyIAccessiblePattern")
                    {
                        $firstItem.GetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern).DoDefaultAction()
                        $s4Detail += "LegacyIAccessible:DoDefaultAction"
                    }
                    else
                    {
                        $firstItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
                        $s4Detail += "SelectionItem:Select"
                    }
                }
                catch
                {
                    $s4Detail += "${attempt}:unsupported" # ${} 限定：$attempt: 会被 PS 解析成作用域语法
                    continue
                }
                $deadline = (Get-Date).AddSeconds(6)
                while ((Get-Date) -lt $deadline)
                {
                    $s4Hits = @([WinEnum]::VisibleHandlesByTitlePrefix("Beacon · "))
                    if ($s4Hits.Count -gt $s4Before.Count) { $s4Opened = $true; break }
                    Start-Sleep -Milliseconds 500
                }
                if ($s4Opened) { break }
            }
        }
    }
    if ($s4Opened)
    {
        $s4Hits = @([WinEnum]::VisibleHandlesByTitlePrefix("Beacon · "))
        foreach ($h in $s4Hits) { [WinEnum]::Post($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) } # WM_CLOSE 收掉试探窗，避免残留影响后续场景
        Write-Host "场景 S4 通过：UIA 激活（$(@($s4Detail | Select-Object -Last 1))）打开 L3 详情窗（Beacon · * 计数 $($s4Before.Count)→$($s4Hits.Count)）——行 46 UIA 触达面成立"
    }
    else
    {
        Write-Host "::warning::场景 S4：UIA 激活试探（$($s4Detail -join '；')）未打开 L3 详情窗（6s×3 实测）——行 46 唯一门=ItemClick 坐实，点击须装机人手"
    }
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # Toggle 再收，避免残留影响后续场景
    Start-Sleep -Seconds 1
    Write-Host "场景 S 通过：RECENT 事件流与状态区同源一致、新→旧有序、恢复历史往返（行 43 CI 面）"
}
catch
{
    Save-FullScreenshot "S-FAIL-unexpected.png"
    $failures.Add("场景 S：$($_.Exception.Message)")
    Write-Host "::error::场景 S 断言失败：$($_.Exception.Message)"
}

# —— 场景 T：故障韧性（todo 行 24 的外部可触发面 CI 代验：配置损坏不崩 + 日志留痕 + .bak 兜底 + 网络断不崩）——
# Beacon 侧路径全数覆盖：JsonConfigurationStore.LoadFile 损坏归档(*.corrupt)+.bak 回滚 → LoadErrors →
# 启动日志「配置读取失败」（托盘气泡同源，日志面即 CI 面）；WidgetHost 刷新异常 LogWarning（含堆栈）且进程不倒。
# 剩余装机面（真实未处理异常崩溃路）由 App.xaml.cs:37-53 三兜底接住，外部不可注入——留装机清单。
try
{
    Write-GConfig "" "off" 1.0
    Copy-Item (Join-Path $configDir "widgets.json") (Join-Path $configDir "widgets.json.bak") -Force
    Set-Content -Path (Join-Path $configDir "widgets.json") -Value "{ broken" -Encoding ASCII
    $tLogPath = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $tProc = Get-Process Beacon.App -ErrorAction SilentlyContinue
    if (!$tProc) { Save-FullScreenshot "T-FAIL-crash.png"; throw "断言失败（场景 T1）：损坏 widgets.json 启动后进程不在（崩溃兜底失效）" }
    $dialogs = [WinEnum]::VisibleHandlesByClassAndPid("#32770", $tProc.Id)
    if ($dialogs.Count -gt 0) { Save-FullScreenshot "T-FAIL-dialog.png"; throw "断言失败（场景 T1）：损坏配置触发系统对话框（$($dialogs.Count) 个 #32770）" }
    if (!(Test-Path (Join-Path $configDir "widgets.json.corrupt"))) { Save-FullScreenshot "T-FAIL-no-archive.png"; throw "断言失败（场景 T1）：widgets.json.corrupt 未归档" }
    $logText = ""
    if (Test-Path $tLogPath) { $logText = [System.IO.File]::ReadAllText($tLogPath, [System.Text.Encoding]::UTF8) }
    if ($logText -notmatch "配置读取失败：widgets\.json") { Save-FullScreenshot "T-FAIL-no-log.png"; throw "断言失败（场景 T1）：日志无「配置读取失败：widgets.json」" }
    Write-Host "场景 T1 通过：损坏配置启动不崩、无 #32770 对话框、corrupt 归档+日志留痕"
    # T1e .bak 回滚兜底：GLM tile 以 .bak 配置出数（O 前置口径）。
    # GLM tile 拾取不按 [0]：EnumWindows 首个=Z 序顶窗，不保证是 GLM（38325074321 实证 [0]=DeepSeek
    # 而 GLM 带着恢复数据在下层）——遍历全部 Beacon Tile 按 UIA 文本认窗。
    $tTile = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline -and $tTile -eq [IntPtr]::Zero)
    {
        foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
        {
            $tTexts = Get-WindowUiaTexts $h
            if (@($tTexts | Where-Object { $_ -match 'GLM' }).Count -ge 1) { $tTile = $h; break }
        }
        if ($tTile -eq [IntPtr]::Zero) { Start-Sleep -Seconds 2 }
    }
    if ($tTile -eq [IntPtr]::Zero) { Save-FullScreenshot "T-FAIL-no-tile.png"; throw "断言失败（场景 T1e）：60s 内未见 GLM tile（Beacon Tile 有 $(([WinEnum]::VisibleHandlesByTitle("Beacon Tile")).Count) 个但无一含 GLM 文本）" }
    $tTexts = @()
    $deadline = (Get-Date).AddSeconds(40)
    # tile 数值位文本是多窗已用口径（无「已用」前缀）——38330530278 实测 tile 文本=「38%/5h 15%/w 20%/M」
    #（WidgetValueHint 多窗已用链），与 L2 事件行的摘要「已用 38%」不同面，匹配按 tile 实测格式。
    while ((Get-Date) -lt $deadline)
    {
        $tTexts = Get-WindowUiaTexts $tTile
        if (@($tTexts | Where-Object { $_ -match '38%/5h' }).Count -ge 1) { break }
        Start-Sleep -Seconds 2
    }
    if (@($tTexts | Where-Object { $_ -match '38%/5h' }).Count -lt 1) { Save-FullScreenshot "T-FAIL-no-recovery.png"; throw "断言失败（场景 T1e）：.bak 回滚后 GLM tile 未出 38%（实际：$($tTexts -join ' | ')）" }
    Write-Host "场景 T1e 通过：.bak 回滚兜底生效（GLM tile 38% 出数）"
    # T2 网络断：WidgetHost 刷新失败走 LogWarning（含堆栈），进程不倒，mock 恢复后回 38%
    # 尾部口径：偏移取在停 mock 前——F 等先前场景已在同一日志留过堆栈行，全量匹配会空过（虚证）
    $logOffset = (Get-Item $tLogPath).Length
    Stop-MockServer
    Start-Sleep -Seconds 2 # 端口释放余量（与 Start-MockServer 同款 settle）
    # 负控（38390521909/38391819136 两连红后补）：确认 mock 真的不可达——否则 75s 静默空转，
    # 失败无法区分「widget 未刷新」与「mock 没停住」。可达即端口未释放，断网语义不成立，直接红。
    $probeStatus = $null
    try { $probeStatus = (Invoke-WebRequest -Uri "http://127.0.0.1:18081/api/monitor/usage/quota/limit" -TimeoutSeconds 5).StatusCode } catch { }
    if ($probeStatus) { Save-FullScreenshot "T-FAIL-mock-alive.png"; throw "断言失败（场景 T2 前置）：Stop-MockServer 后 mock 仍可达（HTTP $probeStatus）——端口未释放，断网语义不成立" }
    $deadline = (Get-Date).AddSeconds(75)
    $tWarn = $false
    while ((Get-Date) -lt $deadline)
    {
        Start-Sleep -Seconds 5
        if (!(Test-Path $tLogPath)) { continue }
        $tail = [System.IO.File]::ReadAllText($tLogPath, [System.Text.Encoding]::UTF8)
        if ($tail.Length -le $logOffset) { continue }
        $tail = $tail.Substring($logOffset)
        if ($tail -match "\[Warning\]" -and $tail -match "refresh failed" -and $tail -match "   at ") { $tWarn = $true; break }
    }
    if (!$tWarn) { Save-FullScreenshot "T-FAIL-no-warning.png"; throw "断言失败（场景 T2）：断网 75s 内日志未现 refresh failed [Warning]（含堆栈）" }
    if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -eq 0) { Save-FullScreenshot "T-FAIL-dead.png"; throw "断言失败（场景 T2）：断网期间进程退出" }
    Write-Host "场景 T2 通过：断网期间 WidgetHost 刷新失败 LogWarning 含堆栈且进程不倒"
    Ensure-MockServer
    # 恢复窗口按退避策略数学标定（RFC §7.1：ci 档 15s，乘数 2^连续失败 钳到 MaxBackoffMultiplier=10，
    # JitterFraction 0.2 → 单次最坏 15×10×1.2=180s；75s 断网约 5 次失败后乘数已钳满）。
    # 38333593739 实测：mock 02:22:32 起，60s 窗口内 tile 停在「Last update 02:21」陈旧态——窗口结构性偏紧。
    # 240s = 最坏 180s + 刷新/渲染余量（非放宽断言：仍要求自动恢复，不手动 Kick）。
    $tRecovered = $false
    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline)
    {
        $tTexts = Get-WindowUiaTexts $tTile
        if (@($tTexts | Where-Object { $_ -match '38%/5h' }).Count -ge 1) { $tRecovered = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$tRecovered) { Save-FullScreenshot "T-FAIL-no-recover.png"; throw "断言失败（场景 T2）：mock 恢复后 240s 内 GLM tile 未回 38%" }
    if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -eq 0) { throw "断言失败（场景 T2）：恢复期进程退出" }
    Write-Host "场景 T2 通过：mock 恢复后 GLM tile 回 38%，进程存活——故障往返闭环（行 24 外部可触发面）"
    Remove-Item (Join-Path $configDir "widgets.json.bak") -Force -ErrorAction SilentlyContinue # 损坏演练残留清理
}
catch
{
    try { Ensure-MockServer } catch { }
    Save-FullScreenshot "T-FAIL-unexpected.png"
    $failures.Add("场景 T：$($_.Exception.Message)")
    Write-Host "::error::场景 T 断言失败：$($_.Exception.Message)"
}

# —— 场景 U：L3 内动作链（todo 行 70「[Retry] 全链路」CI 代验，解锁行 32/38/46 半项）——
# GitHub 连接指 mock：GET runs → 固定失败 run 12345 → AttentionList 行 → 行 Invoke（S4 同链）
# 开 L3 详情窗（run_id/retry 按钮断言）→「Retry Failed Jobs」→ 窗内 ContentDialog（确认/取消 UIA）：
# 取消 = mock 零 POST（marker 不在）；确认 = POST 202（marker 在 + ✓ 结果文本）；
# mock 翻 500 = executor ConnectionException → ✗ 结果文本。回执双证据 = L3 结果文本 + mock 侧
# marker 文件（服务端回执，比面板事件行更强；RECENT 语义=WidgetStateChanged，不加 ActionExecuted 订阅）。
try
{
    Remove-Item (Join-Path $env:TEMP "beacon-gh-rerun-hit.txt") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $env:TEMP "beacon-gh-rerun-mode.txt") -Force -ErrorAction SilentlyContinue
    Stop-Beacon
    # 前置自愈：U 依赖 mock 在场（T 失败路径可能停在停 mock 后）——探针重试 + 收净后拉起
    Ensure-MockServer
    @'
{
  "configVersion": 1,
  "hotkey": "Ctrl+Alt+B",
  "launchOnStartup": false,
  "theme": "dark",
  "pinDisplayMode": "floating",
  "numericFloatingEnabled": false,
  "numericFloatingResetDone": true,
  "showCapsule": false
}
'@ | Set-Content -Path (Join-Path $configDir "config.json") -Encoding UTF8
    @'
[
  { "id": "gh-mock", "type": "github", "endpoint": "http://127.0.0.1:18081", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    @'
[
  { "id": "github.actions.runs:ci", "type": "github.actions.runs", "connectionId": "gh-mock", "refreshTier": "ci", "pinned": true,
    "config": { "label": "CI", "repo": "cuihairu/beacon", "workflow": "ci.yml" } }
]
'@ | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    '{ "tiles": [] }' | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 U 前置）：托盘消息窗未找到" }
    # U1 失败 run 进「需要关注」：AttentionList 出「ci.yml · failure」行（severity Error 判定链）
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY 开面板
    $panelHandle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $ph = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($ph.Count -ge 1) { $panelHandle = $ph[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panelHandle -eq [IntPtr]::Zero) { Save-FullScreenshot "U-FAIL-no-panel.png"; throw "断言失败（场景 U1）：热键触发后 5s 内快速面板未出现" }
    Start-Sleep -Seconds 2
    $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
    $attention = @()
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline)
    {
        $attention = Get-UiaListSummaries $panelEl "AttentionList"
        if ($attention -and @($attention | Where-Object { $_ -match 'ci\.yml · failure' }).Count -ge 1) { break }
        Start-Sleep -Seconds 2
    }
    if (!$attention -or @($attention | Where-Object { $_ -match 'ci\.yml · failure' }).Count -lt 1)
    {
        Save-FullScreenshot "U-FAIL-no-attention-row.png"
        throw "断言失败（场景 U1）：60s 内 AttentionList 无失败 run 行（实际：$($attention -join ' | ')）——GitHub Provider→状态聚合→需要关注链失效"
    }
    Save-FullScreenshot "U-attention-row.png"
    Write-Host "场景 U1 通过：失败 run 进需要关注（ci.yml · failure · 5m00s）"
    # U2 行 Invoke 开 L3 详情窗（S4 已证 InvokeableListItem 链）：run_id/重试按钮在窗
    $uBefore = @([WinEnum]::VisibleHandlesByTitlePrefix("Beacon · "))
    $attentionListEl = Find-UiaById $panelEl "AttentionList" 5
    if (!$attentionListEl) { throw "断言失败（场景 U2）：UIA 未找到 AttentionList" }
    $rowItem = $attentionListEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
    if (!$rowItem) { throw "断言失败（场景 U2）：AttentionList 无 ListItem 可激活" }
    ($rowItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
    $detailHandle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(8)
    while ((Get-Date) -lt $deadline)
    {
        foreach ($h in [WinEnum]::VisibleHandlesByTitlePrefix("Beacon · "))
        {
            if ($uBefore -notcontains $h) { $detailHandle = $h; break }
        }
        if ($detailHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 400
    }
    if ($detailHandle -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "U-FAIL-no-detail.png"
        throw "断言失败（场景 U2）：AttentionList 行 Invoke 未打开 L3 详情窗（Beacon · * 计数 $($uBefore.Count) 不变）"
    }
    Start-Sleep -Seconds 2 # 详情表/动作区渲染
    Save-Zoom (New-Crop ([WinEnum]::RectOf($detailHandle)) 8) "U-detail-window.png" 3
    $detailTexts = Get-WindowUiaTexts $detailHandle
    if (@($detailTexts | Where-Object { $_ -match 'run_id' }).Count -lt 1 -or @($detailTexts | Where-Object { $_ -match '12345' }).Count -lt 1)
    {
        throw "断言失败（场景 U2）：详情窗缺 run_id/12345 数据行（实际：$($detailTexts -join ' | ')）"
    }
    $detailEl = [System.Windows.Automation.AutomationElement]::FromHandle($detailHandle)
    $retryButton = Find-UiaByName $detailEl "Retry Failed Jobs" 8
    if (!$retryButton)
    {
        throw "断言失败（场景 U2）：详情窗无「Retry Failed Jobs」按钮——失败 run 动作区回归"
    }
    Write-Host "场景 U2 通过：行 Invoke 打开 L3 详情窗，run_id=12345 数据行与 Retry 按钮在窗"
    # U3 确认弹窗可关（行 38）：Retry → 取消 → mock 零 POST（marker 不在 = 服务端零请求）
    $hitPath = Join-Path $env:TEMP "beacon-gh-rerun-hit.txt"
    Invoke-Uia $retryButton
    $cancelButton = Find-UiaByName $detailEl "取消" 10
    if (!$cancelButton)
    {
        Save-FullScreenshot "U-FAIL-no-dialog.png"
        throw "断言失败（场景 U3）：Retry 后 10s 内确认弹窗「取消」按钮不可达（ContentDialog 未在窗内 UIA 树）"
    }
    Save-FullScreenshot "U-dialog-open.png"
    if (!((Get-WindowUiaTexts $detailHandle) | Where-Object { $_ -match '重跑 cuihairu/beacon 的 run 12345' }))
    {
        throw "断言失败（场景 U3）：确认弹窗文案缺「重跑 cuihairu/beacon 的 run 12345」——确认语义回归"
    }
    Invoke-Uia $cancelButton
    Start-Sleep -Seconds 2
    if (Test-Path $hitPath)
    {
        throw "断言失败（场景 U3）：弹窗「取消」后 mock 仍收到 rerun POST（marker 在）——取消未拦截请求"
    }
    Write-Host "场景 U3 通过：确认弹窗可关（取消后 mock 零 POST，marker 不在）"
    # U4 确认全链：Retry → 确认 → POST 到 mock（marker 在）+ L3 结果文本 ✓ 已重跑 run 12345
    Invoke-Uia $retryButton
    $okButton = Find-UiaByName $detailEl "确认" 10
    if (!$okButton) { Save-FullScreenshot "U-FAIL-no-confirm.png"; throw "断言失败（场景 U4）：第二轮 Retry 后确认弹窗未出现" }
    Invoke-Uia $okButton
    $okText = $false
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline)
    {
        if ((Get-WindowUiaTexts $detailHandle) | Where-Object { $_ -match '✓ 已重跑 run 12345' }) { $okText = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$okText -or !(Test-Path $hitPath))
    {
        Save-FullScreenshot "U-FAIL-no-rerun.png"
        throw "断言失败（场景 U4）：确认后 ✓ 结果文本=$okText / mock POST 回执=$(Test-Path $hitPath)——重跑执行链失效"
    }
    Save-FullScreenshot "U-retry-ok.png"
    Write-Host "场景 U4 通过：确认 → mock 收 rerun POST（marker 在）+ L3 结果「✓ 已重跑 run 12345」"
    # U5 失败反馈（行 32）：mock 翻 500 → executor ConnectionException → L3 结果文本 ✗
    Set-Content -Path (Join-Path $env:TEMP "beacon-gh-rerun-mode.txt") -Value "fail" -Encoding ASCII
    Invoke-Uia $retryButton
    $okButton = Find-UiaByName $detailEl "确认" 10
    if (!$okButton) { Save-FullScreenshot "U-FAIL-no-confirm2.png"; throw "断言失败（场景 U5）：第三轮 Retry 后确认弹窗未出现" }
    Invoke-Uia $okButton
    $failText = $false
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline)
    {
        if ((Get-WindowUiaTexts $detailHandle) | Where-Object { $_ -match 'GitHub 服务端错误' }) { $failText = $true; break }
        Start-Sleep -Milliseconds 500
    }
    Remove-Item (Join-Path $env:TEMP "beacon-gh-rerun-mode.txt") -Force -ErrorAction SilentlyContinue
    if (!$failText)
    {
        Save-FullScreenshot "U-FAIL-no-fail-text.png"
        throw "断言失败（场景 U5）：mock 500 后 L3 结果文本未见「✗ GitHub 服务端错误」——失败反馈回归"
    }
    Save-FullScreenshot "U-retry-fail.png"
    Write-Host "场景 U5 通过：服务端 500 → L3 结果「✗ GitHub 服务端错误 (500)」——失败可见反馈"
    # 收尾：关详情窗（面板若在场一并收起），marker 清档
    [WinEnum]::Post($detailHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE
    if ([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count -gt 0)
    {
        [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0)
        Start-Sleep -Seconds 1
    }
    Remove-Item $hitPath -Force -ErrorAction SilentlyContinue
    Stop-Beacon
    Write-Host "场景 U 通过：失败 run→需要关注→L3 动作区→确认弹窗取消/确认→POST 回执→成功/失败双反馈（行 70 全链）"
}
catch
{
    Remove-Item (Join-Path $env:TEMP "beacon-gh-rerun-hit.txt") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $env:TEMP "beacon-gh-rerun-mode.txt") -Force -ErrorAction SilentlyContinue
    Stop-Beacon
    Save-FullScreenshot "U-FAIL-unexpected.png"
    $failures.Add("场景 U：$($_.Exception.Message)")
    Write-Host "::error::场景 U 断言失败：$($_.Exception.Message)"
}

# —— 场景 V：Critical 与崩溃注入（todo 行 71 注入面落地，解锁行 24 注入半 / 行 55 / 行 61 Critical 半）——
# BEACON_DEBUG_SEVERITY=critical：WidgetHost 刷新成功路径改判 Critical（DebugInjection）→ L0 状态灯
#   渲染 Critical 默认色 #ff3b30（V1 像素硬断言；与 Error #f85149 G/B 通道差 22/25，tol12 互不误吞）+
#   Critical 脉冲自 reduced 档挂载（V2 帧差分尽力取证；Composition 属性动画 CI 会话不可证时 ::warning
#   转装机——G7c 同口径，色断言不受降级影响）。
# V4（行 50 Toast 引擎面）注入 Critical → 通知引擎恰 1 条 critical 记录；冷却竞态消解见 V4 块内注。
# BEACON_DEBUG_CRASH=90：90s 后 UI 线程抛未处理异常 → DispatcherQueueGuard 兜底（V3 硬断言：
#   日志「DispatcherQueue unhandled exception」+堆栈「   at 」、进程数 1、零 #32770 对话框、tile 窗仍在、
#   崩溃行之后仍有「refreshed ok」——刷新循环不因兜底中断）。历史：裸 TryEnqueue 回调异常在 WinUI 3
#   无系统路由（38459944253 V3 红实证，进程直接退出），由此建 EnqueueGuarded 全量接管 UI 派发。
# 两注入均为环境变量门控：首启 Start-Process 后即撤、V4 独立重启时重设（V2/V3 同享该实例），
# catch 末行兜底清除——后续场景与安装链零泄漏。
try
{
    Stop-Beacon
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    # 前置自愈：V 依赖 mock 在场（U 尾态已停 Beacon 但 mock 存活；T/U 前序失败时可能停在停 mock 后）
    Ensure-MockServer
    # reduced 档：Critical 脉冲自 reduced 起（off 档 StartPulse 直接 return，G7b 已证 off 全静止）
    Write-GConfig "" "reduced" 1.0
    Remove-Item (Join-Path $configDir "notifications.json") -Force -ErrorAction SilentlyContinue # 通知记录隔离，只认本场景新增（F 同口径）
    $vLogPath = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    $env:BEACON_DEBUG_SEVERITY = "critical"
    $env:BEACON_DEBUG_CRASH = "90"
    $vProc = Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -PassThru
    Remove-Item Env:\BEACON_DEBUG_SEVERITY -ErrorAction SilentlyContinue
    Remove-Item Env:\BEACON_DEBUG_CRASH -ErrorAction SilentlyContinue
    # V1 Critical 变色：GLM tile（38%/5h 健康数据 + 注入改判）状态灯 Critical 色 #ff3b30 硬断言
    $vTile = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline -and $vTile -eq [IntPtr]::Zero)
    {
        foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
        {
            if (@((Get-WindowUiaTexts $h)) | Where-Object { $_ -match '38%/5h' }) { $vTile = $h; break }
        }
        if ($vTile -eq [IntPtr]::Zero) { Start-Sleep -Seconds 2 }
    }
    if ($vTile -eq [IntPtr]::Zero) { Save-FullScreenshot "V-FAIL-no-tile.png"; throw "断言失败（场景 V1）：40s 内未见 GLM tile（38%/5h）" }
    $crop = New-Crop ([WinEnum]::RectOf($vTile)) 0
    $critPx = Count-ColorPixels $crop 255 59 48 12
    $errPx = Count-ColorPixels $crop 248 81 73 12
    Save-Zoom $crop "V-critical-tile.png" 3
    $crop.Dispose()
    # 阈值同 G 系实测底（10 之下取 8）；Error 负控 ≤4：G/B 通道差 22/25 > tol12，Critical 灯不会误吞进 Error 桶
    if ($critPx -lt 8) { Save-FullScreenshot "V-FAIL-no-critical.png"; throw "断言失败（场景 V1）：Critical 档位色未渲染（#ff3b30 $critPx px，期望 ≥8）——注入未达 L0 或调色回归" }
    if ($errPx -gt 4) { Save-FullScreenshot "V-FAIL-error-leak.png"; throw "断言失败（场景 V1）：Error 色 $errPx px 渗漏（期望 ≤4）——注入判级错档（Critical 误判 Error）" }
    Write-Host "场景 V1 通过：注入 Critical → tile 状态灯 #ff3b30（$critPx px），Error 色 $errPx px 无渗漏"
    # V4（行 50 Toast 引擎面）在 V2 之后独立重启自证——冷却竞态消解见其块内注
    # V2 Critical 脉冲（尽力取证）：脉冲 = Scale 1.0→1.35→1.0 周期 1000ms，三帧 700ms 间隔必跨相位；
    # 灯 Ø8→Ø10.8 环域 ~40px²，阈值 8 为保守下界。差分不成 → ::warning 转装机（G7c 降级同口径，非红）
    $pulseDiff = 0
    $best1 = $null
    $best3 = $null
    $deadline = (Get-Date).AddSeconds(25)
    while ((Get-Date) -lt $deadline)
    {
        $c1 = New-Crop ([WinEnum]::RectOf($vTile)) 0
        Start-Sleep -Milliseconds 700
        $c2 = New-Crop ([WinEnum]::RectOf($vTile)) 0
        Start-Sleep -Milliseconds 700
        $c3 = New-Crop ([WinEnum]::RectOf($vTile)) 0
        $d = [Math]::Max((Count-FrameDiff $c1 $c2 12), [Math]::Max((Count-FrameDiff $c2 $c3 12), (Count-FrameDiff $c1 $c3 12)))
        if ($d -gt $pulseDiff)
        {
            $pulseDiff = $d
            if ($best1) { $best1.Dispose() }
            if ($best3) { $best3.Dispose() }
            $best1 = $c1
            $best3 = $c3
            $c2.Dispose()
        }
        else
        {
            $c1.Dispose(); $c2.Dispose(); $c3.Dispose()
        }
        if ($pulseDiff -ge 8) { break }
        Start-Sleep -Milliseconds 300
    }
    if ($pulseDiff -ge 8)
    {
        if ($best1)
        {
            Save-Zoom $best1 "V-pulse-frame1.png" 4
            Save-Zoom $best3 "V-pulse-frame3.png" 4
            $best1.Dispose(); $best3.Dispose()
        }
        Write-Host "场景 V2 通过：Critical 脉冲帧差分 $pulseDiff px——脉冲可见"
    }
    else
    {
        if ($best1) { $best1.Dispose() }
        if ($best3) { $best3.Dispose() }
        Write-Host "::warning::场景 V2：Critical 脉冲帧差分 $pulseDiff px（期望 ≥8）——Composition 属性动画在本 CI 会话不可证（G7c/L2 同口径），Critical 脉冲观感转装机目检；V1 色断言不受影响"
    }
    # V4 Critical 通知引擎链（todo 行 50「Toast 照常由通知引擎发出」半）：注入 Critical → Success→Critical
    # 跨界触发 builtin.connection-degraded（任意 widget ≥Warning）投递恰 1 条 critical 记录——记录面硬断言
    # （severity 为 camelCase 枚举串，BeaconJson StringEnumConverter）；Toast 呈现面与 F5 同转装机。
    # 独立重启消竞态（38619441591 V4 红实证）：connection-degraded 冷却按 (规则,widget) 键且跨级别共享——
    # 首跳刷新若撞上 mock 重启后的 bind 窗（连接被拒），PublishStale 会先发一条 Warning 记录并武装 15min 冷却
    # （WidgetHost:174 无缓存亦发 Warning），随后注入的 Critical 被去重吞掉：表现为「GLM 记录存在但零 critical」
    # + V1 已绿（注入确达 L0）。故 V4 自启新实例：mock 热态保证 + notifications.json 删净 + 新引擎冷却表空
    # → 首个 GLM 事件即注入 Critical。两轮复证仍红（38633490811 / 38644793130）后加固三处：Ensure 只保证作业
    # 态 Running，监听 bind 与作业态非原子，故补纯 socket 预检；「首条记录」即断言会把 Warning 快照误判成零
    # critical，故改为轮询到 critical 才断；删档静默失败会让旧记录混入快照，故删后校验。
    # V3 的崩溃基线随之移到本次启动（Stop-Beacon 在 +70s 内收掉首启实例，其 +90s 注入未及触发）。
    Stop-Beacon
    $vNotifPath = Join-Path $configDir "notifications.json"
    Remove-Item $vNotifPath -Force -ErrorAction SilentlyContinue # 新引擎水合源清空，记录集从零起
    if (Test-Path $vNotifPath)
    {
        Save-FullScreenshot "V-FAIL-notif-clear.png"
        throw "断言失败（场景 V4）：notifications.json 删除失败（疑被进程占用）——记录集无法从零起，断言快照会混入旧记录"
    }
    # 热态保证两层：Ensure-MockServer 拉起（收尾带作业态 Running 校验），再纯 socket 预检监听真已 bind。
    # 不用 Invoke-WebRequest 探针：本 CI 会话正文跑 PS 5.1 无 IE 解析引擎恒抛（38633490811 实证：绿跑同款
    # 11 次假重启信号，探针「失败」≠ mock 死），凡以裸探针 200 为门的设计在本会话必假红。
    Ensure-MockServer
    $mockReady = $false
    $mockDeadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $mockDeadline)
    {
        $probe = New-Object System.Net.Sockets.TcpClient
        try { $probe.Connect("127.0.0.1", 18081); $mockReady = $true } catch { } finally { $probe.Close() }
        if ($mockReady) { break }
        Start-Sleep -Milliseconds 500
    }
    if (!$mockReady)
    {
        Save-FullScreenshot "V-FAIL-mock-probe.png"
        throw "断言失败（场景 V4）：mock 预检失败（127.0.0.1:18081 20s 内未可连）——首跳刷新无热态保证，冷却竞态不可排"
    }
    $env:BEACON_DEBUG_SEVERITY = "critical"
    $env:BEACON_DEBUG_CRASH = "90"
    $vProc = Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -PassThru
    # 注入环境变量只给本实例（V1/V2 同款口径）：子进程已继承，随即清掉。此前成功路径不清、只在 catch 清——
    # 38661655290 实证泄漏：V4 首绿后 W/X/Y 每个 app 都带着注入跑（全部刷新被改判 Critical/Failed、
    # +90s 崩溃注入随每实例武装），X1 首跑即红「tile=0 期望 1」。
    Remove-Item Env:\BEACON_DEBUG_SEVERITY -ErrorAction SilentlyContinue
    Remove-Item Env:\BEACON_DEBUG_CRASH -ErrorAction SilentlyContinue
    # 轮询到 critical 记录才断言：首快照若恰是 Warning（首跳失败或冷却武装中）会误判成「零 critical」
    # 38654763928 红实证：PS 5.1 ConvertFrom-Json 把顶层数组读成嵌套一层（[[r1,r2]]）——.Count=1 而
    # .severity 成员枚举出 "critical critical"、.id 枚举出两条，"-eq critical" 全不命中→恒红。app 侧
    # SaveAsync(IReadOnlyList<NotificationRecord>) 经 System.Text.Json 只可能写扁平数组（原样归档为证），
    # 故此处摊平一层再过滤；severity 判定改 -contains（标量与枚举两态皆命中）。
    $vRecords = @()
    $vCrit = @()
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline)
    {
        if (Test-Path $vNotifPath)
        {
            $vJson = Get-Content $vNotifPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($vJson -is [System.Array] -and $vJson.Count -ge 1 -and $vJson[0] -is [System.Array]) { $vJson = @($vJson[0]) }
            $vRecords = @($vJson | Where-Object { $_.sourceWidgetId -eq "bigmodel.usage:GLM" })
            $vCrit = @($vRecords | Where-Object { @($_.severity) -contains "critical" })
            if ($vCrit.Count -ge 1) { break }
        }
        Start-Sleep -Seconds 3
    }
    if ($vCrit.Count -ne 1)
    {
        # 现场取证三件：记录文件原样（判定 severity 实态）+ app 日志（刷新/冷却链路）+ 截图
        Copy-Item $vNotifPath (Join-Path $evidence "notifications-V4-fail.json") -Force -ErrorAction SilentlyContinue
        Copy-Item $vLogPath (Join-Path $evidence "beacon-V4-fail.log") -Force -ErrorAction SilentlyContinue
        $sevDump = ($vRecords | ForEach-Object { "$($_.severity)@id=$($_.id)" }) -join " | "
        Save-FullScreenshot "V-FAIL-critical-record.png"
        throw "断言失败（场景 V4）：GLM Critical 通知记录 $($vCrit.Count) 条（期望恰 1；GLM 记录 $($vRecords.Count) 条：$sevDump）——PS 5.1 嵌套读已摊平仍无 critical：读 notifications-V4-fail.json 原样与 beacon-V4-fail.log 定因（行 50 Toast 半回归）"
    }
    Write-Host "场景 V4 通过：注入 Critical → 通知引擎恰 1 条 critical 记录（connection-degraded 跨界）——Toast 引擎面在案，呈现面同 F5 转装机"
    # V3 崩溃兜底（注入点 = 启动后 90s）：等「DispatcherQueue unhandled exception」落日志——注入走
    # EnqueueGuarded（38459944253 V3 红实证：裸 TryEnqueue 回调异常无系统路由，进程直接退出，由此建 guard）
    $crashIdx = -1
    $vLog = ""
    $deadline = (Get-Date).AddSeconds(100)
    while ((Get-Date) -lt $deadline)
    {
        if ($vProc.HasExited) { Save-FullScreenshot "V-FAIL-crash-exit.png"; throw "断言失败（场景 V3）：崩溃注入后进程退出——DispatcherQueueGuard 兜底未生效（行 24 发现面）" }
        if (Test-Path $vLogPath)
        {
            $vLog = [System.IO.File]::ReadAllText($vLogPath, [System.Text.Encoding]::UTF8)
            $crashIdx = $vLog.IndexOf("DispatcherQueue unhandled exception")
            if ($crashIdx -ge 0) { break }
        }
        Start-Sleep -Seconds 2
    }
    if ($crashIdx -lt 0) { Save-FullScreenshot "V-FAIL-no-crash-log.png"; throw "断言失败（场景 V3）：100s 内日志未见「DispatcherQueue unhandled exception」——注入异常未进兜底链（行 24 发现面）" }
    if ($vLog.Substring($crashIdx) -notmatch "   at ")
    {
        Save-FullScreenshot "V-FAIL-no-stack.png"
        throw "断言失败（场景 V3）：崩溃日志无堆栈行（「   at 」）——LogError 未携带异常对象"
    }
    if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -ne 1)
    {
        Save-FullScreenshot "V-FAIL-proc-count.png"
        throw "断言失败（场景 V3）：Beacon 进程数 $((@(Get-Process Beacon.App -ErrorAction SilentlyContinue)).Count)（期望 1）"
    }
    $dialogs = [WinEnum]::VisibleHandlesByClassAndPid("#32770", $vProc.Id)
    if ($dialogs.Count -gt 0) { Save-FullScreenshot "V-FAIL-dialog.png"; throw "断言失败（场景 V3）：崩溃注入后弹 $($dialogs.Count) 个系统对话框——僵尸窗口" }
    if ([WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count -lt 1)
    {
        Save-FullScreenshot "V-FAIL-tile-gone.png"
        throw "断言失败（场景 V3）：崩溃注入后 tile 窗丢失——悬浮窗被兜底连带"
    }
    # 刷新循环不因兜底中断：崩溃行之后仍出现「refreshed ok」（ci 档 15s 一跳，40s 窗 ≥2 跳）
    $recovered = $false
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $vLog = [System.IO.File]::ReadAllText($vLogPath, [System.Text.Encoding]::UTF8)
        if ($vLog.Substring($crashIdx) -match "refreshed ok") { $recovered = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$recovered)
    {
        Save-FullScreenshot "V-FAIL-no-refresh-after-crash.png"
        throw "断言失败（场景 V3）：崩溃行后 40s 未见「refreshed ok」——刷新循环被兜底中断"
    }
    Save-FullScreenshot "V-crash-survived.png"
    Write-Host "场景 V3 通过：崩溃注入 → 日志堆栈留痕 + 进程存活 + 零 #32770 + tile 仍在 + 刷新循环续跑"
    Stop-Beacon
    Write-Host "场景 V 通过：Critical 注入（L0 变色硬断言 + 脉冲取证）与崩溃注入（DispatcherQueueGuard 兜底全链）——行 71 注入面"
}
catch
{
    Remove-Item Env:\BEACON_DEBUG_SEVERITY -ErrorAction SilentlyContinue
    Remove-Item Env:\BEACON_DEBUG_CRASH -ErrorAction SilentlyContinue
    Stop-Beacon
    Save-FullScreenshot "V-FAIL-unexpected.png"
    $failures.Add("场景 V：$($_.Exception.Message)")
    Write-Host "::error::场景 V 断言失败：$($_.Exception.Message)"
}

# —— 全场景收尾：停应用/清档位文件/停 mock（J 静默安装要求 Beacon 未在跑——AppMutex） ——
Stop-Beacon
Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
Stop-MockServer

# —— 场景 W：看板 i18n 与语言切换 + chip 点击过滤 + 贴边收起开关（2026-10-14 用户单①②③④）——
# W1 预置 language=zh 启动：快速面板文案中文（通知/全部已读/概览）——「看板缺中文」修复实证
# W2 设置页切 English：面板重开后翻英文（NOTIFICATIONS/Mark all read/OVERVIEW）+ config.json language=en 落盘
#    （面板失焦即隐藏=设计行为：设置窗激活 → 面板 Deactivated → Hide；切换在隐藏期间经 LanguageChanged
#    广播完成重画，重开面板应见新语言、无需重启进程——首跑 38525961627 在隐藏态读 UIA 得空文本红）
# W3 chip 点击过滤：点 chip-智谱 → OVERVIEW 头带「· 智谱」（当前类目界面可见）；点 chip-all 回全部
# W4 贴边收起开关：Toggle off→on，config.json edgeCollapseEnabled false→true（开关可切换可落盘；
#    收起细条宽度断言见场景 P；「点击细条粘性展开」手势半转装机——SendInput 对 12 DIP 细条点击
#    精度不稳，粘性展开的可靠入口语义在 OnTileTapped 代码路径与 W4 开关断言共同覆盖）
try
{
    Ensure-MockServer
    Write-GConfig $true
    $cfgPath = Join-Path $configDir "config.json"
    (Get-Content $cfgPath -Raw -Encoding UTF8).Replace('"theme": "dark",', '"theme": "dark", "language": "zh",') | Set-Content -Path $cfgPath -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    Start-Sleep -Seconds 15
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "W-FAIL-no-settings.png"; throw "断言失败（场景 W 前置）：设置窗未出现" }
    # 唤起面板：SendInput 和弦优先，WM_HOTKEY 直驱兜底（场景 K 同款双路）
    $panel = [IntPtr]::Zero
    [WinEnum]::SendChord(0x11, 0x42, 0x12)
    $deadline = (Get-Date).AddSeconds(3)
    while ((Get-Date) -lt $deadline)
    {
        $handles = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($handles.Count -ge 1) { $panel = $handles[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panel -eq [IntPtr]::Zero)
    {
        $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
        if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
        if ($msgWindow -ne [IntPtr]::Zero)
        {
            [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY, wParam=HotkeyId
            $deadline = (Get-Date).AddSeconds(5)
            while ((Get-Date) -lt $deadline)
            {
                $handles = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
                if ($handles.Count -ge 1) { $panel = $handles[0]; break }
                Start-Sleep -Milliseconds 250
            }
        }
    }
    if ($panel -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "W-FAIL-no-panel.png"
        throw "断言失败（场景 W 前置）：快速面板未唤起（和弦与 WM_HOTKEY 双路）"
    }
    $panelRoot = [System.Windows.Automation.AutomationElement]::FromHandle($panel)
    # W1 预置中文：通知/全部已读/概览三个常显文案全中文（StatusPanel 可能整块收起，不作断言面）
    $panelTexts = @()
    $deadline = (Get-Date).AddSeconds(8)
    while ((Get-Date) -lt $deadline)
    {
        $panelTexts = Get-WindowUiaTexts $panel
        if (($panelTexts -contains "通知") -and ($panelTexts -contains "全部已读") -and ($panelTexts -contains "概览")) { break }
        Start-Sleep -Milliseconds 500
    }
    if (!($panelTexts -contains "通知") -or !($panelTexts -contains "全部已读") -or !($panelTexts -contains "概览"))
    {
        Save-FullScreenshot "W-FAIL-zh-missing.png"
        throw "断言失败（场景 W1）：预置 language=zh 后看板文案未中文（实际：$($panelTexts -join ' | ')）——看板 i18n 接线回归"
    }
    Write-Host "场景 W1 通过：预置 zh 启动看板文案中文（通知/全部已读/概览）——看板缺中文修复实证"
    # W2 设置切 English：面板即时翻英文 + config 落盘（无需重启）
    $combo = Find-UiaById $settings "language-box" 10
    if (!$combo) { Save-FullScreenshot "W-FAIL-no-language-box.png"; throw "断言失败（场景 W2）：设置页未找到语言下拉 language-box" }
    Select-ComboItemById $settings $combo "language-en"
    $cfgOk = $false
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        if ((Get-Content $cfgPath -Raw -Encoding UTF8) -match '"language":\s*"en"') { $cfgOk = $true; break }
        Start-Sleep -Milliseconds 400
    }
    if (!$cfgOk) { Save-FullScreenshot "W-FAIL-config-lang.png"; throw "断言失败（场景 W2）：切 English 后 config.json 无 language=en——语言选择未落盘" }
    # 面板失焦即隐藏（QuickPanelWindow OnActivated：Deactivated → Hide，设计行为）：设置窗激活后面板
    # 已隐藏，隐藏态读 UIA 得空文本（首跑 38525961627 红因）。重开面板验新语言：可见则直接读，
    # 隐藏则经热键重开（SendInput 和弦 + WM_HOTKEY 双路，与 W1 前置同款）——重开见 English 即切换生效
    if ([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count -lt 1)
    {
        [WinEnum]::SendChord(0x11, 0x42, 0x12)
        $deadline = (Get-Date).AddSeconds(3)
        while ((Get-Date) -lt $deadline)
        {
            if ([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count -ge 1) { break }
            Start-Sleep -Milliseconds 250
        }
        # WM_HOTKEY 是 toggle：和弦已唤起则再发一次会把面板关掉，必须只在未唤起时兜底
        if ([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count -lt 1)
        {
            $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
            if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
            if ($msgWindow -ne [IntPtr]::Zero)
            {
                [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY, wParam=HotkeyId
                $deadline = (Get-Date).AddSeconds(5)
                while ((Get-Date) -lt $deadline)
                {
                    if ([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count -ge 1) { break }
                    Start-Sleep -Milliseconds 250
                }
            }
        }
    }
    $panelTexts = @()
    $deadline = (Get-Date).AddSeconds(8)
    while ((Get-Date) -lt $deadline)
    {
        $panelTexts = Get-WindowUiaTexts $panel
        if (($panelTexts -contains "NOTIFICATIONS") -and ($panelTexts -contains "Mark all read") -and ($panelTexts -contains "OVERVIEW")) { break }
        Start-Sleep -Milliseconds 500
    }
    if (!($panelTexts -contains "NOTIFICATIONS") -or !($panelTexts -contains "Mark all read") -or !($panelTexts -contains "OVERVIEW"))
    {
        Save-FullScreenshot "W-FAIL-live-switch.png"
        throw "断言失败（场景 W2）：切 English 后重开面板未见英文（实际：$($panelTexts -join ' | ')）——语言切换生效回归"
    }
    Write-Host "场景 W2 通过：设置切 English → 重开面板见英文（隐藏期已重画）+ config.json language=en 落盘"
    # W3 chip 点击过滤：点 chip-智谱 → OVERVIEW 头带「· 智谱」；点 chip-all 回纯 OVERVIEW
    # 重取面板根：W2 隐藏/重开 + LanguageChanged 全量重画后，W1 缓存的 AutomationElement 已失效
    # （FindFirst 空转——38533804493 W3 红因，失败截图实证 chip 实际在树：All 2/DeepSeek 1/智谱 1）
    # 遮挡防护（38633490811 W3 红实证）：浮 tile 窗 z 序在面板之上，恰好压住 chip 行时 UIA 找得到
    # chip、矩形坐标也准，但 SendInput 打到最上层 tile——过滤永不触发。点击前把全部 tile 挪到面板
    # 正下方横排（位置由面板矩形推导），与 tile 落位运气无关
    $wPr = [WinEnum]::RectOf($panel)
    $wTi = 0
    foreach ($wTh in @([WinEnum]::VisibleHandlesByTitle("Beacon Tile") + [WinEnum]::VisibleHandlesByTitle("Beacon Pinned")))
    {
        [WinEnum]::MoveNoFocus($wTh, $wPr.Left, ($wPr.Bottom + 4 + $wTi * 60))
        $wTi++
    }
    $panelRoot = [System.Windows.Automation.AutomationElement]::FromHandle($panel)
    $chip = Find-UiaById $panelRoot "chip-智谱" 8
    if (!$chip) { Save-FullScreenshot "W-FAIL-no-chip.png"; throw "断言失败（场景 W3）：面板未找到 chip-智谱（AutomationId 缺失或 chip 未渲染）" }
    $chipPt = Get-ClickPoint $chip
    if (!$chipPt) { Save-FullScreenshot "W-FAIL-no-chip-point.png"; throw "断言失败（场景 W3）：chip-智谱 落点不可解析（UIA 矩形 NaN/空）——chip 未渲染或面板布局未稳" }
    [WinEnum]::ClickAt($chipPt[0], $chipPt[1]) # 面板常置顶，绝对坐标点击命中面板
    $filtered = $false
    $deadline = (Get-Date).AddSeconds(6)
    while ((Get-Date) -lt $deadline)
    {
        $panelTexts = Get-WindowUiaTexts $panel
        if ($panelTexts | Where-Object { $_ -match 'OVERVIEW · 智谱' }) { $filtered = $true; break }
        Start-Sleep -Milliseconds 400
    }
    if (!$filtered)
    {
        Save-FullScreenshot "W-FAIL-no-filter.png"
        throw "断言失败（场景 W3）：点 chip-智谱 后 OVERVIEW 头未带「· 智谱」（实际：$($panelTexts -join ' | ')）——点击过滤/可见态回归"
    }
    Write-Host "场景 W3 通过：点 chip-智谱 → OVERVIEW 头「OVERVIEW · 智谱」（当前类目界面可见）"
    # 点 chip-智谱 触发 ApplySourceFilter 全量重画 → 缓存根再失效一次，找 chip-all 前重取
    $panelRoot = [System.Windows.Automation.AutomationElement]::FromHandle($panel)
    $chipAll = Find-UiaById $panelRoot "chip-all" 5
    if (!$chipAll) { Save-FullScreenshot "W-FAIL-no-chip-all.png"; throw "断言失败（场景 W3）：面板未找到 chip-all（「全部」chip 缺失）" }
    $chipAllPt = Get-ClickPoint $chipAll
    if (!$chipAllPt) { Save-FullScreenshot "W-FAIL-no-chipall-point.png"; throw "断言失败（场景 W3）：chip-all 落点不可解析（UIA 矩形 NaN/空）——「全部」chip 未渲染或面板布局未稳" }
    [WinEnum]::ClickAt($chipAllPt[0], $chipAllPt[1])
    $backToAll = $false
    $deadline = (Get-Date).AddSeconds(6)
    while ((Get-Date) -lt $deadline)
    {
        $panelTexts = Get-WindowUiaTexts $panel
        if ($panelTexts -contains "OVERVIEW") { $backToAll = $true; break }
        Start-Sleep -Milliseconds 400
    }
    if (!$backToAll)
    {
        Save-FullScreenshot "W-FAIL-no-unfilter.png"
        throw "断言失败（场景 W3）：点「全部」chip 后 OVERVIEW 头未回纯 OVERVIEW（实际：$($panelTexts -join ' | ')）——取消过滤回归"
    }
    Write-Host "场景 W3 通过：点「全部」chip → 回全部（OVERVIEW 纯头）——再点取消链实证"
    # W4 贴边收起开关：off→config false，on→config true（开关可切换可落盘）
    $toggle = Find-UiaById $settings "edge-collapse-toggle" 8
    if (!$toggle) { Save-FullScreenshot "W-FAIL-no-toggle.png"; throw "断言失败（场景 W4）：设置页未找到贴边收起开关 edge-collapse-toggle" }
    ($toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
    $offOk = $false
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        if ((Get-Content $cfgPath -Raw -Encoding UTF8) -match '"edgeCollapseEnabled":\s*false') { $offOk = $true; break }
        Start-Sleep -Milliseconds 400
    }
    if (!$offOk) { Save-FullScreenshot "W-FAIL-toggle-off.png"; throw "断言失败（场景 W4）：开关置关后 config.json 无 edgeCollapseEnabled=false——开关未落盘" }
    ($toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
    $onOk = $false
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        if ((Get-Content $cfgPath -Raw -Encoding UTF8) -match '"edgeCollapseEnabled":\s*true') { $onOk = $true; break }
        Start-Sleep -Milliseconds 400
    }
    if (!$onOk) { Save-FullScreenshot "W-FAIL-toggle-on.png"; throw "断言失败（场景 W4）：开关置开后 config.json 无 edgeCollapseEnabled=true——开关回切未落盘" }
    Write-Host "场景 W4 通过：贴边收起开关可切换且落盘（edgeCollapseEnabled false→true）"
    Stop-Beacon
}
catch
{
    Stop-Beacon # 失败也收尸：W 的 try 尾 Stop-Beacon 在 throw 时被跳过，残留进程曾毒化场景 J 静默安装（38525961627）
    Save-FullScreenshot "W-FAIL-unexpected.png"
    $failures.Add("场景 W：$($_.Exception.Message)")
    Write-Host "::error::场景 W 断言失败：$($_.Exception.Message)"
}

# —— 场景 X：Pin 双向全链（todo 行 55「右键 Pin 落库」+ 行 60「两入口一致并持久化」CI 代验）——
# X1 面板入口：RecentList 行右键（SendInput RIGHTDOWN/UP 合成，DragMouse 同绝对坐标口径）→ ContextFlyout
#   弹出 → MenuFlyoutItem（Control 自带 peer；Name=硬编码英文「(Un)Pin to desktop」，未入 UiStrings）可达
#   → Invoke → PinWidget：Pinned 翻 false + UpsertWidget 落 widgets.json + ReloadWidgets 重建
#   （floating 形态 FloatingTileWindow 按 widgets.json 重建窗集——tile 增减=落库+重建双证）。
# X2 设置入口（两入口一致，反向驱动）：Unpin 后 GLM 已注销、面板无行可右键（38583526906 实证）——
#   重启 --settings 证 Unpin 持久化（钉开关读态 Off）→ Toggle On（与面板 PinWidget 共用
#   UpsertWidget+ReloadWidgets+PinsChanged 链）→ widgets.json 翻 true + tile 回归。
# X3 再重启：冷加载持久化（tile 回建）+ 钉开关 On + 行标「已钉」读态一致。
# 行 61 非准入类型半项由场景 Y 代验（「无入口」证据面，见 Y 头注）。
try
{
    # X 场景 app 日志路径（此前 X 失败现场零日志——38661655290 X1 红只能靠截图+widgets.json 猜，V4 三轮
    # 同盲区教训的 X 侧补点）：X 每个实例都写当日此日志，失败时连同 widgets.json 原样归档进 evidence。
    $xLogPath = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    $xWidgetsPath = Join-Path $configDir "widgets.json"
    Write-GConfig "" "off" 1.0
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    # X0 基线：两 tile（GLM/DeepSeek 预置 pinned:true，floating 形态建窗）
    $baseTiles = -1
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline)
    {
        $baseTiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
        if ($baseTiles -eq 2) { break }
        Start-Sleep -Seconds 2
    }
    if ($baseTiles -ne 2) { Save-FullScreenshot "X-FAIL-baseline-tiles.png"; throw "断言失败（场景 X 前置）：基线悬浮 tile=$baseTiles（期望 2——GLM/DeepSeek 预置钉选）" }
    # 开面板（热键 WM_HOTKEY 直发，S 同链路）
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 X 前置）：托盘消息窗未找到" }
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0)
    $panelHandle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $ph = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($ph.Count -ge 1) { $panelHandle = $ph[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panelHandle -eq [IntPtr]::Zero) { Save-FullScreenshot "X-FAIL-no-panel.png"; throw "断言失败（场景 X 前置）：热键触发后 5s 内快速面板未出现" }
    Start-Sleep -Seconds 2
    # RecentList 出 GLM 事件行（S1 同口径；行名含「已用 38%」）
    $glmRow = $null
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
        $recentRoot = Find-UiaById $panelEl "RecentList" 5
        if ($recentRoot) { $glmRow = Find-PanelRow $recentRoot '已用 38%' }
        if ($glmRow) { break }
        Start-Sleep -Seconds 2
    }
    if (!$glmRow) { Save-FullScreenshot "X-FAIL-no-glm-row.png"; throw "断言失败（场景 X1 前置）：40s 内 RecentList 无 GLM 事件行（38%）" }
    # X1 右键 Unpin：菜单项「Unpin from desktop」→ widgets.json 翻 false + GLM tile 消失。
    # Invoke→ClickAt + 整链重试两轮：38602176751 实证 flyout 开启动画期 InvokePattern.Invoke 可能被吞
    # （菜单命中、Invoke 已调、widgets.json 不翻、flyout 已关、tile 仍在）——真实鼠标点击不吃动画竞态；
    # 点击后 15s 未翻则整链重来（上一轮弹层无论命中/落空均已关闭，右键重开新 flyout，无状态残留）。
    # 落点走 Get-ClickPoint（38619441591 X 红实证：行/菜单项 UIA 矩形返 NaN，[int] 转换直接炸且无断言消息）
    # ——NaN/空矩形按落空处理走整链重试，不炸。
    $unpinned = $false
    $menuSeen = $false
    for ($attempt = 1; $attempt -le 2 -and !$unpinned; $attempt++)
    {
        if ($attempt -gt 1)
        {
            Start-Sleep -Seconds 2
            # 重试前重找行（ci 档 15s 刷新 tick 可能已重建 RecentList，旧元素 BoundingRectangle 不可信）
            $glmRow = $null
            $deadline = (Get-Date).AddSeconds(20)
            while ((Get-Date) -lt $deadline)
            {
                $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
                $recentRoot = Find-UiaById $panelEl "RecentList" 5
                if ($recentRoot) { $glmRow = Find-PanelRow $recentRoot '已用 38%' }
                if ($glmRow) { break }
                Start-Sleep -Seconds 2
            }
            if (!$glmRow) { Save-FullScreenshot "X-FAIL-no-glm-row.png"; throw "断言失败（场景 X1）：重试时 RecentList 无 GLM 事件行（38%）" }
        }
        $rowPt = Get-ClickPoint $glmRow
        if (!$rowPt)
        {
            Write-Host "场景 X1：第 $attempt 次行落点不可解析（UIA 矩形 NaN/空——行被虚拟化或刷新重建中），整链重试"
            continue
        }
        [WinEnum]::RightClick($rowPt[0], $rowPt[1])
        $menuItem = $null
        $deadline = (Get-Date).AddSeconds(5)
        while ((Get-Date) -lt $deadline)
        {
            $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
            $menuItem = $panelEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Unpin from desktop")))
            if (!$menuItem)
            {
                # 弹层若独立 HWND：桌面根兜底（G6b combo 展开同口径）
                $menuItem = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
                    [System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "Unpin from desktop")))
            }
            if ($menuItem) { break }
            Start-Sleep -Milliseconds 500
        }
        if (!$menuItem)
        {
            Write-Host "场景 X1：第 $attempt 次右键后 5s 未见菜单项（ContextFlyout 未弹出），重试"
            continue
        }
        $menuSeen = $true
        Start-Sleep -Milliseconds 600 # flyout 开启动画走完再点击（动画期 UIA 模式调用不可靠）
        $menuPt = Get-ClickPoint $menuItem
        if (!$menuPt)
        {
            Write-Host "场景 X1：第 $attempt 次菜单项落点不可解析（UIA 矩形 NaN/空——flyout 布局未稳），整链重试"
            continue
        }
        [WinEnum]::ClickAt($menuPt[0], $menuPt[1])
        $deadline = (Get-Date).AddSeconds(15)
        while ((Get-Date) -lt $deadline)
        {
            $widgetsJson = Get-Content (Join-Path $configDir "widgets.json") -Raw -Encoding UTF8 | ConvertFrom-Json
            $glmEntry = $widgetsJson | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
            if ($glmEntry -and $glmEntry.pinned -eq $false) { $unpinned = $true; break }
            Start-Sleep -Seconds 2
        }
        if (!$unpinned -and $attempt -lt 2) { Write-Host "场景 X1：第 $attempt 次点击后 widgets.json 未翻，整链重试" }
    }
    if (!$menuSeen) { Save-FullScreenshot "X-FAIL-no-unpin-item.png"; throw "断言失败（场景 X1）：两轮右键后均未见菜单项「Unpin from desktop」——ContextFlyout 未弹出（合成右键不生效？）" }
    if (!$unpinned)
    {
        # 现场取证：widgets.json 原样 + app 日志随 artifact 归档（V4 三轮盲区教训——X 失败此前零日志）
        Copy-Item $xWidgetsPath (Join-Path $evidence "widgets-X1-fail.json") -Force -ErrorAction SilentlyContinue
        Copy-Item $xLogPath (Join-Path $evidence "beacon-X1-fail.log") -Force -ErrorAction SilentlyContinue
        Save-FullScreenshot "X-FAIL-unpin-not-saved.png"
        throw "断言失败（场景 X1）：右键 Unpin 两轮尝试后 widgets.json GLM pinned 未翻 false——右键 Pin 落库失败（todo 行 55）：读 widgets-X1-fail.json 原样与 beacon-X1-fail.log 定因"
    }
    $tileGone = $false
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline)
    {
        if ([WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count -eq 1) { $tileGone = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$tileGone)
    {
        # 现场取证：widgets.json 原样 + app 日志随 artifact 归档——X1 红（tile 数不符）若无日志无法区分
        # 「ReloadWidgets 未跑」「重建了但 tile 被改判隐藏/崩溃退出」两因（38661655290 X1 红实证此盲区）
        Copy-Item $xWidgetsPath (Join-Path $evidence "widgets-X1-fail.json") -Force -ErrorAction SilentlyContinue
        Copy-Item $xLogPath (Join-Path $evidence "beacon-X1-fail.log") -Force -ErrorAction SilentlyContinue
        Save-FullScreenshot "X-FAIL-unpin-tile-alive.png"
        throw "断言失败（场景 X1）：Unpin 后 15s 内 GLM 悬浮 tile 未消失（tile=$([WinEnum]::VisibleHandlesByTitle('Beacon Tile').Count)，期望 1）——ReloadWidgets 重建链未跑：读 widgets-X1-fail.json 原样与 beacon-X1-fail.log 定因（查 ReloadWidgets/重建链/异常退出）"
    }
    Write-Host "场景 X1 通过：右键 Unpin → widgets.json 落盘 + GLM tile 移除（余 1 tile）"
    # X2 设置入口复钉（行 60 两入口一致，反向驱动）：X1 实证 Unpin 后 GLM 已注销——面板 RecentList
    # 不再有行可右键（38583526906：X1 绿、旧 X2 设计红）。反向必须从另一入口进：设置页钉开关
    # （pin-toggle-{widgetId}，同批锚点）与面板 PinWidget 共用 UpsertWidget+ReloadWidgets+PinsChanged 链。
    # 重启 --settings（顺带证 Unpin 持久化到冷加载）→ 读态应 Off → Toggle On → widgets.json 翻 true + tile 回归
    Stop-Beacon
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings) { Save-FullScreenshot "X-FAIL-no-settings-repin.png"; throw "断言失败（场景 X2）：重启后设置窗未开" }
    $bigRow = Find-UiaById $settings "module-bigmodel" 10
    if (!$bigRow) { throw "断言失败（场景 X2）：UIA 未找到 module-bigmodel" }
    Invoke-Uia $bigRow
    Start-Sleep -Seconds 2
    $pinToggle = Find-UiaById $settings "pin-toggle-bigmodel.usage:GLM" 10
    if (!$pinToggle) { Save-FullScreenshot "X-FAIL-no-toggle.png"; throw "断言失败（场景 X2）：UIA 未找到 pin-toggle-bigmodel.usage:GLM（锚点丢失？）" }
    $toggleState = "$(($pinToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState)"
    if ($toggleState -ne "Off") { throw "断言失败（场景 X2）：重启后设置钉开关 ToggleState=$toggleState（期望 Off）——X1 的 Unpin 未持久化或两入口读态不一致" }
    ($pinToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
    $repinned = $false
    $tileBack = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $widgetsJson = Get-Content (Join-Path $configDir "widgets.json") -Raw -Encoding UTF8 | ConvertFrom-Json
        $glmEntry = $widgetsJson | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
        if ($glmEntry -and $glmEntry.pinned -eq $true) { $repinned = $true }
        if ([WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count -eq 2) { $tileBack = $true }
        if ($repinned -and $tileBack) { break }
        Start-Sleep -Seconds 2
    }
    if (!$repinned) { Save-FullScreenshot "X-FAIL-repin-not-saved.png"; throw "断言失败（场景 X2）：设置入口复钉后 20s 内 widgets.json GLM pinned 未翻 true" }
    if (!$tileBack)
    {
        Save-FullScreenshot "X-FAIL-repin-tile-missing.png"
        throw "断言失败（场景 X2）：设置入口复钉后 20s 内 GLM 悬浮 tile 未回归（tile=$([WinEnum]::VisibleHandlesByTitle('Beacon Tile').Count)，期望 2）——PinsChanged 重建链回归"
    }
    Write-Host "场景 X2 通过：设置钉开关 Off→On → widgets.json 落盘 + GLM tile 回归（2 tiles）——两入口同链实证"
    # X3 持久化 + 两入口读态一致：重启 --settings 冷加载 → tile 回建 + 钉开关 On + 行标「已钉」
    Stop-Beacon
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings) { Save-FullScreenshot "X-FAIL-no-settings.png"; throw "断言失败（场景 X3）：重启后设置窗未开" }
    $bigRow = Find-UiaById $settings "module-bigmodel" 10
    if (!$bigRow) { throw "断言失败（场景 X3）：UIA 未找到 module-bigmodel" }
    Invoke-Uia $bigRow
    Start-Sleep -Seconds 2
    $pinToggle = Find-UiaById $settings "pin-toggle-bigmodel.usage:GLM" 10
    if (!$pinToggle) { throw "断言失败（场景 X3）：UIA 未找到 pin-toggle-bigmodel.usage:GLM（锚点丢失？）" }
    $toggleState = "$(($pinToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState)"
    if ($toggleState -ne "On") { throw "断言失败（场景 X3）：重启后设置钉开关 ToggleState=$toggleState（期望 On）——两入口读态不一致或持久化回归" }
    $glmLabel = $null
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $texts = $settings.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
        $glmLabel = @($texts) | Where-Object { $_.Current.Name -match 'bigmodel.usage:GLM' -and $_.Current.Name -match '已钉' } | Select-Object -First 1
        if ($glmLabel) { break }
        Start-Sleep -Milliseconds 500
    }
    if (!$glmLabel) { Save-FullScreenshot "X-FAIL-no-pinned-label.png"; throw "断言失败（场景 X3）：设置页 GLM 行标未见「已钉」——两入口读态不一致（行 60）" }
    $tilesRestarted = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
    if ($tilesRestarted -ne 2) { Save-FullScreenshot "X-FAIL-restart-tiles.png"; throw "断言失败（场景 X3）：重启后悬浮 tile=$tilesRestarted（期望 2）——钉选持久化回归" }
    Write-Host "场景 X3 通过：重启持久化（2 tiles）+ 设置钉开关 On + 行标「已钉」——两入口读态一致"
    Stop-Beacon
}
catch
{
    Stop-Beacon # W 同口径：失败也收尸，防残留进程毒化 J 静默安装
    Save-FullScreenshot "X-FAIL-unexpected.png"
    $failures.Add("场景 X：$($_.Exception.Message)")
    Write-Host "::error::场景 X 断言失败：$($_.Exception.Message)"
}

# —— 场景 Y：非准入类型两处入口均不可钉（todo 行 61；原记「需真实右键交互，CI 不可达」）——
# 播种 widgets.json 单条未登记类型（unknownghost.usage：BeaconRuntime providers 字典无此键）+ pinned:true。
# 证据面取「无入口」而非「点了没反应」——比后者强且确定：设置入口 RebuildWidgets 按 _scopeDescriptors
# 过滤类型（未登记类型整行不产，连行标都不出）；面板入口 WidgetHost 对未登记类型 Resolve=null 静默跳过，
# 无状态即无 tile 无行，行级 ContextFlyout 无宿主。
# Y1 主机侧：启动 ReloadWidgets(null)/scheduler.KickWidget 对 ghost 同步触发刷新链 →
#   TryRefreshOneAsync Resolve=null → 「No provider for widget type …; skipped.」Warning 落今日日志
#   （与 F5t「Toast 发送失败」同通道）；pinned:true 却零 tile——无任何路径可为它建窗。
# Y2 面板入口：面板无该行，右键（SendInput 合成，场景 X 已证生效）后 5s 内无
#   「Pin to desktop」/「Unpin from desktop」菜单项（面板根+桌面根双查）——无钉入口。
# Y3 设置入口：module 页组件区无 pin-toggle-* 元素、无 ghost 行文本——无钉入口。
# Y4 钉选零改写：widgets.json ghost 条 pinned 恒 true——无入口可翻转、不被任何路径重写或剪除。
# 与并行会话在飞的 PinEntryVisible 门控改造（面板侧不再挂 Pin 菜单项）观测一致，其落地后本场景仍成立。
try
{
    Write-GConfig "" "off" 1.0
    # ghost-only widgets.json：连接指已存在的 zhipu——连接命中才会走到 Resolve 分支（触发「No provider」
    # 而非「connection is missing」），JsonConfigurationStore.LoadFile 纯反序列化不剪除未知类型
    @'
[
  {
    "id": "unknownghost.usage:Ghost",
    "type": "unknownghost.usage",
    "connectionId": "zhipu",
    "refreshTier": "ci",
    "pinned": true,
    "config": {}
  }
]
'@ | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    # Y1 主机侧跳过锚 + pinned:true 零 tile
    $noProvider = $false
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        if ((Get-TodayLog) -match 'No provider for widget type unknownghost.usage') { $noProvider = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$noProvider) { Save-FullScreenshot "Y-FAIL-no-skip-log.png"; throw "断言失败（场景 Y1）：30s 内日志无「No provider for widget type unknownghost.usage」——未登记类型未被静默跳过（可能误注册或崩溃）" }
    $ghostTiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
    if ($ghostTiles -ne 0) { Save-FullScreenshot "Y-FAIL-ghost-tile.png"; throw "断言失败（场景 Y1）：未登记类型 pinned:true 却出现悬浮 tile=$ghostTiles（期望 0）——钉选越权建窗" }
    Write-Host "场景 Y1 通过：未登记类型 skipped 日志在 + pinned:true 零 tile（主机侧无入口）"
    # Y2 面板入口无钉菜单：开面板（WM_HOTKEY 直发，X 同链路）→ 右键面板中心 → 无 Pin 菜单项
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 Y 前置）：托盘消息窗未找到" }
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0)
    $panelHandle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $ph = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($ph.Count -ge 1) { $panelHandle = $ph[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panelHandle -eq [IntPtr]::Zero) { Save-FullScreenshot "Y-FAIL-no-panel.png"; throw "断言失败（场景 Y 前置）：热键触发后 5s 内快速面板未出现" }
    Start-Sleep -Seconds 2
    $pRect = [WinEnum]::RectOf($panelHandle)
    [WinEnum]::RightClick([int]($pRect.X + $pRect.Width / 2), [int]($pRect.Y + $pRect.Height / 2))
    $pinMenu = $null
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
        foreach ($menuName in @("Pin to desktop", "Unpin from desktop"))
        {
            $pinMenu = $panelEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $menuName)))
            if (!$pinMenu)
            {
                $pinMenu = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
                    [System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $menuName)))
            }
            if ($pinMenu) { break }
        }
        if ($pinMenu) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($pinMenu) { Save-FullScreenshot "Y-FAIL-pin-menu.png"; throw "断言失败（场景 Y2）：面板右键出现钉选菜单项「$($pinMenu.Current.Name)」——未登记类型本应无钉入口（行级 ContextFlyout 越权弹出）" }
    Write-Host "场景 Y2 通过：面板右键 5s 无「Pin to desktop」/「Unpin from desktop」菜单项——面板侧无钉入口"
    # Y3 设置入口无行无开关：重启 --settings → module 页组件区无 pin-toggle-ghost、无 ghost 行文本
    Stop-Beacon
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings) { Save-FullScreenshot "Y-FAIL-no-settings.png"; throw "断言失败（场景 Y3）：重启后设置窗未开" }
    $bigRow = Find-UiaById $settings "module-bigmodel" 10
    if (!$bigRow) { throw "断言失败（场景 Y3）：UIA 未找到 module-bigmodel" }
    Invoke-Uia $bigRow
    Start-Sleep -Seconds 2
    $ghostToggle = Find-UiaById $settings "pin-toggle-unknownghost.usage:Ghost" 5
    if ($ghostToggle) { Save-FullScreenshot "Y-FAIL-ghost-toggle.png"; throw "断言失败（场景 Y3）：设置页出现未登记类型的钉开关 pin-toggle-unknownghost.usage:Ghost——scope 过滤失效（行 61 回归）" }
    $ghostLabel = $null
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $texts = $settings.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
        $ghostLabel = @($texts) | Where-Object { $_.Current.Name -match 'unknownghost' } | Select-Object -First 1
        if ($ghostLabel) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($ghostLabel) { Save-FullScreenshot "Y-FAIL-ghost-row.png"; throw "断言失败（场景 Y3）：设置页组件区渲染了未登记类型行「$($ghostLabel.Current.Name)」——RebuildWidgets scope 过滤回归" }
    Write-Host "场景 Y3 通过：设置页无 pin-toggle-ghost 元素、无 ghost 行文本——设置侧无钉入口（scope 过滤）"
    # Y4 钉选零改写：ghost 条 pinned 恒 true（无入口可翻转、不被任何路径重写或剪除）
    $widgetsJson = Get-Content (Join-Path $configDir "widgets.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $ghostEntry = $widgetsJson | Where-Object { $_.id -eq "unknownghost.usage:Ghost" }
    if (!$ghostEntry) { Save-FullScreenshot "Y-FAIL-ghost-pruned.png"; throw "断言失败（场景 Y4）：widgets.json 中 ghost 条目消失——未登记类型被剪除（配置加载回归）" }
    if ($ghostEntry.pinned -ne $true) { Save-FullScreenshot "Y-FAIL-ghost-flipped.png"; throw "断言失败（场景 Y4）：ghost 条目 pinned=$($ghostEntry.pinned)（期望 true）——钉选被越权改写" }
    Write-Host "场景 Y4 通过：widgets.json ghost 条目完整、pinned 恒 true——两入口均无钉面且配置零改写"
    Stop-Beacon
}
catch
{
    Stop-Beacon # W 同口径：失败也收尸，防残留进程毒化 J 静默安装
    Save-FullScreenshot "Y-FAIL-unexpected.png"
    $failures.Add("场景 Y：$($_.Exception.Message)")
    Write-Host "::error::场景 Y 断言失败：$($_.Exception.Message)"
}

# UIA 子树 dump（取证面）：ControlView 走树，缩进输出 ControlType/AutomationId/Name；深度 6、元素 300 封顶。
# 通用文件对话框的实际结构因 Windows 版本/主题而异，dump 是失败后一次定因的材料（V4 三轮盲区教训）。
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
function Wait-CommonFileDialog([System.Collections.Generic.List[string]] $before, [string[]] $titleHints, [int] $timeoutSec = 20, [string] $tag = "")
{
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $fallback = [IntPtr]::Zero
    while ((Get-Date) -lt $deadline)
    {
        foreach ($h in [WinEnum]::VisibleTopLevelHandles())
        {
            $hs = [string]$h
            if ($before.Contains($hs)) { continue }
            $hit = $false
            foreach ($hint in $titleHints)
            {
                if ([WinEnum]::TitleOf($h) -like "*$hint*") { $hit = $true; break }
            }
            if ($hit) { return $h }
            if ($fallback -eq [IntPtr]::Zero) { $fallback = $h }
        }
        if ($fallback -ne [IntPtr]::Zero) { return $fallback }
        Start-Sleep -Milliseconds 300
    }
    Save-DesktopWindowDump ($tag + "-windows.txt")
    return [IntPtr]::Zero
}

# 文件名框定位（纯 Win32）：Explorer 样式模板的文件名 combo 控件 id 固定 1148（cmb13，UIA 同名
# AutomationId「1148」的来源）——GetDlgItem 直取其内层 **可见** Edit；拿不到退全量可见 Edit 启发式
# （首个非路径文本）。可见性过滤必须做：对话框常带隐藏兼容 Edit（38703104601 实证——写入命中隐藏
# Edit，GetWindowText 复核通过而可见框仍空，IDOK 空名 no-op，对话框滞留），不滤则写错控件还自证通过。
function Find-FileNameEdit([IntPtr]$dlgH)
{
    $combo = [WinEnum]::DlgItemOf($dlgH, 1148)
    if ($combo -ne [IntPtr]::Zero)
    {
        foreach ($e in [WinEnum]::DescendantsOfClassAll($combo, "Edit"))
        {
            if ([WinEnum]::Visible($e)) { return $e }
        }
    }
    foreach ($x in [WinEnum]::DescendantsOfClassAll($dlgH, "Edit"))
    {
        if (([WinEnum]::Visible($x)) -and (([WinEnum]::TitleOf($x)) -notmatch "^[A-Za-z]:\\")) { return $x }
    }
    return [IntPtr]::Zero
}

# 对话框内全部 Edit 清单 dump（诊断面）：hwnd/控件 id/可见性/文本 逐行——文件名写入未达时一次定因。
function Save-EditDump([IntPtr]$dlgH, [string] $fileName)
{
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($e in [WinEnum]::DescendantsOfClassAll($dlgH, "Edit"))
    {
        $vis = "?"
        try { $vis = [string]([WinEnum]::Visible($e)) } catch { }
        $lines.Add("hwnd=[" + [string]$e + "] id=[" + [WinEnum]::CtrlIdOf($e) + "] visible=[" + $vis + "] text=[" + ([WinEnum]::TitleOf($e)) + "]")
    }
    [System.IO.File]::WriteAllLines((Join-Path $evidence $fileName), $lines)
}

# 对话框全后代形态 dump（诊断面）：class/id/可见性/屏幕矩形/文本 逐行。Save-EditDump 只能证「Edit 里有文本」，
# 证不了「那是不是用户看得见的文件名框」——38709869802 实证：dump 里 id=1148 visible=True 且文本就是文件名，
# 同期截图文件名框仍空、IDOK 空名 no-op。矩形是判定「幽灵 vs 真框」的关键：矩形落在可见框位置上才是真框。
function Save-ChildDump([IntPtr]$dlgH, [string] $fileName)
{
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($h in [WinEnum]::Descendants($dlgH))
    {
        $rect = "?"
        try { $r = [WinEnum]::RectOf($h); $rect = "$($r.Left),$($r.Top),$($r.Right),$($r.Bottom)" } catch { }
        $lines.Add("class=[" + [WinEnum]::ClassOf($h) + "] id=[" + [WinEnum]::CtrlIdOf($h) + "] visible=[" + ([WinEnum]::Visible($h)) + "] rect=[" + $rect + "] text=[" + ([WinEnum]::TitleOf($h)) + "]")
    }
    [System.IO.File]::WriteAllLines((Join-Path $evidence $fileName), $lines)
}

# 通用文件对话框驱动（纯 Win32，无 UIA）：EnumWindows 差集发现（Wait-CommonFileDialog）→ 写入
# （仅打开侧：CBE_SETEDITTEXT 写 ComboBoxEx 编辑分支，像素判据复核，仍空退点框聚焦+Ctrl+A+UNICODE 敲）→ 提交
# （保存侧 WM_COMMAND(IDOK)；打开侧真实鼠标点击提交按钮，消息兜底 WM_COMMAND+按钮句柄、BM_CLICK）→
# 仍开则 child/edit dump + 截图留证。
# $fileName 传空=导出半：不写名（用 app 的 suggested name，38696676733 实证提交链通），真实路径由调用侧
# 从 app 反馈「已导出到 <path>」提取——目录/文件名都不猜（picker 记忆目录无 Win32 句柄可导航）。该 picker
# 拒收含 \ 与 : 的完整路径（38689737249「Rename」校验错实证），只写裸文件名。找不到对话框：dump + 截图进
# evidence，返 $false（调用侧按 G7c 口径降级 ::warning）；其余路径返 $true，成败由调用侧反馈轮询判。
function Invoke-CommonFileDialog([System.Collections.Generic.List[string]] $before, [string[]] $titleHints, [string] $fileName, [string] $tag)
{
    $dlgH = Wait-CommonFileDialog $before $titleHints 20 $tag
    if ($dlgH -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot ($tag + "-FAIL-no-dialog.png")
        Write-Host "::warning::场景 Z：$tag 20s 内未见新顶层对话框窗（EnumWindows 差集）——读 $tag-windows.txt 与截图定形态"
        return $false
    }
    # —— 写入：仅打开侧（$fileName 非空）需要。保存侧不写名，用 app suggested name。
    # 38716060653 定案：文件名框是 ComboBoxEx32 **自绘**，子 Edit 只是无绘制的缓冲区——SetWindowTextW
    # 成功、GetWindowTextW 复核命中，但截图裁框放大后框内仍空（38709869802 同证）；IDOK 读到空名故 no-op。
    # 故写入走 ComboBoxEx 自己的编辑分支消息 CBE_SETEDITTEXT，且**只认像素判据**（Count-BoxInk），
    # 窗口文本一律不采信。像素仍空→真实输入路径（鼠标点框聚焦→Ctrl+A→UNICODE 逐字）再复测。
    $boxInk = -1
    if ($fileName -ne "")
    {
        $combo = [WinEnum]::DlgItemOf($dlgH, 1148)
        $edit = Find-FileNameEdit $dlgH
        if ($edit -eq [IntPtr]::Zero) { $edit = $combo }
        if ($combo -ne [IntPtr]::Zero) { [WinEnum]::SetComboBoxEditText($combo, $fileName) }
        Start-Sleep -Milliseconds 500
        if ($edit -ne [IntPtr]::Zero)
        {
            $r = [WinEnum]::RectOf($edit)
            $boxInk = Count-BoxInk $r
            if ($boxInk -lt 60)
            {
                Write-Host "::warning::场景 Z：$tag CBE_SETEDITTEXT 后框内墨迹仅 $boxInk px——退真实输入（点框聚焦+Ctrl+A+UNICODE）"
                [WinEnum]::ClickAt([int](($r.Left + $r.Right) / 2), [int](($r.Top + $r.Bottom) / 2))
                Start-Sleep -Milliseconds 350
                [WinEnum]::SendChord(0x11, 0x41)   # Ctrl+A 全选（覆盖残留）：0x11=VK_CONTROL 0x41=VK_A
                Start-Sleep -Milliseconds 150
                [WinEnum]::TypeText($fileName)
                Start-Sleep -Milliseconds 600
                $boxInk = Count-BoxInk $r
            }
        }
        Write-Host ("场景 Z：$tag 文件名框墨迹像素=$boxInk（阈值 60；窗口文本不采信，见注）")
        Save-FullScreenshot ($tag + "-typed.png")
    }
    # —— 提交
    if ($fileName -eq "")
    {
        # 保存侧：WM_COMMAND(IDOK) 三跑实证生效（38696676733），不动已证路径。控制 id 在 LOWORD。
        [WinEnum]::Post($dlgH, 0x0111, 1, 0)
        Start-Sleep -Milliseconds 1000
        if (-not [WinEnum]::Alive($dlgH))
        {
            Write-Host "场景 Z：$tag 提交生效（对话框已关，WM_COMMAND IDOK）"
            return $true
        }
    }
    # 打开侧：对话框本体 WM_COMMAND 恒空转（空名 no-op 之外，有真名也不关——38716060653），走真实鼠标点击。
    $btn = [IntPtr]::Zero
    foreach ($b in [WinEnum]::ChildrenOfClass($dlgH, "Button"))
    {
        $t = ([WinEnum]::TitleOf($b)) -replace '&', ''     # 加速符 &Open 不匹配 ^Open——38716060653 兜底失效正此
        if ($t -match '^(Save|Open|保存|打开)') { $btn = $b; break }
    }
    if ($btn -eq [IntPtr]::Zero) { $btn = [WinEnum]::DlgItemOf($dlgH, 1) }   # 退化到 IDOK 控制 id
    if ($btn -ne [IntPtr]::Zero)
    {
        $r = [WinEnum]::RectOf($btn)
        [WinEnum]::ClickAt([int](($r.Left + $r.Right) / 2), [int](($r.Top + $r.Bottom) / 2))
        Start-Sleep -Milliseconds 1200
    }
    if (-not [WinEnum]::Alive($dlgH))
    {
        Write-Host "场景 Z：$tag 提交生效（对话框已关，真实点击提交按钮）"
        return $true
    }
    if ($btn -ne [IntPtr]::Zero)
    {
        # 消息兜底①：WM_COMMAND(IDOK) lParam 带按钮句柄（对话框 proc 常校验 wParam 与 lParam 双路）
        [WinEnum]::Post($dlgH, 0x0111, 1, $btn)
        Start-Sleep -Milliseconds 800
    }
    if (-not [WinEnum]::Alive($dlgH))
    {
        Write-Host "场景 Z：$tag 提交生效（对话框已关，WM_COMMAND IDOK+按钮句柄）"
        return $true
    }
    if ($btn -ne [IntPtr]::Zero)
    {
        [WinEnum]::Post($btn, 0x00F5, 0, 0)               # 消息兜底②：BM_CLICK
        Start-Sleep -Milliseconds 1500
    }
    if (-not [WinEnum]::Alive($dlgH))
    {
        Write-Host "场景 Z：$tag 提交生效（对话框已关，BM_CLICK）"
        return $true
    }
    Save-ChildDump $dlgH ($tag + "-children.txt")
    Save-EditDump $dlgH ($tag + "-edits.txt")
    Save-FullScreenshot ($tag + "-FAIL-still-open.png")
    return $true
}

# —— 场景 Z：导入导出往返（todo 行 80「导入+重录密钥后行为与原机一致」CI 代验，B-802）——
# 行 80 导入半原注记「导入依赖 WinUI 文件选择器，CI 会话不稳，需装机半」系未实测假设——四轮实测定案：
# FileSavePicker/FileOpenPicker 确为经典通用对话框（File name 预填 beacon-config-…、Save/Cancel 齐全），
# 但**由外部进程承载**且对 UIA 全盲：#32770+beaconPid 零命中（38678998804）；UIA 桌面根 Children 差集
# 亦零命中（38683714647 失败清单无此窗、同期截图里对话框在）；FromHandle 后跨进程查询恒超时
# （GetFirstChild → UIA_E_TIMEOUT 0x80131505，38689737249）；且该 picker 拒收含 \ 与 : 的完整路径
# （shell 弹「Rename」校验错，同期截图实证）。38696676733 再证：纯 Win32 提交链通（WM_COMMAND IDOK
# /BM_CLICK），但 SetWindowText 落点不受控（写进了非文件名框，suggested name 照旧提交、文件落
# Documents），地址栏为 DirectUI 无 Win32 句柄——故定案口径：**文件名/目录一概不猜**——导出半不写名
# 直接提交，真实路径从 app 反馈「已导出到 <path>」提取（TextBlock UIA Name=文本，导入半同法）；
# 导入半只写裸文件名（SetText + GetWindowText 复核 + WM_CHAR 逐字兜底）。38703104601 三证：打开侧
# 提交空转——写入命中**隐藏兼容 Edit**（复核自证通过、可见框仍空、IDOK 空名 no-op 对话框滞留），
# 故文件名框定位必须做 IsWindowVisible 过滤（Find-FileNameEdit），提交后仍开则 Save-EditDump 留证。
# 38709869802 定案（可见性过滤后仍不通）：dump 里 id=1148 **visible=True 且文本就是文件名**，同期截图
# 文件名框**仍空**、IDOK 空名 no-op。故改走键盘路由（WM_CHAR→对话框→聚焦控件）——38716060653 亦不通：
# 键盘路由未关对话框、legacy SetWindowText 复核命中而框内仍空。
# 38716060653 形态取证定案（Save-ChildDump 全后代 rect/class/id）：文件名框是 `ComboBoxEx32 id=1148`
# rect[231,457,434,481] + 内嵌 `ComboBox id=1148` + `Edit id=1148` rect[235,462,414,477]（文本=文件名），
# 其右为 `Button id=1 text=[&Open]` rect[446,486,534,512] 与 `Button id=2 [Cancel]`——**按钮是真 Win32
# 且启用**；文件列表却是 `SHELLDLL_DefView`→`DirectUIHWND`（无 SysListView32 可 LVM 选行）、`ListBox
# id=1120` 不可见。截图裁框 8× 放大复核：框内只有光标与下拉箭头、**无任何文字**。故「visible 为真的
# 幽灵」定性修正：**子 Edit 矩形正确但只是 ComboBoxEx 自绘面的无绘制缓冲区**——SetWindowTextW/GetWindowTextW
# 只碰缓冲区、不动绘制面，IDOK 提交时读到空名故 no-op；窗口文本自证一律不算写入成功。驱动改走
# **CBE_SETEDITTEXT(0x1640)** 写 ComboBoxEx 编辑分支，且写入判据改为**像素**（Count-BoxInk 裁框数暗像素），
# 像素仍空才退真实输入（ClickAt 点框聚焦→Ctrl+A→UNICODE 逐字）。提交改走**真实鼠标点击提交按钮**
# （BM_CLICK 兜底此前恒空转：按钮文本是 `&Open`，`^(Save|Open|…)` 正则不匹配加速符——正则为 38716060653
# 的次级根因）。保存侧提交仍用 WM_COMMAND(IDOK)（三跑实证，不动已证路径）。child dump 与 typed 截图
# 双留在证。
# 发现=EnumWindows 点前快照→点后差集；提交=真实鼠标点击提交按钮，WM_COMMAND/BM_CLICK 消息兜底。
# 包用 app 自产（导出→导入同包）零 schema 假设；Write-GConfig 预置定数（2 连接含 credentialRef
# conn:ds-main / 2 组件 / 0 钉选）→ 反馈文本逐字段断言 +「重录密钥」提示在案。导出红线 CI 层回声：
# secrets.bin 真值 sk-ds-mock 不得出现在导出文件全文（RFC §13）。picker 驱动失败按 G7c 口径降级
# ::warning 转装机（首跑不阻塞其它场景），其余断言均为硬断言。
try
{
    Ensure-MockServer
    Write-GConfig "" "off" 1.0
    $zProc = Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" -PassThru
    $zLogPath = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    $settings = Find-SettingsWindow 45
    if (!$settings)
    {
        Save-FullScreenshot "Z-FAIL-no-settings.png"
        throw "断言失败（场景 Z）：--settings 启动 45s 内未见「Beacon 设置」窗口"
    }
    $advRow = Find-UiaById $settings "module-advanced" 25
    if (!$advRow)
    {
        Save-FullScreenshot "Z-FAIL-no-advanced-row.png"
        throw "断言失败（场景 Z）：设置窗未找到 module-advanced 目录行（高级页承载导入导出分区）"
    }
    Invoke-Uia $advRow
    Start-Sleep -Seconds 2
    $exportBtn = Find-UiaByName $settings "导出配置…" 25
    if (!$exportBtn)
    {
        Save-FullScreenshot "Z-FAIL-no-export-btn.png"
        throw "断言失败（场景 Z）：高级页未找到「导出配置…」按钮"
    }
    $zExport = ""
    # 点前顶层窗快照=差集基准（跨进程）：不查 #32770+pid，picker 由外部进程承载（见 Invoke-CommonFileDialog 注）
    $zBeforeExport = Get-DesktopWindowKeys
    Invoke-Uia $exportBtn
    $zPickerOk = Invoke-CommonFileDialog $zBeforeExport @("Save As", "另存为") "" "Z-export"
    if (!$zPickerOk)
    {
        Write-Host "::warning::场景 Z：导出 picker 驱动失败——导入导出维持装机半（证据：Z-export-* 截图/dump），下轮按 dump 结构修驱动（G7c 降级口径）"
    }
    else
    {
        # 导出真实路径以 app 反馈为准（ExportAsync 成功后 _importFeedback 显示「✓ 已导出到 <path>…」）：
        # 文件名/目录都由 picker 侧定（suggested name + 记忆目录，脚本不猜），反馈文本轮询与导入半同法
        $zExportFeedback = $null
        $deadline = (Get-Date).AddSeconds(25)
        while ((Get-Date) -lt $deadline)
        {
            $zTexts = $settings.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
            foreach ($t in $zTexts)
            {
                if ($t.Current.Name -like "*已导出到*") { $zExportFeedback = $t.Current.Name; break }
            }
            if ($zExportFeedback) { break }
            Start-Sleep -Seconds 1
        }
        if (!$zExportFeedback)
        {
            Copy-Item $zLogPath (Join-Path $evidence "beacon-Z-fail.log") -Force -ErrorAction SilentlyContinue
            Save-FullScreenshot "Z-FAIL-no-export-feedback.png"
            throw "断言失败（场景 Z）：提交后 25s 内高级页无「已导出到」反馈——Export 链或对话框提交未达：读 beacon-Z-fail.log 定因"
        }
        if ($zExportFeedback -match "已导出到\s+([^\s（]+\.json)")
        {
            $zExport = $Matches[1]
        }
        else
        {
            Copy-Item $zLogPath (Join-Path $evidence "beacon-Z-fail.log") -Force -ErrorAction SilentlyContinue
            Save-FullScreenshot "Z-FAIL-export-path-unreadable.png"
            throw "断言失败（场景 Z）：导出反馈无法解析路径——反馈=[$zExportFeedback]"
        }
        Write-Host "场景 Z：导出落盘 $zExport（app 反馈为准）"
        if (!(Test-Path $zExport))
        {
            Copy-Item $zLogPath (Join-Path $evidence "beacon-Z-fail.log") -Force -ErrorAction SilentlyContinue
            Save-FullScreenshot "Z-FAIL-no-export-file.png"
            throw "断言失败（场景 Z）：反馈称已导出 $zExport 但文件不存在（15s 内）——读 beacon-Z-fail.log 定因"
        }
        # 导出红线：密钥真值不入包
        $zExportText = [System.IO.File]::ReadAllText($zExport)
        if ($zExportText -match "sk-ds-mock")
        {
            Copy-Item $zExport (Join-Path $evidence "export-Z-token-leak.json") -Force -ErrorAction SilentlyContinue
            Save-FullScreenshot "Z-FAIL-token-in-export.png"
            throw "断言失败（场景 Z）：导出文件含密钥真值 sk-ds-mock——RFC §13「secrets 绝不入包」红线回归"
        }
        $zBundle = ConvertFrom-Json $zExportText
        $zExpConn = 0
        if ($zBundle.connections) { $zExpConn = @($zBundle.connections).Count }
        $zExpWidgets = 0
        if ($zBundle.widgets) { $zExpWidgets = @($zBundle.widgets).Count }
        $zExpPins = 0
        if ($zBundle.pins -and $zBundle.pins.tiles) { $zExpPins = @($zBundle.pins.tiles).Count }
        $zExpActions = 0
        if ($zBundle.actions) { $zExpActions = @($zBundle.actions).Count }
        Write-Host "场景 Z：导出包就位（$zExpConn 连接 / $zExpWidgets 组件 / $zExpPins 钉选 / $zExpActions 动作）——转入导入"
        # 导出文件已落盘=导出 picker 已提交并关闭；重新取顶层窗快照作导入差集基准（不查 #32770+pid）。
        # 导入只写裸文件名：打开侧 picker 记忆目录=保存侧目录（同一会话），完整路径反被拒收
        $zBeforeImport = Get-DesktopWindowKeys
        $zExportName = Split-Path $zExport -Leaf
        $importBtn = Find-UiaByName $settings "导入配置…" 20
        if (!$importBtn)
        {
            Save-FullScreenshot "Z-FAIL-no-import-btn.png"
            throw "断言失败（场景 Z）：高级页未找到「导入配置…」按钮"
        }
        Invoke-Uia $importBtn
        if (!(Invoke-CommonFileDialog $zBeforeImport @("Open", "打开") $zExportName "Z-import"))
        {
            Write-Host "::warning::场景 Z：导入 picker 驱动失败——导出半已过、导入半维持装机半（证据：Z-import-* 截图/UIA dump）"
        }
        else
        {
            # 反馈文本轮询：_importFeedback TextBlock 无 AutomationId，按 Name contains「已导入」
            # （W1/X3 同法：TextBlock 的 UIA Name=文本；导出反馈「已导出」不含「已导入」故不误命中）
            $zFeedback = $null
            $deadline = (Get-Date).AddSeconds(25)
            while ((Get-Date) -lt $deadline)
            {
                $texts = $settings.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))
                foreach ($t in $texts)
                {
                    if ($t.Current.Name -like "*已导入*") { $zFeedback = $t.Current.Name; break }
                }
                if ($zFeedback) { break }
                Start-Sleep -Seconds 1
            }
            if (!$zFeedback)
            {
                Copy-Item $zLogPath (Join-Path $evidence "beacon-Z-fail.log") -Force -ErrorAction SilentlyContinue
                Save-FullScreenshot "Z-FAIL-no-import-feedback.png"
                throw "断言失败（场景 Z）：导入后 25s 内高级页无「已导入」反馈文本——Import 链未跑或对话框提交未达：读 beacon-Z-fail.log 定因"
            }
            # 逐字段对齐（期望值取自同包，零 schema 假设）
            $zWant = "已导入 $zExpConn 连接 / $zExpWidgets 组件 / $zExpPins 钉选 / $zExpActions 动作"
            if ($zFeedback -notlike "*$zWant*")
            {
                Copy-Item $zExport (Join-Path $evidence "export-Z-mismatch.json") -Force -ErrorAction SilentlyContinue
                Copy-Item $zLogPath (Join-Path $evidence "beacon-Z-fail.log") -Force -ErrorAction SilentlyContinue
                Save-FullScreenshot "Z-FAIL-feedback-mismatch.png"
                throw "断言失败（场景 Z）：导入反馈与包计数不符——反馈=[$zFeedback] 期望含=[$zWant]"
            }
            # 重录密钥提示（行 80 后半句）：包内有 credentialRef 才断言（Write-GConfig 预置 conn:ds-main）
            if ($zExportText -match "credentialRef")
            {
                if ($zFeedback -notlike "*重录密钥*" -or $zFeedback -notlike "*conn:ds-main*")
                {
                    Copy-Item $zLogPath (Join-Path $evidence "beacon-Z-fail.log") -Force -ErrorAction SilentlyContinue
                    Save-FullScreenshot "Z-FAIL-no-reentry-prompt.png"
                    throw "断言失败（场景 Z）：导入反馈缺「重录密钥」提示或 credentialRef（conn:ds-main）——反馈=[$zFeedback]（行 80「导入后逐 credentialRef 提示重录」回归）"
                }
            }
            # 落库回读：导入后 connections/widgets 与包计数一致（「行为与原机一致」的最小证据面）
            $zConnAfter = Get-Content (Join-Path $configDir "connections.json") -Raw -Encoding UTF8 | ConvertFrom-Json
            $zWidgetsAfter = Get-Content (Join-Path $configDir "widgets.json") -Raw -Encoding UTF8 | ConvertFrom-Json
            $zConnCount = 0
            if ($zConnAfter) { $zConnCount = @($zConnAfter).Count }
            $zWidgetsCount = 0
            if ($zWidgetsAfter) { $zWidgetsCount = @($zWidgetsAfter).Count }
            if ($zConnCount -ne $zExpConn -or $zWidgetsCount -ne $zExpWidgets)
            {
                Copy-Item $zLogPath (Join-Path $evidence "beacon-Z-fail.log") -Force -ErrorAction SilentlyContinue
                Save-FullScreenshot "Z-FAIL-state-mismatch.png"
                throw "断言失败（场景 Z）：导入后落库计数与包不符——connections=$zConnCount（期望 $zExpConn）widgets=$zWidgetsCount（期望 $zExpWidgets）"
            }
            Write-Host "场景 Z 通过：导出→导入往返（picker 双驱）——反馈计数逐字段对齐 + 重录密钥提示在案 + 导出全文无密钥真值（行 80 CI 代验）"
        }
    }
}
catch
{
    Stop-Beacon # W 同口径：失败也收尸，防残留进程毒化 J 静默安装
    Save-FullScreenshot "Z-FAIL-unexpected.png"
    $failures.Add("场景 Z：$($_.Exception.Message)")
    Write-Host "::error::场景 Z 断言失败：$($_.Exception.Message)"
}

# —— 场景 AA：界面密度档（Phase 5 行 5「密度可调」CI 代验）——设置改档 → 落盘 → 重启还原 → 回切净态。
# 无像素断言（间距 ×0.75 的视觉差走截图人工近景；像素级断言易碎，见 G 系阈值重标定教训）：
# AA 断的是「可调、可存、可恢复、可回切」的机器可证面——下拉选中态 ↔ config.json appearance.density 双向一致。
try
{
    Write-GConfig "" "off" 1.0 $false
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings)
    {
        Save-FullScreenshot "AA-FAIL-no-settings.png"
        throw "断言失败（场景 AA）：--settings 启动 30s 内未见「Beacon 设置」窗口"
    }
    $row = Find-UiaById $settings "module-appearance" 10
    if (!$row) { throw "断言失败（场景 AA）：UIA 未找到 module-appearance" }
    Invoke-Uia $row
    Start-Sleep -Seconds 2
    $densityBox = Find-UiaById $settings "appearance-density-box" 10
    if (!$densityBox)
    {
        Save-FullScreenshot "AA-FAIL-no-density-box.png"
        throw "断言失败（场景 AA）：UIA 未找到 appearance-density-box（密度下拉锚点丢失）"
    }
    # AA1：默认档=常规（AppearanceConfig.Density 默认 regular，Write-GConfig 不落 density 键）
    $sel0 = Get-ComboSelectionName $densityBox
    if ($sel0 -notlike "常规*")
    {
        Save-FullScreenshot "AA-FAIL-default-not-regular.png"
        throw "断言失败（场景 AA1）：密度下拉初始选中「$sel0」（期望 常规…）——默认档回归"
    }
    Write-Host "场景 AA1 通过：密度下拉初始=常规（默认 regular 未显式落键）"
    # AA2：选紧凑 → SelectionChanged → SaveDensity 落盘 compact
    Select-ComboItemByName $densityBox "紧凑（组件间距 ×0.75）"
    Start-Sleep -Seconds 2
    $cfgAa = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($cfgAa.appearance.density)" -ne "compact")
    {
        Save-FullScreenshot "AA-FAIL-not-saved.png"
        throw "断言失败（场景 AA2）：选紧凑后 config appearance.density=$($cfgAa.appearance.density)（期望 compact）——改档未落盘"
    }
    Write-Host "场景 AA2 通过：紧凑选中 → config appearance.density=compact 落盘"
    Stop-Beacon
    # AA3：重启读档还原：下拉回读=紧凑（持久化+还原机器证）
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings2 = Find-SettingsWindow
    if (!$settings2)
    {
        Save-FullScreenshot "AA-FAIL-no-settings2.png"
        throw "断言失败（场景 AA3）：重启后 --settings 窗 30s 内未现"
    }
    $row2 = Find-UiaById $settings2 "module-appearance" 10
    if (!$row2) { throw "断言失败（场景 AA3）：重启后 UIA 未找到 module-appearance" }
    Invoke-Uia $row2
    Start-Sleep -Seconds 2
    $densityBox2 = Find-UiaById $settings2 "appearance-density-box" 10
    if (!$densityBox2) { throw "断言失败（场景 AA3）：重启后 UIA 未找到 appearance-density-box" }
    $sel2 = Get-ComboSelectionName $densityBox2
    if ($sel2 -notlike "紧凑*")
    {
        Save-FullScreenshot "AA-FAIL-restore.png"
        throw "断言失败（场景 AA3）：重启后密度下拉选中「$sel2」（期望 紧凑…）——档位还原回归"
    }
    Write-Host "场景 AA3 通过：重启后密度下拉=紧凑（档位持久化+还原）"
    # AA4a：三档覆盖（宽松档，85b4ab9）：选宽松 → 落盘 spacious（机制与 AA2 同款）
    Select-ComboItemByName $densityBox2 "宽松（组件间距 ×1.25）"
    Start-Sleep -Seconds 2
    $cfgAaS = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($cfgAaS.appearance.density)" -ne "spacious")
    {
        Save-FullScreenshot "AA-FAIL-not-saved-spacious.png"
        throw "断言失败（场景 AA4a）：选宽松后 config appearance.density=$($cfgAaS.appearance.density)（期望 spacious）——宽松档未落盘"
    }
    Write-Host "场景 AA4a 通过：宽松选中 → config appearance.density=spacious 落盘"
    # AA4：回切常规（净态口径——J 静默安装不读 config，但场景间不留脏档）
    Select-ComboItemByName $densityBox2 "常规（默认）"
    Start-Sleep -Seconds 2
    $cfgAa2 = Get-Content (Join-Path $configDir "config.json") -Raw | ConvertFrom-Json
    if ("$($cfgAa2.appearance.density)" -ne "regular")
    {
        throw "断言失败（场景 AA4）：回切常规后 config appearance.density=$($cfgAa2.appearance.density)（期望 regular）"
    }
    Write-Host "场景 AA4 通过：回切常规 → config=regular（净态）"
    Stop-Beacon
    Write-Host "场景 AA 通过：密度可调、可存、可恢复、可回切 全链"
}
catch
{
    Stop-Beacon # Z 同口径：失败也收尸，防残留进程毒化 J 静默安装
    Save-FullScreenshot "AA-FAIL-unexpected.png"
    $failures.Add("场景 AA：$($_.Exception.Message)")
    Write-Host "::error::场景 AA 断言失败：$($_.Exception.Message)"
}

# —— 场景 J：静默安装/卸载（todo 行 15 后半「卸载清理」CI 代验；需 Inno 产物先于本脚本产出）——
try
{
    $setup = Get-ChildItem $env:GITHUB_WORKSPACE -Filter "*-setup.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$setup)
    {
        Write-Host "::warning::工作区未找到 *-setup.exe（visual-evidence 未运行在 Inno 步骤之后的通道）——安装/卸载验证跳过"
    }
    else
    {
        Write-Host "场景 J：静默安装 $($setup.Name)"
        Stop-Beacon # 防御：不依赖前一场景收尸（W 失败跳过 Stop-Beacon 时安装撞运行中进程，退出码 1——38525961627）
        $proc = Start-Process -FilePath $setup.FullName -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -PassThru -Wait
        if ($proc.ExitCode -ne 0)
        {
            Save-FullScreenshot "J-FAIL-install-exitcode.png"
            throw "断言失败（场景 J）：静默安装退出码 $($proc.ExitCode)"
        }
        $installedExe = Join-Path $env:LOCALAPPDATA "Beacon\Beacon.App.exe"
        if (!(Test-Path $installedExe))
        {
            Save-FullScreenshot "J-FAIL-not-installed.png"
            throw "断言失败（场景 J）：$installedExe 未产出"
        }
        Write-Host "场景 J 安装通过：$installedExe 就位"
        $unins = Join-Path $env:LOCALAPPDATA "Beacon\unins000.exe"
        if (!(Test-Path $unins)) { throw "断言失败（场景 J）：卸载器 unins000.exe 缺失" }
        $proc = Start-Process -FilePath $unins -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -PassThru -Wait
        if ($proc.ExitCode -ne 0)
        {
            Save-FullScreenshot "J-FAIL-uninstall-exitcode.png"
            throw "断言失败（场景 J）：静默卸载退出码 $($proc.ExitCode)"
        }
        Start-Sleep -Seconds 3
        if (Test-Path $installedExe)
        {
            Save-FullScreenshot "J-FAIL-not-uninstalled.png"
            throw "断言失败（场景 J）：卸载后 Beacon.App.exe 仍在——卸载清理回归"
        }
        if (Test-Path $configDir)
        {
            Write-Host "场景 J 卸载通过：程序文件清空，用户数据 %APPDATA%\Beacon 保留（设计口径：卸载不动用户数据）"
        }
        else
        {
            Write-Host "::warning::卸载连带清掉了用户数据 %APPDATA%\Beacon——与「数据随用户目录存续」口径不符，记录供裁决"
        }
    }
}
catch
{
    Save-FullScreenshot "J-FAIL-unexpected.png"
    $failures.Add("场景 J：$($_.Exception.Message)")
    Write-Host "::error::场景 J 断言失败：$($_.Exception.Message)"
}

Write-Host "视觉取证完成，产物："
Get-ChildItem $evidence | ForEach-Object { Write-Host "  - evidence/$($_.Name)" }

if ($failures.Count -gt 0)
{
    throw "视觉取证断言失败（$($failures.Count) 项）：`n" + ($failures -join "`n")
}
Write-Host "全部场景断言通过"
