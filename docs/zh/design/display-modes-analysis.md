# 状态展示形态分析：悬浮框 / 悬浮岛 / 任务栏（含扩展调研）

> 状态：draft。本文只做分析，不含实现。唯一按定案记录的是交互模型（§1，用户原话）；§7 的形态推荐为代决（效果后审）。
> 证据基础：本仓库真实代码，行号引用以 commit a498ba7 为准。Windows 系统侧事实单独标注，不冒充仓库事实。
> 英文对应页：[docs/design/display-modes-analysis.md](/design/display-modes-analysis)（内容一致）。

## 0. 问题与方法

Beacon 需要决定聚合状态以什么形态到达用户眼前。先问的三种形态——①悬浮框 ②悬浮岛（Dynamic Island 风格顶部胶囊）③任务栏——后扩展六个面（§5）。每形态按同四步分析：**选显示方式 → 选显示组件 → 链接数据 → 交互**，另附可行性（WinUI 3 / Win32 API 层面）、工作量（S/M/L）与风险。

方法说明：凡涉及本仓库的论断均带 `file:line` 引证；涉及 Windows 本身（API 存在性、系统行为）的标注为系统侧，是唯一不引仓库行号的部分。某形态与 Windows 实际开放给第三方的面相冲突时，本文直说，不绕着幻想中的面做设计。

## 1. 交互模型（用户拍板；约束本文所有形态）

用户原话：**「所有形态只展示状态（只读），不做形态内操作；点击任一形态 → 唤起主面板/看板，面板内操作一律弹二次确认。」**

对照现树逐条审计该契约的落地情况：

- **已合规**
  - 胶囊点击 → 看板：`src/Beacon.App/Windows/CapsuleWindow.cs:49-50`（事件），接线在 `src/Beacon.App/App.xaml.cs:197`（`quickPanel.Toggle`）。悬停预览纯展示（`CapsuleWindow.cs:156-227`）。
  - 托盘左键 → 面板：`src/Beacon.App/Services/TrayIconService.cs:238-241`。
  - Toast 点击 = 深链导航，不是面内操作：`src/Beacon.App/Services/ToastNotificationService.cs:9-10`（`ToastActivation` 载荷 → widget L3）。
  - 操作的确认闸门在执行层已存在：`src/Beacon.Core/Services/ActionRunner.cs:69-76`（`RequireConfirmation` → 注入的 `ConfirmationHandler`；无通道时保守拒绝，`:10`）。
- **需修订才合规**
  - 托盘右键菜单现携带操作项（Notifications / Refresh All / Components，`TrayIconService.cs:275-277`）。按契约，菜单收缩为导航 + Exit（`Exit` 保留——它是唯一退出路径，`TrayIconService.cs:11`）。
  - 面板侧操作必须全部走 `ActionRunner`，让确认闸门单源化；任何绕过执行层直接跑的 XAML 调用路径都违反契约（审计项，非新代码）。
- **对成本的影响**：该契约把「面内菜单/操作」从本文所有新形态的范围里**删掉**，因此下方各形态的工作量都不含交互菜单，只计面板内一份共享确认 UX。

## 2. 形态① 悬浮框（floating panel）

**结论：已实现。本形态就是仓库当前的重心。**

- **可行性**：常驻无边框置顶亚克力窗已上线——`CapsuleWindow.cs:62`（`DesktopAcrylicBackdrop`）、`:79-89`（`OverlappedPresenter.IsAlwaysOnTop`、`SetBorderAndTitleBar(false,false)`、`IsShownInSwitchers=false`、`WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`）。展开看板是第二扇窗、Flyout 语义——`QuickPanelWindow.cs:24-31`（失焦自隐、ESC 关闭 `:116-118`、固定 380×540 `:34-35`），靠事件常驻订阅 + 缓存水合做到打开零网络等待（`:67-71`、`:74-80`）。第三种变体——每个钉选组件一扇悬浮窗——也已上线（`src/Beacon.App/Windows/FloatingTileWindow.cs:18-25`，`app.pinDisplayMode = "floating"`）。
- **显示组件**：状态点 + 健康环 + 分级计数 + 离线横幅（`CapsuleWindow.cs:428-472`，环见 `src/Beacon.App/Components/StatusRing.cs:8-11`）；悬停出按来源聚合的预览行（`CapsuleWindow.cs:55-56`、`:193-227`）。
- **数据接线**（全部现成，无需新服务）：
  - 聚合：`StatusAggregator` → `AggregateStatusChanged`（`src/Beacon.Core/Services/StatusAggregator.cs:31` 快照；`src/Beacon.Core/Events/Events.cs:14`），消费在 `App.xaml.cs:208`。
  - 单组件：`WidgetStateChanged`（`Events.cs:6`）× `WidgetState`（`src/Beacon.Core/Models/WidgetState.cs:9-21`：Severity、Summary、Progress、Payload、FetchedAt、IsStale、ConnectionHealthy），按来源分组在 `App.xaml.cs:487-494`。
- **§1 下的交互**：点击 → 看板（已做）；悬停预览（已做，只读）；拖动改位属窗口管理而非状态操作——保留（`CapsuleWindow.cs:141-277`，按显示器锚点持久化 `:332-395`）。
- **工作量**：S（仅剩打磨）。**风险**：低——历史上的硬坑已在码内解决并留档（DPI 物理像素口径 `:293-301`、非客户区差值 `:303-312`、拖动与 DPI 切换互抢 `:110-127`）。

## 3. 形态② 悬浮岛（Dynamic Island 风格顶部胶囊）

**结论：可行，路线是 CapsuleWindow 演进而非新建窗口类。工作量 M。**

- **可行性（WinUI 3 / Win32）**：
  - 窗体底盘：与胶囊同一配方——无框置顶不激活的 `OverlappedPresenter`（`CapsuleWindow.cs:79-89` 模式已验证，含无焦点悬停）。
  - 顶部居中锚定：按显示器锚点 + 偏移的持久化（`PinLayout`，`CapsuleWindow.cs:332-395`，经 `src/Beacon.App/Infrastructure/MonitorService.cs` 工作区）已支持任意角；顶中锚只是新增一个锚点值，不是新机构。
  - 胶囊形变（收 ↔ 展宽高）：仓库已按实测内容改尺寸并重新贴锚点——`MeasureSizePx`/`ApplySize`（`CapsuleWindow.cs:293-330`）；展开可用 `MotionEngine` 做动画（`CapsuleWindow.cs:94`、`:450-460` 已有 Slide-in/Flash/过渡挂点，含 off 档）。形变只在状态变化/悬停时触发——低频，逐帧 `AppWindow.Resize` 的成本可接受。
  - 圆角观感：XAML `Border.CornerRadius` 叠 `DesktopAcrylicBackdrop`（`CapsuleWindow.cs:62`）是已验证底料。系统侧如实说明：WinUI 3 `Window` 是矩形，不提供逐像素透明，四角像素会带亚克力。`DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE)`（系统侧，Win11）可把窗口框本身切圆角；两种处理都像岛——实现期再选。
  - 全屏应用：TOPMOST 带窗能浮在多数全屏窗之上；独占全屏游戏可能盖住它。对一颗状态胶囊可接受，与今日胶囊同一行为类。
- **显示组件**：收起态一行（`「2 ok · 1 warn」` 计数 + 级别点——正是 `CapsuleWindow.cs:430-448` 已渲染的聚合）+ 可选未读通知徽标（计数取 `NotificationEngine.Records`，`src/Beacon.Core/Services/NotificationEngine.cs:50`，滚动上限 200 `:17`）；展开态 = 悬停预览行的推广（`:193-227`）。
- **数据接线**：与形态①同三源（`AggregateStatusChanged`、`WidgetStateChanged`、`NotificationEngine`）；无新 Provider。
- **§1 下的交互**：悬停展开（只读）、移出收起、点击 → 看板。岛内无菜单、无操作。
- **工作量**：M（新锚点值 + 形变动画 + 徽标；底盘与数据全复用）。**风险**：中低——形变与锚定的配合是唯一真正新的几何代码，其余全是已验证模式的重跑。

## 4. 形态③ 任务栏

**结论：托盘图标就是任务栏形态，且已做完；ITaskbarList3 的三件套 API 真实存在但不适配；第三方真任务栏 Widget 不开放。除已上线者外不再建。**

- **(a) 通知区图标——已上线**：`Shell_NotifyIcon` 自实现，不引第三方托盘库（`TrayIconService.cs:10-12`，NIM_ADD `:84`）；级别色图标按 logo 剪影运行时染色、按色缓存 HICON（`:117-162`、`:168-195`，B-706 配置色即刻生效）；tooltip 承载与胶囊相同的聚合计数（`:206-216`，文案拼装在 `App.xaml.cs:480-482`）；Explorer 重启后经 `TaskbarCreated` 广播恢复（`:90-91`、`:252-259`）；Toast 未就绪路径的气泡兜底（`:95-115`）。
- **(b) ITaskbarList3**（图标 Overlay / 按钮内进度 / 缩略图工具栏——系统侧有文档的 API）：**要求窗口真的持有任务栏按钮。** Beacon 刻意一个都没有——所有窗都不进任务栏和 Alt-Tab（`CapsuleWindow.cs:87-89`、`QuickPanelWindow.cs:100-101`、`FloatingTileWindow.cs:409` tool-window 样式），且仓库零 `ITaskbarList3` 调用（grep `src/` 为空）。采用它意味着为承载一个与托盘图标信息重复的 Overlay 而专门养一扇任务栏按钮窗。诚实的成本收益：架构翻动换冗余信号。不推荐。
- **(c) 真任务栏 Widget**：任务栏表面为系统内容保留，无公开第三方 API 可把活动 Widget 钉上任务栏本体（系统侧事实）。该领域受认可的第三方面是 Widgets Board——见 §5.4。任何「往任务栏里放活动块」的方案都是在 Windows 不卖的地皮上做设计。
- **数据接线**：已接线——每次聚合变化调 `tray.SetSeverity(status.Overall)`（`App.xaml.cs:208-209`）。
- **§1 下的交互**：左键 → 面板（已做）；菜单按 §1 收缩。
- **工作量**：S 已沉没；(b) 若做是 M 且收益为负。**风险**：托盘独占应用的可见性受通知区「隐藏的图标」用户设置支配（系统侧设置）——这正是推荐里托盘只能当**兜底**、永不做主信号的原因。

## 5. 扩展调研：六个候选面

### 5.1 壁纸集成

- **可行性**：两条路。(i) 静态换图：`SystemParametersInfoW(SPI_SETDESKWALLPAPER)` 或按显示器的 `IDesktopWallpaper` COM 接口（系统侧，Win8+）：把状态烘进位图、写盘、设置。每次刷新 = 整图编码 + 写盘 + shell 重载——可见闪烁，逐次 I/O 与 CPU。按刷新档节奏到来的单组件变化（`src/Beacon.Core/Models/RefreshTiers.cs`）会把这条路线来回拖。(ii) 动态渲染：Wallpaper Engine 路线用未公开的 Progman/WorkerW `0x052C` 消息把窗口注入桌面图标之后（系统侧，不受支持）。它在 Explorer 重启时会碎——仓库托盘已在对抗同类脆弱性（`TrayIconService.cs:90-91`），且持续吃全屏合成成本。
- **组件**：零现成；需要位图合成器与 widget 卡的栅格排版。
- **数据接线**：聚合 + 单组件状态——拿得到，但被表面成本判死。
- **§1 下的交互**：壁纸没法被点击唤起面板，除非再养一扇全屏透明命中测试窗——为一次点击加一套机构。
- **结论**：技术上可行，产品上错误：干扰**高**（整桌面为一点而重绘）、性能**高**、风险**高**，可读性还被人家的壁纸拿捏。**不推荐。**

### 5.2 托盘 tooltip / 气球通知

- **可行性**：已上线。tooltip 走 `NIF_TIP`（`TrayIconService.cs:206-216`），气球走 `NIF_INFO`（`:95-115`）。系统侧限制：tooltip 的格式与时长由系统控制、单行；Win10 起 shell 把托盘气球按 Toast 渲染，运行时此面并回 §5.3。
- **组件 / 数据**：无新增——计数文案已拼好（`App.xaml.cs:480-482`）。
- **§1 下的交互**：只读 ✓。
- **结论**：按现码保留，作 Toast 未就绪的兜底（RFC §16 风险 2，码内引证 `TrayIconService.cs:94`）。工作量 S、风险低、干扰低（瞬态）。已完成。

### 5.3 Windows Toast 通知（重要事件推送）

- **可行性**：已上线，含最难的非打包激活。`ToastNotificationManagerCompat` 首触写 AUMID 注册键并自愈（`ToastNotificationService.cs:14-20`、`:38-39`、`:74`）；点击激活深链到组件详情（`:9`）；上游由规则表投递，带去重/冷却（`NotificationEngine.cs:11-14`）。
- **组件 / 数据**：`NotificationRecord` → Toast 文案；无新组件。
- **§1 下的交互**：合规——Toast 只展示 + 导航；今日未提供 Toast 内操作按钮，契约下也不许加。
- **结论**：保留。这是**推送**通道，与「常驻可瞟」的面互补而非互替。工作量 S（已上线）、风险低（AUMID 自愈经 CI 场景 F5t 实测）、干扰中（侵入是设计使然——故有规则闸门）。

### 5.4 Win11 Widgets Board 第三方接入

- **开放现状（系统侧）**：第三方 widget provider 是有文档、已发售的平台：实现 `IWidgetProvider` COM 接口，内容按 Adaptive Cards JSON 编写（Windows App SDK 1.2+；widget 出现在 Widgets Board，经 Win+W 或任务栏图标打开）。要求打包标识——仓库已带 `packaging/msix/`，前置条件在。内容面是 Adaptive Cards 而**非** XAML——`src/Beacon.App/Components/` 全部不可复用，是第二条渲染路径。
- **`ICardsWidget234`**：在 Windows Insider 版本里被观察到的未公开 COM 接口，属于微软在途的 widgets/"cards" 改版。闭源上游、无公开文档，第三方无法定向采用。它的存在只当**变动信号**——Board 契约可能翻——不当 API 接。
- **组件**：新 provider 宿主 + 按尺寸档的 Adaptive Card 模板；随 `WidgetStateChanged` 推送更新。
- **数据接线**：直接（同一 `WidgetState` 流序列化进卡片 JSON）。
- **§1 下的交互**：卡片只读出（剥离 `execute` 动作）；卡片点击 → 深链进面板——激活模型支持。
- **结论**：可行但现在不做。干扰低（Board 由用户打开、绝不自弹——是「主动查看」面而非「可瞟」面）、成本 M–L（第二渲染路径）、风险中高（平台翻动；`ICardsWidget234` 即地面在动的证据）。**观察；待 Board 稳定再议。**

### 5.5 锁屏界面

- **可行性**：**不可行——如实说明。** 第三方不能替换或承载 Windows 锁屏上的 UI（系统侧事实）。Win8 时代的旧锁屏应用 API（`LockScreenManager` 徽标/文本 + 后台任务）是弃用路径，Windows 11 上对现代桌面应用实际不再放行。此处不值得任何设计工作。

### 5.6 第二屏 / 独立小窗

- **可行性**：**已上线。** 悬浮形态让每个钉选组件独占一扇置顶、可拖、按组件记位的窗（`FloatingTileWindow.cs:18-25`，位置在 `pins.json`），多显示器落位与显示器命名由 `MonitorService` + `PinLayout` 承担（与 `CapsuleWindow.cs:332-395` 同一套锚点数学）。把 tile 拖去第二屏即用。另外 `config.keepQuickPanelOpen` 已能把看板变常驻窗（`QuickPanelWindow.cs:26`）——第二屏「迷你仪表盘」= 这个开关 + 落位。
- **结论**：保持现状；工作量 S 剩余打磨（如按显示器记住 keep-open 落位）。干扰低（屏是专用）、风险低。

## 6. 对比总表（全部形态）

| 形态 | 可见性 | 信息密度 | 干扰度 | 实现成本 | 风险 | §1 契约 | 结论 |
|---|---|---|---|---|---|---|---|
| ① 悬浮框（胶囊 + 看板） | 高（常驻） | 高（可展开看板） | 低（小、角落） | S（已上线） | 低（已知坑已解） | 合规 | **保留——主信号** |
| ② 悬浮岛 | 高（常驻、顶中） | 中（一行 → 预览） | 中（抢眼位置） | M | 中低（形变几何是新码） | 设计即合规 | **推荐演进** |
| ③a 托盘图标 + tooltip | 中（可被系统藏起） | 低（图标 + 一行） | 低 | S（已上线） | 低（藏图标是系统设置） | 合规 | **保留——兜底** |
| ③b ITaskbarList3 Overlay | 中 | 低 | 低 | M | 中（需任务栏按钮窗 → 动架构） | 合规 | **否决** |
| ③c 真任务栏 Widget | — | — | — | — | — | — | **不可行**（面不开放） |
| 5.1 壁纸 | 极高 | 低 | **极高**（全桌面重绘） | M | 高（不受支持的注入路） | 差（无法干净点击） | **否决** |
| 5.2 托盘 tooltip/气球 | 中 | 极低 | 低（瞬态） | S（已上线） | 低 | 合规 | 保留为兜底通道 |
| 5.3 Toast 推送 | 中（瞬时） | 中（单事件） | 中（侵入式） | S（已上线） | 低（AUMID 自愈已测） | 合规 | **保留——事件通道** |
| 5.4 Widgets Board | 低（用户打开） | 中 | 低 | M–L（第二渲染路径） | 中高（平台翻动，`ICardsWidget234` 变动） | 合规（只读卡片） | 观察 |
| 5.5 锁屏 | — | — | — | — | — | — | **不可行** |
| 5.6 第二屏小窗 | 高（专用屏） | 高（多 tile） | 低 | S（已上线） | 低 | 合规 | 保持现状 |

## 7. 推荐（按 §1 交互模型修订）

**推荐组合：常驻胶囊（向悬浮岛演进）+ 点击 → Quick Panel 看板 + 托盘级别色图标兜底 + Toast 推阈值事件。** 具体：

1. **主信号 = 胶囊**（`CapsuleWindow`），向岛形态演进（顶中锚点选项、形变展开、未读徽标）。它是唯一同时满足常驻、可瞟、低干扰、且已建成 80% 的面。
2. **深度 = 看板**（`QuickPanelWindow`），由**任一**形态点击唤起——契约规定的唯一交互。
3. **兜底 = 托盘图标**（级别色 + 计数 tooltip）：胶囊关用（`showCapsule=false`，`CapsuleWindow.cs:130-134`）或屏幕背对时仍可见；仅当用户隐藏托盘图标（系统侧设置）时降级。
4. **推送 = Toast**：阈值跨越事件，带去重/冷却（`NotificationEngine`），永不做常驻状态的替身。
5. **已否决**：壁纸（§5.1）、ITaskbarList3 Overlay（§4b）、锁屏（§5.5，不可行）。**观察名单**：Widgets Board（§5.4），平台止动后再议。

**分期**（每期独立可交付；全程只读，遵 §1）：

- **Phase 1（S）——契约清扫**：托盘菜单收缩为导航 + Exit；审计面板操作全部经 `ActionRunner` 确认闸门（`ActionRunner.cs:69-76`）；不建新面。
- **Phase 2（M）——胶囊岛化**：顶中锚点值、`MotionEngine` 悬停/展开形变、取自 `NotificationEngine.Records` 的未读徽标。
- **Phase 3（可选）——第二屏打磨**：keep-open 看板落位记忆。**观察名单（不排期）**：`packaging/msix` 后面的 Widgets Board provider 尖兵。

决策记录：§1 是用户令（原话引用）。上方形态结论与分期为代决（效果后审）——对任何一条有异议，本文按反 verdict 修订。

## 8. 下一步

**用户先选定形态，再出实施计划。** 本文刻意停在可行性 + 推荐。§7 的自然选择是 Phase 1 → Phase 2；用户一句拍板（「按 Phase 1/2 走」或换组合）即解锁实施计划作为下一篇文档。
