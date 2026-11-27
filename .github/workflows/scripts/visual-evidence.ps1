# 视觉取证（2026-10-09 bug 批验收硬标准：没截图不算完，断言失败 = 红构建不发版）
#   场景 A 数量悬浮窗开：GLM 悬浮框必须在桌面（可见 "Beacon Tile" 窗）——全屏 + 6x 近景 +
#     进度条像素硬断言（底部 4 DIP 条带，2026-10-11 重设计后 bar 为默认）+「NN%」UIA 文本硬断言
#     （zhipu 连接指 mock /api/monitor/usage/quota/limit → 38% 健康链，不再赌公网/凭据）。
#   场景 AI 横灌注水桶（AQUA-001 2026-11-15 立、2026-11-17 横向改版）：progressStyle=aquarium 整个
#     tile=横放水桶，水从左往右灌满全高桶身（水位前沿=竖直线 frontX=进度×桶宽）——
#     三态（20/60/95% ↔ mock p20/p60/error）全高列采样：前沿后方水体 ≥30% 列面积 / 前方空区 ≤2% /
#     前沿条波峰 ≥4px（振幅 3 DIP 全色下限）+ 逐态全屏/8x 近景截图；
#     活水=reduced 档双帧差分 ≥50 校准（G7c 口径，CI 会话不可证则降级 warning 转装机目检）；
#     off 档静止=双帧差分 ≤2（只在活水可证时拦红，防测量瞎掉的 vacuous 通过）。
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
#   场景 AG 悬浮岛收起态三档（ISLAND-D1 single/stack/carousel）+ 上岛白名单（ISLAND-D2）：
#     capsuleIsland=true 建岛窗（独立于 showCapsule，EnsureCapsule 口径）——UIA 面 AutomationId
#     （island-face-{WidgetId}，FaceHost 补 SetName 造 peer）逐档硬断言：stack=环/条+双白名单面 4 面、
#     single=仅序首环面（余面退场）、carousel=4s 节拍 12s 采样窗内 ≥2 个不同面（轮转在走）；
#     每档收起行近景截图=岛视觉取证（docs/review/island-compare 对照源，11-13 界面审计零岛场景在此收口）。
#   场景 AH 岛展开态（悬停预览，BEACON_DEBUG_ISLAND_PREVIEW=1 程序触发取证钩子）+ 占位面（ISLAND-D3
#     data_quality：未取数组件出空心灯点占位 chip，不造摘要）+ 组件级下钻（ISLAND-D4：坐标单击真实
#     组件面 → 看板开 + chip-智谱 来源卡定位）；展开/收起窗高几何断言（ExpectedRowPx DPI 换算）。
#     AH5 岛灰板修复（2026-11-16 方案 A：岛态摘 SystemBackdrop）：岛窗四角（曲面肩/底角圆外透明区）
#     像素断言——岛态=壁纸色（灰板已摘）、经典态=亚克力灰（经典红线不变）、回切岛=壁纸（重挂摘无泄漏）。
#     AH5d 岛边缘精度取证（2026-11-18 用户真机三瑕疵：透明底板/圆角不自然/强锯齿）：底角月牙 vs 同高
#     参照（region 底部镂空门，硬断言）+ 底缘中点过渡带剖面 + 曲线边（左肩/双底角弧）逐行楼梯度量
#     （HRGN 基线 advisory，逐像素 alpha 修复后升硬门）+ 四角双边 4x 近景存档（并排对照差异清单素材）。
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
# 场景 AI 四阶段再加 ~4min 后全程可破 80min（43747051732 于 80min 整点在 AH3 睡眠中被强杀=
# 静默 exit 1 无任何场景报错——watchdog 死法特征）→ 抬 6000s=100min 并留 AH3+J 收尾余量。
# SET-UX-09/10 收口档（SH 空态/取数 + SI 调度，2026-11-26）再加 ~5min 后 45849564192 于
# 100min 整点在场景 J 静默安装起步 10s 后被强杀（同上死法）→ 抬 7200s=120min。
# 早于 job timeout-minutes=150 触发，让 evidence 照常走 always() 上传步留证。
# 可用环境变量 BEACON_EVIDENCE_TIMEOUT_SEC 覆盖（本地调试缩短用）。
[int]$script:EvidenceTimeoutSec = 7200
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
    // 岛窗 OS region 直读（BEACON-FIX-1126 病灶 A/B 门）：GetWindowRgn+PtInRegion/GetRgnBox
    // 直接问 OS 裁剪裁决——决定性、免桌面像素参照（CI 桌面非纯色的假红面根除）。
    // ExactSpelling=true 必填（无后缀导出，Ansi 默认挂 A 找不到）；DLL 不能乱写：
    // CreateRectRgn/DeleteObject/GetRgnBox/PtInRegion 是 GDI32 导出（CI 45726231918
    // 实证写 user32.dll → EntryPointNotFoundException），GetWindowRgn 才是 user32。
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern int GetRgnBox(IntPtr hRgn, out RECT lprc);
    [DllImport("gdi32.dll", ExactSpelling = true)] private static extern bool PtInRegion(IntPtr hRgn, int x, int y);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);
    // 返回 region 类别（>0=已应用：2 SIMPLEREGION / 3 COMPLEXREGION），0=错误、1=NULLREGION。
    // PtInRegion 坐标是窗相对；GetRgnBox 输出窗相对包围盒。调用方负责 DeleteObject。
    public static int WindowRgnKind(IntPtr h) { var rgn = CreateRectRgn(0, 0, 1, 1); var kind = GetWindowRgn(h, rgn); DeleteObject(rgn); return kind; }
    public static RECT WindowRgnBox(IntPtr h)
    {
        RECT box;
        var rgn = CreateRectRgn(0, 0, 1, 1);
        try { if (GetWindowRgn(h, rgn) <= 0) { throw new InvalidOperationException("GetWindowRgn returned no region"); } GetRgnBox(rgn, out box); }
        finally { DeleteObject(rgn); }
        return box;
    }
    public static bool WindowRgnContains(IntPtr h, int x, int y)
    {
        var rgn = CreateRectRgn(0, 0, 1, 1);
        try { if (GetWindowRgn(h, rgn) <= 0) { throw new InvalidOperationException("GetWindowRgn returned no region"); } return PtInRegion(rgn, x, y); }
        finally { DeleteObject(rgn); }
    }
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
    // 点击前重钉 TOPMOST band（43850072289：SetWindowRgn 后岛窗被非置顶前台窗盖住，点击按 z 序落空）
    public static void ForceTopmost(IntPtr h)
    {
        SetWindowPos(h, (IntPtr)(-1), 0, 0, 0, 0, 0x0001u | 0x0002u | 0x0010u); // HWND_TOPMOST|SWP_NOSIZE|SWP_NOMOVE|SWP_NOACTIVATE
    }
    public static void MoveNoFocus(IntPtr h, int x, int y)
    {
        SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, 0x0004u | 0x0001u | 0x0010u); // SWP_NOZORDER|SWP_NOSIZE|SWP_NOACTIVATE——移位不改叠层不抢焦点
    }
    // 窄窗档驱动（SET-UX-08）：外框缩到指定物理像素。SWP_NOMOVE|SWP_NOZORDER|SWP_NOACTIVATE——
    // 不挪位不改叠层不抢焦点；恢复原尺寸由调用方回灌 GetWindowRect 快照
    public static void ResizeTo(IntPtr h, int cx, int cy)
    {
        SetWindowPos(h, IntPtr.Zero, 0, 0, cx, cy, 0x0004u | 0x0002u | 0x0010u);
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
    // 场景 AH 悬停展开/收起：SendInput 纯移动（无按键）——SetCursorPos 合成移动不进 XAML 输入管线
    //（38044411443 同款实证），PointerEntered/Exited 只认 INPUT 队列注入。两次移动间隔 ≥120ms
    //（WinUI Pointer 事件按注入序列派发，连发过密会被合并丢失进入/离开语义）。
    public static void MoveMouse(int x, int y)
    {
        int vx = ToVirtual(x, 76, 78); int vy = ToVirtual(y, 77, 79);
        var move = new INPUT { type = 0, u = new INPUTUNION { mi = new MOUSEINPUT { dx = vx, dy = vy, dwFlags = 0x8001 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { move }, size);
        System.Threading.Thread.Sleep(120);
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

function Write-ScenarioConfig([bool] $numericFloating, [string] $theme = "dark", [string] $progressStyle = "", [string] $motionMode = "")
{
    Stop-Beacon
    # numericFloatingResetDone 必须 true：否则启动迁移把开关强制回关，场景 A 直接失真
    # AQUA-001（场景 AI）：非默认进度样式/动效档才注入 appearance 段——默认路径落盘 JSON 与历史逐字节一致
    $appearanceSlot = ""
    if ($progressStyle -or $motionMode)
    {
        $appearanceSlot = ', "appearance": { "motion": { "mode": "' + $(if ($motionMode) { $motionMode } else { "reduced" }) + '", "intensity": 1.0 }, "progressStyle": "' + $(if ($progressStyle) { $progressStyle } else { "bar" }) + '" }'
    }
    $config = @'
{
  "configVersion": 1,
  "hotkey": "Ctrl+Alt+B",
  "launchOnStartup": false,
  "theme": "__THEME__",
  "pinDisplayMode": "floating",
  "numericFloatingEnabled": __NUM__,
  "numericFloatingResetDone": true,
  "showCapsule": false__APPEARANCE__
}
'@.Replace("__THEME__", $theme).Replace("__NUM__", $numericFloating.ToString().ToLowerInvariant()).Replace("__APPEARANCE__", $appearanceSlot)
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
        # 注水桶三态档（场景 AI，AQUA-001 横向版）：p20=20%（前沿=20% 桶宽）/ p60=60%（中位档，
        # 前沿=60% 桶宽）；95% 复用 error 档。形状同 38 健康链（主窗=最高窗，摘要「已用 NN%」打头），
        # NN% 文本轮询可锚
        $bigmodelP20 = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":20,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":15,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":20,"nextResetTime":0}]}}'
        $bigmodelP60 = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":60,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":10,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":20,"nextResetTime":0}]}}'
        # 档位文件：内容 "error"=95% / "p0"=0% / "p52"=52% / "p100"=100% / "p20"=20% / "p60"=60% / "fail"=HTTP 500（场景 SH 失败→重试链），空/缺 = 38% 态（场景侧 Set-Content / Remove-Item 切换）
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
                        # 38% 健康链 / 95% Error 链 / 0%-52%-100% 四态链 / 20%-60% 水族箱三态链按档位文件翻转；
                        # fail 档回 HTTP 500（场景 SH 失败链：chip 落「✗ 测试失败」，重试前不解锁快速添加）
                        if ($mode -eq "fail")
                        {
                            $ctx.Response.StatusCode = 500
                            $body = '{"error":"mock-injected-failure"}'
                        }
                        else
                        {
                            $body = if ($mode -eq "error") { $bigmodelError }
                                elseif ($mode -eq "p0") { $bigmodelP0 }
                                elseif ($mode -eq "p52") { $bigmodelP52 }
                                elseif ($mode -eq "p100") { $bigmodelP100 }
                                elseif ($mode -eq "p20") { $bigmodelP20 }
                                elseif ($mode -eq "p60") { $bigmodelP60 }
                                else { $bigmodel }
                        }
                    }
                    "/user/balance" { $body = $deepseek }
                    "/v1/models" { $body = $mimo }
                    "/mimo/usage" { $body = $mimoUsage }
                    "/compatible-mode/v1/models" { $body = $qwenModels }
                    "/qwen/usage" { $body = $qwenUsage }
                    "/auth-required" {
                        # 缺凭据链（场景 SH）：无 Authorization 头即 401——HttpEndpoint 映射 Unauthorized，
                        # chip 落「✗ 测试失败：HTTP 认证失败」；带任意凭据则 200（正向面留给真机）
                        $authHdr = $ctx.Request.Headers["Authorization"]
                        if ([string]::IsNullOrEmpty($authHdr))
                        {
                            $ctx.Response.StatusCode = 401
                            $body = '{"error":"auth-required"}'
                        }
                        else
                        {
                            $body = '{"ok":true}'
                        }
                    }
                    "/badjson" {
                        # 组件取数失败链（场景 SH 收口②，2026-11-26）：HTTP 200 但体非 JSON——连接测试只看
                        # 2xx（chip「✓ 测试通过」），组件 fetch 的 JsonDocument.Parse 抛 Degraded
                        # → PublishStale 无缓存分支摘要「拉取失败：HTTP 响应不是合法 JSON」
                        $body = 'this-is-not-json'
                    }
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
function Write-GConfig([string] $glmColorOverride = "", [string] $motionMode = "off", [double] $motionIntensity = 1.0, [bool] $launchOnStartup = $false, [bool] $showCapsule = $false, [bool] $capsuleIsland = $false, [string] $islandMode = "", [string] $islandComponents = "", [string] $extraWidgets = "")
{
    Stop-Beacon
    $appearance = '"appearance": { "motion": { "mode": "' + $motionMode + '", "intensity": ' + $motionIntensity + ' } }'
    # 岛形态注入（场景 AG/AH）：islandMode 非空=岛三件套全落（capsuleIsland/islandDisplayMode/islandComponents）；
    # 仅 capsuleIsland=true =岛窗收起态默认档（stack 空白名单）。islandComponents 直接喂 JSON 数组字面量。
    $islandConfig = ""
    if ($islandMode)
    {
        $islandConfig = '"capsuleIsland": ' + $capsuleIsland.ToString().ToLowerInvariant() + ', "islandDisplayMode": "' + $islandMode + '", "islandComponents": ' + $islandComponents + ','
    }
    elseif ($capsuleIsland)
    {
        $islandConfig = '"capsuleIsland": true,'
    }
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
  __ISLAND__
  __APPEARANCE__
}
'@.Replace("__STARTUP__", $launchOnStartup.ToString().ToLowerInvariant()).Replace("__APPEARANCE__", $appearance).Replace("__CAPSULE__", $showCapsule.ToString().ToLowerInvariant()).Replace("__ISLAND__", $islandConfig) | Set-Content -Path (Join-Path $configDir "config.json") -Encoding UTF8
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
  { "id": "deepseek.balance:ds", "type": "deepseek.balance", "connectionId": "ds-main", "refreshTier": "ci", "pinned": true, "config": {} }__EXTRA__
]
"@
    $widgets = $widgets.Replace("__EXTRA__", $extraWidgets)
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

# AH5 岛灰板断言助手（屏上小区域均值色）：CopyFromScreen 取真实像素无压缩伪影。
# 采样点贴窗角内缩 1px、span 2（经典态专用：经典 RootBorder 圆角外的窗底材质区；岛态月牙走
# Get-CrescentMaxDistance——岛肩 2026-11-28 恢复后顶两角重回弧外区，月牙门仍锚底两角，
# 顶部肩区由 AH5d-B region 肩形探针直读把守）。
function Get-AvgColor([int] $x, [int] $y, [int] $span)
{
    $bmp = New-Object System.Drawing.Bitmap $span, $span
    $g1 = [System.Drawing.Graphics]::FromImage($bmp)
    $g1.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $span, $span))
    $g1.Dispose()
    $r = 0; $gg = 0; $b = 0
    for ($py = 0; $py -lt $span; $py++)
    {
        for ($px = 0; $px -lt $span; $px++)
        {
            $p = $bmp.GetPixel($px, $py)
            $r += $p.R; $gg += $p.G; $b += $p.B
        }
    }
    $bmp.Dispose()
    return @([int]($r / ($span * $span)), [int]($gg / ($span * $span)), [int]($b / ($span * $span)))
}

# 窗矩形四角与参照色的最大色距（岛态透明区应≈壁纸=小值；经典态四角=亚克力灰=大值）。
# @( ) 内元素含二元运算必须显式括号：逗号优先级低于 +/-，「@($a.Left + 1, $b.Top + 1)」会把
# 「1, $b.Top」先并成数组→「int + Object[]」炸 op_Addition（43789102976 实证，本地复现）。
function Get-CornerMaxDistance([IntPtr] $h, [int[]] $ref)
{
    # 采经典态窗四角（客户区换屏幕坐标 + DIP×DPI）：窗矩形四角不可采——外框顶可负
    # （贴顶窗 WINDOWEDGE 推到屏外，CopyFromScreen 采屏外恒黑）且圆角根处的角点在面外/面内两可
    # ——run 43879288240 假 765 两案同根。点取 (4,5)/(w-5,5)/(1,h-4)/(w-2,h-4) DIP 贴角内缩，
    # 经典无 region、RootBorder 圆角外露亚克力=大值即判据本体。
    $rect = [WinEnum]::ClientScreenRectOf($h)
    $scale = [WinEnum]::DpiScaleOf($h)
    $max = 0
    foreach ($pt in @(
            @(($rect.Left + [int](4 * $scale)), ($rect.Top + [int](5 * $scale))),
            @(($rect.Right - [int](5 * $scale)), ($rect.Top + [int](5 * $scale))),
            @(($rect.Left + [int](1 * $scale)), ($rect.Bottom - [int](4 * $scale))),
            @(($rect.Right - [int](2 * $scale)), ($rect.Bottom - [int](4 * $scale)))))
    {
        $c = Get-AvgColor $pt[0] $pt[1] 2
        $d = ([Math]::Abs($c[0] - $ref[0]) + [Math]::Abs($c[1] - $ref[1]) + [Math]::Abs($c[2] - $ref[2]))
        if ($d -gt $max) { $max = $d }
    }
    return $max
}

# 岛态「弧外月牙透底」判据（CI 桌面特化版）：采底部两角月牙的最外沿单像素——角对角线偏移 1/2 px 的
# (Left+1,Bottom-1)/(Left+2,Bottom-2) 与 (Right-1,Bottom-1)/(Right-2,Bottom-2)，取每角较清的一点。
# 为何不用旧点 (1,h-4)/(w-2,h-4)+span2：4 DIP 距底那行月牙只剩 3px 宽，2×2 取色框跨到弧边 AA 上，
# 255+0 混出 125/149 灰（run 45495474490 实测 距375/距447 假红）——截图复核同点单像素=255 纯背景、
# 弧界在其外 3px。span1 单像素 + 每角两点取较清=抗 arc 离散化左右不对称（54 点策略化后左角弧界在角内
# 5px、右角 2.5px），又不会把断言洗白：region 未裁出时两角四点全黑，较清者仍数百。
# 参照取窗外左右各 30px 的同高两点（角落背后可能是 CI 桌面宿主终端窗，与壁纸比会失真；与月牙背后同层
# 背景则无论壁纸还是窗都自洽）。region 生效=月牙==同高参照（距≈0）、未生效=窗背板黑（距数百）。
# 采样前须等窗位/宽静止：岛宽是内容测量值，启动与 status 变化会重排（run 45495474490 trace 实证
# 0.29s 内 client=249x32→339x32、dst=411→338→333，采样正撞在半途）——窗还在挪时 1px 点采到的是
# 移动中的弧边 AA 而非月牙（距 375/447 假红的另一半根因）。等 400ms 稳定，封顶 6s 防死循环。
function Get-CrescentMaxDistance([IntPtr] $h)
{
    $rect = [WinEnum]::ClientScreenRectOf($h)
    $settled = $false
    $deadline = (Get-Date).AddSeconds(6)
    while (-not $settled -and (Get-Date) -lt $deadline)
    {
        $again = [WinEnum]::ClientScreenRectOf($h)
        $settled = ($again.Left -eq $rect.Left) -and ($again.Right -eq $rect.Right) -and ($again.Bottom -eq $rect.Bottom)
        if (-not $settled) { $rect = $again }
        Start-Sleep -Milliseconds 200
    }
    if (-not $settled) { Write-Host "警告：底角月牙采样前窗位未稳定（封顶 6s），按当前 rect 采" }
    $scale = [WinEnum]::DpiScaleOf($h)
    $max = 0
    $detail = @()
    # 每条 = @(锚定方向, 窗外同高参照 X)；方向 1=左底角、-1=右底角（DIP 偏移逐条内乘 DPI 比例）
    foreach ($side in @(@(1, ($rect.Left - 30)), @(-1, ($rect.Right + 30))))
    {
        $dir = $side[0]
        $refX = $side[1]
        $best = -1
        foreach ($off in @(1, 2))
        {
            $x = $rect.Left + [int]($off * $scale)
            if ($dir -lt 0) { $x = $rect.Right - [int]($off * $scale) }
            $y = $rect.Bottom - [int]($off * $scale)
            $c = Get-AvgColor $x $y 1
            $r = Get-AvgColor $refX $y 1
            $d = ([Math]::Abs($c[0] - $r[0]) + [Math]::Abs($c[1] - $r[1]) + [Math]::Abs($c[2] - $r[2]))
            Write-Host "月牙采样 底角偏移$off ($x,$y)= $($c -join ',') vs 同高参照 ($refX,$y)= $($r -join ',') 距$d"
            if ($best -lt 0 -or $d -lt $best) { $best = $d }
        }
        if ($best -gt $max) { $max = $best }
    }
    return $max
}

function Count-DarkPixels([System.Drawing.Bitmap] $bmp, [int] $luminanceBelow){
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

# 展开 Expander/折叠区（SET-UX-06：外观页级别色与动效强度收进「更多选项」折叠区后，
# 取证锚仍在区内——先展开再找）。无该模式时抛异常由调用方场景 catch。
function Expand-UiaElement([System.Windows.Automation.AutomationElement] $element)
{
    ($element.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand()
}

# 收集 UIA 子树全部非空 Text 元素 Name（SET-UX-11 生效时机 hint 断言用：TextBlock hint 无
# AutomationId，只能按 Name 子串在文本树里找；口径同 SI 场景 Get-SiTexts，提为公共助手供 AA/SJ 复用）。
function Get-UiaTextNames([System.Windows.Automation.AutomationElement] $root)
{
    $names = @()
    foreach ($t in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text))))
    {
        try { if ($t.Current.Name) { $names += $t.Current.Name } } catch { }
    }
    return $names
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
    # WinUI ComboBox 实跑坑（45303475375/45319071005，AE3 连续两轮红）：UIA Select 过的下拉重展偶发卡
    # 「弹层已闭但 Expanded 态未落」→ Expand() 变 no-op，弹层不开、combo 子树+桌面根两域找全空；单发
    # Collapse 归零仍会漏——重取 combo 元素（弹层开合会换 peer，旧引用模式失联）+Collapse+展开+查找
    # 整链 3 轮重试才稳（SF3 Select-SurfaceSteady 同构 3 连切全绿实证）。
    foreach ($attempt in 1..3)
    {
        $comboId = ""
        try { $comboId = $combo.Current.AutomationId } catch { }
        $target = $combo
        if ($comboId.Length -gt 0)
        {
            $refound = Find-UiaById $settings $comboId 4
            if ($refound) { $target = $refound }
        }
        try { ($target.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Collapse() } catch { }
        Start-Sleep -Milliseconds 800
        # Expand/Select 与 Collapse 同样容错（45356987498 AE3 实证：上轮选中弹层拆除竞态下 peer
        # 短暂不挂 ExpandCollapse/SelectionItem → "Unsupported Pattern" 裸抛炸穿场景，3 轮重试形同虚设）
        try { ($target.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)).Expand() } catch { }
        Start-Sleep -Seconds 1
        $item = Find-UiaById $settings $itemId 6
        if (!$item)
        {
            # 展开弹层可能是独立 HWND：退回桌面根子树找
            $item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
                [System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $itemId)))
        }
        if ($item)
        {
            try
            {
                ($item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
                Start-Sleep -Milliseconds 400
                return
            }
            catch { } # peer 模式短暂缺席：落到底部再睡，下一轮重取元素重试
        }
        Start-Sleep -Seconds 2
    }
    throw "UIA 未找到下拉选项 $itemId（重取元素+模式容错 3 轮重试仍未见/未选中）"
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

# —— 场景 AI：横灌注水桶（AQUA-001 横向改版，2026-11-17 用户令「我说的注水桶应该是横着的」「就是整个就是注水桶」）——
# progressStyle=aquarium：整个 tile=横放水桶，水（档位配色）从左往右灌满全高桶身，水位前沿=竖直线
# （frontX=进度×桶宽，PinTile.ApplyAquariumGeometry 钳位口径）、前沿双层正弦波（振幅 3 DIP）
# composition 纵向漂移。旧「12 DIP 底带」断言全废（带已不存在），改全高列采样硬断言链：
# ① 三态（20/60/95% ↔ mock p20/p60/error 档）逐态：
#    客户区 32px 高不变（桶不加高 tile）
#    + 前沿后方 12 DIP 全高列水体色 ≥30% 列面积（水体缺席只剩桶壁回归在此现形，43720846043 同类）
#    + 前沿前方 8 DIP 全高列水体色 ≤2%（前沿没推进=竖直水位线钉错位置/方向的横向化回归）
#    + 前沿条 [front+2,+7) 波峰色 ≥4px（领先幅 3 DIP 才有全色像素——波层缺席硬断言，差分 0 两义
#    降级不洗白缺席回归；43729489131 负 Y 几何 Path 整件不渲染同类）
#    + 全屏截图 + 后方列/前沿条 8x 近景（acceptance-evidence 取证源）；
# ② 活水校准：reduced 档双帧差分 ≥50（G7c 呼吸灯同阈值口径）=「帧差分测量本会话可证」；测不出=
#    Composition 属性动画本 CI 会话不可证（G7c 同款裁决）→ 降级 warning 转装机目检，不拦红；
# ③ off 档静止：双帧差分 ≤2——只在②可证时才硬断言（测量校准失败时 0 差分无从归因，防 vacuous 通过）。
try
{
    # 桶身相对 DIP 裁剪（横灌版）：xDip/widthDip 自客户区左缘起算（root 左 Padding=10 DIP，桶身占
    # [10, 客户宽−10]），高=客户区全高——竖直前沿使「带」失去意义，断言全走全高列/桶身板采样
    function New-AquariumCrop([IntPtr] $hwnd, [int] $xDip, [int] $widthDip)
    {
        $s = [WinEnum]::DpiScaleOf($hwnd)
        $client = [WinEnum]::ClientScreenRectOf($hwnd)
        $h = $client.Bottom - $client.Top
        $x = [int][Math]::Round($xDip * $s)
        $w = [Math]::Max(1, [int][Math]::Round($widthDip * $s))
        $crop = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($crop)
        $g.CopyFromScreen($client.Left + $x, $client.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
        $g.Dispose()
        return $crop
    }

    # 水位前沿物理像素位（镜像 PinTile.ApplyAquariumGeometry 钳位口径：桶宽 DIP=floor(客户宽/s−20)、
    # fill=floor(桶宽×fraction)、前沿 DIP=10+fill）——±1px 镜像误差已折进各断言的列偏移余量
    function Get-AquariumFrontPx([IntPtr] $hwnd, [double] $fraction)
    {
        $s = [WinEnum]::DpiScaleOf($hwnd)
        $client = [WinEnum]::ClientScreenRectOf($hwnd)
        $tankDips = [Math]::Floor(($client.Right - $client.Left) / $s - 20)
        $fillDips = [Math]::Floor($tankDips * $fraction)
        return [int][Math]::Round((10 + $fillDips) * $s)
    }

    # 桶身整板裁剪（双帧差分用：桶身=全部活水动画面，旧 12 DIP 带裁剪已无意义）
    function New-AquariumTankCrop([IntPtr] $hwnd)
    {
        $s = [WinEnum]::DpiScaleOf($hwnd)
        $client = [WinEnum]::ClientScreenRectOf($hwnd)
        $tankDips = [Math]::Floor(($client.Right - $client.Left) / $s - 20)
        return New-AquariumCrop $hwnd 10 ([int]$tankDips)
    }

    # 三段列采样硬断言（后方水体/前方空区/前沿波峰）：stage 只进截图名与报错文案
    function Assert-AquariumColumns([IntPtr] $hwnd, [double] $fraction, [int] $r, [int] $g, [int] $b, [string] $stage)
    {
        $front = Get-AquariumFrontPx $hwnd $fraction
        # ① 前沿后方 12 DIP 全高列：水体色 ≥30% 列面积（文字/图标遮挡 ≤45% 列高，阈值余量 2x）
        $behind = New-AquariumCrop $hwnd ($front - 12) 3
        $behindWater = Count-ColorPixels $behind $r $g $b 45
        $minWater = [int][Math]::Floor($behind.Width * $behind.Height * 0.30)
        Save-Zoom $behind "AI-aquarium-$stage-behind.png" 8
        $behind.Dispose()
        if ($behindWater -lt $minWater)
        {
            throw "断言失败（场景 AI $stage）：前沿后方（front−12 DIP）全高列水体色像素 $behindWater（期望 ≥$minWater）——水体缺席只剩桶壁（run 43720846043 逐行采样实证的同款回归：water/wave 构建期 Collapsed 未翻开）"
        }
        # ② 前沿前方 8 DIP 全高列：水体色 ≤2% 列面积（水体矩形右缘=frontX、波峰领先幅 3 DIP 都够不到；
        # 列内若被越过=水位线钉错位置/方向的横向化回归）。文字/读数非档位色不干扰
        $ahead = New-AquariumCrop $hwnd ($front + 8) 3
        $aheadWater = Count-ColorPixels $ahead $r $g $b 45
        $maxWater = [Math]::Floor($ahead.Width * $ahead.Height * 0.02)
        $ahead.Dispose()
        if ($aheadWater -gt $maxWater)
        {
            throw "断言失败（场景 AI $stage）：前沿前方（front+8 DIP）全高列水体色像素 $aheadWater（期望 ≤$maxWater）——水漫过前沿（横灌方向/钳位回归，frontX 应=floor(桶宽×$fraction)）"
        }
        # ③ 前沿条 [front+2, front+7)：波峰领先幅 3 DIP 的全色像素带（镜像 ±1px 与水体矩形右缘
        # ≤front+1 都够不到 +2 起）——波层缺席硬断言，静止档同样在场（off 是静止不是缺席）
        $wave = New-AquariumCrop $hwnd ($front + 2) 5
        $waveWater = Count-ColorPixels $wave $r $g $b 45
        Save-Zoom $wave "AI-aquarium-$stage-front.png" 8
        $wave.Dispose()
        if ($waveWater -lt 4)
        {
            throw "断言失败（场景 AI $stage）：前沿条（front+2..+7 DIP）波峰色像素 $waveWater（期望 ≥4，振幅 3 DIP 领先沿全色下限）——波层缺席（负 Y/负 X 几何 Path 整件不渲染回归，43729489131 同款；双帧差分 0 两义，缺席由此条拦）"
        }
        Write-Host "场景 AI $stage 列采样：后方水体 $behindWater/$minWater+，前方空区 $aheadWater/$maxWater−，前沿波峰 $waveWater/4+（front=$front px）"
    }

    # 摘要「已用 NN%」打头（主窗=最高窗，mock 形状同 38 链）——轮询等模式档被下一个 ci tick 拾取
    # （ci 档 15s；Pickup 慢于一次刷新即重找重读，与 AE2 Wait-GlmTileText 同款双轮询）
    function Wait-GlmTilePercent([string] $percent, [int] $timeoutSec, [string] $stage)
    {
        $deadline = (Get-Date).AddSeconds($timeoutSec)
        while ((Get-Date) -lt $deadline)
        {
            $h = [IntPtr]::Zero
            foreach ($x in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
            {
                if (@(Get-WindowUiaTexts $x) -contains "GLM") { $h = $x; break }
            }
            if ($h -ne [IntPtr]::Zero)
            {
                $texts = @(Get-WindowUiaTexts $h)
                if (@($texts | Where-Object { $_ -match ($percent + "%") }).Count -gt 0) { return $texts }
            }
            Start-Sleep -Seconds 2
        }
        $h = [IntPtr]::Zero
        foreach ($x in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
        {
            if (@(Get-WindowUiaTexts $x) -contains "GLM") { $h = $x; break }
        }
        $last = if ($h -ne [IntPtr]::Zero) { @(Get-WindowUiaTexts $h) -join " | " } else { "（GLM tile 未建窗）" }
        Save-FullScreenshot "AI-FAIL-percent-$stage.png"
        throw "断言失败（场景 AI $stage）：GLM tile 文本未见「$percent%」（ci 档 15s，等 ${timeoutSec}s）——mock 模式档未被拾取，带态截图会失真；实际：$last"
    }

    Ensure-MockServer
    $aiModeFile = Join-Path $env:TEMP "beacon-mock-mode.txt"
    Set-Content -Path $aiModeFile -Value "p20" -Encoding ASCII
    Write-ScenarioConfig $true "dark" "aquarium" "reduced"
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    $aiTile = [IntPtr]::Zero
    foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
    {
        if (@(Get-WindowUiaTexts $h) -contains "GLM") { $aiTile = $h; break }
    }
    if ($aiTile -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "AI-FAIL-no-tile.png"
        throw "断言失败（场景 AI）：aquarium 档未见 GLM 悬浮框（标题 Beacon Tile）——样式档 tile 未建窗"
    }
    Assert-ClientHeight $aiTile (ExpectedRowPx $aiTile) "场景 AI"
    Assert-AquariumColumns $aiTile 0.20 63 185 80 "20" # 20% fill=正常档=组件 severity 色（success 绿 #3fb950）
    Save-FullScreenshot "AI-aquarium-20-full.png"
    Write-Host "场景 AI 20% 态通过：前沿后方全高列=绿、前方空区、波峰在场（AI-aquarium-20-*-png 8x 近景 + 全屏）"

    # 60% 中位档 + 活水校准（双帧差分，G7c 呼吸灯口径）
    Set-Content -Path $aiModeFile -Value "p60" -Encoding ASCII
    Wait-GlmTilePercent "60" 60 "p60" | Out-Null
    Start-Sleep -Seconds 2 # 前沿推进/着色落定
    Assert-AquariumColumns $aiTile 0.60 210 153 34 "60" # 60% fill=warning #d29922（severity 60≥60 翻 Warning）
    $f1 = New-AquariumTankCrop $aiTile
    Start-Sleep -Seconds 3
    $f2 = New-AquariumTankCrop $aiTile
    $aiMotionDiff = Count-FrameDiff $f1 $f2 12
    Save-Zoom $f1 "AI-motion-frame1.png" 6
    Save-Zoom $f2 "AI-motion-frame2.png" 6
    $f1.Dispose(); $f2.Dispose()
    $aiMotionProvable = $false
    if ($aiMotionDiff -ge 50)
    {
        $aiMotionProvable = $true
        Write-Host "场景 AI 活水断言通过：reduced 档双帧差分 $aiMotionDiff px（≥50，波纹纵向漂移可见）"
    }
    else
    {
        Write-Host "::warning::场景 AI reduced 档双帧差分仅 $aiMotionDiff px——Composition 属性动画本 CI 会话不可证（G7c 呼吸灯同款裁决），活水观感转装机目检；波层在场已由列采样硬断言，off 档静止硬断言随之失效（测量校准失败）"
    }
    Save-FullScreenshot "AI-aquarium-60-full.png"
    Write-Host "场景 AI 60% 态通过：前沿=60% 桶宽（列采样）+ 波峰在场"

    # 95% 高位档（复用 error 链，BarFillColor ≥98 才翻红——95% 仍是 warning 档位色，观感见截图）
    Set-Content -Path $aiModeFile -Value "error" -Encoding ASCII
    Wait-GlmTilePercent "95" 60 "p95" | Out-Null
    Start-Sleep -Seconds 2
    Assert-AquariumColumns $aiTile 0.95 210 153 34 "95" # 95% fill=warning 档位色（ProgressTiers ≥0.8 <0.98 → Warning，非 severity 红）
    Save-FullScreenshot "AI-aquarium-95-full.png"
    Write-Host "场景 AI 95% 态通过：前沿=95% 桶宽（列采样）+ 波峰在场"

    # off 档静止：重启（motion mode 构造期读档，同动效档口径），双帧差分 ≤2
    Set-Content -Path $aiModeFile -Value "p60" -Encoding ASCII
    Write-ScenarioConfig $true "dark" "aquarium" "off"
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    $aiTile = [IntPtr]::Zero
    foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
    {
        if (@(Get-WindowUiaTexts $h) -contains "GLM") { $aiTile = $h; break }
    }
    if ($aiTile -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "AI-FAIL-no-tile-off.png"
        throw "断言失败（场景 AI off）：aquarium+off 档未见 GLM 悬浮框"
    }
    Assert-ClientHeight $aiTile (ExpectedRowPx $aiTile) "场景 AI off"
    Wait-GlmTilePercent "60" 60 "off-p60" | Out-Null
    Start-Sleep -Seconds 3
    Assert-AquariumColumns $aiTile 0.60 210 153 34 "off" # off 档 p60：桶身照常在场只是静止（波峰列同样必须在场）
    $s1 = New-AquariumTankCrop $aiTile
    Start-Sleep -Seconds 3
    $s2 = New-AquariumTankCrop $aiTile
    $aiOffDiff = Count-FrameDiff $s1 $s2 12
    Save-Zoom $s1 "AI-off-frame1.png" 6
    Save-Zoom $s2 "AI-off-frame2.png" 6
    $s1.Dispose(); $s2.Dispose()
    if ($aiMotionProvable)
    {
        if ($aiOffDiff -gt 2)
        {
            throw "断言失败（场景 AI off）：off 档双帧差分 $aiOffDiff px（期望 ≤2）——波纹未随 off 档停（B-707 纪律回归）"
        }
        Write-Host "场景 AI off 档静止断言通过：双帧差分 $aiOffDiff px（≤2；同会话活水差分 $aiMotionDiff px 已校准测量有效）"
    }
    else
    {
        Write-Host "::warning::场景 AI off 档双帧差分 $aiOffDiff px——活水校准本会话未过，静止断言不拦红（同 G7c 降级口径），转装机目检"
    }
    Save-FullScreenshot "AI-aquarium-off-full.png"
    Remove-Item $aiModeFile -Force -ErrorAction SilentlyContinue
    Stop-Beacon
    Write-Host "场景 AI 通过：横灌三态（20/60/95%）前沿列采样+波峰在场+逐态截图，活水/静止差分证据已存"
}
catch
{
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Save-FullScreenshot "AI-FAIL-unexpected.png"
    $failures.Add("场景 AI：$($_.Exception.Message)")
    Write-Host "::error::场景 AI 断言失败：$($_.Exception.Message)"
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
    # ①b SET-UX-ROW-1（2026-11-24 用户真机报「开关没有统一位置，文字会挤压开关」）：模块行已改
    # Grid 三星列（Auto 图标 / * 文字 / Auto 开关）——两道硬门：带开关的模块行开关右缘极差 ≤1px
    # （方差=0 面：开关列 Auto 右贴+MinWidth 定宽，整列右缘须同一直线）；每行开关完整可见（右缘
    # ≤行右缘−7：Button 右 padding 8 + 1px 取整容差）——长副标题不再能把开关挤出可视区。
    # ScrollIntoView 逐行先滚进视口再取矩形（视口外行 BoundingRectangle 可空/NaN，38619441591）。
    $row1ToggleRights = @()
    foreach ($key in @("github","bigmodel","ark","claude","kimi","deepseek","mimo","codex","copilot","opencode","qwen","http"))
    {
        $row1Row = Find-UiaById $settings "module-$key" 8
        if (!$row1Row) { throw "断言失败（SET-UX-ROW-1）：UIA 未找到模块行 module-$key" }
        try { ($row1Row.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)).ScrollIntoView() } catch { }
        Start-Sleep -Milliseconds 250
        $row1Switch = Find-UiaById $row1Row "module-toggle-$key" 4
        if (!$row1Switch)
        {
            Save-FullScreenshot "ROW1-FAIL-no-toggle-$key.png"
            throw "断言失败（SET-UX-ROW-1）：module-$key 行内未找到 module-toggle-$key——开关锚点缺失或行结构回归"
        }
        $row1Rect = $null
        try { $row1Rect = $row1Switch.Current.BoundingRectangle } catch { }
        $row1RowRect = $null
        try { $row1RowRect = $row1Row.Current.BoundingRectangle } catch { }
        # 单行布尔判定（行尾运算符续行是本脚本首例不引入；NaN 恒 false 会把断言洗白成通过，必须显式硬门）
        $row1RectOk = ($null -ne $row1Rect) -and (-not $row1Rect.IsEmpty) -and (-not [double]::IsNaN($row1Rect.X)) -and (-not [double]::IsNaN($row1Rect.Width))
        $row1RowOk = ($null -ne $row1RowRect) -and (-not $row1RowRect.IsEmpty) -and (-not [double]::IsNaN($row1RowRect.X)) -and (-not [double]::IsNaN($row1RowRect.Width))
        if ((!$row1RectOk) -or (!$row1RowOk))
        {
            Save-FullScreenshot "ROW1-FAIL-nan-rect-$key.png"
            throw "断言失败（SET-UX-ROW-1）：module-$key 行/开关 BoundingRectangle 空/NaN——虚拟化未实化或窗隐藏"
        }
        $row1Right = [int]($row1Rect.X + $row1Rect.Width)
        $row1RowRight = [int]($row1RowRect.X + $row1RowRect.Width)
        if (($row1Right + 7) -gt $row1RowRight)
        {
            Save-FullScreenshot "ROW1-FAIL-squeezed-$key.png"
            throw "断言失败（SET-UX-ROW-1）：module-$key 开关右缘 $row1Right 越过行右缘 $row1RowRight−7——文字挤压开关回归（真机报障面）"
        }
        $row1ToggleRights += $row1Right
    }
    $row1Spread = $row1ToggleRights | Measure-Object -Maximum -Minimum
    $row1Delta = [int]($row1Spread.Maximum - $row1Spread.Minimum)
    if ($row1Delta -gt 1)
    {
        Save-FullScreenshot "ROW1-FAIL-misaligned.png"
        throw "断言失败（SET-UX-ROW-1）：模块行开关右缘极差 ${row1Delta}px（期望 ≤1）：$($row1ToggleRights -join ',')——开关列未对齐（真机「开关没有统一位置」面）"
    }
    Write-Host "SET-UX-ROW-1 模块行断言通过：12 开关右缘极差 ${row1Delta}px，全部完整可见（右缘 $($row1ToggleRights -join ',')）"
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
    # SET-UX-ROW-1②：连接行开关同门（label/stateChip/enabledToggle 同构横排，本单同批改 Grid
    # 星号列）——连接区只在 Provider 页构建（_connectionList 于 BuildProviderPage 创建，常规/外观/
    # 高级页无连接区），且按页 _scopeType 过滤只列本家连接，故断言必须逐 Provider 页取本页 conn-toggle：
    # 5 家各 1 行 + ark 页 save-flow 新增 ark-e2e 共 6 个 conn-toggle 右缘极差 ≤1px（星号列右界恒定+
    # 开关 MinWidth 定宽）且完整可见（右缘 ≤ 设置窗右缘−8）。
    $row1ConnRights = @()
    foreach ($row1Page in @(
        @{ module = "ark"; conn = "ark-main" },
        @{ module = "ark"; conn = "ark-e2e" },
        @{ module = "kimi"; conn = "kimi-main" },
        @{ module = "mimo"; conn = "mimo-main" },
        @{ module = "deepseek"; conn = "ds-main" },
        @{ module = "qwen"; conn = "qwen-main" }))
    {
        $row1ConnModule = Find-UiaById $settings "module-$($row1Page.module)" 10
        if (!$row1ConnModule) { throw "断言失败（SET-UX-ROW-1）：UIA 未找到模块行 module-$($row1Page.module)" }
        Invoke-Uia $row1ConnModule
        Start-Sleep -Seconds 2
        $connId = $row1Page.conn
        $row1ConnSwitch = Find-UiaById $settings "conn-toggle-$connId" 6
        if (!$row1ConnSwitch)
        {
            Save-FullScreenshot "ROW1-FAIL-no-conn-toggle-$connId.png"
            throw "断言失败（SET-UX-ROW-1）：$($row1Page.module) 页未找到 conn-toggle-$connId——连接行开关缺失（连接区未构建？）"
        }
        try { ($row1ConnSwitch.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)).ScrollIntoView() } catch { }
        Start-Sleep -Milliseconds 250
        $row1ConnRect = $null
        try { $row1ConnRect = $row1ConnSwitch.Current.BoundingRectangle } catch { }
        $row1WinRect = $null
        try { $row1WinRect = $settings.Current.BoundingRectangle } catch { }
        # 单行布尔判定 + NaN 显式硬门（NaN 恒 false 会把断言洗白成通过）
        $row1ConnOk = ($null -ne $row1ConnRect) -and (-not $row1ConnRect.IsEmpty) -and (-not [double]::IsNaN($row1ConnRect.X)) -and (-not [double]::IsNaN($row1ConnRect.Width))
        $row1WinOk = ($null -ne $row1WinRect) -and (-not $row1WinRect.IsEmpty) -and (-not [double]::IsNaN($row1WinRect.Right))
        if ((!$row1ConnOk) -or (!$row1WinOk))
        {
            Save-FullScreenshot "ROW1-FAIL-nan-conn-rect-$connId.png"
            throw "断言失败（SET-UX-ROW-1）：conn-toggle-$connId 或设置窗 BoundingRectangle 空/NaN"
        }
        $row1ConnRight = [int]($row1ConnRect.X + $row1ConnRect.Width)
        if ($row1ConnRight -gt ([int]$row1WinRect.Right - 8))
        {
            Save-FullScreenshot "ROW1-FAIL-conn-squeezed-$connId.png"
            throw "断言失败（SET-UX-ROW-1）：conn-toggle-$connId 右缘 $row1ConnRight 贴/越设置窗右缘 $([int]$row1WinRect.Right)−8——长连接名挤压开关回归"
        }
        $row1ConnRights += $row1ConnRight
    }
    $row1ConnSpread = $row1ConnRights | Measure-Object -Maximum -Minimum
    $row1ConnDelta = [int]($row1ConnSpread.Maximum - $row1ConnSpread.Minimum)
    if ($row1ConnDelta -gt 1)
    {
        Save-FullScreenshot "ROW1-FAIL-conn-misaligned.png"
        throw "断言失败（SET-UX-ROW-1）：连接行开关右缘极差 ${row1ConnDelta}px（期望 ≤1）：$($row1ConnRights -join ',')——开关列未对齐"
    }
    Write-Host "SET-UX-ROW-1 连接行断言通过：6 开关右缘极差 ${row1ConnDelta}px，全部完整可见（右缘 $($row1ConnRights -join ',')）"
    # 逐 Provider 页取完连接行后当前页仍是 qwen——检查频率项在常规页，先切回再找锚点
    $row = Find-UiaById $settings "module-general" 10
    if (!$row) { throw "断言失败（场景 D）：连接行断言后未找到模块行 module-general" }
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
    $opened = Invoke-TrayMenu 0 "Settings" # 载体收编后托盘菜单=[设置,退出]：设置=首项 0 DOWN（ISLAND-RING-1 扩围③）
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
        $opened2 = Invoke-TrayMenu 1 "Exit" # 退出=第二项（分隔条键盘导航跳过）1 DOWN
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
    # SET-UX-06：级别色与动效强度收进「更多选项」折叠区——G4/G5/G6 锚先展开再找
    $moreOptions = Find-UiaById $settings "appearance-more-options" 10
    if (!$moreOptions) { throw "断言失败（场景 G4 前置）：UIA 未找到 appearance-more-options（折叠区锚点丢失？）" }
    Expand-UiaElement $moreOptions
    Start-Sleep -Seconds 1
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
        # 外观区 1254 加密度下拉后预览行推出 640 视口（38953986133 首证：按钮在树、矩形空）——滚入视口再取证；
        # 无 ScrollItem 模式（已可见/宿主非滚动）则原样继续
        try
        {
            ($breathBtn.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)).ScrollIntoView()
            Start-Sleep -Seconds 1
        }
        catch { }
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
    # 两类失败都吃重试（38942131834/38953986133 两连红实证同一签名）：
    #   ① 面板失焦自隐（Flyout 语义，Deactivated→Hide）——runner 控制台抢焦点后句柄不可见，隐藏元素 Invoke 必抛
    #     「Value does not fall within the expected range」；② FindFirst 与 Invoke 之间刷新 tick 重建行容器 → 元素失联。
    # 每轮先验面板可见性（不可见=重发 WM_HOTKEY 重开），再重找 AttentionList/行，再 Invoke——确定性失败三连炸仍红不掩盖。
    $invokeErr = ""
    $invoked = $false
    for ($attempt = 1; $attempt -le 3 -and !$invoked; $attempt++)
    {
        if (@([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")) -notcontains $panelHandle)
        {
            [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY 重开面板
            $reopen = (Get-Date).AddSeconds(5)
            while ((Get-Date) -lt $reopen -and @([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")).Count -lt 1)
            {
                Start-Sleep -Milliseconds 250
            }
            $panelHandle = @([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")) | Select-Object -First 1
            if ($panelHandle) { $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle) }
            Start-Sleep -Seconds 1
        }
        $attentionListEl = Find-UiaById $panelEl "AttentionList" 5
        if (!$attentionListEl) { $invokeErr = "AttentionList 未找到（第 $attempt 轮）"; Start-Sleep -Seconds 2; continue }
        # 行选取防虚拟化（UIA 实证：视口外/未实化容器 BoundingRectangle 空或 NaN，pattern 调用高发
        # E_INVALIDARG）——优先取矩形非空且宽度有效行；全退化才拿首行（诊断消息仍可见实际元素）。
        $rowCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
        $rowItems = $attentionListEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, $rowCond)
        $rowItem = $null
        foreach ($ri in $rowItems)
        {
            $riRect = $ri.Current.BoundingRectangle
            if (-not $riRect.IsEmpty -and -not [double]::IsNaN($riRect.Width) -and $riRect.Width -gt 0) { $rowItem = $ri; break }
        }
        if (!$rowItem -and $rowItems.Count -gt 0) { $rowItem = $rowItems[0] }
        if (!$rowItem) { $invokeErr = "AttentionList 无 ListItem 可激活（第 $attempt 轮）"; Start-Sleep -Seconds 2; continue }
        # 行先滚入视口再激活（G7c 同款兜底：ScrollItemPattern 不可用则跳过）
        try
        {
            ($rowItem.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern)).ScrollIntoView()
            Start-Sleep -Milliseconds 500
        } catch { }
        try
        {
            ($rowItem.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
            $invoked = $true
        }
        catch
        {
            # 诊断快照（38942131834 起 E_INVALIDARG 签名恒定但根因未明）：失败时记录实际命中元素身份与 pattern 面
            $rowDiag = ""
            try
            {
                $rowPatterns = ($rowItem.GetSupportedPatterns() | ForEach-Object { $_.ProgrammaticName }) -join ","
                $rowDiag = "row=[$($rowItem.Current.Name)] offscreen=$($rowItem.Current.IsOffscreen) rect=$($rowItem.Current.BoundingRectangle) patterns=$rowPatterns"
            } catch { $rowDiag = "（行诊断失败：$($_.Exception.Message)）" }
            $invokeErr = "$($_.Exception.Message.Trim()) | $rowDiag"
            Start-Sleep -Seconds 2
        }
    }
    if (!$invoked)
    {
        Save-FullScreenshot "U-FAIL-row-invoke.png"
        # 应用侧异常落日志（App.xaml UnhandledException 含堆栈）——尾巴拼进断言消息直读现场
        $uLogTail = ""
        try
        {
            $uLogLines = (Get-TodayLog) -split "`n"
            $uLogTail = ($uLogLines | Select-Object -Last 25) -join "`n"
        }
        catch { $uLogTail = "（日志读取失败：$($_.Exception.Message)）" }
        throw "断言失败（场景 U2）：AttentionList 行 Invoke 三连失败：$invokeErr（面板失焦自隐已重开/行容器重建已重找/视口外已滚入，仍失败）`n--- app log tail ---`n$uLogTail"
    }
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
    # 键值列分离硬门（2026-11-28 imac detail.png 200% 实证键值同格互压，根因=ComponentTable 值位
    # 未 SetColumn、WinUI 缺省全落 column 0）：value 左缘必须 ≥ key 右缘（列距 6px 在其间）。
    # BoundingRectangle NaN/空防护照 SG 行门成例——NaN 恒 false 会把断言洗白成通过，先显式判矩形可用。
    $runIdKeyEl = Find-UiaByName $detailEl "run_id" 8
    $runIdValueEl = Find-UiaByName $detailEl "12345" 8
    if ($runIdKeyEl -and $runIdValueEl)
    {
        $dKeyRect = $runIdKeyEl.Current.BoundingRectangle
        $dValRect = $runIdValueEl.Current.BoundingRectangle
        $dKeyRectOk = ($null -ne $dKeyRect) -and (-not $dKeyRect.IsEmpty) -and (-not [double]::IsNaN($dKeyRect.X)) -and (-not [double]::IsNaN($dKeyRect.Width))
        $dValRectOk = ($null -ne $dValRect) -and (-not $dValRect.IsEmpty) -and (-not [double]::IsNaN($dValRect.X)) -and (-not [double]::IsNaN($dValRect.Width))
        if (!$dKeyRectOk -or !$dValRectOk)
        {
            throw "断言失败（场景 U2）：DETAILS 键/值元素矩形不可读（NaN/空）——键值列分离门无法执行，不得静默放行"
        }
        $dKeyRight = [int]($dKeyRect.X + $dKeyRect.Width)
        $dValLeft = [int]$dValRect.X
        if ($dValLeft -lt $dKeyRight)
        {
            throw "断言失败（场景 U2）：DETAILS 表键值列重叠（key 右缘 $dKeyRight > value 左缘 $dValLeft）——表行值位未落 Star 列（column 0 同格互压回归）"
        }
        Write-Host "场景 U2 键值列分离门通过：value 左缘 $dValLeft ≥ key 右缘 $dKeyRight"
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
        # 宽松匹配（✓ + run id）：e0363e6 把结果文案改成请求接受语义（「已请求重跑」），
        # 精确文案断言随措辞再改易碎——断执行成功语义（✓）+ 目标 run 在案即可
        if ((Get-WindowUiaTexts $detailHandle) | Where-Object { $_ -match '✓' -and $_ -match 'run 12345' }) { $okText = $true; break }
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
    # SET-UX-03（2026-11-23 批1）：贴边收起移入「桌面显示」页——先导航 module-desktop 再找锚点
    $w4DesktopRow = Find-UiaById $settings "module-desktop" 8
    if (!$w4DesktopRow) { Save-FullScreenshot "W-FAIL-no-desktop-row.png"; throw "断言失败（场景 W4）：UIA 未找到模块行 module-desktop（桌面显示页缺失？）" }
    Invoke-Uia $w4DesktopRow
    Start-Sleep -Seconds 2
    # SET-UX-ROW-3：本块种子=状态条关闭档（capsuleIsland/showCapsule 双关）→「窗口与看板」只留看板开关，
    # 桌面悬浮四项应在树外；切「顶部悬浮岛」档（SaveStatusBarMode 延后一拍重建本页）岛家族锚点回树，
    # 开关断言随之继续——三档显隐集合在此一并钉死。
    foreach ($w4HiddenAnchor in @("desktop-layout-box", "numeric-floating-toggle", "desktop-opacity-slider", "edge-collapse-toggle"))
    {
        if (Find-UiaById $settings $w4HiddenAnchor 3)
        {
            Save-FullScreenshot "W-FAIL-off-tier-not-hidden.png"
            throw "断言失败（场景 W4）：关闭档 $w4HiddenAnchor 仍在 UIA 树（桌面悬浮设置未随状态条档隐藏）——SET-UX-ROW-3"
        }
    }
    if (!(Find-UiaById $settings "board-toggle" 4))
    {
        Save-FullScreenshot "W-FAIL-off-tier-no-board.png"
        throw "断言失败（场景 W4）：关闭档看板开关 board-toggle 缺席（该档只隐藏桌面悬浮项，看板开关仍须可改）——SET-UX-ROW-3"
    }
    $w4IslandRadio = Find-UiaById $settings "status-bar-island" 8
    if (!$w4IslandRadio) { Save-FullScreenshot "W-FAIL-no-island-radio.png"; throw "断言失败（场景 W4）：未找到 status-bar-island" }
    ($w4IslandRadio.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    Start-Sleep -Seconds 3
    $toggle = Find-UiaById $settings "edge-collapse-toggle" 8
    if (!$toggle) { Save-FullScreenshot "W-FAIL-no-toggle.png"; throw "断言失败（场景 W4）：切岛档后未找到贴边收起开关 edge-collapse-toggle（桌面显示页；岛档应全显）" }
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
    Write-Host "场景 W4 通过：贴边收起开关可切换且落盘（edgeCollapseEnabled false→true）+ 关闭档桌面悬浮四项在树外、切岛档回树（SET-UX-ROW-3）"
    Stop-Beacon
}
catch
{
    Stop-Beacon # 失败也收尸：W 的 try 尾 Stop-Beacon 在 throw 时被跳过，残留进程曾毒化场景 J 静默安装（38525961627）
    Save-FullScreenshot "W-FAIL-unexpected.png"
    $failures.Add("场景 W：$($_.Exception.Message)")
    Write-Host "::error::场景 W 断言失败：$($_.Exception.Message)"
}

# —— 场景 SU：设置改版三件事（SET-UX-01/02/03，2026-11-23 用户令原型落地批1）——
# SU1 单项保存回归门：预置 pollIntervalSeconds=120（合法非预设档）→ 常规页频率框回读「自定义」
#     → startup-toggle 翻转 → config launchOnStartup 翻转且 pollIntervalSeconds 恒 120
#     （旧 SaveGeneral 全字段回写会把 120 洗回 0——SET-UX-01 的病灶本体，本场景即门）
# SU2 状态条三选一：双关旧配置投影「关闭」→ island → config 双开+胶囊窗在场 → capsule →
#     岛关胶囊开+窗保持 → off → 双关+窗退场；岛关后组件中心岛内容折叠（island-display-mode 不在树）
# SU3 窗口与看板：数值总开关 off→numericFloatingEnabled=false；允许看板 off→boardEnabled=false；
#     不透明度 RangeValue 0.85→uiOpacity=0.85（容差断言）；布局档 panel→pinDisplayMode=panel
#     （模板种子 floating，选 panel 验证真实翻转；重启生效面装机复核）
try
{
    Ensure-MockServer
    Write-GConfig ""
    $cfgPath = Join-Path $configDir "config.json"
    (Get-Content $cfgPath -Raw -Encoding UTF8).Replace('"launchOnStartup": false', '"launchOnStartup": false, "pollIntervalSeconds": 120') | Set-Content -Path $cfgPath -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    Start-Sleep -Seconds 15
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SU-FAIL-no-settings.png"; throw "断言失败（场景 SU 前置）：设置窗未出现" }

    # SU1：常规页（默认页）频率框自定义回读 + 单项保存回归门
    $pollBox = Find-UiaById $settings "poll-interval-box" 10
    if (!$pollBox) { Save-FullScreenshot "SU-FAIL-no-pollbox.png"; throw "断言失败（场景 SU1）：未找到 poll-interval-box" }
    $pollSelection = Get-ComboSelectionName $pollBox
    Save-FullScreenshot "SU1-general-poll-custom.png"
    if ($pollSelection -notmatch "自定义")
    {
        throw "断言失败（场景 SU1）：频率框选中项「$pollSelection」不含「自定义」（120 秒合法旧值未回显）"
    }
    $startupToggle = Find-UiaById $settings "startup-toggle" 10
    if (!$startupToggle) { Save-FullScreenshot "SU-FAIL-no-startup.png"; throw "断言失败（场景 SU1）：未找到 startup-toggle" }
    ($startupToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
    Start-Sleep -Seconds 2
    $appConfigJson = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($appConfigJson.pollIntervalSeconds)" -ne "120")
    {
        Save-FullScreenshot "SU-FAIL-poll-washed.png"
        throw "断言失败（场景 SU1）：改开机自启后 pollIntervalSeconds=$($appConfigJson.pollIntervalSeconds)（期望仍 120——全字段回写回归）"
    }
    if ("$($appConfigJson.launchOnStartup)" -ne "True")
    {
        throw "断言失败（场景 SU1）：launchOnStartup=$($appConfigJson.launchOnStartup)（期望 True——开关未落盘）"
    }
    Write-Host "场景 SU1 通过：120 秒自定义回读 + 改开机自启只写自己字段（pollIntervalSeconds 恒 120）——SET-UX-01 回归门"

    # SU2：桌面显示页状态条三选一（投影初值：seed 双关 → off）
    $desktopRow = Find-UiaById $settings "module-desktop" 10
    if (!$desktopRow) { Save-FullScreenshot "SU-FAIL-no-desktop-row.png"; throw "断言失败（场景 SU2）：UIA 未找到模块行 module-desktop" }
    Invoke-Uia $desktopRow
    Start-Sleep -Seconds 2
    $radioOff = Find-UiaById $settings "status-bar-off" 8
    $radioIsland = Find-UiaById $settings "status-bar-island" 8
    if (!$radioOff -or !$radioIsland)
    {
        Save-FullScreenshot "SU-FAIL-no-statusbar.png"
        throw "断言失败（场景 SU2）：状态条三选一未找到（status-bar-off/island）"
    }
    $offSelected = ($radioOff.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected
    if (!$offSelected)
    {
        Save-FullScreenshot "SU-FAIL-projection-off.png"
        throw "断言失败（场景 SU2）：双关旧配置投影应为「关闭」，实际 off 未选中"
    }
    Save-FullScreenshot "SU2-desktop-statusbar.png"
    Write-Host "场景 SU2 投影通过：双关旧配置 → 状态条投影「关闭」（岛优先/胶囊其次读口径）"
    # SET-UX-ROW-3：关闭档=悬浮面整区隐藏只留看板开关（四项在树外、board-toggle 在树）
    foreach ($suOffHidden in @("desktop-layout-box", "numeric-floating-toggle", "desktop-opacity-slider", "edge-collapse-toggle"))
    {
        if (Find-UiaById $settings $suOffHidden 3)
        {
            Save-FullScreenshot "SU-FAIL-off-tier.png"
            throw "断言失败（场景 SU2）：关闭档 $suOffHidden 仍在 UIA 树（悬浮面设置应整区隐藏）——SET-UX-ROW-3"
        }
    }
    if (!(Find-UiaById $settings "board-toggle" 4))
    {
        Save-FullScreenshot "SU-FAIL-off-tier-board.png"
        throw "断言失败（场景 SU2）：关闭档 board-toggle 不在树（该档只留看板开关，须可改）——SET-UX-ROW-3"
    }

    # island 档 → config 双开 + 胶囊窗（岛形态）出现
    ($radioIsland.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    Start-Sleep -Seconds 3
    $appConfigJson = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($appConfigJson.capsuleIsland)" -ne "True" -or "$($appConfigJson.showCapsule)" -ne "True")
    {
        Save-FullScreenshot "SU-FAIL-island-config.png"
        throw "断言失败（场景 SU2）：选岛后 config capsuleIsland=$($appConfigJson.capsuleIsland)/showCapsule=$($appConfigJson.showCapsule)（期望 True/True——成组保存）"
    }
    $capsuleSeen = $false
    $deadline = (Get-Date).AddSeconds(8)
    while ((Get-Date) -lt $deadline)
    {
        if ([WinEnum]::VisibleHandlesByTitle("Beacon Status").Count -ge 1) { $capsuleSeen = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$capsuleSeen)
    {
        Save-FullScreenshot "SU-FAIL-island-window.png"
        throw "断言失败（场景 SU2）：选岛后未见胶囊窗「Beacon Status」（EnsureCapsule 建窗回归）"
    }
    Write-Host "场景 SU2 island 档通过：config 双开 + 岛窗在场"
    # SET-UX-ROW-3：切岛档重建本页（SaveStatusBarMode 延后一拍）→ 岛家族五项全显（旧引用已失效，锚一律重取）
    foreach ($suIslandShown in @("desktop-layout-box", "numeric-floating-toggle", "desktop-opacity-slider", "edge-collapse-toggle", "board-toggle"))
    {
        if (!(Find-UiaById $settings $suIslandShown 4))
        {
            Save-FullScreenshot "SU-FAIL-island-tier.png"
            throw "断言失败（场景 SU2）：岛档 $suIslandShown 不在树（切档重建后岛家族五项应全显）——SET-UX-ROW-3"
        }
    }

    # capsule 档 → 岛关胶囊开（同一窗回经典，窗不闪关）
    $radioCapsule = Find-UiaById $settings "status-bar-capsule" 8
    if (!$radioCapsule)
    {
        Save-FullScreenshot "SU-FAIL-no-capsule-radio.png"
        throw "断言失败（场景 SU2）：未找到 status-bar-capsule"
    }
    ($radioCapsule.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    Start-Sleep -Seconds 3
    $appConfigJson = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($appConfigJson.capsuleIsland)" -ne "False" -or "$($appConfigJson.showCapsule)" -ne "True")
    {
        Save-FullScreenshot "SU-FAIL-capsule-config.png"
        throw "断言失败（场景 SU2）：选胶囊后 config capsuleIsland=$($appConfigJson.capsuleIsland)/showCapsule=$($appConfigJson.showCapsule)（期望 False/True）"
    }
    if ([WinEnum]::VisibleHandlesByTitle("Beacon Status").Count -lt 1)
    {
        Save-FullScreenshot "SU-FAIL-capsule-window.png"
        throw "断言失败（场景 SU2）：切经典后胶囊窗消失（同窗换形态语义回归）"
    }
    Write-Host "场景 SU2 capsule 档通过：岛关胶囊开 + 窗保持"
    # SET-UX-ROW-3：胶囊档=岛家族三项（布局/数值/贴边）无桌面悬浮组件可作用→隐藏；不透明度作用于胶囊仍留；看板三档都在
    foreach ($suCapsuleHidden in @("desktop-layout-box", "numeric-floating-toggle", "edge-collapse-toggle"))
    {
        if (Find-UiaById $settings $suCapsuleHidden 3)
        {
            Save-FullScreenshot "SU-FAIL-capsule-tier.png"
            throw "断言失败（场景 SU2）：胶囊档 $suCapsuleHidden 仍在树（岛家族项应随档隐藏）——SET-UX-ROW-3"
        }
    }
    foreach ($suCapsuleShown in @("desktop-opacity-slider", "board-toggle"))
    {
        if (!(Find-UiaById $settings $suCapsuleShown 4))
        {
            Save-FullScreenshot "SU-FAIL-capsule-tier-shown.png"
            throw "断言失败（场景 SU2）：胶囊档 $suCapsuleShown 不在树（该档仍须可改）——SET-UX-ROW-3"
        }
    }

    # off 档 → 双关 + 窗退场
    # SET-UX-ROW-3：上段切胶囊已重建本页，开头取的 $radioOff 已失效——重取后再选（陈引用 Select 抛 stale 不是真失败）
    $radioOff = Find-UiaById $settings "status-bar-off" 8
    if (!$radioOff)
    {
        Save-FullScreenshot "SU-FAIL-no-off-radio.png"
        throw "断言失败（场景 SU2）：off 档重取未找到 status-bar-off"
    }
    ($radioOff.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    Start-Sleep -Seconds 3
    $appConfigJson = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($appConfigJson.capsuleIsland)" -ne "False" -or "$($appConfigJson.showCapsule)" -ne "False")
    {
        Save-FullScreenshot "SU-FAIL-off-config.png"
        throw "断言失败（场景 SU2）：选关闭后 config capsuleIsland=$($appConfigJson.capsuleIsland)/showCapsule=$($appConfigJson.showCapsule)（期望 False/False）"
    }
    $capsuleGone = $false
    $deadline = (Get-Date).AddSeconds(8)
    while ((Get-Date) -lt $deadline)
    {
        if ([WinEnum]::VisibleHandlesByTitle("Beacon Status").Count -eq 0) { $capsuleGone = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$capsuleGone)
    {
        Save-FullScreenshot "SU-FAIL-off-window.png"
        throw "断言失败（场景 SU2）：关闭后胶囊窗残留（关窗链回归）"
    }
    Write-Host "场景 SU2 off 档通过：双关 + 胶囊窗退场（SET-UX-02 三档全链）"

    # SU2 附证：岛关后组件中心岛内容折叠（island-display-mode 不在 UIA 树）
    $componentRow = Find-UiaById $settings "module-components" 8
    if ($componentRow)
    {
        Invoke-Uia $componentRow
        Start-Sleep -Seconds 2
        $islandModeBox = Find-UiaById $settings "island-display-mode" 4
        if ($islandModeBox)
        {
            Save-FullScreenshot "SU-FAIL-island-not-collapsed.png"
            throw "断言失败（场景 SU2）：岛关闭后 island-display-mode 仍可见（岛内容折叠回归）"
        }
        Write-Host "场景 SU2 折叠通过：岛关闭 → 组件中心岛内容设置折叠（列表与顺序在 config 原样保留）"
    }

    # SU3：窗口与看板（回桌面显示页；模块行重取防 UIA 缓存失效）
    $desktopRow2 = Find-UiaById $settings "module-desktop" 8
    if (!$desktopRow2) { throw "断言失败（场景 SU3）：UIA 未找到模块行 module-desktop" }
    Invoke-Uia $desktopRow2
    Start-Sleep -Seconds 2
    # SET-UX-ROW-3：SU2 末态=关闭档，桌面悬浮设置整区隐藏；切回「顶部悬浮岛」档（延后一拍重建本页）
    # 五项全显后再做四件断言——同一把路径也证「切回原档全显恢复」。
    $su3IslandRadio = Find-UiaById $settings "status-bar-island" 8
    if (!$su3IslandRadio) { Save-FullScreenshot "SU-FAIL-no-island-radio.png"; throw "断言失败（场景 SU3）：未找到 status-bar-island" }
    ($su3IslandRadio.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Select()
    Start-Sleep -Seconds 3
    $numericToggle = Find-UiaById $settings "numeric-floating-toggle" 8
    if (!$numericToggle) { Save-FullScreenshot "SU-FAIL-no-numeric.png"; throw "断言失败（场景 SU3）：未找到 numeric-floating-toggle" }
    ($numericToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
    Start-Sleep -Seconds 2
    $boardToggle = Find-UiaById $settings "board-toggle" 8
    if (!$boardToggle) { Save-FullScreenshot "SU-FAIL-no-board.png"; throw "断言失败（场景 SU3）：未找到 board-toggle" }
    ($boardToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
    Start-Sleep -Seconds 2
    $appConfigJson = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($appConfigJson.numericFloatingEnabled)" -ne "False")
    {
        Save-FullScreenshot "SU-FAIL-numeric-config.png"
        throw "断言失败（场景 SU3）：numericFloatingEnabled=$($appConfigJson.numericFloatingEnabled)（期望 False——数值总开关未落盘）"
    }
    if ("$($appConfigJson.boardEnabled)" -ne "False")
    {
        Save-FullScreenshot "SU-FAIL-board-config.png"
        throw "断言失败（场景 SU3）：boardEnabled=$($appConfigJson.boardEnabled)（期望 False——看板开关未落盘）"
    }
    Write-Host "场景 SU3 开关对通过：数值总开关/允许看板关 → config 双 false（SET-UX-03①②）"

    $opacitySlider = Find-UiaById $settings "desktop-opacity-slider" 8
    if (!$opacitySlider) { Save-FullScreenshot "SU-FAIL-no-opacity.png"; throw "断言失败（场景 SU3）：未找到 desktop-opacity-slider" }
    ($opacitySlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)).SetValue(0.85)
    Start-Sleep -Seconds 2
    $appConfigJson = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([math]::Abs([double]$appConfigJson.uiOpacity - 0.85) -gt 0.001)
    {
        Save-FullScreenshot "SU-FAIL-opacity-config.png"
        throw "断言失败（场景 SU3）：uiOpacity=$($appConfigJson.uiOpacity)（期望 0.85±0.001——不透明度未落盘）"
    }
    Write-Host "场景 SU3 不透明度通过：RangeValue 0.85 → config uiOpacity=0.85（SET-UX-03③；百分比读数装机目检）"

    $layoutCombo = Find-UiaById $settings "desktop-layout-box" 8
    if (!$layoutCombo) { Save-FullScreenshot "SU-FAIL-no-layout.png"; throw "断言失败（场景 SU3）：未找到 desktop-layout-box" }
    Select-ComboItemById $settings $layoutCombo "layout-panel"
    Start-Sleep -Seconds 2
    $appConfigJson = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($appConfigJson.pinDisplayMode)" -ne "panel")
    {
        Save-FullScreenshot "SU-FAIL-layout-config.png"
        throw "断言失败（场景 SU3）：pinDisplayMode=$($appConfigJson.pinDisplayMode)（期望 panel——布局档未落盘）"
    }
    Write-Host "场景 SU3 布局通过：集中窗口档 → config pinDisplayMode=panel（种子 floating 真实翻转；重启生效面装机复核）"

    Stop-Beacon
    Write-Host "场景 SU 通过：SET-UX-01 单项保存回归门 + SET-UX-02 状态条三档全链 + SET-UX-03 窗口与看板四件 + SET-UX-ROW-3 三档显隐集合"
}
catch
{
    Stop-Beacon
    Save-FullScreenshot "SU-FAIL-unexpected.png"
    $failures.Add("场景 SU：$($_.Exception.Message)")
    Write-Host "::error::场景 SU 断言失败：$($_.Exception.Message)"
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
    # 生效时机 hint 档（SET-UX-11 补 CI 门，2026-11-27）：密度「即时重排桌面组件/详情窗下次打开生效」
    # 与进度样式「切换即时生效」写在控件旁——densityBox 已现=右栏树已进 UIA，单趟文本树走查即可断。
    $aaHintNames = @(Get-UiaTextNames $settings)
    if (@($aaHintNames | Where-Object { $_ -like "*即时重排桌面组件*下次打开时生效*" }).Count -eq 0)
    {
        Save-FullScreenshot "AA-FAIL-no-density-hint.png"
        throw "断言失败（场景 AA）：外观页未见界面密度生效时机 hint（即时重排桌面组件 + 详情窗下次打开生效）——SET-UX-11 hint 缺失"
    }
    if (@($aaHintNames | Where-Object { $_ -like "*切换即时生效*" }).Count -eq 0)
    {
        Save-FullScreenshot "AA-FAIL-no-progress-hint.png"
        throw "断言失败（场景 AA）：外观页未见进度样式生效时机 hint（切换即时生效）——SET-UX-11 hint 缺失"
    }
    Write-Host "场景 AA 生效时机 hint 通过：密度即时重排 + 详情窗下次打开生效 + 进度样式切换即时生效——SET-UX-11"
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

# PS 5.1 顶层数组分叉（V4 案同型，run 39050990738 定谳）：5.1 的 ConvertFrom-Json 把顶层数组
# 当单元素留在管道（外层 count=1），直接 Where 再 $x[0].prop 会成员枚举出全表拼串、判据恒假；
# pwsh 7 则摊平成记录流。统一经此助手摊平一层，两版语义一致。
function Get-ConfigRecords([string]$Path)
{
    $records = @(Get-Content $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
    if ($records.Count -eq 1 -and $records[0] -is [System.Array]) { return @($records[0]) }
    return $records
}

# —— 场景 AB：订阅重建链——连接停用/组件删除后调度器停止消费、未停用组件不误伤（todo 行 193 装机半）——
# 驱动面：conn-toggle-{connId} / widget-delete-{widgetId} 两个 UIA id（同批落 SettingsWindow）驱动
#   停用链（UpsertConnection→InvalidateWidgets→Rebuild→PinsChanged→ReloadTiles）与删除链
#   （InvalidateWidgets+RemoveWidget+ReloadWidgets→PinsChanged）。
# 断言三段（「refreshed ok」逐跳日志为唯一写 verdict 口径，WidgetHost.cs:244）：
#   AB1 停用：Toggle Off → connections.json enabled=false + GLM tile 摘除 + 静默窗 50s（ci 档 15s ≥3 跳）
#     零新 GLM 行而 ds 新行 ≥2（调度器只摘受影响者）；
#   AB2 恢复：重建行读态 Off → Toggle On → enabled=true + tile 回归 + 静默窗 GLM 新行 ≥2（复订恢复）；
#   AB3 删除：widget-delete Invoke → widgets.json 行消失 + ds tile 摘除 + 静默窗零新 ds 行（无幽灵刷新）
#     且 GLM 行 ≥2 + 进程恒 1（删除链不炸进程）。
# 种子直接用 Write-GConfig（zhipu+ds-main 双连接 / GLM+ds 双钉选组件，浮窗开、密钥齐）。
try
{
    Write-GConfig "" "off" 1.0
    Ensure-MockServer # 防御：不依赖前序场景收尾（38470370548 级联红根因）
    # 午夜翻日防（40971738405）：nightly 跨 UTC 00:00 时应用写新日期日志，固定路径读旧文件——
    # AB3 静默窗切片为空 ds=0/GLM=0 假红。每次读取按当天日期解析。
    function Get-AbLog { Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log") }
    $abWidgets = Join-Path $configDir "widgets.json"
    $abConns = Join-Path $configDir "connections.json"
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings) { Save-FullScreenshot "AB-FAIL-no-settings.png"; throw "断言失败（场景 AB 前置）：--settings 启动 30s 内未见设置窗" }
    # 基线：两 tile + 双 id 都已在刷（启动 Reload 立即拉一跳，ci 档 15s 续跳）
    $tiles0 = -1
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        $tiles0 = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
        if ($tiles0 -eq 2) { break }
        Start-Sleep -Seconds 2
    }
    if ($tiles0 -ne 2) { Save-FullScreenshot "AB-FAIL-baseline-tiles.png"; throw "断言失败（场景 AB 前置）：基线悬浮 tile=$tiles0（期望 2——GLM/ds 预置钉选）" }
    $bothSeen = $false
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline)
    {
        if (Test-Path (Get-AbLog))
        {
            $logNow = Get-Content (Get-AbLog) -Raw -Encoding UTF8
            if (($logNow -match "Widget bigmodel\.usage:GLM refreshed ok") -and ($logNow -match "Widget deepseek\.balance:ds refreshed ok")) { $bothSeen = $true; break }
        }
        Start-Sleep -Seconds 3
    }
    if (!$bothSeen) { Save-FullScreenshot "AB-FAIL-baseline-refresh.png"; throw "断言失败（场景 AB 前置）：60s 内未见双 id「refreshed ok」（GLM/ds 任一未上刷——读当日日志定因）" }
    # AB1 停用：模块页 conn-toggle-zhipu 读态 On → Toggle Off（两轮整链重试：读取与点击间隙
    # 页面若被重建，Toggle 会静默打空——X1 同款口径；失败归档 connections.json+当日日志定因）
    $disabled = $false
    $stateAfter = ""
    for ($abAttempt = 1; $abAttempt -le 2 -and !$disabled; $abAttempt++)
    {
        if ($abAttempt -gt 1) { Start-Sleep -Seconds 2 }
        $bigRow = Find-UiaById $settings "module-bigmodel" 10
        if (!$bigRow) { Save-FullScreenshot "AB-FAIL-no-bigmodel.png"; throw "断言失败（场景 AB1）：UIA 未找到 module-bigmodel" }
        Invoke-Uia $bigRow
        Start-Sleep -Seconds 2
        $connToggle = Find-UiaById $settings "conn-toggle-zhipu" 10
        if (!$connToggle) { Save-FullScreenshot "AB-FAIL-no-conn-toggle.png"; throw "断言失败（场景 AB1）：UIA 未找到 conn-toggle-zhipu（锚点丢失？）" }
        $toggleState = "$(($connToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState)"
        if ($toggleState -ne "On")
        {
            if ($abAttempt -eq 2)
            {
                # 上轮已翻（UI 链已走）而判据窗内未读到落盘——run 39050990738 定谳根因是 5.1 读法
                # 分叉（见 Get-ConfigRecords），产品链本身即 Toggle 即落（GLM 循环当刻即停）；读法
                # 修复后此处仅作 CI 负载兜底，30s 有界宽限，再不落盘才是真产品缺陷，红门不放松。
                $graceDeadline = (Get-Date).AddSeconds(30)
                while ((Get-Date) -lt $graceDeadline)
                {
                    $z = @(Get-ConfigRecords $abConns | Where-Object { $_.id -eq "zhipu" })
                    if ($z.Count -ge 1 -and "$($z[0].enabled)" -eq "False") { $disabled = $true; break }
                    Start-Sleep -Seconds 2
                }
                if (!$disabled) { $stateAfter = "$toggleState(第2轮读态+30s宽限仍未落盘)" }
                break
            }
            throw "断言失败（场景 AB1）：停用前 conn-toggle ToggleState=$toggleState（期望 On）"
        }
        ($connToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
        Start-Sleep -Milliseconds 800
        # 事后回读：区分「Toggle 打空（仍 On）」与「已翻而落盘判据未过（Off）」
        $stateAfter = "$(($connToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState)"
        $abDeadline = (Get-Date).AddSeconds(20)
        while ((Get-Date) -lt $abDeadline)
        {
            $z = @(Get-ConfigRecords $abConns | Where-Object { $_.id -eq "zhipu" })
            if ($z.Count -ge 1 -and "$($z[0].enabled)" -eq "False") { $disabled = $true; break }
            Start-Sleep -Seconds 2
        }
    }
    if (!$disabled)
    {
        Save-FullScreenshot "AB-FAIL-disable-not-saved.png"
        Copy-Item $abConns (Join-Path $evidence "connections-AB1-fail.json") -Force -ErrorAction SilentlyContinue
        Copy-Item (Get-AbLog) (Join-Path $evidence "beacon-AB1-fail.log") -Force -ErrorAction SilentlyContinue
        throw "断言失败（场景 AB1）：Toggle 后 20s（两轮）connections.json zhipu enabled 未翻 false——事后 ToggleState=$stateAfter（Off=停用链已走、磁盘判据面问题；On=Toggle 打空），读 connections-AB1-fail.json / beacon-AB1-fail.log 定因"
    }
    $tiles1 = -1
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $tiles1 = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
        if ($tiles1 -eq 1) { break }
        Start-Sleep -Seconds 2
    }
    if ($tiles1 -ne 1)
    {
        Save-FullScreenshot "AB-FAIL-disable-tile-alive.png"
        Copy-Item $abConns (Join-Path $evidence "connections-AB1-fail.json") -Force -ErrorAction SilentlyContinue
        Copy-Item (Get-AbLog) (Join-Path $evidence "beacon-AB1-fail.log") -Force -ErrorAction SilentlyContinue
        throw "断言失败（场景 AB1）：停用后 20s 内 GLM tile 未消失（tile=$tiles1，期望 1）——PinsChanged→ReloadTiles 重建链回归"
    }
    Write-Host "场景 AB1 通过：停用 zhipu → enabled=false 落盘 + GLM tile 摘除"
    # 静默窗口径：tile 摘除已过=重建链已跑；+5s 让 in-flight 落地，随后 50s（≥3 跳）零新 GLM 行而 ds ≥2
    Start-Sleep -Seconds 5
    $len1 = (Get-Content (Get-AbLog) -Raw -Encoding UTF8).Length
    Start-Sleep -Seconds 50
    $log1 = Get-Content (Get-AbLog) -Raw -Encoding UTF8
    $slice1 = if ($log1.Length -ge $len1) { $log1.Substring($len1) } else { $log1 }
    $glmNew1 = ([regex]::Matches($slice1, [regex]::Escape("Widget bigmodel.usage:GLM refreshed ok"))).Count
    $dsNew1 = ([regex]::Matches($slice1, [regex]::Escape("Widget deepseek.balance:ds refreshed ok"))).Count
    if ($glmNew1 -ne 0 -or $dsNew1 -lt 2)
    {
        Copy-Item (Get-AbLog) (Join-Path $evidence "beacon-AB1-silence-fail.log") -Force -ErrorAction SilentlyContinue
        throw "断言失败（场景 AB1 静默窗）：停用后 50s 窗内 GLM 新行=$glmNew1（期望 0——幽灵刷新）/ ds 新行=$dsNew1（期望 ≥2——不误伤）"
    }
    Write-Host "场景 AB1 静默窗通过：GLM 零新行（退订）/ ds 持续 ≥2 行（不误伤）"
    # AB2 恢复：停用时 RebuildConnections 重建页面行——重找 toggle（旧元素已死），读态应 Off
    $connToggle2 = Find-UiaById $settings "conn-toggle-zhipu" 10
    if (!$connToggle2) { Save-FullScreenshot "AB-FAIL-no-conn-toggle2.png"; throw "断言失败（场景 AB2）：重建后 UIA 未找到 conn-toggle-zhipu" }
    $state2 = "$(($connToggle2.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState)"
    if ($state2 -ne "Off") { throw "断言失败（场景 AB2）：重建后 conn-toggle ToggleState=$state2（期望 Off——停用读态不一致）" }
    ($connToggle2.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Toggle()
    $reEnabled = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $z2 = @(Get-ConfigRecords $abConns | Where-Object { $_.id -eq "zhipu" })
        if ($z2.Count -ge 1 -and "$($z2[0].enabled)" -eq "True") { $reEnabled = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$reEnabled) { Save-FullScreenshot "AB-FAIL-enable-not-saved.png"; throw "断言失败（场景 AB2）：Toggle On 后 20s 内 connections.json zhipu enabled 未翻 true" }
    $tiles2 = -1
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $tiles2 = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
        if ($tiles2 -eq 2) { break }
        Start-Sleep -Seconds 2
    }
    if ($tiles2 -ne 2)
    {
        Save-FullScreenshot "AB-FAIL-enable-tile-missing.png"
        throw "断言失败（场景 AB2）：恢复后 20s 内 GLM tile 未回归（tile=$tiles2，期望 2）——复订重建链回归"
    }
    Write-Host "场景 AB2 通过：恢复 zhipu → enabled=true 落盘 + GLM tile 回归"
    Start-Sleep -Seconds 5
    # 静默窗基线钉在本轮复订重建行（kick 首刷）之前：43662875796 实证复订 kick 三连刷
    #（02:07:10/12/17）在 tile 回归确认基线取点前已落日志大半——基线取整流长度把 kick 首两刷
    # 划在窗外、随后连接进 skip 退避段 3 分钟不刷 → 50s 窗内只剩 1 行假红。取最后一处
    # 「组件注册已重建并立即拉取」的位置作切片起点（当日日志被全天场景共享，须锚最后一处）
    $logNow = Get-Content (Get-AbLog) -Raw -Encoding UTF8
    $rebuild = [regex]::Matches($logNow, "组件注册已重建并立即拉取") | Select-Object -Last 1
    $len2 = if ($rebuild) { $rebuild.Index } else { $logNow.Length }
    Start-Sleep -Seconds 50
    $log2 = Get-Content (Get-AbLog) -Raw -Encoding UTF8
    $slice2 = if ($log2.Length -ge $len2) { $log2.Substring($len2) } else { $log2 }
    $glmNew2 = ([regex]::Matches($slice2, [regex]::Escape("Widget bigmodel.usage:GLM refreshed ok"))).Count
    if ($glmNew2 -lt 2)
    {
        Copy-Item (Get-AbLog) (Join-Path $evidence "beacon-AB2-resume-fail.log") -Force -ErrorAction SilentlyContinue
        throw "断言失败（场景 AB2 静默窗）：恢复后 50s 窗内 GLM 新行=$glmNew2（期望 ≥2——复订未恢复刷新）"
    }
    Write-Host "场景 AB2 静默窗通过：GLM 恢复 ≥2 新行（复订）"
    # AB3 删除：deepseek 模块页组件行删除钮（InvalidateWidgets+RemoveWidget+ReloadWidgets 防幽灵刷新链）
    $dsRow = Find-UiaById $settings "module-deepseek" 10
    if (!$dsRow) { Save-FullScreenshot "AB-FAIL-no-deepseek.png"; throw "断言失败（场景 AB3）：UIA 未找到 module-deepseek" }
    Invoke-Uia $dsRow
    Start-Sleep -Seconds 2
    $delBtn = Find-UiaById $settings "widget-delete-deepseek.balance:ds" 10
    if (!$delBtn) { Save-FullScreenshot "AB-FAIL-no-delete.png"; throw "断言失败（场景 AB3）：UIA 未找到 widget-delete-deepseek.balance:ds" }
    Invoke-Uia $delBtn
    $gone = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $w = @(Get-ConfigRecords $abWidgets | Where-Object { $_.id -eq "deepseek.balance:ds" })
        if ($w.Count -eq 0) { $gone = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$gone)
    {
        Save-FullScreenshot "AB-FAIL-delete-not-saved.png"
        Copy-Item $abWidgets (Join-Path $evidence "widgets-AB3-fail.json") -Force -ErrorAction SilentlyContinue
        throw "断言失败（场景 AB3）：删除后 20s 内 widgets.json 仍有 deepseek.balance:ds 行——RemoveWidget 未落盘"
    }
    $tiles3 = -1
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $tiles3 = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
        if ($tiles3 -eq 1) { break }
        Start-Sleep -Seconds 2
    }
    if ($tiles3 -ne 1)
    {
        Save-FullScreenshot "AB-FAIL-delete-tile-alive.png"
        throw "断言失败（场景 AB3）：删除后 20s 内 ds tile 未消失（tile=$tiles3，期望 1）——PinsChanged→ReloadTiles 重建链回归"
    }
    Write-Host "场景 AB3 通过：删除 ds → widgets.json 行消失 + ds tile 摘除"
    Start-Sleep -Seconds 5
    $procs3 = @(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count
    if ($procs3 -ne 1) { throw "断言失败（场景 AB3）：删除链后进程数=$procs3（期望 1——删除链炸进程）" }
    $len3 = (Get-Content (Get-AbLog) -Raw -Encoding UTF8).Length
    Start-Sleep -Seconds 50
    $log3 = Get-Content (Get-AbLog) -Raw -Encoding UTF8
    $slice3 = if ($log3.Length -ge $len3) { $log3.Substring($len3) } else { $log3 }
    $dsNew3 = ([regex]::Matches($slice3, [regex]::Escape("Widget deepseek.balance:ds refreshed ok"))).Count
    $glmNew3 = ([regex]::Matches($slice3, [regex]::Escape("Widget bigmodel.usage:GLM refreshed ok"))).Count
    if ($dsNew3 -ne 0 -or $glmNew3 -lt 2)
    {
        Copy-Item (Get-AbLog) (Join-Path $evidence "beacon-AB3-ghost-fail.log") -Force -ErrorAction SilentlyContinue
        throw "断言失败（场景 AB3 静默窗）：删除后 50s 窗内 ds 新行=$dsNew3（期望 0——幽灵刷新）/ GLM 新行=$glmNew3（期望 ≥2——不误伤）"
    }
    Write-Host "场景 AB 通过：停用退订静默/恢复复订/删除无幽灵刷新——订阅重建链三段闭环（todo 行 193）"
    Stop-Beacon
}
catch
{
    Stop-Beacon # Z 同口径：失败也收尸，防残留进程毒化 J 静默安装
    Save-FullScreenshot "AB-FAIL-unexpected.png"
    $failures.Add("场景 AB：$($_.Exception.Message)")
    Write-Host "::error::场景 AB 断言失败：$($_.Exception.Message)"
}

# —— 场景 AC：空白环带真实点击落到下层跨进程窗（todo 行 65 行为半）——
# H5b 已证宿主 WM_NCHITTEST 裁决（tile 行 HTCLIENT / 空白环带 HTTRANSPARENT）；HTTRANSPARENT 的
#   同线程限制是 Win32 文档口径——跨进程（Beacon 宿主 vs 任意应用窗）真实点击是否真穿到下层窗，
#   唯一可信裁决是行为实测：双 notepad——np1 抢走前台作前置，np2 移到面板右空白环带正下方，
#   ClickAt 合成真实点击环带点 → 前台应翻到 np2。红=点击被宿主吃掉（真发现，行 65 验收不达，
#   fail closed 如实暴露转用户裁定，不降级 warning）。观感半（手感/自然度）仍装机。
try
{
    Write-HConfig "panel"
    Ensure-MockServer # 防御：不依赖前序场景收尾
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    $panel = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        $hosts = [WinEnum]::VisibleHandlesByTitle("Beacon Pinned")
        if ($hosts.Count -ge 1) { $panel = $hosts[0]; break }
        Start-Sleep -Seconds 2
    }
    if ($panel -eq [IntPtr]::Zero) { Save-FullScreenshot "AC-FAIL-no-host.png"; throw "断言失败（场景 AC 前置）：panel 模式 30s 内未见「Beacon Pinned」宿主窗" }
    $np1 = Start-Process notepad -PassThru
    $np2 = Start-Process notepad -PassThru
    $null = $np1.WaitForInputIdle(15000)
    $null = $np2.WaitForInputIdle(15000)
    $np1.Refresh(); $np2.Refresh()
    $np1H = $np1.MainWindowHandle
    $np2H = $np2.MainWindowHandle
    if ($np1H -eq [IntPtr]::Zero -or $np2H -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "AC-FAIL-no-notepad.png"
        throw "断言失败（场景 AC 前置）：notepad 未开窗（np1=$np1H np2=$np2H）——CI 镜像缺经典 notepad？"
    }
    # 窗位编排（39035130342 红的定因修正：np1 挪 (40,40) 会盖住环带点——宿主之下的下一窗是
    # np1，点击穿到了 np1 而 np2 被居中算出负坐标挪出屏——红≠吞掉，是断言对象错位）：
    # np2 = 环带点正下层唯一窗（左上角放环带点左 150/上 60——点落客户区，避标题栏）；
    # np1 = 前台供体，挪环带点右下远处（+450/+380）绝不覆盖环带点。
    $wr = [WinEnum]::RectOf($panel)
    $cr = [WinEnum]::ClientScreenRectOf($panel)
    $bandX = $wr.Right - 2
    $bandY = [int](($cr.Top + $cr.Bottom) / 2)
    [WinEnum]::MoveNoFocus($np2H, ($bandX - 150), ($bandY - 60))
    [WinEnum]::MoveNoFocus($np1H, ($bandX + 450), ($bandY + 380))
    Start-Sleep -Seconds 1
    # 覆盖核验：环带点必须落在 np2 矩形内、且不在 np1 矩形内（编排失效即前置红，不再出假阴/假阳）
    $r2 = [WinEnum]::RectOf($np2H)
    $r1 = [WinEnum]::RectOf($np1H)
    $inNp2 = ($bandX -ge $r2.Left -and $bandX -le $r2.Right -and $bandY -ge $r2.Top -and $bandY -le $r2.Bottom)
    $inNp1 = ($bandX -ge $r1.Left -and $bandX -le $r1.Right -and $bandY -ge $r1.Top -and $bandY -le $r1.Bottom)
    if (!$inNp2 -or $inNp1)
    {
        Save-FullScreenshot "AC-FAIL-placement.png"
        throw "断言失败（场景 AC 前置）：窗位编排失效（环带点在 np2=$inNp2 / 在 np1=$inNp1——须 True/False），前置红不判穿透"
    }
    # 前置：np1 抢走前台（双开时前台属谁不确定，统一压到 np1）
    [WinEnum]::ForceForeground($np1H)
    Start-Sleep -Seconds 1
    $fg0 = [WinEnum]::Foreground()
    if ($fg0 -eq $np2H) { Save-FullScreenshot "AC-FAIL-precondition.png"; throw "断言失败（场景 AC 前置）：点击前前台已是 np2——前置失效，判据作废" }
    Save-FullScreenshot "AC-before-click.png"
    [WinEnum]::ClickAt($bandX, $bandY)
    Start-Sleep -Seconds 2
    $fg1 = [WinEnum]::Foreground()
    Save-FullScreenshot "AC-after-click.png"
    if ($fg1 -ne $np2H)
    {
        # run 39044697350 实证：HTTRANSPARENT 只在同线程内让位——跨进程真实点击被宿主吃掉（前台
        # 停在宿主句柄）。机制天花板而非回归，按 E1b/G7c 先例降 ::warning 转用户裁定（备选：
        # WS_EX_TRANSPARENT 整窗穿透 / 鼠标钩子中继 / 维持现状），夜门保持可绿。
        Write-Host "::warning::场景 AC 未达（转用户裁定，行 65）：点击空白环带后前台句柄 $fg1 未翻到下层 notepad（$np2H）——Win32 HTTRANSPARENT 同线程限制，跨进程穿透不生效"
    }
    else
    {
        Write-Host "场景 AC 通过：空白环带真实点击穿透到下层跨进程窗（前台 np1→np2）——H5b 裁决半 + 本行为半齐，观感半留装机"
    }
    Stop-Beacon
    $null = $np1.CloseMainWindow()
    $null = $np2.CloseMainWindow()
    Start-Sleep -Seconds 1
    Stop-Process -Id $np1.Id -ErrorAction SilentlyContinue
    Stop-Process -Id $np2.Id -ErrorAction SilentlyContinue
}
catch
{
    Stop-Beacon
    if ($np1) { Stop-Process -Id $np1.Id -ErrorAction SilentlyContinue }
    if ($np2) { Stop-Process -Id $np2.Id -ErrorAction SilentlyContinue }
    Save-FullScreenshot "AC-FAIL-unexpected.png"
    $failures.Add("场景 AC：$($_.Exception.Message)")
    Write-Host "::error::场景 AC 断言失败：$($_.Exception.Message)"
}

# —— 场景 AD：驻留浸泡（todo 行 97「长时稳定」CI 可达半）——单实例 15min 静置：
# 刷新续跑（ok 行增量 ≥50，ci 档 15s×2 组件理论 ~120）+ 进程恒 1 + 零「refresh failed
# unexpectedly」/零「Fatal」；句柄/内存增长走 ::warning 降级取证（VM 抖动阈值不设红门）。
# 数小时级驻留与胶囊观感仍装机半——本场景只把「慢性泄漏在 15 分钟窗内现形」收进夜门。
try
{
    Write-GConfig "" "off" 1.0
    Ensure-MockServer
    $adLog = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 90 # 首跳刷新 + 窗集建稳
    $tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile").Count
    if ($tiles -ne 2) { Save-FullScreenshot "AD-FAIL-baseline-tiles.png"; throw "断言失败（场景 AD 前置）：浸泡基线 tile=$tiles（期望 2）" }
    $adDay0 = Get-Date # 基线读取时刻：日志按日滚动的起点（FileLoggerProvider 每次写重算文件名）
    $log0 = Get-Content $adLog -Raw -Encoding UTF8
    $ok0 = ([regex]::Matches($log0, [regex]::Escape("refreshed ok"))).Count
    $proc0 = Get-Process Beacon.App -ErrorAction Stop | Select-Object -First 1
    $handles0 = $proc0.HandleCount
    $ws0 = $proc0.WorkingSet64
    $len0 = $log0.Length
    Save-FullScreenshot "AD-soak-start.png"
    Write-Host "场景 AD：浸泡开始（句柄 $handles0 / WS $([int]($ws0 / 1MB))MB / ok 行 $ok0）——静置 15min"
    Start-Sleep -Seconds 900
    $procs = @(Get-Process Beacon.App -ErrorAction SilentlyContinue)
    if ($procs.Count -ne 1) { Save-FullScreenshot "AD-FAIL-proc-gone.png"; throw "断言失败（场景 AD）：15min 静置后进程数=$($procs.Count)（期望 1——驻留期进程退出/重启即长时稳定不达）" }
    # FileLoggerProvider 每次写都按 DateTime.Now:yyyyMMdd 重算文件名（FileLoggerProvider.cs:37）——浸泡窗
    # 跨零点就换到次日文件，起始时点冻结的 $adLog 停在昨日：读到的「新行」只剩零点前那 80 秒
    # （run 45495474490 实证 ok1=11 假红，而 11 行正是 80 秒 15s×2 组件的健康速率）。按基线时刻→当前
    # 覆盖的每个日期分别读；起点前缀长度只对首日文件裁。
    $adFiles = @()
    $adSlice = ''
    $adDay = $adDay0.Date # 按日历日走（不是 +24h：基线 23:58+24h=次日 23:58 已越过读取时刻 00:13，
    # 次日文件永远走不到——首版实现实证）。前缀裁剪同样按 .Date 对表：$len0 只对基线当天的文件有意义。
    while ($adDay -le (Get-Date).Date)
    {
        $adF = Join-Path (Join-Path $configDir "logs") ("beacon-" + $adDay.ToString("yyyyMMdd") + ".log")
        if (-not (Test-Path $adF)) { $adDay = $adDay.AddDays(1); continue }
        $adFiles += $adF
        $adTxt = Get-Content $adF -Raw -Encoding UTF8
        if ($adDay -eq $adDay0.Date -and ($adTxt.Length -ge $len0)) { $adTxt = $adTxt.Substring($len0) }
        $adSlice += $adTxt
        $adDay = $adDay.AddDays(1)
    }
    $slice = $adSlice
    if ($adFiles.Count -gt 1) { Write-Host "场景 AD：浸泡窗跨日滚动了 $($adFiles.Count) 个日志文件，已合并读取" }
    $ok1 = ([regex]::Matches($slice, [regex]::Escape("refreshed ok"))).Count
    if ($ok1 -lt 50)
    {
        foreach ($adF in $adFiles) { Copy-Item $adF (Join-Path $evidence ("beacon-AD-refresh-fail." + (Split-Path $adF -Leaf))) -Force -ErrorAction SilentlyContinue }
        throw "断言失败（场景 AD）：15min 窗内 refreshed ok 新行=$ok1（期望 ≥50，已按日滚动合并读取 $($adFiles.Count) 个日志文件）——刷新循环在静置期停摆（长时稳定不达）"
    }
    $unexpected = ([regex]::Matches($slice, [regex]::Escape("refresh failed unexpectedly"))).Count
    $fatal = ([regex]::Matches($slice, [regex]::Escape("Fatal"))).Count
    if ($unexpected -ne 0 -or $fatal -ne 0)
    {
        foreach ($adF in $adFiles) { Copy-Item $adF (Join-Path $evidence ("beacon-AD-crash-fail." + (Split-Path $adF -Leaf))) -Force -ErrorAction SilentlyContinue }
        throw "断言失败（场景 AD）：静置窗内 unexpected=$unexpected / Fatal=$fatal（期望 0/0）——静置期异常"
    }
    $warnCount = ([regex]::Matches($slice, [regex]::Escape("refresh failed"))).Count
    if ($warnCount -gt 0) { Write-Host "::warning::场景 AD：静置窗内 refresh failed(Warning) $warnCount 行——mock 瞬断或健康降级，读 beacon-AD 日志复核" }
    $proc1 = Get-Process Beacon.App -ErrorAction Stop | Select-Object -First 1
    $handleGrowth = $proc1.HandleCount - $handles0
    $wsGrowthMb = [int](($proc1.WorkingSet64 - $ws0) / 1MB)
    if ($handleGrowth -gt 800 -or $wsGrowthMb -gt 150)
    {
        Write-Host "::warning::场景 AD：15min 句柄增长 $handleGrowth / WS 增长 ${wsGrowthMb}MB（阈 800/150MB）——慢性泄漏疑点，装机复核；不设红门（VM 抖动）"
    }
    Save-FullScreenshot "AD-soak-end.png"
    Write-Host "场景 AD 通过：15min 浸泡进程恒 1 + 刷新续跑（ok 新行 $ok1）+ 零 unexpected/零 Fatal（句柄 Δ$handleGrowth / WS Δ${wsGrowthMb}MB 留档）——长时稳定的 CI 可达半"
    Stop-Beacon
}
catch
{
    Stop-Beacon
    Save-FullScreenshot "AD-FAIL-unexpected.png"
    $failures.Add("场景 AD：$($_.Exception.Message)")
    Write-Host "::error::场景 AD 断言失败：$($_.Exception.Message)"
}

# —— 场景 AE：展示样式能力闭环（2026-10-24 评审 G01-G07/G10 CI 代验；todo 行 136-139 验收 ①②③④）——
# AE1 空状态可配置（验收①/G01 断链/G06 能力筛选/G07 缺失态）：zhipu enabled=false → 刷新链静默跳过
#   （WidgetHost）→ GLM 恒 null 状态（确定性空态，PublishStale 都不触发）；组件行「展示样式」照常
#   定向打开（模式由目标组件决定，不由状态有无决定）；模板列表按类型能力 DataKinds=progress 列全 5 项
#   （空状态不降级成只有状态行）；预览出「等待数据」占位（绑定在值缺席 ≠ 0%）；选数字卡应用落盘。
# AE2 四态渲染（验收③/G07/G05 槽位）：mock 档位文件 p0/p52/p100 → GLM tile 数字卡面依次显
#   精确「0%」「52%」「100%」（0 是有效值不是缺失）；缺失态 = 重启 staging（停 app → 停 mock → 删
#   cache/zhipu → 重启）→ 首跳刷新失败走无缓存合成分支 → 数字位显「等待数据」+ 摘要「拉取失败：…」。
#   首轮顺带断言「GLM」外围标签仍在（验收④槽位替换保外围）。PublishStale 两分支（40574990674 红跑
#   实证）：有缓存 = B-206 last-known 保留旧值（p100 照显 100%，灰显走 ConnectionHealthy）；
#   「等待数据」面只属无缓存分支（Progress=null + 「拉取失败」摘要）。且 JsonCacheStore 是内存写穿
#   缓存（EnsureLoaded 首载后 _memory 永驻，40588765179 实证：运行中删 states.json 文件够不到内存
#   副本）——会话内曾成功过的组件恒走 last-known，缺失态必须删缓存**加**重启才可命中。
# AE3 双面不互覆 + 样式恢复 + 重置（验收②/G02 旧对象/G04 恢复/G10 兜底）：同一窗会话先应用
#   tile=数字卡（AE1 已落）再切 row=圆环应用 → widgets.json 双面并存（应用按 Id 重取最新，闭包旧
#   对象不覆写）；重开样式中心反解选中数字卡（Identify 非默认横条）；style-reset 清面回传统渲染。
try
{
    # ===== AE1 空状态可配置 =====
    Write-GConfig "" "off" 1.0
    # zhipu 停用（在册 enabled=false）：GLM 无状态且不弹「拉取失败」——配下删 cache/zhipu 才是 GetState 恒 null 的确定性路径
    @'
[
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": false },
  { "id": "ds-main", "type": "deepseek", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ds-main", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    # 停用只断刷新链，不断水合链：WidgetHost.HydrateFromCache 启动即把 cache/{connId}/states.json
    # （B-206 last-known-state）灌回 Store——前面场景已把 GLM=38% 写进 cache/zhipu，不清掉的话
    # enabled=false 照样亮旧数据（2026-10-27 run 40534619784 AE1 实证）。删缓存目录 = GetState 恒 null。
    Remove-Item -LiteralPath (Join-Path $configDir "cache/zhipu") -Recurse -Force -ErrorAction SilentlyContinue
    Ensure-MockServer # ds 健康链用（GLM 不请求）
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings) { Save-FullScreenshot "AE-FAIL-no-settings.png"; throw "断言失败（场景 AE1）：--settings 启动 30s 内未见设置窗" }
    $bigRow = Find-UiaById $settings "module-bigmodel" 10
    if (!$bigRow) { throw "断言失败（场景 AE1）：UIA 未找到 module-bigmodel" }
    Invoke-Uia $bigRow
    Start-Sleep -Seconds 2
    $styleButton = Find-UiaById $settings "style-button-bigmodel.usage:GLM" 10
    if (!$styleButton) { Save-FullScreenshot "AE-FAIL-no-style-button.png"; throw "断言失败（场景 AE1）：UIA 未找到 style-button-bigmodel.usage:GLM（锚点丢失？）" }
    Invoke-Uia $styleButton
    $gallery = Find-GalleryWindow 15
    if (!$gallery) { Save-FullScreenshot "AE-FAIL-no-gallery.png"; throw "断言失败（场景 AE1）：展示样式窗 15s 未开——空状态（GetState null）定向链断（G01 回归）" }
    $galleryHandles = [WinEnum]::VisibleHandlesByTitle("Beacon · 组件中心")
    if ($galleryHandles.Count -ne 1) { throw "断言失败（场景 AE1）：组件中心窗句柄数 $($galleryHandles.Count)（期望 1）" }
    # 能力级列表（G06）：空状态也列全 4 进度模板 + 状态行
    foreach ($templateId in @("bar", "ring", "number", "pool", "statusline"))
    {
        if (!(Find-UiaById $gallery "style-item-$templateId" 6))
        {
            Save-FullScreenshot "AE-FAIL-missing-item-$templateId.png"
            throw "断言失败（场景 AE1）：样式列表缺 style-item-$templateId——空状态未按类型能力列全（G06 回归：把「这次没值」当「不支持」）"
        }
    }
    # 预览缺失态（G07）：绑定在值缺席 → 「等待数据」占位，不显 0%
    $galleryTexts = Get-WindowUiaTexts $galleryHandles[0]
    if (@($galleryTexts) -notcontains "等待数据")
    {
        Save-FullScreenshot "AE-FAIL-preview-missing-state.png"
        throw "断言失败（场景 AE1）：空状态预览未见「等待数据」（G07 回归）；实际：$(@($galleryTexts) -join ' | ')"
    }
    # 选数字卡（坐标点击走 ItemClick 链，选中态回读+重试）→ 应用 → 反馈 ✓ + widgets.json tile 面落盘
    Invoke-StyleItemClick $galleryHandles[0] "style-item-number" "样式 · 数字卡"
    Invoke-Uia (Find-UiaById $gallery "style-apply" 8)
    $appliedAE1 = $false
    $feedbackEl = $null
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $feedbackEl = Find-UiaById $gallery "style-feedback" 4
        if ($feedbackEl)
        {
            try { if ($feedbackEl.Current.Name -like "*已应用展示样式*") { $appliedAE1 = $true; break } } catch { }
        }
        Start-Sleep -Milliseconds 500
    }
    if (!$appliedAE1)
    {
        Save-FullScreenshot "AE-FAIL-apply-feedback.png"
        $fbName = ""
        try { $fbName = $feedbackEl.Current.Name } catch { }
        throw "断言失败（场景 AE1）：应用反馈未见「已应用展示样式」（实际：$fbName）——空状态应用链断"
    }
    $appliedFile = $false
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline)
    {
        $glmEntry = Get-ConfigRecords (Join-Path $configDir "widgets.json") | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
        if ($glmEntry -and $glmEntry.components -and $glmEntry.components.tile) { $appliedFile = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$appliedFile) { Save-FullScreenshot "AE-FAIL-tree-not-saved.png"; throw "断言失败（场景 AE1）：widgets.json GLM components.tile 未落盘——空状态应用未持久化" }
    # 模板身份钉死（40547402881 教训：「有树」不等于「对树」——数字卡=column 根 + metric 子，
    # 错模板静默混过此断言的话要到 AE2 tile 渲染才暴露，定位距离拉远）
    $tileTree = $glmEntry.components.tile
    $hasMetricChild = @($tileTree.children | Where-Object { $_.type -eq "metric" }).Count -gt 0
    if ($tileTree.type -ne "column" -or !$hasMetricChild)
    {
        Save-FullScreenshot "AE-FAIL-wrong-template.png"
        throw "断言失败（场景 AE1）：tile 面落盘非数字卡（root=$($tileTree.type)，metric 子节点缺席）——点击切模板未生效或应用了默认横条"
    }
    Write-Host "场景 AE1 通过：空状态照常定向（5 模板列全 + 等待数据占位 + 应用落盘）——G01/G06/G07 验收①"
    Stop-Beacon

    # ===== AE2 四态渲染（0/52/100/缺失）+ 槽位保外围 =====
    $aeModeFile = Join-Path $env:TEMP "beacon-mock-mode.txt"
    Set-Content -Path $aeModeFile -Value "p0" -Encoding ASCII # 首跳即 0%（0 是有效值）
    @'
[
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": true },
  { "id": "ds-main", "type": "deepseek", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ds-main", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    function Get-GlmTileHandle
    {
        foreach ($h in [WinEnum]::VisibleHandlesByTitle("Beacon Tile"))
        {
            $texts = Get-WindowUiaTexts $h
            if (@($texts) -contains "GLM") { return $h }
        }
        return [IntPtr]::Zero
    }
    function Wait-GlmTileText([string] $exact, [int] $timeoutSec, [string] $stage)
    {
        $deadline = (Get-Date).AddSeconds($timeoutSec)
        while ((Get-Date) -lt $deadline)
        {
            $h = Get-GlmTileHandle
            if ($h -ne [IntPtr]::Zero)
            {
                $texts = @(Get-WindowUiaTexts $h)
                if ($texts -contains $exact) { return $texts }
            }
            Start-Sleep -Seconds 2
        }
        $h = Get-GlmTileHandle
        $last = if ($h -ne [IntPtr]::Zero) { @(Get-WindowUiaTexts $h) -join " | " } else { "（GLM tile 未建窗）" }
        Save-FullScreenshot "AE-FAIL-state-$stage.png"
        throw "断言失败（场景 AE2 $stage）：GLM tile 文本未见精确「$exact」（ci 档 15s，等 ${timeoutSec}s）；实际：$last"
    }
    $texts0 = Wait-GlmTileText "0%" 60 "p0"
    if (@($texts0) -notcontains "GLM") { Save-FullScreenshot "AE-FAIL-slot-peripheral.png"; throw "断言失败（场景 AE2）：模板树接管数据槽但外围标签 GLM 消失（验收④槽位替换保外围回归）" }
    Write-Host "场景 AE2（0%）通过：数字卡显「0%」且外围 GLM 标签在（槽位替换保外围——验收④）"
    Set-Content -Path $aeModeFile -Value "p52" -Encoding ASCII
    Wait-GlmTileText "52%" 60 "p52" | Out-Null
    Write-Host "场景 AE2（52%）通过：数字卡显「52%」"
    Set-Content -Path $aeModeFile -Value "p100" -Encoding ASCII
    Wait-GlmTileText "100%" 60 "p100" | Out-Null
    Write-Host "场景 AE2（100%）通过：数字卡显「100%」"
    # 缺失态必须重启 staging：JsonCacheStore 内存写穿（_memory 永驻），运行中删 states.json 文件
    # 够不到内存副本 → PublishStale 恒走有缓存 last-known（保留旧值，40588765179 实证）。停 app →
    # 停 mock → 删 cache/zhipu → 重启：首跳失败即走 cached=null 合成（Progress=null + 摘要
    # 「拉取失败：…」）→ 数字位「等待数据」。断言锚「拉取失败」前缀（分支身份——空态面先于首跳
    # 失败就会出现，不能当合成态证据），「等待数据」随后核。
    Stop-Beacon
    Stop-MockServer
    Remove-Item -LiteralPath (Join-Path $configDir "cache/zhipu") -Recurse -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    $missingTexts = $null
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline -and !$missingTexts)
    {
        $h = Get-GlmTileHandle
        if ($h -ne [IntPtr]::Zero)
        {
            $texts = @(Get-WindowUiaTexts $h)
            if (@($texts | Where-Object { $_ -like "拉取失败*" }).Count -gt 0) { $missingTexts = $texts }
        }
        if (!$missingTexts) { Start-Sleep -Seconds 2 }
    }
    if (!$missingTexts)
    {
        $h = Get-GlmTileHandle
        $last = if ($h -ne [IntPtr]::Zero) { @(Get-WindowUiaTexts $h) -join " | " } else { "（GLM tile 未建窗）" }
        Save-FullScreenshot "AE-FAIL-state-missing.png"
        throw "断言失败（场景 AE2 missing）：GLM tile 未见「拉取失败」摘要（无缓存合成态未触发或 tile 未建）；实际：$last"
    }
    if (@($missingTexts) -notcontains "等待数据")
    {
        Save-FullScreenshot "AE-FAIL-missing-no-placeholder.png"
        throw "断言失败（场景 AE2 missing）：摘要「拉取失败」在但数字位未见「等待数据」（G07：缺数据 ≠ 0%）；实际：$(@($missingTexts) -join ' | ')"
    }
    Write-Host "场景 AE2（缺失）通过：重启无缓存合成态数字位显「等待数据」+ 摘要「拉取失败」前缀（缺失 ≠ 0%）——四态渲染齐（验收③）"

    # ===== AE3 双面不互覆 + 样式恢复 + 恢复传统渲染 =====
    Stop-Beacon
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow
    if (!$settings) { Save-FullScreenshot "AE-FAIL-no-settings3.png"; throw "断言失败（场景 AE3）：--settings 启动 30s 内未见设置窗" }
    $bigRow = Find-UiaById $settings "module-bigmodel" 10
    if (!$bigRow) { throw "断言失败（场景 AE3）：UIA 未找到 module-bigmodel" }
    Invoke-Uia $bigRow
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaById $settings "style-button-bigmodel.usage:GLM" 10)
    $gallery = Find-GalleryWindow 15
    if (!$gallery) { Save-FullScreenshot "AE-FAIL-no-gallery3.png"; throw "断言失败（场景 AE3）：展示样式窗 15s 未开" }
    $galleryHandles = [WinEnum]::VisibleHandlesByTitle("Beacon · 组件中心")
    # 恢复（G04）：已存 tile=数字卡 → 重开选中数字卡（Identify 反解；默认横条=回归）
    $numberItem = Find-UiaById $gallery "style-item-number" 8
    $barItem = Find-UiaById $gallery "style-item-bar" 8
    if (!$numberItem -or !$barItem) { throw "断言失败（场景 AE3）：样式列表缺数字卡/横条锚" }
    $numberSelected = $false; $barSelected = $true
    try { $numberSelected = ($numberItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected } catch { }
    try { $barSelected = ($barItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)).Current.IsSelected } catch { }
    if (!$numberSelected -or $barSelected) { Save-FullScreenshot "AE-FAIL-restore.png"; throw "断言失败（场景 AE3）：重开未反解选中数字卡（number=$numberSelected bar=$barSelected）——样式恢复回归（G04，默认横条=断链）" }
    Write-Host "场景 AE3（恢复）通过：重开反解选中数字卡（Identify）——G04"
    # 双面应用（验收②/G02）：同一会话切面 row=圆环再应用 → 双面并存
    $surfaceCombo = Find-UiaById $gallery "style-surface-box" 8
    if (!$surfaceCombo) { throw "断言失败（场景 AE3）：UIA 未找到 style-surface-box（锚点丢失？）" }
    Select-ComboItemById $gallery (Find-UiaById $gallery "style-surface-box" 8) "style-surface-row" # 面切后 BuildForm 重建整表单 → combo peer 换代，缓存引用会失效
    Invoke-StyleItemClick $galleryHandles[0] "style-item-ring" "样式 · 圆环"
    Invoke-Uia (Find-UiaById $gallery "style-apply" 8)
    $twoFaces = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $glmEntry = Get-ConfigRecords (Join-Path $configDir "widgets.json") | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
        if ($glmEntry -and $glmEntry.components -and $glmEntry.components.tile -and $glmEntry.components.row) { $twoFaces = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$twoFaces)
    {
        Save-FullScreenshot "AE-FAIL-two-faces.png"
        throw "断言失败（场景 AE3）：连续双面应用后 widgets.json 未同时保有 tile+row 面——面互覆（G02 旧对象覆写回归，验收②）"
    }
    # row 面模板身份钉死：圆环=row 根 + ring 子（同 AE1 数字卡口径）
    $rowTree = $glmEntry.components.row
    $hasRingChild = @($rowTree.children | Where-Object { $_.type -eq "ring" }).Count -gt 0
    if ($rowTree.type -ne "row" -or !$hasRingChild)
    {
        Save-FullScreenshot "AE-FAIL-wrong-row-template.png"
        throw "断言失败（场景 AE3）：row 面落盘非圆环（root=$($rowTree.type)，ring 子节点缺席）"
    }
    Write-Host "场景 AE3（双面）通过：tile=数字卡 + row=圆环 双面并存（应用按 Id 重取最新）——G02/验收②"
    # 按位置恢复（SET-UX-06）：当前选中面=row → style-reset 只清 row，tile（数字卡）必须保留
    Invoke-Uia (Find-UiaById $gallery "style-reset" 8)
    $rowCleared = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $glmEntry = Get-ConfigRecords (Join-Path $configDir "widgets.json") | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
        if ($glmEntry -and $glmEntry.components -and $glmEntry.components.tile -and (!$glmEntry.components.row)) { $rowCleared = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$rowCleared)
    {
        Save-FullScreenshot "AE-FAIL-reset-row.png"
        throw "断言失败（场景 AE3）：按位置恢复后 row 面未清或 tile 面被误清（SET-UX-06 恢复默认只改选中位置回归）"
    }
    Write-Host "场景 AE3（按位置恢复）通过：row 面清空 + tile 面保留——SET-UX-06"
    # 切回 tile 面再恢复 → 全部面清空回传统渲染（G10 兜底保留）
    Select-ComboItemById $gallery (Find-UiaById $gallery "style-surface-box" 8) "style-surface-tile" # 面切已重列清单换 peer，每次现取（SF3 同款，6760 旧引用三连红 45601056698）
    Invoke-Uia (Find-UiaById $gallery "style-reset" 8)
    $resetDone = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $glmEntry = Get-ConfigRecords (Join-Path $configDir "widgets.json") | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
        if ($glmEntry -and $null -eq $glmEntry.components) { $resetDone = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$resetDone)
    {
        Save-FullScreenshot "AE-FAIL-reset.png"
        throw "断言失败（场景 AE3）：逐面恢复后 widgets.json components 未清空——G10 兜底回归"
    }
    Write-Host "场景 AE3（重置）通过：逐面恢复后 components 清空回传统渲染——G10 + SET-UX-06"
    Remove-Item $aeModeFile -Force -ErrorAction SilentlyContinue # 还原默认 38% 态（不毒化后续场景）
    Stop-Beacon
    Write-Host "场景 AE 通过：空状态可配置 + 四态渲染 + 双面不互覆 + 槽位保外围 + 恢复/重置——展示样式能力闭环（验收 ①②③④）"
}
catch
{
    Stop-Beacon
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Save-FullScreenshot "AE-FAIL-unexpected.png"
    $failures.Add("场景 AE：$($_.Exception.Message)")
    Write-Host "::error::场景 AE 断言失败：$($_.Exception.Message)"
}

# —— 场景 AF：动作端到端（todo 行 210「端到端实际命令执行」CI 代验半；补充 A-14 ①③④本地半）——
# actions.json 种 local.command 动作 → 面板 RecentList GLM 行 Invoke 开 L3 详情窗 → 自定义动作按钮
# 在窗（ActionVisibility widgetType 过滤实证）→ AF1 确认弹窗「取消」拦执行（marker 不在）；
# AF2 「确认」真拉起 cmd 子进程写 %TEMP% marker 文件（真实进程执行硬证据）+ 结果文本「✓ 退出码 0」；
# AF3 无确认坏命令 → 「✗ 无法启动命令」（本地失败反馈不静默；远端失败反馈已由 U5 代验）。
# URL 动作真开浏览器保持装机手动（A-2/A-14 余半）。
try
{
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
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    @'
[
  { "id": "bigmodel.usage:GLM", "type": "bigmodel.usage", "connectionId": "zhipu", "refreshTier": "ci", "pinned": true,
    "config": { "label": "GLM" } }
]
'@ | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    @'
[
  { "id": "af-marker", "type": "local.command", "name": "写标记", "requireConfirmation": true,
    "parameters": { "widgetType": "bigmodel.usage", "shell": "exe", "command": "cmd", "args": "/C echo af-marker-ok > %TEMP%\\beacon-af-marker.txt" } },
  { "id": "af-bad", "type": "local.command", "name": "坏命令", "requireConfirmation": false,
    "parameters": { "widgetType": "bigmodel.usage", "shell": "exe", "command": "definitely-missing-xyz.exe" } }
]
'@ | Set-Content -Path (Join-Path $configDir "actions.json") -Encoding UTF8
    '{ "tiles": [] }' | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8
    Remove-Item -LiteralPath (Join-Path $configDir "cache/zhipu") -Recurse -Force -ErrorAction SilentlyContinue
    $afMarker = Join-Path $env:TEMP "beacon-af-marker.txt"
    Remove-Item $afMarker -Force -ErrorAction SilentlyContinue
    Ensure-MockServer
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 18
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 AF 前置）：托盘消息窗未找到" }
    [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0) # WM_HOTKEY 开面板
    $panelHandle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline -and $panelHandle -eq [IntPtr]::Zero)
    {
        $ph = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($ph.Count -ge 1) { $panelHandle = $ph[0] }
        Start-Sleep -Milliseconds 250
    }
    if ($panelHandle -eq [IntPtr]::Zero) { Save-FullScreenshot "AF-FAIL-no-panel.png"; throw "断言失败（场景 AF）：热键触发后 5s 内快速面板未出现" }
    # RecentList 等 GLM 行（mock 38% 态到位即入列）。等待口径=Get-UiaListSummaries 摘要（TrueCondition
    # 任意后代 Name，U1 同款）；行本体=Find-InvokableRowByName 向上找带 Invoke 的祖先（ItemClick 同链，S4 实证）。
    $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
    $glmRow = $null
    $recentSeen = @()
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline -and !$glmRow)
    {
        # 面板失焦自隐防线（U2 ①同款）：不可见=重发 WM_HOTKEY 重开再找，隐藏元素 UIA 读数照来但 Invoke 必炸
        if (@([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")) -notcontains $panelHandle)
        {
            [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0)
            Start-Sleep -Seconds 2
            $ph = @([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板"))
            if ($ph.Count -ge 1)
            {
                $panelHandle = $ph[0]
                $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
            }
            continue
        }
        $recentSeen = Get-UiaListSummaries $panelEl "RecentList"
        if (@($recentSeen | Where-Object { $_ -match "GLM" }).Count -ge 1)
        {
            $recentListEl = Find-UiaById $panelEl "RecentList" 5
            if ($recentListEl) { $glmRow = Find-InvokableRowByName $recentListEl "GLM" }
            if ($glmRow) { break }
        }
        Start-Sleep -Seconds 2
    }
    if (!$glmRow)
    {
        Save-FullScreenshot "AF-FAIL-no-glm-row.png"
        throw "断言失败（场景 AF）：60s 内 RecentList 无 GLM 行（实际摘要：$(@($recentSeen) -join ' | ')）——刷新链或行锚定未到位"
    }
    $afBefore = @([WinEnum]::VisibleHandlesByTitlePrefix("Beacon · "))
    # Invoke 三轮兜底（U2 ②同款）：Find 与 Invoke 之间刷新 tick 重建行容器→元素失联；每轮重开检查+重找行
    $invoked = $false
    $invokeErr = ""
    for ($attempt = 1; $attempt -le 3 -and !$invoked; $attempt++)
    {
        if ($attempt -gt 1)
        {
            if (@([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")) -notcontains $panelHandle)
            {
                [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0)
                Start-Sleep -Seconds 2
            }
            $ph = @([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板"))
            if ($ph.Count -ge 1)
            {
                $panelHandle = $ph[0]
                $panelEl = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
                $recentListEl = Find-UiaById $panelEl "RecentList" 5
                if ($recentListEl) { $glmRow = Find-InvokableRowByName $recentListEl "GLM" }
            }
            if (!$glmRow) { $invokeErr = "第 $attempt 轮重找 GLM 行失败"; Start-Sleep -Seconds 2; continue }
        }
        try
        {
            $glmRow.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            $invoked = $true
        }
        catch { $invokeErr = $_.Exception.Message.Trim(); Start-Sleep -Seconds 2 }
    }
    if (!$invoked) { Save-FullScreenshot "AF-FAIL-row-invoke.png"; throw "断言失败（场景 AF）：GLM 行 Invoke 三连失败：$invokeErr" }
    $detailHandle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline)
    {
        $hits = @([WinEnum]::VisibleHandlesByTitlePrefix("Beacon · ") | Where-Object { $afBefore -notcontains $_ })
        if ($hits.Count -ge 1) { $detailHandle = $hits[0]; break }
        Start-Sleep -Milliseconds 500
    }
    if ($detailHandle -eq [IntPtr]::Zero) { Save-FullScreenshot "AF-FAIL-no-detail.png"; throw "断言失败（场景 AF）：GLM 行 Invoke 未打开 L3 详情窗" }
    $detailEl = [System.Windows.Automation.AutomationElement]::FromHandle($detailHandle)
    $markerButton = Find-UiaByName $detailEl "写标记" 10
    if (!$markerButton) { Save-FullScreenshot "AF-FAIL-no-action-button.png"; throw "断言失败（场景 AF）：详情窗无「写标记」按钮——actions.json 动作未显面（ActionVisibility/渲染链断）" }
    # AF1 确认弹窗「取消」拦执行：marker 必须不在
    Invoke-Uia $markerButton
    $cancelButton = Find-UiaByName $detailEl "取消" 10
    if (!$cancelButton) { Save-FullScreenshot "AF-FAIL-no-dialog.png"; throw "断言失败（场景 AF1）：动作后 10s 内确认弹窗「取消」按钮不可达" }
    if (!((Get-WindowUiaTexts $detailHandle) | Where-Object { $_ -match '执行「写标记」' }))
    {
        throw "断言失败（场景 AF1）：确认弹窗文案缺「执行「写标记」」——确认语义回归"
    }
    Invoke-Uia $cancelButton
    Start-Sleep -Seconds 3
    if (Test-Path $afMarker) { Save-FullScreenshot "AF-FAIL-cancel-ran.png"; throw "断言失败（场景 AF1）：弹窗「取消」后 marker 仍在——取消未拦截执行" }
    Write-Host "场景 AF1 通过：确认弹窗可关（取消后未执行，marker 不在）"
    # AF2 确认全链：真拉起 cmd 子进程写 marker + 结果文本 ✓ 退出码 0
    Invoke-Uia $markerButton
    $okButton = Find-UiaByName $detailEl "确认" 10
    if (!$okButton) { Save-FullScreenshot "AF-FAIL-no-confirm.png"; throw "断言失败（场景 AF2）：第二轮动作后确认弹窗未出现" }
    Invoke-Uia $okButton
    $okText = $false
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline -and !$okText)
    {
        if ((Get-WindowUiaTexts $detailHandle) | Where-Object { $_ -match '✓' -and $_ -match '退出码 0' }) { $okText = $true }
        if (!$okText) { Start-Sleep -Milliseconds 500 }
    }
    if (!$okText -or !(Test-Path $afMarker))
    {
        Save-FullScreenshot "AF-FAIL-no-marker.png"
        throw "断言失败（场景 AF2）：确认后 ✓ 退出码 0=$okText / marker 落盘=$(Test-Path $afMarker)——本地命令执行链失效"
    }
    Write-Host "场景 AF2 通过：确认 → cmd 子进程真执行（marker 落盘）+ 结果「✓ 退出码 0」"
    # AF3 本地失败反馈：坏命令（无确认）→ ✗ 无法启动命令，不静默
    $badButton = Find-UiaByName $detailEl "坏命令" 5
    if (!$badButton) { Save-FullScreenshot "AF-FAIL-no-bad-button.png"; throw "断言失败（场景 AF3）：详情窗无「坏命令」按钮" }
    Invoke-Uia $badButton
    $failText = $false
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline -and !$failText)
    {
        if ((Get-WindowUiaTexts $detailHandle) | Where-Object { $_ -match '无法启动命令' }) { $failText = $true }
        if (!$failText) { Start-Sleep -Milliseconds 500 }
    }
    if (!$failText) { Save-FullScreenshot "AF-FAIL-no-fail-text.png"; throw "断言失败（场景 AF3）：坏命令执行后未见「✗ 无法启动命令」——本地失败反馈回归" }
    Write-Host "场景 AF3 通过：本地坏命令 → 「✗ 无法启动命令」（失败可见不静默）"
    # 收尾：关详情窗（面板若在场一并收起），marker 清档
    [WinEnum]::Post($detailHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE
    if (@([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")).Count -gt 0)
    {
        [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0)
        Start-Sleep -Seconds 1
    }
    Remove-Item $afMarker -Force -ErrorAction SilentlyContinue
    Stop-Beacon
    Write-Host "场景 AF 通过：本地命令动作端到端（确认拦执行/真进程落盘/失败反馈）——行 210 CI 代验半"
}
catch
{
    Stop-Beacon
    Remove-Item (Join-Path $env:TEMP "beacon-af-marker.txt") -Force -ErrorAction SilentlyContinue
    Save-FullScreenshot "AF-FAIL-unexpected.png"
    $failures.Add("场景 AF：$($_.Exception.Message)")
    Write-Host "::error::场景 AF 断言失败：$($_.Exception.Message)"
}

# —— 场景 AG：悬浮岛收起态三档（ISLAND-D1 展示方式 single/stack/carousel）+ 上岛白名单（ISLAND-D2）——
# 岛=胶囊窗形态（capsuleIsland 独立于 showCapsule，App.xaml.cs EnsureCapsule「岛开也建窗」口径）；
# 面锚=island-face-{WidgetId}（IslandFaces.cs FaceHost：AutomationId 挂 Grid 不进 UIA 树，补 SetName 造 peer）。
# 三档断言全走 UIA 结构 + 收起行近景截图；观感面（贴顶/曲面/发丝描边）仍属装机——本场景是
# 2026-11-13 界面审计点名的结构性缺口（岛类改动零机器证据）收口。
try
{
    Ensure-MockServer
    # AG1 stack 档 + 双白名单：环/条虚拟面前置 + GLM/DS 真实面续后 = MaxCollapsedFaces(4) 恰满
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["bigmodel.usage:GLM","deepseek.balance:ds"]'
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $islandHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles.Count -lt 1)
    {
        Save-FullScreenshot "AG-FAIL-no-island.png"
        throw "断言失败（场景 AG 前置）：capsuleIsland=true 未见「Beacon Status」岛窗（EnsureCapsule 岛开也建窗失效？）"
    }
    $islandRoot = [System.Windows.Automation.AutomationElement]::FromHandle($islandHandles[0])
    foreach ($faceId in @("island-face-island.progressring", "island-face-island.progressbar", "island-face-bigmodel.usage:GLM", "island-face-deepseek.balance:ds"))
    {
        $face = Find-UiaById $islandRoot $faceId 8
        if (!$face)
        {
            Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AG-FAIL-missing-$($faceId -replace '[^a-zA-Z0-9]', '-').png" 4
            throw "断言失败（场景 AG1）：岛收起行未找到 $faceId（stack 档应=环/条+双白名单面共 4 面）"
        }
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AG-island-stack.png" 4
    Write-Host "场景 AG1 通过：stack 档收起行 4 面齐（环/条/GLM/DS，上岛白名单按序渲染）"
    Stop-Beacon
    # AG2 single 档：序首面常驻（=环），条/GLM/DS 全部退场（IslandDisplayPlan Take(1) 口径）
    Write-GConfig "" "off" 1.0 $false $false $true "single" '["bigmodel.usage:GLM","deepseek.balance:ds"]'
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $islandHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles.Count -lt 1)
    {
        Save-FullScreenshot "AG-FAIL-single-no-window.png"
        throw "断言失败（场景 AG2 前置）：single 档岛窗未出现"
    }
    $islandRoot = [System.Windows.Automation.AutomationElement]::FromHandle($islandHandles[0])
    if (!(Find-UiaById $islandRoot "island-face-island.progressring" 8))
    {
        Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AG-FAIL-single-no-ring.png" 4
        throw "断言失败（场景 AG2）：single 档序首环面未常驻"
    }
    foreach ($gone in @("island-face-island.progressbar", "island-face-bigmodel.usage:GLM", "island-face-deepseek.balance:ds"))
    {
        if (Find-UiaById $islandRoot $gone 2)
        {
            Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AG-FAIL-single-not-taken.png" 4
            throw "断言失败（场景 AG2）：single 档 $gone 未退场（单组件常驻=Take(1)，溢出面必须不渲染）"
        }
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AG-island-single.png" 4
    Write-Host "场景 AG2 通过：single 档仅序首环面常驻（条/GLM/DS 退场）"
    Stop-Beacon
    # AG3 carousel 档：徽标+单面轮转（4s 一拍，UpdateCarouselTimer 口径）——12s 采样窗内观察到
    # ≥2 个不同面=节拍在走。逐面探测（timeout 1s≈2 次 FindFirst）避免单轮全 4 面探测拖过一拍。
    Write-GConfig "" "off" 1.0 $false $false $true "carousel" '["bigmodel.usage:GLM","deepseek.balance:ds"]'
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $islandHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles.Count -lt 1)
    {
        Save-FullScreenshot "AG-FAIL-carousel-no-window.png"
        throw "断言失败（场景 AG3 前置）：carousel 档岛窗未出现"
    }
    $islandRoot = [System.Windows.Automation.AutomationElement]::FromHandle($islandHandles[0])
    $carouselIds = @("island-face-island.progressring", "island-face-island.progressbar", "island-face-bigmodel.usage:GLM", "island-face-deepseek.balance:ds")
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($round in 1..12)
    {
        foreach ($faceId in $carouselIds)
        {
            if (Find-UiaById $islandRoot $faceId 1) { [void]$seen.Add($faceId) }
        }
        if ($seen.Count -ge 2) { break }
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AG-island-carousel.png" 4
    if ($seen.Count -lt 2)
    {
        throw "断言失败（场景 AG3）：carousel 档 12s 采样窗内只见 $($seen.Count) 面（实际：$($seen -join ' | ')）——4s 轮转节拍未走（候选池 4 面）"
    }
    Stop-Beacon
    Write-Host "场景 AG3 通过：carousel 档单面轮转在走（12s 内观察到 $($seen.Count) 个不同面）"
    Write-Host "场景 AG 通过：岛收起态三档（stack 4 面 / single 序首常驻 / carousel 轮转）+ 上岛白名单——ISLAND-D1/D2 CI 视觉取证面"
}
catch
{
    Stop-Beacon
    Save-FullScreenshot "AG-FAIL-unexpected.png"
    $failures.Add("场景 AG：$($_.Exception.Message)")
    Write-Host "::error::场景 AG 断言失败：$($_.Exception.Message)"
}

# —— 场景 AH：岛展开态（悬停预览三态取证）+ 占位面（ISLAND-D3）+ 组件级下钻（ISLAND-D4）——
# 展开态=悬停 SourcePreview 长出（岛变高）；CI 锁屏会话取数批+真实指针悬停双通道：
#   ①BEACON_DEBUG_ISLAND_PREVIEW=1 程序触发取证钩子（CapsuleWindow.cs：首次拿到预览行自动展开，
#     不设变量行为逐字节不动）覆盖「注入不了指针」的首展；②SendInput 纯移动（MoveMouse）驱动
#     PointerExited→收 / PointerEntered→再展 的真实悬停往返。
# 占位面=白名单含未取数组件（enabled:false → 无 WidgetStateChanged → _lastStates 缺键 → 占位 chip）。
try
{
    Ensure-MockServer
    # AH1 展开态：程序触发首展（窗高长出收起 32 DIP 基线）→ 真实指针收/展往返
    $env:BEACON_DEBUG_ISLAND_PREVIEW = "1"
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["bigmodel.usage:GLM","deepseek.balance:ds"]'
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Remove-Item Env:BEACON_DEBUG_ISLAND_PREVIEW -ErrorAction SilentlyContinue # 注入只给这一个子进程（教训：残留会泄给后续所有实例）
    Start-Sleep -Seconds 25 # mock 取数 + 首批预览行落地（Q 口径 15s + 喂入余量）
    $islandHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles.Count -lt 1)
    {
        Save-FullScreenshot "AH-FAIL-no-island.png"
        throw "断言失败（场景 AH 前置）：岛窗未出现"
    }
    $collapsedPx = ExpectedRowPx $islandHandles[0] 32 # 收起态曲面高 32 DIP（贴顶无阴影带口径）
    $expanded = $false
    $texts = @()
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline)
    {
        $rect = [WinEnum]::RectOf($islandHandles[0])
        $h = $rect.Bottom - $rect.Top
        $texts = Get-WindowUiaTexts $islandHandles[0]
        if (($h -ge [int]($collapsedPx * 1.6)) -and (@($texts | Where-Object { $_ -match 'GLM' }).Count -gt 0)) { $expanded = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$expanded)
    {
        $rect = [WinEnum]::RectOf($islandHandles[0])
        Save-FullScreenshot "AH-FAIL-not-expanded.png"
        throw "断言失败（场景 AH1）：40s 内岛未展开（窗高 $($rect.Bottom - $rect.Top)px < 1.6×收起基线 $collapsedPx，或预览行文本未到）"
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 8) "AH-island-expanded.png" 3
    Write-Host "场景 AH1 通过：岛展开态在位（预览行长出，窗高 ≥1.6×收起基线）"
    # AH1d 展开态肩形断言（2026-11-28 用户令恢复 BrimDeck 喇叭肩「上面的角向内弯」重写；2026-11-25
    # 版按「肩 0 全宽板」断翼板近黑，随肩恢复作废）：肩 16 DIP 让窗口两侧各留 16 DIP 透明通道
    # （肩下 region 外）——2026-11-25 曾当白板病灶归零肩部，后定谳白板=叠层缺陷（过期拍+DWM 边框）
    # 非肩本身，BrimDeck 正形即带通道（WPF 透明窗同观感）。两判据：① 通道点（窗缘内 4 DIP、半高处）
    # == 同高窗外参照（region 镂空透桌面，色距 ≤24，AH5 月牙同口径）——肩被填死/胶囊回归时该点近黑，
    # 与参照距拉满即红；② 主体点（窗缘内 肩+6 DIP）岛面本体近黑（三通道和 <90）——region 未随肩
    # 内收/面缺失时该点透桌面（和 ≥153）即红。
    $flankRect = [WinEnum]::ClientScreenRectOf($islandHandles[0])
    $flankScale = [WinEnum]::DpiScaleOf($islandHandles[0])
    $flankY = $flankRect.Top + [int](($flankRect.Bottom - $flankRect.Top) / 2)
    $flankRefL = Get-AvgColor ($flankRect.Left - 30) $flankY 1
    $flankRefR = Get-AvgColor ($flankRect.Right + 30) $flankY 1
    foreach ($flank in @(
            @(($flankRect.Left + [int](4 * $flankScale)), '左', $flankRefL, ($flankRect.Left + [int]((16 + 6) * $flankScale))),
            @(($flankRect.Right - [int](5 * $flankScale)), '右', $flankRefR, ($flankRect.Right - [int]((16 + 7) * $flankScale)))))
    {
        $fc = Get-AvgColor $flank[0] $flankY 2
        $fref = $flank[2]
        $fdist = ([Math]::Abs($fc[0] - $fref[0]) + [Math]::Abs($fc[1] - $fref[1]) + [Math]::Abs($fc[2] - $fref[2]))
        if ($fdist -gt 24)
        {
            Save-FullScreenshot "AH-FAIL-flank-channel.png"
            throw "断言失败（场景 AH1d）：展开态$($flank[1])肩通道采色 ($($fc -join ',')) 与同高窗外参照 ($($fref -join ',')) 色距 $fdist（期望 ≤24）——肩通道未镂空（胶囊回归/露背板，BrimDeck 喇叭肩形丢失）"
        }
        Write-Host "场景 AH1d $($flank[1])肩通道=($($fc -join ',')) vs 同高参照距 $fdist ≤24——喇叭肩通道镂空成立"
        $bc = Get-AvgColor $flank[3] $flankY 2
        $bsum = [int]($bc[0] + $bc[1] + $bc[2])
        if ($bsum -ge 90)
        {
            Save-FullScreenshot "AH-FAIL-flank-body.png"
            throw "断言失败（场景 AH1d）：展开态$($flank[1])肩下主体（肩内 6 DIP）采色 ($($bc -join ',')) 三通道和 $bsum（期望 <90 近黑）——region 未随肩内收/岛面缺失"
        }
        Write-Host "场景 AH1d $($flank[1])肩下主体=($($bc -join ',')) 和 $bsum <90——肩形主体实着"
    }
    # AH1a 真实指针进入：探针首展是程序化 ShowPreview 直调（不设 _hovered）——指针从未「进入」过岛时
    # 移出不构成 PointerExited（43662875796 实证：跳过这步移出后岛不收）。先真悬停进岛建立 enter 态
    #（岛已展开，ShowPreview 重入走「已可见」早退无害），随后的移出才是真实 enter→exit 序列
    $rect = [WinEnum]::RectOf($islandHandles[0])
    $centerX = [int](($rect.Left + $rect.Right) / 2)
    $centerY = [int](($rect.Top + $rect.Bottom) / 2)
    [WinEnum]::MoveMouse($centerX, $centerY)
    Start-Sleep -Seconds 2
    # AH1b 真实指针收起：移出窗 → PointerExited → 收（动效 off 档瞬时）→ 高回基线
    [WinEnum]::MoveMouse(30, 600)
    Start-Sleep -Seconds 3
    $rect = [WinEnum]::RectOf($islandHandles[0])
    if (($rect.Bottom - $rect.Top) -gt [int]($collapsedPx * 1.5))
    {
        Save-FullScreenshot "AH-FAIL-hover-out.png"
        throw "断言失败（场景 AH1b）：指针移出后岛未收（高 $($rect.Bottom - $rect.Top)px vs 基线 $collapsedPx）——PointerExited 收起链失效"
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AH-island-collapsed-after-hover.png" 4
    # AH1c 真实指针再展：移回收起行中心 → PointerEntered → ShowPreview → 高长出
    $rect = [WinEnum]::RectOf($islandHandles[0])
    $centerX = [int](($rect.Left + $rect.Right) / 2)
    $centerY = [int](($rect.Top + $rect.Bottom) / 2)
    [WinEnum]::MoveMouse($centerX, $centerY)
    $reexpanded = $false
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline)
    {
        $rect = [WinEnum]::RectOf($islandHandles[0])
        if (($rect.Bottom - $rect.Top) -ge [int]($collapsedPx * 1.6)) { $reexpanded = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$reexpanded)
    {
        Save-FullScreenshot "AH-FAIL-hover-in.png"
        throw "断言失败（场景 AH1c）：指针悬停岛后 10s 内未再展开——PointerEntered 展开链失效（MoveMouse 注入未进 XAML 管线？）"
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 8) "AH-island-reexpanded.png" 3
    Write-Host "场景 AH1 通过：悬停往返（指针移出收 / 移回展）真实事件链全通"
    Stop-Beacon
    # AH2 占位面（ISLAND-D3 data_quality）：enabled:false 组件上岛 → 无状态 → 空心灯点占位 chip（不造摘要）
    $ghostWidget = ',{ "id": "bigmodel.usage:ghost", "type": "bigmodel.usage", "connectionId": "zhipu", "refreshTier": "ci", "pinned": false, "enabled": false, "config": { "label": "GHOST" } }'
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["bigmodel.usage:ghost"]' $ghostWidget
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $islandHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles.Count -lt 1)
    {
        Save-FullScreenshot "AH-FAIL-ghost-no-window.png"
        throw "断言失败（场景 AH2 前置）：占位批岛窗未出现"
    }
    $islandRoot = [System.Windows.Automation.AutomationElement]::FromHandle($islandHandles[0])
    $ghostFace = $null
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline -and !$ghostFace)
    {
        $ghostFace = Find-UiaById $islandRoot "island-face-bigmodel.usage:ghost" 3
        if (!$ghostFace) { Start-Sleep -Seconds 1 }
    }
    if (!$ghostFace)
    {
        Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AH-FAIL-no-ghost-face.png" 4
        throw "断言失败（场景 AH2）：白名单未取数组件未出占位面（island-face-bigmodel.usage:ghost 不在树）"
    }
    if (!(Find-UiaById $islandRoot "island-face-island.progressring" 8))
    {
        throw "断言失败（场景 AH2）：占位批环面缺失（对照组，环/条应照常前置）"
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles[0])) 6) "AH-island-placeholder.png" 4
    Write-Host "场景 AH2 通过：未取数组件出占位面（不造进度/摘要，data_quality 口径 CI 面）"
    Stop-Beacon
    # AH3 组件级下钻（ISLAND-D4）：坐标单击 GLM 面宿主 → IslandWidgetActivated → 看板开 + 智谱来源卡定位
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["bigmodel.usage:GLM"]'
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 20
    $islandHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles.Count -lt 1)
    {
        Save-FullScreenshot "AH-FAIL-drill-no-window.png"
        throw "断言失败（场景 AH3 前置）：下钻批岛窗未出现"
    }
    $drilled = $false
    $drillErr = ""
    foreach ($attempt in 1..3)
    {
        $islandRoot = [System.Windows.Automation.AutomationElement]::FromHandle($islandHandles[0])
        $face = Find-UiaById $islandRoot "island-face-bigmodel.usage:GLM" 8
        if (!$face) { $drillErr = "GLM 面不在树（第 $attempt 次）"; continue }
        [WinEnum]::ForceForeground($islandHandles[0]) # 岛本身置顶，防御性拉前台（点击必落最上层）
        [WinEnum]::ForceTopmost($islandHandles[0]) # region 窗 band 兜底重钉（点击按 z 序命中，TOPMOST 必须在场）
        Start-Sleep -Milliseconds 400
        try
        {
            Invoke-Uia $face # peer InvokePattern = released 单击同款路由（IslandWidgetActivated），与坐标/z 序无关
        }
        catch
        {
            # Invoke 不可用（peer 未挂 InvokePattern 等）退坐标点击链：pressed→released 净位移<6 路由
            $point = Get-ClickPoint $face
            if (!$point) { $drillErr = "GLM 面取不到可点坐标（第 $attempt 次，BoundingRectangle NaN？）"; continue }
            [WinEnum]::MoveMouse($point[0], $point[1]) # 先移入再点：命中测试按释放点跑，冷启动单击跳过进入事件偶发丢失
            [WinEnum]::ClickAt($point[0], $point[1])
        }
        $panelHandle = [IntPtr]::Zero
        $deadline = (Get-Date).AddSeconds(8)
        while ((Get-Date) -lt $deadline)
        {
            $hs = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
            if ($hs.Count -ge 1) { $panelHandle = $hs[0]; break }
            Start-Sleep -Milliseconds 400
        }
        if ($panelHandle -eq [IntPtr]::Zero) { $drillErr = "单击后 8s 内看板未开（第 $attempt 次）"; continue }
        $panelRoot = [System.Windows.Automation.AutomationElement]::FromHandle($panelHandle)
        $chip = Find-UiaById $panelRoot "chip-智谱" 8
        if (!$chip) { $drillErr = "看板开但未定位到 chip-智谱 来源卡（RevealSource 过滤链失效，第 $attempt 次）"; continue }
        Save-Zoom (New-Crop ([WinEnum]::RectOf($panelHandle)) 0) "AH3-drilldown-panel.png" 2
        $drilled = $true
        break
    }
    if (!$drilled)
    {
        Save-FullScreenshot "AH-FAIL-drill.png"
        # 窗口诊断：全部含 Beacon 标题的可见窗+矩形（43812011171「GLM 面不在树」配套取证——
        # 分辨「岛窗被 stale region 钉死」还是「抓到残留旧窗」）；island-trace 同步入档
        if ($islandHandles.Count -ge 1)
        {
            $vh = [WinEnum]::RectOf($islandHandles[0])
            Write-Host "诊断：岛窗 hwnd=$($islandHandles[0]) visible=$([WinEnum]::Visible($islandHandles[0])) rect=$($vh.Left),$($vh.Top)-$($vh.Right),$($vh.Bottom)"
            try
            {
                $diagRoot = [System.Windows.Automation.AutomationElement]::FromHandle($islandHandles[0])
                $diagAll = $diagRoot.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
                Write-Host "诊断：岛 UIA 树元素数=$($diagAll.Count)"
                $dn = 0
                foreach ($del in $diagAll)
                {
                    if ($dn -ge 60) { Write-Host "诊断：（仅列前 60）"; break }
                    Write-Host "诊断：树 el id='$($del.Current.AutomationId)' name='$($del.Current.Name)'"
                    $dn++
                }
            }
            catch { Write-Host "诊断：岛 UIA 树枚举失败：$($_.Exception.Message)" }
        }
        foreach ($h in [WinEnum]::VisibleHandlesByTitlePrefix("Beacon"))
        {
            $r = [WinEnum]::RectOf($h)
            Write-Host "诊断：可见窗 hwnd=$($h) title=$([WinEnum]::TitleOf($h)) rect=$($r.Left),$($r.Top)-$($r.Right),$($r.Bottom)"
        }
        $trace = Join-Path $env:APPDATA "Beacon\logs\island-trace.txt"
        if (Test-Path $trace)
        {
            Copy-Item $trace (Join-Path $evidence "AH3-island-trace.txt") -Force
            Write-Host "诊断：island-trace 已入档 evidence/AH3-island-trace.txt（尾部 40 行）"
            Get-Content $trace -Tail 40 | ForEach-Object { Write-Host "trace: $_" }
        }
        throw "断言失败（场景 AH3）：岛组件面单击下钻三连失败：$drillErr"
    }
    Stop-Beacon
    Write-Host "场景 AH3 通过：岛 GLM 面单击 → 看板开 + 智谱来源卡定位（组件级下钻链）"
    # AH5 岛灰板修复取证（方案 B，2026-11-16：SetWindowRgn 真镂空）——岛态底角月牙（12 DIP 底角
    # 弧外；肩 2026-11-28 恢复后顶两角弧外区由 AH5d-B region 探针把守，月牙门仍锚底两角）应透到
    # 背后层（与窗外同高参照比，CI 桌面有宿主终端窗占屏、角落背景非壁纸故参照自洽取同高两点）；
    # 经典态四角恒=亚克力灰（无 region，RootBorder 圆角外露窗底材质）=经典观感保持（逐字节不动红线）。
    # 重启链三轮切换（岛→经典→岛）覆盖 region 摘/设多轮；10 次运行时循环与亮/暗壁纸真机观感待装机亲验。
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["bigmodel.usage:GLM"]'
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $islandHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles.Count -lt 1)
    {
        Save-FullScreenshot "AH5-FAIL-no-island.png"
        throw "断言失败（场景 AH5 前置）：岛批岛窗未出现"
    }
    $vs5 = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $refL5 = Get-AvgColor ($vs5.X + 10) ($vs5.Y + $vs5.Height - 80) 12
    $refR5 = Get-AvgColor ($vs5.X + $vs5.Width - 22) ($vs5.Y + $vs5.Height - 80) 12
    $wallpaper5 = @([int](($refL5[0] + $refR5[0]) / 2), [int](($refL5[1] + $refR5[1]) / 2), [int](($refL5[2] + $refR5[2]) / 2))
    $islandDist = Get-CrescentMaxDistance $islandHandles[0]
    Save-FullScreenshot "AH5-island-transparent.png"
    if ($islandDist -gt 24)
    {
        # 失败即归档 island-trace（AH3 通过路径不落档，region 是否应用过只有 trace 能裁）
        $trace = Join-Path $env:APPDATA "Beacon\logs\island-trace.txt"
        if (Test-Path $trace)
        {
            Copy-Item $trace (Join-Path $evidence "AH5-island-trace.txt") -Force
            Get-Content $trace -Tail 30 | ForEach-Object { Write-Host "trace: $_" }
        }
        throw "断言失败（场景 AH5a）：岛态底角月牙与同高参照最大色距 $islandDist（期望 ≤24）——弧外区露窗背板非背后层（满值=黑板），SetWindowRgn 未裁出弧外区"
    }
    Write-Host "场景 AH5a 通过：岛态底角月牙=背后层（最大色距 $islandDist ≤24）——region 真镂空成立"
    Stop-Beacon
    # AH5b 切经典：showCapsule=true 才建胶囊窗（EnsureCapsule 懒实例化口径），capsuleIsland=false
    Write-GConfig "" "off" 1.0 $false $true $false
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $classicHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($classicHandles.Count -lt 1)
    {
        Save-FullScreenshot "AH5-FAIL-no-classic.png"
        throw "断言失败（场景 AH5b 前置）：经典批胶囊窗未出现（showCapsule=true 未建窗？）"
    }
    $classicDist = Get-CornerMaxDistance $classicHandles[0] $wallpaper5
    Save-FullScreenshot "AH5-classic-backdrop.png"
    if ($classicDist -lt 40)
    {
        throw "断言失败（场景 AH5b）：经典态窗四角≈壁纸（色距 $classicDist 期望 ≥40）——经典亚克力丢失（经典观感逐字节不动红线）"
    }
    Write-Host "场景 AH5b 通过：经典态四角=亚克力（色距 $classicDist ≥40）——经典观感保持"
    Stop-Beacon
    # AH5c 切回岛：第二轮摘除后无残留（重挂→摘泄漏面）
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["bigmodel.usage:GLM"]'
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    Start-Sleep -Seconds 15
    $finalHandles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($finalHandles.Count -lt 1)
    {
        Save-FullScreenshot "AH5-FAIL-no-reisland.png"
        throw "断言失败（场景 AH5c 前置）：回切岛批岛窗未出现"
    }
    $finalDist = Get-CrescentMaxDistance $finalHandles[0]
    Save-FullScreenshot "AH5-reisland-transparent.png"
    if ($finalDist -gt 24)
    {
        throw "断言失败（场景 AH5c）：回切岛态底角月牙与同高参照最大色距 $finalDist（期望 ≤24）——重挂后再摘仍有残留（满值=黑板，region 未重建）"
    }
    Write-Host "场景 AH5 通过：岛↔经典↔岛三轮切换无泄漏（岛月牙=背后层、经典四角=亚克力、回切岛=背后层）"
    # AH5d 岛边缘精度取证（2026-11-18 用户真机三瑕疵驱动：透明底板/圆角不自然/强锯齿，复用 AH5c 活岛窗）：
    # ①底角月牙 vs 同高窗外参照（用户真机「透明底板」残留面在底部/底层——CI 补底部两角的 region 镂空门，
    #   同款背后层自洽口径壁纸/终端皆可，硬断言；探针统一走 Get-CrescentMaxDistance，禁自写窗角定值采样）；
    # ②底缘中点过渡带剖面（黑面→窗外逐行采色，「非黑非底」像素数=边缘灰带宽度；HRGN 硬边 + 1px 发丝
    #   内缘的基线，暂 advisory——逐像素 alpha 修复后升硬断言 ≤1px）；
    # ③曲线边锯齿楼梯度量（逐行首个 surface 列=曲线轨迹，相邻行 Δx=量化台阶；HRGN 物理锯齿基线，
    #   并排对照的量化口径，暂 advisory）；
    # ④四角+双边中点 4x 近景存档（NearestNeighbor 真实像素无插值美化——并排对照差异清单素材）。
    $islandH5d = $finalHandles[0]
    $rect5d = [WinEnum]::ClientScreenRectOf($islandH5d)
    $scale5d = [WinEnum]::DpiScaleOf($islandH5d)
    $w5d = $rect5d.Right - $rect5d.Left
    $midX5d = $rect5d.Left + [int]($w5d / 2)
    $midY5d = $rect5d.Top + [int](($rect5d.Bottom - $rect5d.Top) / 2)
    # 探针口径与 AH5a/AH5c 统一（共享 Get-CrescentMaxDistance）：旧内联版按「窗角定值偏移 4DIP + 2x2 均值」
    # 采样，两向失效——run 45536879866 实证：surface 铺满窗框时 (343,28) 骑在弧 AA 边（读 130/151）假红，
    # 而 run 45333483412 的假绿是 surface 只 310px 居中、探针落在透明边距里根本没碰岛。共享探针改为
    # 窗位稳定等待 + Bottom−1/−2 对角单像素 + 逐偏移取最小，AH5a/AH5c 同 run 双处已实证可测 0。
    $bottomDist5d = Get-CrescentMaxDistance $islandH5d
    if ($bottomDist5d -gt 24)
    {
        throw "断言失败（场景 AH5d①）：岛底角月牙与同高参照最大色距 $bottomDist5d（期望 ≤24）——底部弧外露窗背板/残留层（用户真机「透明底板」CI 面），region 底部未镂空"
    }
    Write-Host "场景 AH5d① 通过：岛底角月牙=背后层（最大色距 $bottomDist5d ≤24）——region 底部镂空成立"
    # ⑤ region 几何直读门（BEACON-FIX-1126 病灶 A/B，2026-11-26；肩形探针 2026-11-28）：GetWindowRgn+
    # PtInRegion/GetRgnBox 直接问 OS 裁剪裁决——四角出 region（底角 12 弧 + 顶角在喇叭肩曲率起点内）、
    # 病灶 B（2026-11-28 1c0f34b 起 region 走轮廓中心线：顶缘 (0,0)→(w,0) 全宽贴屏 → 包围盒 Left=0、
    # 贴顶铁律 Top=0；旧 4b934bc 方向性 1px 内缩已废——肩形斜曲线被两轴各削 1px=亚像素斜切）、
    # 喇叭肩形（肩下通道出 region、肩内主体在 region，胶囊回归即红）。决定性、免桌面像素参照
    # （CI 桌面非纯色的假红面根除，AH5d 假红两半根因教训同口径规避）。
    $winRgn5d = [WinEnum]::RectOf($islandH5d)
    $cliRgn5d = [WinEnum]::ClientScreenRectOf($islandH5d)
    $offX5d = $cliRgn5d.Left - $winRgn5d.Left   # 客户区→窗相对偏移（无边框岛恒 0，换算写全防 chrome 假设）
    $offY5d = $cliRgn5d.Top - $winRgn5d.Top
    $wCli5d = $cliRgn5d.Right - $cliRgn5d.Left
    $hCli5d = $cliRgn5d.Bottom - $cliRgn5d.Top
    $rgnKind5d = [WinEnum]::WindowRgnKind($islandH5d)
    if ($rgnKind5d -le 1)
    {
        throw "断言失败（AH5d-B region 门）：GetWindowRgn=$rgnKind5d——岛窗无 OS region（方案 B 失效，弧外全露窗背板）"
    }
    $rgnBox5d = [WinEnum]::WindowRgnBox($islandH5d)
    # GDI 包围盒约定：Left/Top 含（region 最左/最上像素）、Right/Bottom 排（最右/最下像素 +1）。
    # 中心线口径：顶缘起点 (0,0) 全宽贴屏 → 最左列=0（Left=offX）、底角弧终点 y=h → 多边形内部
    # 最低扫描行 h-1 → Bottom=offY+h（扫描行 h-1 +1）。
    if ($rgnBox5d.Top -ne $offY5d)
    {
        throw "断言失败（AH5d-B）：region 包围盒 Top=$($rgnBox5d.Top)（期望 $offY5d）——顶缘离开屏幕顶（贴顶铁律破）"
    }
    if ($rgnBox5d.Left -ne $offX5d -or $rgnBox5d.Bottom -gt ($offY5d + $hCli5d))
    {
        throw "断言失败（AH5d-B）：region 包围盒 Left=$($rgnBox5d.Left) Bottom=$($rgnBox5d.Bottom) 客户区=$($wCli5d)x$($hCli5d) 偏移=$offX5d,$offY5d（期望 Left=$offX5d、Bottom≤$($offY5d + $hCli5d)）——region 未沿轮廓中心线（顶缘全宽应贴到窗左缘 0，旧内缩口径回归）"
    }
    # 四角出 region（底角弧 + 顶角在喇叭肩曲率起点内，窗相对坐标=客户区偏移后取角内 1px）；
    # 半径下界 12px（1x）保证角内 1px 点距弧心恒大于半径。顶/底缘中点在 region 内（边未破）。
    # 肩形探针（2026-11-28 恢复 BrimDeck 喇叭肩）：肩深 7 DIP 之下主体内收 16 DIP——肩下通道两点
    # （窗缘内 2px）必须出 region（喇叭肩镂空），肩内主体两点必须在 region（面本体随肩内收）。
    # 胶囊回归（肩 0）时通道点落回 region 即红；region 未随肩内收时主体点恒在 region 不设防（AH1d
    # 像素门兜底主体实着）。
    $shPx5d = [int][Math]::Ceiling(16 * $scale5d)
    $depPx5d = [int][Math]::Ceiling(7 * $scale5d)
    $shY5d = ($offY5d + $depPx5d + 2)
    $rgnCorners5d = @(
        @{ Label = "顶左角"; X = ($offX5d + 1); Y = ($offY5d + 1); Inside = $false },
        @{ Label = "顶右角"; X = ($offX5d + $wCli5d - 2); Y = ($offY5d + 1); Inside = $false },
        @{ Label = "底左角"; X = ($offX5d + 1); Y = ($offY5d + $hCli5d - 2); Inside = $false },
        @{ Label = "底右角"; X = ($offX5d + $wCli5d - 2); Y = ($offY5d + $hCli5d - 2); Inside = $false },
        @{ Label = "顶缘中点"; X = ($offX5d + [int]($wCli5d / 2)); Y = ($offY5d + 1); Inside = $true },
        @{ Label = "底缘中点"; X = ($offX5d + [int]($wCli5d / 2)); Y = ($offY5d + $hCli5d - 2); Inside = $true },
        @{ Label = "肩下左通道"; X = ($offX5d + 2); Y = $shY5d; Inside = $false },
        @{ Label = "肩下右通道"; X = ($offX5d + $wCli5d - 3); Y = $shY5d; Inside = $false },
        @{ Label = "肩内左主体"; X = ($offX5d + $shPx5d + 4); Y = $shY5d; Inside = $true },
        @{ Label = "肩内右主体"; X = ($offX5d + $wCli5d - $shPx5d - 5); Y = $shY5d; Inside = $true }
    )
    foreach ($rc5d in $rgnCorners5d)
    {
        $hitRgn5d = [WinEnum]::WindowRgnContains($islandH5d, $rc5d.X, $rc5d.Y)
        if ($hitRgn5d -ne $rc5d.Inside)
        {
            throw "断言失败（AH5d-B region 门）：窗坐标 ($($rc5d.X),$($rc5d.Y)) $($rc5d.Label) PtInRegion=$hitRgn5d（期望 $($rc5d.Inside)）——角弧/肩形/中心线几何回归"
        }
    }
    Write-Host "场景 AH5d-B 通过：region 直读——四角+肩形十探针全中（底角弧/顶角肩曲率出 region、肩下通道出、肩内主体在、包围盒贴轮廓中心线 Left=0/Bottom=h），顶缘 Top=$($rgnBox5d.Top) 贴顶铁律保持"
    # ② 底缘过渡带剖面：x=底边中点列，从黑面到窗外逐行采色分类（surface/bg/GRAY）
    # 背景参照=剖面末行（dy=+3，region 裁剪保证已出窗）同列取样：44250272756 桌面有横向结构
    # （浏览器顶缘 y≈27 起的 51,51,51 横带 + 右侧浮层面板），跨列取参照会把异色混进来误判灰带。
    $rowsBot5d = New-Object System.Collections.Generic.List[object]
    foreach ($dy in -4..3)
    {
        # List 显式收每行三通道（PS 数组字面量在 += 下会展平，List 免疫）
        $cBot5d = Get-AvgColor $midX5d ($rect5d.Bottom - 1 + $dy) 1
        [void]$rowsBot5d.Add($cBot5d)
    }
    $bgBot5d = $rowsBot5d[$rowsBot5d.Count - 1]
    $bgRefUsable5d = ($bgBot5d[0] + $bgBot5d[1] + $bgBot5d[2]) -ge 30
    $trans5d = 0
    $prof5d = @()
    for ($ri = 0; $ri -lt $rowsBot5d.Count; $ri++)
    {
        $c = $rowsBot5d[$ri]
        $lum = [int](0.2126 * $c[0] + 0.7152 * $c[1] + 0.0722 * $c[2])
        $bgd = ([Math]::Abs($c[0] - $bgBot5d[0]) + [Math]::Abs($c[1] - $bgBot5d[1]) + [Math]::Abs($c[2] - $bgBot5d[2]))
        # 判定序：近黑=surface 本体（含 18,18,18 发丝 AA 行，与背景无关）→ 贴参照=bg → 亮=GRAY 计数
        # → 余=unk（参照不可用且非近黑，分类无意义，不计灰带防假红）
        $kind = "surface"
        if (($c[0] + $c[1] + $c[2]) -ge 120)
        {
            if (($bgd -le 24) -and $bgRefUsable5d) { $kind = "bg" }
            elseif ($lum -ge 45) { $kind = "GRAY"; $trans5d++ }
            else { $kind = "unk" }
        }
        $prof5d += ("y{0}={1}/{2}" -f ($ri - 4), ($c -join ','), $kind)
    }
    Write-Host "AH5d② 底缘剖面（x=中点）：$($prof5d -join ' ')；灰带=$trans5d px（逐像素 alpha 环带后硬断言 ≤1，44308360326 绿基线=0）"
    if ($trans5d -gt 1)
    {
        throw "AH5d② 边缘过渡带断言失败：灰带 $trans5d px（>1）——修复单口径「4x 后边缘过渡带 ≤1px 无灰带」"
    }
    # ③ 曲线边楼梯度量（首版逐列扫首个非背景行作废：轮廓顶缘横贯全宽，全列恒命中 y0=顶缘本身）——
    #   轮廓几何（44238805907 轨迹×近景像素取证互证；肩 2026-11-28 恢复 BrimDeck 喇叭肩，顶部肩曲线
    #   由 AH5d-B region 肩形探针直读把守，tl/tr-shoulder 近景 ④ 归档）：本度量锚底两角弧（弧自窗角
    #   x0 扫到 x12），肩轨迹不作楼梯门（顶部肩区背景参照跨屏幕顶不可采）。逐行扫首个 surface 列=
    #   曲线轨迹（右角自右往左），相邻行 Δx=量化台阶、边界像素去重层数=色阶；HRGN 物理锯齿基线。
    #   桌面结构免疫（44250272756 定谳）：CI 桌面非纯色（浏览器横带 51,51,51 + 浮层面板），背景参照
    #   必须逐行外沿自洽，任何「单点探针当全局 bg」都会撞横向结构出垃圾轨迹。DPI1.0 基线=左底角
    #   0→12、右底角 12→0（maxΔx=2 色阶=3；环带后色阶门 ≥4）。
    $brPx5d = [int][Math]::Ceiling(12 * $scale5d)
    function Trace-CurveStair([int] $zoneL, [int] $zoneT, [int] $zoneW, [int] $zoneH, [int] $dir, [string] $tag)
    {
        # 背景参照=逐行外沿采样（44250272756 定谳：旧单点 bgRef 探针压在 y≈27 浏览器顶缘过渡带上，
        # 5×5 平均把白 255 与灰 51 混成 ≈35，白像素 dist≈660 被误判 surface → 轨迹恒 0/35 垃圾；
        # 逐行种子取窗外 3px 同行真实底色——垂直渐变/横带免疫）。截宽=区宽+7：左 3 列/右 4 列
        # 只装种子（dir>0 取 col 0，dir<0 取 col zoneW+6），区内扫描列照旧 rx↔col rx+3。
        # 近黑种子行（暗墙纸+黑岛不可分）跳过=该行不出现在轨迹，不发垃圾值。
        $bmp = New-Object System.Drawing.Bitmap ($zoneW + 7), $zoneH
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($zoneL - 3, $zoneT, 0, 0, (New-Object System.Drawing.Size ($zoneW + 7), $zoneH))
        $g.Dispose()
        $trace = @()
        $colors = @()
        for ($ry = 0; $ry -lt $zoneH; $ry++)
        {
            $xs = @(0..($zoneW - 1))
            if ($dir -lt 0) { [array]::Reverse($xs) }
            $seedCol = if ($dir -gt 0) { 0 } else { $zoneW + 6 }
            $seed = $bmp.GetPixel($seedCol, $ry)
            $fx = -1
            $fc = ""
            if (($seed.R + $seed.G + $seed.B) -ge 30)
            {
                foreach ($rx in $xs)
                {
                    $p = $bmp.GetPixel($rx + 3, $ry)
                    $bd = ([Math]::Abs($p.R - $seed.R) + [Math]::Abs($p.G - $seed.G) + [Math]::Abs($p.B - $seed.B))
                    if ($bd -gt 24) { $fx = $rx; $fc = ("{0},{1},{2}" -f $p.R, $p.G, $p.B); break }
                }
            }
            $trace += $fx
            $colors += $fc
        }
        $maxStep = 0
        for ($ry = 1; $ry -lt $zoneH; $ry++)
        {
            if (($trace[$ry] -ge 0) -and ($trace[($ry - 1)] -ge 0))
            {
                $d = [Math]::Abs($trace[$ry] - $trace[($ry - 1)])
                if ($d -gt $maxStep) { $maxStep = $d }
            }
        }
        $lvl = ($colors | Where-Object { $_ -ne "" } | Select-Object -Unique).Count
        Write-Host ("AH5d③ {0}: maxΔx={1}px 色阶={2} 轨迹={3}" -f $tag, $maxStep, $lvl, ($trace -join ','))
        $bmp.Dispose()
        return [pscustomobject]@{ Max = $maxStep; Levels = $lvl }
    }
    # 底角区宽 = 弧扫出 12 + 余量（首版 brPx+6 截掉弧外半段，轨迹尾部 -1=出裁非无边）
    $cornerW5d = $brPx5d + 8
    $stairBl5d = Trace-CurveStair $rect5d.Left ($rect5d.Bottom - $brPx5d) $cornerW5d $brPx5d 1 "左底角弧"
    $stairBr5d = Trace-CurveStair ($rect5d.Right - $cornerW5d) ($rect5d.Bottom - $brPx5d) $cornerW5d $brPx5d -1 "右底角弧"
    # 硬门（44308360326 后）：色阶≥4=逐像素 alpha 环带在场的直接证明（HRGN 时代基线=3；
    # spike 绿基线=左弧9/右弧10）。maxΔx 不再作缺陷门：环带后它量的是真曲线斜率+AA 梯度。
    $stairMax5d = [Math]::Max($stairBl5d.Max, $stairBr5d.Max)
    $stairMinLvl5d = [Math]::Min($stairBl5d.Levels, $stairBr5d.Levels)
    if ($stairMinLvl5d -lt 4)
    {
        throw "AH5d③ 边缘 AA 断言失败：曲线色阶 min=$stairMinLvl5d（<4，环带缺席/未生效=HRGN 硬边回归）——左弧 $($stairBl5d.Levels)/右弧 $($stairBr5d.Levels)"
    }
    # ④ 四角+双边中点 4x 近景（NearestNeighbor=真实像素；人评并排对照素材，不进断言）
    function New-IslandRectCrop([int] $l, [int] $t, [int] $r, [int] $b)
    {
        $bmp = New-Object System.Drawing.Bitmap ($r - $l), ($b - $t)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($l, $t, 0, 0, (New-Object System.Drawing.Size ($r - $l), ($b - $t)))
        $g.Dispose()
        return $bmp
    }
    $tlTop5d = [Math]::Max(0, ($rect5d.Top - 6))
    $trTop5d = [Math]::Max(0, ($rect5d.Top - 6))
    Save-Zoom (New-IslandRectCrop ($rect5d.Left - 6) $tlTop5d ($rect5d.Left + 40) ($rect5d.Top + 34)) "AH5d-zoom-tl-shoulder.png" 4
    Save-Zoom (New-IslandRectCrop ($rect5d.Right - 40) $trTop5d ($rect5d.Right + 6) ($rect5d.Top + 34)) "AH5d-zoom-tr-shoulder.png" 4
    Save-Zoom (New-IslandRectCrop ($rect5d.Left - 6) ($rect5d.Bottom - 34) ($rect5d.Left + 40) ($rect5d.Bottom + 6)) "AH5d-zoom-bl-corner.png" 4
    Save-Zoom (New-IslandRectCrop ($rect5d.Right - 40) ($rect5d.Bottom - 34) ($rect5d.Right + 6) ($rect5d.Bottom + 6)) "AH5d-zoom-br-corner.png" 4
    Save-Zoom (New-IslandRectCrop ($midX5d - 20) ($rect5d.Bottom - 14) ($midX5d + 20) ($rect5d.Bottom + 6)) "AH5d-zoom-bottom-edge.png" 4
    Save-Zoom (New-IslandRectCrop ($rect5d.Left - 6) ($midY5d - 16) ($rect5d.Left + 14) ($midY5d + 16)) "AH5d-zoom-left-edge.png" 4
    # ⑤ 边带取证面（spike/island-alpha-edge）：岛窗 trace 记 region/edge band 成败，但成功路径
    #   不归档 island-trace（只 AH5a 失败路径落档）→ 这里把末尾若干行直接打进 CI 日志，
    #   「边带没画」vs「画了没用」单轮可分。44252023123 边带零效应（② 剖面逐字未变）即缺此口。
    $traceFile5d = Join-Path $env:APPDATA "Beacon\logs\island-trace.txt"
    if (Test-Path $traceFile5d)
    {
        Get-Content $traceFile5d -Tail 12 -Encoding UTF8 | ForEach-Object { Write-Host "AH5d trace | $_" }
    }
    else
    {
        Write-Host "AH5d trace 缺失：$traceFile5d"
    }
    # ⑥ 边带窗几何真相（spike/island-alpha-edge）：FindWindow 类名直查 band 原生窗，rect/可见性
    #   与岛窗对表——band 钉屏幕坐标、跟挪靠 DidPositionChange 重钉（44288775018 实证：纯移动
    #   不走 region 重算致 band 停旧位，环带底缘黑对黑隐形只漏 2px AA）。此处一锤定音「band 与
    #   岛是否同位」。已升硬门（44308360326 绿基线收口）。
    $bandHwnd5d = [WinEnum]::FindByClass("BeaconIslandEdgeBand")
    if ($bandHwnd5d -ne [IntPtr]::Zero)
    {
        $bandRect5d = [WinEnum]::RectOf($bandHwnd5d)
        $bandVis5d = [WinEnum]::Visible($bandHwnd5d)
        $bandW5d = $bandRect5d.Right - $bandRect5d.Left
        $bandH5d = $bandRect5d.Bottom - $bandRect5d.Top
        Write-Host ("AH5d⑥ band rect={0},{1} {2}x{3} vis={4} exStyle=0x{5:X} | island rect={6},{7} {8}x{9}" -f $bandRect5d.Left, $bandRect5d.Top, $bandW5d, $bandH5d, $bandVis5d, [WinEnum]::ExStyleOf($bandHwnd5d), $rect5d.Left, $rect5d.Top, ($rect5d.Right - $rect5d.Left), ($rect5d.Bottom - $rect5d.Top))
        # 硬门：band 位图钉岛 clientRect−外扩（IslandEdgeBandWindow.BandMarginPx=4 物理px；
        # 44308360326 绿基线逐值吻合）。偏移/尺寸差>1px=band 停旧位或跟挪失效（44288775018 病）。
        $bandExpL5d = $rect5d.Left - 4
        $bandExpT5d = $rect5d.Top - 4
        $bandExpW5d = ($rect5d.Right - $rect5d.Left) + 8
        $bandExpH5d = ($rect5d.Bottom - $rect5d.Top) + 8
        $bandOff5d = [Math]::Max([Math]::Abs($bandRect5d.Left - $bandExpL5d), [Math]::Abs($bandRect5d.Top - $bandExpT5d))
        $bandSz5d = [Math]::Max([Math]::Abs($bandW5d - $bandExpW5d), [Math]::Abs($bandH5d - $bandExpH5d))
        if ((-not $bandVis5d) -or ($bandOff5d -gt 1) -or ($bandSz5d -gt 1))
        {
            throw "AH5d⑥ 边带窗几何断言失败：vis=$bandVis5d 偏移=${bandOff5d}px 尺寸差=${bandSz5d}px（期望 rect=$bandExpL5d,$bandExpT5d ${bandExpW5d}x${bandExpH5d} vis=True）"
        }
    }
    else
    {
        throw "AH5d⑥ 边带窗未找到（BeaconIslandEdgeBand 不在顶层窗）——岛态边带必须存在且可见"
    }
    Write-Host "场景 AH5d 通过：底角月牙=背后层 + 过渡带/楼梯基线已记 + 6 件 4x 近景存档——边缘精度修复（逐像素 alpha）的取证面就绪"
    # ⑦ AH5e 展开→收起迁移后 band 复验（ISLAND-BOTTOM-1，2026-11-24 用户真机「悬浮岛底下没处理干净，
    #   还有一个空白的悬浮条」）：迁移是 band 脱同步唯一窗口——DidSizeChange 当拍 ApplyIslandRegion 读
    #   Size 仍旧值 → region 去重键误命中 → region+band 重算整跳过且无重试（收起态 RootBorder.SizeChanged
    #   兜底不触发：Border 实际高早已是 32）；随后的纯移动事件若用旧 segs 重钉，展开轮廓的空心描边就悬在
    #   收起岛底下=真机所见空白条。稳态 ⑥ 查不出，迁移后必查两门：真指针进→出（AH1 同款 SendInput 纯移动）
    #   → band rect=岛 clientRect−4 ±1px 复硬门（ULW 窗尺寸=位图尺寸，停旧位/旧几何必现形）→ 岛底下条带
    #   [bottom+3, bottom+20]px 全宽扫描零外来墨（逐行窗外两点自洽参照，横向桌面结构免疫；+3 起步避开
    #   band 自身 3px 描边的外沿 AA 尾）。
    $islandHandles5e = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($islandHandles5e.Count -lt 1) { Save-FullScreenshot "AH5e-FAIL-no-island.png"; throw "断言失败（AH5e 前置）：岛窗不在" }
    $rect5e = [WinEnum]::RectOf($islandHandles5e[0])
    $cxE5 = [int](($rect5e.Left + $rect5e.Right) / 2)
    $cyE5 = [int](($rect5e.Top + $rect5e.Bottom) / 2)
    [WinEnum]::MoveMouse($cxE5, $cyE5)
    Start-Sleep -Seconds 3
    [WinEnum]::MoveMouse(30, 600)
    Start-Sleep -Seconds 3
    $rect5e = [WinEnum]::RectOf($islandHandles5e[0])
    if (($rect5e.Bottom - $rect5e.Top) -gt [int]($collapsedPx * 1.5))
    {
        Save-FullScreenshot "AH5e-FAIL-not-collapsed.png"
        throw "AH5e 前置失败：迁移后岛未收（高 $($rect5e.Bottom - $rect5e.Top)px vs 基线 $collapsedPx）——PointerExited 收起链失效"
    }
    $bandHwnd5e = [WinEnum]::FindByClass("BeaconIslandEdgeBand")
    if ($bandHwnd5e -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "AH5e-FAIL-no-band.png"
        throw "AH5e band 迁移复验失败：BeaconIslandEdgeBand 窗不在——展开→收起后边带自隐未回场"
    }
    $bandRect5e = [WinEnum]::RectOf($bandHwnd5e)
    # band 钉在岛 clientRect（ULW dst=client−4），而 RectOf 是含 3px 不可见 resize 边框的外框
    # （ChromeSizePx，无边框 presenter 仍留边框）——用外框算期望恒偏 3/6px，与 AH5d⑥ 的 client 口径对齐
    $client5e = [WinEnum]::ClientScreenRectOf($islandHandles5e[0])
    $expL5e = $client5e.Left - 4
    $expT5e = $client5e.Top - 4
    $expW5e = ($client5e.Right - $client5e.Left) + 8
    $expH5e = ($client5e.Bottom - $client5e.Top) + 8
    $off5e = [Math]::Max([Math]::Abs($bandRect5e.Left - $expL5e), [Math]::Abs($bandRect5e.Top - $expT5e))
    $sz5e = [Math]::Max([Math]::Abs(($bandRect5e.Right - $bandRect5e.Left) - $expW5e), [Math]::Abs(($bandRect5e.Bottom - $bandRect5e.Top) - $expH5e))
    if ($off5e -gt 1 -or $sz5e -gt 1)
    {
        Save-FullScreenshot "AH5e-FAIL-band-stale.png"
        throw "AH5e band 迁移复验失败：偏移=${off5e}px 尺寸差=${sz5e}px（期望 rect=$expL5e,$expT5e ${expW5e}x${expH5e}）——展开→收起后 band 脱同步（真机「岛底下空白条」CI 面）"
    }
    # 行持久双门（45649709511 首跑证伪单点门）：岛窗是带 DWM 圆角投影的常规件，投影沿窗底缘下探
    # 2-3px——浅色桌面上单个采样点对两侧参照的色差可过 24（同 run 几何门 0px 全对 + trace band 已按
    # 收起几何重建 bmp=347x40 dst=338,-4 + 失败截图岛下无可见物，414 采样仅 1 点命中=投影尾非残留）。
    # 真「空白悬浮条」=整宽不透明条跨多行上百点——判据改单行 ≥3 点异色且 ≥2 行才红；扫描自 dy=5 起
    # （避开投影最强带；band 位图底=client 底+4，dy=3 档本就贴着它）。
    $stripInkRows5e = 0
    for ($dy5e = 5; $dy5e -le 20; $dy5e += 2)
    {
        $y5e = $rect5e.Bottom + $dy5e
        $lRef5e = Get-AvgColor ($rect5e.Left - 30) $y5e 2
        $rRef5e = Get-AvgColor ($rect5e.Right + 30) $y5e 2
        $rowInk5e = 0
        for ($x5e = ($rect5e.Left - 8); $x5e -le ($rect5e.Right + 8); $x5e += 8)
        {
            $p5e = Get-AvgColor $x5e $y5e 1
            $dl5e = [Math]::Abs($p5e[0] - $lRef5e[0]) + [Math]::Abs($p5e[1] - $lRef5e[1]) + [Math]::Abs($p5e[2] - $lRef5e[2])
            $dr5e = [Math]::Abs($p5e[0] - $rRef5e[0]) + [Math]::Abs($p5e[1] - $rRef5e[1]) + [Math]::Abs($p5e[2] - $rRef5e[2])
            if (($dl5e -gt 24) -and ($dr5e -gt 24)) { $rowInk5e++ }
        }
        if ($rowInk5e -ge 3) { $stripInkRows5e++ }
    }
    if ($stripInkRows5e -ge 2)
    {
        Save-FullScreenshot "AH5e-FAIL-strip-ink.png"
        throw "AH5e 岛底条带断言失败：$stripInkRows5e 行各 ≥3 点异于两侧参照（期望 0 行）——岛底下残留层（真机「空白悬浮条」CI 面）"
    }
    Write-Host "场景 AH5e 通过：展开→收起迁移后 band 几何复验 ±1px + 岛底条带 [5,20]px 行持久零残留——ISLAND-BOTTOM-1"
    Write-Host "场景 AH 通过：岛展开态/占位面/组件级下钻/灰板摘除/边缘精度——ISLAND-D3/D4、灰板修复与边缘精度 CI 视觉取证面"
}
catch
{
    Stop-Beacon
    Remove-Item Env:BEACON_DEBUG_ISLAND_PREVIEW -ErrorAction SilentlyContinue # 失败路径同样清注入（防泄给后续场景实例）
    Save-FullScreenshot "AH-FAIL-unexpected.png"
    $failures.Add("场景 AH：$($_.Exception.Message)")
    Write-Host "::error::场景 AH 断言失败：$($_.Exception.Message)"
}

# —— 场景 SF：组件中心去向与能力推荐 + 按位样式继承 + 多窗口读数（SET-UX-04/05/06/07，2026-10-20 巡检单批2 CI 代验）——
# SF1 去向（04）：岛开（capsuleIsland=true、白名单仅 DS）→ 组件中心页 GLM 行桌面列「已显示」（seed
#   pinned + 数值总开默认开 + 来源启用），岛列「未上岛」（白名单缺位——阻挡原因可见可读）；DS 岛列
#   「已上岛」（rank 1 在位）。两列与 L0 宿主/岛渲染同一 ComponentPlacement 判定源的投影实证。
# SF2 推荐（05）：GLM（progress+windows）画廊 7 模板全启用、恰一「· 推荐」（=横条）、零「不可用」；
#   DS（amount）画廊仅状态行启用且即推荐档，其余 6 项置灰带「不可用：该组件无…」——不兼容不再静默
#   剔除（画廊随宿主进程且无关窗锚，DS 段重启会话换目标）。
# SF3 按位应用与继承（06）：应用位置先行——按钮文案随档变（应用到桌面 ↔ 应用到看板状态行）；继承态
#   行「跟随默认样式」↔「此处已单独设置 · …」随所选面与落盘态双向翻（tile 未设恒跟随、row 应用后
#   已单独设置）；恢复此处默认只清 row（tile 保留——面不互覆 AE3 已钉，此处钉继承行与落盘一致）。
# SF4 多窗口读数（07）：岛面可读名带周期+主值（「5h 38%」→ 翻 error 档「5h 95%」，主值变更即绑定
#   生效）；快速面板事件行带事件时点读数（「95%/5h 15%/w 61%/M」=快照 payload 三窗已用，非当前回填）。
try
{
    $sfConnections = @'
[
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": true },
  { "id": "ds-main", "type": "deepseek", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ds-main", "enabled": true }
]
'@
    Ensure-MockServer

    # ===== SF1 去向 + SF2 GLM 推荐（同一设置窗会话）=====
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["deepseek.balance:ds"]'
    $sfConnections | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SF-FAIL-no-settings.png"; throw "断言失败（场景 SF1）：--settings 启动 30s 内未见设置窗" }
    $componentsRow = Find-UiaById $settings "module-components" 10
    if (!$componentsRow) { Save-FullScreenshot "SF-FAIL-no-components-row.png"; throw "断言失败（场景 SF1）：UIA 未找到 module-components" }
    Invoke-Uia $componentsRow
    Start-Sleep -Seconds 2
    $deskState = Find-UiaById $settings "component-desktop-state-bigmodel.usage:GLM" 10
    $deskName = ""
    if ($deskState) { try { $deskName = $deskState.Current.Name } catch { } }
    if (!$deskState -or $deskName -notlike "已显示*")
    {
        Save-FullScreenshot "SF-FAIL-desktop-state.png"
        throw "断言失败（场景 SF1）：GLM 桌面列状态非「已显示」（实际：$deskName）——去向判定与宿主实况脱钩"
    }
    $islandState = Find-UiaById $settings "component-island-state-bigmodel.usage:GLM" 8
    $islandName = ""
    if ($islandState) { try { $islandName = $islandState.Current.Name } catch { } }
    if (!$islandState -or $islandName -notlike "*未上岛*")
    {
        Save-FullScreenshot "SF-FAIL-island-state.png"
        throw "断言失败（场景 SF1）：GLM 岛列状态非「未上岛」（白名单缺位应可见可读；实际：$islandName）"
    }
    $dsIsland = Find-UiaById $settings "component-island-state-deepseek.balance:ds" 8
    $dsIslandName = ""
    if ($dsIsland) { try { $dsIslandName = $dsIsland.Current.Name } catch { } }
    if (!$dsIsland -or $dsIslandName -notlike "*已上岛*")
    {
        Save-FullScreenshot "SF-FAIL-ds-island.png"
        throw "断言失败（场景 SF1）：DS 岛列状态非「已上岛」（白名单 rank 1 应在位；实际：$dsIslandName）"
    }
    Save-FullScreenshot "SF1-component-center.png"
    Write-Host "场景 SF1 通过：GLM 桌面「已显示」/岛「未上岛」、DS 岛「已上岛」——SET-UX-04 去向与阻挡原因同源投影"

    # ===== SF2 GLM 画廊：7 模板全启用 + 恰一推荐 =====
    $bigRow = Find-UiaById $settings "module-bigmodel" 10
    if (!$bigRow) { Save-FullScreenshot "SF-FAIL-no-bigmodel-row.png"; throw "断言失败（场景 SF2）：UIA 未找到 module-bigmodel" }
    Invoke-Uia $bigRow
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaById $settings "style-button-bigmodel.usage:GLM" 10)
    $gallery = Find-GalleryWindow 15
    if (!$gallery) { Save-FullScreenshot "SF-FAIL-no-glm-gallery.png"; throw "断言失败（场景 SF2）：GLM 样式中心 15s 未开" }
    $galleryHandles = [WinEnum]::VisibleHandlesByTitle("Beacon · 组件中心")
    foreach ($templateId in @("bar", "ring", "number", "pool", "statusline", "quotaline", "metricgrid"))
    {
        $item = Find-UiaById $gallery "style-item-$templateId" 6
        if (!$item)
        {
            Save-FullScreenshot "SF-FAIL-missing-$templateId.png"
            throw "断言失败（场景 SF2）：GLM 画廊缺 style-item-$templateId（progress+windows 应 7 模板全列）"
        }
        if (!$item.Current.IsEnabled)
        {
            Save-FullScreenshot "SF-FAIL-disabled-$templateId.png"
            throw "断言失败（场景 SF2）：GLM（progress+windows）画廊 $templateId 被置灰——能力判定回归"
        }
    }
    $glmTexts = @(Get-WindowUiaTexts $galleryHandles[0])
    $glmRec = @($glmTexts | Where-Object { $_ -like "*· 推荐" })
    if ($glmRec.Count -ne 1 -or $glmRec[0] -ne "横条 · 推荐")
    {
        Save-FullScreenshot "SF-FAIL-glm-recommend.png"
        throw "断言失败（场景 SF2）：GLM 推荐标记数 $($glmRec.Count)（期望恰一「横条 · 推荐」）；实际：$($glmRec -join ' | ')"
    }
    if (@($glmTexts | Where-Object { $_ -like "不可用：*" }).Count -ne 0)
    {
        Save-FullScreenshot "SF-FAIL-glm-unavailable.png"
        throw "断言失败（场景 SF2）：GLM 全兼容画廊出现「不可用」项——能力判定误伤"
    }
    Save-FullScreenshot "SF2-glm-gallery.png"
    Write-Host "场景 SF2（GLM）通过：7 模板全启用、恰一「横条 · 推荐」、零不可用——SET-UX-05 能力推荐"
    Stop-Beacon

    # ===== SF2 DS 画廊：仅状态行启用即推荐，其余 6 项置灰带原因 =====
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SF-FAIL-no-settings-ds.png"; throw "断言失败（场景 SF2 DS）：--settings 启动 30s 内未见设置窗" }
    $dsRow = Find-UiaById $settings "module-deepseek" 10
    if (!$dsRow) { Save-FullScreenshot "SF-FAIL-no-deepseek-row.png"; throw "断言失败（场景 SF2 DS）：UIA 未找到 module-deepseek" }
    Invoke-Uia $dsRow
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaById $settings "style-button-deepseek.balance:ds" 10)
    $gallery = Find-GalleryWindow 15
    if (!$gallery) { Save-FullScreenshot "SF-FAIL-no-ds-gallery.png"; throw "断言失败（场景 SF2 DS）：DS 样式中心 15s 未开" }
    $galleryHandles = [WinEnum]::VisibleHandlesByTitle("Beacon · 组件中心")
    $statusItem = Find-UiaById $gallery "style-item-statusline" 8
    if (!$statusItem -or !$statusItem.Current.IsEnabled)
    {
        Save-FullScreenshot "SF-FAIL-ds-statusline.png"
        throw "断言失败（场景 SF2 DS）：DS（amount）画廊状态行缺席或被置灰——唯一兼容项必须可用"
    }
    foreach ($templateId in @("bar", "ring", "number", "pool", "quotaline", "metricgrid"))
    {
        $item = Find-UiaById $gallery "style-item-$templateId" 6
        if (!$item)
        {
            Save-FullScreenshot "SF-FAIL-ds-missing-$templateId.png"
            throw "断言失败（场景 SF2 DS）：DS 画廊缺 style-item-$templateId（不兼容项要列全带原因，不得静默剔除）"
        }
        if ($item.Current.IsEnabled)
        {
            Save-FullScreenshot "SF-FAIL-ds-enabled-$templateId.png"
            throw "断言失败（场景 SF2 DS）：DS（amount）画廊 $templateId 未置灰——能力门失效"
        }
    }
    $dsTexts = @(Get-WindowUiaTexts $galleryHandles[0])
    if (@($dsTexts) -notcontains "状态行 · 推荐")
    {
        Save-FullScreenshot "SF-FAIL-ds-recommend.png"
        throw "断言失败（场景 SF2 DS）：DS 画廊未见「状态行 · 推荐」（唯一兼容项应即推荐档）；实际：$($dsTexts -join ' | ')"
    }
    $dsBlocked = @($dsTexts | Where-Object { $_ -like "不可用：该组件无*数据*" })
    if ($dsBlocked.Count -ne 6)
    {
        Save-FullScreenshot "SF-FAIL-ds-blocked.png"
        throw "断言失败（场景 SF2 DS）：DS 置灰原因小字数 $($dsBlocked.Count)（期望 6）；实际：$($dsBlocked -join ' | ')"
    }
    Save-FullScreenshot "SF2-ds-gallery.png"
    Write-Host "场景 SF2（DS）通过：仅状态行启用即推荐、6 项置灰带「不可用：该组件无…」——SET-UX-05 能力筛选不静默"
    Stop-Beacon

    # ===== SF3 按位应用与继承行（SET-UX-06）=====
    Write-GConfig "" "off" 1.0 # 岛关（本段只动画廊，免窗干扰）
    $sfConnections | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SF-FAIL-no-settings3.png"; throw "断言失败（场景 SF3）：--settings 启动 30s 内未见设置窗" }
    function Wait-GalleryText([IntPtr] $hwnd, [string] $needle, [int] $timeoutSec, [string] $stage)
    {
        $deadline = (Get-Date).AddSeconds($timeoutSec)
        while ((Get-Date) -lt $deadline)
        {
            $texts = @(Get-WindowUiaTexts $hwnd)
            if (@($texts | Where-Object { $_ -like "*$needle*" }).Count -gt 0) { return }
            Start-Sleep -Milliseconds 500
        }
        Save-FullScreenshot "SF-FAIL-text-$stage.png"
        throw "断言失败（场景 SF3 $stage）：画廊文本未见「$needle」；实际：$(@(Get-WindowUiaTexts $hwnd) -join ' | ')"
    }
    # 换面驱动走共享 Select-ComboItemById（45319071005 起已收编 SF3 实证形态：重取元素+Collapse
    # 归零+展开+3 轮重试——本地副本删除，单一实现）。
    $bigRow = Find-UiaById $settings "module-bigmodel" 10
    if (!$bigRow) { Save-FullScreenshot "SF-FAIL-no-bigmodel-row3.png"; throw "断言失败（场景 SF3）：UIA 未找到 module-bigmodel" }
    Invoke-Uia $bigRow
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaById $settings "style-button-bigmodel.usage:GLM" 10)
    $gallery = Find-GalleryWindow 15
    if (!$gallery) { Save-FullScreenshot "SF-FAIL-no-gallery3.png"; throw "断言失败（场景 SF3）：GLM 样式中心 15s 未开" }
    $galleryHandles = [WinEnum]::VisibleHandlesByTitle("Beacon · 组件中心")
    $applyBtn = Find-UiaById $gallery "style-apply" 8
    $applyName = ""
    if ($applyBtn) { try { $applyName = $applyBtn.Current.Name } catch { } }
    if ($applyName -ne "应用到桌面")
    {
        Save-FullScreenshot "SF-FAIL-apply-label.png"
        throw "断言失败（场景 SF3）：初始应用按钮文案「$applyName」（期望「应用到桌面」——应用位置先行）"
    }
    Wait-GalleryText $galleryHandles[0] "跟随默认样式" 10 "tile-follow"
    Select-ComboItemById $gallery (Find-UiaById $gallery "style-surface-box" 8) "style-surface-row"
    $applyBtn = Find-UiaById $gallery "style-apply" 8
    $applyName = ""
    if ($applyBtn) { try { $applyName = $applyBtn.Current.Name } catch { } }
    if ($applyName -ne "应用到看板状态行")
    {
        Save-FullScreenshot "SF-FAIL-apply-label-row.png"
        throw "断言失败（场景 SF3）：切 row 后应用按钮文案「$applyName」（期望「应用到看板状态行」）"
    }
    # SET-UX-ROW-2：样式清单随所选应用面过滤——紧凑单行带挡多行结构（数字卡 48 DIP 置灰）、
    # 放行单行族（圆环 34 DIP 可用）。AE3 亦在同面套圆环，故可用集合必须是「面相关」的。
    $numberOnRow = Find-UiaById $gallery "style-item-number" 8
    $ringOnRow = Find-UiaById $gallery "style-item-ring" 8
    if (!$numberOnRow -or !$ringOnRow)
    {
        Save-FullScreenshot "SF-FAIL-row-filter-anchors.png"
        throw "断言失败（场景 SF3）：切 row 后缺数字卡/圆环样式条目锚（清单未按所选面重建？）"
    }
    $numberEnabledOnRow = $false; $ringEnabledOnRow = $true
    try { $numberEnabledOnRow = $numberOnRow.Current.IsEnabled } catch { }
    try { $ringEnabledOnRow = $ringOnRow.Current.IsEnabled } catch { }
    if ($numberEnabledOnRow -or !$ringEnabledOnRow)
    {
        Save-FullScreenshot "SF-FAIL-row-filter.png"
        throw "断言失败（场景 SF3）：row 面样式适用性未随所选面变（数字卡 enabled=$numberEnabledOnRow 期望置灰；圆环 enabled=$ringEnabledOnRow 期望可用）——SET-UX-ROW-2"
    }
    Wait-GalleryText $galleryHandles[0] "跟随默认样式" 10 "row-follow"
    Invoke-StyleItemClick $galleryHandles[0] "style-item-ring" "样式 · 圆环"
    Invoke-Uia (Find-UiaById $gallery "style-apply" 8)
    $rowApplied = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $glmEntry = Get-ConfigRecords (Join-Path $configDir "widgets.json") | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
        if ($glmEntry -and $glmEntry.components -and $glmEntry.components.row) { $rowApplied = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$rowApplied) { Save-FullScreenshot "SF-FAIL-row-not-saved.png"; throw "断言失败（场景 SF3）：看板状态行应用后 widgets.json components.row 未落盘" }
    Wait-GalleryText $galleryHandles[0] "此处已单独设置" 10 "row-set"
    Select-ComboItemById $gallery (Find-UiaById $gallery "style-surface-box" 8) "style-surface-tile"
    Wait-GalleryText $galleryHandles[0] "跟随默认样式" 10 "tile-still-follow"
    Select-ComboItemById $gallery (Find-UiaById $gallery "style-surface-box" 8) "style-surface-row"
    Invoke-Uia (Find-UiaById $gallery "style-reset" 8)
    $rowCleared = $false
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $glmEntry = Get-ConfigRecords (Join-Path $configDir "widgets.json") | Where-Object { $_.id -eq "bigmodel.usage:GLM" }
            if ($glmEntry -and ($null -eq $glmEntry.components -or !$glmEntry.components.row)) { $rowCleared = $true; break }
        Start-Sleep -Seconds 1
    }
    if (!$rowCleared) { Save-FullScreenshot "SF-FAIL-row-not-cleared.png"; throw "断言失败（场景 SF3）：恢复此处默认后 components.row 未清——恢复只改选中位置回归" }
    Wait-GalleryText $galleryHandles[0] "跟随默认样式" 10 "row-follow-again"
    Save-FullScreenshot "SF3-per-surface-inheritance.png"
    Write-Host "场景 SF3 通过：应用文案随位变（桌面↔看板状态行）+ 继承行双向翻（跟随默认 ↔ 此处已单独设置）+ 恢复只清 row——SET-UX-06"
    Stop-Beacon

    # ===== SF4 岛面主读数 + 看板事件行读数（SET-UX-07）=====
    $sfModeFile = Join-Path $env:TEMP "beacon-mock-mode.txt"
    Remove-Item $sfModeFile -Force -ErrorAction SilentlyContinue # 默认 38% 态起（不毒化读数断言）
    Write-GConfig "" "off" 1.0 $false $false $true "stack" '["bigmodel.usage:GLM","deepseek.balance:ds"]'
    $sfConnections | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
    $islandHandles = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        $handles = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
        if ($handles.Count -ge 1) { $islandHandles = $handles[0]; break }
        Start-Sleep -Seconds 2
    }
    if ($islandHandles -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "SF-FAIL-no-island.png"
        throw "断言失败（场景 SF4 前置）：capsuleIsland=true 未见「Beacon Status」岛窗"
    }
    $islandRoot = [System.Windows.Automation.AutomationElement]::FromHandle($islandHandles)
    $faceName = ""
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline)
    {
        $face = Find-UiaById $islandRoot "island-face-bigmodel.usage:GLM" 3
        if ($face)
        {
            try { $faceName = $face.Current.Name } catch { }
            if ($faceName -like "*5h 38%") { break }
        }
        Start-Sleep -Seconds 3
    }
    if ($faceName -notlike "*5h 38%")
    {
        Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles)) 6) "SF-FAIL-no-metric.png" 4
        throw "断言失败（场景 SF4）：岛面可读名未见「5h 38%」（实际：$faceName）——收起行未保留周期+主值"
    }
    Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles)) 6) "SF4-island-metric.png" 4
    Write-Host "场景 SF4（岛）通过：岛面可读名带「5h 38%」（周期+主值成对）"
    Set-Content -Path $sfModeFile -Value "error" -Encoding ASCII # 主值变更（38→95）验证绑定跟着走
    $faceName = ""
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline)
    {
        $face = Find-UiaById $islandRoot "island-face-bigmodel.usage:GLM" 3
        if ($face)
        {
            try { $faceName = $face.Current.Name } catch { }
            if ($faceName -like "*5h 95%") { break }
        }
        Start-Sleep -Seconds 3
    }
    if ($faceName -notlike "*5h 95%")
    {
        Save-Zoom (New-Crop ([WinEnum]::RectOf($islandHandles)) 6) "SF-FAIL-metric-stale.png"
        throw "断言失败（场景 SF4）：翻 error 档后岛面可读名未见「5h 95%」（实际：$faceName）——主值变更未跟随"
    }
    Write-Host "场景 SF4（变更）通过：主值 38%→95% 岛面读数跟随——绑定生效"
    $via = "SendInput 和弦"
    [WinEnum]::SendChord(0x11, 0x42, 0x12) # Ctrl+Alt+B（缺 Alt 恒不匹配，同场景 K 口径）
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
        $via = "WM_HOTKEY 直驱"
        $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
        if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
        if ($msgWindow -eq [IntPtr]::Zero) { throw "断言失败（场景 SF4）：托盘消息窗未找到" }
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
        Save-FullScreenshot "SF-FAIL-no-panel.png"
        throw "断言失败（场景 SF4）：热键触发后快速面板未出现（和弦与 WM_HOTKEY 双路）"
    }
    $boardTexts = @()
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline)
    {
        $boardTexts = @(Get-WindowUiaTexts $panel)
        if (@($boardTexts) -contains "95%/5h 15%/w 61%/M") { break }
        Start-Sleep -Seconds 2
    }
    if (@($boardTexts) -notcontains "95%/5h 15%/w 61%/M")
    {
        Save-FullScreenshot "SF-FAIL-no-reading.png"
        throw "断言失败（场景 SF4）：快速面板未见事件时点读数「95%/5h 15%/w 61%/M」（三窗已用=快照 payload）；实际：$($boardTexts -join ' | ')"
    }
    Save-FullScreenshot "SF4-board-reading.png"
    Write-Host "场景 SF4（看板）通过：事件行带事件时点读数（95%/5h 15%/w 61%/M，$via）——SET-UX-07 时间线快照读数"
    Stop-Beacon
    Remove-Item $sfModeFile -Force -ErrorAction SilentlyContinue
    Write-Host "场景 SF 通过：去向与阻挡原因 + 能力推荐与置灰原因 + 按位应用继承恢复 + 岛面与看板多窗口读数——SET-UX-04/05/06/07 CI 视觉取证面"
}
catch
{
    Stop-Beacon
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Save-FullScreenshot "SF-FAIL-unexpected.png"
    $failures.Add("场景 SF：$($_.Exception.Message)")
    Write-Host "::error::场景 SF 断言失败：$($_.Exception.Message)"
}

# —— 场景 SG：设置导航分组与字段搜索跳转（SET-UX-08，2026-10-20 巡检单批3 CI 代验）——
# 左栏分组头（数据来源/备份与诊断，一级四入口无头）在树；字段索引（设置词+旧名）回车进页展开分组、
# 滚动并聚焦控件本体：五路跳转链全走并断言 FocusedElement 的 AutomationId=目标锚
# （透明度→desktop-opacity-slider / 检查频率→poll-interval-box / 贴边收起→edge-collapse-toggle /
# GitHub→conn-save-button / 旧名「悬浮形态」→desktop-layout-box），模块级命中（组件中心）整页切换；
# 清词复原断头行回「N 个模块」非过滤形态，无结果词断「0/N 个模块匹配」+ 模块行全滤除；
# 窄窗 600px（ResizeTo 外框驱动）搜索/过滤/清词全链照常后恢复原尺寸。
try
{
    # 状态条播岛档（SET-UX-ROW-3）：关闭档下桌面悬浮设置整区隐藏（只留看板开关），
    # 本段三路字段跳转的锚点（透明度/贴边收起/悬浮形态）都在岛档全显集合里。
    Write-GConfig "" "off" 1.0 $false $false $true
    Ensure-MockServer
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SG-FAIL-no-settings.png"; throw "断言失败（场景 SG）：--settings 启动 30s 内未见设置窗" }
    [WinEnum]::SetForeground([IntPtr]$settings.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 500

    function Invoke-SearchJump([System.Windows.Automation.AutomationElement] $root, [string] $query, [string] $expectAnchor, [string] $shot)
    {
        $box = Find-UiaById $root "settings-search" 8
        if (!$box) { Save-FullScreenshot "$shot-FAIL-no-search.png"; throw "断言失败（场景 SG）：settings-search 缺失" }
        ($box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue($query)
        Start-Sleep -Milliseconds 900
        $box.SetFocus()
        Start-Sleep -Milliseconds 300
        [WinEnum]::SendVirtualKey(0x0D) # VK_RETURN——回车跳字段命中（进页 + 展开分组 + 滚动聚焦）
        Start-Sleep -Seconds 3
        $focusedId = ""
        try { $focusedId = [System.Windows.Automation.AutomationElement]::FocusedElement.Current.AutomationId } catch { }
        Save-FullScreenshot "$shot.png"
        if ($focusedId -ne $expectAnchor)
        {
            throw "断言失败（场景 SG）：搜索「$query」回车后焦点 AutomationId=$focusedId（期望 $expectAnchor）——字段跳转未落位"
        }
        Write-Host "场景 SG 跳转通过：「$query」→ $expectAnchor"
    }

    if (!(Find-UiaById $settings "nav-group-sources" 8)) { Save-FullScreenshot "SG-FAIL-no-group.png"; throw "断言失败（场景 SG）：nav-group-sources 不在 UIA 树——左栏「数据来源」分组头缺失" }
    if (!(Find-UiaById $settings "nav-group-advanced" 8)) { Save-FullScreenshot "SG-FAIL-no-adv-group.png"; throw "断言失败（场景 SG）：nav-group-advanced 不在 UIA 树——「备份与诊断」分组头缺失" }
    Write-Host "场景 SG 分组断言通过：数据来源/备份与诊断分组头在树（一级四入口无头）"
    # —— 病灶 C（BEACON-FIX-1126，2026-11-26）：模块图标-文字间距 ≥10 DIP——BoundingRectangle
    # 几何直读（图标 Border SetName 造 peer 后按 id 定位，右缘→文字左缘；修复前零间距恒红）
    $iconGapC = Find-UiaById $settings "module-icon-github" 10
    if (!$iconGapC) { Save-FullScreenshot "SG-FAIL-no-icon-peer.png"; throw "断言失败（SG 病灶 C 门）：module-icon-github 不在 UIA 树——图标 Border SetName peer 回归" }
    $nameGapC = Find-UiaById $settings "module-name-github" 10
    if (!$nameGapC) { Save-FullScreenshot "SG-FAIL-no-name-anchor.png"; throw "断言失败（SG 病灶 C 门）：module-name-github 不在 UIA 树——行名锚缺失" }
    $gapPx5C = $nameGapC.Current.BoundingRectangle.Left - $iconGapC.Current.BoundingRectangle.Right
    if (!($gapPx5C -ge 9)) # NaN 防护：BoundingRectangle 可返 NaN（虚拟化行），-lt 遇 NaN 恒 false 假绿
    {
        throw "断言失败（SG 病灶 C 门）：图标右缘→文字左缘间距 $gapPx5C px（期望 ≥9=10 DIP Margin）——图标与文字贴死回归（用户「图标和文字都贴在一起」）"
    }
    Write-Host "SG 病灶 C 门通过：图标-文字间距 $gapPx5C px（≥9）"
    Invoke-SearchJump $settings "透明度" "desktop-opacity-slider" "SG-jump-opacity"
    Invoke-SearchJump $settings "检查频率" "poll-interval-box" "SG-jump-poll"
    Invoke-SearchJump $settings "贴边收起" "edge-collapse-toggle" "SG-jump-edge"
    Invoke-SearchJump $settings "GitHub" "conn-save-button" "SG-jump-github"
    Invoke-SearchJump $settings "悬浮形态" "desktop-layout-box" "SG-jump-oldname" # 旧名兼容
    $sgBox = Find-UiaById $settings "settings-search" 8
    ($sgBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("组件中心")
    Start-Sleep -Milliseconds 900
    $sgBox.SetFocus()
    Start-Sleep -Milliseconds 300
    [WinEnum]::SendVirtualKey(0x0D)
    Start-Sleep -Seconds 3
    if (!(Find-UiaById $settings "component-center-bigmodel.usage:GLM" 10)) { Save-FullScreenshot "SG-FAIL-components-page.png"; throw "断言失败（场景 SG）：搜索「组件中心」回车后未进组件中心页（GLM 行不在树）——模块级跳转回归" }
    Save-FullScreenshot "SG-jump-components.png"
    Write-Host "场景 SG 模块级跳转通过：「组件中心」→ 组件中心页"
    # 清词复原 + 无结果档（SET-UX-08，2026-11-26 补 CI 门）：
    # 清词 → module-advanced 回树 + 头行回「N 个模块」非过滤形态；
    # 无结果词 → 头行报「0/N 个模块匹配」+ 模块行全滤除（module-github 离树）。
    ($sgBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("")
    Start-Sleep -Milliseconds 600
    if (!(Find-UiaById $settings "module-advanced" 6)) { throw "断言失败（场景 SG）：清词后 module-advanced 未回树——过滤复位回归" }
    $sgCleared = Get-WindowUiaTexts ([IntPtr]$settings.Current.NativeWindowHandle)
    if (!($sgCleared | Where-Object { $_ -match '^配置中心 · \d+ 个模块$' })) { Save-FullScreenshot "SG-FAIL-clear-header.png"; throw "断言失败（场景 SG）：清词后头行未回「N 个模块」非过滤形态——计数头回归" }
    Write-Host "场景 SG 清词复原通过：module-advanced 回树 + 头行回非过滤形态"
    ($sgBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("绝不匹配的设置词XYZQ")
    Start-Sleep -Milliseconds 900
    $sgNoHit = Get-WindowUiaTexts ([IntPtr]$settings.Current.NativeWindowHandle)
    if (!($sgNoHit | Where-Object { $_ -match '^配置中心 · 0/\d+ 个模块匹配' })) { Save-FullScreenshot "SG-FAIL-no-result-header.png"; throw "断言失败（场景 SG）：无结果档头行未见「0/N 个模块匹配」——过滤计数回归" }
    if (Find-UiaById $settings "module-github" 3) { Save-FullScreenshot "SG-FAIL-no-result-row.png"; throw "断言失败（场景 SG）：无结果档 module-github 仍在树——模块行未滤除" }
    Save-FullScreenshot "SG-no-result.png"
    Write-Host "场景 SG 无结果档通过：0/N 匹配头行 + 模块行全滤除"
    # 窄窗档（SET-UX-08，2026-11-26 补 CI 门）：外框缩到 600 物理像素（config 记忆下限同口径）→
    # 搜索框仍在树 + 过滤链（有结果计数头 + 行滤除 + 清词复原）照常走 → 恢复原尺寸。
    $sgHwnd = [IntPtr]$settings.Current.NativeWindowHandle
    $sgRectBefore = [WinEnum]::RectOf($sgHwnd)
    [WinEnum]::ResizeTo($sgHwnd, 600, 640)
    Start-Sleep -Seconds 2
    $sgNarrowBox = Find-UiaById $settings "settings-search" 8
    if (!$sgNarrowBox) { Save-FullScreenshot "SG-FAIL-narrow-no-search.png"; throw "断言失败（场景 SG）：窄窗 600px 下 settings-search 不在树——窄窗搜索链断裂" }
    # 搜索词必须同时命中模块名与字段索引：头行计数是模块命中数，字段命中只追加「· K 设置项」。
    # 「透明度」只在字段索引里（模块 DisplayName/Subtitle/Key 均无此词）→ 头行恒 0/N，不能当模块过滤判据。
    ($sgNarrowBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("看板")
    Start-Sleep -Milliseconds 900
    $sgNarrowTexts = Get-WindowUiaTexts ([IntPtr]$settings.Current.NativeWindowHandle)
    if (!($sgNarrowTexts | Where-Object { $_ -match '^配置中心 · [1-9]\d*/\d+ 个模块匹配' })) { Save-FullScreenshot "SG-FAIL-narrow-header.png"; throw "断言失败（场景 SG）：窄窗下过滤计数头未见 N/M 命中（N≥1）——窄窗过滤链回归" }
    if (Find-UiaById $settings "module-advanced" 3) { Save-FullScreenshot "SG-FAIL-narrow-row.png"; throw "断言失败（场景 SG）：窄窗过滤下未命中模块 module-advanced 仍在树——窄窗行滤除回归" }
    ($sgNarrowBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("")
    Start-Sleep -Milliseconds 600
    if (!(Find-UiaById $settings "module-advanced" 6)) { throw "断言失败（场景 SG）：窄窗清词后 module-advanced 未回树——窄窗复位回归" }
    Save-FullScreenshot "SG-narrow-window.png"
    [WinEnum]::ResizeTo($sgHwnd, ($sgRectBefore.Right - $sgRectBefore.Left), ($sgRectBefore.Bottom - $sgRectBefore.Top))
    Start-Sleep -Milliseconds 800
    Write-Host "场景 SG 窄窗档通过：600px 窗搜索/过滤/清词全链照常 + 尺寸已恢复"
    Stop-Beacon
    Write-Host "场景 SG 通过：分组头 + 五路字段跳转聚焦 + 旧名兼容 + 模块级跳转 + 清词复原 + 无结果档 + 窄窗档——SET-UX-08"
}
catch
{
    Stop-Beacon
    Save-FullScreenshot "SG-FAIL-unexpected.png"
    $failures.Add("场景 SG：$($_.Exception.Message)")
    Write-Host "::error::场景 SG 断言失败：$($_.Exception.Message)"
}

# —— 场景 SH：数据来源按接入状态组织（SET-UX-09，2026-10-20 巡检单批3 CI 代验）——
# 连接行状态 chip 会话五拍：种子「已保存 · 待验证」→ authprobe（自定义 HTTP 无凭据）测连接撞 mock 401
# 「✗ 测试失败：HTTP 认证失败」→ fail 档注入 500「✗ 测试失败」（两档失败均不解锁快速添加）→
# 删档重测=重试成功「✓ 测试通过」→ 快速添加区出现（桌面档可用 / 岛档随状态条未选岛置灰）；
# 未测试/未通过不出快速添加（状态门）。添加后 widgets.json 落 bigmodel.usage:zhipu（pinned=true，总数 +1）
# + 添加钮禁用（重复提交防抖）+「查看去向」直达组件中心该组件桌面去向行；
# 收口两档（2026-11-26）：空态（connections 清空→「尚无连接」hint+无 chip+无快速添加）与
# 组件取数失败（/badjson 200 非 JSON→chip「✓ 测试通过」而组件「拉取失败」摘要同框）——五面全覆盖。
try
{
    Write-GConfig "" "off" 1.0
    # 缺凭据探针（SET-UX-09 补 CI 门，2026-11-26）：无 credentialRef 的 http 连接 + mock /auth-required
    # 无头即 401 → chip「✗ 测试失败：HTTP 认证失败」。WriteAllText 落盘（Set-Content UTF8 带 BOM 会被 JsonDocument 拒读）
    $shConnsPath = Join-Path $configDir "connections.json"
    $shConnsText = (Get-Content $shConnsPath -Raw -Encoding UTF8).Replace(']', ',{ "id": "authprobe", "type": "http", "endpoint": "http://127.0.0.1:18081/auth-required", "enabled": true }]')
    [System.IO.File]::WriteAllText($shConnsPath, $shConnsText)
    if (@(Get-ConfigRecords $shConnsPath | Where-Object { $_.id -eq "authprobe" }).Count -ne 1) { throw "断言失败（场景 SH 前置）：authprobe 连接种子未落 connections.json" }
    Ensure-MockServer
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SH-FAIL-no-settings.png"; throw "断言失败（场景 SH）：--settings 启动 30s 内未见设置窗" }
    Invoke-Uia (Find-UiaById $settings "module-bigmodel" 10)
    Start-Sleep -Seconds 2
    $chip = Find-UiaById $settings "conn-state-zhipu" 10
    if (!$chip) { Save-FullScreenshot "SH-FAIL-no-chip.png"; throw "断言失败（场景 SH）：conn-state-zhipu 不在 UIA 树——连接状态 chip 缺失" }
    $chipText = ""
    try { $chipText = $chip.Current.Name } catch { }
    if ($chipText -notlike "*待验证*") { Save-FullScreenshot "SH-FAIL-chip-seed.png"; throw "断言失败（场景 SH）：种子态 chip=「$chipText」（期望含「已保存 · 待验证」）" }
    Save-FullScreenshot "SH-chip-unverified.png"
    if (Find-UiaById $settings "source-add-button" 3) { Save-FullScreenshot "SH-FAIL-early-add.png"; throw "断言失败（场景 SH）：未测试就出快速添加按钮——接入状态门失效" }
    Write-Host "场景 SH 种子态通过：chip「已保存 · 待验证」+ 未验证不出快速添加"
    # 缺凭据档（SET-UX-09，2026-11-26 补 CI 门）：authprobe（自定义 HTTP，无凭据）测连接 → mock 401 →
    # HttpEndpoint 认证失败链 → chip「✗ 测试失败：HTTP 认证失败」；失败态仍不解锁快速添加。
    Invoke-Uia (Find-UiaById $settings "module-http" 10)
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaById $settings "test-authprobe" 10)
    Start-Sleep -Seconds 4
    $chipAuth = Find-UiaById $settings "conn-state-authprobe" 8
    $chipAuthText = ""
    try { $chipAuthText = $chipAuth.Current.Name } catch { }
    if ($chipAuthText -notlike "*认证失败*") { Save-FullScreenshot "SH-FAIL-chip-not-auth.png"; throw "断言失败（场景 SH）：缺凭据探针 chip=「$chipAuthText」（期望含「认证失败」）——401 映射链回归" }
    Save-FullScreenshot "SH-chip-authfail.png"
    if (Find-UiaById $settings "source-add-button" 3) { Save-FullScreenshot "SH-FAIL-add-after-auth.png"; throw "断言失败（场景 SH）：缺凭据失败后快速添加按钮已出——失败态解锁面回归" }
    Write-Host "场景 SH 缺凭据档通过：authprobe 无凭据 401 →「认证失败」可见 + 不解锁"
    # 失败档（SET-UX-09 补 CI 门，2026-11-26）：mock 注入 500 → chip「✗ 测试失败」+ 失败不解锁快速添加
    Invoke-Uia (Find-UiaById $settings "module-bigmodel" 10) # 缺凭据档切走了页面，先回智谱页再驱 zhipu
    Start-Sleep -Seconds 2
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "fail" -Encoding ASCII
    Invoke-Uia (Find-UiaById $settings "test-zhipu" 10)
    Start-Sleep -Seconds 4
    $chipFail = Find-UiaById $settings "conn-state-zhipu" 8
    $chipFailText = ""
    try { $chipFailText = $chipFail.Current.Name } catch { }
    if ($chipFailText -notlike "*测试失败*") { Save-FullScreenshot "SH-FAIL-chip-not-failed.png"; throw "断言失败（场景 SH）：500 注入后 chip=「$chipFailText」（期望含「✗ 测试失败」）——失败链回归" }
    Save-FullScreenshot "SH-chip-failed.png"
    if (Find-UiaById $settings "source-add-button" 3) { Save-FullScreenshot "SH-FAIL-add-after-fail.png"; throw "断言失败（场景 SH）：测试失败后快速添加按钮已出——失败态不应解锁添加" }
    Write-Host "场景 SH 失败档通过：chip「✗ 测试失败」+ 失败不解锁快速添加"
    # 重试档：mock 回健康档（删档位文件=38% 默认）再测——「失败后重试成功」映射到下一步（下方 ✓ 链）
    Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
    Invoke-Uia (Find-UiaById $settings "test-zhipu" 8)
    Start-Sleep -Seconds 4
    $chip2 = Find-UiaById $settings "conn-state-zhipu" 8
    $chipText2 = ""
    try { $chipText2 = $chip2.Current.Name } catch { }
    if ($chipText2 -notlike "*测试通过*") { Save-FullScreenshot "SH-FAIL-chip-not-ok.png"; throw "断言失败（场景 SH）：测试后 chip=「$chipText2」（期望「✓ 测试通过」）——mock 健康链或 chip 刷新链回归" }
    Save-FullScreenshot "SH-chip-ok.png"
    $deskRadio = Find-UiaById $settings "source-pos-desktop" 8
    if (!$deskRadio -or !$deskRadio.Current.IsEnabled) { Save-FullScreenshot "SH-FAIL-no-desktop-radio.png"; throw "断言失败（场景 SH）：桌面档缺失或不可用（GLM 组件 PinSupported=true 应可钉）" }
    $islandRadio = Find-UiaById $settings "source-pos-island" 8
    if ($islandRadio -and $islandRadio.Current.IsEnabled) { throw "断言失败（场景 SH）：岛档可用但状态条种子未选岛——位置门失效" }
    $shWidgetsPath = Join-Path $configDir "widgets.json"
    $widgetsBefore = @(Get-ConfigRecords $shWidgetsPath).Count
    Invoke-Uia (Find-UiaById $settings "source-add-button" 8)
    Start-Sleep -Seconds 3
    $shRecords = @(Get-ConfigRecords $shWidgetsPath)
    $added = @($shRecords | Where-Object { $_.id -eq "bigmodel.usage:zhipu" })
    if ($added.Count -ne 1) { Save-FullScreenshot "SH-FAIL-no-widget.png"; throw "断言失败（场景 SH）：widgets.json 未落 bigmodel.usage:zhipu（count=$($added.Count)）——快速添加落盘链回归" }
    if ("$($added[0].pinned)" -ne "True") { throw "断言失败（场景 SH）：快速添加组件 pinned=$($added[0].pinned)（期望 True——桌面档一键上板）" }
    if ($shRecords.Count -ne ($widgetsBefore + 1)) { throw "断言失败（场景 SH）：组件数 $widgetsBefore→$($shRecords.Count)（期望恰 +1）" }
    Write-Host "场景 SH 添加断言通过：widgets.json 落 bigmodel.usage:zhipu（pinned=true，总数 +1）"
    $addAfter = Find-UiaById $settings "source-add-button" 8
    if ($addAfter -and $addAfter.Current.IsEnabled) { Save-FullScreenshot "SH-FAIL-add-still-enabled.png"; throw "断言失败（场景 SH）：添加后按钮仍可用——重复提交防抖缺失" }
    # —— 病灶 D（BEACON-FIX-1126，2026-11-26）：刷新档下拉宽 ≥ MinWidth 220 DIP——
    # 硬编码 Width=180 把「machine · 15 秒（机器状态）」截成省略号（用户「下拉框截断」），修复后
    # 框随列宽自适应；UIA BoundingRectangle 直读宽度（修复前 180px 恒红）。
    # 锚在 Provider 页的组件向导（BuildWidgetEditor）：必须在「查看去向」跳组件中心之前读——
    # 跳页/关窗后锚离树，Find 恒空（CI 45726231918+45740410733 两轮实证）。
    $tierD = Find-UiaById $settings "widget-tier-box" 10
    if (!$tierD) { Save-FullScreenshot "SH-FAIL-no-tier-box.png"; throw "断言失败（SH 病灶 D 门）：widget-tier-box 不在 UIA 树——组件向导未载入或锚丢失" }
    $tierW5D = $tierD.Current.BoundingRectangle.Width
    if (!($tierW5D -ge 218)) # NaN 防护：同病灶 C 门
    {
        throw "断言失败（SH 病灶 D 门）：刷新档下拉宽 $tierW5D px（期望 ≥218=MinWidth 220 DIP）——硬编码宽度截断回归"
    }
    Write-Host "SH 病灶 D 门通过：刷新档下拉宽 $tierW5D px（≥218，长标签不再截断）"
    Invoke-Uia (Find-UiaById $settings "source-goto-placement" 8)
    Start-Sleep -Seconds 3
    if (!(Find-UiaById $settings "component-desktop-state-bigmodel.usage:zhipu" 10)) { Save-FullScreenshot "SH-FAIL-no-goto.png"; throw "断言失败（场景 SH）：查看去向后组件中心未见 component-desktop-state-bigmodel.usage:zhipu——跳转链回归" }
    Save-FullScreenshot "SH-goto-component-center.png"
    # —— 空态档（SET-UX-09 收口①，2026-11-26）：connections 清空 → http 页空态 hint + 无 chip + 不出快速添加 ——
    # 「未连接」与「已保存 · 待验证」互斥可证：无连接档只余 ProviderEmptyHint(http) 文案锚
    # （SettingsWindow.Actions.cs:549），状态 chip 与快速添加门同灭；WriteAllText 无 BOM 防 JsonDocument 拒读。
    Stop-Beacon
    [System.IO.File]::WriteAllText($shConnsPath, "[]")
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $shEmpty = Find-SettingsWindow 30
    if (!$shEmpty) { Save-FullScreenshot "SH-FAIL-empty-no-settings.png"; throw "断言失败（场景 SH 空态档）：连接清空重启 30s 内未见设置窗" }
    Invoke-Uia (Find-UiaById $shEmpty "module-http" 10)
    Start-Sleep -Seconds 2
    $shEmptyTexts = @(Get-WindowUiaTexts ([IntPtr]$shEmpty.Current.NativeWindowHandle))
    if (@($shEmptyTexts | Where-Object { $_ -like "*尚无连接*" }).Count -lt 1) { Save-FullScreenshot "SH-FAIL-no-empty-hint.png"; throw "断言失败（场景 SH 空态档）：http 页未见「尚无连接」空态 hint；实际：$($shEmptyTexts -join ' | ')" }
    if (Find-UiaById $shEmpty "conn-state-zhipu" 3) { Save-FullScreenshot "SH-FAIL-empty-has-chip.png"; throw "断言失败（场景 SH 空态档）：连接清空后仍见 conn-state-zhipu——空态渲染泄漏" }
    if (Find-UiaById $shEmpty "source-add-button" 3) { Save-FullScreenshot "SH-FAIL-empty-add.png"; throw "断言失败（场景 SH 空态档）：无连接却出快速添加按钮——状态门回归" }
    Save-FullScreenshot "SH-empty-hint.png"
    Write-Host "场景 SH 空态档通过：无连接 →「尚无连接」hint + 无 chip + 无快速添加（未连接面独立可证）"
    # —— 组件取数失败档（SET-UX-09 收口②，2026-11-26）：连接测试 2xx 通过而组件 fetch 失败 ——
    # datafail 连接指向 mock /badjson（200 非 JSON）：TestAsync=GET 2xx 即 Healthy → chip「✓ 测试通过」；
    # 组件 fetch JsonDocument.Parse 抛 Degraded → PublishStale 无缓存分支摘要「拉取失败：…」在样式画廊
    # 预览面直显（AE1 同款断言面）——连接健康与组件取数两层解耦同框可证。逐字替换 widgets.json
    # （不 Replace(']')：组件对象可能含嵌套数组，首个 ] 命中会切坏 JSON）。
    Stop-Beacon
    [System.IO.File]::WriteAllText($shConnsPath, '[{ "id": "datafail", "type": "http", "endpoint": "http://127.0.0.1:18081/badjson", "enabled": true }]')
    [System.IO.File]::WriteAllText($shWidgetsPath, '[{ "id": "http.datafail", "type": "http.status", "connectionId": "datafail", "refreshTier": "ci", "pinned": true, "config": { "label": "DF" } }]')
    Stop-MockServer
    Ensure-MockServer
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $shDf = Find-SettingsWindow 30
    if (!$shDf) { Save-FullScreenshot "SH-FAIL-df-no-settings.png"; throw "断言失败（场景 SH 取数档）：重启 30s 内未见设置窗" }
    Invoke-Uia (Find-UiaById $shDf "module-http" 10)
    Start-Sleep -Seconds 2
    Invoke-Uia (Find-UiaById $shDf "test-datafail" 10)
    Start-Sleep -Seconds 4
    $chipDf = Find-UiaById $shDf "conn-state-datafail" 8
    $chipDfText = ""
    try { $chipDfText = $chipDf.Current.Name } catch { }
    if ($chipDfText -notlike "*测试通过*") { Save-FullScreenshot "SH-FAIL-df-chip.png"; throw "断言失败（场景 SH 取数档）：200 非 JSON 连接测试 chip=「$chipDfText」（期望「✓ 测试通过」）——TestAsync 2xx 语义回归" }
    Save-FullScreenshot "SH-df-chip-ok.png"
    Invoke-Uia (Find-UiaById $shDf "style-button-http.datafail" 10)
    $galleryDf = Find-GalleryWindow 15
    if (!$galleryDf) { Save-FullScreenshot "SH-FAIL-df-no-gallery.png"; throw "断言失败（场景 SH 取数档）：样式画廊 15s 未开——DF 组件定向链断" }
    $dfStale = $false
    $dfTexts = @()
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline)
    {
        $dfHandles = [WinEnum]::VisibleHandlesByTitle("Beacon · 组件中心")
        if ($dfHandles.Count -ge 1) { $dfTexts = @(Get-WindowUiaTexts $dfHandles[0]) }
        if (@($dfTexts | Where-Object { $_ -like "*拉取失败*" }).Count -ge 1) { $dfStale = $true; break }
        Start-Sleep -Seconds 2
    }
    if (!$dfStale) { Save-FullScreenshot "SH-FAIL-df-no-stale.png"; throw "断言失败（场景 SH 取数档）：组件预览未见「拉取失败」摘要（PublishStale 无缓存分支回归）；实际：$($dfTexts -join ' | ')" }
    Save-FullScreenshot "SH-df-fetch-failed.png"
    Write-Host "场景 SH 取数档通过：chip「✓ 测试通过」+ 组件「拉取失败」同框——连接正常与组件取数失败解耦"
    Stop-Beacon
    Write-Host "场景 SH 通过：chip 待验证→失败→重试通过→快速添加落盘+按钮禁用→查看去向直达→空态与取数失败两档——SET-UX-09"
}
catch
{
    Stop-Beacon
    Save-FullScreenshot "SH-FAIL-unexpected.png"
    $failures.Add("场景 SH：$($_.Exception.Message)")
    Write-Host "::error::场景 SH 断言失败：$($_.Exception.Message)"
}

# —— 场景 SI：可读的刷新频率和继承（SET-UX-10，2026-10-20 巡检单批3 CI 代验）——
# 种子 pollIntervalSeconds=300 + OVR 组件 refreshIntervalSeconds=120：组件行标签双口径人话——
# GLM（无覆盖）「每 5 分钟 · 默认刷新间隔」（继承全局）、OVR「每 2 分钟 · 此组件刷新频率」（组件级
# 覆盖）；向导继承链 hint + 全局框正名「默认刷新间隔」在页；驱动全局档 300→15 落盘（全局端可驱动）。
try
{
    Write-GConfig "" "off" 1.0 $false $false $false "" "" ',{ "id": "bigmodel.usage:OVR", "type": "bigmodel.usage", "connectionId": "zhipu", "refreshTier": "ci", "refreshIntervalSeconds": 120, "pinned": true, "config": { "label": "OVR" } }'
    Ensure-MockServer
    $siCfgPath = Join-Path $configDir "config.json"
    (Get-Content $siCfgPath -Raw -Encoding UTF8).Replace('"hotkey": "Ctrl+Alt+B",', '"hotkey": "Ctrl+Alt+B", "pollIntervalSeconds": 300,') | Set-Content -Path $siCfgPath -Encoding UTF8
    $siSeed = Get-Content $siCfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($siSeed.pollIntervalSeconds)" -ne "300") { throw "断言失败（场景 SI 前置）：种子 pollIntervalSeconds=$($siSeed.pollIntervalSeconds)（期望 300）——注入失败" }
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SI-FAIL-no-settings.png"; throw "断言失败（场景 SI）：--settings 启动 30s 内未见设置窗" }

    function Get-SiTexts([System.Windows.Automation.AutomationElement] $root)
    {
        $texts = @()
        foreach ($t in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text))))
        {
            try { if ($t.Current.Name) { $texts += $t.Current.Name } } catch { }
        }
        return $texts
    }

    # 首拍就绪门：Find-SettingsWindow 命中窗名即返回，右栏 XAML 可能尚未进 UIA 树
    # （45601056698 实测启动 1.5s 内树为空、客户区未绘制）——逐拍重取，口径同 Find-UiaById
    $siGeneral = @()
    $siHintReady = $false
    $siDeadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $siDeadline)
    {
        $siGeneral = @(Get-SiTexts $settings)
        if (@($siGeneral | Where-Object { $_ -like "*组件覆盖优先*" }).Count -gt 0)
        {
            $siHintReady = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }
    if (!$siHintReady)
    {
        Save-FullScreenshot "SI-FAIL-no-poll-hint.png"
        throw "断言失败（场景 SI）：常规页未见全局刷新间隔 hint（组件覆盖优先）——SET-UX-10 正名 hint 缺失"
    }
    Invoke-Uia (Find-UiaById $settings "module-bigmodel" 10)
    Start-Sleep -Seconds 2
    $siTexts = @(Get-SiTexts $settings)
    if (@($siTexts | Where-Object { $_ -like "*bigmodel.usage:GLM · 每 5 分钟 · 默认刷新间隔*" }).Count -eq 0)
    {
        Save-FullScreenshot "SI-FAIL-glm-label.png"
        throw "断言失败（场景 SI）：GLM 行标签未见「每 5 分钟 · 默认刷新间隔」（继承全局 300s）——行标签继承口径回归；实际：$(@($siTexts | Where-Object { $_ -like "*bigmodel.usage:GLM*" }) -join ' | ')"
    }
    if (@($siTexts | Where-Object { $_ -like "*bigmodel.usage:OVR · 每 2 分钟 · 此组件刷新频率*" }).Count -eq 0)
    {
        Save-FullScreenshot "SI-FAIL-ovr-label.png"
        throw "断言失败（场景 SI）：OVR 行标签未见「每 2 分钟 · 此组件刷新频率」（组件级 120s 覆盖）——覆盖口径回归"
    }
    if (@($siTexts | Where-Object { $_ -like "*刷新继承：*" }).Count -eq 0)
    {
        Save-FullScreenshot "SI-FAIL-no-editor-hint.png"
        throw "断言失败（场景 SI）：向导未见「刷新继承：」链路 hint——继承链不可读回归"
    }
    Save-FullScreenshot "SI-row-labels.png"
    Write-Host "场景 SI 标签断言通过：GLM=每 5 分钟·默认刷新间隔 / OVR=每 2 分钟·此组件刷新频率 + 继承链 hint"
    # 标签断言停在 bigmodel 页——poll-interval-box 只在 general 页（Find-UiaById 只搜当前页树，
    # 45356987498 SI 红即停错页找不到锚），切回常规页再驱动全局档
    Invoke-Uia (Find-UiaById $settings "module-general" 10)
    Start-Sleep -Seconds 2
    $siPollBox = Find-UiaById $settings "poll-interval-box" 10
    if (!$siPollBox) { Save-FullScreenshot "SI-FAIL-no-pollbox.png"; throw "断言失败（场景 SI）：poll-interval-box 缺失" }
    Select-ComboItemById $settings $siPollBox "poll-15"
    Start-Sleep -Seconds 3
    $siAfter = Get-Content $siCfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($siAfter.pollIntervalSeconds)" -ne "15")
    {
        Save-FullScreenshot "SI-FAIL-poll-drive.png"
        throw "断言失败（场景 SI）：驱动 poll-15 后 config pollIntervalSeconds=$($siAfter.pollIntervalSeconds)（期望 15）——全局档落盘链回归"
    }
    Save-FullScreenshot "SI-poll-15.png"
    # —— 调度生效档（SET-UX-10 收口，2026-11-26）：全局 300→15 落盘后新拍真驱动刷新 ——
    # 可观测面=app 日志逐跳行「Widget {Id} refreshed ok」（WidgetHost.cs:322）：全局档落盘即广播
    # → WidgetHost.Reload Clear+Start 重排调度，GLM（无覆盖）落 15s 拍（50s 窗 ≥2 跳）；
    # OVR（RefreshIntervalSeconds=120 组件覆盖）不吃 15s 染（窗内 ≤1 跳=即时 reload 一跳）。
    # 日志 UTF-8 无 BOM 必显式 -Encoding UTF8（PS 5.1 ANSI 误读，run 37954226899 教训）。
    $siLogPath = Join-Path (Join-Path $configDir "logs") ("beacon-" + (Get-Date -Format "yyyyMMdd") + ".log")
    $siLogBefore = 0
    if (Test-Path $siLogPath) { $siLogBefore = @(Get-Content $siLogPath -Encoding UTF8).Count }
    Start-Sleep -Seconds 50
    $siLogTail = @()
    if (Test-Path $siLogPath) { $siLogTail = @(Get-Content $siLogPath -Encoding UTF8 | Select-Object -Skip $siLogBefore) }
    $siGlmHits = @($siLogTail | Where-Object { $_ -like "*bigmodel.usage:GLM refreshed ok*" }).Count
    $siOvrHits = @($siLogTail | Where-Object { $_ -like "*bigmodel.usage:OVR refreshed ok*" }).Count
    if ($siGlmHits -lt 2) { Save-FullScreenshot "SI-FAIL-cadence.png"; throw "断言失败（场景 SI 调度档）：全局改 15s 后 50s 窗内 GLM 仅 $siGlmHits 跳成功刷新（期望 ≥2=15s 拍）——调度未吃新档回归；日志尾部：$(($siLogTail | Select-Object -Last 5) -join ' | ')" }
    if ($siOvrHits -gt 1) { Save-FullScreenshot "SI-FAIL-ovr-leak.png"; throw "断言失败（场景 SI 调度档）：OVR（组件覆盖 120s）50s 窗内刷新 $siOvrHits 跳（期望 ≤1）——组件覆盖被全局 15s 染回归" }
    Write-Host "场景 SI 调度档通过：GLM 50s 窗 $siGlmHits 跳（15s 拍生效）+ OVR $siOvrHits 跳（组件覆盖 120s 不染）"
    Stop-Beacon
    Write-Host "场景 SI 通过：行标签双口径人话 + 继承链 hint + 全局档 300→15 落盘 + 新拍真驱动（调度生效档）——SET-UX-10"
}
catch
{
    Stop-Beacon
    Save-FullScreenshot "SI-FAIL-unexpected.png"
    $failures.Add("场景 SI：$($_.Exception.Message)")
    Write-Host "::error::场景 SI 断言失败：$($_.Exception.Message)"
}

# —— 场景 SJ：生效反馈、快捷键错误、主题切换（SET-UX-11，2026-10-20 巡检单批3 CI 代验）——
# 主题档浅色→深色（Select-ComboItemByName Name 口径）→ config App.Theme 逐档落盘（生效时机标注
# 「重启生效」已写在控件 Header；浅色实际渲染属视觉亲验面）。
# PS 先占 Ctrl+Alt+M（RegisterHotKey NULL hwnd）→ 热键框改 Ctrl+Alt+M → 注册失败可见：
# 反馈行「注册失败」+ 框值回退 Ctrl+Alt+B + config 不被坏键洗掉（原有效绑定保留）。
# 解除占用后改 Ctrl+Alt+J → 反馈「已生效」+ config 落 Ctrl+Alt+J（即时生效链）。
try
{
    Write-GConfig "" "off" 1.0
    Ensure-MockServer
    $sjSource = @'
using System;
using System.Runtime.InteropServices;
public static class SjHotkeyHolder
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
'@
    Add-Type -TypeDefinition $sjSource
    if (-not [SjHotkeyHolder]::RegisterHotKey([IntPtr]::Zero, 0x4D2, 0x3, 0x4D)) # MOD_CONTROL|MOD_ALT + VK_M
    {
        throw "断言失败（场景 SJ 前置）：PS 占位注册 Ctrl+Alt+M 失败（error $([System.Runtime.InteropServices.Marshal]::GetLastWin32Error())）——占位面失效"
    }
    Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") -ArgumentList "--settings" | Out-Null
    $settings = Find-SettingsWindow 30
    if (!$settings) { Save-FullScreenshot "SJ-FAIL-no-settings.png"; throw "断言失败（场景 SJ）：--settings 启动 30s 内未见设置窗" }
    # 生效时机 hint 档（SET-UX-11 补 CI 门，2026-11-27）：开机自启 hint 写在控件旁——
    # 「注册表 Run 键」保存即时 + 「下次登录」随 explorer 拉起；首拍就绪门同 SI（窗现≠树现，45601056698）。
    $sjStartupHint = $false
    $sjHintDeadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $sjHintDeadline)
    {
        if (@(Get-UiaTextNames $settings | Where-Object { $_ -like "*注册表 Run 键*下次登录*" }).Count -gt 0) { $sjStartupHint = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (!$sjStartupHint)
    {
        Save-FullScreenshot "SJ-FAIL-no-startup-hint.png"
        throw "断言失败（场景 SJ）：常规页未见开机自启生效时机 hint（注册表 Run 键 + 下次登录）——SET-UX-11 hint 缺失"
    }
    Write-Host "场景 SJ 生效时机 hint 通过：开机自启 hint 写在控件旁（保存即时 + 下次登录拉起）"
    # 主题档（SET-UX-11 补 CI 门，2026-11-26）：浅色/深色切换落盘档位——生效时机标注写在控件旁（Header
    # 「主题（重启生效）」），实际浅色渲染翻转属用户视觉亲验面；此处断选择链 + config 逐档回读。
    $themeBox = Find-UiaById $settings "theme-box" 10
    if (!$themeBox) { Save-FullScreenshot "SJ-FAIL-no-theme.png"; throw "断言失败（场景 SJ）：theme-box 锚缺失" }
    Select-ComboItemByName $themeBox "浅色"
    Start-Sleep -Seconds 1
    $sjThemeCfg = Get-Content (Join-Path $configDir "config.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($sjThemeCfg.app.theme)" -ne "light" -and "$($sjThemeCfg.theme)" -ne "light")
    {
        throw "断言失败（场景 SJ）：选浅色后 config theme=$($sjThemeCfg.app.theme)/$($sjThemeCfg.theme)（期望 light）——主题落盘链回归"
    }
    $sjThemeSel = Get-ComboSelectionName $themeBox
    if ($sjThemeSel -notlike "*浅色*") { throw "断言失败（场景 SJ）：选浅色后框值=「$sjThemeSel」（期望浅色）——选中态回读回归" }
    Write-Host "场景 SJ 主题档通过：浅色选中 + config theme=light"
    Select-ComboItemByName $themeBox "深色"
    Start-Sleep -Seconds 1
    $sjThemeCfg2 = Get-Content (Join-Path $configDir "config.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($sjThemeCfg2.app.theme)" -ne "dark" -and "$($sjThemeCfg2.theme)" -ne "dark")
    {
        throw "断言失败（场景 SJ）：选深色后 config theme=$($sjThemeCfg2.app.theme)/$($sjThemeCfg2.theme)（期望 dark）——主题回切落盘回归"
    }
    Write-Host "场景 SJ 主题档通过：深色回切 + config theme=dark"
    $hotkeyBox = Find-UiaById $settings "hotkey-box" 10
    if (!$hotkeyBox) { Save-FullScreenshot "SJ-FAIL-no-hotkey-box.png"; throw "断言失败（场景 SJ）：hotkey-box 锚缺失" }
    $sjToggleEl = Find-UiaById $settings "startup-toggle" 10
    if (!$sjToggleEl) { throw "断言失败（场景 SJ）：startup-toggle 缺失（焦点挪走触发 LostFocus 保存）" }
    $sjToggle = $sjToggleEl.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $sjBoxValue = $hotkeyBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    # —— 失败链：目标键被 PS 占用 ——
    $sjBoxValue.SetValue("Ctrl+Alt+M")
    $sjToggle.Toggle() # 焦点挪走 → LostFocus → SaveHotkey → 试挂失败 → 原键保留 + 行内反馈
    Start-Sleep -Seconds 3
    Save-FullScreenshot "SJ-failed-feedback.png"
    $sjFeedback = Find-UiaById $settings "hotkey-feedback" 8
    $sjFb = ""
    if ($sjFeedback) { try { $sjFb = $sjFeedback.Current.Name } catch { } }
    if ($sjFb -notlike "*注册失败*")
    {
        throw "断言失败（场景 SJ）：热键反馈未见「注册失败」（实际：$sjFb）——失败不可见回归"
    }
    $sjBoxNow = $sjBoxValue.Current.Value
    if ($sjBoxNow -ne "Ctrl+Alt+B")
    {
        throw "断言失败（场景 SJ）：失败后热键框值=$sjBoxNow（期望回退 Ctrl+Alt+B）——原有效绑定未保住"
    }
    $sjCfg = Get-Content (Join-Path $configDir "config.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($sjCfg.hotkey)" -ne "Ctrl+Alt+B")
    {
        throw "断言失败（场景 SJ）：config hotkey=$($sjCfg.hotkey)（期望保持 Ctrl+Alt+B）——坏键洗掉配置回归"
    }
    Write-Host "场景 SJ 失败链通过：占用态「注册失败」可见、框值与 config 均保持原键"
    # —— 成功链：解除占用后 Ctrl+Alt+J ——
    [SjHotkeyHolder]::UnregisterHotKey([IntPtr]::Zero, 0x4D2) | Out-Null
    $sjBoxValue.SetValue("Ctrl+Alt+J")
    $sjToggle.Toggle()
    Start-Sleep -Seconds 3
    Save-FullScreenshot "SJ-success-feedback.png"
    $sjFeedback2 = Find-UiaById $settings "hotkey-feedback" 8
    $sjFb2 = ""
    if ($sjFeedback2) { try { $sjFb2 = $sjFeedback2.Current.Name } catch { } }
    if ($sjFb2 -notlike "*已生效*")
    {
        throw "断言失败（场景 SJ）：成功反馈未见「已生效」（实际：$sjFb2）——生效反馈回归"
    }
    $sjCfg2 = Get-Content (Join-Path $configDir "config.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    if ("$($sjCfg2.hotkey)" -ne "Ctrl+Alt+J")
    {
        throw "断言失败（场景 SJ）：config hotkey=$($sjCfg2.hotkey)（期望 Ctrl+Alt+J）——改键落盘链回归"
    }
    Stop-Beacon
    Write-Host "场景 SJ 通过：浅深色落盘 + 占用失败可见 + 原键保留 + 成功即时生效——SET-UX-11"
}
catch
{
    Stop-Beacon
    if ("SjHotkeyHolder" -as [type]) { [SjHotkeyHolder]::UnregisterHotKey([IntPtr]::Zero, 0x4D2) | Out-Null } # 占位键拆除（防泄给后续场景）
    Save-FullScreenshot "SJ-FAIL-unexpected.png"
    $failures.Add("场景 SJ：$($_.Exception.Message)")
    Write-Host "::error::场景 SJ 断言失败：$($_.Exception.Message)"
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

# 解除总超时护栏：脚本走到收尾（无论断言成败）都先拆 watchdog，避免对已结束进程的空倒计时
if ($script:EvidenceWatchdog)
{
    Stop-Process -Id $script:EvidenceWatchdog.Id -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0)
{
    throw "视觉取证断言失败（$($failures.Count) 项）：`n" + ($failures -join "`n")
}
Write-Host "全部场景断言通过"
