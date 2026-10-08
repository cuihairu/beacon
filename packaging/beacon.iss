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

; -- Config protection: preserve existing config on install unless user explicitly overrides
; 警告文案：保留可能存在配置不兼容
[Code]
const
  sOverwriteWarn = '保留可能存在配置不兼容——新版本若调整配置结构，旧配置可能无法加载或部分功能异常，出问题可重装选覆盖';
  sOverwriteConfirm = '我已知悉将丢失现有配置';
var
  selRestore: Boolean;
  selOverride: Boolean;
procedure CurStepChanged(step: Integer);
var
  cfgDir: string;
begin
  if step = ssInstall then
  begin
    cfgDir := '{userappdata}\Beacon';
    if not DirectoryExists(cfgDir) then
    begin
      ; 无旧配置，跳过询问，直接使用默认
      exit;
    end;
    ; 弹出自定义页确认：保留(默认) / 覆盖
    if not CreateCustomPage(wpSelectTasks, @SelectConfigPage) then
      Fail('创建配置保留页失败');
    ; 若是静默安装，默认保留，自动带参数覆盖
    if IsSilent then
    begin
      if ParamIsCmdLine('/OVERWRITE_CONFIG') then
        SelectOverride
      else
        SelectRestore;
    end
    else
    begin
      ; 交互模式：询问用户
      SelectRestore;
    end;
  end;
end;

function SelectConfigPage(wp: Integer): Boolean;
var
  r: Integer;
begin
  Result := False;
  { 单选按钮：保留默认 / 覆盖 }
  r := MsgBox('Beacon 将要安装到 {localappdata}\Beacon'#13#10'已检测到旧配置目录'#13#10'是否保留现有配置？', mbConfirmation, MB_YESNOCANCEL or MB_DEFBUTTON2);
  case r of
    IDYES: begin selRestore := True; selOverride := False; end;
    IDNO:  begin selOverride := True; end;
    IDCANCEL: begin abort; end;
  end;
  Result := True;
end;

procedure DoSelectRestore;
begin
  selRestore := True;
  selOverride := False;
  ; 保留旧配置 - 通过 DelTree 后重新写入默认模板实现
  DelTree('{userappdata}\Beacon');
end;

procedure DoSelectOverride;
begin
  selOverride := True;
  ; 显式确认：二次确认框
  if not AskYesNo('Beacon 安装'#13#10'确定要覆盖现有配置吗？'#13#10''#13#10+sOverwriteWarn + #13#10+'「' + sOverwriteConfirm + '」才能继续。', mbConfirmation, MB_YESNOCANCEL or MB_DEFBUTTON2) then
  begin
    ; 用户取消，回到保留分支
    selRestore := True;
    selOverride := False;
    exit;
  end;
  ; 执行清理：删除旧配置目录
  DelTree('{userappdata}\Beacon');
end;
