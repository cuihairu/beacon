# Toast detailUrl 点击→激活路由复现（2026-11-08 imac 探针）— BLOCKED

> 复现口径：Failed 态不再依赖 GitHub 匿名 403 凭据面——本地 mock（127.0.0.1:18099）构造
> `{"status":"failed","message":"mock failed build","url":"badproto://invalid-detail-url"}`，
> `http.status` widget 配 `url_path=$.url`，让应用**真实发出一条约带无效协议 detailUrl 的 Error toast**，
> 再在 OS 横幅上点击，断言激活路由（无效 URL→`Process.Start` 抛→日志「打开 Toast 深链失败」+看板回退；
> 有效 https URL→浏览器落点）。
>
> 本轮结论：**状态触发与投递 PASS；横幅显示被 imac 系统侧压制；点击步 BLOCKED**（§5）。与既有
> 2026-11-08-bm2-toast-tray.md 的关系：该台账已证「详情窗 Open→浏览器」半段 + 呈现面；本文补
> 「OS 横幅本体点击→激活」整段，断点定位到机器环境。

## 1. 复现链（代码逐段核验）

| 环节 | 位置 | 核验 |
|---|---|---|
| payload `url` → DetailUrl | `HttpStatusProvider`（`url_path` 读 JSON，默认无 = 空） | ✅ 配置 `url_path=$.url` 即取到 |
| Error 态规则 | `NotificationEngine`：首次 Error 即 Deliver（默认规则 `SeverityAtLeast=Error, Toast=true`；阈值跨越 `_activeCrossings` 首次 `Add` 返回 true） | ✅ 首拉 Error → 必发 |
| toast 携带 detailUrl | `ToastNotificationService.Show`：`AddArgument(ArgDetailUrl, notification.DetailUrl)`（仅 DetailUrl 非空时） | ✅ |
| 点击激活路由 | `OnActivated`→`ToastActivation(WidgetId, DetailUrl)`→`OpenToastTarget`：`Process.Start(new ProcessStartInfo(url){ UseShellExecute = true })`，catch→`LogError("打开 Toast 深链失败：{Url}")`→`ToggleBoard()` 回退 | ✅ 无效协议必走 catch 分支 |

## 2. 探针设计（两轮，重启隔轮保证首拉即 Error=raise）

- 配置：`connections.json` conn-toast → `http://127.0.0.1:18099/status.json`；`widgets.json` toast-w
  `http.status` { label Build, status_path `$.status`, summary_path `$.message`, url_path `$.url` }。
- A 轮 url=`badproto://invalid-detail-url`（无效协议→期望 catch 分支）；B 轮 url=`https://example.com/beacon-detail`
  （有效→期望浏览器落点、看板不弹）。
- 检测：每 700ms 枚举顶层窗口（含类 `*CoreWindow*` 与标题 `*Beacon*`，排除 Beacon 自身 pid），右下角尺寸带
  150-1400×50-600 即横幅；命中后整屏截图+中心 `SendInput` 点击；B 轮另查 `Get-CimInstance Win32_Process`
  CommandLine 含 `beacon-detail` 的进程。

## 3. 结果

| 阶段 | 断言 | 结果 | 证据 |
|---|---|---|---|
| 状态触发 | `Widget toast-w refreshed ok: Build → mock failed build` | ✅ | 应用日志 beacon-20261108.log 06:57:33.188 起连续多轮（06:57:46/06:58:03/06:58:18/06:58:38/06:58:54/06:59:09/06:59:24） |
| 引擎交付 | 默认规则 Error→toast；无「Toast 发送失败」/「AUMID 注册缺失」日志 | ✅ | 全日志无该两串（Show 成功路径静默，失败才落错） |
| OS 接受投递 | `HKCU\...\Notifications\Settings\{Beacon AUMID}` 键由无到有 | ✅ | 探针前检查 `Beacon.App` 键不存在；直发后 4 个 Beacon AUMID 键均在（含 `C:/beacon-verify/beacon-app/Beacon.App.exe`） |
| 横幅显示 | A/B 两轮各 45s 轮询；另 OS 直发探针（注册 AUMID `C:/beacon-verify/beacon-app/Beacon.App.exe`，CustomActivator 在案、DisplayName=Beacon.App）@+2/+4/+6s 整屏截图 | ❌ **全程无横幅** | 枚举仅 3 个系统 CoreWindow（Windows 5120×2880@0,0 / 开始 1796×1396@0,1 / Realtek 3056×1853@0,65，均超尺寸带被拒）；虚拟屏 9440×3840 全图无 toast 横幅（P1/P2） |
| 点击→激活 | 无横幅可点 | ⛔ BLOCKED | A2/A3/B2/B3/B4 全负：无 `badproto` 日志、无看板窗、无浏览器进程 |

## 4. 对照（同管线另一机器可显示）

2026-11-06 用户本机（1920×1080）BM2 批：GLM Failed 态 OS 横幅实拍 **PRESENT**（12:04）→MISS（12:05）
相邻帧 + 通知中心两条 Beacon 通知在栏 + 详情窗 Open→浏览器落 bigmodel.cn/usage
（`bm2-toast-tray/` 台账 2026-11-08-bm2-toast-tray.md）。**同一应用管线在用户本机可显示横幅**，
imac 不显示 → 差异在机器/会话环境，非管线缺陷。

## 5. BLOCKED 原因（精确）

- **缺什么**：① 一个能显示 toast 横幅的 Windows 交互会话（imac 当前系统侧压制：直发**已注册 AUMID**的
  OS toast 亦无横幅，而投递键已生成——非投递问题）；② 真人点击 shell 横幅（合成 `SendInput` 点击
  shell 表面未经验证可靠，且横幅仅存 ~5-6s，时机窗口极窄）。
- **为什么**：① imac 环境（焦点助手「请勿打扰」或通知横幅关闭——投递照常但横幅不显示；`QuietHours`
  键与每 app `Enabled` 均未落显式值，属 UI 侧设置而非注册表可判）；② OS 横幅为 shell 层合成表面，
  合成点击需真实指针注入且时机敏感，自动化下无法稳定复现。
- **需要谁做什么**：装机/真人验收时在**能显示横幅的机器**（用户本机已证可显示）上执行——mock 或真实
  5h 额度耗尽触发 Error toast（带 detailUrl）→ **真人点击横幅** → 断言：有效 URL→浏览器落点 /
  无效协议 URL→应用日志「打开 Toast 深链失败：{url}」+看板回退（`ToggleBoard`）。imac 侧先关
  「请勿打扰」/打开通知横幅再复验。此为 `2026-11-08-l0-action-surfaces.md` 装机清单第 6 项（面 12）。

## 6. 证据文件（imac C:\beacon-verify\evidence-toast\，含第三方窗口，按隐私口径不入仓）

- `P1/P2-os-probe-banner.png`：OS 直发注册 AUMID toast 后 +2/+6s 整屏——无横幅（阴性证据）。
- `A0/B0-notification-center-fallback.png`：Win+N 回退截图（NC 未可靠开启，仅供参考）。
- `N1-nc-after-clock.png`：任务栏时钟点击（多屏任务栏布局复杂，NC 未开）。
- 结论性证据已文字化：应用日志段、通知设置键存在性、窗口枚举清单（上文 §3 表内）。

## 7. 时间线

- 2026-11-08 06:57-06:59 imac 探针（A/B 两轮）；06:5x OS 直发探针×2；07:0x 通知中心/时钟点击探针。
- 复现先决条件变化：旧卡点「匿名 GitHub 恒 403 无法稳定复现 Failed 态」**已解除**（本地 mock 稳定
  触发 Failed+detailUrl 并投递 OS）；新卡点=imac 横幅显示压制+真人点击。
