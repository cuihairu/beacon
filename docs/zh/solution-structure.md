
# Beacon Solution / 项目目录

> 配套 [RFC-001](rfc/RFC-001-technical-design.md)。本文以**当前仓库实际结构**为准（2026-11-14 同步）。
> Beacon 代码只能在 Windows 上构建运行（WinUI 3，CI `windows-latest`）；Linux 工作环境可构建并运行 4 个测试项目作为本地门禁。

## 1. Solution 总览

```text
Beacon/
├── Beacon.sln
├── Directory.Build.props            # 公共编译设置（nullable、langversion、invariants）
├── Directory.Packages.props         # Central Package Management（统一版本）
├── global.json                      # 钉 SDK 10.0.100（rollForward latestFeature）
├── .editorconfig
├── .github/workflows/               # ci.yml（构建+测试+Codecov）/ nightly.yml（nightly）/ docs.yml（Pages）/ msix-dry-run.yml（MSIX 打包演练）+ scripts/（visual-evidence.ps1 等取证脚本）
├── docs/                            # 任务书 / RFC / Issue 列表 / 定位 / 审计 / 验收记录
├── packaging/                       # beacon.iss：Inno Setup 安装包定义（nightly 产出 setup.exe）+ wizard 图 + ChineseSimplified.isl + msix/（appxmanifest + logo PNG）
├── src/
│   ├── Beacon.App/                  # WinUI 3 壳（唯一 exe，代码建 UI 为主）
│   ├── Beacon.Core/                 # 领域模型 + 抽象 + 服务（无 UI 依赖）
│   ├── Beacon.Connections/          # 连接与 Widget Provider（17 类：GitHub / Jenkins / 本机 / Herald / HTTP / 智谱 / 方舟 / Claude / Kimi / Moonshot / DeepSeek / MiMo / Codex / Copilot / OpenCode / 百炼 / 商汤）
│   ├── Beacon.Actions/              # Action 执行器
│   └── Beacon.Storage/              # JSON 配置 / 缓存 / DPAPI Secrets / 导入导出
└── tests/
    ├── Beacon.Core.Tests/
    ├── Beacon.Connections.Tests/
    ├── Beacon.Storage.Tests/
    └── Beacon.Actions.Tests/
```

## 2. 项目依赖规则

```text
Beacon.App ──────► Beacon.Core ◄────── Beacon.Connections
    │                  ▲              Beacon.Actions
    │                  │              Beacon.Storage
    └──────────────────┴──────────────────┘（实现 Core 抽象，被 App 组合）
tests/* ──► 对应被测项目（+ Beacon.Core 接口）；例外：Connections.Tests 另引 Beacon.Storage（共享 fixture，tests/Beacon.Connections.Tests/Beacon.Connections.Tests.csproj:8）
```

硬规则：

1. `Beacon.Core` **不引用任何项目**（仅引用 BCL + `Microsoft.Extensions.Logging.Abstractions`）——保证 UI 与 Provider 可替换。
2. Connections / Actions / Storage 只依赖 Core（实现其接口），互相不依赖。
3. `Beacon.App` 是唯一可执行项目，负责 DI 组装：把 Provider/Executor/Store 注册进 `Microsoft.Extensions.DependencyInjection`（`App.BuildServices` + `BeaconRuntime`）。
4. 二期 Plugin 化时，把 Connections 内的 Provider 拆为独立程序集，接口不变。

## 3. 各项目内容

### 3.1 src/Beacon.Core（类库）

```text
Beacon.Core/
├── Models/                            # 19 文件
│   ├── Severity.cs                    # Info/Success/Warning/Error/Critical（+ Offline 展示态）
│   ├── LifecycleState.cs              # Queued/Running/…/Unknown
│   ├── ConnectionConfig.cs  ConnectionHealthState.cs  WidgetConfig.cs  WidgetTypeDescriptor.cs
│   ├── WidgetDisplayPolicy.cs         # 数值类悬浮总闸（FloatingOptIn × NumericFloatingEnabled，两 L0 形态共用）
│   ├── WidgetState.cs                 # widgetId/widgetType/connectionId/severity/summary/payload/…
│   ├── ActionModels.cs                # ActionConfig / ActionResult（合并文件）
│   ├── NotificationModels.cs          # NotificationRule / NotificationRecord / Delivery（合并文件）
│   ├── RefreshTiers.cs                # 7 级刷新策略（pr/ci/machine/agent/workflow/static/default）
│   ├── AppConfig.cs                   # 全局设置 + PinTile/PinsConfig
│   ├── PinLayout.cs                   # monitor/anchor/offsetDips/collapsed + floatingX/Y（悬浮框位置）
│   ├── CapsuleIslandLayout.cs         # 悬浮岛布局（岛=顶中锚点 + DIP 偏移，shell-state.json islandLayout 段；与四角 PinLayout 分开存，切形态各回各的记忆位）
│   ├── QuickPanelLayout.cs            # 看板窗口布局（显示器+锚点角+DIP 偏移+窗口 DIP 尺寸，shell-state.json quickPanelLayout 段；看板可调尺寸，尺寸记 DIP 随 DPI 缩放）
│   ├── ComponentBinding.cs             # 组件实例绑定（widget 数据 → 展示字段，组件库）
│   ├── ComponentDescriptor.cs          # 组件声明描述（type/label/默认值，组件库注册表）
│   ├── ComponentSourcesValidator.cs    # 组件源字段合法性校验（fail-closed 口径）
│   └── WidgetInstance.cs               # 组件实例（组件库，含实例样式覆盖）
├── Abstractions/                      # 9 文件
│   ├── IConnectionProvider.cs         # IConnectionProvider + IWidgetProvider + IWidgetProviderResolver（合并文件）
│   ├── IActionExecutor.cs  ISecretStore.cs  IConfigurationStore.cs  ICacheStore.cs
│   ├── IClock.cs                      # 注入时钟，供退避/冷却单测
│   ├── IStateStore.cs                  # 状态存储抽象（Store 注入，组件库）
│   ├── IEventBus.cs
│   └── INotificationSink.cs           # 通知出口（App 侧 Toast/声音/托盘着色实现）+ 记录落盘抽象 INotificationRecordStore
├── Services/                           # 42 文件
│   ├── EventBus.cs                    # 线程安全的进程内发布/订阅
│   ├── RefreshScheduler.cs            # (connection,tier,间隔覆盖) 合并轮询 + 抖动 + 退避
│   ├── StatusAggregator.cs            # WidgetState* → 总体 Severity + 计数
│   ├── NotificationEngine.cs          # 规则求值 + 冷却去重 + 默认规则
│   ├── ActionRunner.cs                # 确认策略 + 超时 + 取消 + 可选重试（DSL retry 参数，默认 1 次）
│   ├── ActionVisibility.cs            # 动作显隐纯函数（widgetType 逗号多值 + connection_id 限定）
│   ├── WidgetHost.cs  TemplateRenderer.cs  PaletteResolver.cs  PinLayoutMath.cs
│   ├── SvgPathParser.cs               # SVG path d → 中性几何（品牌图标渲染前置层，纯函数供单测）
│   ├── BrandIcons.cs                  # Simple Icons CC0 品牌 path 字典 + 连接类型别名归一（纯字符串零依赖，供单测；未收录由 App 字形兜底）
│   ├── WidgetValueHint.cs             # 悬浮 tile 数值位短文本（各家额度源 payload 键 → 主数值映射 + 多窗口额度「NN%/窗」拼接，纯函数供单测）
│   ├── DebugInjection.cs              # debug 注入面（BEACON_DEBUG_SEVERITY=critical → 刷新成功态改判 Critical/Failed；环境变量门控、缺省零行为，仅 CI 场景 V 与装机演练用，不进用户文档）
│   ├── WidgetTypeNames.cs             # 组件类型 → 短显示名（tile 标签/L2 分组 chip 共用，纯函数供单测）
│   ├── QuickPanelSections.cs          # L2 面板三分区判定（Warning+/过期/断连 → NEEDS ATTENTION，纯函数供单测）
│   ├── DpiLayoutMath.cs               # DIP↔物理像素唯一换算口径（AppWindow 只认物理像素）
│   ├── ContrastMath.cs                # WCAG 2.x 对比度数学（纯函数，Linux 门禁可测）
│   ├── HighContrastPalette.cs         # 高对比调色板（WCAG AA 4.5:1，配对经 Core.Tests 断言）
│   ├── UpdateCheck.cs                 # 版本解析/比较/判定 + GitHub tag_name 提取（纯函数，Linux 门禁可测；App 侧只做网络与 UI）
│   ├── DispatchGuard.cs                 # UI 派发回调异常包装（WinUI 无 DispatcherQueue.HandlerException，转交回调/兜底吞）
│   ├── QuickPanelFilter.cs              # L2 面板分组过滤（SourceKey/组件源分组，纯函数）
│   ├── UiDensity.cs                     # 密度三档（常规/紧凑/宽舒，纯函数换算）
│   ├── ProgressTiers.cs                 # 进度条分档（档内线性/档间分段，纯函数）
│   ├── QuotaWindows.cs                  # 多窗口额度展示（NN%/窗 拼接，纯函数）
│   ├── TrendSeries.cs                   # 趋势序列归一化（payload trend 键，fail-closed）
│   ├── TimelineEntries.cs               # 时间线条目构建（纯函数）
│   ├── UiStrings.cs                     # 界面文案单源（标签/提示，纯函数）
│   ├── WidgetFeedback.cs                # 组件反馈四态判定（错误/离线/空/无，纯函数）
│   ├── WidgetInstanceResolver.cs        # 组件实例解析（widget 数据 → 实例绑定）
│   ├── ComponentBindingEngine.cs        # 组件绑定引擎（字段填充 + 缺失/越界处理）
│   ├── ComponentCenterCatalog.cs        # 组件中心目录（注册类型按 status/machine/events/quota/other 五组分组 + 显隐判定/落库单源，纯函数）
│   ├── ComponentSourceIndex.cs          # 组件源索引（数据源字段 → 组件的可用性索引）
│   ├── ComponentVisibility.cs           # 组件可见性纯函数（stale/offline/has:字段/字段 op 值）
│   ├── DisplayTemplates.cs              # 显示模板注册表（横条/圆环/数字卡/水位/状态行 + 最小高）
│   ├── StateStore.cs                    # 状态存储（事件总线 → 组件状态，替换/合并语义）
│   ├── CapsuleIslandMath.cs             # 悬浮岛几何（USER-RPT-4，BrimDeck Notch 口径对拍重做：顶缘恒贴屏幕顶、水平居中 + 仅 X 可持久化，纯函数 Core 层供单测）
│   ├── CarrierCatalog.cs                # 展示载体统一选择（ISLAND-RING-1 扩围②：岛形态/数量悬浮窗/悬浮形态/看板开关收编为单源读写，纯函数）
│   ├── IslandDisplayPlan.cs             # 岛收起态展示计划（ISLAND-D1..D3：展示档位 + 上岛白名单 + 真实状态 → 可见面清单，决策沉 Core 供单测）
│   ├── IslandMotion.cs                  # 悬浮岛开合动效（USER-RPT-4，BrimDeck PanelMotion 口径：展开 360ms/收起 270ms + Bounce/Drawer 曲面，纯函数）
│   ├── IslandSurfaceOutline.cs          # 悬浮岛单连续曲面轮廓（USER-RPT-4，BrimDeck IslandBorder 同式：肩部双段 C2 三次贝塞尔 + 底圆角 12 DIP，纯函数）
│   └── HtmlTemplateParser.cs            # 受控 HTML 子集解析器（白名单标签/属性，sanitize）
├── Events/
│   └── Events.cs                      # WidgetStateChanged / AggregateStatusChanged 等事件
└── Json/
    └── BeaconJson.cs                  # JsonSerializerOptions（camelCase + 枚举字符串）
```

### 3.2 src/Beacon.Connections（类库，文件平铺无子目录）

```text
Beacon.Connections/                    # 34 文件
├── TileSizing.cs                      # 全类型 tile 宽度字宽预算（逐列最坏文本：标签/数值/读数 + 固定件 + 间距，下限 168；纯函数供单测，2026-10-12 二修）
├── GitHub 6 文件
│   ├── GitHubConnectionProvider.cs    # PAT 认证、连接测试、rate-limit 感知
│   ├── GitHubApiClient.cs             # 手写 REST 客户端（不引 Octokit）+ ETag 条件请求 + 限额感知
│   ├── GitHubWidgetDescriptors.cs     # Widget 类型元数据注册表（type: github.pull_requests / github.actions.runs）
│   ├── GitHubPullRequestsProvider.cs  # type: github.pull_requests
│   ├── GitHubActionsProvider.cs       # type: github.actions.runs
│   └── GitHubWorkflowExecutors.cs     # gh.workflow_dispatch/rerun/cancel 执行器
├── Jenkins 1 文件
│   └── JenkinsBuildProvider.cs        # type: jenkins.build（开源标准 JSON API，color 判级）+ JenkinsConnectionProvider（同文件）
├── Machine 4 文件（本机数据，零凭据零网络）
│   ├── MachineNetProvider.cs          # type: machine.net（IP/↑↓速率/累计流量，差分算速率）+ MachineWidgetDescriptors + MachineConnectionProvider（同文件）
│   ├── MachineLocalProvider.cs        # type: machine.local（CPU/内存/磁盘，CPU 差分同速率口径；Linux /proc + Windows API 注入抽象）
│   ├── MachineShowOptions.cs          # config["show"] 可选展示项解析（缺省全选/未知键 fail-loud；宽度推算在 TileSizing）
│   └── GitWorkspaceProvider.cs        # type: machine.git（本地 Git 工作区 ↑↓·改·stash，git CLI 只读）
├── Herald 1 文件（用户自有项目 cuihairu/herald：统一订阅与投递中枢）
│   └── HeraldProvider.cs              # type: herald.incidents（事件台账 open 亮黄/critical 亮红）+ herald.feed（RSS 未读基线，token 走 DPAPI）+ herald.deliveries（投递失败 failed 即黄）+ HeraldConnectionProvider（同文件）
├── HTTP 6 文件（Generic HTTP 接入）
│   ├── HttpConnectionProvider.cs      # 连接测试（支持 auth_header/auth_prefix 覆盖）
│   ├── HttpEndpoint.cs                # 端点请求与凭据装配
│   ├── HttpJsonPath.cs                # $.a.b[0] 点路径提取
│   ├── HttpQuotaProvider.cs           # type: http.quota（任意配额 JSON 字段映射 → 数值卡 + 进度条）
│   ├── HttpStatusProvider.cs          # type: http.status（点路径 + 状态词表 → Severity）
│   └── ProviderDsl.cs                 # YAML provider DSL（批3-补42）：dsl_path 声明层（mapping 数据提取缺省 + actions 带 retry）+ YamlLite 极小解析器
├── BigModel 2 文件
│   ├── BigModelConnectionProvider.cs  # 裸 Key 认证（Authorization 不带 Bearer）
│   └── BigModelUsageProvider.cs       # type: bigmodel.usage（quota/limit 三窗口归一化）
├── Ark 2 文件
│   ├── ArkConnectionProvider.cs       # V4 签名连接测试（失败按 4xx/5xx 分级 Degraded/Offline）
│   └── ArkUsageProvider.cs            # type: ark.usage（GetCodingPlanUsage 三窗口百分比；AK/SK 只进 DPAPI）
├── Claude 2 文件
│   ├── ClaudeConnectionProvider.cs    # 零网络连接测试（会话目录存在即 Healthy）
│   └── ClaudeUsageProvider.cs         # type: claude.usage（本机 JSONL 聚合，零凭据）
├── Kimi 1 文件
│   └── KimiCodingUsageProvider.cs     # type: kimi.coding（5h/周套餐余量）+ KimiConnectionProvider（同文件）
├── DeepSeek 1 文件
│   └── DeepSeekBalanceProvider.cs     # type: deepseek.balance（开放平台余额）+ DeepSeekConnectionProvider（同文件）
├── MiMo 1 文件
│   └── MiMoUsageProvider.cs           # type: mimo.usage（官方未开放用量口→模型目录真数据）+ MiMoConnectionProvider（同文件）
├── Codex 1 文件
│   └── CodexUsageProvider.cs          # type: codex.usage（本机 rollout 会话统计，source=local 如实标注）+ CodexConnectionProvider（同文件）
├── Copilot 1 文件
│   └── CopilotUsageProvider.cs        # type: copilot.usage（官方扩展同款配额端点直连，quota_snapshots 三槽）+ CopilotConnectionProvider（同文件）
├── OpenCode 1 文件
│   └── OpenCodeUsageProvider.cs       # type: opencode.usage（官方 Console Budgets API 直连，micro-cents 金额）+ OpenCodeConnectionProvider（同文件）
├── Qwen 1 文件
│   └── QwenUsageProvider.cs           # type: qwen.usage（百炼 Token Plan 四档探测：官方口→自定义→配额探针→模型目录兜底）+ QwenConnectionProvider（同文件）
├── Moonshot 1 文件
│   └── MoonshotBalanceProvider.cs     # type: moonshot.balance（开放平台余额，区别于 Kimi For Coding 套餐）+ MoonshotConnectionProvider（同文件）
└── SenseNova 1 文件
    └── SenseNovaUsageProvider.cs     # type: sensenova.usage（控制台 Token Plan 积分池：多池 × 5h/7d 双窗，最差窗判级）+ SenseNovaWidgetDescriptors + SenseNovaConnectionProvider（同文件）
```

### 3.3 src/Beacon.Actions（类库）

```text
Beacon.Actions/                        # 5 文件
├── ActionVars.cs                      # 参数模板占位符替换（{repo} 等）
├── OpenUrlExecutor.cs                 # open.url（系统默认浏览器）
├── HttpExecutor.cs                    # http（GET/POST + 头/体模板）
├── WebhookExecutor.cs                 # webhook（POST + HMAC-SHA256 签名头，密钥 credentialRef 只进 DPAPI、取不到即 fail closed）
└── LocalCommandExecutor.cs            # local.command（powershell/cmd/exe，工作目录/Env/超时/捕获输出）
```

- `gh.workflow_dispatch / gh.workflow_rerun / gh.workflow_cancel` 三个执行器在 `Beacon.Connections/GitHubWorkflowExecutors.cs`。

### 3.4 src/Beacon.Storage（类库）

```text
Beacon.Storage/                        # 6 文件
├── JsonConfigurationStore.cs          # config/connections/widgets/pins.json 原子写 + 备份
├── AppConfigFile.cs                   # config.json 快速读写（App 启动主题等早期读取）
├── JsonNotificationRecordStore.cs     # notifications.json 通知记录 + 已读态（滚动保留跨重启）
├── JsonCacheStore.cs                  # cache\{connId}\states.json last-known-state
├── DpapiSecretStore.cs                # DPAPI CurrentUser → secrets.bin（唯一默认实现）
└── ImportExport.cs                    # 打包导出（不含 secrets）/导入
```

备选（未实现）：Windows Credential Manager 密钥存储——同一 `ISecretStore` 接口，代码未落地。

### 3.5 src/Beacon.App（WinUI 3，打包=None）

```text
Beacon.App/
├── App.xaml / App.xaml.cs             # 入口：单实例、全局异常兜底、DI 组装、窗口装配、退出清理
├── app.manifest / Assets/             # PerMonitorV2 DPI；图标与托盘 logo 掩膜（嵌入资源）
├── Infrastructure/
│   ├── MonitorService.cs              # 显示器枚举/工作区/按像素定位（多屏）
│   ├── NativeMethods.cs               # P/Invoke（NOTIFYICONDATA、窗口扩展样式、DPI、热键）
│   └── Win32MessageWindow.cs          # 自驻留消息窗（托盘/热键/单实例事件回调）
├── Services/                          # 17 文件
│   ├── BeaconRuntime.cs               # 运行时装配：Provider 解析器、调度、聚合、启停联动
│   ├── UpdateCheckService.cs          # 检查更新（B-806）：GitHub releases/latest，10s 超时 + 进程内缓存 + 失败降级不抛异常
│   ├── TrayIconService.cs             # Win32 Shell_NotifyIcon 自实现：菜单、按配置色着色、气泡
│   ├── ToastNotificationService.cs    # Microsoft.Toolkit.Uwp.Notifications（Compat 激活路由、深链 L3）
│   ├── TopmostGuard.cs                # 置顶哨兵（Watch 单窗 / WatchAll 动态窗集）
│   ├── HotkeyService.cs               # RegisterHotKey，可配置（默认 Ctrl+Alt+B）
│   ├── StartupService.cs              # HKCU Run 键开关
│   ├── SingleInstanceGuard.cs         # Mutex + 命名事件（二次启动通知既有实例弹气泡）
│   ├── FileLoggerProvider.cs  CrashLog.cs    # beacon-YYYYMMDD.log / crash-*.log
│   ├── UiPalette.cs  MotionEngine.cs  ShellStateStore.cs  UiDispatcher.cs
│   ├── DispatcherQueueGuard.cs        # UI 派发兜底（TryEnqueue 回调异常不闪退：落日志+CrashLog，包装 Core DispatchGuard）
│   ├── DesignTokens.cs                # 设计令牌（间距/圆角/字阶/尺寸，UI 升级）
│   └── AppIcon.cs                     # 应用图标唯一来源（逐窗 AppWindow.SetIcon——unpackaged 任务栏图标坑）
└── Windows/                           # 33 文件（31 .cs + 2 .xaml，UI 多为代码构建）
    ├── CapsuleWindow.xaml|cs          # L1 聚合胶囊（独立置顶常驻窗，位置记忆；右键直达设置）
    ├── CapsuleWindow.{Drag,FullscreenAvoidance,IslandChrome,IslandFaces,Positioning,StatusFaces}.cs
    │                                  # 胶囊 6 个分部文件：拖拽 / 全屏避让 / 悬浮岛形态开关与锚点（config.capsuleIsland）/
    │                                  # 岛收起态面（状态环+徽标）/ 定位贴顶居中 / 状态面重绘
    ├── QuickPanelWindow.xaml|cs       # L2 快捷面板（可激活，ESC 关；通知中心/组件列表）
    ├── QuickPanelWindow.{Drag,InvokeableRow,Notifications,Sections}.cs
    │                                  # 看板 4 个分部文件：拖拽 / 可点行 / 通知中心（notifications.json 水合、标已读）/
    │                                  # NEEDS ATTENTION 三分区
    ├── SettingsWindow.cs              # B-801/802/805：PowerToys 形态配置中心（模块目录+启停+连接/组件 CRUD，
    │                                  #   悬浮形态二选一、导入导出、通知规则；代码建 UI）
    ├── SettingsWindow.{Actions,Appearance,ComponentStyles,ComponentTree,Connections,Navigation,Pages,Widgets}.cs
    │                                  # 设置页 8 个分部文件：动作库 / 外观 / 组件样式 / 组件中心树 / 连接 CRUD /
    │                                  # 导航 / 页面装配（展示载体单源落库）/ 组件 CRUD
    ├── PinnedHostWindow.cs            # ★ L0 宿主面板形态（默认）：每显示器一窗，代码建 UI，
    │                                  #   Topmost+NoActivate+空白点击穿透；贴边收起由 config.edgeCollapseEnabled 门控
    ├── FloatingTileWindow.cs          # ★ L0 独立悬浮框形态（pinDisplayMode=floating）：
    │                                  #   每钉选组件一窗，拖放位置按组件持久化（FloatingX/Y）
    ├── PinTile.cs                     # tile 控件（自 PinnedHostWindow.cs 拆出独立文件：高=max(尺寸档, 模板最小高)）
    ├── DragSession.cs                 # 一次拖动会话（左键按住 tile 起手；FreeX/FreeY = 吸附前自由落点，供贴边收起判定）
    ├── IslandSurfacePath.cs           # 悬浮岛曲面轮廓 → WinUI 路径（轮廓常量与 Core IslandSurfaceOutline 对拍）
    ├── SeverityPalette.cs             # 中性灰阶色（L1/L2/L3 文本与底色共用；级别色由 UiPalette 承载，B-706）
    ├── BrandIconFactory.cs            # 品牌 path → Path(Stretch=Uniform)（设置目录/悬浮 tile 共用，
    │                                  #   空几何/渲染失败退 null 让调用方字形兜底）
    ├── DetailWindow.cs                # L3 详情 + 全部 Action（代码建 UI）
    ├── ThemeColors.cs                 # 主题色桥：Core 高对比调色板 → WinUI 画刷（配对断言见 ContrastMathTests）；语义色板 StatusOk/Warn/Crit/Off/Accent（UI 升级）
    └── SampleGalleryWindow.cs         # 组件中心（组件库预览/参数表单/实时渲染，A-16 复核用）
```

### 3.5b src/Beacon.App/Components（组件库渲染层，2026-10-16 组件化重构）

```text
Beacon.App/Components/                 # 21 文件
├── ComponentSurface.cs  ComponentSurfaceRenderer.cs   # 表面 + 声明式渲染器（注册表在渲染侧）
├── ComponentAction.cs  ComponentBadge.cs  ComponentCard.cs  ComponentChart.cs
├── ComponentDivider.cs  ComponentFeedback.cs  ComponentIcon.cs  ComponentKeyValue.cs
├── ComponentLayout.cs  ComponentList.cs  ComponentMetric.cs  ComponentProgress.cs
├── ComponentStatus.cs  ComponentTable.cs  ComponentText.cs  ComponentTimeline.cs
├── BarProgressBadge.cs  RingProgressBadge.cs   # 额度徽标两式（横向进度条徽标 / 环形百分比徽标，胶囊与卡片共用）
└── StatusRing.cs                        # 状态圆环（轨道+弧长描边，胶囊健康环/面板卡共用）
```

注：无 `Controls/`、`ViewModels/`、`Themes/`、`Notifications/` 目录——但 `Components/`（组件库渲染层）存在，见 §3.5b；状态灯/PinTile 等以代码类组合（`PinTile` 已拆为独立文件 `Windows/PinTile.cs`），无 XAML 资源字典；Toast 在 `Services/`。

### 3.5c 抽象层职责

目录是装配和测试边界；下面按运行时职责归纳源码层。英文完整版见 [Source Layer Architecture](../source-layer-architecture.md)，术语表见 [Source Layer Glossary](../glossary.md)。

### 应用生命周期与装配

`App` 负责进程启动、服务注册、单实例、托盘、热键、窗口创建、睡眠唤醒和退出清理；`BeaconRuntime.Start` 先读盘再装配事件总线、存储、Provider、刷新管线、聚合、通知和动作。实现：`src/Beacon.App/App.xaml.cs:122`、`src/Beacon.App/Services/BeaconRuntime.cs:104`。配置 schema 在 `src/Beacon.Core/Models/AppConfig.cs:4`；进程级诊断输入 `BEACON_DEBUG_CRASH` / `BEACON_DEBUG_SEVERITY` 分别见 `src/Beacon.App/App.xaml.cs:102` 和 `src/Beacon.Core/Services/DebugInjection.cs`；看板与托盘文案挂在 `src/Beacon.Core/Services/UiStrings.cs:12`。

### 配置、密钥、缓存与 Shell 状态

用户配置、通知历史、最近状态、凭据和窗口几何是五类不同存储，不可合并。五份配置走原子写与 `.bak` 恢复（`src/Beacon.Storage/JsonConfigurationStore.cs:9`）；`secrets.bin` 走 DPAPI CurrentUser，JSON 只留 `credentialRef`（`src/Beacon.Storage/DpapiSecretStore.cs:12`）；`cache/{connectionId}/states.json` 是可丢失的 Last-Known-State（`src/Beacon.Storage/JsonCacheStore.cs:9`）；通知记录有独立备份恢复（`src/Beacon.Storage/JsonNotificationRecordStore.cs:12`）；窗口/布局在 `shell-state.json`（`src/Beacon.App/Services/ShellStateStore.cs:8`）。对应 schema 主要在 `src/Beacon.Core/Models`，五文件名在 `src/Beacon.Storage/JsonConfigurationStore.cs:160`。

### 采集与归一化

`RefreshScheduler` 按“连接 + 刷新档 + 间隔覆盖”分组并处理抖动/退避（`src/Beacon.Core/Services/RefreshScheduler.cs:41`）；`WidgetHost` 负责缓存水合、晚到结果丢弃、Provider 调用、缓存写回、健康/状态发布和陈旧重发布（`src/Beacon.Core/Services/WidgetHost.cs:196`）。`IConnectionProvider` 只测连通性，`IWidgetProvider` 归一化输出（`src/Beacon.Core/Abstractions/IConnectionProvider.cs:12`）。17 类连接与 20 类组件 Provider 在启动时注册（`src/Beacon.App/Services/BeaconRuntime.cs:116`、`:183`）。通用 HTTP / 厂商 REST / 本地文件与进程是各自通道；可选 DSL 声明 `mapping` 与 `actions`，入口和 schema 在 `src/Beacon.Connections/ProviderDsl.cs:15`。

### 进程内状态与通知

同步进程内事件总线是唯一状态广播通道，UI 订阅方负责回线程（`src/Beacon.Core/Services/EventBus.cs:6`）。`StateStore` 做内容去重、快照和失效（`src/Beacon.Core/Services/StateStore.cs:14`）；`StatusAggregator` 把状态与连接健康折算为总状态（`src/Beacon.Core/Services/StatusAggregator.cs:11`）；`NotificationEngine` 评估规则、去重并保留记录（`src/Beacon.Core/Services/NotificationEngine.cs:14`）。Toast 是 `INotificationSink` 的 App 实现（`src/Beacon.App/Services/ToastNotificationService.cs:20`），记录持久化在 `notifications.json`（`src/Beacon.Storage/JsonNotificationRecordStore.cs:12`）。事件 schema 在 `src/Beacon.Core/Events/Events.cs:5`。

### 意图与副作用

L3 详情窗汇总内置动作与 DSL 动作（`src/Beacon.App/Windows/DetailWindow.cs:285`），确认后统一交给 `ActionRunner`（`src/Beacon.App/Windows/DetailWindow.cs:373`）。Runner 处理路由、确认、超时、可选重试和 `ActionExecuted` 事件（`src/Beacon.Core/Services/ActionRunner.cs:18`）；`WidgetHost.OnActionExecuted` 再刷新来源组件（`src/Beacon.Core/Services/WidgetHost.cs:67`）。执行器分属 `src/Beacon.Actions` 与 GitHub 动作适配；动作 schema 在 `src/Beacon.Core/Models/ActionModels.cs:6`，DSL 动作词表在 `src/Beacon.Connections/ProviderDsl.cs:29`。

### 原生 Shell 与展示

L0 有单宿主面板和独立悬浮窗两形态，L1 有经典胶囊/悬浮岛，L2 是看板，L3 是详情窗；托盘是常驻聚合入口。所有窗先取快照/缓存再订阅事件，不直接轮询 Provider。实现与装配见 `src/Beacon.App/Windows/PinnedHostWindow.cs:684`、`src/Beacon.App/Windows/FloatingTileWindow.cs:26`、`src/Beacon.App/Windows/CapsuleWindow.cs:23`、`src/Beacon.App/Windows/QuickPanelWindow.cs:40`、`src/Beacon.App/Windows/DetailWindow.cs:20` 和 `src/Beacon.App/Services/TrayIconService.cs:13`。声明式组件树 schema 与校验在 `src/Beacon.Core/Models/ComponentDescriptor.cs:9`，闭合注册表渲染在 `src/Beacon.App/Components/ComponentSurfaceRenderer.cs:109`；Win32/P/Invoke 集中在 `src/Beacon.App/Infrastructure/NativeMethods.cs:6`。

### 3.5d 拆分理由

1. **Core 无 UI/Provider/Storage 依赖。** 项目描述明确服务为 UI/provider free（`src/Beacon.Core/Beacon.Core.csproj:7`），方案规则禁止项目引用（`docs/solution-structure.md:44`）。这样调度、状态、聚合、通知求值和动作编排可以在没有 WinUI 的环境锁定行为。
2. **采集面按连通性与取数两个接口拆开。** 连接测试返回健康态，Widget Provider 归一化 `WidgetState`（`src/Beacon.Core/Abstractions/IConnectionProvider.cs:12`、`:29`）；启动注册表分别容纳 17 和 20 个类型（`src/Beacon.App/Services/BeaconRuntime.cs:183`、`:116`）。新增来源不需要向调度、聚合或展示分支注入类型逻辑。
3. **持久化按风险和用途拆分。** 配置可恢复、缓存可丢、凭据必须 DPAPI、通知历史要跨重启、Shell 几何与用户意图无关（分别见 `src/Beacon.Storage/JsonConfigurationStore.cs:9`、`src/Beacon.Storage/JsonCacheStore.cs:44`、`src/Beacon.Storage/DpapiSecretStore.cs:9`、`src/Beacon.Storage/JsonNotificationRecordStore.cs:7`、`src/Beacon.App/Services/ShellStateStore.cs:6`）。合并会让过期策略、安全边界和用户配置互相污染。
4. **调度与归一化管线分离。** Scheduler 只管节奏（`src/Beacon.Core/Services/RefreshScheduler.cs:41`），Host 只管缓存水合、晚到保护、Provider 调用和发布（`src/Beacon.Core/Services/WidgetHost.cs:93`、`:75`）。Provider 无法绕过缓存、健康、失效和陈旧语义。
5. **状态广播是进程内契约。** EventBus 明确同步且线程安全（`src/Beacon.Core/Services/EventBus.cs:6`），StateStore 按内容去重（`src/Beacon.Core/Services/StateStore.cs:8`），聚合器只折算该流（`src/Beacon.Core/Services/StatusAggregator.cs:7`）。托盘、胶囊、L0、L2、L3 因此共享同一快照源，不会各自拉取。
6. **通知拆成求值、出口、持久化。** Core 求值规则（`src/Beacon.Core/Services/NotificationEngine.cs:14`），App 只实现 Toast 出口（`src/Beacon.App/Services/ToastNotificationService.cs:20`），Storage 只恢复/写入记录（`src/Beacon.Storage/JsonNotificationRecordStore.cs:12`）；装配在 `src/Beacon.App/Services/BeaconRuntime.cs:148`。平台通知与历史策略可分别变化。
7. **动作集中编排、执行器窄隔离。** Runner 统一确认、超时、重试、结果事件和来源刷新（`src/Beacon.Core/Services/ActionRunner.cs:18`、`:59`），`IActionExecutor` 只暴露单动作契约（`src/Beacon.Core/Abstractions/IActionExecutor.cs:6`）；webhook 取不到签名密钥时 fail closed（`src/Beacon.Actions/WebhookExecutor.cs:61`、`:68`）。副作用不会绕过安全编排。
8. **展示只消费状态，不制造采集。** 看板缓存优先并订阅状态、失效、聚合、通知事件（`src/Beacon.App/Windows/QuickPanelWindow.cs:93`、`:100`）；L0 两条渲染路径走同一事件路由（`src/Beacon.App/Windows/PinnedHostWindow.cs:737`、`src/Beacon.App/Windows/FloatingTileWindow.cs:80`）；详情动作只调 Runner（`src/Beacon.App/Windows/DetailWindow.cs:387`）。窗体可从快照重建，也不会成为第二套轮询器。
9. **声明式 UI 是原生组件树，不是网页。** `ComponentDescriptor` 是纯数据（`src/Beacon.Core/Models/ComponentDescriptor.cs:9`），校验显式限深/限节点/限子节点（`src/Beacon.Core/Models/ComponentDescriptor.cs:36`），渲染侧是闭合注册表和诊断占位（`src/Beacon.App/Components/ComponentSurfaceRenderer.cs:109`、`:201`）。用户可裁剪界面，同时保持原生渲染边界。
10. **App 是唯一组合根。** `BuildServices` 注册平台服务（`src/Beacon.App/App.xaml.cs:602`），`BeaconRuntime.Start` 装配运行时（`src/Beacon.App/Services/BeaconRuntime.cs:104`）。组合逻辑不散落到静态单例，测试可以用真实构造边界替换适配器。

### 3.6 tests/

xUnit；`Beacon.Core.Tests`（聚合/调度/退避/规则/通知引擎/ActionRunner，时钟注入；SvgPathParser、BrandIcons 字典全量可解析与别名、WidgetValueHint 数值映射）、`Beacon.Connections.Tests`（Fake HttpMessageHandler + API 夹具 + ETag 分支，程序集串行化避免共享客户端缓存竞态）、`Beacon.Storage.Tests`（往返/原子写/DPAPI；无 Windows 门控，net10.0 实跑全绿）、`Beacon.Actions.Tests`（open.url/local.command/http/webhook 执行器，Windows 专属用例 OS 门控）。全部接入 coverlet.collector（CI 上报 Codecov）。当前 4 个项目共 2408 个测试（Core 1277 / Connections 1017 / Actions 55 / Storage 59），即本地门禁（2026-11-14 重数，四个项目 net10.0 全绿）。Core 侧另覆盖 QuickPanelSections 三分区判定、ContrastMath/HighContrastPalette 对比度配对、WidgetTypeNames 短名映射、PinLayoutMath/DpiLayoutMath 换算与钳位、UpdateCheck 版本解析与 SemVer 比较（v 前缀/构建元数据/预发布规则/限流降级体）。

## 4. 关键 NuGet 包（Directory.Packages.props 统一版本）

| 包 | 用于 |
|---|---|
| `Microsoft.WindowsAppSDK` | WinUI 3 运行时 |
| `Microsoft.Windows.SDK.BuildTools` | Win32 interop（NoActivate/热键等） |
| `CommunityToolkit.Mvvm` | App 工程已引用，当前代码未使用（UI 为 code-behind 代码建，见 §6） |
| 托盘 | 不引第三方库，Win32 `Shell_NotifyIcon` 自实现（TrayIconService） |
| `Microsoft.Toolkit.Uwp.Notifications` | Toast（unpackaged AUMID 自愈与激活路由） |
| `System.Drawing.Common` | 托盘图标按配置色运行时绘制（GDI+ 位图 → HICON，B-706） |
| GitHub API | 手写 REST 客户端（ETag/限额可控，B-301 决策，不引 Octokit） |
| `System.Security.Cryptography.ProtectedData` | DPAPI 密钥存储 |
| `Microsoft.Extensions.Hosting` / `.Logging.Console`+`.Logging.Abstractions`+`.DependencyInjection.Abstractions` | DI/日志（`Logging.Abstractions` 是 Core 唯一直接引用；`Hosting`/`Logging.Console`/DI 抽象由 App 与测试项目直接引用） |
| `xunit` / `NSubstitute` / `coverlet.collector` / `Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio` | 测试与覆盖率 |

（版本集中在 Directory.Packages.props；表中不含未引用的包。）

## 5. Windows 机器上的脚手架命令（历史存档，工程已建成）

> 下列为初始建仓时实际使用的命令，保留备查；当前结构与文件以 §1/§3 为准。

```powershell
dotnet new sln -n Beacon
dotnet new classlib -o src/Beacon.Core -f net10.0
dotnet new classlib -o src/Beacon.Connections -f net10.0
dotnet new classlib -o src/Beacon.Actions -f net10.0
dotnet new classlib -o src/Beacon.Storage -f net10.0
dotnet new winui -o src/Beacon.App        # 模板可用缺失时：手建 csproj（WindowsAppSDKSelfContained，打包=None）
dotnet new xunit -o tests/Beacon.Core.Tests
dotnet new xunit -o tests/Beacon.Connections.Tests
dotnet new xunit -o tests/Beacon.Storage.Tests
dotnet new xunit -o tests/Beacon.Actions.Tests
dotnet sln add (git ls-files "**/*.csproj")
```

注意（.NET 10 LTS，RFC §16 决策 8）：四个类库目标框架 `net10.0`（DPAPI 走 `System.Security.Cryptography.ProtectedData` 包，无 Windows 专属 TFM）；`Beacon.App` 目标 `net10.0-windows10.0.19041.0`，`TargetPlatformMinVersion=10.0.17763.0`（WinUI 3 / Windows App SDK）。SDK 版本由根 `global.json` 钉 `10.0.100`（rollForward latestFeature）。

## 6. 编码约定

- `nullable enable`、file-scoped namespace、C# 12。
- UI 以**代码构建 + code-behind** 为主（WinUI `Window` 子类直接组装控件），状态来自事件总线订阅（`BeaconRuntime`/窗口内 `Subscribe`）；`CommunityToolkit.Mvvm` 包已引用但当前未使用（无 ViewModel 层）。
- 所有时间相关逻辑经 `IClock` 注入。
- 配置 JSON 用 `System.Text.Json`：`BeaconJson` 统一 `JsonSerializerOptions`（camelCase 属性 + 枚举字符串），非 source-gen。
- 日志经 `ILogger`，Secrets 一律不进日志（RFC-001 §13）。
