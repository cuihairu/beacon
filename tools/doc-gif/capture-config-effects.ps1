<#
  DOC-GIF capture half (2026-11-01 user order, "same as kode todo C-24"):
  for every switch/config item that has an effect only after it is switched on, capture an
  off/on pair (plus motion frames where the effect is animation) so docs can show them as
  images and animated GIFs.

  Runs on a Windows machine with a built Beacon. Window-level PrintWindow capture is used on
  purpose: it renders the live window content and is independent of what the console session
  currently displays (locked console / another virtual desktop / screen saver), so the
  pipeline stays re-runnable unattended.

  Usage (PowerShell 5.1):
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\doc-gif\capture-config-effects.ps1 `
        -Publish C:\beacon-verify\publish\Beacon.App.exe `
        -OutDir  C:\beacon-verify\gif-frames
  Then assemble on any machine with Python+Pillow:
    python tools/doc-gif/assemble.py C:\beacon-verify\gif-frames docs/img/config-effects
#>
[CmdletBinding()]
param(
    [string]$Publish = "C:\beacon-verify\publish\Beacon.App.exe",
    [string]$OutDir = "",
    [string]$ConfigDir = "",
    [int]$SettleSeconds = 6,
    [int]$MotionFrames = 4,
    [int]$MotionFrameMs = 450,
    [string[]]$Only = @()   # capture just these keys (cheap re-run after a fix)
)
$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -ne 5) { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath @PSBoundParameters; exit $LASTEXITCODE }

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent   # tools/doc-gif -> repo root
if (!(Test-Path (Join-Path $repoRoot "mkdocs.yml"))) { $repoRoot = $null }  # copied elsewhere (e.g. a remote run dir)
if (-not $ConfigDir) { $ConfigDir = Join-Path $env:APPDATA "Beacon" }
if (-not $OutDir) {
    $OutDir = if ($repoRoot) { Join-Path $repoRoot "docs\img\config-effects-src" }
              else { Join-Path $env:TEMP "beacon-gif-frames" }
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class Cap {
  [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
  private delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static IntPtr FindByTitle(string exact) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      if (IsWindowVisible(h)) {
        var sb = new StringBuilder(512); GetWindowText(h, sb, 512);
        if (sb.ToString() == exact) { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static bool Rect(IntPtr h, out int x, out int y, out int w, out int hh) {
    RECT r; x = 0; y = 0; w = 0; hh = 0;
    if (!GetWindowRect(h, out r)) return false;
    x = r.Left; y = r.Top; w = r.Right - r.Left; hh = r.Bottom - r.Top;
    return (w > 0 && hh > 0 && w <= 4000 && hh <= 4000);
  }
}
"@
# bitmap work stays in PowerShell: Add-Type cannot compile System.Drawing types here
# (PS 5.1 already loaded System.Drawing, the compiler reports a duplicate/ambiguous Bitmap)
function Save-Window([IntPtr]$hwnd, [string]$path)
{
    $x = 0; $y = 0; $w = 0; $h = 0
    if (-not [Cap]::Rect($hwnd, [ref]$x, [ref]$y, [ref]$w, [ref]$h)) { return $false }
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc(); [Cap]::PrintWindow($hwnd, $hdc, 2) | Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return $true
}

function Get-Config { Get-Content (Join-Path $ConfigDir "config.json") -Raw | ConvertFrom-Json }
# ConvertFrom-Json gives PSCustomObject: assigning a property that does not exist yet throws
# ("在此对象上找不到属性"), so every nested write goes through these two helpers.
function Ensure-Prop($obj, [string]$name, $initial)
{
    if (-not $obj.PSObject.Properties[$name]) { $obj | Add-Member -NotePropertyName $name -NotePropertyValue $initial }
    return $obj.$name
}
function Set-Prop($obj, [string]$name, $value)
{
    if ($obj.PSObject.Properties[$name]) { $obj.$name = $value }
    else { $obj | Add-Member -NotePropertyName $name -NotePropertyValue $value }
}
function Set-Config([scriptblock]$mutate)
{
    $cfg = Get-Config
    & $mutate $cfg
    $cfg | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $ConfigDir "config.json") -Encoding UTF8
}
function Restart-Beacon
{
    Get-Process Beacon.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    Start-Process -FilePath $Publish | Out-Null
    Start-Sleep -Seconds 14
}
function Ensure-Board
{
    # the L2 board is hidden until it is summoned; open it the same way the hotkey handler does
    $h = [Cap]::FindByTitle($boardTitle)
    if ($h -eq [IntPtr]::Zero) {
        Add-Type @"
using System; using System.Runtime.InteropServices;
public static class MsgW {
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
  [DllImport("user32.dll")] public static extern IntPtr PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  // HWND_MESSAGE parent: the tray/message window lives on the message-only desktop
  public static IntPtr FindMsgWindow(string cls) { return FindWindowEx(new IntPtr(-3), IntPtr.Zero, cls, null); }
}
"@
        $msg = [MsgW]::FindMsgWindow("BeaconMessageWindow")
        if ($msg -ne [IntPtr]::Zero) { [MsgW]::PostMessage($msg, 0x0312, [IntPtr]1, [IntPtr]0) | Out-Null; Start-Sleep -Seconds 3 }
    }
}
function Save-Surface([string]$name, [string[]]$titles)
{
    if ($titles -contains $boardTitle) { Ensure-Board }
    # one image per key: the first surface in the caller's priority order that is actually on
    # screen (board > host panel > floating tile > capsule), so off/on pairs stay comparable
    foreach ($t in $titles) {
        $h = [Cap]::FindByTitle($t)
        if ($h -ne [IntPtr]::Zero) {
            $path = Join-Path $OutDir "$name.png"
            if (Save-Window $h $path) { return @($path) }
        }
    }
    Write-Warning "[$name] no target window visible: $($titles -join ', ')"
    return @()
}
function Save-Motion([string]$name, [string[]]$titles)
{
    if ($titles -contains $boardTitle) { Ensure-Board }
    for ($i = 1; $i -le $MotionFrames; $i++) {
        foreach ($t in $titles) {
            $h = [Cap]::FindByTitle($t)
            if ($h -ne [IntPtr]::Zero) { Save-Window $h (Join-Path $OutDir "$name-f$i.png") | Out-Null }
        }
        Start-Sleep -Milliseconds $MotionFrameMs
    }
}

if (!(Test-Path $Publish)) { throw "publish build not found: $Publish" }
$script:log = @()

# ---- canonical surfaces -------------------------------------------------------
$panelTitles = @("Beacon Pinned")            # L0 host panel (pinDisplayMode=panel)
$tileTitles  = @("Beacon Tile")              # L0 standalone floating tiles
$capsuleTitle = "Beacon Status"              # L1 capsule / island
$boardTitle  = "Beacon 快速面板"              # L2 board

# make sure the app runs with a deterministic baseline before the first pair
if (@(Get-Process Beacon.App -ErrorAction SilentlyContinue).Count -eq 0) {
    Start-Process -FilePath $Publish | Out-Null
    Start-Sleep -Seconds 14
}

$swaps = @(
    @{ key = "theme"; restart = $true; targets = @($boardTitle, $capsuleTitle, $panelTitles[0])
       off = { param($c) Set-Prop $c "theme" "light" }
       on  = { param($c) Set-Prop $c "theme" "dark" } },
    @{ key = "progress-style"; restart = $false; targets = @($panelTitles[0], $tileTitles[0])
       off = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); Set-Prop $app "progressStyle" "pool" }
       on  = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); Set-Prop $app "progressStyle" "bar" } },
    @{ key = "motion-mode"; restart = $false; motion = $true; targets = @($panelTitles[0], $tileTitles[0])
       off = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); $m = Ensure-Prop $app "motion" ([pscustomobject]@{}); Set-Prop $m "mode" "off" }
       on  = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); $m = Ensure-Prop $app "motion" ([pscustomobject]@{}); Set-Prop $m "mode" "full" } },
    @{ key = "density"; restart = $true; targets = @($boardTitle)
       off = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); Set-Prop $app "density" "regular" }
       on  = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); Set-Prop $app "density" "spacious" } },
    @{ key = "severity-color-warning"; restart = $false; targets = @($capsuleTitle, $panelTitles[0], $tileTitles[0])
       off = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); $sc = Ensure-Prop $app "severityColors" ([pscustomobject]@{}); Set-Prop $sc "warning" "#d29922" }
       on  = { param($c) $app = Ensure-Prop $c "appearance" ([pscustomobject]@{}); $sc = Ensure-Prop $app "severityColors" ([pscustomobject]@{}); Set-Prop $sc "warning" "#ff00ff" } },
    @{ key = "ui-opacity"; restart = $false; targets = @($boardTitle, $capsuleTitle)
       off = { param($c) Set-Prop $c "uiOpacity" 1.0 }
       on  = { param($c) Set-Prop $c "uiOpacity" 0.5 } },
    @{ key = "capsule-island"; restart = $false; targets = @($capsuleTitle)
       off = { param($c) Set-Prop $c "capsuleIsland" $false }
       on  = { param($c) Set-Prop $c "capsuleIsland" $true } },
    @{ key = "pin-display-mode"; restart = $true; targets = @($panelTitles[0], $tileTitles[0])
       off = { param($c) Set-Prop $c "pinDisplayMode" "panel" }
       on  = { param($c) Set-Prop $c "pinDisplayMode" "floating" } },
    @{ key = "widget-color-override"; restart = $false; targets = @($panelTitles[0], $tileTitles[0]); widgetId = "bigmodel.usage:GLM"
       off = { }; on = { } }   # handled separately below (widgets.json, not config.json)
)

$widgetsPath = Join-Path $ConfigDir "widgets.json"

foreach ($swap in $swaps)
{
    $key = $swap.key
    if ($Only -and ($Only -notcontains $key)) { continue }
    try {
        if ($swap.key -eq "widget-color-override") {
            foreach ($state in @("off", "on")) {
                $widgets = Get-Content $widgetsPath -Raw | ConvertFrom-Json
                $w = $widgets | Where-Object { $_.id -eq $swap.widgetId }
                if ($state -eq "off") { $w.PSObject.Properties.Remove("colorOverride") }
                else { if ($w.PSObject.Properties["colorOverride"]) { $w.colorOverride = "#00e5ff" } else { $w | Add-Member colorOverride "#00e5ff" } }
                $widgets | ConvertTo-Json -Depth 30 | Set-Content $widgetsPath -Encoding UTF8
                Restart-Beacon
                Start-Sleep -Seconds $SettleSeconds
                Save-Surface "$key-$state" $swap.targets | Out-Null
                $script:log += "$key-$state"
            }
            continue
        }

        foreach ($state in @("off", "on")) {
            $mutator = if ($state -eq "off") { $swap.off } else { $swap.on }
            Set-Config $mutator
            if ($swap.restart) { Restart-Beacon } else { # live effect: nudge the app to re-read config.json
                Get-Process Beacon.App -ErrorAction SilentlyContinue | Stop-Process -Force
                Start-Sleep -Seconds 2
                Start-Process -FilePath $Publish | Out-Null
                Start-Sleep -Seconds 14
            }
            Start-Sleep -Seconds $SettleSeconds
            if ($swap.motion) { Save-Motion "$key-$state" $swap.targets }
            else { Save-Surface "$key-$state" $swap.targets | Out-Null }
            $script:log += "$key-$state"
        }
    } catch {
        Write-Warning "[$key] failed: $($_.Exception.Message)"
        $script:log += "$key-FAILED:$($_.Exception.Message)"
    }
}

$script:log | Set-Content (Join-Path $OutDir "capture-manifest.txt") -Encoding UTF8
Write-Output ("captured -> " + $OutDir)
$script:log | ForEach-Object { Write-Output ("  " + $_) }
