# PANEL-UPGRADE-1 real-machine verification (todo 面板升级看板 2026-11-05 用户令).
# Evidence is window-level PrintWindow capture + UIA text/geometry assertions: the .5 console's
# GDI screen capture returns a stale wallpaper-only frame (see docs/acceptance-evidence note),
# while PrintWindow renders the live window content.
$ErrorActionPreference = "Continue"
$env:GITHUB_WORKSPACE = "C:\beacon-verify"
$env:BEACON_EVIDENCE_TIMEOUT_SEC = "2400"
. "$PSScriptRoot\lib.ps1"
$script:results = New-Object System.Collections.Generic.List[string]
function Note([string]$id, [string]$status, [string]$detail)
{
    $script:results.Add("[$status] $id :: $detail")
    Write-Output "[$status] $id :: $detail"
}
function Save-Win([IntPtr]$hwnd, [string]$name)
{
    if ($hwnd -eq [IntPtr]::Zero) { return "" }
    $r = New-Object PW+R
    if (-not [PW]::GetWindowRect($hwnd, [ref]$r)) { return "" }
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    if ($w -le 0 -or $h -le 0) { return "" }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [PW]::PrintWindow($hwnd, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save((Join-Path $evidence $name), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return $name
}
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class PW {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  public struct R { public int Left, Top, Right, Bottom; }
}
"@
function ClientSize([IntPtr]$hwnd)
{
    $r = New-Object PW+R
    [PW]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    return @{ Left = $r.Left; Top = $r.Top; Right = $r.Right; Bottom = $r.Bottom }
}
function Get-Board([int]$timeout = 25)
{
    $deadline = (Get-Date).AddSeconds($timeout)
    while ((Get-Date) -lt $deadline)
    {
        $h = [WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")
        if ($h.Count -gt 0 -and [WinEnum]::Visible($h[0])) { return $h[0] }
        Start-Sleep -Milliseconds 500
    }
    return [IntPtr]::Zero
}
function Open-Board
{
    $h = Get-Board 3
    if ($h -ne [IntPtr]::Zero) { return $h }
    # path 1: SendInput chord (works when the console session accepts injected input)
    [WinEnum]::SendChord(0x11, 0x42, 0x12) # Ctrl + B + Alt
    $h = Get-Board 4
    if ($h -ne [IntPtr]::Zero) { return $h }
    # path 2: WM_HOTKEY straight to the app message window (same fallback as CI scenario K1)
    $msgWindow = [WinEnum]::FindByClass("BeaconMessageWindow")
    if ($msgWindow -eq [IntPtr]::Zero) { $msgWindow = [WinEnum]::FindByClassMsgOnly("BeaconMessageWindow") }
    if ($msgWindow -ne [IntPtr]::Zero)
    {
        [WinEnum]::Post($msgWindow, 0x0312, [IntPtr]1, [IntPtr]0)
        $h = Get-Board 8
        if ($h -ne [IntPtr]::Zero) { return $h }
    }
    return [IntPtr]::Zero
}
function Close-Board
{
    $h = Get-Board 2
    if ($h -ne [IntPtr]::Zero) { [WinEnum]::ForceForeground($h); Start-Sleep -Milliseconds 400; [WinEnum]::SendVirtualKey(0x1B) }
    $deadline = (Get-Date).AddSeconds(6)
    while ((Get-Date) -lt $deadline)
    {
        if (@([WinEnum]::VisibleHandlesByTitle("Beacon 快速面板")).Count -eq 0) { return $true }
        Start-Sleep -Milliseconds 400
    }
    return $false
}

$publish = "C:\beacon-verify\publish\Beacon.App.exe"
# input-injection sanity: does SendInput reach this console session at all?
Add-Type -AssemblyName System.Windows.Forms
$beforePos = [System.Windows.Forms.Cursor]::Position
[WinEnum]::SetCursor(($beforePos.X + 120), ($beforePos.Y + 90))
Start-Sleep -Milliseconds 400
$afterPos = [System.Windows.Forms.Cursor]::Position
$moved = ([Math]::Abs($afterPos.X - $beforePos.X) + [Math]::Abs($afterPos.Y - $beforePos.Y))
Note "ENV-input-injection" $(if ($moved -gt 20) { "PASS" } else { "FAIL" }) "SetCursor 位移=${moved}px (before=$($beforePos.X),$($beforePos.Y) after=$($afterPos.X),$($afterPos.Y))"
$logTail = @()
$lp = Join-Path $configDir ("logs/beacon-" + (Get-Date -Format yyyyMMdd) + ".log")
if (Test-Path $lp) { $logTail = @(Select-String -Path $lp -Pattern "RegisterHotKey|热键" | Select-Object -Last 3 | ForEach-Object { $_.Line }) }
Note "ENV-hotkey-registration" "INFO" "日志热键行：$(($logTail -join ' || '))"
Ensure-MockServer
if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -eq 0) { Start-Process -FilePath $publish; Start-Sleep -Seconds 18 }

# ---- 1. default board geometry (720x560 可调) ----------------------------------
$board = Open-Board
if ($board -eq [IntPtr]::Zero) { Note "PU1-board-open" "FAIL" "热键 Ctrl+Alt+B 未唤起「Beacon 快速面板」"; }
else
{
    $rect = ClientSize $board
    $frameW = $rect.Right - $rect.Left; $frameH = $rect.Bottom - $rect.Top
    Save-Win $board "PU1-board-default.png" | Out-Null
    # client size via WinEnum (chrome-aware)
    $client = [WinEnum]::ClientRectOf($board)
    $cw = $client.Right - $client.Left; $ch = $client.Bottom - $client.Top
    if ([Math]::Abs($cw - 720) -le 3 -and [Math]::Abs($ch - 560) -le 3) {
        Note "PU1-default-size" "PASS" "看板客户区 ${cw}x${ch}px（目标 720x560 @100% DPI），外框 ${frameW}x${frameH}px"
    } else {
        Note "PU1-default-size" "FAIL" "看板客户区 ${cw}x${ch}px（期望 720x560 ±3），外框 ${frameW}x${frameH}px"
    }

    # ---- 2. timeline main area + retained header --------------------------------
    $texts = @(Get-WindowUiaTexts $board)
    $joined = ($texts -join " | ")
    foreach ($need in @("EVENT TIMELINE", "OVERVIEW", "NOTIFICATIONS", "NEEDS ATTENTION"))
    {
        if (@($texts | Where-Object { $_ -like "*$need*" }).Count -gt 0) { Note "PU1-section-$need" "PASS" "看板 UIA 含区块「$need」" }
        else { Note "PU1-section-$need" "FAIL" "看板 UIA 缺区块「$need」；实际：$joined" }
    }
    if (@($texts | Where-Object { $_ -match "\d+ warn" -or $_ -match "警告" }).Count -gt 0) {
        Note "PU1-overall-summary" "PASS" "头部 OverallSummary 保留：$(($texts | Where-Object { $_ -match 'warn|警告' }) -join ' / ')"
    } else { Note "PU1-overall-summary" "FAIL" "头部 OverallSummary 未见；实际：$joined" }
    $rows = @($texts | Where-Object { $_ -match "^\d{2}:\d{2}:\d{2}" })
    Note "PU1-timeline-rows" $(if ($rows.Count -gt 0) { "PASS" } else { "FAIL" }) "时间线行（时刻开头）数=$($rows.Count)；样例：$(($rows | Select-Object -First 3) -join ' || ')"

    # ---- 3. header drag (拖动整窗) ------------------------------------------------
    $before = ClientSize $board
    [WinEnum]::DragMouse(($before.Left + 200), ($before.Top + 10), ($before.Left + 380), ($before.Top + 130))
    Start-Sleep -Seconds 2
    $after = ClientSize $board
    $dx = $after.Left - $before.Left; $dy = $after.Top - $before.Top
    if ([Math]::Abs($dx) -ge 50 -and [Math]::Abs($dy) -ge 50) { Note "PU1-header-drag" "PASS" "头部把手拖动位移 dx=$dx dy=$dy px" }
    else { Note "PU1-header-drag" "FAIL" "头部拖动未生效（dx=$dx dy=$dy）" }
    Save-Win $board "PU1-board-dragged.png" | Out-Null

    # ---- 4. resize via thick frame ------------------------------------------------
    $before2 = ClientSize $board
    [WinEnum]::DragMouse(($before2.Right - 3), ($before2.Bottom - 3), ($before2.Right + 120), ($before2.Bottom + 90))
    Start-Sleep -Seconds 2
    $after2 = ClientSize $board
    $client2 = [WinEnum]::ClientRectOf($board)
    $cw2 = $client2.Right - $client2.Left; $ch2 = $client2.Bottom - $client2.Top
    if (($after2.Right - $after2.Left) -gt ($before2.Right - $before2.Left) + 60) {
        Note "PU1-resize" "PASS" "边框拖拽调尺寸生效：外框宽 $($before2.Right - $before2.Left) → $($after2.Right - $after2.Left)px，客户区 ${cw2}x${ch2}px"
    } else {
        Note "PU1-resize" "FAIL" "边框拖拽未生效：外框宽 $($before2.Right - $before2.Left) → $($after2.Right - $after2.Left)px"
    }
    Save-Win $board "PU1-board-resized.png" | Out-Null
    $savedW = $cw2; $savedH = $ch2; $savedL = $after2.Left; $savedT = $after2.Top

    # ---- 5. persistence across restart (shell-state 记忆) --------------------------
    Close-Board | Out-Null
    Stop-Beacon
    Start-Process -FilePath $publish
    Start-Sleep -Seconds 18
    $board2 = Open-Board
    if ($board2 -eq [IntPtr]::Zero) { Note "PU1-persist" "FAIL" "重启后热键未能再唤起看板" }
    else {
        $client3 = [WinEnum]::ClientRectOf($board2)
        $cw3 = $client3.Right - $client3.Left; $ch3 = $client3.Bottom - $client3.Top
        $rect3 = ClientSize $board2
        Save-Win $board2 "PU1-board-after-restart.png" | Out-Null
        $sizeOk = [Math]::Abs($cw3 - $savedW) -le 4 -and [Math]::Abs($ch3 - $savedH) -le 4
        $posOk = [Math]::Abs($rect3.Left - $savedL) -le 40 -and [Math]::Abs($rect3.Top - $savedT) -le 40
        if ($sizeOk -and $posOk) { Note "PU1-persist" "PASS" "重启后尺寸/位置还原：${cw3}x${ch3} @($($rect3.Left),$($rect3.Top))（重启前 ${savedW}x${savedH} @($savedL,$savedT)）" }
        else { Note "PU1-persist" "FAIL" "重启后未还原：${cw3}x${ch3} @($($rect3.Left),$($rect3.Top))（重启前 ${savedW}x${savedH} @($savedL,$savedT)）" }
    }

    # ---- 6. capsule click唤起看板 (悬浮岛/胶囊点击目标同步) -------------------------
    Close-Board | Out-Null
    $caps = [WinEnum]::VisibleHandlesByTitle("Beacon Status")
    if ($caps.Count -gt 0) {
        Save-Win $caps[0] "PU1-capsule.png" | Out-Null
        $cr = [WinEnum]::RectOf($caps[0])
        $cx = [int](($cr.Left + $cr.Right) / 2); $cy = [int](($cr.Top + $cr.Bottom) / 2)
        [WinEnum]::ClickAt($cx, $cy)
        $b3 = Get-Board 8
        if ($b3 -ne [IntPtr]::Zero) { Note "PU1-capsule-invokes-board" "PASS" "点击胶囊 ($cx,$cy) 唤起看板" ; Close-Board | Out-Null }
        else { Note "PU1-capsule-invokes-board" "FAIL" "点击胶囊 ($cx,$cy) 未唤起看板" }
    } else { Note "PU1-capsule-invokes-board" "FAIL" "未见胶囊窗「Beacon Status」" }
}

$script:results | Add-Content (Join-Path $evidence "panel-upgrade-verify.txt") -Encoding UTF8
Write-Output "PANEL-UPGRADE VERIFY DONE"
