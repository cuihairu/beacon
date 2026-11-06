# 真机验收取证 · 2026-11-06（19 未勾项批次）

> 本轮用 `tools/realmachine/` 的 SSH + `schtasks /IT` 流程在 **10.240.0.5（Windows 11 26H2 专业版，1920×1080 @100%，NVIDIA GT 1030 + Oray 虚拟显示驱动）** 上跑真机验收。
> 被测构建：`origin/main` `56e4f277835116584b26c0766806712ed740ea53` 现场 `dotnet publish`（`InformationalVersion=2026.11.6+56e4f27…`，401 个文件）。
> 本地门禁：`dotnet test` 四个测试工程 **1697 例全绿**（Actions 27 / Connections 673 / Core 956 / Storage 41）；`Beacon.App` 在 Linux 上因 WinUI XAML 编译器不可用（NETSDK1100→MSB3073）只能由 CI windows-latest 覆盖。

## 取证口径（为什么不是整屏截图）

该机控制台会话**处于锁屏**：`GetForegroundWindow()` 返回 `Windows 默认锁屏界面`，`LogonUI` 常驻。
由此产生两个硬约束，本轮全部证据都绕开它们：

| 约束 | 实测证据 | 绕开手段 |
|---|---|---|
| `Graphics.CopyFromScreen` 只拿到锁屏壁纸帧（无窗口、无任务栏） | 在 (700,300)-(1000,500) 建了顶层红色 `BEACON-MARKER` 窗（`VisibleTopLevelHandles` 可见、rect 正确），截图该区域红色像素比 **0.000**（`markerRedRatio=0`） | 一律用 `PrintWindow(PW_RENDERFULLCONTENT)` 截**窗口自身内容**（看板/tile/胶囊/详情/托盘窗） |
| 合成鼠标键盘（SendInput）落在锁屏安全桌面，到不了应用窗口 | 头部拖动 dx=0、边框拖宽 736→736px；同一注入方式的 `SetCursorPos` 却能移动光标（说明是「投递不到目标窗口」而非注入整体失效） | 交互改走 **UIA**（`Invoke` / `Transform.Resize`）与 **`PostMessage`**（WM_HOTKEY、托盘消息窗） |

因此：**需要真人解锁会话才能收口的项**（真实鼠标拖拽、Toast 横幅点击、托盘溢出区展开、整屏录屏、跨 DPI 双屏）在下表标 `OPEN(锁屏)`，不冒充已验。

## 逐项结果

| todo 行 | 项 | 结论 | 证据 |
|---|---|---|---|
| :33 | URL 拼接端到端打开 | **PASS** | 看板时间线行 → L3 详情 → 点「Open」→ 默认 Edge 打开 `open.bigmodel.cn`（窗口标题「智谱丨BigModel 平台」，标签页 13→14）；`B1-detail.png`、`B1-browser.png`、`behaviour-verify.txt` |
| :39 | Toast 发送 / 点击 / 激活路由 | **PARTIAL** | 发送半：错误态触发后 `notifications.json` 落 1 条记录（含 `detailUrl=https://open.bigmodel.cn/usage`）、AUMID 注册在案、日志 0 条「Toast 发送失败」。横幅呈现与点击路由 **OPEN(锁屏)**：锁屏下不产生可枚举的新窗（`T3-toast-window FAIL`，新增顶层窗=无） |
| :43 | 托盘图标颜色 / 刷新周期 | **PARTIAL** | `TrayNotifyWnd` 健康态 vs error 态 PrintWindow 两态差分：**189 px 变化**（簇 x552-567、x890-902），15s 轮询 + 40s 等待窗口内发生；但该簇归属未能在锁屏下确证为 Beacon 图标（另一簇是系统时钟），托盘溢出区需解锁后展开 → `T2-tray-diff`、`T2-tray-xaml-diff`、`tray-toast-verify.txt` |
| :49 | 跨进程点击穿透 / 焦点 | **PARTIAL** | 结构面：`WS_EX_APPWINDOW` 未置位（看板/胶囊不进任务栏与 Alt-Tab）、状态翻转前后前台不变（`Windows 默认锁屏界面` → 同）；真实「点到下层窗口」实测 **OPEN(锁屏)**（合成点击到不了桌面窗口）→ `behaviour-verify.txt` |
| :62 | tile/胶囊改色 → 托盘同步 | **PARTIAL** | 改色半：`severity-color-warning`、`widget-color-override` 两组 off/on 实拍图见 [`docs/img/config-effects/`](../img/config-effects/)（`severity-color-warning-off/on.png`）；托盘同步半同 :33 行口径 → `docs/img/config-effects/` |
| :90 | explorer 登录会话调度 | **PASS(会话内) / OPEN(真实注销登录)** | `launchOnStartup=true` → `HKCU\Run\Beacon = "C:\beacon-verify\publish\Beacon.App.exe"`；`explorer.exe <path>` 拉起进程 pid=14236、父进程 explorer、会话=1；关掉开关后 Run 值清空。真实注销/重登 **OPEN**（2026-10-30 决策 13：不在未安排会话时擅自注销用户）→ `behaviour-verify.txt` |
| :92 | 长时稳定观察 | **PARTIAL(进行中)** | `soak-log.csv` 每 60s 采样进程数/工作集/句柄/CPU/日志错误数；起点 08:27，采集仍在跑（8h 上限）。4 小时口径未满 → `soak-log.csv` |
| :112 | 展示模板尺寸裁切复核 | **OPEN(未执行)** | 本轮未跑到组件中心模板矩阵（时间让位给看板/DOC-GIF/推送）；组件中心 UIA 驱动链已在 CI 场景 AE 验证可复用 |
| :184 | 重启恢复 + DPI 拖动 | **PARTIAL** | 重启后看板尺寸/位置还原：720x560 @(1160,316) 重启前后一致（`PU1-persist`、`PU1b-persist`）；DPI 缩放切换与拖动 **OPEN(锁屏+单屏)** → `panel-upgrade-verify.txt` |
| :73 | B-803 干净 Win11 装包验收 | **OPEN(未执行)** | 未做装包走查；本机非「干净快照」（装有 .NET SDK/scoop），干净 VM 半仍需 B-804 环境 |
| :56 | 跨 DPI 拖放 / 降分辨率越界回收 | **OPEN(单屏+锁屏)** | 需双屏或改分辨率后仍可操作的会话 |
| :60 | RFC §6.2.7 四项铁律 | **PARTIAL** | ① 点击下钻：时间线行 UIA `Invoke` → 新开 L3（`CAP-uia-row-invoke PASS`）；② 状态告警不抢焦点：翻转前后前台不变；③ L0/tile 可点项面：本轮未取到按钮清单（详情窗当时已关）；④ 不进任务栏/Alt-Tab：`WS_EX_APPWINDOW` 未置位 → `behaviour-verify.txt`、`capability-probe.txt` |
| :67 | 视觉观感真人验收 | **OPEN(待用户)** | 已给真机实拍：`PU1-board-default.png`（看板）、`PU1-capsule.png`（胶囊）、`docs/img/config-effects/*`（各开关）；主观裁量留给用户 |
| :71 | 真人全流程录屏 | **OPEN(锁屏)** | 整屏录制依赖可显示的桌面；锁屏下 `CopyFromScreen` 只有壁纸帧，无法形成有效录屏 |

### 面板升级看板（PANEL-UPGRADE-1）核验

| 验收点 | 结论 | 证据 |
|---|---|---|
| 默认 ~720x560 可调可记忆 | **PASS** | 客户区实测 720x560（外框 736x576）；UIA `Transform.Resize` 736x576 → 856x666 生效；重启后尺寸/位置还原 720x560 @(1160,316) |
| 时间线成主区、时刻/级别/来源/摘要逐行可读 | **PASS** | UIA 行文本逐行含 `HH:mm:ss` 时刻 + 级别点 + 来源（智谱/DeepSeek）+ 摘要；真机实拍 `PU1-board-default.png`（7 行在屏，25 条事件在列） |
| 条数放宽 | **PASS** | 屏上 25 条事件，代码口径 `TimelineEntries.BoardMaxEntries=50`（`MaxRecentEvents=50` 同口径） |
| 头部 OverallSummary 与来源过滤保留 | **PASS** | UIA：`2 警告 · 0 错误`；概览卡 `全部 2 / DeepSeek 1 / 智谱 1` |
| 字体行距按可读性重排 | **PASS(代码) / 待用户观感** | `BoardRowFontSize=13`、`BoardRowCaptionSize=12`（72a99cb）；实拍图已给 |
| 尺寸记忆走 shell-state | **PASS** | `ShellStateStore.LoadQuickPanelLayout` + `PersistLayout()`（06134d1 接线），重启还原实测 |
| 头部把手拖动 / 边框拖拽 | **OPEN(锁屏)** | 合成拖拽到不了窗口（dx=0、736→736）；需解锁会话复测 |
| 悬浮岛/胶囊点击唤起目标同步指向看板 | **OPEN(存疑)** | 首轮 `PU1-capsule-invokes-board PASS` 在锁屏下可能是「看板本就开着」的假阳性（ESC 关窗同样收不到输入），不作为通过依据 |
| Core 侧筛选/聚合单测 | **PASS** | `TimelineEntriesTests`（含 `Assert.Same(evt.State, row.State)`，72a99cb）；本地 956 Core 例绿 |
| CI windows-latest 出看板截图 | **PASS** | nightly run **41981480531**（head `06134d1`）visual-evidence 工件 `K-panel-open-offline.png`：720x560 看板、EVENT TIMELINE 主区、右副栏三段齐全 |
| 真机人工核验可读性 | **PASS(主区) / 发现一处** | 时间线主区逐行可读；**右副栏（通知/需要关注）行文本右缘被截断**，建议后续给副栏行加换行或 Tooltip（本轮记录不改码） |

## 本轮新增文件

- `tools/realmachine/`：`run-remote.sh`（schtasks /IT 远程跑 + 取证回传）、`extract-lib.sh`（从 CI 取证脚本抽共享库）、`checks/panel-upgrade.ps1`、`checks/behaviour.ps1`、`README.md`
- `tools/doc-gif/`：`capture-config-effects.ps1`（开关 off/on 抓帧）、`assemble.py`（合成 GIF）
- `docs/configuration-effects.md` + `docs/zh/configuration-effects.md`：配置效果双语页
- `docs/img/config-effects/`：9 组 off/on 静帧 + 8 张 GIF
- 本目录：日志与截图（`panel-upgrade-verify.txt`、`behaviour-verify.txt`、`tray-toast-verify.txt`、`capability-probe.txt`、`soak-log.csv`、`PU1-*.png`、`B1-*.png`、`T2-*.png`）
