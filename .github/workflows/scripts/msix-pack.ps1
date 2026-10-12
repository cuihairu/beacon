# MSIX 打包（商店前置）：把 unpackaged 自包含的 publish 输出整体包成 MSIX。
# 应用本体仍是 unpackaged（RFC §10，WindowsPackageType=None）——包内 Identity 只给商店标识用，
# 运行形态不变。本脚本失败一律 throw，是否降级 warning 由调用方决定。
#
# 两个调用方共用本脚本，避免逻辑漂移：
#   .github/workflows/daily-build.yml   版本 tag 通道：产物进 release + SHA256SUMS（失败降级 warning）
#   .github/workflows/msix-dry-run.yml  试跑台：只上传 run artifact，不碰 release/tag
#
# 用法：& .github/workflows/scripts/msix-pack.ps1 -Version 0.1.3.0 -Output beacon-0.1.3-windows-x86_64.msix
# 版本必须四段且与 v* tag 对齐（v0.1.3 -> 0.1.3.0）；清单里的 Version="0.0.0.0" 是占位，此处替换。
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Output,
    [string]$ManifestDir = 'packaging\msix',
    [string]$Content = 'publish',
    [string]$Staging = 'msix-staging'
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "MSIX 版本必须四段（如 0.1.3.0）：$Version"
}

$manifestFile = Join-Path $ManifestDir 'appxmanifest.xml'
$manifest = Get-Content $manifestFile -Raw
if ($manifest -notmatch 'Version="0\.0\.0\.0"') {
    throw "清单里未找到 Version=`"0.0.0.0`" 占位：$manifestFile"
}
$manifest = $manifest.Replace('Version="0.0.0.0"', "Version=`"$Version`"")

# MakeAppx 只认 /d 根下的 AppxManifest.xml：暂存 publish 全部内容 + 清单与图标，
# 再用替换版本后的清单覆盖（仓库里的清单保持 0.0.0.0 占位，不随构建改）。
Remove-Item -Recurse -Force $Staging -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $Staging | Out-Null
Copy-Item -Path "$Content\*" -Destination $Staging -Recurse -Force
Copy-Item -Path "$ManifestDir\*" -Destination $Staging -Recurse -Force
Set-Content -Path (Join-Path $Staging 'AppxManifest.xml') -Value $manifest -Encoding UTF8

# 取最高版 Windows SDK 的 MakeAppx.exe（路径按版本目录排序取最后一个）
$makeappx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter MakeAppx.exe -Recurse -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) {
    throw 'MakeAppx.exe 未找到（Windows Kits 未安装）'
}
Write-Host "MakeAppx: $($makeappx.FullName)"

try {
    & $makeappx.FullName pack /d $Staging /p $Output
    $exit = $LASTEXITCODE
    if ($exit -ne 0) {
        throw "MakeAppx pack 退出码 $exit"
    }
}
finally {
    Remove-Item -Recurse -Force $Staging -ErrorAction SilentlyContinue
}

Write-Host "MSIX 打包完成：$Output（版本 $Version，未签名——商店提交由 Microsoft 代签）"
