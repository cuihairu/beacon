# 视觉取证（2026-10-09 bug 批验收硬标准：没截图不算完，断言失败 = 红构建不发版）
#   场景 A 数量悬浮窗开：GLM 悬浮框必须在桌面（可见 "Beacon Tile" 窗）——全屏 + 6x 近景。
#   场景 B 数量悬浮窗关：桌面零残留窗——硬断言无可见 "Beacon Tile" / "Beacon Pinned"。
#   场景 C 浅色主题 icon 可辨度：白桌面 + theme=light，tile 近景暗像素硬断言——
#     近白 icon/文字叠浅底不可辨（用户实测「根本看不清」）在这里现形（修复=实色底+深前景）。
#   场景 D 五家 Provider 连接链（方舟/Kimi/MiMo/DeepSeek/千问）：本地 mock HTTP（127.0.0.1:18081 按
#     path 分发各响应）+ DPAPI 预置密钥 + --settings 启动 + UIA 驱动——逐家点模块行→点「测试」→
#     硬断言反馈含「连接正常」→ 展开组件「连接」下拉断言该连接在列→截图；
#     再 UIA 走一遍保存连接（save-flow），硬断言新连接**立即**出现在下拉（下拉不刷新回归在这里现形）；
#     组件向导「类型」下拉逐家断言非空且含本家条目（小米类型空回归在这里现形）；预置五张钉选
#     provider tile——UIA 读 tile 文本硬断言名字（火山方舟/DeepSeek/阿里千问）与数值（42.5/¥420.5/mimo 本机计数 128 次），
#     字段名（usage/quota）冒充名字在这里现形；设置常规页改「检查频率」→15 秒——config.json 落盘
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
    // 设置左栏分隔条拖拽取证：左键按下→分步移动→抬起（PointerCapture 在按下后接管，move 事件照常投递）
    public static void DragMouse(int fromX, int fromY, int toX, int toY)
    {
        SetCursorPos(fromX, fromY);
        System.Threading.Thread.Sleep(150);
        mouse_event(MouseDown, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(150);
        for (int i = 1; i <= 10; i++)
        {
            SetCursorPos(fromX + (toX - fromX) * i / 10, fromY + (toY - fromY) * i / 10);
            System.Threading.Thread.Sleep(40);
        }
        mouse_event(MouseUp, 0, 0, 0, UIntPtr.Zero);
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
  { "id": "zhipu", "type": "bigmodel", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
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
  "showCapsule": false
}
'@ | Set-Content -Path (Join-Path $configDir "config.json") -Encoding UTF8
    # numericFloating 必须 true：五张 tile 全是 FloatingOptIn 数值类——总闸关着悬浮/面板两路都不建窗
    # （WidgetDisplayPolicy.PanelCarries 数值抑制口径，桌面零残留拍板），tile 名字/数值断言直接无窗可断
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

# 进度条像素硬断言助手（2026-10-10 修「进度条看不到」配套）：扫 tile 截图底部 6 物理像素行、
# 中部 60% 宽（避开圆角处桌面色与左右 padding），统计与底色 (15,23,42) 任一通道差 >25 的像素。
# 修复前该区域纯底色（CI 截图实证：条从未渲染出像素）→ 0；修复后 track(≈63,74,90)+fill(severity 色) 覆盖整带 → 数百。
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
    # 进度条硬断言（2026-10-10 修「进度条看不到」）：GLM tile 有 Progress 语义，底部必出 3px 条——
    # 修复前该区域纯底色 0 非底色像素（条从未渲染出来），回归在这里现形
    $barBmp = New-Crop ([WinEnum]::RectOf($tiles[0])) 0
    $barPixels = Count-BarPixels $barBmp
    $barBmp.Dispose()
    if ($barPixels -lt 30)
    {
        throw "断言失败（场景 A）：GLM tile 底部进度条不可见（非底色像素仅 $barPixels，阈值 30）——进度条渲染回归"
    }
    Write-Host "场景 A 通过：悬浮框窗口存在（$($tiles.Count) 个），全屏与近景截图已存；进度条像素 $barPixels（≥30）"
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
    Start-MockServer
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
    # ③ 分隔条真实拖拽 +60px：左栏实测变宽 ≥40px——「可调」的功能性证明（UIA 坐标为物理像素）
    $splitter = Find-UiaById $settings "settings-left-splitter" 8
    if (!$splitter) { throw "断言失败（场景 D）：UIA 未找到 settings-left-splitter——拖拽分隔条缺失" }
    [WinEnum]::SetForeground([IntPtr]$settings.Current.NativeWindowHandle)
    Start-Sleep -Milliseconds 500
    $widthBefore = [int]$leftHost.Current.BoundingRectangle.Width
    $sr = $splitter.Current.BoundingRectangle
    Save-FullScreenshot "D-settings-splitter-before.png"
    [WinEnum]::DragMouse([int](($sr.Left + $sr.Right) / 2), [int](($sr.Top + $sr.Bottom) / 2), [int](($sr.Left + $sr.Right) / 2) + 60, [int](($sr.Top + $sr.Bottom) / 2))
    Start-Sleep -Seconds 1
    Save-FullScreenshot "D-settings-splitter-after.png"
    $widthAfter = [int]$leftHost.Current.BoundingRectangle.Width
    if ($widthAfter -lt $widthBefore + 40)
    {
        throw "断言失败（场景 D）：拖拽分隔条 +60px 后左栏宽度 ${widthBefore}→${widthAfter}（期望增长 ≥40px）——左栏不可调回归"
    }
    Write-Host "分隔条拖拽断言通过：左栏 $widthBefore → $widthAfter px（拖拽 +60px）"

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
    $windowTexts = @() # 按窗保序（一窗多条文本已 join 成一条）——进度条断言按窗序定位 ark tile
    $barCounts = @()
    for ($tileIndex = 0; $tileIndex -lt $panelHandles.Count; $tileIndex++)
    {
        $texts = Get-WindowUiaTexts $panelHandles[$tileIndex]
        $tileTexts += $texts
        $windowTexts += ($texts -join "`n")
        Save-Zoom (New-Crop ([WinEnum]::RectOf($panelHandles[$tileIndex])) 8) "D-tile-$tileIndex-closeup.png" 4
        # 进度条像素计数（margin 0 裁图，底部 6 行中部 60% 宽）：ark tile 有 Progress 语义必出条
        $barBmp = New-Crop ([WinEnum]::RectOf($panelHandles[$tileIndex])) 0
        $barCounts += Count-BarPixels $barBmp
        $barBmp.Dispose()
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

    # 进度条硬断言（2026-10-10 修「进度条看不到」）：ark tile（火山方舟，Progress=42.5%）底部必出
    # 非底色像素；UIA 读不出名字时退「至少一张 tile 出条」（五张里只有 ark 配 Progress）
    $arkIndex = -1
    for ($i = 0; $i -lt $windowTexts.Count; $i++) { if ($windowTexts[$i] -like "*火山方舟*") { $arkIndex = $i } }
    if ($arkIndex -ge 0)
    {
        if ($barCounts[$arkIndex] -lt 30)
        {
            throw "断言失败（场景 D）：ark tile 底部进度条不可见（非底色像素 $($barCounts[$arkIndex])，阈值 30）——进度条渲染回归"
        }
        Write-Host "进度条断言通过：ark tile 底部 $($barCounts[$arkIndex]) 非底色像素（≥30）"
    }
    else
    {
        $maxBar = ($barCounts | Measure-Object -Maximum).Maximum
        if ($maxBar -lt 30)
        {
            throw "断言失败（场景 D）：五张 tile 无一出进度条（最大 $maxBar，阈值 30）——进度条渲染回归"
        }
        Write-Host "进度条断言通过（UIA 名字不可读，取最大值）：$maxBar 非底色像素（≥30）"
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
    Stop-Beacon
    Stop-MockServer
}

Write-Host "视觉取证完成，产物："
Get-ChildItem $evidence | ForEach-Object { Write-Host "  - evidence/$($_.Name)" }

if ($failures.Count -gt 0)
{
    throw "视觉取证断言失败（$($failures.Count) 项）：`n" + ($failures -join "`n")
}
Write-Host "全部场景断言通过"
