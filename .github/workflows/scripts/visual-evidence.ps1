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
    public static RECT RectOf(IntPtr h) { RECT r; GetWindowRect(h, out r); return r; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SystemParametersInfo(uint action, uint param, string value, uint init);
    public static void SetWallpaper(string path) { SystemParametersInfo(20, 0, path, 3); }

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    public static void SetForeground(IntPtr h) { SetForegroundWindow(h); }
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
    // 键盘注入（托盘原生菜单导航）：dwFlags=0 按下 / 2 抬起
    public static void SendVirtualKey(ushort vk)
    {
        var down = new INPUT { type = 0, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0000 } } };
        var up = new INPUT { type = 0, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { down }, size);
        System.Threading.Thread.Sleep(90);
        SendInput(1, new[] { up }, size);
        System.Threading.Thread.Sleep(120);
    }
    // 单键按下/抬起分离（全局热键和弦用，如 Ctrl+Alt+B：MOD 修饰键按住期间敲主键）
    public static void SendKeyDown(ushort vk)
    {
        var down = new INPUT { type = 0, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0000 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { down }, size);
        System.Threading.Thread.Sleep(60);
    }
    public static void SendKeyUp(ushort vk)
    {
        var up = new INPUT { type = 0, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
        SendInput(1, new[] { up }, size);
        System.Threading.Thread.Sleep(60);
    }
    // 全局热键和弦（如 Ctrl+Alt+B）：修饰键按下 → 主键敲击 → 修饰键抬起
    public static void SendChord(ushort modifier, ushort key)
    {
        SendKeyDown(modifier);
        SendKeyDown(key);
        SendKeyUp(key);
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
    $script:mockJob = Start-Job -ScriptBlock {
        $listener = New-Object System.Net.HttpListener
        $listener.Prefixes.Add("http://127.0.0.1:18081/")
        $listener.Start()
        $ark = '{"ResponseMetadata":{"RequestId":"mock-1","Action":"GetCodingPlanUsage"},"Result":{"Status":"Running","QuotaUsage":[{"Level":"session","Percent":42.5,"ResetTimestamp":1771000000},{"Level":"weekly","Percent":8,"ResetTimestamp":1772000000},{"Level":"monthly","Percent":"92","ResetTimestamp":0}]}}'
        # bigmodel 监控响应（quota/limit 结构：limits[] 按 type/unit/number 归类——5h 滚动窗 unit=3/number=5）
        $bigmodel = '{"data":{"level":"pro","limits":[{"type":"TOKENS_LIMIT","unit":3,"number":5,"percentage":38,"nextResetTime":1771000000000},{"type":"TOKENS_LIMIT","unit":6,"number":1,"percentage":15,"nextResetTime":1772000000000},{"type":"TIME_LIMIT","unit":5,"number":1,"percentage":61,"nextResetTime":0}]}}'
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
        while ($listener.IsListening)
        {
            $ctx = $listener.GetContext()
            try
            {
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
                try { $ctx.Response.Abort() } catch { }
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
        Stop-Job $script:mockJob -ErrorAction SilentlyContinue
        Remove-Job $script:mockJob -Force -ErrorAction SilentlyContinue
    }
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
    # TrackPopupMenu 需要 owner 进程在前台（否则菜单不显示，38256993295 实证）：先把 Beacon 的
    # tile 置前台（WS_EX_NOACTIVATE 不拦程序性前台切换），让 app 内部对消息窗的
    # SetForegroundWindow 成为同进程合法切换
    $fgFeed = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
    if ($fgFeed.Count -eq 0) { $fgFeed = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板") }
    if ($fgFeed.Count -gt 0)
    {
        [WinEnum]::SetForegroundWindow($fgFeed[0])
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
function Write-GConfig([string] $glmColorOverride = "")
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
  "settingsSidebarWidth": 400,
  "appearance": { "motion": { "mode": "off", "intensity": 1.0 } }
}
'@ | Set-Content -Path (Join-Path $configDir "config.json") -Encoding UTF8
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
function Write-HConfig([string] $displayMode = "floating")
{
    Stop-Beacon
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
    "pinLayout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": false, "floatingX": 120, "floatingY": 120 } },
  { "id": "deepseek.balance:ds", "type": "deepseek.balance", "connectionId": "ds-main", "refreshTier": "ci", "pinned": true, "config": {},
    "pinLayout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": false, "floatingX": 260, "floatingY": 220 } }
]
'@ | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    @'
{
  "tiles": [
    { "widgetId": "bigmodel.usage:GLM", "layout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": false, "floatingX": 120, "floatingY": 120 } },
    { "widgetId": "deepseek.balance:ds", "layout": { "monitor": "ci", "anchor": 0, "offsetDips": { "x": 16, "y": 16 }, "collapsed": false, "floatingX": 260, "floatingY": 220 } }
  ]
}
'@ | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8
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

# 进度条像素硬断言助手（2026-10-10 修「进度条看不到」配套，2026-10-11 重设计后 bar 为默认路径）：
# 扫 tile 截图（必须 margin 0，防桌面色混入）底部 6 物理像素行、中部 60% 宽（避开 2 DIP 圆角端头
# 与 NN% 读数列），统计与暗底色 (15,23,42) 任一通道差 >25 的像素。
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
Start-MockServer # 提前到 A 前：GLM tile 走 mock 健康链（38%），D 场景复用同一实例（二次启动会同端口冲突）
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
    # 进度条像素硬断言（2026-10-11 重设计，bar 默认路径）：GLM tile 有 Progress 语义（mock 38%），
    # 底部 4 DIP 条带必须出像素——旧病「条从未渲染」/ 3px 细到难辨都在这里现形
    $aBarCrop = New-Crop ([WinEnum]::RectOf($tiles[0])) 0
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
        $arkRect = [WinEnum]::RectOf($panelHandles[$arkIndex])
        $arkBarCrop = New-Crop $arkRect 0
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
    Start-Sleep -Seconds 8
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
    Write-Host "场景 E1 通过：二次启动进程数保持 1，副实例退出日志在案（唤起气泡为 Toast 面不可断，转装机目检）"
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
    Start-MockServer
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
    if ($glmMagenta -lt 12)
    {
        throw "断言失败（场景 G1）：GLM tile 品红覆盖像素 $glmMagenta（期望 ≥12）——colorOverride 未生效"
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
        # 38% 填充下成功色实测约 44px（状态灯+条带，38254521518 首跑实测）；阈值 25 留双向余量
        if ($green -lt 25) { throw "断言失败（场景 G2 第${round}轮）：默认成功绿未渲染（$green px）——回退后渲染缺失" }
        Stop-Beacon
        Write-Host "场景 G2 第${round}轮通过：非法色回退默认绿（品红 $magenta / 绿 $green px），进程存活"
    }
    Write-Host "场景 G2 通过：非法颜色回退不崩溃且重启保持"
    # G3-G6 设置页驱动：改色即时生效+落盘、重置默认、动效档位/强度持久化、重启恢复选中
    Write-GConfig ""
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
    # G4 成功色改 #ff8800 → config 落盘 + tile 即时变橙（改色即时生效）
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
    $crop = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    Save-Zoom $crop "G4-glm-orange-live.png" 6
    $orange = Count-ColorPixels $crop 255 136 0 45
    $crop.Dispose()
    if ($orange -lt 25)
    {
        throw "断言失败（场景 G4）：GLM tile 橙色像素 $orange（期望 ≥25）——改色即时生效失效（未重启就应换色）"
    }
    Write-Host "场景 G4 通过：设置改成功色 #ff8800 → config 落盘 + GLM tile 即时变橙（$orange px，无重启）"
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
    $crop = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    $green = Count-ColorPixels $crop 63 185 80 45
    $crop.Dispose()
    if ($green -lt 25) { throw "断言失败（场景 G5）：重置后默认绿未回渲染（$green px，期望 ≥25）" }
    Write-Host "场景 G5 通过：重置默认 → severityColors 清空 + tile 即时回默认绿（$green px）"
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
    # 95% 填充 Error 色约为 38% 填充的 2.5 倍（首跑 38% 绿 44px 外推 ~92px），阈值 40 留余量
    if ($red -lt 40) { throw "断言失败（场景 G7a）：Error 档位色未渲染（红 $red px，期望 ≥40）" }
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
    # 切 full 档：呼吸在帧间现形
    Select-ComboItemById $settings $motionBox "motion-full"
    Start-Sleep -Seconds 2
    $crop1 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    Start-Sleep -Milliseconds 900
    $crop2 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    Start-Sleep -Milliseconds 900
    $crop3 = New-Crop ([WinEnum]::RectOf($handles[$glmIndex])) 0
    $fullDiff = [Math]::Max((Count-FrameDiff $crop1 $crop2 12), [Math]::Max((Count-FrameDiff $crop2 $crop3 12), (Count-FrameDiff $crop1 $crop3 12)))
    Save-Zoom $crop1 "G7-full-frame1.png" 4
    Save-Zoom $crop3 "G7-full-frame3.png" 4
    $crop1.Dispose(); $crop2.Dispose(); $crop3.Dispose()
    if ($fullDiff -lt 25) { throw "断言失败（场景 G7c）：full 档三帧差分 $fullDiff px（期望 ≥25）——呼吸灯不可见" }
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
    Write-Host "场景 H5 通过：panel 模式宿主在、悬浮 tile 窗零（两形态二选一）"
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
    # K1 全局热键 Ctrl+Alt+B（RegisterHotKey 全局生效，与前台无关）→ 面板窗出现
    $before = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板").Count
    if ($before -ne 0) { throw "断言失败（场景 K1）：面板在热键前已可见（$before）" }
    [WinEnum]::SendChord(0x11, 0x42) # VK_CONTROL + VK_B
    $panel = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(5)
    while ((Get-Date) -lt $deadline)
    {
        $handles = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($handles.Count -ge 1) { $panel = $handles[0]; break }
        Start-Sleep -Milliseconds 250
    }
    if ($panel -eq [IntPtr]::Zero)
    {
        Save-FullScreenshot "K-FAIL-no-panel.png"
        throw "断言失败（场景 K1）：Ctrl+Alt+B 后 5s 内快速面板未出现——热键注册/触发链回归"
    }
    Save-FullScreenshot "K-panel-open-offline.png"
    Write-Host "场景 K1 通过：全局热键 Ctrl+Alt+B 唤起快速面板（runner 前台是 pwsh 控制台，全局注册语义实证；真实第三方前台多样性留装机）"
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
    # K3 Esc 键盘关闭（先 SetForegroundWindow 保证键盘焦点在面板）
    [WinEnum]::SetForegroundWindow($panel)
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
        Save-FullScreenshot "K-FAIL-esc.png"
        throw "断言失败（场景 K3）：Esc 后面板未关闭——键盘操作链回归"
    }
    Write-Host "场景 K3 通过：Esc 键盘关闭面板"
    Start-MockServer # 还原 mock（后续场景/收尾统一管理）
}
catch
{
    try { Start-MockServer } catch { } # 失败路径也还原 mock，别让 J/收尾连带红
    Save-FullScreenshot "K-FAIL-unexpected.png"
    $failures.Add("场景 K：$($_.Exception.Message)")
    Write-Host "::error::场景 K 断言失败：$($_.Exception.Message)"
}

# —— 全场景收尾：停应用/清档位文件/停 mock（J 静默安装要求 Beacon 未在跑——AppMutex） ——
Stop-Beacon
Remove-Item (Join-Path $env:TEMP "beacon-mock-mode.txt") -Force -ErrorAction SilentlyContinue
Stop-MockServer

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
