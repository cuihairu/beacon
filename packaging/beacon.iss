; Beacon Windows 安装包（B-804，daily-build 产出）——Inno Setup 6.5+ 编译。
; 与「用户态权限」口径一致：装到 {localappdata}、注册表只碰 HKCU，无需管理员。
; 用法：ISCC.exe /DMyAppVersion=2026.10.9 /DMyCommit=<sha> packaging\beacon.iss
; （版本四段日期 yyyy.M.d：每段 ≤65535——0.0.yyyyMMdd 八位数段会超 Inno/AssemblyVersion 上限）
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

; -- 安装器配置保护（不许静默覆盖用户配置）--
; ①检测到旧配置目录 → 单选页：保留现有配置(默认) / 覆盖为全新配置；
; ②「覆盖」须勾选「我已知悉将丢失现有配置」才放行；③/SILENT 默认保留，覆盖须显式 /OVERWRITE_CONFIG；
; ④无旧配置直接跳页。配置目录 %APPDATA%\Beacon（应用运行时生成，安装包不带配置文件）。
; 布局注：警告文案与知悉勾选**固定常显**（压缩 CheckListBox 高度腾出底部两行）——
; 不做动态显隐：其一，TInputOptionPage 的列表本就占满 Surface，控件叠在 CheckListBox.Bottom
; 之后整个落在可视区外（2026-10-08 用户装机实证：MsgBox 拦截在响、勾选框却看不见）；
; 其二，动态显隐依赖 OnClickCheck 在 radio 切换时触发，时序未经实证，能不依赖就不依赖。
[Code]
const
  OverwriteParam = '/OVERWRITE_CONFIG';

var
  ConfigPage: TInputOptionWizardPage;
  AckCheckBox: TNewCheckBox;
  WarnLabel: TNewStaticText;
  OverrideConfig: Boolean; // 是否覆盖（NextButtonClick 记录 → CurStepChanged 执行；静默时由参数定）

function CmdLineParamExists(const Value: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

function HasExistingConfig: Boolean;
begin
  Result := DirExists(ExpandConstant('{userappdata}\Beacon'));
end;

procedure InitializeWizard;
begin
  OverrideConfig := False;
  ConfigPage := CreateInputOptionPage(wpSelectTasks,
    '现有 Beacon 配置', '检测到已存在的配置目录', '如何处理现有配置？（%APPDATA%\Beacon）', True, False);
  ConfigPage.Add('保留现有配置（推荐，默认）');
  ConfigPage.Add('覆盖为全新配置（删除现有连接/组件/设置）');
  ConfigPage.Values[0] := True; // 默认保留

  // 底部固定两行：先压缩列表（两个 radio 项绰绰有余），再按列表新底缘顺排
  ConfigPage.CheckListBox.Height := ConfigPage.CheckListBox.Height - ScaleY(60);
  WarnLabel := TNewStaticText.Create(WizardForm);
  WarnLabel.Parent := ConfigPage.Surface;
  WarnLabel.Left := 0;
  WarnLabel.Top := ConfigPage.CheckListBox.Top + ConfigPage.CheckListBox.Height + ScaleY(6);
  WarnLabel.Width := ConfigPage.Surface.Width;
  WarnLabel.Height := ScaleY(32); // WordWrap 需固定 Width+Height（不设 AutoSize）
  WarnLabel.WordWrap := True;
  WarnLabel.Caption :=
    '提示：选「覆盖」将删除 %APPDATA%\Beacon 下全部现有配置（连接/组件/设置），不可恢复；' +
    '选「保留」时若新版本调整了配置结构，旧配置可能无法加载或部分功能异常。';

  AckCheckBox := TNewCheckBox.Create(WizardForm);
  AckCheckBox.Parent := ConfigPage.Surface;
  AckCheckBox.Left := 0;
  AckCheckBox.Top := WarnLabel.Top + ScaleY(36);
  AckCheckBox.Width := ConfigPage.Surface.Width;
  AckCheckBox.Caption := '我已知悉，覆盖将丢失现有配置（选择覆盖时必须勾选）';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = ConfigPage.ID then
    Result := (not HasExistingConfig) or WizardSilent; // 无旧配置/静默安装不弹页
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ConfigPage.ID then
  begin
    if ConfigPage.Values[1] then
    begin
      if not AckCheckBox.Checked then
      begin
        MsgBox('请先勾选「我已知悉，覆盖将丢失现有配置」。', mbError, MB_OK);
        Result := False;
        Exit;
      end;
      OverrideConfig := True;
    end
    else
      OverrideConfig := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // 静默安装不弹页：默认保留，覆盖必须显式带 /OVERWRITE_CONFIG（参数即确权）
  if WizardSilent then
    OverrideConfig := CmdLineParamExists(OverwriteParam);
  if (CurStep = ssInstall) and OverrideConfig then
    DelTree(ExpandConstant('{userappdata}\Beacon'), True, True, True);
end;
