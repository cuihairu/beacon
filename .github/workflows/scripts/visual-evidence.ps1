# 两态视觉取证（2026-10-09 bug 批验收硬标准：没截图不算完）
#   场景 A 数量悬浮窗开：GLM 悬浮框必须在桌面（可见 "Beacon Tile" 窗）——全屏 + 悬浮框 6x 近景
#     截图（品牌 icon 可辨认）；窗不存在 = 断言失败。
#   场景 B 数量悬浮窗关：桌面零残留窗——硬断言无可见 "Beacon Tile" / "Beacon Pinned"，附全屏截图。
# 预置 floating 模式 + bigmodel 连接 + GLM 钉选组件（无凭据：拉取失败不影响 icon/标签渲染）。
# 产物上传 run artifact（daily-build.yml 的 Upload visual evidence 步）；断言失败 = 红构建不发版。
$ErrorActionPreference = "Stop"
Set-Location $env:GITHUB_WORKSPACE

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
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
}
"@

$configDir = Join-Path $env:APPDATA "Beacon"
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
$evidence = Join-Path $env:GITHUB_WORKSPACE "evidence"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null

function Stop-Beacon
{
    Get-Process Beacon.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

function Write-ScenarioConfig([bool] $numericFloating)
{
    Stop-Beacon
    # numericFloatingResetDone 必须 true：否则启动迁移把开关强制回关，场景 A 直接失真
    $config = @'
{
  "configVersion": 1,
  "hotkey": "Ctrl+Alt+B",
  "launchOnStartup": false,
  "theme": "dark",
  "pinDisplayMode": "floating",
  "numericFloatingEnabled": __NUM__,
  "numericFloatingResetDone": true,
  "showCapsule": false
}
'@.Replace("__NUM__", $numericFloating.ToString().ToLowerInvariant())
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

function Save-ZoomedCrop([string] $name, [WinEnum+RECT] $rect, [int] $margin, [int] $zoom)
{
    $w = $rect.Right - $rect.Left + 2 * $margin
    $h = $rect.Bottom - $rect.Top + 2 * $margin
    $crop = New-Object System.Drawing.Bitmap $w, $h
    $g1 = [System.Drawing.Graphics]::FromImage($crop)
    $g1.CopyFromScreen($rect.Left - $margin, $rect.Top - $margin, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $g1.Dispose()
    $big = New-Object System.Drawing.Bitmap ($w * $zoom), ($h * $zoom)
    $g2 = [System.Drawing.Graphics]::FromImage($big)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g2.DrawImage($crop, 0, 0, $w * $zoom, $h * $zoom)
    $g2.Dispose()
    $big.Save((Join-Path $evidence $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $big.Dispose()
    $crop.Dispose()
    Write-Host "近景截图已存：evidence/$name"
}

# —— 场景 A：数量悬浮窗开 ——悬浮框必须在桌面，icon 近景留证
Write-ScenarioConfig $true
Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
Start-Sleep -Seconds 18
$tiles = [WinEnum]::VisibleHandlesByTitle("Beacon Tile")
if ($tiles.Count -lt 1)
{
    Save-FullScreenshot "A-FAIL-no-tile-window.png"
    Stop-Beacon
    throw "断言失败（场景 A）：数量悬浮窗开启时未见悬浮框窗口（标题 Beacon Tile）——悬浮窗未创建"
}
Save-FullScreenshot "A-numeric-on-desktop.png"
Save-ZoomedCrop "A-numeric-on-tile-closeup.png" ([WinEnum]::RectOf($tiles[0])) 10 6
Write-Host "场景 A 通过：悬浮框窗口存在（$($tiles.Count) 个），全屏与近景截图已存"
Stop-Beacon

# —— 场景 B：数量悬浮窗关 ——桌面零残留窗（硬断言）
Write-ScenarioConfig $false
Start-Process -FilePath (Join-Path $env:GITHUB_WORKSPACE "publish/Beacon.App.exe") | Out-Null
Start-Sleep -Seconds 18
$leftover = @([WinEnum]::VisibleHandlesByTitle("Beacon Tile") + [WinEnum]::VisibleHandlesByTitle("Beacon Pinned"))
if ($leftover.Count -gt 0)
{
    Save-FullScreenshot "B-FAIL-residue-windows.png"
    Stop-Beacon
    throw "断言失败（场景 B）：数量悬浮窗关闭后桌面残留 $($leftover.Count) 个 Beacon 窗口"
}
Save-FullScreenshot "B-numeric-off-desktop.png"
Write-Host "场景 B 通过：桌面零残留窗（无可见 Beacon Tile / Beacon Pinned）"
Stop-Beacon

Write-Host "视觉取证完成，产物："
Get-ChildItem $evidence | ForEach-Object { Write-Host "  - evidence/$($_.Name)" }
