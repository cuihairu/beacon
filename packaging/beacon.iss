; Beacon Windows 安装包（B-804，daily-build 产出）——Inno Setup 6.5+ 编译。
; 与「用户态权限」口径一致：装到 {localappdata}、注册表只碰 HKCU，无需管理员。
; 用法：ISCC.exe /DMyAppVersion=0.0.20261007 /DMyCommit=<sha> packaging\beacon.iss
; 向导全部简体中文（语言包 vendor 在本目录，不依赖编译器内置版本）；
; 向导图与图标出自 docs/img/logo.svg（wizard-small/large @1x+@2x，Inno 6.3+ 按 DPI 自取）。
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef MyCommit
  #define MyCommit "unknown"
#endif
#define MyCommitShort Copy(MyCommit, 1, 8)

[Setup]
AppId={{6D1D74C7-1191-4EE2-BC04-CB48B50B2005}
AppName=Beacon
AppVersion={#MyAppVersion}
AppVerName=Beacon {#MyAppVersion} (nightly {#MyCommitShort})
AppPublisher=Beacon
DefaultDirName={localappdata}\Beacon
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..
OutputBaseFilename=beacon-nightly-windows-x86_64-setup
SetupIconFile=..\src\Beacon.App\Assets\beacon.ico
WizardSmallImageFile=wizard-small.png
WizardImageFile=wizard-large.png
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Beacon.App.exe
; 托盘常驻应用在跑时提示先退出（单实例互斥体，见 SingleInstanceGuard）
AppMutex=Beacon.SingleInstance

[Languages]
Name: "chs"; MessagesFile: "ChineseSimplified.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\Beacon"; Filename: "{app}\Beacon.App.exe"
Name: "{userdesktop}\Beacon"; Filename: "{app}\Beacon.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
; 默认勾选与 config.LaunchOnStartup=true 对齐（应用首跑也会按配置自管 HKCU Run）
Name: "autostart"; Description: "开机自动启动"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Beacon"; ValueData: """{app}\Beacon.App.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\Beacon.App.exe"; Description: "启动 Beacon"; Flags: nowait postinstall skipifsilent
