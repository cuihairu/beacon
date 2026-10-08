# Beacon Solution / 项目目录

> 配套 [RFC-001](rfc/RFC-001-technical-design.md)。本文以**当前仓库实际结构**为准（2026-10-08 一致性审计核对）。
> Beacon 代码只能在 Windows 上构建运行（WinUI 3，CI `windows-latest`）；Linux 工作环境可构建并运行 4 个测试项目作为本地门禁。

## 1. Solution 总览

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
│   ├── Beacon.Connections/          # 连接与 Widget Provider（GitHub / HTTP / 智谱 / Claude / Kimi / DeepSeek）
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
tests/* ──► 对应被测项目（+ Beacon.Core 接口）
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
├── Models/                            # 12 文件
│   ├── Severity.cs                    # Info/Success/Warning/Error/Critical（+ Offline 展示态）
│   ├── LifecycleState.cs              # Queued/Running/…/Unknown
│   ├── ConnectionConfig.cs  ConnectionHealthState.cs  WidgetConfig.cs  WidgetTypeDescriptor.cs
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
│   └── INotificationSink.cs           # 通知出口（App 侧 Toast/声音/托盘着色实现）
├── Services/
│   ├── EventBus.cs                    # 线程安全的进程内发布/订阅
│   ├── RefreshScheduler.cs            # (connection,tier) 合并轮询 + 抖动 + 退避
│   ├── StatusAggregator.cs            # WidgetState* → 总体 Severity + 计数
│   ├── NotificationEngine.cs          # 规则求值 + 冷却去重 + 默认规则
│   ├── ActionRunner.cs                # 确认策略 + 超时 + 取消
│   ├── WidgetHost.cs  TemplateRenderer.cs  PaletteResolver.cs  PinLayoutMath.cs
├── Events/
│   └── Events.cs                      # WidgetStateChanged / AggregateStatusChanged 等事件
└── Json/
    └── BeaconJson.cs                  # JsonSerializerOptions（camelCase + 枚举字符串）
```

### 3.2 src/Beacon.Connections（类库，文件平铺无子目录）

```text
Beacon.Connections/                    # 18 文件
├── GitHub 6 文件
│   ├── GitHubConnectionProvider.cs    # PAT 认证、连接测试、rate-limit 感知
│   ├── GitHubApiClient.cs             # 手写 REST 客户端（不引 Octokit）+ ETag 条件请求 + 限额感知
│   ├── GitHubWidgetDescriptors.cs     # Widget 类型元数据注册表（type: github.pull_requests / github.actions.runs）
│   ├── GitHubPullRequestsProvider.cs  # type: github.pull_requests
│   ├── GitHubActionsProvider.cs       # type: github.actions.runs
│   └── GitHubWorkflowExecutors.cs     # gh.workflow_dispatch/rerun/cancel 执行器
├── HTTP 6 文件（Generic HTTP 接入）
│   ├── HttpConnectionProvider.cs      # 连接测试（支持 auth_header/auth_prefix 覆盖）
│   ├── HttpEndpoint.cs                # 端点请求与凭据装配
│   ├── HttpJsonPath.cs                # $.a.b[0] 点路径提取
│   ├── HttpQuotaProvider.cs           # type: http.quota（任意配额 JSON 字段映射 → 数值卡 + 进度条）
│   └── HttpStatusProvider.cs          # type: http.status（点路径 + 状态词表 → Severity）
├── BigModel 2 文件
│   ├── BigModelConnectionProvider.cs  # 裸 Key 认证（Authorization 不带 Bearer）
│   └── BigModelUsageProvider.cs       # type: bigmodel.usage（quota/limit 三窗口归一化）
├── Claude 2 文件
│   ├── ClaudeConnectionProvider.cs    # 零网络连接测试（会话目录存在即 Healthy）
│   └── ClaudeUsageProvider.cs         # type: claude.usage（本机 JSONL 聚合，零凭据）
├── Kimi 1 文件
│   └── KimiCodingUsageProvider.cs     # type: kimi.coding（5h/周套餐余量）+ KimiConnectionProvider（同文件）
└── DeepSeek 1 文件
    └── DeepSeekBalanceProvider.cs     # type: deepseek.balance（开放平台余额）+ DeepSeekConnectionProvider（同文件）
```

### 3.3 src/Beacon.Actions（类库）

```text
Beacon.Actions/                        # 4 文件
├── ActionVars.cs                      # 参数模板占位符替换（{repo} 等）
├── OpenUrlExecutor.cs                 # open.url（系统默认浏览器）
├── HttpExecutor.cs                    # http（GET/POST + 头/体模板）
└── LocalCommandExecutor.cs            # local.command（powershell/cmd/exe，工作目录/Env/超时/捕获输出）
```

- `gh.workflow_dispatch / gh.workflow_rerun / gh.workflow_cancel` 三个执行器在 `Beacon.Connections/GitHubWorkflowExecutors.cs`。
- **未实现**：webhook 执行器（RFC 设计保留，代码未落地）。

### 3.4 src/Beacon.Storage（类库）

```text
Beacon.Storage/                        # 5 文件
├── JsonConfigurationStore.cs          # config/connections/widgets/pins.json 原子写 + 备份
├── AppConfigFile.cs                   # config.json 快速读写（App 启动主题等早期读取）
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
├── Services/                          # 13 文件
│   ├── BeaconRuntime.cs               # 运行时装配：Provider 解析器、调度、聚合、启停联动
│   ├── TrayIconService.cs             # Win32 Shell_NotifyIcon 自实现：菜单、按配置色着色、气泡
│   ├── ToastNotificationService.cs    # Microsoft.Toolkit.Uwp.Notifications（Compat 激活路由、深链 L3）
│   ├── TopmostGuard.cs                # 置顶哨兵（Watch 单窗 / WatchAll 动态窗集）
│   ├── HotkeyService.cs               # RegisterHotKey，可配置（默认 Ctrl+Alt+B）
│   ├── StartupService.cs              # HKCU Run 键开关
│   ├── SingleInstanceGuard.cs         # Mutex + 命名事件（二次启动通知既有实例弹气泡）
│   ├── FileLoggerProvider.cs  CrashLog.cs    # beacon-YYYYMMDD.log / crash-*.log
│   ├── UiPalette.cs  MotionEngine.cs  ShellStateStore.cs  UiDispatcher.cs
│   └── （+ Monitor 相关辅助）
└── Windows/                           # 6 文件（UI 多为代码构建）
    ├── CapsuleWindow.xaml|cs          # L1 聚合胶囊（独立置顶常驻窗，位置记忆）
    ├── QuickPanelWindow.xaml|cs       # L2 快捷面板（可激活，ESC 关；通知中心/组件列表）
    ├── PinnedHostWindow.cs            # ★ L0 宿主面板形态（默认）：每显示器一窗，代码建 UI，
    │                                  #   Topmost+NoActivate+空白点击穿透；含 PinTile 控件
    ├── FloatingTileWindow.cs          # ★ L0 独立悬浮框形态（pinDisplayMode=floating）：
    │                                  #   每钉选组件一窗，拖放位置按组件持久化（FloatingX/Y）
    ├── DetailWindow.cs                # L3 详情 + 全部 Action（代码建 UI）
    └── SettingsWindow.cs              # B-801/802/805：PowerToys 形态配置中心（模块目录+启停+连接/组件 CRUD，
                                       #   悬浮形态二选一、导入导出、通知规则；代码建 UI）
```

注：无 `Controls/`、`ViewModels/`、`Themes/`、`Notifications/` 目录——状态灯/PinTile 等以代码类组合（`PinTile` 在 `PinnedHostWindow.cs` 内），无 XAML 资源字典；Toast 在 `Services/`。

### 3.6 tests/

xUnit；`Beacon.Core.Tests`（聚合/调度/退避/规则/通知引擎/ActionRunner，时钟注入）、`Beacon.Connections.Tests`（Fake HttpMessageHandler + API 夹具 + ETag 分支，程序集串行化避免共享客户端缓存竞态）、`Beacon.Storage.Tests`（往返/原子写/DPAPI，需 Windows 环境）、`Beacon.Actions.Tests`（open.url/local.command/http 执行器，Windows 专属用例 OS 门控）。全部接入 coverlet.collector（CI 上报 Codecov）。当前 4 个项目共 298 个测试（Connections 163 / Core 101 / Actions 19 / Storage 22），即本地门禁。

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
| `Microsoft.Extensions.Hosting` / `.DependencyInjection` / `.Logging` | DI/日志 |
| `xunit` / `NSubstitute` / `coverlet.collector` | 测试与覆盖率 |

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
