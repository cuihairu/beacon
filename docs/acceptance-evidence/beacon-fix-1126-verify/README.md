# BEACON-FIX-1126 验收取证状态（2026-11-26）

## CI / Core（已绿）

- Core 测试：ci run 45752403468 全绿。
- nightly run 45754679256（head dfeb789）三道新门全绿：
  - AH5d-B region 直读：四角全出 region、顶缘 Top=3 贴顶；
  - SG 病灶 C：图标-文字间距 10px（≥9）；
  - SG 窄窗：600px 窗搜索/过滤/清词复原全链；
  - SH 病灶 D：刷新档下拉宽 220px（≥218）。
- 资产：beacon-nightly-windows-x86_64.zip sha256 ee4efd3b8ba4e4d89efbd0e171ca11d77d3bf6234175f851f3b1bb88b8fda7a9（70,435,439 bytes，2026-11-26T06:02:04Z 发布）。

## imac 真机取证（阻断：装机环境问题，不是本次代码回归）

新 nightly 与改动前代码（d67ef48 源码 tar）在 imac 上**同样启动即崩**
`0xC000027B`，且所有 WindowsAppSDK 版本（1.6/1.8/2.7）、所有启动会话
（ssh session 0 / 交互 console session 1）均崩。

- `cdb-analyze-20261126.txt`：WinDbg `!analyze -v` 实证——
  stowed exception `0x80070057`（E_INVALIDARG），
  `Microsoft.UI.Xaml!RefreshRateInfo::RegisterForPowerNotification+b1`
  （`RefreshRateInfo.cpp:174`），崩在 `Application.Start` 原生初始化，
  beacon 托管代码一行未执行。
- `crash-history-20261126.txt`：崩溃从 2026-11-25 04:57 起密集出现，
  全部同一 fault offset `0x9125`（FailFastWithStowedExceptions）。
  今早一次「d67 ALIVE」是假阳性：09:05:10 恰有 WER 崩溃记录，
  12 秒判定窗口在崩溃落地前截断。
- `display-config-20261126.txt`：主显示是 `OrayIddDriver Device`
  （向日葵虚拟显示器，WinDisc 1024x768，BitsPerPixel=0）；
  物理卡为 RX 6900 XT 5120x2880@60。禁用该虚拟显示器（当日已恢复）
  不能解崩，虚拟屏仍在。

判定：WinUI XAML 在 imac 当前系统分支（Windows 26H2 build 26300.9974）
上的电源通知注册恒 E_INVALIDARG，属装机环境缺陷；仓库侧改动
（IslandSurfaceOutline / DesignTokens / SettingsWindow / CapsuleWindow）
与此崩溃无因果关系。imac 真机截屏与用户装机亲验因此均无法完成，
BEACON-FIX-1126 保持未勾，等待装机环境修复后补验。
