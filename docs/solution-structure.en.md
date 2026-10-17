
# Beacon Solution / Project Directory

> Companion to [RFC-001](rfc/RFC-001-technical-design.md). This document is based on **the actual current repository structure** (synchronized 2026-10-09).
> Beacon code can only be built and run on Windows (WinUI 3, CI `windows-latest`); a Linux development environment can build and run the 4 test projects as a local gate.

## 1. Solution Overview

```text
Beacon/
├── Beacon.sln
├── Directory.Build.props            # 公共编译设置（nullable、langversion、invariants）
├── Directory.Packages.props         # Central Package Management（统一版本）
├── global.json                      # 钉 SDK 10.0.100（rollForward latestFeature）
├── .editorconfig
├── .github/workflows/               # ci.yml（构建+测试+Codecov）/ daily-build.yml（nightly）/ docs.yml（Pages）
├── docs/                            # 任务书 / RFC / Issue 列表 / 定位 / 审计 / 验收记录
├── packaging/                       # beacon.iss：Inno Setup 安装包定义（daily-build 产出 setup.exe）
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

## 2. Project Dependency Rules

```text
Beacon.App ──────► Beacon.Core ◄────── Beacon.Connections
    │                  ▲              Beacon.Actions
    │                  │              Beacon.Storage
    └──────────────────┴──────────────────┘（实现 Core 抽象，被 App 组合）
tests/* ──► 对应被测项目（+ Beacon.Core 接口）
```

Hard rules:

1. `Beacon.Core` **references no projects** (only BCL + `Microsoft.Extensions.Logging.Abstractions`) — ensures UI and Provider are replaceable.
2. Connections / Actions / Storage depend only on Core (implementing its interfaces) and are independent of each other.
3. `Beacon.App` is the only executable project, responsible for DI assembly: registers Providers/Executors/Stores into `Microsoft.Extensions.DependencyInjection` (`App.BuildServices` + `BeaconRuntime`).
4. In the Phase 2 plugin transition, split the Providers inside Connections into separate assemblies, keeping the interfaces unchanged.

## 3. Contents of Each Project

### 3.1 src/Beacon.Core (class library)

```text
Beacon.Core/
├── Models/                            # 13 文件
│   ├── Severity.cs                    # Info/Success/Warning/Error/Critical（+ Offline 展示态）
│   ├── LifecycleState.cs              # Queued/Running/…/Unknown
│   ├── ConnectionConfig.cs  ConnectionHealthState.cs  WidgetConfig.cs  WidgetTypeDescriptor.cs
│   ├── WidgetDisplayPolicy.cs         # 数值类悬浮总闸（FloatingOptIn × NumericFloatingEnabled，两 L0 形态共用）
│   ├── WidgetState.cs                 # widgetId/widgetType/connectionId/severity/summary/payload/…
│   ├── ActionModels.cs                # ActionConfig / ActionResult（合并文件）
│   ├── NotificationModels.cs          # NotificationRule / NotificationRecord / Delivery（合并文件）
│   ├── RefreshTiers.cs                # 7 级刷新策略（pr/ci/machine/agent/workflow/static/default）
│   ├── AppConfig.cs                   # 全局设置 + PinTile/PinsConfig
│   └── PinLayout.cs                   # monitor/anchor/offsetDips/collapsed + floatingX/Y（悬浮框位置）
├── Abstractions/                      # 8 文件
│   ├── IConnectionProvider.cs         # IConnectionProvider + IWidgetProvider + IWidgetProviderResolver（合并文件）
│   ├── IActionExecutor.cs  ISecretStore.cs  IConfigurationStore.cs  ICacheStore.cs
│   ├── IClock.cs                      # 注入时钟，供退避/冷却单测
│   ├── IEventBus.cs
│   └── INotificationSink.cs           # 通知出口（App 侧 Toast/声音/托盘着色实现）+ 记录落盘抽象 INotificationRecordStore
├── Services/                           # 20 文件
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
│   └── UpdateCheck.cs                 # 版本解析/比较/判定 + GitHub tag_name 提取（纯函数，Linux 门禁可测；App 侧只做网络与 UI）
├── Events/
│   └── Events.cs                      # WidgetStateChanged / AggregateStatusChanged 等事件
└── Json/
    └── BeaconJson.cs                  # JsonSerializerOptions（camelCase + 枚举字符串）
```

### 3.2 src/Beacon.Connections (class library, flat files with no subdirectories)

```text
Beacon.Connections/                    # 35 文件
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
├── HTTP 7 文件（Generic HTTP 接入）
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

### 3.3 src/Beacon.Actions (class library)

```text
Beacon.Actions/                        # 5 文件
├── ActionVars.cs                      # 参数模板占位符替换（{repo} 等）
├── OpenUrlExecutor.cs                 # open.url（系统默认浏览器）
├── HttpExecutor.cs                    # http（GET/POST + 头/体模板）
├── WebhookExecutor.cs                 # webhook（POST + HMAC-SHA256 签名头，密钥 credentialRef 只进 DPAPI、取不到即 fail closed）
└── LocalCommandExecutor.cs            # local.command（powershell/cmd/exe，工作目录/Env/超时/捕获输出）
```

- The three executors `gh.workflow_dispatch / gh.workflow_rerun / gh.workflow_cancel` are in `Beacon.Connections/GitHubWorkflowExecutors.cs`.

### 3.4 src/Beacon.Storage (class library)

```text
Beacon.Storage/                        # 6 文件
├── JsonConfigurationStore.cs          # config/connections/widgets/pins.json 原子写 + 备份
├── AppConfigFile.cs                   # config.json 快速读写（App 启动主题等早期读取）
├── JsonNotificationRecordStore.cs     # notifications.json 通知记录 + 已读态（滚动保留跨重启）
├── JsonCacheStore.cs                  # cache\{connId}\states.json last-known-state
├── DpapiSecretStore.cs                # DPAPI CurrentUser → secrets.bin（唯一默认实现）
└── ImportExport.cs                    # 打包导出（不含 secrets）/导入
```

Alternative (not implemented): Windows Credential Manager secret storage — same `ISecretStore` interface, code not landed.

### 3.5 src/Beacon.App (WinUI 3, packaging=None)

```text
Beacon.App/
├── App.xaml / App.xaml.cs             # 入口：单实例、全局异常兜底、DI 组装、窗口装配、退出清理
├── app.manifest / Assets/             # PerMonitorV2 DPI；图标与托盘 logo 掩膜（嵌入资源）
├── Infrastructure/
│   ├── MonitorService.cs              # 显示器枚举/工作区/按像素定位（多屏）
│   ├── NativeMethods.cs               # P/Invoke（NOTIFYICONDATA、窗口扩展样式、DPI、热键）
│   └── Win32MessageWindow.cs          # 自驻留消息窗（托盘/热键/单实例事件回调）
├── Services/                          # 15 文件
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
│   └── AppIcon.cs                     # 应用图标唯一来源（逐窗 AppWindow.SetIcon——unpackaged 任务栏图标坑）
└── Windows/                           # 10 文件（8 .cs + 2 .xaml，UI 多为代码构建）
    ├── CapsuleWindow.xaml|cs          # L1 聚合胶囊（独立置顶常驻窗，位置记忆；右键直达设置）
    ├── QuickPanelWindow.xaml|cs       # L2 快捷面板（可激活，ESC 关；通知中心/组件列表）
    ├── PinnedHostWindow.cs            # ★ L0 宿主面板形态（默认）：每显示器一窗，代码建 UI，
    │                                  #   Topmost+NoActivate+空白点击穿透；含 PinTile 控件
    ├── FloatingTileWindow.cs          # ★ L0 独立悬浮框形态（pinDisplayMode=floating）：
    │                                  #   每钉选组件一窗，拖放位置按组件持久化（FloatingX/Y）
    ├── BrandIconFactory.cs            # 品牌 path → Path(Stretch=Uniform)（设置目录/悬浮 tile 共用，
    │                                  #   空几何/渲染失败退 null 让调用方字形兜底）
    ├── DetailWindow.cs                # L3 详情 + 全部 Action（代码建 UI）
    ├── ThemeColors.cs                 # 主题色桥：Core 高对比调色板 → WinUI 画刷（配对断言见 ContrastMathTests）
    └── SettingsWindow.cs              # B-801/802/805：PowerToys 形态配置中心（模块目录+启停+连接/组件 CRUD，
                                       #   悬浮形态二选一、导入导出、通知规则；代码建 UI）
```

Note: no `Controls/`, `ViewModels/`, `Themes/`, `Notifications/` directories — status lights/PinTile etc. are composed as code classes (`PinTile` is inside `PinnedHostWindow.cs`), no XAML resource dictionaries; Toast is in `Services/`.

### 3.6 tests/

xUnit; `Beacon.Core.Tests` (aggregation/scheduling/backoff/rules/notification engine/ActionRunner, clock injection; SvgPathParser, full parseability and aliases of the BrandIcons dictionary, WidgetValueHint numeric mapping), `Beacon.Connections.Tests` (fake HttpMessageHandler + API fixtures + ETag branches, assembly serialization to avoid shared client cache races), `Beacon.Storage.Tests` (round-trip/atomic write/DPAPI, requires Windows environment), `Beacon.Actions.Tests` (open.url/local.command/http/webhook executors, Windows-only test cases OS-gated). All wired into coverlet.collector (CI reports to Codecov). Currently 4 projects with a total of 1057 tests (Connections 652 / Core 347 / Actions 27 / Storage 31), i.e. the local gate. The Core side also covers QuickPanelSections three-section classification, ContrastMath/HighContrastPalette contrast pairings, WidgetTypeNames short-name mapping, PinLayoutMath/DpiLayoutMath conversion and clamping, and UpdateCheck version parsing and SemVer comparison (v prefix/build metadata/pre-release rules/rate-limit degraded body).

## 4. Key NuGet Packages (unified versions in Directory.Packages.props)

| Package | Used for |
|---|---|
| `Microsoft.WindowsAppSDK` | WinUI 3 runtime |
| `Microsoft.Windows.SDK.BuildTools` | Win32 interop (NoActivate/hotkeys, etc.) |
| `CommunityToolkit.Mvvm` | Referenced in App project but currently unused (UI is code-behind, see §6) |
| Tray | No third-party library; Win32 `Shell_NotifyIcon` implemented in-house (TrayIconService) |
| `Microsoft.Toolkit.Uwp.Notifications` | Toast (unpackaged AUMID self-healing and activation routing) |
| `System.Drawing.Common` | Tray icon drawn at runtime with configured color (GDI+ bitmap → HICON, B-706) |
| GitHub API | Hand-written REST client (ETag/rate-limit controllable, B-301 decision, no Octokit) |
| `System.Security.Cryptography.ProtectedData` | DPAPI secret storage |
| `Microsoft.Extensions.Hosting` / `.DependencyInjection` / `.Logging` | DI/logging |
| `xunit` / `NSubstitute` / `coverlet.collector` | Testing and coverage |

(Versions centralized in Directory.Packages.props; the table excludes unreferenced packages.)

## 5. Scaffolding Commands on Windows (historical archive, project already built)

> The following are the commands actually used when initially creating the repository, retained for reference; the current structure and files are authoritative as in §1/§3.

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

Note (.NET 10 LTS, RFC §16 decision 8): the four class libraries target `net10.0` (DPAPI via the `System.Security.Cryptography.ProtectedData` package, no Windows-specific TFM); `Beacon.App` targets `net10.0-windows10.0.19041.0`, `TargetPlatformMinVersion=10.0.17763.0` (WinUI 3 / Windows App SDK). The SDK version is pinned to `10.0.100` by the root `global.json` (rollForward latestFeature).

## 6. Coding Conventions

- `nullable enable`, file-scoped namespace, C# 12.
- UI is primarily **code-built + code-behind** (WinUI `Window` subclasses directly compose controls), state comes from event bus subscriptions (`BeaconRuntime`/`Subscribe` inside windows); the `CommunityToolkit.Mvvm` package is referenced but currently unused (no ViewModel layer).
- All time-related logic is injected via `IClock`.
- Configuration JSON uses `System.Text.Json`: `BeaconJson` centralizes `JsonSerializerOptions` (camelCase properties + enum strings), not source-gen.
- Logging via `ILogger`, Secrets never go into logs (RFC-001 §13).
