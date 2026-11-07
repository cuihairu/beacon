# 悬浮岛视觉链收口 · 取证（USER-RPT-1/2/4 + 看板回归，2026-11-08）

> 用户令（2026-11-08 凌晨「赶紧推进」）：用最新 main 构建在 .5 与 imac 真机截图诊断全部显示缺陷
>（按 Dynamic Island 观感标准），一次性修完不再挤牙膏。待用户装机亲验后闭项。

## 修了哪些显示缺陷（a413ec0，一次到位）

| # | 缺陷（实机取证定位） | 根因 | 修复 |
|---|---|---|---|
| 1 | **看板打不开**：热键/托盘/岛点击/L0 下钻/Toast 回退任何唤起面都弹不出看板 | `ToggleBoard()` 经 DI 解析 `JsonConfigurationStore`，但 `BuildServices` 从未注册该服务 → `InvalidOperationException`（回归源自 ad7043f 扩围②） | App 持 `_runtime` 直读实时配置（`BeaconRuntime.Config` 即 `JsonConfigurationStore`），DI 查询退场 |
| 2 | **岛不贴顶**：收起窗高 43 DIP（32 曲面+8 影带），面下拖一条全宽暗带，岛读成「矩形板」 | `MeasureSizePx` 收起态取内容实测高度（含影子层 8 DIP 预留） | 收起态窗高恒钉 `CapsuleIslandMath.HeightDips`=32（刘海=贴顶平齐，无落影带） |
| 3 | **展开→收起后窗高卡高不回落** | 刚收面那一帧 `Root.DesiredSize` 仍是展开态实测残留，实测值不可信 | 同上钉 32（确定性高度消除残留） |
| 4 | **面下暗带本体**：岛「软阴影」画成两层不透明剪影（Fill=曲面轮廓+粗描边+下沉）叠在面下 | 模糊黑影被实现为不透明轮廓拷贝（技法错层） | 剪影层退休（整层恒 Collapsed，节点留给未来真模糊挂回）；`ApplyIslandChrome` 不再挂影 |
| 5 | **启动闪一帧近全屏窗**（~1907×990）再收窄 | WinUI 3 Window 构造即建原生窗，`OnLoaded` 才取 AppWindow 预排尺寸 | ctor 抢先取 `_appWindow`/`_hwnd`（取不到则 OnLoaded 兜底，绝不因此崩溃） |

## 机器与构建口径

- 机器 1：imac（192.168.5.117，Windows，200% DPI 多屏，console 会话解锁运行中）——主视觉证据机。
- 机器 2：.5（10.240.0.5，100% DPI）——**取证被环境挡死**：console 停在锁屏（前台=「Windows 默认
  锁屏」，无边框 1920×1080 盖满主屏），岛被全屏避让**正确**隐藏（`EvaluateFullscreenAvoidance`：
  前台无边框窗盖满岛所在屏 → 退场；EnumWindows 实证 `Beacon Status vis=False`、`Beacon Pinned
  vis=True`）。**锁屏前台躲岛=避让面真机行为确认**（非缺陷）；无凭据解锁，100% DPI 视觉证据留待
  装机补。
- 代码：origin/main a413ec0 imac 现场构建 0 错误 + 四测试套全绿 + `dotnet publish
  -r win-x64 --self-contained true`；.5 同源 publish（diag 实证 app 起得来、仅避让藏岛）。
- 取证：窗口级 `PrintWindow(PW_RENDERFULLCONTENT)` + 整屏 `CopyFromScreen`，mock HttpListener
  双端点（打包机 A=Success、部署流水线=Error）。
- CI：run 42396968796 success（a413ec0）。

## 前后对比（窗口外框物理 px @200% DPI；外框=客户区+隐形边框 3px×2）

| 状态 | 修前（c78200e 构建，2026-11-08 01:0x 修前取证跑） | 修后（a413ec0 本轮） |
|---|---|---|
| 岛收起窗 | 482×86（40 DIP=32 曲面+8 影带） | **482×70（32 DIP 贴顶平齐）** |
| 看板唤起 | **打不开**（取证中断于 `window not found: Beacon 快速面板`） | **1466×1146 完整渲染**（04-board-window.png） |

修前岛影带另见 `island-ring/island-ring-bar-both.png`（11-07 帧，面下暗带可见）；修后同位干净。

## 证据（docs/acceptance-evidence/island-head-closeout/）

| 证据 | 内容 |
|---|---|
| `01-island-collapsed.png` / `02-island-collapsed-10x.png` | 收起岛 482×70：贴顶平齐黑药丸、肩 16 内收、底角 12、发丝描边；10x 像素级无暗带无板感 |
| `03-desktop-island.png` | 整屏剪影：岛贴顶长出、下缘干净（桌面 crop 复核过） |
| `04-board-window.png` | **看板打开**：左「现在」聚合灯+需要关注+概览卡、右时间线+通知+底条状态栏（mock 数据全渲染） |
| `05-desktop-board.png` | 看板整屏实拍 |
| `06-island-expanded.png` / `07-desktop-expanded.png` | 悬停展开 482×134：展开面从曲面内长出（「岛变高」而非「下方贴块」） |
| `08-island-critical.png` | `BEACON_DEBUG_SEVERITY=critical` 注入：红态徽标、窗高恒 70 不随级别变形 |
| `09-capsule-classic.png` / `10-desktop-classic.png` | 经典胶囊形态回归对照：256×70 照旧（岛改动零外溢） |

## 验收口径

- 待用户装机确认：装最新包看①岛贴顶无暗带、②看板热键/岛点击能开、③展开/收起无残留；
  亲验通过后 USER-RPT-1/2/4 闭项。
