# Beacon MVP Issue 列表

> 配套 [RFC-001](rfc/RFC-001-technical-design.md) 与 [Solution 目录](solution-structure.md)。
> P0 方向（公司打包工具 / AI 额度 / Generic HTTP）与增补计划见 [定位与首批场景](positioning.md)。
> 每个 Issue 自包含（可直接粘贴进 GitHub），验收标准即 Done 定义。
> **范围纪律**：不在此列表中的需求一律进二期（RFC §2.2 非目标清单为评审基线）。

## 里程碑

| 里程碑 | Phase | 目标 | 完成即 |
|---|---|---|---|
| M1 | P0–P1 | 工程地基 + Windows Shell，Beacon 可常驻 | 可安装可驻留 |
| M2 | P2 | Core 内核：配置/密钥/事件/调度/聚合/缓存 | 数据能流转 |
| M3 | P3–P4 | GitHub Provider + Action，Signal→Action 闭环 | 核心场景可跑 |
| M4 | P5–P6 | 通知引擎 + L2/L3 | 会提醒、能下钻 |
| M5 | P7–P8 | **L0 悬浮组件** + Settings + 发布验收 | **MVP** |

## 依赖总览

```text
B-001 ─ B-002 ─ B-003          (P0 地基)
  └─ B-101..105  (P1 Shell)
       └─ B-201..207 (P2 Core) ── B-301..304 (P3 GitHub) ── B-401..405 (P4 Action)
            │                        └──────────── B-501..504 (P5 通知) ── B-601..603 (P6 L2/L3)
            └────────────────────────────────────── B-701..705 (P7 L0 悬浮组件)
                                   B-801..804 (P8 Settings/导入导出/验收，依赖全部)
```

---

## P0 工程地基

### B-001 建立 Solution 与项目骨架
- **依赖**：无
- **内容**：按 [Solution 目录](solution-structure.md) 创建 sln、5 个 src 项目、3 个 test 项目；Directory.Build.props（nullable、C# 12）+ Directory.Packages.props；.editorconfig。
- **验收**：
  - [x] Windows 上 `dotnet build` 全绿；依赖规则符合（Core 零项目引用）
    - 证据（2026-10-09）：CI run 37781910707（8990676）windows-latest `dotnet build Beacon.sln` + test success；`src/Beacon.Core/Beacon.Core.csproj` 仅 Logging.Abstractions 包引用、零项目引用；本地 Linux 四库（Core/Connections/Actions/Storage）build 0 Error。
  - [x] 目录结构与文档一致
    - 证据（2026-10-09）：实测计数 = solution-structure.md 清单（Core Models 13 / App Windows 7 / Connections 21 文件，逐目录 ls 核对）。

### B-002 CI：Windows Runner 构建与测试
- **依赖**：B-001
- **内容**：GitHub Actions workflow（windows-latest）：`dotnet build` + `dotnet test`，PR 触发。
- **验收**：
  - [ ] PR 上 CI 自动跑 build+test；失败阻断合并
    - 状态（2026-10-09）：`ci.yml` 已配置 push(main)/pull_request 触发、windows-latest build+test，main 上连绿（37781910707 / 37781345045 均success）；「失败阻断合并」需仓库分支保护把 CI 设为 required check——仓库设置项，非代码缺口，待配置。

### B-003 Core 模型与枚举
- **依赖**：B-001
- **内容**：RFC §4 全部模型：Severity/LifecycleState/ConnectionConfig/WidgetConfig/WidgetTypeDescriptor/WidgetState/ActionConfig/ActionResult/NotificationRecord/PinLayout；JSON 序列化（枚举存字符串）。
- **验收**：
  - [x] 模型往返序列化单测通过
    - 证据（2026-10-09）：`SerializationRoundTripTests`（Core）+ Storage 三套往返（JsonConfigurationStore/JsonCacheStore/ImportExport RoundTrips）。
  - [x] 无任何 Provider 具体类型进入 Core
    - 证据（2026-10-09）：`grep -rlE "class \w*(Provider|Executor)|HttpClient" src/Beacon.Core/` 空；开闭性实证——方舟/MiMo/Codex 三笔 Provider 新增仅触 Core 的 BrandIcons（图标字典数据），模型与接口零改动（commits 31e408e / 26cf6b7 / 8990676）。

---

## P1 Windows Shell（M1）

### B-101 单实例 + 托盘 + 后台常驻
- **依赖**：B-001
- **内容**：命名 Mutex 单实例（二次启动退出并通知既有实例，气泡提示「Beacon 已在运行。」）；Win32 `Shell_NotifyIcon` 自实现托盘图标（菜单：Open / Settings / Exit）；启动时不打开主窗口，仅托盘。
- **验收**：
  - [ ] 双击 exe 二次启动不重复进程，既有实例收到唤起（气泡提示）
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 关闭所有窗口后进程驻留托盘，Exit 才退出
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-102 L1 Status Capsule 窗口
- **依赖**：B-101
- **内容**：小型 Topmost 无边框窗口（此时显示占位聚合 `🟢0 🟡0 🔴0`）；可拖动、位置持久化、PerMonitorV2 DPI；不进任务栏/Alt-Tab。
- **验收**:
  - [ ] 重启后胶囊回到上次位置；跨 DPI 拖动无尺寸错乱
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-103 全局热键 Ctrl+Alt+B
- **依赖**：B-101
- **内容**：RegisterHotKey 显示/隐藏 Quick Panel（本阶段可为占位窗口）；热键写入 config.json，可修改；冲突时提示。
- **验收**：
  - [ ] 任意前台应用下热键生效；可在设置改键并持久化
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-104 开机自启
- **依赖**：B-101
- **内容**：HKCU Run 键写入/移除；Settings 开关；自启时静默（仅托盘+胶囊）。
- **验收**：
  - [ ] 开关即时生效（重启验证）；卸载清理
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-105 崩溃兜底与日志框架
- **依赖**：B-101
- **内容**：全局异常处理 → `%AppData%\Beacon\logs` 滚动日志 + 托盘气泡提示，不闪退；ILogger 接入。
- **验收**：
  - [ ] 人为抛异常：进程存活、日志含堆栈、无僵尸窗口
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。代码路径已就位（非运行时验证）：App.xaml.cs OnLaunched catch → LogCritical + CrashLog.Alert，CrashLog 落 %AppData%\Beacon\logs。

---

## P2 Core 内核（M2）

### B-201 配置存储 JsonConfigurationStore
- **依赖**：B-003
- **内容**：config/connections/widgets/pins 四类 JSON 的读写；临时文件+原子替换+备份；损坏时回退备份。
- **验收**：
  - [x] 写入中断（模拟）不损坏现役配置；往返单测通过
    - 证据（2026-10-09）：`JsonConfigurationStoreTests`——CorruptMainFile_FallsBackToBackup / CorruptMainFile_KeepsCorruptCopy_AndReportsDiagnostics（中断最坏形态=半写主文件，损坏回退 .bak 用例覆盖）/ LoadAllBeforeMutate_PreservesPreexistingConfig（往返回归锚）；原子写 tmp+File.Replace。

### B-202 Secret 存储（DPAPI）
- **依赖**：B-201
- **内容**：`ISecretStore` + DpapiSecretStore（secrets.bin）；connections.json 只存 credentialRef；日志脱敏钩子。
- **验收**：
  - [x] set/get/delete 单测通过；磁盘文件不可读出明文；日志无密钥
    - 证据（2026-10-09）：`DpapiSecretStoreTests`——SetGetDelete_RoundTrip_AndPlaintextNeverOnDisk / DifferentCredentialRefs_CannotDecryptEachOther / GetUnknownRef_ReturnsNull（NonWindows 用例 OS 门控）；日志无密钥：全仓 Log 调用无 Authorization/token/secret 形参（静态 grep 空）。

### B-203 Event Bus 与 WidgetState 流转
- **依赖**：B-003
- **内容**：线程安全进程内 Event Bus；事件：WidgetStateChanged / ConnectionHealthChanged / NotificationRaised / ActionExecuted；UI 订阅方线程 marshal 约定。
- **验收**：
  - [x] 并发发布单测无死锁/丢发；有 UI marshal 辅助器
    - 证据（2026-10-09）：`EventBusTests`——ConcurrentPublish_NoDeadlockNoLoss / Publish_UsesSnapshot_HandlerAddedDuringPublishNotInvokedUntilNext / HandlerException_DoesNotAffectOtherSubscribers / SubscribeOnUi_MarshalsToDispatcher（marshal 走 IUiDispatcher.Post）。

### B-204 RefreshScheduler（分级刷新）
- **依赖**：B-203
- **内容**：RFC §7 策略表落地：tier 周期、±20% 抖动、失败指数退避（×2^n 封顶 10×）、成功复位、按 `(connection,tier)` 合并轮询、手动刷新入口、IClock 注入。
- **验收**：
  - [x] 退避/复位/合并（模拟时钟）单测通过
    - 证据（2026-10-09）：`RefreshSchedulerTests` 15 例——NextDelay_RespectsJitterBounds / ExponentialBackoffGrowth / CappedAtMaxBackoffMultiplier / SameConnectionTier_MergedIntoSingleGroup / BackoffOnFailure_ResetOnSuccess / Kick_WakesGroupBeforeDelayElapses；时钟注入 FakeClock。

### B-205 StatusAggregator
- **依赖**：B-203
- **内容**：WidgetState 集合 → overall max severity + 分级计数；发聚合变化事件。
- **验收**：
  - [x] 表驱动单测覆盖全组合（含空集=Success）
    - 证据（2026-10-09）：`StatusAggregatorTests`——MaxSeverityCases 表驱动 / EmptySet_IsSuccessBaseline_NoEvents / Counts_IncludeAllFiveSeverities / OfflineConnection_Counted_AndDeduped / PublishesOnlyOnChange。

### B-206 CacheStore 与缓存优先启动
- **依赖**：B-201、B-204
- **内容**：last-known-state 按 Connection 落盘；启动先渲缓存（标 fetchedAt/isStale）再后台刷新。
- **验收**：
  - [ ] 无网启动：界面有数据且标注 Last update，随后自动刷新
    - 状态（2026-10-09）：存储/宿主层单测已过（JsonCacheStoreTests：SaveThenLoad_RoundTripsAcrossInstances / CorruptCacheFile_TreatedAsEmpty；WidgetHostTests 缓存优先水合）；「界面有数据」为运行时表现——待 Windows 装机。

### B-207 连接健康与离线模型
- **依赖**：B-204
- **内容**：ConnectionHealth（Healthy/Degraded/Offline/Unauthorized）；失败→受影响 Widget 转 Offline 态（灰灯+Last update）；恢复自动复位；文案统一「⚠ Unable to refresh · Last update … · [Retry]」。
- **验收**：
  - [ ] 断网/恢复场景单测+手动验证；任何错误不产生未捕获异常
    - 状态（2026-10-09）：单测侧已过——WidgetHostTests：ConnectionException_MarksCachedStateStale_AndPublishesOffline / UnexpectedError_PublishesDegradedHealth_NoCrash / GroupAllFail_TriggersBackoff_HealthDeduped_RecoveryResets；手动断网/恢复验证——待 Windows 装机。
  - [x] 降级必须带真实原因，不吃成通用文案（2026-10-09 修复：方舟连上却显示「降级（限流等）」）
    - 根因：`IConnectionProvider.TestAsync` 只返回 HealthState，设置页把 Degraded 拼成通用文案「降级（限流等）」——真实失败原因（缺密钥/4xx/端点错）全部被吞。
    - 修法：合同升级为 `ConnectionTestResult(Health, Detail)`，11 家 provider 全迁；Degraded/Offline 一律携带真实异常信息直拼进设置页反馈（ArkUsageProvider 缺 AK/SK 文案给出录入位置指引；endpoint 改为连接配置可覆盖，不再硬编码）。单测：ArkUsageProviderTests ConnectionTest_DegradedCarriesRealReason / ConnectionTest_MissingCredentialCarriesRealReason。截图 `docs/img/evidence/2026-10-09-connections/ark-before-degraded-detail.png`（降级反馈直显「缺少火山 AK/SK」真实原因）。

---

## P3 GitHub Provider（M3）

### B-301 GitHub Connection
- **依赖**：B-202、B-207
- **内容**：GitHubConnectionProvider：PAT（repo+workflow 最小授权）校验、连接测试、rate-limit 感知（低额度自动拉长间隔）、ETag 条件请求（304 不计数不回调）。
- **验收**：
  - [x] 假 Handler 单测：ETag/401/403/限流分支全覆盖
    - 证据（2026-10-09）：`GitHubApiClientTests`——FirstGet_ReturnsBody_AndStoresEtag_SecondGetSendsIfNoneMatch（ETag/304）/ Unauthorized401_ThrowsUnauthorized / Forbidden403_WithQuotaExhausted_ThrowsOffline / RateLimitHeader_IsParsed_AndLowQuotaFlagged；`GitHubConnectionProviderTests`——MapsExceptionHealth / NetworkDown_Offline。
  - [ ] 真实 PAT 手动验证连接测试成功/失败路径
    - 待 Windows 装机。

### B-302 PR Widget Provider
- **依赖**：B-301
- **内容**：`github.pull_requests`：指定仓库（可多）open PR 数与列表；Severity 映射（有 review-requested/红 CI 的 PR → Warning，可配置）；refreshTier=pr。
- **验收**：
  - [ ] 夹具单测：计数/映射正确；真实仓库手动验证
    - 状态（2026-10-09）：夹具单测已过（GitHubPullRequestsProviderTests 6 例：计数/Severity 映射）；真实仓库手动验证——待 Windows 装机。

### B-303 Actions(CI) Widget Provider
- **依赖**：B-301
- **内容**：`github.actions.runs`：workflow×branch 最新 run → LifecycleState + Severity（Failed→Error，Running→Info+进度，超 15min→Warning）、时长、conclusion；refreshTier=ci。
- **验收**：
  - [ ] 状态映射表驱动单测；真实 workflow 手动验证（成功/失败/运行中）
    - 状态（2026-10-09）：表驱动单测已过（GitHubActionsProviderTests：Lifecycle/Severity 映射含 Failed→Error、Running→进度、超 15min→Warning）；真实 workflow 手动验证——待 Windows 装机。

### B-304 Widget 注册与类型元数据
- **依赖**：B-302、B-303
- **内容**：WidgetTypeDescriptor 注册表（type、pinSupported、建议 tier、默认配置模板）；DI 注册 Provider；为 Settings 的 Widget 创建向导供数据。
- **验收**：
  - [x] 新增 Provider 零改动 Core（验证开闭性）；描述符单测
    - 证据（2026-10-09）：开闭性实证=方舟/MiMo/Codex 三笔新增（31e408e/26cf6b7/8990676）Core 仅触 BrandIcons 图标字典，模型/接口零改动；描述符单测 `GitHubWidgetDescriptorTests` + 各 Provider 测试内描述符断言。

---

## P4 Action（M3）

### B-401 Action 框架
- **依赖**：B-203
- **内容**：ActionRunner：按 ActionType 路由 IActionExecutor；确认策略（可按 Action 关闭）；超时/取消；结果→ActionResult 事件（驱动 UI 反馈与通知）。
- **验收**：
  - [ ] 路由/确认/超时单测；失败在 UI 有可见反馈
    - 状态（2026-10-09）：单测侧已过——ActionRunnerTests：Routes_ToExecutorByType / Confirmation_Rejected_SkipsExecutor / Confirmation_Accepted_Executes / Timeout_CancelsExecutor_ReturnsFailure / ExecutorThrows_FailsGracefully / UserCancellation_ReturnsCancelled；UI 可见反馈——待 Windows 装机。

### B-402 Open URL Action
- **依赖**：B-401
- **内容**：`open.url`：系统默认浏览器打开（PR/Actions/Logs 链接模板支持 `{owner}/{repo}/{runId}` 占位）。
- **验收**：
  - [ ] 模板渲染单测；手动验证浏览器打开正确 URL
    - 状态（2026-10-09）：模板单测已过——OpenUrlExecutorTests：Renders_Template_FromVarsAndPayload / Payload_OverridesVars_OnConflict / MissingPlaceholder_FailsClosed；浏览器打开——待 Windows 装机。

### B-403 GitHub Workflow Dispatch / Retry / Cancel
- **依赖**：B-301、B-401
- **内容**：`gh.workflow_dispatch` 触发运行；Retry（rerun failed jobs）、Cancel（cancel run）封装为三个 Action；触发后创建短轮询跟踪至终态（§7 workflow 行）。
- **验收**：
  - [ ] API 封装单测（假 Handler）；真实仓库手动：Run→运行中→终态全程可在 Beacon 看到
    - 状态（2026-10-09）：API 封装单测已过（GitHubWorkflowExecutorTests：dispatch/rerun/cancel 三执行器假 Handler）；真实仓库全程可见——待 Windows 装机。

### B-404 Local Command Action
- **依赖**：B-401
- **内容**：`local.command`：powershell/cmd/exe；WorkingDirectory/环境变量/超时；捕获输出写缓存供「Open Logs」；默认执行前确认。
- **验收**：
  - [ ] 超时终止生效；输出落盘；确认弹窗可关（按 Action 配置）
    - 状态（2026-10-09）：单测侧已过——LocalCommandExecutorTests：Timeout_KillsProcess_AndStillWritesLog / RunsRealCommand_CapturesOutput_WritesLogFile；确认开关配置层已测（ActionRunnerTests NoConfirmationNeeded_ExecutesWithoutHandler）；确认弹窗本体 UI——待 Windows 装机。

### B-405 Generic HTTP Action
- **依赖**：B-401
- **内容**：`http`：方法/URL/头/体模板；非 2xx 视为失败并回报状态码。
- **验收**：
  - [x] 本地 stub 服务验证成功/失败/超时路径
    - 证据（2026-10-09）：`HttpExecutorTests`——Get_2xx_Succeeds_WithRenderedUrl / Post_WithHeadersAndBody_SendsAll / Timeout_FailsWithMessage（假 Handler 等价本地 stub，三路径全覆盖）。

---

## P5 Notification（M4）

### B-501 Toast 基础设施（unpackaged）
- **依赖**：B-101
- **内容**：CommunityToolkit Notifications；AUMID 快捷方式自检/修复；Toast 点击激活路由（深链到对应 L3）。
- **验收**：
  - [ ] 发送/点击/激活路由全链路手动通过；快捷方式缺失可自愈
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-502 NotificationEngine + 默认规则
- **依赖**：B-203、B-207、B-501
- **内容**：规则求值（when: widgetType/severityAtLeast → then: toast/sound/dockColor）；冷却去重；内置规则：CI Failed→Toast、运行>15min→Warning；通知记录滚动 200 条 + 已读/未读。
- **验收**：
  - [ ] 规则引擎表驱动单测（含冷却）；手动：注入 Failed 状态收到且仅收到一条 Toast
    - 状态（2026-10-09）：单测侧已过——NotificationEngineTests：DefaultThreshold_SeverityBand_TableDriven / SeverityAtMost_Band_WarningOnlyRule_IgnoresError / Crossing_SameAlertPeriod_FiresOnce_RearmsAfterRecovery（冷却/去重）；Toast 手动——待 Windows 装机。

### B-503 托盘着色与菜单
- **依赖**：B-205、B-502
- **内容**：托盘图标随 overall Severity 着色（绿/黄/红/灰）；托盘菜单含常用 Action 与 Notifications 入口。
- **验收**：
  - [ ] 聚合状态变化 ≤1 个刷新周期内反映到托盘
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-504 胶囊接入真实聚合
- **依赖**：B-205、B-102
- **内容**：胶囊显示真实分级计数；Offline 横幅（Last update）；点击打开 L2。
- **验收**：
  - [ ] 断网显示 Offline+时间；计数与实际 Widget 状态一致
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

---

## P6 Quick Panel / Detail（M4）

### B-601 Quick Panel 窗口（L2）
- **依赖**：B-504
- **内容**：Flyout 出现在胶囊/托盘附近；可激活（ESC 关、失焦关、Tab 导航）；不进任务栏。
- **验收**：
  - [ ] 打开 <100ms（缓存优先，无网络等待白屏）；键盘可完整操作关闭
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - 通知中心半区（2026-10-09 补齐）：NOTIFICATIONS 区渲染 `NotificationEngine.Records`（notifications.json 落盘水合，跨重启带出历史）——未读徽标/单击标已读/全部已读；此前 App 层零处消费 Records，仅 Recent Events 状态流。

### B-602 Overview + Recent Events
- **依赖**：B-601、B-203
- **内容**：按来源计数区（GitHub/CI/…）；Recent Events 列表（状态变化流，与通知同源）；列表项右键 Pin to desktop（写入 widgets.json，L7 阶段消费）。
- **验收**：
  - [ ] 计数/事件与状态一致；右键 Pin 落库（本阶段无 UI 效果）
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-603 Detail Window（L3）
- **依赖**：B-601、B-401
- **内容**：单对象视图（状态/仓库/分支/时长/阶段/错误摘要）+ Action 区（Open/Retry/Cancel/Open Logs，按 Widget 能力显隐）。
- **验收**：
  - [ ] 从 L2 事件一键打开对应详情；[Retry] 全链路可用
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

---

## P7 L0 悬浮组件（M5，RFC §6.2）

### B-701 PinnedHostWindow：单窗口多 tile 宿主
- **依赖**：B-102、B-304
- **内容**：每显示器一个透明置顶宿主（Topmost + WS_EX_NOACTIVATE + 空白点击穿透）；内部 tile 化布局渲染全部 pinned widgets；Composition 渲染，状态变化只更新对应 tile 视觉。（扩展已落地：设置「悬浮形态」可切换为独立悬浮框——每钉选组件一窗、桌面任意拖放、位置按组件记 `PinLayout.FloatingX/Y`，见 positioning §8。）
- **验收**：
  - [ ] 点击 tile 不打断当前应用焦点；空白区域点击落到桌面
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 悬浮形态：每组件一窗可拖放、重启后位置还原；与宿主面板二选一
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [x] 数量悬浮窗全局关 → 悬浮框零残留；悬浮框品牌 icon 可见（2026-10-09 修复 + CI 取证）
    - 白板残留根因：`Window.Close` 在无边框 NOACTIVATE 窗不保证销毁 HWND，内容已拆壳残留——Close 后句柄存活即 `DestroyWindow` 硬销毁；icon 空白根因：`Viewbox(PathIcon)` 在 WinUI 3 量测为空，改 `Path(Stretch=Uniform)` 直渲。
    - 截图证据：daily-build run artifact「visual-evidence」（开=悬浮框+icon 六倍近景；关=零残留，硬断言失败即红构建）。
  - [x] icon/文字对比度主题感知 ≥4.5:1（2026-10-09 二次修复：浅底近白发虚）
    - 根因：PinTile 前景硬编码近白 Rgb(255,226,232,240)，浅色底上对比不足；且 tile 根 Border 原为 12% 透明 scrim——叠在未知桌面/窗口底上对比度不可判定。
    - 修法：Core 新增 ContrastMath（WCAG 亮度/对比比）+ HighContrastPalette（Surface/Label/Value/IconOnTint 白黑择优，MinContrast=4.5）；tile 底改实色 `ThemeColors.Surface()`（浅底(241,245,249)/深底(15,23,42)随 RequestedTheme，重启生效）；icon 前景按品牌 tint 择优（九色 tint 全部 ≥4.5:1 有测试断言）。SettingsWindow 模块 icon 同链同修。
    - 证据：Core.Tests 191 绿（ContrastMathTests 硬断言）；CI 场景 C 白壁纸+light 主题暗像素 ≥30 硬断言；前后对比截图归档 `docs/img/evidence/2026-10-09-icon-contrast/`（before-light / after-light / after-dark 三态）。

### B-702 Pin/Unpin + PinTile 渲染
- **依赖**：B-701、B-602
- **内容**：消费 B-602 落库的 pinned 标记 + Settings 逐个开关；PinTile（StatusLight+标签+数字/进度，32 DIP 高）；仅 pinSupported 类型可钉；pins.json 读写。
- **验收**：
  - [ ] 两种入口（设置开关/右键 Pin）行为一致并持久化
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 非准入类型在两处入口均不可钉
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-703 拖动 / 吸边收起 / 位置记忆 / DPI
- **依赖**：B-701
- **内容**：tile 拖动+边缘吸附；贴死边缘收起为细条/圆点（悬停展开、水滴态点击弹回展开、近边松手回弹不误收）；pinLayout 持久化（monitor+anchor+offsetDips+collapsed）；显示拓扑变化按锚点恢复、越界回收；DPIChanged 适配。
- **验收**：
  - [ ] 重启后位置/收起态还原；拔显示器再接回 tile 不丢
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 跨 DPI 显示器拖放尺寸正确；分辨率变小 tile 被回收到可见区
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-704 L0 离线/错误降级
- **依赖**：B-701、B-207
- **内容**：tile 灰空心灯 + `Last update HH:mm`（tooltip）；绝不弹异常；恢复自动复位；isStale 标注。
- **验收**：
  - [ ] 断网全流程无任何弹窗；恢复后 tile 自愈
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-705 L0 行为铁律检查（只看不弹）
- **依赖**：B-702、B-502
- **内容**：验收性任务：确认 L0 不产生 Toast/声音/弹窗、不承载确认类 Action、Critical 仅灯脉冲（≤1Hz）；Alert 全部由 NotificationEngine 通道表达。
- **验收**：
  - [ ] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 手动清单（RFC §6.2.7 四条）逐条通过并记录
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-706 状态色用户配置（级别/Widget 两级覆盖）
- **依赖**：B-701、B-201..207（config/widgets 存储）
- **内容**（RFC §4.1/§6.2.8）：五级 + Offline 默认色表；`config.json appearance.severityColors` 按级别全局覆盖、`widgets.json colorOverride` 按 Widget 覆盖（Widget > 级别 > 默认，非法值回退默认）；`SeverityPalette` 改为读配置，L0 tile/L1 胶囊/L2-L3 状态点/托盘图标同源换色；改色即时生效（事件通知重绘，无需重启）。
- **验收**：
  - [ ] 设置改级别色 → L0 tile、胶囊、托盘图标同步变色
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 某 Widget 设 colorOverride → 仅该 tile 变色，优先级正确
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 非法颜色值回退默认色不崩溃；重启后保持
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-707 动效系统（状态过渡/呼吸灯/脉冲/滑入）
- **依赖**：B-701、B-706
- **内容**（RFC §6.2.8）：Composition 属性动画五族——状态变色交叉过渡（~200ms×intensity）、提醒闪烁（状态变化后短促 2 次）、呼吸灯（full 档常驻）、Critical 脉冲（reduced 档默认，≤1Hz×intensity）、tile 滑入；`motion.mode=full/reduced/off` 三档 + `intensity` 0.5–2.0；窗口不可见/收起态暂停动画循环；动画只作用于对应 tile 视觉层不重建整窗。
- **验收**：
  - [ ] Success→Error 状态变化有平滑变色过渡（reduced 档）
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] Critical 脉冲默认开；off 档全静止；full 档呼吸灯可见且强度可调
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 动效不抢焦点、不发声；隐藏/收起时无动画循环（功耗）
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

---

## P8 Settings / 发布 / 验收（M5）

### B-801 Settings UI
- **依赖**：B-201..207、B-301..304
- **内容**：Connections CRUD（测试连接按钮）、Widgets CRUD（含钉桌面开关）、热键、自启、外观（Light/Dark/System、透明度）、通知规则默认值；GitHub token 录入走 ISecretStore。调色与动效设置入口见 B-805。
- **验收**：
  - [ ] 全部设置项持久化且重启生效；token 不出现在任何 JSON
    - 状态（2026-10-09）：token 不落 JSON 已单测（DpapiSecretStoreTests 明文不落盘 + ImportExportTests Export_ContainsCredentialRefButNeverSecretMaterial；db56f28 起 App 启动 LoadAll，持久化链路回归锚 LoadAllBeforeMutate 在测）；「重启生效」运行时表现——待 Windows 装机。
  - [x] 连接保存/启停/删除后组件「连接」下拉即时刷新；四家 Provider（方舟/Kimi/MiMo/DeepSeek）全链路可见（2026-10-09 修复）
    - 根因：SaveConnectionAsync 只重建连接列表不刷组件向导下拉（`RefreshWidgetConnectionOptions` 未被调用，且无 null 守卫——高级页导入路径潜伏 NRE）；用户报「类型缺失」实为旧 nightly 未含新 provider + 下拉不刷新的叠加。
    - 修法：保存/启停 toggle/删除三处统一调 `RefreshWidgetConnectionOptions`（带 `_widgetConnectionBox is null` 守卫）。四家 provider 注册早已在（BeaconRuntime widgets+connections），非注册缺失。
    - 证据：CI 场景 D——本地 mock HTTP + DPAPI 预置密钥 + UIA 逐家点「测试」断言「连接正常」+ 断言组件下拉含该项 + save-flow（新建 ark 连接保存后立即出现在下拉）。四家截图 + save-flow 归档 `docs/img/evidence/2026-10-09-connections/`（ark/kimi/mimo/deepseek-healthy-dropdown + save-flow-dropdown-refresh）。连接链 CI 证据为 mock 口（CI 无真实凭据）；真实凭据行为由用户装机后设置页「测试连接」确认。

### B-802 导入导出
- **依赖**：B-801
- **内容**：导出 config+connections+widgets+pins 打包 JSON（**不含 secrets**）；导入后逐 credentialRef 提示重录。
- **验收**：
  - [ ] 导出文件全文无 token；导入+重录密钥后行为与原机一致
    - 状态（2026-10-09）：存储层已过——ImportExportTests：ExportImport_RoundTripsAllFourFiles / Export_ContainsCredentialRefButNeverSecretMaterial / Import_ReplacesPreviousStateInsteadOfMerging / Import_ListsCredentialRefsForReEntry / Import_RejectsUnknownBundleVersion；重录密钥后的端到端行为——待 Windows 装机。

### B-803 MVP 验收走查（任务书 §成功标准）
- **依赖**：全部
- **内容**：端到端手动脚本：自启 → 胶囊常驻 → GitHub Actions 失败 → Toast → 点击进详情 → [Retry] → 成功 Toast，全程不开浏览器；同时执行 B-705 铁律清单与四层交互清单。
- **验收**：
  - [ ] 全流程录屏/截图归档至 docs/；发现问题全部修复或开 Issue
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-804 打包发布
- **依赖**：B-803
- **内容**：self-contained 发布——Inno Setup `setup.exe`（x86_64，装到 %LOCALAPPDATA%\Beacon）+ 便携 zip（win-x64 自包含）双产物；版本号；GitHub Release 产物 + 安装说明（自启/托盘/热键/权限）。
- **验收**：
  - [ ] 干净 Windows 11 虚拟机：下载→运行→完成 B-803 脚本
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-805 外观/动效设置页（调色板 + 动效档位）
- **依赖**：B-801、B-706、B-707
- **内容**（RFC §4.1/§6.2.8/§9.1）：级别色编辑器（六色 + 重置默认）；Widget 级 colorOverride 入口（随 Widgets CRUD）；动效三档 full/reduced/off + intensity 滑杆 + 逐族预览（过渡/闪烁/呼吸灯/脉冲/滑入）；改动即时预览、落 config.json appearance。
- **验收**：
  - [ ] 调色/动效改动即时预览并持久化，重启生效
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
  - [ ] 重置默认一键还原；off 档预览即全静止
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

---

## 附：MVP 明确不做（评审基线）

复杂插件市场 · Workflow DSL/引擎 · AI Agent · Jenkins/GitLab/Docker 全功能管理 · 数据库 · 云账号/用户系统/团队协作/云同步 · 复杂图表 · 复杂主题 · macOS/Linux。
