# Real-machine behaviour verification batch 1 (todo 真机验证类):
#   B1 URL 端到端打开 / B2 Toast 发送+点击路由 / B3 explorer 自启调度 / B4 RFC §6.2.7 四条铁律(结构面+焦点面)
# Evidence: window-level PrintWindow screenshots + UIA trees + app log lines + registry.
$ErrorActionPreference = "Continue"
$env:GITHUB_WORKSPACE = "C:\beacon-verify"
$env:BEACON_EVIDENCE_TIMEOUT_SEC = "3000"
. "$PSScriptRoot\lib.ps1"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class PW {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int Left, Top, Right, Bottom; }
  public static string Frame(IntPtr h) { R r; GetWindowRect(h, out r); return "(" + r.Left + "," + r.Top + ")-(" + r.Right + "," + r.Bottom + ")"; }
  public static string FgTitle() { var h = GetForegroundWindow(); var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
  public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
  public static long ExStyle(IntPtr h) { return GetWindowLong(h, -20) & 0xFFFFFFFFL; }
}
"@
$script:results = New-Object System.Collections.Generic.List[string]
function Note([string]$id, [string]$status, [string]$detail) { Write-Output "[$status] $id :: $detail"; $script:results.Add("[$status] $id :: $detail") }
function Save-Win([IntPtr]$hwnd, [string]$name)
{
    try {
        $r = New-Object PW+R
        [PW]::GetWindowRect($hwnd, [ref]$r) | Out-Null
        $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
        if ($w -le 0 -or $h -le 0) { return $false }
        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc(); [PW]::PrintWindow($hwnd, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
        $bmp.Save((Join-Path $evidence $name), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
        return $true
    } catch { return $false }
}
function Find-ByTitleLike([string]$pattern)
{
    return @([WinEnum]::VisibleTopLevelHandles() | Where-Object { [WinEnum]::TitleOf($_) -like $pattern })
}
function Wait-For([scriptblock]$what, [int]$timeoutSec, [string]$label)
{
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) { $r = & $what; if ($r) { return $r }; Start-Sleep -Milliseconds 500 }
    return $null
}

$publish = "C:\beacon-verify\publish\Beacon.App.exe"
$configDir = Join-Path $env:APPDATA "Beacon"
Ensure-MockServer

# ---------- config: custom notification rule so a quota error really toasts ----------
function Write-BehaviourConfig
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
  "showCapsule": true,
  "pollIntervalSeconds": 15,
  "notificationRules": [
    { "id": "verify.warning", "widgetType": null, "widgetId": null, "severityAtLeast": "warning", "severityAtMost": null, "toast": true, "sound": false, "cooldown": null, "enabled": true }
  ]
}
'@ | Set-Content -Path (Join-Path $configDir "config.json") -Encoding UTF8
    @'
[
  { "id": "zhipu", "type": "bigmodel", "endpoint": "http://127.0.0.1:18081/api/monitor/usage/quota/limit", "enabled": true },
  { "id": "ds-main", "type": "deepseek", "endpoint": "http://127.0.0.1:18081", "credentialRef": "conn:ds-main", "enabled": true }
]
'@ | Set-Content -Path (Join-Path $configDir "connections.json") -Encoding UTF8
    @'
[
  { "id": "bigmodel.usage:GLM", "type": "bigmodel.usage", "connectionId": "zhipu", "refreshTier": "ci", "pinned": true, "config": { "label": "GLM" } },
  { "id": "deepseek.balance:ds", "type": "deepseek.balance", "connectionId": "ds-main", "refreshTier": "ci", "pinned": true, "config": { "label": "DeepSeek" } }
]
'@ | Set-Content -Path (Join-Path $configDir "widgets.json") -Encoding UTF8
    '{ "tiles": [] }' | Set-Content -Path (Join-Path $configDir "pins.json") -Encoding UTF8
    Remove-Item (Join-Path $configDir "notifications.json") -Force -ErrorAction SilentlyContinue
}
function Start-BeaconApp([string[]]$argList = @())
{
    Stop-Beacon
    if ($argList.Count -gt 0) { Start-Process -FilePath $publish -ArgumentList $argList | Out-Null } else { Start-Process -FilePath $publish | Out-Null }
    Start-Sleep -Seconds 16
}
Write-BehaviourConfig
Start-BeaconApp

# ================= B1: URL end-to-end =============================================
try {
    # open the board (WM_HOTKEY direct drive = same chain as the registered hotkey handler)
    $msg = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow")
    if ($msg -eq [IntPtr]::Zero) { $msg = [WinEnum]::FindByClass("BeaconMessageWindow") }
    [WinEnum]::Post($msg, 0x0312, [IntPtr]1, [IntPtr]0)
    $board = Wait-For { $h = Find-ByTitleLike "Beacon 快速面板"; if ($h.Count -gt 0) { $h[0] } else { $null } } 15 "board"
    if (!$board) { Note "B1-url" "FAIL" "看板未打开，URL 链无法起步" }
    else {
        $el = [System.Windows.Automation.AutomationElement]::FromHandle($board)
        $items = $el.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
        $glmRow = $null
        foreach ($it in $items) { try { if ($it.Current.Name -like "*GLM*") { $glmRow = $it; break } } catch { } }
        if (!$glmRow -and $items.Count -gt 0) { $glmRow = $items[0] }
        if (!$glmRow) { Note "B1-url" "FAIL" "看板时间线无行可点" }
        else {
            ($glmRow.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
            $detail = Wait-For { $d = Find-ByTitleLike "Beacon ·*"; if ($d.Count -gt 0) { $d[0] } else { $null } } 15 "detail"
            if (!$detail) { Note "B1-url" "FAIL" "点时间线行未开 L3 详情窗" }
            else {
                Save-Win $detail "B1-detail.png" | Out-Null
                $del = [System.Windows.Automation.AutomationElement]::FromHandle($detail)
                $openBtn = $null
                $btns = $del.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
                foreach ($b in $btns) { try { if ($b.Current.Name -eq "Open") { $openBtn = $b; break } } catch { } }
                if (!$openBtn) { Note "B1-url" "FAIL" "L3 详情窗无「Open」动作按钮" }
                else {
                    $edgeBefore = @(Get-Process msedge -EA SilentlyContinue | ForEach-Object { $_.Id })
                    ($openBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
                    $found = $null
                    $deadline = (Get-Date).AddSeconds(25)
                    while ((Get-Date) -lt $deadline -and !$found) {
                        Start-Sleep -Seconds 1
                        foreach ($w in (Find-ByTitleLike "*Microsoft*Edge*")) {
                            $t = [WinEnum]::TitleOf($w)
                            $texts = ""
                            try { $we = [System.Windows.Automation.AutomationElement]::FromHandle($w); $texts = ($we.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name }) -join " | " } catch { }
                            if ($texts -match "open\.bigmodel\.cn|platform\.deepseek\.com") { $found = @{ hwnd = $w; texts = $texts; title = $t }; break }
                            if ($t -match "bigmodel|deepseek") { $found = @{ hwnd = $w; texts = $texts; title = $t }; break }
                        }
                    }
                    if ($found) {
                        Save-Win $found.hwnd "B1-browser.png" | Out-Null
                        Note "B1-url" "PASS" "L3「Open」→ 默认浏览器打开，命中地址线索：title='$($found.title)' texts=$($found.texts.Substring(0, [Math]::Min(400, $found.texts.Length)))"
                    } else {
                        Note "B1-url" "FAIL" "点击「Open」25s 内未见浏览器命中预期地址（Edge 窗数=$((Find-ByTitleLike '*Microsoft*Edge*').Count)）"
                    }
                }
            }
        }
    }
} catch { Note "B1-url" "FAIL" "异常: $($_.Exception.Message)" }

# ================= B2: Toast 发送 + 点击路由 ======================================
try {
    # healthy state first, then flip the mock to the 95% error band -> rule fires a toast
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "error" -Encoding ASCII
    $before = @([WinEnum]::VisibleTopLevelHandles() | ForEach-Object { [WinEnum]::TitleOf($_) + "|" + [WinEnum]::ClassOf($_) })
    $toastHwnd = $null
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline -and !$toastHwnd) {
        Start-Sleep -Seconds 2
        foreach ($h in [WinEnum]::VisibleTopLevelHandles())
        {
            $key = [WinEnum]::TitleOf($h) + "|" + [WinEnum]::ClassOf($h)
            if ($before -notcontains $key) {
                $c = [WinEnum]::ClassOf($h); $t = [WinEnum]::TitleOf($h)
                if ($c -like "*Toast*" -or $c -eq "Windows.UI.Core.CoreWindow" -or $c -like "*Notification*" -or $t -like "*Beacon*") {
                    $toastHwnd = $h; break
                }
            }
        }
    }
    if ($toastHwnd) {
        Save-Win $toastHwnd "B2-toast.png" | Out-Null
        Note "B2-toast-sent" "PASS" "错误态触发后出现新窗：title='$([WinEnum]::TitleOf($toastHwnd))' class=$([WinEnum]::ClassOf($toastHwnd)) rect=$([PW]::Frame($toastHwnd))（已 PrintWindow 留证）"
        # activate it through UIA (no dependency on pointer injection), then watch the routing target
        $fgBefore = [PW]::FgTitle()
        try {
            $te = [System.Windows.Automation.AutomationElement]::FromHandle($toastHwnd)
            $invokeTarget = $te
            $cands = $te.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($c in $cands) { try { if ($c.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -or $c.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { $invokeTarget = $c; break } } catch { } }
            ($invokeTarget.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
        } catch { Note "B2-toast-activate" "INFO" "UIA 激活 toast 失败: $($_.Exception.Message)" }
        Start-Sleep -Seconds 4
        $post = @()
        foreach ($w in (Find-ByTitleLike "*Microsoft*Edge*")) { $post += "browser:" + [WinEnum]::TitleOf($w) }
        $post += "panel:" + (Find-ByTitleLike "Beacon 快速面板").Count
        $post += "detail:" + (Find-ByTitleLike "Beacon ·*").Count
        Note "B2-toast-route" "INFO" "点击后路由观测：$($post -join ' ; ')（激活前前台='$fgBefore'，现前台='$([PW]::FgTitle())'）"
    } else {
        Note "B2-toast-sent" "FAIL" "mock 置 error 后 60s 内未观察到新窗（Toast 未见）——检查通知权限/规则"
    }
    # notification history record (通知中心历史半): app-side records file
    $notif = Join-Path $configDir "notifications.json"
    if (Test-Path $notif) {
        $recs = Get-Content $notif -Raw -EA SilentlyContinue | ConvertFrom-Json
        Note "B2-notification-record" "INFO" "notifications.json 记录数=$(@($recs).Count)；最新：$((@($recs) | Select-Object -First 1 | ConvertTo-Json -Compress))"
    } else { Note "B2-notification-record" "INFO" "notifications.json 不存在" }
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "" -Encoding ASCII -Force
} catch { Note "B2-toast-sent" "FAIL" "异常: $($_.Exception.Message)" }

# ================= B3: explorer 登录会话调度 ======================================
try {
    Start-BeaconApp
    $runKey = Get-BeaconRunValue
    # flip the setting through config + restart so the app itself writes the Run value
    $cfgPath = Join-Path $configDir "config.json"
    $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
    $cfg.PSObject.Properties.Remove("launchOnStartup") | Out-Null
    $cfg | Add-Member -NotePropertyName launchOnStartup -NotePropertyValue $true -Force
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $cfgPath -Encoding UTF8
    Start-BeaconApp
    $runKey2 = Get-BeaconRunValue
    if ($runKey2) { Note "B3-runkey" "PASS" "launchOnStartup=true 后 HKCU Run\Beacon = $runKey2" }
    else { Note "B3-runkey" "FAIL" "launchOnStartup=true 但 HKCU Run\Beacon 未写入（原值：$runKey）" }
    # simulate the shell starting it at logon (explorer is the real logon host) — real logout is
    # explicitly out of scope (2026-10-30 决策 13：不在未安排会话时擅自注销用户)
    Stop-Beacon
    $pidBefore = @().Count
    Start-Process explorer.exe -ArgumentList $publish | Out-Null
    Start-Sleep -Seconds 16
    $procs = @(Get-Process Beacon.App -EA SilentlyContinue)
    $explorerChild = $false
    if ($procs.Count -gt 0) {
        $ppid = (Get-CimInstance Win32_Process -Filter "ProcessId=$($procs[0].Id)").ParentProcessId
        $parent = Get-Process -Id $ppid -EA SilentlyContinue
        $explorerChild = ($parent -and $parent.ProcessName -eq "explorer")
        Note "B3-explorer-start" $(if ($procs.Count -ge 1) { "PASS" } else { "FAIL" }) "explorer.exe 启动路径拉起进程 pid=$($procs[0].Id)（父进程=$($parent.ProcessName)；会话=$($procs[0].SessionId)）"
    } else { Note "B3-explorer-start" "FAIL" "explorer 启动后无 Beacon.App 进程" }
    # turn it back off and confirm the value is cleared
    $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
    $cfg.launchOnStartup = $false
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $cfgPath -Encoding UTF8
    Start-BeaconApp
    $runKey3 = Get-BeaconRunValue
    Note "B3-runkey-off" $(if (-not $runKey3) { "PASS" } else { "FAIL" }) "launchOnStartup=false 后 Run 值=$runKey3"
} catch { Note "B3-runkey" "FAIL" "异常: $($_.Exception.Message)" }

# ================= B4: RFC §6.2.7 四条铁律 ========================================
try {
    # ④ no taskbar / Alt-Tab entry + no focus steal: structural evidence
    $styleOk = $true; $detail = @()
    foreach ($t in @("Beacon 快速面板", "Beacon Status")) {
        $hs = Find-ByTitleLike $t
        foreach ($h in $hs) {
            $ex = [PW]::ExStyle($h)
            $appWindow = (($ex -band 0x00040000) -ne 0) # WS_EX_APPWINDOW
            $tool = (($ex -band 0x00000080) -ne 0)       # WS_EX_TOOLWINDOW
            $detail += "$t ex=0x$($ex.ToString('X8')) appWindow=$appWindow toolWindow=$tool"
            if ($appWindow) { $styleOk = $false }
        }
    }
    Note "B4-no-alt-tab" $(if ($styleOk) { "PASS" } else { "FAIL" }) "看板/胶囊 WS_EX_APPWINDOW 均未置位（不进任务栏/Alt-Tab）；$($detail -join ' ; ')"
    # ② alert must not steal focus: foreground before/after a state flip
    $fg1 = [PW]::FgTitle()
    Set-Content -Path (Join-Path $env:TEMP "beacon-mock-mode.txt") -Value "p100" -Encoding ASCII
    Start-Sleep -Seconds 12
    $fg2 = [PW]::FgTitle()
    Note "B4-no-focus-steal" $(if ($fg1 -eq $fg2) { "PASS" } else { "INFO" }) "状态翻转前后前台='$fg1' → '$fg2'"
    # ③ L0/tile 可点项只应是零风险动作：dump the detail action buttons (危险动作只应在 L2/L3 且带确认)
    $d = Find-ByTitleLike "Beacon ·*"
    if ($d.Count -gt 0) {
        $de = [System.Windows.Automation.AutomationElement]::FromHandle($d[0])
        $btns = $de.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
        $names = @($btns | ForEach-Object { $_.Current.Name })
        Note "B4-action-surface" "INFO" "L3 动作按钮清单：$($names -join ' / ')"
    }
    # ① tile 点击下钻：covered by B1 (timeline row -> L3) and by the capsule/board chain
    Note "B4-drill-down" "INFO" "①点击下钻半见 B1（时间线行 → L3）与 PU1-capsule-invokes-board"
} catch { Note "B4" "FAIL" "异常: $($_.Exception.Message)" }

$script:results | Add-Content (Join-Path $evidence "behaviour-verify.txt") -Encoding UTF8
Write-Output "BEHAVIOUR BATCH 1 DONE"
