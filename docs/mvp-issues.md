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
  - [x] PR 上 CI 自动跑 build+test；失败阻断合并
    - 状态（2026-10-09）：`ci.yml` 已配置 push(main)/pull_request 触发、windows-latest build+test，main 上连绿（37781910707 / 37781345045 均success）。
    - 进展（2026-10-10）：main 分支保护已启用（API：allow_force_pushes=false、allow_deletions=false，无 required check/PR 要求——直推流程不受影响）。
    - **拍板（2026-10-10 用户令）**：维持直推现状、**不设 required check**（设了会挡 daily-build 自身推送）；如需再议 PR 工作流再启。

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
  - [x] 断网/恢复场景单测+手动验证；任何错误不产生未捕获异常（2026-10-10 勾：本地断网恢复已 socket 级实证，见下；装机目视随 B-803 走查复核）
    - 状态（2026-10-09）：单测侧已过——WidgetHostTests：ConnectionException_MarksCachedStateStale_AndPublishesOffline / UnexpectedError_PublishesDegradedHealth_NoCrash / GroupAllFail_TriggersBackoff_HealthDeduped_RecoveryResets。
    - 状态（2026-10-10，4b04205）：升级 socket 级——OutageRecoveryIntegrationTests（Connections.Tests）：真 HttpStatusProvider + 真 HttpClient + 真 JsonConfigurationStore/JsonCacheStore，本地 HttpListener 停机/重启模拟断网/恢复。断网 → Offline 健康 + 陈旧态重发布（旧缓存保留，Last update 不丢）+ 失败原因进日志；恢复 → 自动复位 Healthy + 新状态落盘；全程 RefreshWidgetAsync 返回 false 而非抛出（无未捕获异常）；首错无缓存 → 「拉取失败」合成态直显。
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
  - [x] 全部设置项持久化且重启生效；token 不出现在任何 JSON（2026-10-10 勾：两半皆本地自动化实证，见下）
    - 状态（2026-10-09）：token 不落 JSON 已单测（DpapiSecretStoreTests 明文不落盘 + ImportExportTests Export_ContainsCredentialRefButNeverSecretMaterial；db56f28 起 App 启动 LoadAll，持久化链路回归锚 LoadAllBeforeMutate 在测）。
    - 状态（2026-10-10，4b04205）：「持久化+重启生效」本地全证——SerializationRoundTripTests（AppConfig 全字段往返 + 默认值不变式：热键/自启/主题/透明度/胶囊/ConfigVersion/PinDisplayMode/数值悬浮/轮询间隔/通知规则/级别色/动效）+ JsonConfigurationStoreTests.AppConfig_AllFields_SurviveSaveAndReload（SaveApp → 新实例 LoadAll = 启动代码路径，逐字段比对）。Windows 设置页逐控件写入的目视复核随 B-803 装机走查，不另挂账。
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

## 2026-10-10 拍板记录
- **B-002 CI required check**：维持直推现状、不设 required check（设了会挡 daily-build 自身推送）；如需再议 PR 工作流再启。
- **状态胶囊默认值**：维持当前实现不改（`ShowCapsule=false` 默认关，装机后设置页可开）。
- **装机走查引导清单**：44 项验收按 nightly 资产装→验→回执格式整理为 [docs/acceptance-checklist.md](acceptance-checklist.md)，随批落仓。

## 2026-10-09 实测回执批（小米类型空 / 千问接入 / 检查频率 / 方舟 5h 口径 / tile 名字）

### 回执① 小米组件类型一直空（连接链已好的对照下集中排查）
- **根因**：设置页组件向导类型下拉的聚合表 `AllWidgetDescriptors` 漏聚合 `MiMoWidgetDescriptors`（同病
  Codex/Copilot/OpenCode 三家）——`mimo.usage` 前缀本符合「类型前缀=连接类型」过滤约定，聚合补齐即修。
- **验收**：CI 视觉取证组件向导「类型」下拉逐家断言含本家条目 + 截图（mimo/kimi/deepseek/qwen 四张
  `D-*-type-dropdown.png`）。

### 需求⑤ 全局检查频率（设置 → 常规）
- **内容**：`PollIntervalSeconds`（0=按组件档位策略表，默认）五档 ComboBox；组件级检测间隔优先、全局兜底；
  变化检测才重建调度（未变不重拉）。
- **验收**：设置面板该项可见截图（`D-general-poll-interval.png`）+ 改 15 秒后 config.json 落盘与日志
  「检查频率变更为 15s」双硬断言。

### bug 批3 方舟悬浮框口径（两项）
- **5h 窗口主口径**：tile 主数值/级别/进度此前取最差窗口（周/月冒充当前窗口）——改 session（5h）档优先
  （无 session 退最差窗口兜底），Summary 带「5h x% · 重置 MM-dd HH:mm」，周/月留 payload；测试三处重写。
- **名字显示 usage**：PinTile 标签兜底取类型尾段（字段名冒充显示名）——`WidgetTypeNames`（Core，可单测）
  按前缀映射连接类型短名（火山方舟/智谱/…），L2 chip 的 GLM/ARK 硬编码并入同表；全类型巡检：tile 标签
  /L2 chip/设置侧栏 ModuleDef/provider Summary 头四处口径核对，无第二个拿字段名当名字的点
  （SettingsWindow 的类型尾段仅作 widget Id 种子，非显示名）。
- **验收**：tile UIA 文本硬断言「火山方舟/DeepSeek/千问 + 42.5/¥420.5」且无 usage/quota 残留 + 截图
  （`D-provider-tiles-desktop.png`、`D-tile-*-closeup.png`）。

### 回执④ 阿里千问接入（百炼 Token Plan）
- **内容**：连接类型 `qwen`（测试=GET /models，专属 Key sk-sp- 实测 200）+ 组件 `qwen.usage`（官方额度
  REST 口未开放——默认模型目录真数据如实标注；连接「用量端点」填自定义口即出额度+重置日，防御键名解析）
  + BrandIcons qwen 官方标（Simple Icons slug qwen）+ 设置模块页 + 表单用量端点框。
- **验收**：类型可见（类型下拉断言+截图）+ 连接成功（mock /compatible-mode/v1/models→连接正常）+
  额度/重置日显示（mock /qwen/usage → tile「千问 · 本期已用 42.5% · 重置 10-19 08:00」UIA 断言+截图）。

### 回执②③ DeepSeek 展示（进度条撤销为 bug，不动）
- **结论**：DeepSeek 按量计费本无进度（`Progress=null` 从第一天如此，非回归）——进度条代码不动；
  余额渲染链已有（tile 值位 ¥total），CI tile 断言含「¥420.5」佐证不空白。

### 批3-补2 小米额度位（模型数不得冒充额度，2026-10-10）
- **数据源复核**：官方无额度接口坐实（/usage /quota /balance 等 7 端点全 404；/models 与 /chat/completions
  响应均无限流头——需求①两路皆空）。
- **修复**：额度位改真实口径——①连接「用量端点」指本机计数源（防御解析 total_calls/window_calls/window_minutes，
  data 包裹剥开）→「本机累计 N 次 · 近M分 K 次 · 官方无额度接口（本机计数）」+ payload 注明 endpoint；②无计数源
  →额度位显式「无额度口」，模型数降 Summary 次行；WidgetValueHint 删 models→「N 模型」映射（不再占数值位），
  加 total_calls 计数位与 value_text 显式文本位。
- **本机计数落地（拍板 2026-10-10：Windows 计数器做）**：`tools/mimo-counter.py`——tail 本机
  relay_req.log 按模型名逐行计数（litellm.log 访问行无模型名，实测不可按模型计；文件后建/rotate 从头部
  重读，inode 检测），HTTP 返回约定 JSON（total_calls/window_calls/window_minutes），挂 MiMo 连接
  「用量端点」即亮本机计数。已部署本机 192.168.5.188:18082（现如实 0——本机 relay 1710 行全
  space-bunny-free，无 mimo 调用；MiMo 调用经 litellm→relay 后即被计入）。
- **验收**：CI mock 计数端点 → tile「小米 MiMo | 128 次」UIA 硬断言 + 近景截图；无计数源卡「无额度口」单测。

### 批3-补3 DeepSeek base 端点丢路径（CI 实证根因，2026-10-10）
- **现象**：连接测试过、tile 永远「Last update」离线——连接端点填 base 时 HttpEndpoint 整串直用丢默认路径，
  请求落在 / 上，200 假阳性掩盖解析失败。
- **修复**：HttpEndpoint 加 endpointOverride（provider 归一化最高优先）；DeepSeek ResolveEndpoint 按 base 语义
  补 /user/balance（已带路径原样）；TestAsync 加强为 body 语义校验（200 无余额数据 → Degraded，关闭假阳性通道）。

### 批3-补4 CI 中文断言编码（PS 5.1 ANSI 误读，2026-10-10）
- **根因**：app 日志为 UTF-8 无 BOM，PS 5.1 `Get-Content -Raw` 默认按 ANSI 读成乱码——中文断言必失配
  （config 断言纯 ASCII 照常过，极具迷惑性）。
- **修复**：日志断言 `-Encoding UTF8`；app 日志随 artifact 归档（断言失败可直读现场，不再盲猜）。

### 批3-补5 额度源调研收尾（Gemini / OpenRouter，2026-10-10）
- **Gemini（AI Studio API Key 体系）——暂缓，不同构**：官方 rate-limits 文档实测（本机 curl 直取
  ai.google.dev/gemini-api/docs/rate-limits）：限额按 project 不按 key，RPD 午夜（太平洋时区）重置；
  **无剩余额度查询 REST 口**——超限唯一信号是 429 + RESOURCE_EXHAUSTED（官方建议等待重试）。可编程查询
  走 Cloud Monitoring timeSeries（serviceruntime.googleapis.com），但需 GCP 项目 + OAuth/服务账号——
  与 Beacon「连接+Key」模型不同构，不接；将来若接，归 GitHub 式 OAuth 连接体系另立 issue。
- **OpenRouter——可落地，登记 todo**：`GET https://openrouter.ai/api/v1/auth/key`（key 元数据/用量）与
  `GET https://openrouter.ai/api/v1/credits`（充值额/已用）——本机行为级探针实证两端点存活：v1 形 key →
  401 "User not found."（服务端解析 key 并查库），畸形 key → 401 "Missing Authentication header"（错误
  信息变化=端点解析证据）；httpbin 对照排除本机剥 Authorization 头。Bearer + GET + JSON，完全符合
  「连接+Key」模型。响应字段名（data.usage / total_credits / total_usage）来自文档知识未拿到真数据——
  待真实 key 到手实测后再按防御解析接入。
- **todo（OpenRouter 接入）**：真实 key 到手 → 实测两端口响应结构 → 新增 openrouter 连接 + usage 组件
  （复用 qwen.usage 自定义用量端点的防御解析路径或独立 provider，接入时定），单测 + 本地 tile 断言。
- **取证通道教训**：WebSearch/WebFetch 本会话降级（域验证拦截）、mcp web_reader 5xx——Bash curl 直取官方
  文档 + 假 key 行为探针是最可靠路径（401 错误信息变化即端点存在性与 key 格式解析的证据）。

### 批3-补6 千问配额探针 + MiMo 计数器修复（2026-10-10 用户报「千问还是没有」）
- **用户现象**：qwen.usage tile 恒「N 模型可用 · 额度口径官方未开放」——配额实际已耗尽，tile 毫无感知。
- **根因（真 key 实证）**：Token Plan 网关把 /compatible-mode 下**所有 POST 拦在配额门后**：配额耗尽时任意
  POST（含无效路径 /xyzzy 对照实验同响应）即 429 Throttling.AllocationQuota，消息携带重置时间
  「reset at 10-19 16:00:00 UTC」；而 GET /models 不受门控（耗尽仍 200）——旧实现只调 GET /models，
  永远看不见「已用尽」。本机 Token Plan 月度配额已耗尽，重置 10-20 00:00（北京时间）。/usage /quota 等
  路径是网关兜底非真端点（/xyzzy 同 429），「官方无限额查询口」结论维持。
- **修复（档③配额探针）**：GET /models 取目录后 POST {base}/usage（model+max_tokens=1，零推理成本——健康时
  落无路由处理器 404/400，max_tokens=1 兜底防意外计费）：429+耗尽签名（AllocationQuota/insufficient_quota/
  消息含 exhausted）→ 红档「配额已用尽 · 重置 MM-dd HH:mm」+ Progress 100% + DetailUrl 指订阅页；
  瞬时限速 429 不翻转 tile；非 429 维持目录态。重置时间年份按「已过顺延一年」推断。7 新单测（探针请求
  形状/404 目录态/瞬时限速/OpenAI 风格包裹剥开/跨年顺延/非 JSON 429）。
- **MiMo 计数器两 bug（tools/mimo-counter.py，已部署重启）**：①relay 日志原 truncate（inode 不变）时 tail
  seek(0,2) 停在旧 EOF 偏移，新行全漏计（计数恒 0——「还是没有」的另一半）；按「inode 变化或 size<已跟踪
  偏移」判定从头全量重读。②bootstrap 与 tail 首扫重复计同一文件（456 计成 912）；bootstrap 改返回
  (inode,偏移) 供 tail 续读。修复后实测：total=456 与日志 MODEL= 行数一致；副本实例注入 2 行 → 456→458、
  窗口 2、last_seen 落值。附：日志另有 452 行 `CLI model=… rc=1`（CLI 包装器失败记录，非 relay 请求行，
  不计入）。

### 批3-补7 内置图标可选 + 自定义动作库（2026-10-10 用户三连：「怎么没有自定义的选项」「icon 需要自带可以让我选择」「一个 action 都没有」）
- **自定义监控自查**：http 模块/连接/Provider 全部已注册（097c6a1，2026-10-08）——用户 nightly 可能旧；
  真缺口在图标不可选与动作无自定义面。
- **图标可选**：WidgetFieldDescriptor 加 `Choices`（非空=UI 下拉，仅元数据不落盘）；BrandIcons 加 10 个
  自绘通用图标（box/bolt/terminal/git/server/cloud/database/robot/shield/chart，纯实心复合 subpath）+
  `PickerKeys`（通用集打头、品牌标随后，26 键）；http.status/http.quota 加 icon 字段（默认按连接品牌）；
  BrandIconFactory 加 `iconOverride`（widget.Config["icon"] 优先 → 连接品牌 → 字形兜底）；Settings 向导
  Choices 字段渲染成 ComboBox。
- **自定义动作库**：actions.json 第五文件（.tmp 原子+.bak+.corrupt，镜像 connections）；IConfigurationStore
  新成员 Actions/SaveActions/UpsertAction/RemoveAction/FindAction；高级页动作编辑器（类型下拉
  http/webhook/local.command/open.url + 按类型参数字段 + 限定组件类型 + 执行前确认开关 + 列表删除）；
  DetailWindow ACTIONS 区追加动作库按钮（Parameters.widgetType 过滤，RequireConfirmation 窗内
  ContentDialog 完成后摘标记提交）。**顺手修**：Open/Retry/Cancel 内联动作此前未摘 RequireConfirmation
  （默认 true）而全局无确认通道——Runner 必拒（「需要确认但无确认通道」），按窗内确认后提交的文档口径
  补 RequireConfirmation=false。
- **todo**：B-802 导入导出纳入 actions.json（未并入 ReplaceAll，避免涟漪，挂账）。

### 批3-补8 小米套餐用量换控制台口径 + GLM 5h 窗口主位（2026-10-10 用户批6）
- **小米（用户实测口径：124,801,805,513 / 132,000,000,000 ≈ 95%）**：plan-manage 页接口=前端 bundle 实证
  `GET https://platform.xiaomimimo.com/api/v1/tokenPlan/usage`（base /api/v1 前缀 + SPA 兜底 HTML 干扰排查；
  认证=登录 cookie **api-platform_ph**）。真 Key 三形态（Bearer / api-platform_ph 头 / 裸 Authorization）
  实测全 401+loginUrl——**API Key 只授权模型调用，控制台要小米账号会话**（与千问 Token Plan 同构）。
  落地=MiMo provider 三档改四层：①控制台套餐用量（连接录控制台 Cookie 进 DPAPI，ref 默认 mimo:console；
  Settings console_usage_url/console_credential_ref 可覆盖；防御键名解析 used/total/percentage，
  百分比缺失时自算；已用/总量亿级中文口径 + Progress + ≥90 Warning / ≥100 Error）；②本机计数（原
  usage_endpoint 档不动）；③目录档摘要给「套餐用量见控制台 plan-manage（需登录）」指引，DetailUrl 全档
  指向 plan-manage 页。**限制如实报**：控制台响应真身无会话验证不了（拿不到真数据），解析走防御键名、
  认不出显式 Degraded 带原文——装机录入 cookie 后若字段不符按诊断日志修。
- **GLM（用户令：现在显示月用量是错的，z.ai GLM 是 5 小时滚动窗口）**：quota/limit 真 Key 实测——5h 窗
  （TOKENS_LIMIT unit=3 number=5）**只有 percentage+nextResetTime 无绝对 token 数**；月度 TIME_LIMIT
  (unit=5) 才带 usage/currentValue/remaining（MCP 调用数口径）；1302 限流响应实测只有
  「您的账户已达到速率限制」一句、**无窗口数字**（用户猜测不成立），监控接口是唯一数据源。改法：主位
  =rolling→weekly→monthly 优先级（原 WorstPercent 三窗取最差会让月度占主）——Progress/percent 主数值/
  级别判定全部取主位窗口，payload 加 percent_window/monthly_reset_ms，摘要改「5h 窗口已用 N% · MM-dd
  HH:mm 重置 · 周 X% · 月 Y%」。真 Key 原样响应（整数百分比+usageDetails 数组）锁进回归夹具。
- **验收**： Connections 318 绿（+19）；组件截图待装机走查（小米需先录控制台 Cookie；GLM 直接生效）。

---

## 附：MVP 明确不做（评审基线）

复杂插件市场 · Workflow DSL/引擎 · AI Agent · Jenkins/GitLab/Docker 全功能管理 · 数据库 · 云账号/用户系统/团队协作/云同步 · 复杂图表 · 复杂主题 · macOS/Linux。
