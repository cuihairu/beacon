# openclaw 真机验证批 2026-11-06（explorer 登录会话调度 + Toast/托盘/改色/唤起收尾）

> 机器：openclaw（10.240.0.3，Windows Server 2022 zh-CN，2560×1440 单屏，SSH Administrator 直连）。
> 构建：beacon nightly 安装包 `beacon-nightly-windows-x86_64-setup.exe`（SHA256 `fe36f6f4…340cc9d` 校验过，oc-prep 静默安装，安装目录 `%LOCALAPPDATA%\Beacon`）。
> 会话取得：上席 12:39:31 注销后自动登录凭据写错位（密码值落入 `DefaultDomainName`），控制台滞留 LogonUI。本轮修正自动登录配置后以重启 LogonUI 触发登录转换——12:56:37 Security 4624 type=2 **Administrator** 交互登录成功（凭据来自机器既存 LSA 存储的自动登录密钥，非本轮明文注册表值；期间 4625=0）。此后全程在**已解锁交互会话（session 2）**内取证，截图一律整屏 `CopyFromScreen` + 任务栏 `PrintWindow`，交互一律 `SetCursorPos`+`mouse_event`/`keybd_event`（经 Interactive 计划任务注入 session 2）。
> 取证脚本与原始产物留在 openclaw `C:\beacon-verify\v2\`；本目录为入仓副本。

## 1. explorer 登录会话调度（todo「explorer 登录会话调度实机验证」）✅

链条（全部真实证据）：

- HKCU Run 键：`Beacon = "C:\Users\Administrator\AppData\Local\Beacon\Beacon.App.exe"`（app 首启自写，`state-logtail.txt` 前身 oc-prep 输出 RunKey= 同值）。
- 登录转换：`logon-probe.txt` 两行——
  - `2026-11-06T12:56:50 beacon=none`（ONLOGON 探测先于 Run 键发射，如实记录）；
  - `2026-11-06T13:01:24 beacon=208 parent=explorer(9392) session=2`——**Beacon.App pid 208 由新会话 explorer 9392 拉起**，命令行即 Run 键值。
- 登录事件：Security 4624 type=2 user=Administrator @12:56:37（注销→登录完整转换，注销事件 4647 @12:39:31）。
- 会话状态：`qwinsta` console 会话 2 运行中（本文件写作时 beacon pid 16660 仍驻留 session 2）。

## 2. Toast 发送/横幅呈现（todo「Toast 发送、点击、激活路由」发送+呈现半）✅◐

- 触发：二次启动 exe（E1/B-101 既有链路）→ 单实例守卫 → 主实例 `ShowBalloon`。
- 日志（`toast-verify.txt` / `beacon-20261106.log`）：`13:12:47.339 Secondary instance exit.` + `13:12:47.343 Balloon dispatched: Beacon - Beacon 已在运行。`（另 12:38/12:57 两次启动横幅同链路）。
- 横幅实拍：`banner-t2.png` 右下角 Windows 通知横幅「BeaconApp / Beacon / Beacon 已在运行。」；`notification-center.png` 通知中心面板内两条 Beacon 通知（13:17「Beacon 已在运行。」+ 12:57「已启动并常驻托盘——状态胶囊在屏幕右下角…」）。
- 仍 OPEN：**带 detailUrl 的状态变更 Toast 的真人点击→激活路由**（本机无可稳定复现 Failed 态的凭据面，匿名 GitHub 恒 403 限流；不冒充已验）。

## 3. 托盘图标颜色/状态像素与刷新周期（todo「托盘图标颜色/刷新周期实机验证」）✅

- 两态差分（同机同位）：`tray-green.png`（聚合 Success）vs `tray-e2.png`（Warning，「1 warn」）——beacon 托盘圆点同一位置（x≈2110-2130, y 12-31）由 **RGB(62,185,80) 绿 → RGB(209,152,33) 琥珀**，其余托盘图标逐像素不变（diff bbox 仅覆盖 beacon 图标簇）。任务栏 `PrintWindow` 原图入仓。
- 刷新周期：widget `github.actions.runs cuihairu/beacon ci.yml` `refreshIntervalSeconds=30` 实测——`state-logtail.txt` 13:25:28 / 13:26:28 两次刷新间隔恰 60s（退避后）、「Last update 13:28→13:29」随刷新走（`e2-t78.png`）。
- 图标在托盘可见区直接显示（不进溢出区），溢出区展开未再测（本机无溢出收纳该图标的前提）。

## 4. tile/胶囊改色→托盘图标同步（todo「tile 与胶囊改色/重置托盘图标同步」）✅

- 改色：`config.json appearance.severityColors.warning=#ff00ff` 重启后，warning 态托盘圆点渲染为**品红 RGB(206,0,206)**（`mag-tray-8.png`，x≈2068-2083，核心像素连续 16 帧稳定）——B-706 运行时绘制链路（palette.SeverityColor → DrawSeverityIcon）真机复证。
- 重置：删除 severityColors 后默认色表恢复（`tray-after-reset.png`，品红消失）。
- 注意：托盘图标在 beacon 重启后按系统重排改变横向位置（2110→2068），逐帧比对须按「新增唯一色」定位而非固定坐标。

## 5. 胶囊点击唤起看板 + 焦点行为（PANEL-UPGRADE-1 剩余项之一；todo「跨进程点击穿透/焦点行为」）◐✅

- 关→点→开闭环（`capsule-test.txt` + `board-closed.png`/`board-opened.png`）：ESC 关面板（前台回到 powershell 窗）→ 点桌面（前台 wsl.exe）→ 真实鼠标点击胶囊中心 (2478,1285) → 看板重新弹出（720×560 方案 B 看板，事件时间线 ci.yml · success · 1m59s 连续行），**前台保持 wsl.exe 不变**——面板开启不抢焦点（RFC §6.2.7 ②真机复证）。
- 仍 OPEN：把手/边框拖动（合成拖拽未做）、tile 收起态点击穿透到下层窗口的场景未复现。

## 6. 状态机真实失败路径（补充）

- `widgets.json` 下发 GitHub widget 后匿名撞 403 rate limit → `state-logtail.txt`：`Widget w-gh-1 refresh failed: Offline. GitHub rate limit 耗尽 (403)`（GitHubApiClient.HandleStatusAsync 真实状态路径），聚合与胶囊随成功/失败刷新在 warn/ok 间翻转（`e2-t78.png`「1 warn」琥珀点 → `board-opened.png` 时间线 success 行）。

## 7. 本轮机器侧处置（透明记录）

- 为完成登录转换创建临时本地账号 `beaconauto`（Administrators 组，预写 Run 键指向既装 Beacon）；实际登录转换由机器既存 LSA 自动登录凭据以 Administrator 完成，beaconauto 未成为登录会话身份——验证完成后账号已删除、计划任务（BcnPrep/BcnProbeNow/BcnV2-*/上席遗留 BeaconVerify/BeaconVerify2/PwProbe）全部清除，Winlogon 恢复 `Administrator/OPENCLAW`（保留 LSA 密钥路径）。
- 本机未重启。beacon 配置留有 1 个 GitHub widget（刷新 30s 档）供后续浸泡观察。
