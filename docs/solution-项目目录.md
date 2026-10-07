# Beacon Solution / 项目目录

> 配套 [RFC-001](rfc/RFC-001-技术方案.md)。本文档足够精确，任何 Code Agent 在 **Windows + .NET 8 SDK + Windows App SDK** 机器上可直接落地。
> 当前仓库工作环境为 Linux——**本阶段只产出文档，不建工程**；Beacon 代码只能在 Windows 上构建运行（WinUI 3）。

## 1. Solution 总览

```text
Beacon/
├── Beacon.sln
├── Directory.Build.props            # 公共编译设置（nullable、langversion、invariants）
├── Directory.Packages.props         # Central Package Management（统一版本）
├── .editorconfig
├── docs/                            # 任务书 / RFC / Issue 列表（已存在）
├── src/
│   ├── Beacon.App/                  # WinUI 3 壳（唯一 exe）
│   ├── Beacon.Core/                 # 领域模型 + 抽象 + 服务（无 UI 依赖）
│   ├── Beacon.Connections/          # 连接与 Widget Provider（GitHub / Rest）
│   ├── Beacon.Actions/              # Action 执行器
│   └── Beacon.Storage/              # JSON 配置 / 缓存 / DPAPI Secrets
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

1. `Beacon.Core` **不引用任何项目**（只引用 BCL/扩展/MVVM 工具库）——保证 UI 与 Provider 可替换。
2. Connections / Actions / Storage 只依赖 Core（实现其接口），互相不依赖。
3. `Beacon.App` 是唯一可执行项目，负责 DI 组装：把 Provider/Executor/Store 注册进 `Microsoft.Extensions.Hosting`。
4. 二期 Plugin 化时，把 Connections 内的 Provider 文件夹拆为独立程序集，接口不变。

## 3. 各项目内容

### 3.1 src/Beacon.Core（类库）

```text
Beacon.Core/
├── Models/
│   ├── Severity.cs                  # Info/Success/Warning/Error/Critical
│   ├── LifecycleState.cs            # Queued/Running/…/Unknown
│   ├── ConnectionConfig.cs  WidgetConfig.cs  WidgetTypeDescriptor.cs
│   ├── WidgetState.cs               # severity + payload + fetchedAt + isStale
│   ├── ActionConfig.cs  ActionResult.cs
│   ├── NotificationRecord.cs  NotificationRule.cs
│   └── PinLayout.cs                 # monitor/anchor/offsetDips/collapsed
├── Abstractions/
│   ├── IConnectionProvider.cs  IWidgetProvider.cs  IActionExecutor.cs
│   ├── ISecretStore.cs  IConfigurationStore.cs  ICacheStore.cs
│   └── IEventBus.cs
├── Services/
│   ├── EventBus.cs                  # 线程安全的进程内发布/订阅
│   ├── RefreshScheduler.cs          # (connection,tier) 合并轮询 + 抖动 + 退避
│   ├── StatusAggregator.cs          # WidgetState* → 总体 Severity + 计数
│   ├── NotificationEngine.cs        # 规则求值 + 冷却去重
│   └── ActionRunner.cs              # 确认策略 + 超时 + 取消
└── Time/
    └── IClock.cs                    # 注入时钟，供退避/冷却单测
```

### 3.2 src/Beacon.Connections（类库）

```text
Beacon.Connections/
├── GitHub/
│   ├── GitHubConnectionProvider.cs  # PAT 认证、连接测试、rate-limit 感知
│   ├── GitHubApiClient.cs           # 手写 REST 客户端（不引 Octokit）+ ETag 条件请求 + 限额感知
│   ├── GitHubWidgetDescriptors.cs   # Widget 类型元数据注册表（开闭性：新增 Provider 零 Core 改动）
│   ├── GitHubPullRequestsProvider.cs  # type: github.pull_requests
│   ├── GitHubActionsProvider.cs       # type: github.actions.runs
│   └── GitHubWorkflowExecutors.cs   # gh.workflow_dispatch/rerun/cancel 执行器
│       └── WorkflowDispatchAction.cs # Run/Retry/Cancel（gh.workflow_dispatch）
└── Rest/
    ├── RestConnectionProvider.cs
    └── GenericStatusWidget.cs       # type: rest.status（JSON 路径映射 → Severity）
```

### 3.3 src/Beacon.Actions（类库）

```text
Beacon.Actions/
├── OpenUrlAction.cs                 # open.url
├── HttpAction.cs                    # http（GET/POST + 头/体模板）
├── LocalCommandAction.cs            # local.command（powershell/cmd/exe，
│                                    #   WorkingDirectory/Env/Timeout/捕获输出）
└── WebhookAction.cs                 # webhook（POST + secret 签名）
```

### 3.4 src/Beacon.Storage（类库）

```text
Beacon.Storage/
├── JsonConfigurationStore.cs        # %AppData%\Beacon\*.json 原子写 + 备份
├── JsonCacheStore.cs                # cache\{connId}\last-known-state
├── DpapiSecretStore.cs              # 默认：DPAPI CurrentUser → secrets.bin
├── CredentialManagerSecretStore.cs  # 备选：Windows Credential Manager
└── ImportExport.cs                  # 打包导出（不含 secrets）/导入
```

### 3.5 src/Beacon.App（WinUI 3，打包=None）

```text
Beacon.App/
├── App.xaml / App.xaml.cs           # Host/DI、单实例 Mutex、全局异常兜底
├── Services/
│   ├── TrayIconService.cs           # Win32 Shell_NotifyIcon 自实现：菜单、按配置色着色、气泡
│   ├── UiPalette.cs                 # B-706：级别/Widget 覆盖色实时解析（改色即刻生效）
│   ├── MotionEngine.cs              # B-707：Composition 动效五族，full/reduced/off + intensity
│   ├── HotkeyService.cs             # RegisterHotKey，可配置
│   ├── StartupService.cs            # HKCU Run 键开关
│   └── SingleInstanceGuard.cs
├── Windows/
│   ├── PinnedHostWindow.xaml|cs     # ★ L0+L1 共享宿主：每显示器一个，
│   │                                #   Topmost+NoActivate+透明+hit-test tile
│   ├── QuickPanelWindow.xaml|cs     # L2 Flyout（可激活，ESC 关）
│   ├── DetailWindow.xaml|cs         # L3 详情 + 全部 Action
│   └── SettingsWindow.xaml|cs
├── Controls/
│   ├── StatusLight.xaml             # 五级状态灯（含 Critical 脉冲 / Offline 灰）
│   ├── PinTile.xaml                 # L0 小胶囊 tile（灯+标签+数字/进度）
│   ├── CapsuleTile.xaml             # L1 聚合 tile
│   └── WidgetListItem.xaml          # L2 列表项（右键 Pin 入口）
├── ViewModels/
│   ├── ShellViewModel.cs  PinnedTilesViewModel.cs  QuickPanelViewModel.cs
│   ├── DetailViewModel.cs  SettingsViewModel.cs    NotificationsViewModel.cs
├── Notifications/
│   └── ToastService.cs              # AUMID 快捷方式自检/修复 + Toast 激活路由
└── Themes/
    └── Light/Dark 资源字典（颜色源自 Severity 映射）
```

### 3.6 tests/

xUnit；`Beacon.Core.Tests`（聚合/调度/退避/规则/通知引擎/ActionRunner，时钟注入）、`Beacon.Connections.Tests`（Fake HttpMessageHandler + API 夹具 + ETag 分支，程序集串行化避免共享客户端缓存竞态）、`Beacon.Storage.Tests`（往返/原子写/DPAPI，需 Windows 环境）、`Beacon.Actions.Tests`（open.url/local.command/http 执行器，Windows 专属用例 OS 门控）。全部接入 coverlet.collector（CI 上报 Codecov）。

## 4. 关键 NuGet 包（Directory.Packages.props 统一版本）

| 包 | 用于 |
|---|---|
| `Microsoft.WindowsAppSDK` | WinUI 3 运行时 |
| `Microsoft.Windows.SDK.BuildTools` | Win32 interop（NoActivate/热键等） |
| `CommunityToolkit.Mvvm` | MVVM（ObservableObject/RelayCommand） |
| `CommunityToolkit.WinUI.*` | 控件/动画 |
| 托盘 | 不引第三方库，Win32 `Shell_NotifyIcon` 自实现（TrayIconService） |
| `Microsoft.Toolkit.Uwp.Notifications` | Toast（unpackaged AUMID 自愈与激活路由） |
| `System.Drawing.Common` | 托盘图标按配置色运行时绘制（GDI+ 位图 → HICON，B-706） |
| GitHub API | 手写 REST 客户端（ETag/限额可控，B-301 决策，不引 Octokit） |
| `System.Security.Cryptography.ProtectedData` | DPAPI 密钥存储 |
| `Microsoft.Extensions.Hosting` / `.DependencyInjection` / `.Logging` | DI/日志 |
| `xunit` / `NSubstitute` / `coverlet.collector` | 测试与覆盖率 |

（以还原时最新 stable 为准，版本集中在 Directory.Packages.props。）

## 5. Windows 机器上的脚手架命令

```powershell
dotnet new sln -n Beacon
dotnet new classlib -o src/Beacon.Core -f net8.0-windows
dotnet new classlib -o src/Beacon.Connections -f net8.0-windows
dotnet new classlib -o src/Beacon.Actions -f net8.0-windows
dotnet new classlib -o src/Beacon.Storage -f net8.0-windows
dotnet new winui -o src/Beacon.App        # 模板可用缺失时：手建 csproj（WindowsAppSDK, WindowsAppSDKSelfTop=true, 打包=None）
dotnet new xunit -o tests/Beacon.Core.Tests
dotnet new xunit -o tests/Beacon.Connections.Tests
dotnet new xunit -o tests/Beacon.Storage.Tests
dotnet new xunit -o tests/Beacon.Actions.Tests
dotnet sln add (git ls-files "**/*.csproj")
```

注意：四个类库目标框架 `net8.0-windows`（DPAPI/Interop 需要）；仅 `Beacon.Core` 可考虑纯净 `net8.0` 以便未来跨平台复用——若如此，ISecretStore 等平台接口留接口、实现放对应项目。

## 6. 编码约定

- `nullable enable`、file-scoped namespace、C# 12。
- UI = MVVM（CommunityToolkit.Mvvm），View 不写业务；状态一律来自 ViewModel 订阅 Event Bus。
- 所有时间相关逻辑经 `IClock` 注入。
- 配置 JSON 用 `System.Text.Json` + source-gen converter；枚举存字符串。
- 日志经 `ILogger`，Secrets 一律不进日志（§RFC-001 §13）。
