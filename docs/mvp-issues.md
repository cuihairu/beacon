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
    - CI 已代验进程半（2026-10-12 批3-补50 场景 E1）：二次启动进程数恒 1 + 副实例退出日志硬断言；唤起气泡是 OS Toast 面不可脚本取证——待 Windows 装机。
  - [x] 关闭所有窗口后进程驻留托盘，Exit 才退出
    - CI 已代验（2026-10-12 批3-补50 场景 E2/E3）：设置窗 WM_CLOSE 后进程驻留 + 托盘菜单 Exit 后进程退出（菜单驱动失败时降级 warning 转装机）。

### B-102 L1 Status Capsule 窗口
- **依赖**：B-101
- **内容**：小型 Topmost 无边框窗口（此时显示占位聚合 `🟢0 🟡0 🔴0`）；可拖动、位置持久化、PerMonitorV2 DPI；不进任务栏/Alt-Tab。
- **验收**:
  - [ ] 重启后胶囊回到上次位置；跨 DPI 拖动无尺寸错乱
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
    - 位置还原半已由场景 R CI 代验（2026-10-13 run 38303355372：预设 %APPDATA%\Beacon\shell-state.json capsuleLayout（TopLeft，offsetDips 40,40）→ 启动落位 40,40 ±4px → 重启落位一致，与场景 H tile 位置还原同口径）；拖动保存与跨 DPI 仍待装机。

### B-103 全局热键 Ctrl+Alt+B
- **依赖**：B-101
- **内容**：RegisterHotKey 显示/隐藏 Quick Panel（本阶段可为占位窗口）；热键写入 config.json，可修改；冲突时提示。
- **验收**：
  - [x] 任意前台应用下热键生效；可在设置改键并持久化
    - CI 已代验改键持久化半（2026-10-13，run 38297713667，场景 N）：设置页热键输入框 Ctrl+Alt+B→Ctrl+Alt+C 落盘 config.json；重启后新键注册无失败日志（RegisterHotKey/Invalid hotkey string 双断言）；WM_HOTKEY 直驱触发面板出现（应用侧触发链）。
    - 勾账（2026-10-13 patrol-B）：应用侧链全量齐备（注册成功 K1 + 持久化 N + WM_HOTKEY 同入口触发）；「任意前台生效」即 RegisterHotKey 的 OS 派发契约——注册成功后 OS 必达，应用无按前台分支代码，剩余仅为真实键盘和弦的顺手复核（SendInput 注入 CI 会话不可达）。

### B-104 开机自启
- **依赖**：B-101
- **内容**：HKCU Run 键写入/移除；Settings 开关；自启时静默（仅托盘+胶囊）。
- **验收**：
  - [x] 开关即时生效（重启验证）；卸载清理
    - CI 已代验卸载半（场景 J）与开机自启注册表半（场景 M，2026-10-13 run 38287590006：launchOnStartup true/false 双向断言 HKCU Run\Beacon 写入/清值）；「重启验证」半即 OS 登录时执行 HKCU Run 的契约，Beacon 无登录期代码，真实重启留装机顺手复核。

### B-105 崩溃兜底与日志框架
- **依赖**：B-101
- **内容**：全局异常处理 → `%AppData%\Beacon\logs` 滚动日志 + 托盘气泡提示，不闪退；ILogger 接入。
- **验收**：
  - [x] 人为抛异常：进程存活、日志含堆栈、无僵尸窗口
    - 已由 CI 代验（2026-10-14 勾账）：外部故障半场景 T2（断网 75s 进程存活 + 堆栈日志）+ 注入半场景 V3（38459944253 红出裸 TryEnqueue 缺口 → DispatcherQueueGuard 23 处接管，patrol-20261014-A3；38494525200 复证：崩溃注入 → 日志堆栈留痕 + 进程存活 + 零 #32770 + tile 仍在 + 刷新循环续跑）。代码路径：App.xaml.cs OnLaunched catch → LogCritical + CrashLog.Alert。

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
  - [x] 无网启动：界面有数据且标注 Last update，随后自动刷新
    - 状态（2026-10-09）：存储/宿主层单测已过（JsonCacheStoreTests：SaveThenLoad_RoundTripsAcrossInstances / CorruptCacheFile_TreatedAsEmpty；WidgetHostTests 缓存优先水合）；「界面有数据」为运行时表现——待 Windows 装机。
    - 状态（2026-10-10）：断网→标注→恢复全周期单测收口——WidgetHostTests.OfflineThenRecovery_StaleAnnotationPreserved_FreshReplaces：断网后陈旧态重发布且 FetchedAt 保持原值（Last update 不刷新成失败时刻）/Summary 保留旧数据；恢复后自动刷新 IsStale=false、缓存同步替换。
    - CI 已代验（2026-10-12 批3-补50 场景 F1-F4）：断网启动有窗有缓存数据 + Last update 45s 硬断言 + 恢复后 90s 自愈百分比读数。

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
    - 状态（2026-10-10）：假 Handler 全分支单测收口——GitHubConnectionProviderTests 8 例（200→Healthy 走 /rate_limit、401→Unauthorized、网络断→Offline、缺 PAT 短路→Unauthorized 带录入指引、403 限流耗尽→Offline、403 权限→Unauthorized、超时→Offline、缓存键随 token 换客户端）；真实 PAT 手动验证——待 Windows 装机。
    - 修复（2026-10-10）两处真 bug：①缓存键只用连接 Id——重录 PAT/改端点后连接测试与刷新永远拿旧客户端（旧 Authorization/ETag 表），不重启不生效；键改含 endpoint+token 后自然失效。②缺凭据不短路——/rate_limit 匿名也 200，空 PAT 误报「连接正常」；现先查 Secrets 空则 Unauthorized+指引，请求不发。

### B-302 PR Widget Provider
- **依赖**：B-301
- **内容**：`github.pull_requests`：指定仓库（可多）open PR 数与列表；Severity 映射（有 review-requested/红 CI 的 PR → Warning，可配置）；refreshTier=pr。
- **验收**：
  - [ ] 夹具单测：计数/映射正确（已由 GitHubPullRequestsProviderTests 18 例代验）；真实仓库手动验证（待装机：需真实 PAT 与真实仓库数据）
    - 状态（2026-10-09）：夹具单测已过（GitHubPullRequestsProviderTests 6 例：计数/Severity 映射）；真实仓库手动验证——待 Windows 装机。
    - 状态（2026-10-10）：夹具补至 10 例（单数摘要「1 open PR · 1 review needed」/草稿带 reviewer 不豁免/缺 requested_reviewers 字段按 0/坏 JSON→Degraded 带解析原因）。
    - 状态（2026-10-10 晚）：红 CI 判据收口——有状态设计落地（`warnOnRedCi`，默认关）：按 PR 记忆
      (head sha → combined status state)，终态跨刷新复用（稳态零额外请求），仅探测 sha 变化或非终态
      （pending）的 PR；pulls 口 304 期间 pending→failure 翻转按记忆续探仍可回报，全终态才返回 null；
      默认关因首刷每 PR 一请求（大仓库代价高），描述符字段注明。夹具 10→18 例
      （默认关零额外请求/新 sha 探测红→Warning/终态复用/pending 翻红/304 期间翻转可回报/304 全终态 null/sha 换重查/坏 status JSON→Degraded）。
      零成本备选已证死路（2026-10-10 官方文档核实）：list /pulls 响应项不含 mergeable_state（仅单 PR 口带）。
      真实仓库手动验证——待 Windows 装机。

### B-303 Actions(CI) Widget Provider
- **依赖**：B-301
- **内容**：`github.actions.runs`：workflow×branch 最新 run → LifecycleState + Severity（Failed→Error，Running→Info+进度，超 15min→Warning）、时长、conclusion；refreshTier=ci。
- **验收**：
  - [ ] 状态映射表驱动单测（已由 GitHubActionsProviderTests 9 例代验）；真实 workflow 手动验证（成功/失败/运行中）（待装机：需真实 PAT 与真实仓库 workflow 状态流）
    - 状态（2026-10-09）：表驱动单测已过（GitHubActionsProviderTests：Lifecycle/Severity 映射含 Failed→Error、Running→进度、超 15min→Warning）；真实 workflow 手动验证——待 Windows 装机。
    - 状态（2026-10-10）：端到端补至 8 例（运行中→Running/Info 且 Summary 不拼时长、16min 疑似卡住→Warning、success→Success 且时长「2m00s」格式、坏 JSON→Degraded 带解析原因）；真实 workflow 手动验证——待 Windows 装机。

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
  - [x] 路由/确认/超时单测；失败在 UI 有可见反馈
    - 状态（2026-10-09）：单测侧已过——ActionRunnerTests：Routes_ToExecutorByType / Confirmation_Rejected_SkipsExecutor / Confirmation_Accepted_Executes / Timeout_CancelsExecutor_ReturnsFailure / ExecutorThrows_FailsGracefully / UserCancellation_ReturnsCancelled。
    - CI 已代验（2026-10-14 场景 U4/U5，run 38434164526）：mock 翻 500 后 L3 结果文本失败反馈硬断言 + 成功反馈「已重跑 run 12345」同链对照。

### B-402 Open URL Action
- **依赖**：B-401
- **内容**：`open.url`：系统默认浏览器打开（PR/Actions/Logs 链接模板支持 `{owner}/{repo}/{runId}` 占位）。
- **验收**：
  - [ ] 模板渲染单测（已由 TemplateRendererTests 11 例代验）；手动验证浏览器打开正确 URL（待装机：默认浏览器属用户环境）
    - 状态（2026-10-09）：模板单测已过——OpenUrlExecutorTests：Renders_Template_FromVarsAndPayload / Payload_OverridesVars_OnConflict / MissingPlaceholder_FailsClosed；浏览器打开——待 Windows 装机。

### B-403 GitHub Workflow Dispatch / Retry / Cancel
- **依赖**：B-301、B-401
- **内容**：`gh.workflow_dispatch` 触发运行；Retry（rerun failed jobs）、Cancel（cancel run）封装为三个 Action；触发后创建短轮询跟踪至终态（§7 workflow 行）。
- **验收**：
  - [ ] API 封装单测（假 Handler）（已由 GitHubApiClientTests 10 例 + GitHubWorkflowExecutorTests 6 例代验）；真实仓库手动：运行中到终态全程可在 Beacon 看到（待装机：需真实仓库 workflow 触发与真实 PAT）
    - 状态（2026-10-09）：API 封装单测已过（GitHubWorkflowExecutorTests：dispatch/rerun/cancel 三执行器假 Handler）；真实仓库全程可见——待 Windows 装机。

### B-404 Local Command Action
- **依赖**：B-401
- **内容**：`local.command`：powershell/cmd/exe；WorkingDirectory/环境变量/超时；捕获输出写缓存供「Open Logs」；默认执行前确认。
- **验收**：
  - [x] 超时终止生效；输出落盘；确认弹窗可关（按 Action 配置）
    - 状态（2026-10-09）：单测侧已过——LocalCommandExecutorTests：Timeout_KillsProcess_AndStillWritesLog / RunsRealCommand_CapturesOutput_WritesLogFile；确认开关配置层已测（ActionRunnerTests NoConfirmationNeeded_ExecutesWithoutHandler）。
    - CI 已代验（2026-10-14 场景 U3/U4，run 38434164526）：Retry 触发 ContentDialog 可关（取消后 mock 零 POST 负证）+ 确认全链走通（POST 回执 marker + L3 结果文本）。

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
    - 自愈半已由 CI 代验（2026-10-14，38494525200 场景 F5t：预置删除 HKCU AppUserModelId 注册键 → Initialize() 重建硬断言；toolkit 7.1.3 注册表机制，不建 .lnk）；点击/激活路由与横幅呈现属 OS Toast 面，待装机。

### B-502 NotificationEngine + 默认规则
- **依赖**：B-203、B-207、B-501
- **内容**：规则求值（when: widgetType/severityAtLeast → then: toast/sound/dockColor）；冷却去重；内置规则：CI Failed→Toast、运行>15min→Warning；通知记录滚动 200 条 + 已读/未读。
- **验收**：
  - [x] 规则引擎表驱动单测（含冷却）；手动：注入 Failed 状态收到且仅收到一条 Toast
    - 状态（2026-10-09）：单测侧已过——NotificationEngineTests：DefaultThreshold_SeverityBand_TableDriven / SeverityAtMost_Band_WarningOnlyRule_IgnoresError / Crossing_SameAlertPeriod_FiresOnce_RearmsAfterRecovery（冷却/去重）。
    - 勾账（2026-10-13 patrol-B）：「恰一条」Beacon 侧全链齐备——引擎冷却去重单测 + 场景 F5 真实链（断网降级通知恰 1 条，notifications.json 硬断言）；ToastNotificationService.Show 为直线发送代码（delivery.Toast 门控 → builder.Show()，失败有「Toast 发送失败」LogError 可观测），发送后呈现（横幅/操作中心）属 OS 面无应用分支代码；装机时顺手目检即可。

### B-503 托盘着色与菜单
- **依赖**：B-205、B-502
- **内容**：托盘图标随 overall Severity 着色（绿/黄/红/灰）；托盘菜单含常用 Action 与 Notifications 入口。
- **验收**：
  - [ ] 聚合状态变化 ≤1 个刷新周期内反映到托盘
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）：托盘图标像素在通知区域不可脚本取证（全新会话图标落溢出折叠区，需注册表 IsPromoted+重启 Explorer 才能取证，版本强依赖不做）。
    - tile 层同链已由 CI 代验（2026-10-12 批3-补50 场景 G7a）：38% 到 95% 75s 内反映。

### B-504 胶囊接入真实聚合
- **依赖**：B-205、B-102
- **内容**：胶囊显示真实分级计数；Offline 横幅（Last update）；点击打开 L2。
- **验收**：
  - [x] 断网显示 Offline+时间；计数与实际 Widget 状态一致
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
    - 已由 CI 代验三面（2026-10-13 patrol-20261013-A2）：tile 半场景 F2（断网 45s 内「Last update」离线标注）；面板半场景 O（run 38299509997：all clear ↔ 0 warn · 1 error 三态往返，chip 计数随状态）；胶囊半场景 Q（run 38303355372：2 ok ↔ 1 ok · 1 err ↔ 断网 Last update 横幅 + stale 抬档 2 warn，UIA 文本断言不碰像素；胶囊观感仍可装机复核）。

---

## P6 Quick Panel / Detail（M4）

### B-601 Quick Panel 窗口（L2）
- **依赖**：B-504
- **内容**：Flyout 出现在胶囊/托盘附近；可激活（ESC 关、失焦关、Tab 导航）；不进任务栏。
- **验收**：
  - [ ] 打开 <100ms（缓存优先，无网络等待白屏）；键盘可完整操作关闭
    - CI 已代验两半（2026-10-13 patrol-B 场景 K2/K3）：断网 Stop mock 后热键开面板、缓存百分比读数在即无白屏 + Esc 关闭优先、热键 toggle 硬断言面板可关；Esc 键盘注入与 100ms 精确计时——待 Windows 装机。
  - 通知中心半区（2026-10-09 补齐）：NOTIFICATIONS 区渲染 `NotificationEngine.Records`（notifications.json 落盘水合，跨重启带出历史）——未读徽标/单击标已读/全部已读；此前 App 层零处消费 Records，仅 Recent Events 状态流。

### B-602 Overview + Recent Events
- **依赖**：B-601、B-203
- **内容**：按来源计数区（GitHub/CI/…）；Recent Events 列表（状态变化流，与通知同源）；列表项右键 Pin to desktop（写入 widgets.json，L7 阶段消费）。
- **验收**：
  - [ ] 计数/事件与状态一致；右键 Pin 落库（本阶段无 UI 效果）
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。
    - 计数半已由场景 O CI 代验（2026-10-13 run 38299509997：面板摘要/chip 三态往返与 Widget 状态一致）。
    - Recent 事件流一致半已由场景 S CI 代验（2026-10-13 run 38315240633：x:Name→AutomationId 结构化树遍历分行取证——RecentList 事件行随状态翻转（95% 行新于 38% 行）、AttentionList 同现同摘要（两区同源）、恢复后状态区摘除而事件流保留（当前 vs 历史））。随行修复：OnStateChanged 原不重建状态区（唯一触发点 OnWidgetsInvalidated），面板常驻期间「需要关注」恒为开面板前旧影——已补现场重建。
    - 右键 Pin 交互仍待装机（ContextFlyout 需真实右键，无 UIA 入口）。

### B-603 Detail Window（L3）
- **依赖**：B-601、B-401
- **内容**：单对象视图（状态/仓库/分支/时长/阶段/错误摘要）+ Action 区（Open/Retry/Cancel/Open Logs，按 Widget 能力显隐）。
- **验收**：
  - [x] 从 L2 事件一键打开对应详情；[Retry] 全链路可用
    - 2026-10-13 复核：L3 开启唯一入口=ItemClick（DetailRequested 全仓唯一订阅在 App.xaml.cs，OnStateItemClick/OnRecentItemClick 均 ItemClick 驱动），UIA 无法可靠触发 ItemClick（WinUI ListViewItem 只暴露 SelectionItem/ScrollItem pattern）。
    - CI 已代验（2026-10-14 场景 S4 + U1-U5）：详情半 S4（run 38338077989，应用侧补 InvokeableListItem 与 ItemClick 同链，RecentList 首行 UIA Invoke 打开 L3 详情窗）+ Retry 半 U1-U5（run 38434164526：失败 run 进需要关注、L3 数据行与 Retry 按钮在窗、确认弹窗取消零 POST 负证、确认全链 POST 回执 marker、500 失败反馈）；面板行点击的真实手势留装机顺手复核。

---

## P7 L0 悬浮组件（M5，RFC §6.2）

### B-701 PinnedHostWindow：单窗口多 tile 宿主
- **依赖**：B-102、B-304
- **内容**：每显示器一个透明置顶宿主（Topmost + WS_EX_NOACTIVATE + 空白点击穿透）；内部 tile 化布局渲染全部 pinned widgets；Composition 渲染，状态变化只更新对应 tile 视觉。（扩展已落地：设置「悬浮形态」可切换为独立悬浮框——每钉选组件一窗、桌面任意拖放、位置按组件记 `PinLayout.FloatingX/Y`，见 positioning §8。）
- **验收**：
  - [ ] 点击 tile 不打断当前应用焦点；空白区域点击落到桌面
    - CI 已代验刷新半（2026-10-12 批3-补50 场景 H1）：mock 翻档期间前台窗句柄不变硬断言；点击 tile 打开详情属用户意图合法夺焦，空白点击穿透——待 Windows 装机。
  - [x] 两个 tile 状态各自独立更新；窗口不进任务栏/Alt-Tab
    - CI 已代验（2026-10-12 批3-补50 场景 G7a + H）：GLM 95% 时 DeepSeek 读数不动（两 tile 独立）+ 每 tile WS_EX_TOOLWINDOW 置位、WS_EX_APPWINDOW 清位硬断言。
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
    - CI 已代验位置与收起态半（2026-10-12/13 场景 H 两轮重启精确落位 + 场景 P run 38303355372：collapsed=true 细条宽启动、重启仍细条、展开 260px 两态可辨）；拔显示器物理事件——待 Windows 装机。
  - [ ] 跨 DPI 显示器拖放尺寸正确；分辨率变小 tile 被回收到可见区
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-704 L0 离线/错误降级
- **依赖**：B-701、B-207
- **内容**：tile 灰空心灯 + `Last update HH:mm`（tooltip）；绝不弹异常；恢复自动复位；isStale 标注。
- **验收**：
  - [x] 断网全流程无任何弹窗；恢复后 tile 自愈
    - CI 已代验（2026-10-12 批3-补50 场景 F3/F4）：断网期间 Beacon 进程名下 #32770 对话框数=0 + mock 恢复 90s 内百分比读数回归。

### B-705 L0 行为铁律检查（只看不弹）
- **依赖**：B-702、B-502
- **内容**：验收性任务：确认 L0 不产生 Toast/声音/弹窗、不承载确认类 Action、Critical 仅灯脉冲（≤1Hz）；Alert 全部由 NotificationEngine 通道表达。
- **验收**：
  - [ ] 注入 Critical 状态：L0 仅变色+脉冲，无打断；Toast 照常由通知引擎发出
    - CI 已代验 L0 变色+脉冲半（2026-10-14 场景 V run 38459944253：V1 Critical 默认色 #ff3b30 52px 硬断言 + Error 负控 0px、V2 脉冲帧差分 85px；注入面=Core DebugInjection）；「无打断」观感与 Toast 照常发出的通知引擎链——待 Windows 装机。
  - [ ] 手动清单（RFC §6.2.7 四条）逐条通过并记录
    - 待 Windows 装机（2026-10-09 标注；本机 Linux 不代验）。

### B-706 状态色用户配置（级别/Widget 两级覆盖）
- **依赖**：B-701、B-201..207（config/widgets 存储）
- **内容**（RFC §4.1/§6.2.8）：五级 + Offline 默认色表；`config.json appearance.severityColors` 按级别全局覆盖、`widgets.json colorOverride` 按 Widget 覆盖（Widget > 级别 > 默认，非法值回退默认）；`SeverityPalette` 改为读配置，L0 tile/L1 胶囊/L2-L3 状态点/托盘图标同源换色；改色即时生效（事件通知重绘，无需重启）。
- **验收**：
  - [ ] 设置改级别色后 L0 tile、胶囊、托盘图标同步变色
    - CI 已代验 tile 半（2026-10-12 批3-补50 场景 G4）：设置页改 #ff8800 后 config 落盘 + tile 像素即时变橙；胶囊与托盘图标像素不可脚本取证——待 Windows 装机。
  - [x] 某 Widget 设 colorOverride 后仅该 tile 变色，优先级正确
    - CI 已代验（2026-10-12 批3-补50 场景 G1）：GLM 设 #ff00ff 品红 ≥8px 硬断言 + DeepSeek 零渗 ≤4px。
  - [x] 非法颜色值回退默认色不崩溃；重启后保持
    - CI 已代验（2026-10-12 批3-补50 场景 G2 两轮启动）：非法值进程存活 + 品红 ≤4px + 默认绿 ≥8px 硬断言。

### B-707 动效系统（状态过渡/呼吸灯/脉冲/滑入）
- **依赖**：B-701、B-706
- **内容**（RFC §6.2.8）：Composition 属性动画五族——状态变色交叉过渡（~200ms×intensity）、提醒闪烁（状态变化后短促 2 次）、呼吸灯（full 档常驻）、Critical 脉冲（reduced 档默认，≤1Hz×intensity）、tile 滑入；`motion.mode=full/reduced/off` 三档 + `intensity` 0.5–2.0；窗口不可见/收起态暂停动画循环；动画只作用于对应 tile 视觉层不重建整窗。
- **验收**：
  - [x] Success→Error 状态变化有平滑变色过渡（reduced 档）
    - CI 代验（2026-10-13，run 38284623093，场景 L）：三轮翻档连拍均取到渐入中间态帧（与两端差分 ≥8px）；同色 tick 不闪灯（L1 连拍灯恒在）。渐入观感最终裁量仍可装机复核。
  - [ ] Critical 脉冲默认开；off 档全静止；full 档呼吸灯可见且强度可调
    - CI 已代验四半（场景 G7b off 三帧差分 ≤12px 全静止 + G7c full 帧差分 32px 呼吸可见，run 38274507626 + G6 强度 1.5 落盘 + 场景 V2 Critical 脉冲帧差分 85px，run 38459944253）；观感与强度可调的最终裁量——待 Windows 装机。
  - [ ] 动效不抢焦点、不发声；隐藏/收起时无动画循环（功耗）
    - CI 已代验焦点半（2026-10-12 批3-补50 场景 H1）：翻档期间前台窗句柄不变硬断言；发声与隐藏期动画循环——待 Windows 装机。

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
  - [x] 调色/动效改动即时预览并持久化，重启生效
    - CI 已代验（2026-10-12 批3-补50 场景 G4/G6/G6b）：设置页改色即时变橙 + config 落盘 + 档位/强度落盘 + 重启后 UIA IsSelected 恢复选中态。
  - [x] 重置默认一键还原；off 档预览即全静止
    - CI 已代验（2026-10-12 批3-补50 场景 G5/G7b）：重置后 severityColors 清空 + tile 即时回默认绿 + off 档三帧差分 ≤12px 全静止。

---

## 2026-10-10 拍板记录
- **B-002 CI required check**：维持直推现状、不设 required check（设了会挡 daily-build 自身推送）；如需再议 PR 工作流再启。
- **状态胶囊默认值**：维持当前实现不改（`ShowCapsule=false` 默认关，装机后设置页可开）。
- **装机走查引导清单**：44 项验收按 nightly 资产装→验→回执格式整理为 [docs/acceptance-checklist.md](acceptance-checklist.md)，随批落仓。
- **主题/悬浮形态「重启生效」改即时**：不通过，维持重启生效（2026-10-10 用户令自行拍板）。理由：
  ①验收基线已把重启生效列为预期（acceptance-B803-e2e-walkthrough.md「即刻生效（主题重启生效属预期）」）；
  ②两处均为装机时一次性设置，诚实标注已就位（设置页「主题（重启生效）」+ 配置参考逐项注明生效时机），
  非静默失效；③主题即时的真代价 = 动色源真相（ThemeColors 静态读 Application.Current.RequestedTheme，
  属启动口径）+ 逐窗根 RequestedTheme + Acrylic 背景重挂 + 全悬浮面重渲染，半主题化风险（背景/标题栏
  错色）CI 抓不到，只能装机肉眼验收；④悬浮形态即时 = L0 宿主窗重建，该路径有前科（白板残留批，
  现双宿主各自兜异常即是补丁），一次失误 = tile 消失到重启；⑤与本仓拍板记录保守取向一致
  （胶囊默认关维持/直推不设 check/数量悬浮窗默认关）。要推翻：直接改本条，实现入口 =
  App.ApplySettingsEffects + ThemeColors 色源 + SettingsWindow 标签去「重启生效」。
  **2026-10-11 推翻记录**：「数量悬浮窗默认关」已按用户令翻转为**默认开**（批3-补24；一次性强制置关迁移撤除）；
  「胶囊默认关维持」与「直推不设 check」两项维持不变（2026-10-11 用户令重申胶囊默认关）。
- **evidence-before 分支处置**：`git branch -D` 删除（2026-10-10 用户令）。该分支为 2026-10-09 晚的
  rebase 前安全快照（3 commit：visual-evidence 扩展/BOM/automation ids），`git cherry origin/main
  evidence-before` 全 `-` 证实补丁内容已全部等价存在于 main，无未合并工作；删除前已逐 commit 核对
  （daily-build.yml 场景 C/D 描述、ps1 BOM、automation ids 均在 main）。

### 2026-10-11 总方案四项拍板（用户授权「按你的权限自行拍板，选有据可依的一边」）
1. **层级编号**：**RFC-001 编号为产品唯一口径**（L0=钉选悬浮组件 / L1=胶囊 / L2=Quick Panel / L3=详情）。
   依据：RFC 编号贯穿代码注释、mvp-issues、acceptance、configuration 全仓文档与实现，master-plan 一文难敌全仓；
   master-plan §3 的 L0/L1/L2 仅为原文行文，头部对照表已注明映射（本文 L0=胶囊→RFC L1，L1=面板→RFC L2，L2=详情→RFC L3）。
   零代码改动。
2. **悬浮组件默认**：**维持现行默认（2026-10-11 用户令优先于 master-plan 原文 §3.6）**——数量悬浮窗默认开 +
   胶囊默认关 + 默认形态=单宿主面板（panel）。依据：用户令是直接、更晚、更具体的产品指令；master-plan 自身
   Small by default 精神未被破坏（panel 形态默认单窗、独立悬浮框须显式切换）。master-plan §3.6 已按此改写。
3. **local.command**：**保留现状，记例外**。依据：master-plan 自身第 1 条要求「复用现有成熟实现」、§5.5
   「实际接口以仓库现有实现为准」；已落地实现有默认确认弹窗 + 设置明文可见 + 超时捕获（RFC §13），且是
   mission 原案 [Build][Test][Deploy] 快捷动作的承载——它不是「脚本执行平台」（无编排/无批量/无编辑器）。
   master-plan §7.1 加例外注；不再扩展脚本能力，新操作类型按「来源平台简单明确操作」原则评审。
4. **面板三分区**：**采纳 master-plan §3.2，落地实现**（批3-补27）：NEEDS ATTENTION / RUNNING /
   RECENT EVENTS（即「最近状态」），OVERVIEW 与通知中心保留；宽 320→380（建议带 360–420 取中）。

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
- **todo（已销账）**：B-802 导入导出纳入 actions.json——已由 1e0efc0 收口（Export/Import 带 Actions、ReplaceAll 收 actions 参数、ImportExportTests 往返+旧包兼容+整体替换语义）。

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

### 批3-补9 独立悬浮框进度条消失 + 快捷面板底部裁切修复（2026-10-10 用户批7）
- **独立悬浮框进度条完全不显示（用户批7①，疑截断）**：FloatingTileWindow 窗口写死 32 DIP 高，
  PinTile 进度条走「1* 主行 + RowSpacing 2 + Auto 条行」双行布局——32 DIP 里给 2px 条留零余量，
  Auto 行/间距/客户区亚像素取整任一吃掉 1-2px 条即整个不可见。改**叠放**：单 Grid 双子（主行 +
  贴底 bar，后加者上层），总高恒 32 不随条显隐变化；条抬离底边 1 DIP，裁切余量吃 Margin 不吃条；
  主内容竖直居中与 2px 条互不碰撞。单宿主面板与独立悬浮框共用 PinTile，一处修两形态同生效。
- **快捷面板右下角内容被切（用户批7②，菜单底部显示不全）**：QuickPanel RECENT EVENTS 区是
  StackPanel（Grid * 行内）套 ListView——纵向 StackPanel 以**无限高**量测子元素，ListView 撑到全部
  内容高（50 条事件）、溢出窗底被窗口裁掉，且 ListView 自认为全可见、**滚动条永不出现**，底部条目
  够不着。对照组：通知列表有 MaxHeight=118 封顶故无恙。改纵向 Grid（Auto 标签 + * 列表 + RowSpacing 6），
  列表压进剩余高度走内部滚动，底部 14 DIP Border Padding 完整可见。托盘菜单为 Win32 TrackPopupMenu
  （系统绘制自动翻边）已排除。
- **验收**：修复均为 App 层布局（本地不可编译，CI windows-latest 为准）；截图验收（悬浮框进度条可见 +
  面板右下完整）待装机走查。

### 批3-补10 界面透明度统一来源 + CI 告警清零（2026-10-10 用户批8/告警清理令）
- **透明度调整没有生效（批8，写入/读取链排查）**：写入链正常（slider ValueChanged→SaveGeneral→
  SaveApp→SettingsApplied→App.ApplySettingsEffects），断点在**应用链只到胶囊**——`capsule.ApplyOpacity`
  是唯一消费者，而胶囊默认关（2026-10-09 拍板）=调透明度肉眼零变化。修法：uiOpacity 统一来源四类悬浮面
  同值——胶囊（原有）+宿主面板 PinnedHostWindow.ApplyOpacity（挂 _tilePanel）+独立悬浮框
  FloatingTileHost.ApplyOpacity（存值广播+ReloadTiles 新窗补挂；**挂包装 Grid 而非 tile 根**——
  MotionEngine Flash/SlideIn 直接动画 tile 根 Opacity，同元素双写互相覆盖）+快捷面板
  QuickPanelWindow.ApplyOpacity。启动初始化+设置变更实时生效双路接线（App.xaml.cs）。持久化本就落
  config.json（重启保持）。**同类病排查**：主题/悬浮形态均有「重启生效」标注（诚实 UI 非静默失效），
  其余设置项（热键/自启/胶囊/数量悬浮窗/检查频率/外观色/动效）全部实时接线；字号设置不存在。
  （更正：本条初稿写「通知规则全部实时接线」不成立——NotificationEngine 实际未接 rulesProvider，
  详见批3-补12。）
- **CI 告警清零（告警清理令）**：①CS8604（SettingsWindow:529 TagOf(_actionTypeBox)）——TagOf 签名改
  `ComboBox?` 空条件兜底（页面未构建/无选中返回 null，调用方本就 `??` 兜底），全调用点一次覆盖；
  ②CS0414 ×3（_githubEnabled/_httpEnabled/_bigmodelEnabled）——**实证非未接线**：每类连接整体开关功能
  已由局部 toggle + `_moduleToggles` 字典 + `SetConnectionTypeEnabled`（BeaconRuntime:183）完整承载，
  三字段是早期 per-类型三字段设计的残留死声明（连赋值都没有，`= null!` 初始器触发 CS0414），删除；
  与透明度 bug 不同病（透明度=值只到一个消费者；这三个=字段被通用机制取代）。
- **验收**：告警归零编译日志=CI windows-latest（本地不可编译）；透明度截图（肉眼可见变化）+重启保持
  =装机走查（保存链 config.json 已有，四窗同值代码已接线）。

### 批3-补11 方舟额度用尽显示修复 + 三家措辞对齐（2026-10-10 用户批9）
- **病灶（用户实测：月/周额度尽，卡片却显示有额度）**：ArkUsageProvider.ToState 主位恒 5h session 窗
  （bug 批3 修复的副作用）——周/月窗 100% 尽、新 5h 窗刚重置（低%）时，主数值/级别全按新 5h 窗算，
  卡片显示健康。修法：ToState 加**尽窗优先级**——任一窗已用 ≥100% 即整卡按「已尽」展示：
  Error + Lifecycle.Failed + 进度条满格 + 摘要「{head} · 额度用尽（{窗名}，MM-dd HH:mm 重置）」
  （窗名 5h 窗/周/月；多窗尽按重置时间最早取首窗命名，全部尽窗列 payload["exhausted_windows"]）；
  无重置时间显示「待重置」。各窗余量仍留 payload 供 L3 详情。已尽恒 Error 不受阈值配置降级。
- **429 耗尽型特判（数据源接真实错误码）**：FetchUsageAsync 对 429 检查响应体——含配额耗尽码
  （AccountQuotaExceeded/QuotaExceeded/AllocationQuota/insufficient_quota，忽略大小写）即按该窗已尽
  返回（窗 100% + 重置时间），连接保持健康不落 Degraded；窗名按 weekly/monthly/session 关键字
  （含中文 周/月/5小时）解析，认不出 → ArkPlanUsage.ExhaustedNote 兜底（通用「额度用尽（服务端返回
  {码}，重置时间未知）」卡）。重置时间防御解析 TryParseResetSeconds：JSON 键嵌套递归
  （ResetTimestamp/resetTime/nextResetTime/ResetAt 等 8 键，毫秒/秒归一+ISO 字符串），
  非 JSON 正文才走正则（ISO → 毫秒 epoch → 秒 epoch；JSON 有效但无键 → null 防误取 RequestTime）。
  **纯限流型 429（无耗尽码，如 RPM 限速）不翻转**——瞬时限速≠用尽（同千问 ParseQuotaExhausted 先例），
  维持 4xx=Degraded。④恢复口径：每轮刷新重推——重置后用量接口回 <100% 或 429 停止即自动恢复，无状态机。
- **⑤两家核对（结论：不同病，只差措辞）**：kimi.coding 以 WorstRemainingPercent=min(滚动,周) 最差剩余
  主判，周尽（剩 0%）不可能被新 5h 窗掩盖——Summarize 余 0% 从「剩0%」改「已用尽」；
  千问 token-plan 单窗+429 探针已有耗尽判别——ToUsageState percent≥100 摘要从「本期已用 100%」
  改「本期已用尽」。方舟周/月尽被新窗掩盖的问题为方舟独有（三窗结构+5h 主位口径）。
- **验收**：测试 15 个新增（尽窗优先/双窗最早重置命名/100 边界/429 耗尽解析窗名+秒毫秒 ISO 重置/
  纯文本正则兜底/未知窗兜底/纯限流保持 Degraded/健康路径回归+kimi/qwen 措辞）四项目全绿；
  装机截图（方舟额度尽卡片显示已尽+重置时间）待装机走查——接口佐证已由 429 mock 测试夹具承担。

### 批3-补12 通知规则接线（2026-10-10 文档盘点发现）
- **config.App.NotificationRules 从不被读**：BeaconRuntime 装配 NotificationEngine 时未传 rulesProvider，
  引擎恒用 DefaultNotificationRules.All——设置页「规则经 config.json 自定义」的提示是空头支票
  （批3-补10 同病排查时误报为「已接线」，写配置文档逐项核对时抓出）。修法一行：装配处传
  `rulesProvider: () => config.App.NotificationRules.Count > 0 ? config.App.NotificationRules
  : DefaultNotificationRules.All`（空列表回退内置默认，与 AppConfig 字段注释口径一致）。
  引擎每次评估事件都调 provider（lambda 闭包实时读 config）——导入配置包路径即时生效；
  手改 config.json 磁盘文件需重启（Store 不热加载磁盘）。
- **验收**：Beacon.App 本地不可编译，CI windows-latest 为准；规则生效实测（自定义规则触发 Toast）
  待装机走查。

### 批3-补13 UI/UX 审计：可发现性修复（2026-10-10 用户令）
- **痛点**：「不拉到窗口底部都不知道右边下方还有配置项」——高级页四分组（动作编辑器/诊断/导入导出/通知
  规则）堆一页，动作表单 9 行占满首屏把后半压出视口且无提示；可滚动区 `Auto` 档滚动条不显眼。
  审计证据 11 条（E1-E13，E6/E9 核实不成立删除），修复 6 组：
  ① 自定义动作拆独立模块页（左侧目录新增「自定义动作」，高级页保留诊断+导入导出+通知规则）；
  ② 全部滚动区 Auto→Visible 常驻（设置右栏/详情窗/快捷面板两 ListView）；
  ③ 动作表单横排压缩（URL+Method、命令+参数各并一行，可视 4 行）；
  ④ 组件刷新档下拉中文标签带秒数（tag 仍存档位名，IndexOfTag/TagOf 改读 Tag）；
  ⑤ 界面透明度滑杆下限 0.4→0.2（与运行时 clamp 对齐，修「配 0.2 一碰滑杆跳回 0.4」）；
  ⑥ 钉选宿主面板 tile>18 改双列 Grid（单列超工作区高时底部裁切无提示；ApplyLayout/WM_NCHITTEST
  同口径按行列数算）。
- **验收**：四测试项目全绿（211/332/27/31）；Beacon.App 仅 CI windows-latest 可编译；
  设置页默认窗口尺寸下各模块完整可见的截图待装机走查。

### 批3-补14 巡检点火：B-301/302/303/206 代码层收口（2026-10-10 用户令）
- **范围**：挑纯代码层可验收的未完项推进，装机走查类 42 项与 WinUI 截图项维持挂起。
- **B-301**（连接测试成功/失败路径）：修两处真 bug（缓存键只用 Id 导致重录 PAT 后仍验旧 token；
  空 PAT 走 /rate_limit 匿名 200 误报连接正常），缺凭据短路带录入指引；假 Handler 分支补至 8 例
  （含 403 限流/权限两分支、超时、token 变更换客户端）。
- **B-302**（PR 计数夹具）：夹具 6→10 例（单数摘要/草稿不豁免/缺字段按 0/坏 JSON→Degraded）；
  红 CI 判据当晚收口（见 B-302 状态行：有状态设计，`warnOnRedCi` 默认关）。
- **B-303**（workflow 映射）：表驱动 12 例不动，端到端 3→8 例（运行中/16min 卡住/成功时长格式/坏 JSON）。
- **B-206**（离线标注 Last update 后自动刷新）：全周期单测——断网重发布陈旧态 FetchedAt 保原值，
  恢复自动刷新清 IsStale 且缓存替换。
- **验收**：四测试项目全绿（212/345/27/31，+14 例）；真实 PAT/真实仓库/真实 workflow 手动路径随装机走查。

### 批3-补15 B-302 红 CI 判据收口：有状态 combined status（2026-10-10 用户令「推进到全清或卡点」）
- **背景**：批3-补14 后非装机未完项只剩红 CI 判据一条；零成本备选（list /pulls 自带 mergeable_state）
  经官方文档核实为死路（该字段仅单 PR 口带），按规格走有状态设计。
- **实现**（`GitHubPullRequestsProvider`）：`warnOnRedCi` 配置（默认关，描述符字段注明「每 PR 一请求」代价）；
  provider 单例按 widget.Id 记忆 PR 号 → (head sha, combined state)：
  - 终态（success/failure）且 sha 不变 → 跨刷新复用，稳态零额外请求；
  - sha 变化或非终态（pending 等）→ 打 `/repos/{repo}/commits/{sha}/status`（ETag 304 = 与上次一致，沿用记忆态）；
  - pulls 口 304 → 仅当记忆中存在非终态 PR 才续探，有翻转则按记忆计数重建状态（含 payload/摘要），全终态返回 null；
  - severity：review-flagged ∪ 红 CI 任一非空 → Warning；摘要追加「N red ci」段；payload 增 red_ci_count/red_ci_numbers。
- **测试**：夹具 10→18 例（默认关零额外请求、新 sha 探测红、终态复用、pending 翻红、304 期间翻转可回报、
  304 全终态 null、sha 换重查、坏 status JSON→Degraded）。
- **验收**：四测试项目全绿（212/353/27/31，+8 例）；真实仓库红 PR 手动验证随装机走查。

### 批3-补16 修复单：设置左栏可调 + 悬浮框进度条重构 + icon 加重（2026-10-10 用户装机实测修复单，commit 86ac00e）
- **透明度（批3-补13⑤）——已验，保持别动**：用户装机确认修好（0.4→0.2 下限与运行时 clamp 对齐）。
- **修复① 设置左侧不可调、很多项目看不到**（SettingsWindow）：
  ①左栏包 ScrollViewer——16 个模块在 640 高窗口必溢出，此前无滚动整段被裁掉（「很多项目看不到」根因）；
  ②列宽 Auto→Pixel 拖拽分隔条（7px 命中区+居中 3px 高亮线，PointerCapture 拖拽，钳位 [260,420]——
  下限=旧内容宽 232+常驻滚动条/内边距，最长模块名+开关不截断）；③标题可见计数「配置中心 · 16 个模块」；
  ④PowerToys 一屏一模块形态（点模块行切右栏整屏）本就在，未动。
- **修复② 悬浮框底部进度条看不到**（PinTile）：CI 截图像素实证前代 Star 列设计**从未渲染出任何像素**
  （A/D 场景 tile 底行纯底色）——数据链无问题（单测 Progress=0.425），病在布局结构。重构为两枚显式
  Rectangle：高 3 DIP、贴底 margin 0、宽由 container.SizeChanged 实测重放（首刷 ActualWidth=0 不丢，
  比例缓存 _barFraction）；track=Value 色 35% 微底轨（双主题可辨）、fill=severity 色；
  结构上不存在「布局余量被吃掉整条消失」路径。
- **修复③ icon 颜色太浅看不清**（PinTile+BrandIconFactory）：像素实证填色/对比度一直正确（15:1），
  真因=Simple Icons 细线几何缩到 14 DIP 前景覆盖率仅 7-22%（千问 7%/DeepSeek 8%/Kimi 10%，肉眼近不可见）。
  尺寸 14→16 DIP + 同色 1px Stroke（等效增重唯一正解）；兜底字形同步 Label() 前景+14px；
  暗主题（D 场景）/亮主题（C 场景）近景截图各验一次。
- **验收（CI 硬断言，失败红构建）**：visual-evidence.ps1 新增——16 模块行 UIA 全可达；左栏滚到底
  「高级」IsOffscreen=false；分隔条真实鼠标拖拽 +60px 左栏实测变宽 ≥40px（截图 before/after）；
  进度条像素断言×2（A 场景 GLM tile、D 场景 ark tile 底部 6 行中部 60% 宽非底色像素 ≥30）；
  新截图 D-settings-window.png / D-settings-splitter-{before,after}.png；既有 C/D 场景近景覆盖双主题 icon。
  四测试项目全绿（212/353/27/31）。

### 批3-补17 悬浮框进度换形态水波纹 + 设置边栏真可拖（2026-10-10 用户令双批，已完成）
- **背景（诚实口径）**：86ac00e 三修复进不了 nightly——38043008092/38043812483/38044411443 三次
  daily-build 全在视觉取证步红（PS 5.1 SetScrollPercent 重载 → UIA peer 缺失 → 合成拖拽 264→264），
  Pack/Installer/Publish 三步被跳过，**用户装的 nightly 仍是 2e530ed 旧版**；「装了还是看不见」由此而来。
- **改① 悬浮框进度条→水波纹**（用户令「不许再调那根条的像素，直接换形态」；86ac00e 的 bar 改法降级保留）：
  ①MotionEngine.StartWave（MotionEngine.cs :173）——Composition 双关键帧（Scale 0.4→1.8 + Opacity
  0→0.9→0）forever 循环，合成线程 60fps 不占 UI 线程；phase ∈ [0,1) 平移波形（双环错相 0/0.5 成扩散涟漪）；
  周期 1800/(0.5+progress) 钳 [600,2400]ms；off 档不启动（全静止铁律）。②PinTile（PinnedHostWindow.cs
  :91-177）：六列布局 [灯][icon][label*][value][wave 16][percent auto]；双环 Stroke=ThemeColors.Label()
  （主题令牌对比度最高的前景，暗亮双主题 ≥4.5:1）；**「NN%」常驻静态文本 FontSize 12**（加载中显示
  「加载中」状态字，离线全收；动画抓不到截图也不影响数字可读）；bar 降级为 config-only 可选路径
  `widgets.json config.progressStyle=="bar"`（不加设置 UI）；加载中波纹转；离线/收起全停（StopWaves+
  ResumeMotion 重放，修「StopWaves 清 _waveOn 后永不复播」状态机 bug）。
- **改② 设置边栏真可拖**（用户令「左边加一个可以拖动的边栏……不要不修」，PowerToys 范式）：
  ①列宽钳位 [260,420]→**[200,420]**（用户规格「固定起始 200」/右 min 420），初值读 config
  `settingsSidebarWidth`；②**拖完落盘**（PointerReleased → SettingsSidebarWidth → SaveApp，
  SettingsWindow.cs :282）+ **启动恢复**（BuildRoot :189，钳位后上列）——「可以拖动」本体是持久化；
  ③分隔条悬停变品牌色 #60A5FA（:264）；④模块行选中态加品牌色左缘 3px 条（SelectModule，
  BorderThickness(3,0,0,0)+BorderBrush #60A5FA）；⑤**设置搜索框**（settings-search，输入过滤
  DisplayName/Subtitle/Key，标题计数「N/M 个模块匹配」，回车跳第一个命中，清词复原）；⑥**窗口尺寸
  记忆** settingsWindowWidth/Height（root.SizeChanged + 800ms DispatcherTimer 防抖落盘，构造器恢复）；
  ⑦模块名 TextTrimming（200 窄列不挤烂）。
- **验收（CI 硬断言，visual-evidence.ps1 同批改）**：场景 A——「NN%」UIA 文本断言 + 波纹双帧近景
  （相隔 700ms，图案不同=动画在跑的目检证据；bar 像素断言随降级作废）；场景 D——
  **config settingsSidebarWidth=400 启动恢复→左栏实测 400±8px（确定性「可拖」证据链，绕过合成鼠标）**、
  搜索「额度」过滤断言（高级隐藏/智谱保留+清词复原）、tile「NN%」常驻断言 + ark 波纹双帧、
  SendInput 绝对坐标拖拽 +60px（38044411443 mouse_event 版不生效，本版换 INPUT 队列级注入；
  仍不生效降级 warning 不拦 nightly，手势转装机手验）。四测试项目全绿（212/353/27/31，
  +SettingsSidebarWidth/WindowWidth/Height 往返断言）。
- **证据（全部落地，run 38050851807 全绿零 warning）**：
  run 链接 https://github.com/cuihairu/beacon/actions/runs/38050851807 ；artifact `visual-evidence`：
  `A-numeric-on-tile-closeup.png`（GLM tile：绿灯+icon+「GLM」+「38%」×2+波纹环同框）/
  `A-numeric-wave-frame2.png` + `D-tile-ark-wave-frame{1,2}.png`（**双帧相位不同=动画在跑**：frame1 环
  近隐、frame2 环扩散中——首版 scalar 动画挂 Vector3 属性 throw 被吞成静止环，38048738860 双帧全同
  实证，commit 55aa306 修 Vector3KeyFrameAnimation）/ `D-settings-window.png`（搜索框+计数+16 模块+
  选中态左缘条）/ `D-settings-splitter-{before,after}.png`（拖后右栏内容整体右移）。
  CI 日志硬断言：**「分隔条拖拽断言通过：左栏 400 → 420 px（钳位感知，期望 ≥420）；config 落盘 420
  （拖完即存，启动恢复闭环）」**（38048738860 已证 SendInput 生效、只是旧断言没算钳位上限数学不可能）+
  「场景 A 百分比断言通过：GLM tile 常驻静态文本『38%』」+「百分比常驻断言通过：tile 静态文本『剩70%』」+
  「边栏宽度恢复断言通过：config 400 → 左栏实测 400px」+「设置搜索断言通过：『额度』过滤+清词复原」。

### 批3-补18 池子注水最终稿 + 设置自选显示方式 + 图标配色 + 右栏间距（2026-10-10 用户令三轮纠偏，已完成）
- **背景**：令C/D mid-turn 用户连续纠偏：①常规页内容贴分隔条黑线 → 要留距离；②GLM 100% 后圆圈不停闪 → 涟漪动画至少可配置，默认关；③"我说的是池子注水不是这种效果" → 水位=进度，满格变 severity 色；④"为啥有的图标有颜色，有的黑灰" → 全模块品牌色。用户最终令：涟漪动画去掉，设置里让用户自己选显示方式（如一开始的进度条）。
- **改① 池子注水去涟漪**：PinTile 移除双环涟漪动画、保留 16DIP 圆池（_poolRing 池边描边 + _poolFill 底部填充，Clip 成圆）；水位=progress×16，满格 Error 红；加载中=空池环+「加载中」；离线/无进度全收；收起/恢复不复播动画。
- **改② 设置自选显示方式**：`appearance.progressStyle`（"pool"/"bar"）默认 "pool"；SettingsWindow 外观页新增「额度显示方式」下拉（池子注水 / 进度条）；PinTile 构造时读 MotionEngine.ProgressStyle（实时），widgets.json config.progressStyle 仍可覆盖（向后兼容）。
- **改③ 配置中心右栏内边距**：`_rightHost.Padding = new Thickness(16,0,0,0)`，常规页内容不再贴分隔条黑线。
- **改④ 模块图标全品牌色**：MakeModuleIcon 按 module.Key 分 16 色（general 蓝 #3B82F6、appearance 紫 #A855F7、github 墨黑 #24292F、claude 赭橙 #D97757、kimi 蓝 #2563EB、deepseek 蓝 #4D6BFE、actions 琥珀 #F59E0B、advanced 玫红 #F43F5E 等），不再落灰兜底。
- **验收**：4 测试项目全绿（212/353/27/31）；daily-build run 待填。证据：静态池水近景、设置页「额度显示方式」下拉、右栏间距、全彩模块图标。
  **可装版本**：nightly 资产 `beacon-nightly-windows-x86_64-setup.exe`（SHA256
  c2bf4e035cf878c113b6722a20d3d7f597da0dc08589a619d92ad7417b1dd48f）/ `beacon-nightly-windows-x86_64.zip`
  （9ad54dc344d33db2d8dfdbd805a08cf24f8aae92abf2504a01162c566c52c6e2），publishedAt 2026-10-10T12:15:19Z
  ——**86ac00e 三修复 + 本批全部首次进 nightly**（此前三次取证红导致 Pack/Publish 跳过，用户装机一直是
  2e530ed 旧版）。场景 A 连接指 mock `/api/monitor/usage/quota/limit`（38% 健康链，不再赌公网）。

### 批3-补19 吸边细条无法恢复修复 + 池水圆弓形 + CI Path 二义收口（2026-10-10 用户令「坍缩成竖线无法恢复」，commit 994753e）
- **根因（用户实测）**：细条 x = work.Right - stripWidth 忽略 offsetX，而展开面板 x 内缩 offsetX——悬停展开瞬间光标落在面板 footprint 外 → PointerExited 立刻坍缩 → 再悬停再展开死循环。
- **修**：细条改用同一 `PinLayoutMath.Place` 数学（宽度换细条宽），条缘与面板缘重合，悬停细条任意位置都在展开面板内；回归 `PinLayoutMathTests.Place_CollapsedStrip_StaysWithinExpandedPanelFootprint`（4 锚点 × 3 偏移断言 strip ⊆ panel，含 clamp 分支）。
- **连带 CI 红（两轮实证）**：CS0246（WinUI 3 无 `Microsoft.UI.Xaml.Point` 投影 → `global::Windows.Foundation.Point`）；CS0104（`Path` 在 Shapes 与 System.IO 间二义 → `global::Microsoft.UI.Xaml.Shapes.Path`，同 BrandIconFactory 模式）。
- **池水几何**：`UIElement.Clip` 只收 RectangleGeometry → 圆水位改 `Path.Data` 14 段 LineSegment 圆弓形（不碰 Clip）。
- **首次进 nightly**：daily-build run 38060109090（994753e）；visual-evidence.zip SHA256 `7689ef64a902e0dc08ea33a7d1dcf2f0b558b13e12887566f35a9b3745903e67`。

### 批3-补20 DPI 修复单（5K 裁字）+ 拖动跟手/跨屏闪烁（2026-10-10 用户装机报障，commits 5879b60 / 78d0592）
- **根因①（5K 裁字）**：DetailWindow 从未 `AppWindow.Resize` → 落 WinUI 默认固定物理像素窗尺寸，200%/250% 客户区装不下 DIP 内容 → 裁字；150%（公司 4K）勉强容下所以「正常」。另 10 处 `(int)(dips * dpi)` 截断换算在小数 dpi（如自定义 130%）下按行/按边累计欠高。
- **修（5879b60）**：`Beacon.Core.Services.DpiLayoutMath` 唯一换算口径（四舍五入非截断；`PanelHeightPx` 总高一次换算防逐行累计；`ClampedHeightPx` 内容封顶走滚动）。DetailWindow Loaded 后按内容自然高实测定尺寸（520/320/560 DIP）；PinnedHost 新增 `PanelPxSize`（ApplyLayout 与 WndProc 命中共用）+ 细条宽 + 命中判定、QuickPanel Resize/贴胶囊定位、SettingsWindow 构造器、Capsule 实测尺寸/默认位、FloatingTile 级联/OnLoaded 共 10 处收口，窗口类不再出现裸 `(int)(dips * dpi)`。
- **回归（④自动化）**：`tests/Beacon.Core.Tests/DpiLayoutMathTests.cs` 四档 dpi（1.0/1.5/2.0/2.5）真断言——舍入界（误差 ≤0.5px）、判别线（`ToPx(32,1.3)=42`、`ToPx(96,1.3)=125`）、裁字反例（`TextFits(14,12,dpi)` 必须假）、逐行累计损失、Capsule/Detail/QuickPanel bounds 精确值。CI 是 100% DPI，此测试是唯一防回归物。
- **修②（78d0592，拖动「拖不动」）**：胶囊逐事件 `_x += Math.Round(delta×dpi)` 在高回报率鼠标下单事件增量 <0.5px 被舍成 0 → 窗口爬行。改起点绝对定位（origin + round(总位移×dpi)，滞后自纠偏）；单击判定改净位移（抖动抵消）。
- **修③（78d0592，跨屏闪烁）**：WM_DPICHANGED 里 ApplyLayout 的 Resize+Move 与拖动 Move 互抢窗口位置 → 拖动中跳过重排（落点 FinalizeDrag 兜底）；两个拖动窗目标没变不重发 Move（压 SetWindowPos 洪水）。**残留**：三屏异 DPI 时窗口横跨两个 DPI 区，DWM 重采样闪属系统行为，App 侧不可消除。
- **验收**：4 项目 246/353(+Moonshot 后 371)/27/31 全绿；ci 38062889229 绿。
- **限制（诚实口径）**：CI windows-latest 恒 100% DPI 单显示器，per-monitor 缩放不可程序化伪造——200%/250% 实效只能装机验（对应 B-102/B-703 装机行）。

### 批3-补21 Moonshot 开放平台余额接入（2026-10-10 用户令，commit 4c3ecb2 + 设置行/单测）
- **接口**：moonshot.balance = GET `https://api.moonshot.cn/v1/users/me/balance`（整串端点无补路径，Bearer）；响应 `{code,data.available_balance}`，`ParseBalance` 纯函数兼容数字/字符串金额；阈值 `warn_below=20`/`error_below=5`（≤error→Error、≤warn→Warning）；连接测试 200 但结构不对 → Degraded（代理假阳性不算连通）。与 Kimi For Coding 是两套账号体系（Key 不通用），品牌标共用月之暗面（`BrandIcons["moonshot"]="kimi"` 别名）。
- **Settings**：模块行「Moonshot 开放平台 · 开放平台余额 · 与 Kimi 不通用」（靛蓝 #4F46E5 区分 kimi 行 #2563EB）、空连接提示、连接表单 4 处文案（Id 预填 moonshot-main）；类型下拉走 `ConnectionProviders.Keys` 自动出现。
- **单测**：`MoonshotBalanceProviderTests` 18 例（解析 5 / 阈值 7 / 摘要 1 / 端到端 5 含 Bearer 与整串端点断言 / 连接测试 3 含 401→Unauthorized）。4 项目 246/371/27/31 全绿。



### 批3-补22 拖动跨屏坐标混算 + 详情窗标题栏裁字续修（2026-10-11 装机反馈「没有修复」，commits f898a40 / c80c244）
- **拖动比例错乱（f898a40）**：三处拖动（胶囊/悬浮框/宿主面板）按下时把起点偏移记在**按下时的 DPI 空间**，每帧 Move 却**重读当前 GetDpi()** 换算——跨到不同缩放的屏瞬间两套坐标混算，位移比例错乱（拖不动/跳变/滞后），三屏异缩放场景必现。修：拖动锚点改**光标屏幕物理像素**（GetCursorPos，P/Invoke 本就存在），与 AppWindow.Position/Move 同一坐标系，全程零换算；死区 3 物理像素与吸附判定口径不变。
- **详情窗裁字续修（c80c244）**：批3-补20 只补了「没 Resize」，漏了 DetailWindow 是**标准标题栏窗**——AppWindow.Resize 设的是**外框**尺寸，客户区 = 外框 − 标题栏/边框；200% 下标题栏物理高度翻倍，客户区比内容矮一截，底部照旧裁字（用户装机复测「没有修复」的直接根因）。修：GetWindowRect − GetClientRect 求非客户区物理差，Resize 时加回（无边框窗差值本就为 0，不受影响）。
- **验收**：4 项目 246/371/27/31 全绿。装后复测：拖胶囊/悬浮框跨三个缩放不同的屏看跟手性；200% 下开详情窗看字完整。
- **诚实口径**：拖动跟手性若仍差，剩余嫌疑是亚克力背板每帧重采样 + UI 线程事件频率（渲染管线开销），需装机现象再定位；异 DPI 屏间 DWM 重采样闪烁为系统行为，App 侧不可消除。
- **nightly 归属核实（2026-10-11）**：daily 38071417837（c880f5d）**成功**（此前 `gh run watch` exit 1 是 watch 流中断误报，API 查 conclusion=success；发布断言「远端 SHA256SUMS 与本地构建一致」已过）。当前 nightly 资产 zip `a0b255bd…`/setup `bdea86f1…`、publishedAt=2026-10-10T17:29:59Z、targetCommitish=c880f5d——三处修复**全部在装**。历史指纹 02327773/8d40d878 与 f682aef0/c9507c8c 分别来自 37822f2 的两跑（38064839291/38066719768，同 commit 重编译内嵌时间戳不同哈希不同），38071029645（6f71173）失败在 dotnet publish（CS0103）从未发布。**装机复测必须用 17:30Z 之后的 nightly**——用户报「没有修复」时装的是更早的包（f898a40 三修复未进装）。


### 批3-补23 进度特效重设计：bar 翻回默认 + 4 DIP 圆角条（2026-10-11 用户令「现在的进度特效太丑了……不要现在这个圆圈的效果」）
- **默认翻转**：`appearance.progressStyle` 缺省 `"pool"`→`"bar"`（旧配置显式写 pool 的保持不动，不迁移）；MotionEngine 归一化——缺省/非法值一律落 bar，圆池绝不做默认路径（仅显式选择）。
- **bar 重设计**（PinTile）：3px→**4 DIP 高 + 2 DIP 圆角**（胶囊形整条；3px 在 100% 缩放下细到难辨，用户实测「进度条看不到」）；track=底色 35% 透明底轨、fill=档位色（**≥80% warning、≥98% Error 红**，正常档跟组件 severity）；「**NN%**」静态读数常驻（动画+文字双通道，字号随 tile 高度 0.375×分级，任何尺寸不裁字）。
- **推进动画**：Storyboard+DoubleAnimation(Width) 平滑过渡（CubicEase EaseOut，时长=300ms×intensity，off 档瞬时直设）；首次显示直接落位不播假动画；WinUI 无 LayoutTransaction，Storyboard 动 Width 是不改视觉树的布局动画唯一口（微软 reference 实证 LayoutTransition 仅是 ItemCollectionTransitionTriggers 枚举成员，非类型）。
- **Settings**：下拉标签按用户易读序——「进度条（默认：底部条 + 百分比读数）」在前、「圆池注水（旧特效：水位=进度，满格变红）」在后；仍即时生效。
- **CI 视觉断言**（visual-evidence.ps1）：复用 Count-BarPixels 做硬断言（margin 0 裁窗，底部 4px 带 ≥120 像素，A/D 两场景）+ 新增 `A-tile-bar-band.png` / `D-tile-ark-bar-closeup.png` 证据截图（池水近景命名随之退役）。
- **验收**：4 项目全绿（含新增默认值断言 `AppConfig_defaults_survive_round_trip`）。装后看：tile 底部圆角进度条 + NN% 读数；≥80% 变黄、满格变红；设置里两档可切即时生效。
- **诚实口径**：CI 单屏恒 100% 缩放，150/200/250% 的条带与字号表现 CI 无法代验（DIP 恒定，缩放由系统等比放大；装机复测为准）。


### 批3-补24 默认值两连翻转（2026-10-11 用户令「数量悬浮窗 默认开 状态胶囊默认关」）
- **数量悬浮窗默认开**：`NumericFloatingEnabled` 缺省 `false`→`true`（推翻 2026-10-08 默认关拍板，见拍板记录推翻行）；
  **一次性强制置关迁移整块撤除**（App.xaml.cs——迁移不撤会把新默认在新配置首启即强制回关，与新令直接冲突）；
  `NumericFloatingResetDone` 字段废弃保留（旧配置 JSON 兼容，不再读写）；设置开关标签改「默认开」；
  悬浮形态迁移提示文案改条件式措辞（只在总闸关时提示，不再写「默认不显示」）。
- **状态胶囊默认关**：维持现状（`ShowCapsule=false` 本就是 2026-10-09 拍板，用户令重申，零代码改动）。
- **验收**：默认值断言翻转（`AppConfig_defaults_survive_round_trip` 断 `NumericFloatingEnabled=true`），4 项目全绿。
  装后看：新装/新配置数值类组件（GLM/Kimi 等额度卡）默认出现在桌面悬浮框；设置里可关回零残留。


### 批3-补25 面板常驻：托盘菜单可勾选「Keep Panel Open」（2026-10-11 用户令「右键菜单前打勾，显示消息的面板就可以一直在」）
- **交互**：托盘右键菜单 Open Beacon 下新增可勾选项 **Keep Panel Open**（Win32 `MF_CHECKED`，勾选态每次开菜单实时读 config）——
  打勾 = L2 快捷面板失焦**不再自动隐藏**，一直留在桌面；Esc / 再次点托盘仍可显式关（勾选保持）；
  取消勾选 = 回 Flyout 瞬态语义（点外面自动收，RFC §6.4 默认）。**打勾即刻显示面板**（未开则打开，已开不动）。
- **落盘**：`config.keepQuickPanelOpen`（默认 false = 现状瞬态语义），即时生效 + 重启保持勾选；
  启动不自动弹面板（勾选只控「不失焦自隐」，开机首显仍需点一下托盘/胶囊——避免开机桌面突现一窗）。
- **验收**：默认值断言入 `AppConfig_defaults_survive_round_trip`；4 项目全绿。装后看：右键托盘 → 勾 Keep Panel Open →
  面板弹出且点别处不消失；取消勾选 → 点别处即收。诚实口径：托盘菜单是 Win32 弹出菜单，CI 截图链（UIA 走设置窗）不覆盖，装机目检。



### 批3-补26 设计基线收编 + 差异审计（2026-10-11 用户令「把总方案定为正式设计基线，先审计后只改真冲突」）
- **收编**：用户新总方案全文入库 `docs/master-plan.md`（产品范围与设计基线 v1.0；mkdocs 导航 + README 文档表已挂）。
  实施路线按其附言：**先差异审计，后只改真正冲突的部分**，不大规模重构。
- **审计结论——一致面（复用，零改动）**：定位/边界（§2.3 不做清单 ≈ RFC §2.2 + mission 分期）、五级 Severity +
  LifecycleState 映射（RFC §4.1）、Provider/Action/Secret 抽象（RFC §5，文档自认「以仓库现有实现为准」）、
  分级刷新 + 抖动退避 + ETag + 限额感知（RFC §7.1 已实现）、缓存优先永不白屏（§7.2）、通知规则 + 冷却去重 +
  notifications.json（§8 已落盘）、单实例/热键冲突提示/自启/原子写/导入导出无密钥/日志脱敏（§10/§9/§13 全就位）、
  凭据仅 DPAPI（新增连接全走 DPAPI 既有口径）、面板 Esc/失焦关 + Keep Panel Open 打勾常驻（默认关 = 文档瞬态默认）、
  存储 5 文件划分（文档自认「不必强行改名」，history ≈ notifications.json 滚动 200 条）。
- **审计结论——冲突/拍板点（4 条，待用户拍板后才动代码/文档）**：
  1. **层级编号两套**：总方案 L0=胶囊/L1=面板/L2=详情 vs RFC 现行 L0=钉选悬浮组件/L1=胶囊/L2=面板/L3=详情。
     建议最小改法：master-plan 头部对照表已注明，文档行文不强制统一，代码与 RFC 口径不动。
  2. **多 Widget 悬浮桌面 vs「只留胶囊+面板+详情」**（总方案 §3.6）：与 2026-10-11 用户令
     「数量悬浮窗默认开 + 胶囊默认关」**直接相反**，且 FloatingTileHost/PinnedHostWindow/胶囊已完整落地。
     建议：维持现行默认（用户令优先于基线文档），总方案 §3.6 记「默认体验为单宿主面板形态，悬浮框为用户显式选择」口径；待拍板。
  3. **local.command 执行器**（总方案 §7.1 禁任意 shell）：与已落地 `LocalCommandExecutor`（默认确认弹窗 +
     设置明文可见，RFC §13）冲突。建议：保留但收口为「默认确认 + 可按 Action 关闭」现状不动、文档记例外；或按总方案冻结入口。待拍板。
  4. **Quick Panel 三分区改版**（需要关注/正在运行/最近状态 + 行内操作按钮；建议宽 360–420，现 320×540）：
     未实现，属总方案 M4 范围。建议列为待办（拍板后入清单）；行内 [重试] 等按钮注意 L2 与 L3 Action 分工不变。
- **小缺口备查**（不阻塞）：胶囊右键菜单（总方案 §3.1）现无（CapsuleWindow 仅点击下钻）；Explorer 重启恢复托盘、
  睡眠唤醒恢复刷新未在验收清单显式列项（总方案 §9/§11.3 有）——可并入装机走查清单增补。
- **边界冻结建议**（总方案 §2.3 vs mission 第二阶段/RFC §14）：Machines/Agents/Docker/Control Plane/本地 AI（Ask Beacon）
  建议在 mission/RFC 标「冻结」而非删除（拍板后随 M0 一起改）。Jenkins Provider 即总方案「公司打包工具」槽位——
  公共实例 fixtures 已录（weekly.ci.jenkins.io 真实 JSON），**实现继续挂起等用户给真实 Jenkins 地址**（总方案 §13 后续可选口径一致）。

### 批3-补27 四项拍板落地 + 面板三分区 + 生命周期自愈（2026-10-11 用户令「四项冲突自行拍板，只改真冲突」）
- **拍板①层级编号**：RFC 编号定为产品唯一口径；master-plan 头部对照表注明映射。零代码。
- **拍板②悬浮默认**：维持现行默认（用户令优先）；master-plan §3.6 改写为「默认单宿主面板 + 显式选择的悬浮框」。零代码。
- **拍板③local.command**：保留现状（默认确认 + 明文可见 + 超时捕获），master-plan §7.1 加例外注。零代码。
- **拍板④面板三分区**（d1ccb6a）：新增 Core 纯函数 `QuickPanelSections.Resolve`（需要关注 = Warning 及以上/过期/连接断；
  正在运行 = Running/Queued；其余不单独成区——与胶囊「黄=需要注意」同口径，RFC §4.1），面板宽 320→380（建议带 360–420 取中），
  新增 NEEDS ATTENTION / RUNNING 两个状态区（空区隐藏，两区全空整块收起回落旧观感），行复用 MakeStateRow（单击进 L3、右键 Pin），
  行文本 MaxWidth 210→260 随窗宽。10 例单测（Core 246→256）。
- **生命周期自愈**（ff8208a，总方案 §9 明列但此前代码无实现——审计新发现缺口）：
  ①Explorer 重启托盘恢复：`RegisterWindowMessage("TaskbarCreated")` 取动态消息号，收到即重发 `NIM_ADD`
  （hIcon 保留最后一次 SetSeverity 的级别色图标句柄，恢复即回到当前色）；
  ②睡眠唤醒补刷：message-only 窗收不到电源广播，走 `RegisterSuspendResumeNotification` 定向注册，
  唤醒（PBT_APMRESUMEAUTOMATIC）立即 `runtime.RefreshAll`，不等下个周期；ShutdownApp 反注册。
- **范围冻结**（总方案 §2.3 vs mission 第二阶段 / RFC §14）：GitLab/Docker/Machines/Agents/Workflow/[Run Task]/
  Control Plane/本地 AI 一律标「冻结」（mission 第二阶段 + RFC §14 加注），不删除历史记录；
  Generic REST（http.status/http.quota）与 Webhook 已落地不受影响；Jenkins 待真实地址另议。
- **验收**：4 项目 685/685 全绿。真机相关（三分区观感/托盘恢复/唤醒补刷）无 Linux 自动化路径，
  CI 恒 100% 无法代验，装机走查清单新增 2 条（56→58）。诚实口径：Win32 消息链路只能装机肉眼验证。

### 批3-补28 胶囊右键直达设置 + 咨询项收口（2026-10-11，master-plan §3.1 余项）
- **胶囊右键 → 设置**：`CapsuleWindow` 加 `RightTapped`（左键拖动在 Pressed 已过滤非左键，互不干扰）→
  `SettingsRequested` → App 复用托盘同款 `OpenSettings`。master-plan §3.1「右键打开托盘菜单或常用设置」
  取「常用设置直达」——不复制托盘全菜单（托盘菜单是唯一生命周期出口）。
- **ConnectionConfig.Type 注释修正**：`"github" | "rest"` 陈旧残句改为 13 个真实类型全集（纯注释）。
- **咨询项收口（不改代码）**：①open.pr/ci 悬浮 arm 维持现状——无 Provider 用 FloatingSupported，
  状态 tile 面板承载 + 数值卡悬浮是 2026-10-08/11 拍板链的既有口径，无缺陷不动；②趋势持久化无承载
  （趋势图 tile 尚无实现者），随实现者一并设计；③RefreshScheduler 组 kick 已有（每 `(connection,tier)`
  组 Kick 信号量 + runtime.RefreshAll），无缺口；④open.issue executor 超出 master-plan 范围
  （§2.3 反对 GitHub 全功能客户端），不做。
- **验收**：4 项目全绿；胶囊右键弹设置窗为装机目检项（清单 58→59）。

### 批3-补29 Kimi 套餐余量按真实响应重写 + HTTP 通道 POST 体（2026-10-11 用户实抓 GetUsages）
- **真因**：旧解析器假设顶层 `usage` 对象 + 数字值；真实响应（POST
  `www.kimi.com/apiv2/...BillingService/GetUsages`，用户浏览器实抓）是 `usages[]`（按 scope=FEATURE_CODING
  取条目，条目 detail=周期窗、条目内 limits[]=5h 滚动窗）+ `totalQuota`，**数值全是字符串**（"100"）——
  旧 `ReadDouble` 只认 Number → 全 null → Degraded/显示错。
- **修复**：解析器双形兼容（真实形状优先、旧社区端点形状兜底）；`ReadDouble` 容忍字符串数值；
  `totalQuota` 进 payload（total_limit/total_used/total_remaining）。
- **HttpEndpoint 增强**：通用 Settings 新增 `http_method`（默认 GET）与 `http_body`（默认不带），
  控制台 POST 口可配（Cookie 认证：`auth_header=Cookie` + `auth_prefix` 置空）；存量 13 类零行为变化。
- **凭据模型（文档已明示）**：Cookie 由用户从浏览器 F12 复制粘贴进凭据框，DPAPI 存储、请求回放；
  Beacon **不做扫码/登录流**；会话过期后重录一次（与 MiMo 控制台 Cookie 同口径）。
- **验收**：真实形状夹具（实抓 JSON 结构，零凭据）+ 旧形状回归 + POST/Cookie 头断言共 9 例新增，
  4 项目 690/690 全绿。测试 Cookie 用假值 `sk-kimi-demo`，真实 Cookie 只进用户 DPAPI。

### 批3-补30 MiMo 控制台响应真身实抓落地（2026-10-11 用户实抓 tokenPlan/usage）
- **真身**：`{"code":0,"data":{"monthUsage":{"percent":0.0007,"items":[{"name":"month_total_token",
  "used":…,"limit":…,"percent":…}]},"usage":{"items":[plan_total_token, compensation_total_token]}}}`——
  **percent 字段是 0-1 比例（非 0-100）**，且舍入粗糙（usage 块 2 位小数会把 0.069% 舍成 0.00）。
- **修复**：`ParseConsoleUsage` 真实形状优先（monthUsage → usage 块，items[] 按 name 取主条目），
  used/limit 自算精确百分比、比例字段仅作无绝对数时的兜底（×100 归一 0-100）；旧防御键名路径保留兜底。
  `SendConsoleAsync` 补 plan-manage referer（实抓请求带，防来源校验）。
- **文档改口**：positioning「官方无额度接口（7 端点全 404）」限定为**推理域 API** 实测，控制台域例外
  （早前表述会误导「小米无任何额度口」）；configuration.md mimo 行补真实形状与 0-1 比例口径。
- **凭据模型**：同 Kimi 控制台口——浏览器复制 Cookie 粘贴 + DPAPI 回放，不做扫码登录，过期重录。
- **验收**：真实形状 4 例（比例归一/块回退/Cookie+referer/95% 告警边界）+ 旧防御理论全保留，
  4 项目全绿。测试零真实凭据。

### 批3-补31 Jenkins Provider 落地（2026-10-11，用户拍板走开源标准 JSON API，关闭批3-补27 挂起项）
- **拍板回执**：批3-补27 曾挂起「等用户给真实 Jenkins 地址」——用户令：Jenkins 是开源软件，标准 JSON API
  任意实例通用，地址只是配置值，不构成卡点。据此按 weekly.ci.jenkins.io 真实夹具（freestyle blue #1 /
  pipeline red #22 / test notbuilt / queue / Folder）实现。
- **jenkins.build**：`{base}/job/<每段>/api/json?tree=…`（Folder 逐段转义）；一次请求按 `color` 判级
  （blue/yellow/red/dark_red/aborted/gray/notbuilt/disabled，`_anime`=构建中）——job 级 `lastBuild` 实测
  **不含 result**，不追打第二击；`lastBuild.number/url` 与 `healthReport[0]` 进 payload。
- **jenkins 连接**：Endpoint=实例基址必填；凭据可空（匿名读）或 `user:apiToken` Basic（DPAPI）；
  测试取根视图 `/api/json`，2xx 且是 JSON 才算连通（防反代 200 HTML 假阳性）。
- **注册 6 点**：WidgetTypeNames、SettingsWindow（模块行/模块色 #D33833/类型下拉/空连接提示/连接表单
  4 处文案）、BeaconRuntime（widget+connection 映射）；BrandIcons `["jenkins"]` 品牌标此前已就位。
- **文档**：README/index 13→14 类；solution-structure（26 文件/树/722 测试）；configuration.md 两表加行；
  positioning §6 落地清单 + §8 条目；master-plan §10 落地状态改口径。

### 批3-补32 本机网络 Provider 落地（2026-10-11，用户拍板「本机资源 和本机网络 都可以搞」）
- **拍板回执**：用户令「本机的cpu 内存 磁盘空间 这个倒是一个低成本的显示」「还有网络资源比如ip 上行 下行速度
  总的流量啥的」「网络资源 和 git 都搞吗」→「都搞」→「本机资源 和本机网络 都可以搞」。本机资源=只读本机 OS
  数据，不触及 mission 冻结的远程 Machines 管理（服务器/PC/Agent 远程监控，§第二阶段冻结记录）。
- **machine.net**：组件字段 `label` + `interface`（网卡名，空=自动选活动网卡）；自动选卡=Up+非回环+有非回环
  IPv4，有网关者优先（默认路由所在卡）；点名网卡不存在按 Degraded fail-loud（与 Jenkins job_path 同口径）。
  速率=相邻两次采样计数差÷时间差（machine 档 15s；首刷无基线只出 IP 与累计值；dt<1s 不出速率防手动连刷尖峰；
  计数回退/换卡重记基线）；累计流量（自开机）进 payload 走 L3 详情，tile 一行只放 IP+实时速率（32 DIP 装不下四个数）。
- **machine 连接**：零凭据零网络，恒通（本机即本机）；Endpoint/Token 留空。
- **注册 6 点**：WidgetTypeNames、SettingsWindow（模块行/模块色 #107C10/描述聚合/空连接提示/连接表单 4 处文案）、
  BeaconRuntime（widget+connection 映射）。
- **文档**：README/index 14→15 类；solution-structure（27 文件/树/748 测试）；configuration.md 两表加行；
  positioning §6 Machine Provider 行改口径 + §8 条目；master-plan §10 落地状态改口径。
- **验收**：26 例（选卡 4 + 差分/状态 7 + 单位换算 12 + 连接 1 + 描述 1 + 异常透传 1）748/748 全绿；
  真机：装后建 machine 连接 + machine.net 组件，首刷 IP+累计、第二刷起速率随刷新走。
- **验收**：真实夹具 21 例（URL 转义/color 词汇表全量/SPA 假阳性/Basic 匿名两态/401 指引），4 项目 722/722
  全绿。真机项：装后对任意可达 Jenkins 实例（公共或公司）建连接 + 添加 job_path 组件看构建灯。

### 批3-补33 本机 Git 仓库 Provider 落地（2026-10-11，用户拍板「git 都搞」）
- **machine.git**：每仓库一张卡 `beacon ↑2↓5 · 改 7 · stash 1`（label 覆盖仓库名；↑=ahead 待推、↓=behind 待拉）。
  git CLI 三连只读：`status --porcelain=v1 -b`（分支行三态：常规/未出生/detached；dirty=全部条目、staged=X 位非空格非 ?、
  untracked=??）、`rev-list --left-right --count @{u}...HEAD`（左=behind；无上游跳过不追打第二击）、`stash list`。
  behind>0 或分叉 → Warning（Signal 不是 Dashboard：要动手才亮黄，仅待推 ahead 是 Info）；变更文件前 5 条预览
  （重命名取 -> 右侧）进 payload 走 L3，32 DIP 不分页（用户拍板「展示多少页」收口）。路径缺失/目录不存在/
  git 未安装/超时 10s → Degraded fail-loud。字段 `label` + `repo_path`（必填）。
- **注册**：连接复用 machine（零凭据，本机即本机）；SettingsWindow 类型下拉与模块行描述聚合、BeaconRuntime 映射。
- **文档**：solution-structure（28 文件/766 测试，树自组 Machine 组）、configuration.md 两表 machine 行、positioning §6/§8。
- **验收**：12 例（porcelain 解析 4 + rev-list 1 + Provider 映射 6 + 真仓库集成 1——git init/stash 真链路，
  Linux 与 windows-latest 双跑），766/766 全绿；真机：装后建 machine 连接 + machine.git 组件看四数与亮黄。

### 批3-补34 MiMo 控制台 Cookie 整行容错（2026-10-11，用户问「复制多了也能正确处理吧」）
- **问题**：控制台凭据原样塞进 `api-platform_ph` 头，用户从 DevTools 整行复制 Cookie 粘进来
  （xiaomichatbot_serviceToken/userId 等一起带上）→ 服务端不认 → 卡 offline（2026-10-11 用户实测）。
- **修法**：`NormalizeConsoleCookie`——含分号按整行解析只取 `api-platform_ph` 值（引号剥掉）；不含分号按裸值用
  （引号照剥）；键不存在原样透传由服务端 401 走既有指引。存量整行凭据无需重录，装新包即生效。
- **验收**：6 例（裸值/引号/整行带引号/无引号/值含分号不干扰/无目标键透传）。

### 批3-补35 Herald 接入落地（2026-10-11，用户拍板「先做 incidents+feed」）
- **背景**：用户令为自有项目 herald（cuihairu/herald，Go 统一订阅与投递中枢）加配置主动拉关心的消息；
  先分析 API 面（feeds/logs/incidents/stats）后拍板：三张卡候选先做两张，deliveries 暂缓。
- **herald 连接**：Endpoint=基址必填；凭据可空（Herald 未开 auth 匿名可用）或 API Key 走 X-API-Key 头（DPAPI）；
  测试打恒公开口 `/api/v1/status`，2xx 且是 JSON 才算连通（反代 HTML 壳按 Degraded，Jenkins 同口径）。
- **herald.incidents**：`/api/v1/incidents?status=open|acked|resolved|all`（默认 open，乱填 fail-loud）；
  open>0 亮黄、含 critical 亮红（Error/Failed）、回看口径恒 Info；最新一条 id/title/level/opened_at 进 payload 走 L3。
- **herald.feed**：公开类目 `/feeds/{category}.xml`（slug 校验 `^[a-zA-Z0-9._-]{1,64}$` 防注入）或私密
  `/feeds/private/{token}.xml`（token 即凭据，`feed_token_ref` 默认 herald:feed 走 DPAPI，不落 widgets.json
  不进 payload；类目优先）；RSS 按最新在前；未读=自上一次刷新新增 guid（按组件记基线，首刷只记基线不出未读）。
- **坑（实证）**：Go `time.RFC1123Z` 时区写 `+0000`（无冒号），且本机/CI 容器 .NET 跑 globalization-invariant
  模式——带星期名的日期解析四种时区写法全挂（连 GMT+TryParseExact 都 False）；剥「Tue, 」前缀后 TryParse 全通。
- **顺带修复**：GitWorkspaceProviderTests 真仓库集成清理在 Windows 撞 .git 只读对象（UnauthorizedAccessException，
  CI windows-latest 实证，Linux 复现不了）→ 清理 catch 扩面。
- **注册 6 点**：WidgetTypeNames、SettingsWindow（模块行/模块色琥珀/类型下拉/空连接提示/连接表单 4 处文案）、
  BeaconRuntime（2 widget + 1 connection 映射）。
- **文档**：README/index 15→16 类；solution-structure（29 文件/787 测试）；configuration.md 两表加行；
  positioning §8 条目；master-plan §10 计数。
- **验收**：21 例（incidents 解析 2 + 事件映射 5 + RSS 解析 3 + feed 6 + 连接 3 + 描述/slug 2），787/787 全绿；
  真机：装后对自建 herald 实例建连接 + 两张卡看数。

### 批3-补36 本机资源 Provider 落地（2026-10-11，批3-补32 拍板回执「本机的cpu 内存 磁盘空间」）
- **背景**：批3-补32 拍板「本机资源 和本机网络 都可以搞」的第三块拼图——net/git 已落（补32/33），本条补 CPU/内存/磁盘。
- **machine.local**：一行三段 `本机资源 · CPU 40% · 内存 62% · C: 70%`（首刷无 CPU 基线只出两段，第二刷起三段）。
  CPU=相邻两次累计 tick 差分（同速率口径；idle 回退/原地不动只换基线不出数）；内存/磁盘即时占比。
  阈值 `cpu_threshold`/`mem_threshold`/`disk_threshold` 默认 90，只认 1-100 数字（乱填 fail-loud）；
  超线亮黄不分级（Signal 不是 Dashboard），**按四舍五入后的显示值比线**（显示 90 就按 90 报警，所见即所警）。
- **采样跨平台**：`IMachineResourceSource` 注入抽象（假样本 Linux 全程可测）；生产 Linux 读 /proc
  （stat 首行 user..steal，idle+iowait 记闲；meminfo MemAvailable，老内核 MemFree 兜底），Windows 走
  GetSystemTimes（kernel 段已含 idle）+ GlobalMemoryStatusEx；磁盘统一 DriveInfo（空=系统盘/根分区；
  点名不存在/相对路径/未就绪都 Degraded fail-loud）。真实 /proc+DriveInfo 集成测试 Linux 跑、windows-latest 跳过。
- **坑**：`Enumerable.Sum` 无 ulong 重载（CS1929），jiffies 累加手写 foreach。
- **注册**：MachineWidgetDescriptors.Local（5 字段：label/disk_path/三阈值）、BeaconRuntime 映射、
  SettingsWindow 模块行描述与空连接提示文案（资源/网络/Git 并列）。
- **文档**：solution-structure（30 文件/801 测试，Machine 3 文件）；configuration.md machine 行；
  positioning §6/§8。
- **验收**：14 例（/proc 解析 2 + 阈值 1 + Provider 映射 8 + 描述 1 + 真实采样链路 2 条件跑），801/801 全绿；
  真机：machine 连接 + machine.local 组件，首刷两段第二刷三段随刷新走，调低阈值（如 20）看亮黄。

### 批3-补37 Herald 投递失败卡落地（2026-10-11 续批，批3-补35 暂缓项收尾）
- **拍板**：源码实证（herald core/logstore+runtime/manager+api/server）后落地。日志态词表写死四态
  pending/success/failed/shadow（types.go 注释 "maps it to its wire strings unchanged"）；重试耗尽的
  dead 在 settle() 里 task 态是 dead 但**日志行统一写 failed**——Beacon 从 logs 口看二者不可分也不必分
  （都要人管）；shadow 是 dry-run 观察（"never represent delivery"）被 `status=failed` 过滤天然排除；
  pending 是排队/投递中瞬态不报。
- **herald.deliveries**：`GET /api/v1/logs?status=failed&limit=200[&provider=X]`（提供方筛选可空，转义）；
  failed>0 亮黄（Signal 同构 incidents：送信没送达=要人管，挂着直到处理——不做增量基线，跨重启状态一致）；
  计数取响应 total（全窗口过滤总数，logs 数组截断在 limit）；最新一条 id/provider/error/created_at 进 payload
  走 L3；created_at RFC3339 带纳秒（.NET tick 精度 100ns 四舍五入，测试按 tick 口径断言）。无全局
  deliveries 专口（`/apps/{app}/deliveries` 是按 app 的），logs 端点即正确取数面。字段 `label`+`provider`。
- **注册**：HeraldWidgetDescriptors.Deliveries（FloatingOptIn）、BeaconRuntime 映射、SettingsWindow 模块行
  描述改「事件台账 · 订阅源 · 投递失败」。
- **验收**：6 例（logs 解析 2 + 投递映射 4，含 provider 筛选进 URL 与缺 Endpoint fail-loud），807/807 全绿；
  真机：装后对 herald 实例加 herald.deliveries 组件，人为制造一次失败投递看亮黄与计数。

### 批3-补38 MiMo 401 明示 + LiteLLM 网关记账档（2026-10-11 修复单「beacon 小米的还是不行」）
- **拍板（用户诊断直接采信）**：裸 GET 控制台口回 401+loginUrl=端点活着；卡片不行=档① `api-platform_ph`
  cookie 没录/已过期（会话 cookie 过期是常态）。此前 401 走笼统 Degraded 文案，用户看不出要重录 cookie。
- **改动一（401 明示）**：控制台 401/403 → Degraded 文案明示「控制台 Cookie 已失效或未录制」+ 重录指引
  原文（浏览器登录 platform.xiaomimimo.com → F12 → 应用 → Cookies → 复制 api-platform_ph → 设置重录）；
  **Degraded 中止不静默降档**（否则验收「卡片显示指引原文」过不了）；②/③/④ 档代码不动。
- **改动二（LiteLLM 网关记账档，插在 cookie 之后）**：连接 Settings `litellm_spend_url`（默认空=不外发；
  本机 127.0.0.1 记账不算外发）+ `litellm_key_ref`（默认 `mimo:litellm`，有则带 Bearer 无则裸调——用户
  实测本机网关无参 400 非 401=auth 没拦）。请求带本月起止 `start_date/end_date + summarize=true`
  （参数名按网关 openapi 实证）；解析四形兼容（根数组 / response[] / results[] / summarize 汇总对象）
  累计 spend → 「本月已用 $X（网关记账）」；**网关无额度分母只显已用量（如实，不造）**。
- **实证（2026-10-11 用户本机网关 127.0.0.1:4000，config 无 database）**：`/spend/logs` 无 key 401 auth_error、
  带 master key 400 no_db_connection、/metrics 401、/v1/usage 404——无 DB 时全部 spend 面同一个
  no_db_connection，参数修不出来；代码按 Degraded 明示「未接数据库（spend 面需网关 config 接 DB）」。
  LiteLLM 真调 200+数据（修复单验收第二项）**卡点=网关是否接 DB，属网关侧决策**，登记待用户拍板。
- **验收**：MiMo 单测 43→57（401 新文案、四形解析、no_db_connection/401 指引/5xx Offline、档序
  cookie>litellm>本机计数、URL & 续接），四项目 821/821 全绿；真机：①故意填过期 cookie 看「已失效+
  指引」原文；②网关接 DB 后录 litellm_spend_url 看真实已用量。

### 批3-补39 单测复点巡检（2026-10-11 续批：todo.md 六条「夹具/表驱动单测」逐条核对）
- **核对结论（六条全部已有测试，其中一条补缺）**：PR 计数/映射=GitHubPullRequestsProviderTests 18 例；
  workflow 状态映射=GitHubActionsProviderTests MapRun_TableDriven TheoryData+端到端 9 例；
  路由/确认/超时=ActionRunnerTests 9 例；API 封装假 Handler=GitHubApiClientTests 10 例+
  GitHubWorkflowExecutorTests 6 例；规则引擎含冷却=NotificationEngineTests 17 例（阈值 TheoryData/
  冷却抑制与到期/跨越重臂）；超时终止+输出落盘=LocalCommandExecutorTests。以上此前批3-补14/15 与
  B-401 落地时已建，本次复点全部绿。
- **补缺（模板渲染 4→11 例）**：TemplateRenderer 此前只有 4 例占位/缺键，本次按源码语义补 7 例：
  键字符集含点与连字符、同键重复占位全替换、替换值不二次展开（单趟正则防注入递归）、
  键字符集外花括号原样保留不抛（JSON body 模板实况）、缺键去重清单。
- **口径**：todo.md 对应六条拆为「单测半勾选 + 手动半标注【需装机】」，不伪造手动验收；
  未修类（50 条）逐条扫过均为「代码就位待装机首验」，无新增可代码修复项。
- **验收**：四项目 828/828 全绿（Core 256→263）。
- **顺带清警告（同日续）**：MachineNetProviderTests 8 处——CS8602×5 补 Assert.NotNull（流分析+断言质量）、
  CS8625×2 Sample 假源参数改可空、xUnit1031 阻塞 GetResult 改 async——**全仓编译警告清零**，828/828 维持。

## 2026-10-10 装机对照清单（70 条三类，2026-10-12 十一次整理：修正加和漂移）

**口径**：`docs/mvp-issues.md` 中「待 Windows 装机」B-* 验收行 +「待装机走查」（批3-补8/9/11/12/13 验收行）
+ 补19/20/21/22/23 新增项 + 补27 增补 2 项（托盘 Explorer 重启恢复/唤醒补刷）+ 补28 增补 1 项（胶囊右键直达设置）+ 补31/32/33/34/35/36/37/38 新增项。2026-10-11 复点：
原「44+5+6=55」算术差 1，以实际类计数为准 = ✅ 已验 1 + 🔧 已修待验 16 + ⬜ 未修 53 = **70 条**。
（2026-10-12 十一次整理修正：未修表逐行累加实为 53，此前 50/51 系加和漂移——本仓第三次同型错误，详见批3-补43。）
另有 1 条关联项（批3-补10 透明度）用户已确认修好，列「已验」。装 nightly 后按此清单对照回执；带 CI 证据的项已注证据文件。
**装包版本门**：2026-10-10T17:30Z 后的 nightly（zip `a0b255bd…`，commit c880f5d）才含补22 三修复；
此前用户报「没有修复」时装的是旧包（见补22 nightly 归属核实）。

### ✅ 已验（1 条，无需再看）
| 项 | 位置 | 结论 |
|---|---|---|
| 悬浮透明度下限 0.4→0.2 | 批3-补10/批3-补13⑤ | 用户 2026-10-10 装机确认修好，保持别动 |

### 🔧 已修待验（16 条，装最新 nightly 重点看这些）
| 项 | 位置 | 修了什么 / 证据 |
|---|---|---|
| 真实 PAT 连接测试成功/失败路径 | B-301 | 批3-补14 修 2 真 bug（缓存键只用 Id 换 token 不生效；空 PAT 匿名 200 误报「连接正常」）；假 Handler 8 例 |
| 真实仓库 PR 手动验证 | B-302 | 夹具 6→18 例（批3-补14/15）；红 CI 有状态判据 warnOnRedCi（默认关） |
| 真实仓库红 CI 判据 | B-302 | 同上（304 续探/终态复用/摘要「N red ci」段） |
| 真实 workflow 手动验证 | B-303 | 端到端 8 例（批3-补14：Running/16min 卡住/时长格式/坏 JSON） |
| 真实 workflow 映射复核 | B-303 | 同上第二行口径 |
| 小米控制台口径 + GLM 5h 主位 | 批3-补8 | 小米需先录控制台 Cookie；GLM 直接生效（真 Key 夹具回归） |
| 悬浮框进度条 + 快捷面板底部 | 批3-补9/17/18 + 补23 | 进度显示 2026-10-11 用户令**重设计**：bar 翻回默认（4 DIP 圆角条 + NN% 读数，≥80% 黄/满格红）；池水降为可选旧特效（设置里用户自选）；面板 RECENT EVENTS 改 Grid 内滚 |
| 方舟额度用尽显示 | 批3-补11 | 尽窗优先级+429 耗尽特判；装后看卡片「额度用尽（窗名，重置时间）」 |
| 通知规则接线 | 批3-补12 | rulesProvider 一行接线（此前 config 自定义规则是空头支票）；自定义规则触发 Toast |
| UI/UX 审计 6 组可发现性修复 | 批3-补13 | 动作独立页/滚动区常驻/表单压缩/档位中文标签/双列 tile（⑤透明度已验）；设置页截图待看 |
| 吸边细条无法恢复 | 批3-补19 | 细条与展开面板同一 Place 数学（strip ⊆ panel 几何不变量+回归测试）；装后拖面板到屏边收起，悬停细条应展开不再死循环 |
| 5K/高缩放裁字（DPI） | 批3-补20 | DetailWindow 按内容定尺寸 + 10 处 DpiLayoutMath 唯一口径 + 四档 dpi 回归测试；装 200%/250% 看详情窗/悬浮框/胶囊字完整（CI 恒 100% 无法代验） |
| 拖动跟手 + 跨屏闪烁 | 批3-补20 | 胶囊改起点绝对定位（高回报率鼠标不再爬行）；拖动中跳过 WM_DPICHANGED 重排；装后慢拖胶囊并跨三屏看跟手与闪烁 |
| Moonshot 余额卡片 | 批3-补21 | 开放平台 Key（platform.moonshot.cn，与 Kimi 不通用）录入后看余额/阈值告警；设置模块行与连接表单就位；18 例单测 |
| 拖动跨三屏跟手 | 批3-补22 | 拖动锚点改光标屏幕物理像素（旧实现按下 DPI 与每帧当前 DPI 混算，跨缩放屏比例错乱）；装后慢拖胶囊/悬浮框跨三个缩放不同的屏看 |
| 详情窗 200% 仍裁字 | 批3-补22 | DetailWindow 是标题栏窗，Resize 补回非客户区差值（GetWindowRect−GetClientRect）；装 200% 开详情窗看字完整 |

### ⬜ 未修（50 条：无已知缺陷，代码就位，待装机首验）
| 项 | 条数 | 说明 |
|---|---|---|
| B-101 单实例/托盘/常驻 | 2 | 二次启动唤起气泡；Exit 才退出 |
| 批3-补27 托盘 Explorer 重启恢复 / 唤醒补刷 | 2 | 任务管理器重启 Explorer 后托盘图标自回（级别色不丢）；睡眠唤醒后 ≤1 周期出新数据（Win32 消息链路，只能装机验证） |
| 批3-补28 胶囊右键直达设置 | 1 | 右键胶囊弹设置窗；左键拖动/单击开面板不受扰 |
| 批3-补31 Jenkins 构建卡 | 1 | 对可达 Jenkins 实例建连接（匿名或 user:apiToken）+ job_path 组件：构建灯/构建号/构建中随刷新走 |
| 批3-补32 本机网络卡 | 1 | 建 machine 连接（零凭据）+ machine.net 组件：IP 与累计流量在 L3，首刷无速率、第二刷起 ↓↑ 速率随刷新走 |
| 批3-补33 本机 Git 卡 | 1 | machine 连接 + machine.git 组件：repo_path 填本机任一仓库，↑↓/改/stash 随刷新走；落后远端亮黄 |
| 批3-补34 MiMo 整行 Cookie | 1 | 整行 Cookie 粘进控制台凭据也能出数（自动取 api-platform_ph）；此前整行粘贴卡 offline |
| 批3-补35 Herald 两卡 | 2 | 对自建 herald 实例建连接：herald.incidents 未决计数/亮黄亮红随台账走；herald.feed 公开类目或私密 token，新条目未读数随刷新走 |
| 批3-补36 本机资源卡 | 1 | machine 连接 + machine.local 组件：CPU 首刷不出、第二刷起百分比随刷新走；内存/磁盘即时百分比；阈值调低（如 20）看亮黄 |
| 批3-补37 Herald 投递失败卡 | 1 | herald 连接 + herald.deliveries 组件：无失败显「无失败」；人为制造一次失败投递（停掉目标 provider 再 notify）看亮黄与失败计数 |
| 批3-补38 MiMo 401 指引 + LiteLLM 档 | 2 | ①故意填过期控制台 cookie：卡面明示「Cookie 已失效或未录制」+ 重录指引原文；②本机 LiteLLM 网关接 DB 后（现状 no_db_connection 是网关侧缺口）Settings 录 litellm_spend_url：卡显「本月已用 $X（网关记账）」 |
| 批3-补42 YAML Provider DSL | 1 | http 连接 Settings 配 dsl_path 指向声明文件：mapping 缺省出数（widget.Config 实例覆盖仍优先）；详情页出现 DSL 动作（仅限该连接组件），带 retry 的动作失败自动重试（消息带「已重试 N 次」）；坏 YAML 卡 Degraded 明示、启动不受影响；不配 dsl_path 的连接行为零变化 |
| B-102 胶囊位置记忆/DPI | 1 | 重启回位；跨 DPI 无错乱 |
| B-105 崩溃兜底 | 1 | 人为异常：进程存活+日志堆栈+无僵尸窗（代码路径已就位） |
| B-206 断网启动有数据 | 1 | 缓存优先水合+Last update+自动刷新（全周期单测已过） |
| B-401 Action UI 反馈 | 1 | 失败在 UI 有可见反馈（Runner 单测已过） |
| B-402 浏览器打开 URL | 1 | 模板渲染单测已过，浏览器手验 |
| B-403 Workflow Dispatch 全程 | 1 | 真实仓库 Run→运行中→终态可见 |
| B-404 本机命令确认弹窗 | 1 | 弹窗本体 UI（超时/落盘单测已过） |
| B-501 Toast 全链路 | 1 | 发送/点击/激活路由；快捷方式自愈 |
| B-503 托盘着色 | 1 | 聚合变化 ≤1 周期反映到托盘 |
| B-504 胶囊真实聚合 | 1 | 断网 Offline 横幅；计数一致 |
| B-601 Quick Panel 开关 | 1 | <100ms 打开；ESC/失焦关；键盘完整操作 |
| B-602 Overview/Pin 落库 | 1 | 计数/事件一致；右键 Pin 落库 |
| B-603 Detail + Retry 链路 | 1 | L2 一键开详情；[Retry] 全链路 |
| B-701 焦点/独立更新/悬浮形态 | 3 | 点击不断焦；两 tile 独立；拖放+重启还原 |
| B-702 Pin 双入口/准入 | 2 | 行为一致持久化；非准入不可钉 |
| B-703 位置/DPI 回收 | 2 | 重启还原；拔插显示器不丢；分辨率变小回收 |
| B-704 断网自愈 | 1 | 全流程无弹窗；恢复自愈（socket 级集成测试已过） |
| B-705 L0 铁律 | 2 | Critical 仅脉冲；RFC 四条清单 |
| B-706 状态色覆盖 | 3 | 级别色三面同步；colorOverride 单 tile；非法回退 |
| B-707 动效三档 | 3 | 过渡/脉冲/呼吸灯；不抢焦点；隐藏暂停 |
| B-802 导入导出端到端 | 1 | 重录密钥后与原机一致（存储层 5 例已过） |
| B-803 MVP 全流程走查 | 1 | 任务书 §成功标准录屏归档 |
| B-804 干净 VM 安装 | 1 | 下载→装→跑 B-803 脚本 |
| B-805 调色/动效设置页 | 2 | 即时预览持久化；重置默认；off 档全静止 |

---

## 附：MVP 明确不做（评审基线）

复杂插件市场 · Workflow DSL/引擎 · AI Agent · Jenkins/GitLab/Docker 全功能管理 · 数据库 · 云账号/用户系统/团队协作/云同步 · 复杂图表 · 复杂主题 · macOS/Linux。

### 批3-补40 MiMo 排障条目（2026-10-11 续批：用户实踩「小米的还是不行」后的文档面补缺）
- **背景**：troubleshooting.md 无 MiMo 条目，用户只能靠对话排障；本次修复单（批3-补38）落地后把自查路径写进手册。
- **内容**：新增「小米 MiMo 卡显示 Offline / 不出数？」段——胶囊 Offline 横幅=聚合告警（含 Degraded 401），真因在 tile 失败原因行；三种常见坑：Cookie 失效重录指引（F12 → api-platform_ph，整行粘贴也行）/ 只填 Key 不够（Key 只授权模型调用，两条凭据链）/ litellm 档默认空=不外发、no_db_connection 归属网关侧。
- **验收**：文档增量，审计台账 F-58（58 条）；无代码改动。

### 批3-补41 「后续可选：AI Usage Provider」可行性评估收口（2026-10-11 巡检点火优先席）
- **评估问句**：数据来源是否可靠（各厂商/网关 usage 接口有无稳定可查口径）？维护成本是否合理？
- **可靠性盘点（11 家三档）**：① 官方稳定 API 六家——deepseek `/user/balance`、moonshot
  `/v1/users/me/balance`、bigmodel 监控 `quota/limit`（三窗口百分比）、ark 管控面 OpenAPI（V4 签名，
  三窗+429 耗尽特判）、copilot 官方扩展同款配额端点（需两个集成头）、opencode Console Budgets
  （micro-cents 口径）——均 key 长效无会话态，口径稳定；② 本机零网络两家——claude/codex 读本机
  会话 jsonl（零凭据零网络，绝对可靠零维护；codex 明示「本地统计，非官方口径」）；③ 会话态两家——
  kimi/mimo 控制台口（端点稳定，2026-10-11 双双实抓真身修正解析）但会话过期是常态，卡面已给
  明示+重录指引；外加 qwen 官方额度口未开放——如实降级链（自定义端点→配额探针→模型目录）守住口径不造数。
- **维护成本实测**：全项目至今两次形状漂移事件（kimi F-50 社区实现假设≠真实响应、mimo F-51
  percent 0-1 vs 0-100），均被「每家单文件多类 + 防御解析 fail-loud + 如实降级（认不出显式
  Degraded 不冒充）」隔离在单文件、当日收口；刷新失败不清缓存（master-plan §14.5），单家漂移
  不伤核心桌面体验。当前 13 种 usage 组件类型全部带表驱动单测，828/828 绿。
- **结论：做——且已实质完成，项关闭**。本项是 master-plan §13 写下的条件门控占位（§4.1 架构图注
  「可以在未来以同样的方式接入，但不影响第一版」）；positioning P0 #5 早已将其提为首发 P0 并逐家
  落地，条件「数据源可靠 + 维护成本合理」在每家落地时都过了一遍门（不可靠者也以降级链守住如实口径）。
- **处置**：todo.md 勾选关闭（附依据）；master-plan §13 该节补落地状态注记（同 批3-补31 先例）；
  positioning §8 已有逐家清单不重复。厂商私有打包 Provider 维持不动（用户令）；网关接 DB 仍等用户。

### 批3-补42 YAML provider DSL 落地（2026-10-12 续批：positioning §6「计划中」项 → 已实现）
- **口径**：positioning §6 原设计为准——per-provider `mapping` 声明式字段映射 + `actions`（含 retry）动作配置；**可选声明层**：连接 Settings 配 `dsl_path` 才启用，不配置即零变化（http.status/http.quota 既有 widget.Config 路径逐字节等价）；mapping 只管数据提取缺省（路径/词表/单位），widget.Config 实例值优先，label/icon/阈值不进 DSL；只做开源标准 API 侧（http 连接），冻结范围未触碰。
- **实现**（零新第三方依赖，master-plan §15 最小改动）：`Beacon.Connections/ProviderDsl.cs`——文档模型 + 校验（顶层键白名单 / mapping 键白名单 / action 必填与类型白名单 open.url|http|webhook / confirm 默认按类型 / retry 范围 1–10 与 0–300，全部 fail-loud 带行号）+ `YamlLite` 极小 YAML 子集解析器（缩进映射 / `- ` 列表 / 引号 / 行内注释；flow、块标量、Tab、多文档、重复键、未闭合引号全 fail-loud）+ LoadFile mtime+size 缓存 + `EffectiveConfig` 合并视图（DSL 为底、实例覆盖）。`HttpStatusProvider`/`HttpQuotaProvider` 数据提取键改走 EffectiveConfig（dsl_path 未配返回原字典，行为不变）。`ActionRunner` 支持 `retry_max_attempts/retry_backoff_seconds`：默认 1 次=不重试（既有单次路径消息逐字节不变），重试共享同一总超时预算，用户取消/超时/退避被打断不重试，多次失败消息带「已重试 N 次」标注。DSL 动作经 `ToActionConfig`（Id=`dsl.{连接}.{id}`，connection_id 限定）进既有执行器链路；`BeaconRuntime.DslActions` 每取现算（坏文件记警告跳过，不影响启动与其他连接）；`Beacon.Core/ActionVisibility.cs` 纯函数统一显隐（widgetType 逗号多值 + connection_id 限定）；DetailWindow 动作区合并 actions.json 与 DSL 两来源。
- **验收**：新增单测 60 例全绿——ProviderDslTests 46（解析全表 / 校验 fail-loud / mtime+size 缓存 / MappingValue 次序 / 三端到端：DSL 缺省生效、实例覆盖、缺文件 Degraded、quota 路径+单位）+ ActionRunnerTests 6（重试成功 / 耗尽标注 / 默认单次逐字节 / 非法参数钳位 / 退避中取消 / 超时打断退避）+ ActionVisibilityTests 8；四项目 880/880 绿（Core 277 / Connections 553 / Storage 31 / Actions 27），非 App 工程 0 警告；windows-latest CI 绿后交付（Beacon.App 两处接线 Linux 无法本地编 XAML）。
- **文档**：positioning §6「计划中（未实现）」→「已落地（批3-补42）」+ YAML 示例与口径；configuration.md 新增 dsl_path 行 + 「Provider DSL」参考节；solution-structure.md 880/553/277 计数与文件清单（Connections 31、Core Services 18）；装机清单 67→68。

### 批3-补43 装机清单可执行化 + 加和漂移修正（2026-10-12 续批）
- **背景**：`acceptance-checklist.md` 停在 44 项一页打勾版（2026-10-10），后续 24 项新增无人回填；用户要求改成可执行文档。
- **内容**：重写为 70 条三段式——每条含**前置条件 / 验证步骤 / 预期结果**，配通用准备包（E0-E2 环境、C-GH/C-HTTP/C-MACHINE/C-HERALD/C-MIMO/C-JENKINS/C-MOONSHOT/C-DISCONNECT/C-DPI 连接与工具），按 A 安装进程 / B 数据源 / C Actions / D 通知 / E L2L3 / F L0 / G 外观动效 / H 导入导出设置 / I 总验收 分组；3 条例外项（B-301 匿名补充、SHA256 复核）标注「配套不计入」，F2 标注为 B-701 台账条 1 的子拆分，账目行 53 全覆盖（逐行映射核对）。文末保留并扩充「本地自动化覆盖注记」，新增批3-补14/15（GitHub 假 Handler）、补42（DSL 46 例）已证面。
- **加和漂移修正**：未修表逐行累加实为 **53** 条（原表头 51、总数 68 错）——本仓**第三次**同型错误（前两次：原 44+5+6=55 差 1；十次整理时只 +1 未重算）。现计数 = ✅1 + 🔧16 + ⬜53 = **70 条**。口径固化：清单类计数一律逐行累加校验，不得按上次值 +N 平移。
- **验收**：文档增量，无代码改动；`docs/index.md` 导航同步；CI docs 流程绿。

### 批3-补44 文档收尾复扫（2026-10-12 巡检续批）
- **复扫面**：todo/mvp-issues 未完项（全装机或用户输入卡点）、审计台账 F-01~F-58（全收口）、mkdocs nav（14 项齐）、src TODO/FIXME（零）、README/README.zh/mkdocs/troubleshooting 陈旧引用。
- **发现与处置**：① README.md 路线行仍指「44-item acceptance checklist」→ 改 70 条可执行版；② README.zh.md 路线行缺装机清单链接 → 对齐 en 版补链接；③ troubleshooting.md 补「DSL 声明文件（dsl_path）报错？」条目（解析器子集三类踩点：Tab/JSON 引号/白名单外键 + 文件不存在 + mtime 缓存生效时机——MiMo 先例，配置坑无文档就多一轮排查）。
- **验收**：文档增量无代码改动；grep 全仓无「44 项/44-item」残留；CI 绿。

### 批3-补45 本机组件可选展示项 + 悬浮框宽度按值行推算（2026-10-12 用户实测反馈）
- **背景**：用户实测两条——① machine.local（cpu/内存/磁盘）、machine.net（上下行带宽）在悬浮 tile 值位被省略号截断（本机类型无 value_text 映射，跌进 Summary[..8] 兜底再叠 168 DIP 窗宽不够）；② 要求本机组件能在配置里多选展示项。
- **①可选展示项**：组件 config `show` 逗号分隔多选（local：cpu_percent/mem_percent/disk_percent；net：down_bps/up_bps/ip），留空 = 全显（默认版式逐字节不变，存量零迁移）。解析在 `MachineShowOptions`（大小写不敏感/去重/规范序；未知键与选空 fail-loud Degraded，与阈值同口径）。三处同源的根在 Provider：未选中的键不出 payload——悬浮 tile 数值位（新增 `value_text`，WidgetValueHint 首优先键）、快捷面板 Summary、详情页 payload 表自然一致省略；原始台账键（mem_used/total_sent 等）不参与选择恒留详情页；超线判黄只看选中项。DetailWindow 跳过 value_text 行（tile 提示键不重复占行）。改 show 下次刷新即生效免重启（ReloadTiles 对已存悬浮窗补排尺寸）。
- **②宽度按值行推算**：`MachineTileSizing` 按所选展示项的最坏值行（百分比顶格 100、速率顶格「999.9 MB/s」= FormatRate 最大长度形态、IP 顶格 15 位；net 数值位速率行/IP 行互斥取宽者）× 保守字宽模型（ASCII=7、CJK=11、%=10、箭头/·=11、空格=4）+ 固定件 74（padding 20/灯 8/icon 16/列间距 30）+ 标签实测宽 + 取整余量 4，下限 168。同口径三处一次改齐：面板列定义 / PanelPxSize / WM_NCHITTEST HitWidthPx（`PanelWidthDips()` 取钉选 tile 最大值）与独立悬浮窗 ApplyDesignSize（按组件各算）。默认全选下 local = **302 DIP**、net = **262 DIP**（原 168）。
- **验收**：新增 MachineShowOptionsTests 13 例 + local/net provider show 过滤 9 例（未选中不占位/不判黄/value_text 同源/首刷占位符/无网卡提示）；全绿 910（Connections 575，553+22 逐项复核）。

### 批3-补46 「关于」页补全：许可证 + 项目主页/检查更新入口（2026-10-12 巡检续批·优先席位）
- **立项依据**：master-plan §10 审计补记（2026-10-11）明确「开源许可证、检查更新/项目主页入口未做」——设置界面四类内容里唯一未落地面，不需要 Windows 真机即可实现。同轮排查其余「未做」标记：指纹级去重/事件合并（§5.4 注记）已由用户令「待真机验收后排期」挂账不动；第二阶段冻结面维持冻结；通知点击深链（§6.3「点击定位到事件」）已落地（ToastActivation widget→L3 + DetailUrl 兜底）。
- **内容**：设置 → 关于 = 版本行（原有）+「开源许可证：Apache License 2.0（见仓库 LICENSE）」+「项目主页」「检查更新」两枚外链按钮（`github.com/cuihairu/beacon` 与 `/releases/tag/nightly`，Process.Start UseShellExecute 系统浏览器，与 open.url Action 同口径；失败落运行日志不弹窗）+ nightly 滚动更新说明行（无正式版发布、覆盖安装后托盘 Exit 重启）。检查更新口径 = 打开 nightly 下载页人工比对，不自动下载安装（版本发布红线：正式版由用户手动触发）。
- **验收**：App 程序集 Linux 不可编译（既有口径），编译门 CI windows-latest；本机 4 测试项目全绿 910。

### 批3-补47 检查更新真比较 + 版本口径统一（2026-10-12 用户令，接批3-补46）
- **背景**：批3-补46 的「检查更新」只是打开 nightly 下载页的链接按钮，没有版本比较；`Beacon.App.csproj` `<Version>0.1.0</Version>` 落后于仓库最新正式发布 v0.1.2（2026-10-12 Latest）。用户令：口径统一 + 真检查 + 失败降级 + 比较逻辑进 Core 可 Linux 单测。
- **版本口径（读 .github/workflows 确认）**：csproj `<Version>` = **唯一权威基准**（FileVersion/AssemblyVersion 同步 0.1.2.0；InformationalVersion 由 SDK 派生，关于页展示用）。每日构建 daily-build.yml 发布时注入 `-p:Version=yyyy.M.d -p:InformationalVersion=$version+$env:GITHUB_SHA` 并滚 `nightly` tag（`--latest=false`，不冒充 latest）——**仓库无正式发版 workflow**（仅 ci/daily-build/docs），正式发版时的 tag 注入待用户首次令发版时再建（红线：版本号只能用户手动给）。nightly 日期版本（2026.10.9+sha）在 SemVer 比较中数值高于 0.1.x → 滚动 main 如实报「已是最新」，无需特判。
- **实现**：`Beacon.Core/Services/UpdateCheck.cs` 纯函数——`Parse`（v/V 前缀、+构建元数据剥离、-预发布段、1–3 段全数字、前导零/符号/空段 fail → null）、`Compare`（SemVer 2.0.0：主干三元组 → 正式版>预发布 → 数字段按值/字母段按序数/数字<字母 → 前缀相等段多者大）、`Evaluate`（任一侧不可解析 → Incomparable）、`ExtractTagName`（System.Text.Json 取 tag_name，限流体/畸形 JSON → null）。`Beacon.App/Services/UpdateCheckService.cs`——静态 HttpClient（User-Agent 必带，缺失 403）、10s 超时链、SemaphoreSlim 串行、进程生命周期缓存 `Last`（启动 20s 后自检一次 force=false 命中缓存不重打；手动按钮 force=true）；所有失败（断网/超时/非 200/限流体）→ `UpdateCheckOutcome.Failure` 折返 + 日志，绝不向 UI 抛异常（Offline 口径）。设置 → 关于三行：当前版本（InformationalVersion 含 +SHA）/ 远程最新版本（tag_name，未检查显示 —）/ 检查结果（已是最新 / 有新版本 vN + 「打开下载页」按钮 / 无法检查更新 + 时间）+ 最后检查时间行；比较只认正式发布（`releases/latest` 天然排除 prerelease/draft，nightly 不参与）。
- **验收**：新增 UpdateCheckTests 48 例（解析形态/拒绝表、SemVer 12 组比较、Evaluate 含 nightly 日期口径、限流降级体）；本机四项目 958 全绿（Core 325 = 277+48 逐项复核 / Connections 575 / Actions 27 / Storage 31）；App 编译门 CI windows-latest。

### 批3-补48 tile 宽度真字宽预算（全类型）+ 多窗口额度展示（2026-10-12 用户令二修）
- **背景**：用户复测悬浮框信息**还是放不下**。批3-补45 的字符数常数估宽（ASCII=7/CJK=11）只覆盖 machine 且偏小（Segoe 大写字母 0.6–0.9em 被按 0.5em 折算）；最初需求的多窗口额度展示（bigmodel 三窗/kimi 双窗）一直没落码。用户拍板：① 宽度换真字宽预算、适用**所有** tile；② 多窗口渲染 `5%/5h 7%/w 8%/M`（kimi 剩余口径前缀「剩」），进度条仍取最小可用窗口不变，多窗口时收起重复的「NN%」进度读数。
- **①TileSizing（Connections 新文件，MachineTileSizing 收编其内）**：`TextWidthDips` 按字号逐字符类别折算（em = 列 FontSize；数字/ASCII ≈0.62em、% ≈0.9em、空格 ≈0.36em、箭头/·/…/CJK ≈1.0em，标签列 SemiBold ≈0.66em——系数取 Segoe 实测上界，「数值列不裁字」硬口径优先于系数精度）→ `TileWidthDips` 对 tile 每列分别预算（标签列 = 最长 label、数值列 = 该类型最坏 ValueHint 文本、进度读数列 = 34 DIP）+ 固定件 44（padding 20/灯 8/icon 16）+ 列间距 6×(列−1) + 余量 4，下限 168。**所有类型同一路径**（machine 值行按所选 show 构造、额度类按各自最坏文本、无 hint 键类型按 7 CJK+… 兜底），面板列宽 = 钉选 tile 最大值，独立悬浮窗按各自组件——`PanelWidthDips`/`ApplyDesignSize` 同口径三处不变。每类型最坏值行 = WidgetValueHint 优先级链可达形态的并集取最宽（多窗三窗顶格 "100%/5h 100%/w 100%/M" = 147 DIP、mimo 四路径取最宽本机计数、machine 默认全选 "CPU 100% · 内存 100% · C: 100%" = 197 DIP）；多窗口类型最坏形态读数收起不占预算（单窗形态值行骤窄 147→31，腾宽远多于读数 40）。
- **②多窗口额度（WidgetValueHint）**：payload 同口径窗口键（bigmodel/ark 已用 rolling/weekly/monthly_percent；kimi 剩余 rolling/weekly/monthly_remaining_percent）任意两个以上非空 → `NN%/5h NN%/w NN%/M`（窗序 5h→周→月、只出实际存在的窗口、空串=缺）；单窗走既有优先级链不变。`SuppressProgressReadout` 供 PinTile 收起读数；进度条与判级语义零改动（provider 现有 Progress = 最小可用窗口同源）。
- **验收**：新增 TileSizingTests 32 例（字宽模型逐类/每类型最坏值行→合计宽/标签优先级与 PinTile 同源/machine show 联动）+ WidgetValueHintTests 多窗口 8 例；MachineShowOptionsTests 缩回 show 解析 5 例（宽度测试移 TileSizingTests）；本机四项目 990 全绿（Connections 599 = 575−8+32 / Core 333 = 325+8 / Actions 27 / Storage 31，逐项复核）；App 编译门 CI windows-latest（PinTile/FloatingTile 两处接线 Linux 不可编 XAML）。
- **文档**：configuration.md「多窗口额度展示」节 + TileSizing 改名引用；solution-structure.md Connections 33 文件 + 测试计数 990。

### 批3-补49 hint 链缺口收口：moonshot/qwen 用尽/machine.git 数值位（2026-10-12 巡检点火）
- **起点**：批3-补48 回报挂账的 moonshot.balance 缺口 + 用户令「自查各 provider 的 hint 链/功能面还有无可自主推进项」。全量对照 16 家 provider 的 payload 键与 `WidgetValueHint` 优先级链，补三处、其余确认无缺口（deepseek granted/topped_up、codex sessions、http.quota used/remaining、jenkins/github.actions/herald 状态键 = 台账/状态型，Summary 即显示，维持兜底口径）。
- **①moonshot.balance**（用户倾向补一行映射 → 拍板落地，决策记录入 todo.md）：hint 链加 `available_balance` → `¥{value}`。理由：数值全没的截断兜底正是 hint 链立项要消灭的失败模式；口径：固定 CNY（平台只按 CNY 计价，Summarize 已硬编码 ¥），宽预算 `¥99999.99` 与 deepseek 同款 → moonshot tile 218 → **192**。
- **②qwen.usage 用尽路径**（网关 429 配额探针档）：payload 补 `value_text="已用尽"`——原兜底 `Summary[..7]` = 「千问 · 配额…」丢「用尽」语义，比照 mimo「无额度口」显式文本先例；qwen 最坏值行 31 →「已用尽」33 → tile 207 → **209**。
- **③machine.git**：provider 补 `value_text` = Summary 计数段（`↑A↓B · 改 N · stash M`，同源构造与 machine.local/net 的 value_text 先例一致）——原兜底把 ↑↓/改/stash 计数全截没（「beacon …」），是补45 同族 bug；最坏值行 = 五数顶格 250 → tile 226 → **340**（标签 = 类型尾段「git」）。
- **④同轮自查揪出五处「最坏值」低估并抬预算**：http.quota percent = used/total 计算值**不封顶**（注释明示「可超 100 供严重度判定」）→ 最坏 `"100%"` 改 `"999.9%"`（191 → **204**，读数列来自 Clamp 后 Progress 恒 ≤100 不受影响）；`FormatCount` 的 M 形态尾数无上界（无 B 档）→ codex/claude/mimo 计数形态尾数顶格 `9999.9`（≈百亿 token，generous 口径与 PR 5 位/金额 6 位同档）：codex 172 → **179**、claude 成本/token 两形态取宽者 168 → **187**（token 形态顶开后不再触 168 下限）、mimo 244 → **251**；`FormatRate` 档内尾数上限是 **1023.9** 非 999.9（≥1024 才换档），千兆/万兆网卡 MB 档 6 位尾数真实可达 → machine.net 最坏值行 157 → 171（net 默认 247 → **261**）。qwen percent 已 `Math.Clamp(0,100)` 核实不需抬；kimi 月窗键是预留（provider 今天不产出）预算维持双窗并注记「接口加月窗时同步抬一行」。
- **验收**：Core +1 用例（moonshot ¥110.00）；TileSizingTests qwen/moonshot/git/http.quota/codex/claude/mimo 七处按新形态重算；QwenUsageProviderTests 用尽用例补 value_text 断言、GitWorkspaceProviderTests 两用例补 value_text 断言；四项目 991 全绿（Core 334 / Connections 599 / Actions 27 / Storage 31，逐项复核）。另修两轮 CI 红（都因 Linux 本地编不到 App 漏网）：ab9649b 遗留 `SettingsWindow.cs:608` 插值内三元未加括号（CS8361，已括号修复随本单上 CI）、随后编译器过 608 行才暴露 `BeaconRuntime.UpdateCheck` 属性 public 而 `UpdateCheckService` 为 internal 的 CS0053（服务类改 public，与 BeaconRuntime 暴露的其余服务类 public sealed 口径一致），第三轮同类：服务类转 public 后其 `Last`/`CheckAsync` 又把 internal 的 `UpdateCheckOutcome` 暴露成 CS0053/CS0050，结果 record 一并转 public（成员类型 string?/DateTimeOffset/bool + Core public enum Verdict，全公开面干净）。可访问性链只能人工核对：实测 Linux 上 XamlCompiler 在 Csc 之前就中止（放必炸探针照样零 error CS、无 dll 产物），本地没有 C# 门禁，CI 是 App 唯一编译关。

### 批3-补52 tile/胶囊底部裁切修复：外框/客户区尺寸混用（2026-10-14 用户缺陷单「底部进度条显示不全（还是一样的问题）」，commits 02a5de1 + 4ae613d/7e4c2b0/f4b0eb1 收口）
- **取证**（run 38488861749 工件逐像素）：AppWindow.Resize 设的是窗口外框，PinnedHostWindow 面板/细条、FloatingTileWindow、CapsuleWindow、QuickPanelWindow 四处全把客户区需求尺寸（行×32 DIP / 内容实测）直接喂 Resize——无边框 OverlappedPresenter 仍保留 ~6px 非客户区 resize 边框，每窗底部 ~6px 落进不可见区（tile 设计 32px 实测客户区 26px，底部 4 DIP 进度条整条不可见；胶囊与快捷面板同型裁字）。同型先例：DetailWindow c80c244（2026-10-11 装机「没有修复」根因，批3-补22）。CI 旧条带断言 vacuous：裁外框矩形、底部 6 行落在窗框外桌面区，「465/507 像素」全是桌面边缘，零渲染也通过。
- **修复**：ChromeSizePx（GetWindowRect − GetClientRect，取不到兜 0）收编 NativeMethods 供五窗共用（DetailWindow 私有副本同收），Resize 一律补回差值；CapsuleWindow._sizePx 改记外框尺寸（save/restore 锚点数学同口径不漂移）；QuickPanel 定位按外框口径保 24/140 DIP 可见边距；SettingsWindow 构造期同型（记忆尺寸存客户区 DIP 直喂外框，逐次开窗矮一标题栏）一并修复。CI 断言同步去 vacuous：条带裁图改锚客户区（裁屏图经 ClientToScreen 换算屏坐标——GetClientRect 返回窗内坐标），新增 Assert-ClientHeight 硬断言（客户区高=设计值 ±1，期望值按窗 DPI 缩放与 DpiLayoutMath.ToPx 同取整）。
- **验收**（run 38500538817 全绿含发布）：场景 A client 32px/frame 38px（chrome 6px 实证）+ 条带 620 像素（原 465=桌面边缘）；场景 D 五悬浮窗全 32px + ark 条带 676 像素（原 507）；场景 H5 面板 client 64px=2 行×32（原 58）。Assert-ClientHeight 为永久回归守卫。GLM tile 空态收起与有值路径同一行高预算（缺陷在窗口层不在 tile 层）；完整机制记录见 todo.md 决策记录同名条。
- **台账注记**：批3-补50（2026-10-12 装机清单 CI 代验转化：场景 E/F/G/H/J）与批3-补51（G 系色彩断言阈值逐像素重标定，run 38261911354）的完整记录在 todo.md 头注与决策记录，不在本文件重复展开；本文件 B-xxx 验收行勾账已按 todo.md 同步（B-101/102/104/206/401/404/603/701/704/706/805 各行，勾账证据随行注记）。

### 批3-补53 看板过滤 + 多语言机制 + 贴边收起开关 + 托盘菜单语义分化（2026-10-14 用户需求+缺陷单四条，commit e905a48 收口）
- **单① 事件看板默认全部 + 可点击过滤**：落点 Beacon.Core/Services/QuickPanelFilter（widget.Type → 源键，github.actions.runs 单列 CI 与 PR 计数区分；Matches=活动源为 null 即全过）。QuickPanelWindow._activeSource 单一活动源贯通四区：通知/需要关注/运行中/最近事件全按 Matches 过滤（通知区经 record.SourceWidgetId 反查 widget.Type 归源）。可见态三重：活动 chip 实心高亮（RGB 255,37,99,235 白字）+ 概览标题「OVERVIEW · {源}」后缀 + 恒在「全部」chip（AutomationId chip-all）；点活动 chip 再点即回全部（ApplySourceFilter 翻转 null）。默认态 _activeSource=null=全部，不隐式记忆。
- **单② 多语言**：先核机制——本仓原无 i18n（src/ 与 docs/ 全量 grep Localization/LocalizedString/I18n 零命中，唯一近邻=WidgetTypeNames 中文短名硬编码）。「不另起一套」的落实=建全仓唯一一套 Beacon.Core/Services/UiStrings：en/zh 双字典 18 键；SetLanguage（""或非法=跟随 CurrentUICulture 双字母、invariant 兜 en）；Get 回退链 当前→英文→键名；LanguageChanged 事件（同值幂等）；Keys(language) 供两语言键集奇偶测试。覆盖面=看板全文案+托盘菜单+设置页新增两控件（语言下拉/贴边收起开关）。接线：App.xaml.cs:158 启动即 SetLanguage（zh 系统开箱即中文——修「看板没有中文」）；SettingsWindow.cs:573 语言下拉（language-box：自动/中文/English）保存即 SetLanguage 广播，QuickPanelWindow.cs:69/:191 订阅重画（即时生效）；托盘菜单右键现建现取词（TrayIconService.cs:256-264）。范围注记：设置页存量中文文案未迁移（后续项）。
- **单③ 贴边收起开关 + 坍缩难展开**：默认值拍板=开（AppConfig.EdgeCollapseEnabled=true）——理由：保持已发布行为（无开关时代即默认开），翻转默认=行为变更须用户明令（先例 NumericFloatingEnabled 2026-10-11）；开关本身解决「失去控制」主诉。开关链路：SettingsWindow.cs:603 edge-collapse-toggle → 保存 → ApplySettingsEffects（App.xaml.cs:316）→ PinnedHostWindow.ApplyEdgeCollapseSetting（:820，关=已收起立刻弹回+落盘；OnLoaded :1059 兜手改配置残留收起态）；FinalizeDrag:976 收起判定加开关门控（关=拖边只吸附不收起）。「难展开」根因=点击展开原只设 _hoverExpanded（临时预览语义），指针移出立即回缩——体感「点不开」；修复=_stickyExpanded（:548/:996 点击细条=粘性展开，鼠标移开不回缩；拖动重判 :978、拓扑 Reconcile :843、开关关闭 :827 三处清除），悬停保持临时预览。可复现操作：拖面板贴死屏幕边松手→收 12 DIP 细条；点细条任意处→展开并保持；拖离边缘→保持展开；设置关开关→已收起立刻弹回、再贴边不收。
- **单④ 托盘菜单 Open Beacon / Notifications 语义**：设计意图取证——docs/mvp-issues.md:266 B-503「托盘菜单含常用 Action 与 Notifications 入口」+ 原 App.xaml.cs:174 注释「B-503：Notifications 入口 → L2 通知中心视图（148c3bd 落地）」：本应不同（Open Beacon=面板开关、Notifications=直达通知中心）。缺陷=两事件同接 quickPanel.Toggle（原 App.xaml.cs:175 vs :159）→ 菜单两项同效=同义入口。处理=语义分化（保双入口，不删）：Open Beacon 维持 Toggle（App.xaml.cs:162）；Notifications 改 OpenNotifications（App.xaml.cs:180 → QuickPanelWindow.cs:172：Show+Activate+滚动至最新通知+焦点落通知列表）；两文案均入 UiStrings（tray.openPanel / tray.notifications）随语言切换。
- **验证**：四测试项目 1078 例全绿（Core 368 含新增 QuickPanelFilterTests 6 + UiStringsTests 8；Actions 27；Connections 652；Storage 31）；daily-build 场景 W（visual-evidence.ps1 :3390 起本批新增）：W1 预置 language=zh 启动看板文案中文（通知/全部已读/概览）、W2 设置切 English 重开面板见英文+config 落盘 language=en、W3 点 chip-智谱 → OVERVIEW 头「OVERVIEW · 智谱」+ 点 chip-all 回纯 OVERVIEW、W4 贴边收起开关 off→on 落盘 edgeCollapseEnabled false→true。四跑取证（三红皆脚本/取证锚问题，产品行为本身绿——W1 每跑必过）：① 38525961627：W1 绿、W2 红=脚本缺陷（面板失焦即隐藏 QuickPanelWindow.cs:123-131 Deactivated→Hide 设计行为，隐藏态读 UIA 空文本；语言切换本身成功：config 落盘 en，隐藏期经 LanguageChanged 广播完成重画）、J 红=级联（W2 throw 跳过 W 的 Stop-Beacon，静默安装撞运行中进程）→ 修复 35bcfe2（W2 改切换后重开面板验英文 + W catch/J 安装前补 Stop-Beacon）。② 38533804493：W1/W2/J 全绿；W3 红=W1 缓存 $panelRoot 在 W2 隐藏/重开+全量重画后失效 → b2f4bdd（W3 两处重取根：点 chip 前 + ApplySourceFilter 重画后找 chip-all 前——重取根本身仍必要，过滤点击会重建树）。③ 38542642112：W3 仍红——真根因=chip AutomationId 设在 Border 上，Border 非 Control 无自带 peer 不进 UIA 树（失败截图实证 chip 在树外可见：All 2/DeepSeek 1/智谱 1）；与 settings-left-splitter 同型坑（CI 38043812483 先例，SettingsWindow.cs:305 注记）→ 修复 23660f8（MakeChip 补 SetName 强制创建 FrameworkElementAutomationPeer，Name=可见文本「{label} {count}」）。④ 38549671960 终证全绿。细条点击粘性展开手势属 SendInput 12 DIP 精度面半转装机（脚本 :3394 注记；收起/展开宽度回归由场景 P 存量断言守卫）。

### 批3-补54 胶囊级别色同步 CI 代验（2026-10-14 巡检纠「胶囊像素不可取证」误判，commit 37297ec 收口）
- **误判来源**：todo 行 67 原记「胶囊与托盘像素不可脚本取证，需装机半」——胶囊半实为误判：胶囊 OverallLight（CapsuleWindow.xaml:19，Ø9 DIP Ellipse）与 tile 灯同一取色链（CapsuleWindow.cs:356 → UiPalette.SeverityColor 每次渲染实时读 config appearance.SeverityColors），批3-补51 已标定的像素断言机制（Count-ColorPixels + 轮询等渲染 tick）直接可复用，无需装机。
- **落点（仅 CI 脚本）**：visual-evidence.ps1 G3-G6 播种改 `Write-GConfig "" "off" 1.0 $false $true`（showCapsule=true；按 Q 场景既有标题「Beacon Status」找窗；胶囊只存活 G3-G7 会话，L 场景重播种回 false）；G4 tile 橙断言后补胶囊半（≤10s 找窗 + ≤20s 轮询 255,136,0 tol45 阈值 8px，工件 G4-capsule-orange-live.png）；G5 tile 绿回断言后补胶囊绿回（63,185,80 tol45 阈值 8px，工件 G5-capsule-green-back.png）。前置风险预清零：胶囊默认右下角 vs tile 右上角级联无重叠（CapsuleWindow.cs:248-253 vs FloatingTileWindow.cs:336-338）、UiOpacity 默认 1.0 无混色、motion off 无呼吸调光。
- **验证**：run 38561621450 全绿——G4 胶囊换橙 57 px、G5 胶囊回绿 61 px（阈值 8，失效模式=0 px）；同跑 tile 380/380 px 与 Q/R 胶囊场景不受播种改动影响照常绿。
- **遗留**：托盘图标同步变色半维持装机（系统托盘 16×16 HICON 由 shell 渲染、无稳定取证锚点），行 67 保持未勾。

### 批3-补55 右键 Pin 全链 + 两入口一致性 CI 代验（2026-10-15 patrol P1014C 纠「右键 Pin 无 UIA 入口」误判，commits ce6d3b7 + cdd05ad + 7822aae 收口）
- **误判来源**：todo 行 55/60 原记「右键 Pin 无 UIA 锚点/无 UIA 入口，需装机」——面板行 ContextFlyout 实为可达：行级 Flyout 挂在 ListViewItem 上，右键落点（含子 TextBlock 后代）即触发；MenuFlyoutItem 是带原生 peer 的 Control，右键后从面板窗口根 `FindFirst(Descendants, PropertyCondition(NameProperty, "Unpin from desktop"))` 命中，`InvokePattern.Invoke` 即点击（坐标注入完全不依赖 UIA 树）。
- **右键合成口径**：SendInput 按钮注入（MOVE|ABSOLUTE|VIRTUALDESK 0x8001 + RIGHTDOWN 0x0008 + RIGHTUP 0x0010，绝对归一化坐标与 DragMouse 同口径）在 CI 会话生效；mouse_event/SetCursorPos 合成输入已在 38044411443 实证死路。键盘 SendInput 同机制（K3 Esc 先例）。
- **落点**：visual-evidence.ps1——WinEnum `RightClick(x,y)` 助手 + `Find-PanelRow` 助手（RecentList 行锚定）+ 场景 X（X0 基线 2 tile（GLM+DeepSeek 播种均 pinned:true）→ X1 面板右键 Unpin → widgets.json GLM pinned 翻转 + 悬浮 GLM tile 移除；X2 重启 --settings → 设置钉开关读态 Off → Toggle On → 同链落库 true + tile 回归；X3 再重启读态 On + 行文本带「已钉」+ tile 回建）。SettingsWindow.cs 钉开关补 `AutomationId=pin-toggle-{widget.Id}`（ToggleSwitch 自带 peer，与 chip/appearance-color-success 同口径，不需 Border 式 SetName）。widget 行在 provider module 页内，须先 Invoke `module-bigmodel` 行才现出（D 场景同型）。
- **两机制坑（红跑实证，均非产品缺陷）**：① `InvokeableListItem` 不报 `ControlType.ListItem`——`FindAll(Descendants, ControlType==ListItem)` 零命中（38571373088，失败截图里行明明渲染着，极易误诊为面板没开）；行锚定必须 `TrueCondition` + `.Current.Name -match` 匹配后沿控制视图**限深向上找 ListItem 祖先**取整行矩形，无 ListItem 时停在命中元素自身（绝不越界到 ListView 整框）。② 悬浮模式（pinDisplayMode: floating）unpin 即从面板注销 widget（无 RecentList 行），面板侧架构性无法再右键回钉——**反向（钉回）必须由设置入口驱动**，恰是更强的两入口一致性证据。
- **验证**：run 38590456994 全绿——X1 右键链（ContextFlyout 弹出 + Name 命中 + Invoke + 落盘 + tile 移除余 1）、X2 重启后设置钉开关读态 Off 断言成立再 Toggle On（落盘 true + tile 回归 2）、X3 重启持久化（读态 On + 行标已钉 + tile 回建）；两入口同一落库链 `QuickPanelWindow.PinWidget`（Config.UpsertWidget + ReloadWidgets）实证，勾行 55/60。
- **遗留**：行 61（非准入类型两处入口均不可钉）维持未勾——面板侧 Pin 菜单项常挂+静默空操作的门控改造（WidgetDisplayPolicy.PinEntryVisible，PanelCarries 改调）在并行会话在飞，待其落地后随批转化。

### 批3-补56 非准入类型两入口不可钉 CI 代验 + X1 抗 flake 加固（2026-10-15 patrol P1014D 纠「需真实右键交互，CI 不可达」误判，commits 803329a + 67f144f 收口）
- **误判来源**：todo 行 61 原记「需真实右键交互，CI 不可达」——批3-补55 场景 X 已证右键合成可达；且「不可钉」的证据面取「无入口」比「点了没反应」更强：设置入口 RebuildWidgets 按 `_scopeDescriptors` 过滤类型（未登记类型整行不产，连行标都不出），面板入口 WidgetHost 对未登记类型 Resolve=null 静默跳过（无状态即无 tile 无行，行级 ContextFlyout 无宿主），FloatingTileHost.ReloadTiles 的 IsPinSupported 门同样拒绝建窗——两处钉面结构性不存在。
- **落点（仅 CI 脚本）**：visual-evidence.ps1 场景 Y（X 后 J 前）：播种 widgets.json 单条未登记类型 `unknownghost.usage:Ghost` + pinned:true + connectionId 指已存在的 zhipu（连接命中才走到 Resolve 分支，JsonConfigurationStore.LoadFile 纯反序列化不剪除未知类型）。Y1 启动 ReloadWidgets(null)/scheduler.KickWidget 同步触发刷新链 → 「No provider for widget type unknownghost.usage; skipped.」Warning 落今日日志（Get-TodayLog，F5t 同通道）+ pinned:true 却零 tile；Y2 开面板（WM_HOTKEY 直发）右键面板中心 → 5s 内无「Pin to desktop」/「Unpin from desktop」（面板根+桌面根双查）——无钉菜单；Y3 重启 --settings → module 页无 pin-toggle-ghost 元素、无 ghost 行文本——scope 过滤整行不产；Y4 widgets.json ghost 条 pinned 恒 true（不翻转、不剪除、不重写）。与并行会话在飞的 PinEntryVisible 门控改造观测一致，其落地后本场景仍成立。
- **随批 X1 抗 flake 加固（38602176751 红因）**：该跑 Y 全绿但 X1 红——flyout 开启动画期 InvokePattern.Invoke 被吞（菜单命中、Invoke 已调、widgets.json 不翻、flyout 已关、tile 仍在——失败截图 X-FAIL-unpin-not-saved.png 实证 GLM tile 38% 绿灯照常）。修复 67f144f：菜单项改真实鼠标点击（ClickAt，不吃动画竞态——渲染即命中）+ 整链重试两轮（重试前重找 RecentList 行：ci 档 15s 刷新 tick 重建后旧元素 BoundingRectangle 不可信；上一轮弹层无论命中/落空均已关闭，右键重开新 flyout，无状态残留）；落库断言与 tile 断言拆为先后两窗。复证 38608625892 X1-X3+Y1-Y4 全绿。
- **验证**：run 38608625892 全绿，勾行 61（MVP 计数 28 勾 / 22 未勾）。
- **遗留**：无（本条全闭环；PinEntryVisible 单判定收编属并行会话改造，不改观测行为）。

### 批3-补57 V4 冷却竞态消解 + E1b 气泡探测降级 + UIA 矩形 NaN 防护（2026-10-15 patrol P1014E，commit 8b4b82b 首跑红 + f42f822 收口）
- **红因①（V4，38619441591）**：V1 绿（tile #ff3b30 382px，注入确达 L0）后 2s 即红「GLM Critical 记录 0 条」——60s 轮询 2s 即退出（break 条件=记录存在）→ 记录在但 severity 非 critical。读码锁因：connection-degraded 冷却按 (规则,widget) 键且跨级别共享（NotificationEngine._lastNotified）；首启实例 GLM 首跳刷新若撞 Ensure-MockServer 重启后的 bind 窗（连接被拒），PublishStale 先发一条 Warning 记录并武装 15min 冷却（WidgetHost:174 无缓存亦发 Warning），随后注入的 Critical 被去重吞掉。F5 同跑绿证引擎链本身无恙——且 F5 从不断言 severity，「camelCase critical」期望首由 V4 落地。产品去重语义本身正确，红的是场景预期。
- **修法（V4 独立重启自证）**：V4 移至 V2 后（V1/V2 同享首启实例）——Stop-Beacon → notifications.json 删净（新引擎水合源空）→ mock 探针确认热态（5 次未 200 即前置红，首跳刷新必成）→ 重设 BEACON_DEBUG_SEVERITY/CRASH → 新实例首个 GLM 事件即注入 Critical → critical 记录确定性落盘；失败信息带记录 severity dump（severity=warning 即冷却竞态再临的可判信号）。V3 崩溃基线随 V4 重启移动：首启实例 +70s 内被 Stop-Beacon 收掉、其 +90s 注入未及触发，日志中崩溃行唯属 V4 实例，断言语义不变。
- **红因②（X，同 run）**：W4 绿后 24s、X0 基线已过（行已找到）炸于 X1 行右键落点 `[int]($rect.X+$rect.Width/2)`——「Cannot convert value NaN to Int32」，无任何断言消息。RecentList 行被虚拟化或刷新 tick 重建中途，UIA BoundingRectangle 返 NaN 矩形。
- **修法（Get-ClickPoint 助手）**：GetClickablePoint 优先（元素不可点抛 PointUnavailableException 而非返 NaN），退回矩形中心；NaN/空/解析失败一律返 null——X1 两处落点（行右键+菜单点击）NaN 按落空走整链两轮重试不炸；W3/W4 chip 点击同型隐患（catch 回退分支直喂 [int]）一并收编，落点不可解析即前置红（带截图）。Y2 面板中心右键用 RectOf（Win32 整数矩形）无此险不改。
- **E1b（8b4b82b 新增，降级非红）**：二次启动唤起气泡「Beacon 已在运行」12s UIA 轮询未现于无头会话（toast 基础设施不可用或专注助手抑制）→ 按设计 ::warning 转装机（F5t/G7c 同口径）；E1 进程半已绿，行 31 呈现半维持装机。
- **验证**：CI 文件改动门禁全量 1078 测试绿（Core 368/Actions 27/Connections 652/Storage 31）；mkdocs strict 维持已知基线告警（troubleshooting.md 链 ../README.md，干净 HEAD 同在）；待 nightly 复证 V4/X 绿后行 50 引擎链半转「已代验」。
- **遗留**：行 50（「无打断」观感 + Toast 呈现面）与行 31（唤起气泡呈现）维持装机半。

### 批3-补58 V4 前置裸探针假红消解 + W3 浮 tile 遮挡防护（2026-10-15 patrol P1014E 二轮，commit f42f822 首跑 38633490811 红两处 + 本批收口）
- **红因①（V4 前置）**：批3-补57 新增的 mock 探针 5 次全败 → 前置红 V-FAIL-mock-cold——但 V1/V2 恰在同一 mock 上绿（V1 需刷新成功注入才达 L0）。读全程日志锁因：**本 CI 会话 PS 5.1 `Invoke-WebRequest`（无 `-UseBasicParsing`）恒抛**（IE 解析引擎不可用，响应永远解析不了），探针永远「失败」。铁证：绿跑 38608625892 同款全程 11 次「mock HTTP server 已监听」假重启信号（Ensure-MockServer 探针 3 败→无谓 Stop/Start），T/U/V/W 每场景起头都重启——脚本从未依赖探针成功，f42f822 是首个以探针 200 为门的设计，故必假红；红跑与绿跑重启次数完全一致（11=11），mock 本身无恙。
- **修法①**：V4 前置改走 `Ensure-MockServer`（重启收尾带作业态 Running 校验，38440205547 起全场景统一助手）——重启先于本场景启动完成，首跳刷新必命中已绑定监听，冷却竞态不可发；块内注释明令本会话禁以裸探针 200 为门。既有探针（Stop/Ensure 内）一字不改：其恒抛正是「每场景无谓重启」常态的组成部分，改探针反而改变 mock 生命周期语义。
- **红因②（W3）**：「点 chip-智谱 后 OVERVIEW 头未带·智谱」——失败截图 W-FAIL-no-filter.png 实证：GLM/DeepSeek 浮 tile 窗 z 序在面板之上、恰好压住 chip 行。UIA 找得到 chip（树含被遮挡元素）、落点也准，但 SendInput 打到最上层 tile 窗——过滤永不触发。tile 落位随前序场景配置/状态迁移漂移，纯落位运气（前两跑绿、本跑红）。
- **修法②**：W3 点击前把全部「Beacon Tile」/「Beacon Pinned」窗经新增 `WinEnum.MoveNoFocus`（SetWindowPos，SWP_NOZORDER|NOSIZE|NOACTIVATE——移位不改叠层不抢焦点）挪到面板正下方横排（x=panel.Left，y=panel.Bottom+4+60·i）——落位由面板矩形推导，与运气无关；W3/W4 无任何 tile 视觉断言，挪移零副作用。
- **X 面复证**：38633490811 X 全绿——Get-ClickPoint 行右键+菜单点击两处 NaN 防护过跑（批3-补57 修法独立成立）。
- **验证**：CI 文件改动门禁=本地全量 1078 绿（Core 368/Actions 27/Connections 652/Storage 31）+ mkdocs strict 已知基线；nightly 复证 V4/W3/X 绿后行 50 引擎链半转「已代验」。
- **遗留**：行 50（「无打断」观感 + Toast 呈现面）与行 31（唤起气泡呈现）维持装机半。
