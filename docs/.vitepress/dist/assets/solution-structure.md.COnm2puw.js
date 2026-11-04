import{_ as n,o as a,c as e,a2 as i}from"./chunks/framework.DGk0zlJw.js";const g=JSON.parse('{"title":"Beacon Solution / Project Directory","description":"","frontmatter":{},"headers":[],"relativePath":"solution-structure.md","filePath":"solution-structure.md"}'),t={name:"solution-structure.md"};function p(o,s,c,l,r,d){return a(),e("div",null,[...s[0]||(s[0]=[i(`<h1 id="beacon-solution-project-directory" tabindex="-1">Beacon Solution / Project Directory <a class="header-anchor" href="#beacon-solution-project-directory" aria-label="Permalink to &quot;Beacon Solution / Project Directory&quot;">​</a></h1><blockquote><p>Companion to <a href="./rfc/RFC-001-technical-design.html">RFC-001</a>. This document is based on <strong>the actual current repository structure</strong> (synchronized 2026-10-09). Beacon code can only be built and run on Windows (WinUI 3, CI <code>windows-latest</code>); a Linux development environment can build and run the 4 test projects as a local gate.</p></blockquote><h2 id="_1-solution-overview" tabindex="-1">1. Solution Overview <a class="header-anchor" href="#_1-solution-overview" aria-label="Permalink to &quot;1. Solution Overview&quot;">​</a></h2><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon/</span></span>
<span class="line"><span>├── Beacon.sln</span></span>
<span class="line"><span>├── Directory.Build.props            # 公共编译设置（nullable、langversion、invariants）</span></span>
<span class="line"><span>├── Directory.Packages.props         # Central Package Management（统一版本）</span></span>
<span class="line"><span>├── global.json                      # 钉 SDK 10.0.100（rollForward latestFeature）</span></span>
<span class="line"><span>├── .editorconfig</span></span>
<span class="line"><span>├── .github/workflows/               # ci.yml（构建+测试+Codecov）/ nightly.yml（nightly）/ docs.yml（Pages）/ msix-dry-run.yml（MSIX 打包演练）+ scripts/（visual-evidence.ps1 等取证脚本）</span></span>
<span class="line"><span>├── docs/                            # 任务书 / RFC / Issue 列表 / 定位 / 审计 / 验收记录</span></span>
<span class="line"><span>├── packaging/                       # beacon.iss：Inno Setup 安装包定义（nightly 产出 setup.exe）+ wizard 图 + ChineseSimplified.isl + msix/（appxmanifest + logo PNG）</span></span>
<span class="line"><span>├── src/</span></span>
<span class="line"><span>│   ├── Beacon.App/                  # WinUI 3 壳（唯一 exe，代码建 UI 为主）</span></span>
<span class="line"><span>│   ├── Beacon.Core/                 # 领域模型 + 抽象 + 服务（无 UI 依赖）</span></span>
<span class="line"><span>│   ├── Beacon.Connections/          # 连接与 Widget Provider（17 类：GitHub / Jenkins / 本机 / Herald / HTTP / 智谱 / 方舟 / Claude / Kimi / Moonshot / DeepSeek / MiMo / Codex / Copilot / OpenCode / 百炼 / 商汤）</span></span>
<span class="line"><span>│   ├── Beacon.Actions/              # Action 执行器</span></span>
<span class="line"><span>│   └── Beacon.Storage/              # JSON 配置 / 缓存 / DPAPI Secrets / 导入导出</span></span>
<span class="line"><span>└── tests/</span></span>
<span class="line"><span>    ├── Beacon.Core.Tests/</span></span>
<span class="line"><span>    ├── Beacon.Connections.Tests/</span></span>
<span class="line"><span>    ├── Beacon.Storage.Tests/</span></span>
<span class="line"><span>    └── Beacon.Actions.Tests/</span></span></code></pre></div><h2 id="_2-project-dependency-rules" tabindex="-1">2. Project Dependency Rules <a class="header-anchor" href="#_2-project-dependency-rules" aria-label="Permalink to &quot;2. Project Dependency Rules&quot;">​</a></h2><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.App ──────► Beacon.Core ◄────── Beacon.Connections</span></span>
<span class="line"><span>    │                  ▲              Beacon.Actions</span></span>
<span class="line"><span>    │                  │              Beacon.Storage</span></span>
<span class="line"><span>    └──────────────────┴──────────────────┘（实现 Core 抽象，被 App 组合）</span></span>
<span class="line"><span>tests/* ──► 对应被测项目（+ Beacon.Core 接口）；例外：Connections.Tests 另引 Beacon.Storage（共享 fixture，tests/Beacon.Connections.Tests/Beacon.Connections.Tests.csproj:8）</span></span></code></pre></div><p>Hard rules:</p><ol><li><code>Beacon.Core</code> <strong>references no projects</strong> (only BCL + <code>Microsoft.Extensions.Logging.Abstractions</code>) — ensures UI and Provider are replaceable.</li><li>Connections / Actions / Storage depend only on Core (implementing its interfaces) and are independent of each other.</li><li><code>Beacon.App</code> is the only executable project, responsible for DI assembly: registers Providers/Executors/Stores into <code>Microsoft.Extensions.DependencyInjection</code> (<code>App.BuildServices</code> + <code>BeaconRuntime</code>).</li><li>In the Phase 2 plugin transition, split the Providers inside Connections into separate assemblies, keeping the interfaces unchanged.</li></ol><h2 id="_3-contents-of-each-project" tabindex="-1">3. Contents of Each Project <a class="header-anchor" href="#_3-contents-of-each-project" aria-label="Permalink to &quot;3. Contents of Each Project&quot;">​</a></h2><h3 id="_3-1-src-beacon-core-class-library" tabindex="-1">3.1 src/Beacon.Core (class library) <a class="header-anchor" href="#_3-1-src-beacon-core-class-library" aria-label="Permalink to &quot;3.1 src/Beacon.Core (class library)&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.Core/</span></span>
<span class="line"><span>├── Models/                            # 17 文件</span></span>
<span class="line"><span>│   ├── Severity.cs                    # Info/Success/Warning/Error/Critical（+ Offline 展示态）</span></span>
<span class="line"><span>│   ├── LifecycleState.cs              # Queued/Running/…/Unknown</span></span>
<span class="line"><span>│   ├── ConnectionConfig.cs  ConnectionHealthState.cs  WidgetConfig.cs  WidgetTypeDescriptor.cs</span></span>
<span class="line"><span>│   ├── WidgetDisplayPolicy.cs         # 数值类悬浮总闸（FloatingOptIn × NumericFloatingEnabled，两 L0 形态共用）</span></span>
<span class="line"><span>│   ├── WidgetState.cs                 # widgetId/widgetType/connectionId/severity/summary/payload/…</span></span>
<span class="line"><span>│   ├── ActionModels.cs                # ActionConfig / ActionResult（合并文件）</span></span>
<span class="line"><span>│   ├── NotificationModels.cs          # NotificationRule / NotificationRecord / Delivery（合并文件）</span></span>
<span class="line"><span>│   ├── RefreshTiers.cs                # 7 级刷新策略（pr/ci/machine/agent/workflow/static/default）</span></span>
<span class="line"><span>│   ├── AppConfig.cs                   # 全局设置 + PinTile/PinsConfig</span></span>
<span class="line"><span>│   ├── PinLayout.cs                   # monitor/anchor/offsetDips/collapsed + floatingX/Y（悬浮框位置）</span></span>
<span class="line"><span>│   ├── ComponentBinding.cs             # 组件实例绑定（widget 数据 → 展示字段，组件库）</span></span>
<span class="line"><span>│   ├── ComponentDescriptor.cs          # 组件声明描述（type/label/默认值，组件库注册表）</span></span>
<span class="line"><span>│   ├── ComponentSourcesValidator.cs    # 组件源字段合法性校验（fail-closed 口径）</span></span>
<span class="line"><span>│   └── WidgetInstance.cs               # 组件实例（组件库，含实例样式覆盖）</span></span>
<span class="line"><span>├── Abstractions/                      # 9 文件</span></span>
<span class="line"><span>│   ├── IConnectionProvider.cs         # IConnectionProvider + IWidgetProvider + IWidgetProviderResolver（合并文件）</span></span>
<span class="line"><span>│   ├── IActionExecutor.cs  ISecretStore.cs  IConfigurationStore.cs  ICacheStore.cs</span></span>
<span class="line"><span>│   ├── IClock.cs                      # 注入时钟，供退避/冷却单测</span></span>
<span class="line"><span>│   ├── IStateStore.cs                  # 状态存储抽象（Store 注入，组件库）</span></span>
<span class="line"><span>│   ├── IEventBus.cs</span></span>
<span class="line"><span>│   └── INotificationSink.cs           # 通知出口（App 侧 Toast/声音/托盘着色实现）+ 记录落盘抽象 INotificationRecordStore</span></span>
<span class="line"><span>├── Services/                           # 37 文件</span></span>
<span class="line"><span>│   ├── EventBus.cs                    # 线程安全的进程内发布/订阅</span></span>
<span class="line"><span>│   ├── RefreshScheduler.cs            # (connection,tier,间隔覆盖) 合并轮询 + 抖动 + 退避</span></span>
<span class="line"><span>│   ├── StatusAggregator.cs            # WidgetState* → 总体 Severity + 计数</span></span>
<span class="line"><span>│   ├── NotificationEngine.cs          # 规则求值 + 冷却去重 + 默认规则</span></span>
<span class="line"><span>│   ├── ActionRunner.cs                # 确认策略 + 超时 + 取消 + 可选重试（DSL retry 参数，默认 1 次）</span></span>
<span class="line"><span>│   ├── ActionVisibility.cs            # 动作显隐纯函数（widgetType 逗号多值 + connection_id 限定）</span></span>
<span class="line"><span>│   ├── WidgetHost.cs  TemplateRenderer.cs  PaletteResolver.cs  PinLayoutMath.cs</span></span>
<span class="line"><span>│   ├── SvgPathParser.cs               # SVG path d → 中性几何（品牌图标渲染前置层，纯函数供单测）</span></span>
<span class="line"><span>│   ├── BrandIcons.cs                  # Simple Icons CC0 品牌 path 字典 + 连接类型别名归一（纯字符串零依赖，供单测；未收录由 App 字形兜底）</span></span>
<span class="line"><span>│   ├── WidgetValueHint.cs             # 悬浮 tile 数值位短文本（各家额度源 payload 键 → 主数值映射 + 多窗口额度「NN%/窗」拼接，纯函数供单测）</span></span>
<span class="line"><span>│   ├── DebugInjection.cs              # debug 注入面（BEACON_DEBUG_SEVERITY=critical → 刷新成功态改判 Critical/Failed；环境变量门控、缺省零行为，仅 CI 场景 V 与装机演练用，不进用户文档）</span></span>
<span class="line"><span>│   ├── WidgetTypeNames.cs             # 组件类型 → 短显示名（tile 标签/L2 分组 chip 共用，纯函数供单测）</span></span>
<span class="line"><span>│   ├── QuickPanelSections.cs          # L2 面板三分区判定（Warning+/过期/断连 → NEEDS ATTENTION，纯函数供单测）</span></span>
<span class="line"><span>│   ├── DpiLayoutMath.cs               # DIP↔物理像素唯一换算口径（AppWindow 只认物理像素）</span></span>
<span class="line"><span>│   ├── ContrastMath.cs                # WCAG 2.x 对比度数学（纯函数，Linux 门禁可测）</span></span>
<span class="line"><span>│   ├── HighContrastPalette.cs         # 高对比调色板（WCAG AA 4.5:1，配对经 Core.Tests 断言）</span></span>
<span class="line"><span>│   ├── UpdateCheck.cs                 # 版本解析/比较/判定 + GitHub tag_name 提取（纯函数，Linux 门禁可测；App 侧只做网络与 UI）</span></span>
<span class="line"><span>│   ├── DispatchGuard.cs                 # UI 派发回调异常包装（WinUI 无 DispatcherQueue.HandlerException，转交回调/兜底吞）</span></span>
<span class="line"><span>│   ├── QuickPanelFilter.cs              # L2 面板分组过滤（SourceKey/组件源分组，纯函数）</span></span>
<span class="line"><span>│   ├── UiDensity.cs                     # 密度三档（常规/紧凑/宽舒，纯函数换算）</span></span>
<span class="line"><span>│   ├── ProgressTiers.cs                 # 进度条分档（档内线性/档间分段，纯函数）</span></span>
<span class="line"><span>│   ├── QuotaWindows.cs                  # 多窗口额度展示（NN%/窗 拼接，纯函数）</span></span>
<span class="line"><span>│   ├── TrendSeries.cs                   # 趋势序列归一化（payload trend 键，fail-closed）</span></span>
<span class="line"><span>│   ├── TimelineEntries.cs               # 时间线条目构建（纯函数）</span></span>
<span class="line"><span>│   ├── UiStrings.cs                     # 界面文案单源（标签/提示，纯函数）</span></span>
<span class="line"><span>│   ├── WidgetFeedback.cs                # 组件反馈四态判定（错误/离线/空/无，纯函数）</span></span>
<span class="line"><span>│   ├── WidgetInstanceResolver.cs        # 组件实例解析（widget 数据 → 实例绑定）</span></span>
<span class="line"><span>│   ├── ComponentBindingEngine.cs        # 组件绑定引擎（字段填充 + 缺失/越界处理）</span></span>
<span class="line"><span>│   ├── ComponentCenterCatalog.cs        # 组件中心目录（注册类型按 status/machine/events/quota/other 五组分组 + 显隐判定/落库单源，纯函数）</span></span>
<span class="line"><span>│   ├── ComponentSourceIndex.cs          # 组件源索引（数据源字段 → 组件的可用性索引）</span></span>
<span class="line"><span>│   ├── ComponentVisibility.cs           # 组件可见性纯函数（stale/offline/has:字段/字段 op 值）</span></span>
<span class="line"><span>│   ├── DisplayTemplates.cs              # 显示模板注册表（横条/圆环/数字卡/水位/状态行 + 最小高）</span></span>
<span class="line"><span>│   ├── StateStore.cs                    # 状态存储（事件总线 → 组件状态，替换/合并语义）</span></span>
<span class="line"><span>│   └── HtmlTemplateParser.cs            # 受控 HTML 子集解析器（白名单标签/属性，sanitize）</span></span>
<span class="line"><span>├── Events/</span></span>
<span class="line"><span>│   └── Events.cs                      # WidgetStateChanged / AggregateStatusChanged 等事件</span></span>
<span class="line"><span>└── Json/</span></span>
<span class="line"><span>    └── BeaconJson.cs                  # JsonSerializerOptions（camelCase + 枚举字符串）</span></span></code></pre></div><h3 id="_3-2-src-beacon-connections-class-library-flat-files-with-no-subdirectories" tabindex="-1">3.2 src/Beacon.Connections (class library, flat files with no subdirectories) <a class="header-anchor" href="#_3-2-src-beacon-connections-class-library-flat-files-with-no-subdirectories" aria-label="Permalink to &quot;3.2 src/Beacon.Connections (class library, flat files with no subdirectories)&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.Connections/                    # 34 文件</span></span>
<span class="line"><span>├── TileSizing.cs                      # 全类型 tile 宽度字宽预算（逐列最坏文本：标签/数值/读数 + 固定件 + 间距，下限 168；纯函数供单测，2026-10-12 二修）</span></span>
<span class="line"><span>├── GitHub 6 文件</span></span>
<span class="line"><span>│   ├── GitHubConnectionProvider.cs    # PAT 认证、连接测试、rate-limit 感知</span></span>
<span class="line"><span>│   ├── GitHubApiClient.cs             # 手写 REST 客户端（不引 Octokit）+ ETag 条件请求 + 限额感知</span></span>
<span class="line"><span>│   ├── GitHubWidgetDescriptors.cs     # Widget 类型元数据注册表（type: github.pull_requests / github.actions.runs）</span></span>
<span class="line"><span>│   ├── GitHubPullRequestsProvider.cs  # type: github.pull_requests</span></span>
<span class="line"><span>│   ├── GitHubActionsProvider.cs       # type: github.actions.runs</span></span>
<span class="line"><span>│   └── GitHubWorkflowExecutors.cs     # gh.workflow_dispatch/rerun/cancel 执行器</span></span>
<span class="line"><span>├── Jenkins 1 文件</span></span>
<span class="line"><span>│   └── JenkinsBuildProvider.cs        # type: jenkins.build（开源标准 JSON API，color 判级）+ JenkinsConnectionProvider（同文件）</span></span>
<span class="line"><span>├── Machine 4 文件（本机数据，零凭据零网络）</span></span>
<span class="line"><span>│   ├── MachineNetProvider.cs          # type: machine.net（IP/↑↓速率/累计流量，差分算速率）+ MachineWidgetDescriptors + MachineConnectionProvider（同文件）</span></span>
<span class="line"><span>│   ├── MachineLocalProvider.cs        # type: machine.local（CPU/内存/磁盘，CPU 差分同速率口径；Linux /proc + Windows API 注入抽象）</span></span>
<span class="line"><span>│   ├── MachineShowOptions.cs          # config[&quot;show&quot;] 可选展示项解析（缺省全选/未知键 fail-loud；宽度推算在 TileSizing）</span></span>
<span class="line"><span>│   └── GitWorkspaceProvider.cs        # type: machine.git（本地 Git 工作区 ↑↓·改·stash，git CLI 只读）</span></span>
<span class="line"><span>├── Herald 1 文件（用户自有项目 cuihairu/herald：统一订阅与投递中枢）</span></span>
<span class="line"><span>│   └── HeraldProvider.cs              # type: herald.incidents（事件台账 open 亮黄/critical 亮红）+ herald.feed（RSS 未读基线，token 走 DPAPI）+ herald.deliveries（投递失败 failed 即黄）+ HeraldConnectionProvider（同文件）</span></span>
<span class="line"><span>├── HTTP 6 文件（Generic HTTP 接入）</span></span>
<span class="line"><span>│   ├── HttpConnectionProvider.cs      # 连接测试（支持 auth_header/auth_prefix 覆盖）</span></span>
<span class="line"><span>│   ├── HttpEndpoint.cs                # 端点请求与凭据装配</span></span>
<span class="line"><span>│   ├── HttpJsonPath.cs                # $.a.b[0] 点路径提取</span></span>
<span class="line"><span>│   ├── HttpQuotaProvider.cs           # type: http.quota（任意配额 JSON 字段映射 → 数值卡 + 进度条）</span></span>
<span class="line"><span>│   ├── HttpStatusProvider.cs          # type: http.status（点路径 + 状态词表 → Severity）</span></span>
<span class="line"><span>│   └── ProviderDsl.cs                 # YAML provider DSL（批3-补42）：dsl_path 声明层（mapping 数据提取缺省 + actions 带 retry）+ YamlLite 极小解析器</span></span>
<span class="line"><span>├── BigModel 2 文件</span></span>
<span class="line"><span>│   ├── BigModelConnectionProvider.cs  # 裸 Key 认证（Authorization 不带 Bearer）</span></span>
<span class="line"><span>│   └── BigModelUsageProvider.cs       # type: bigmodel.usage（quota/limit 三窗口归一化）</span></span>
<span class="line"><span>├── Ark 2 文件</span></span>
<span class="line"><span>│   ├── ArkConnectionProvider.cs       # V4 签名连接测试（失败按 4xx/5xx 分级 Degraded/Offline）</span></span>
<span class="line"><span>│   └── ArkUsageProvider.cs            # type: ark.usage（GetCodingPlanUsage 三窗口百分比；AK/SK 只进 DPAPI）</span></span>
<span class="line"><span>├── Claude 2 文件</span></span>
<span class="line"><span>│   ├── ClaudeConnectionProvider.cs    # 零网络连接测试（会话目录存在即 Healthy）</span></span>
<span class="line"><span>│   └── ClaudeUsageProvider.cs         # type: claude.usage（本机 JSONL 聚合，零凭据）</span></span>
<span class="line"><span>├── Kimi 1 文件</span></span>
<span class="line"><span>│   └── KimiCodingUsageProvider.cs     # type: kimi.coding（5h/周套餐余量）+ KimiConnectionProvider（同文件）</span></span>
<span class="line"><span>├── DeepSeek 1 文件</span></span>
<span class="line"><span>│   └── DeepSeekBalanceProvider.cs     # type: deepseek.balance（开放平台余额）+ DeepSeekConnectionProvider（同文件）</span></span>
<span class="line"><span>├── MiMo 1 文件</span></span>
<span class="line"><span>│   └── MiMoUsageProvider.cs           # type: mimo.usage（官方未开放用量口→模型目录真数据）+ MiMoConnectionProvider（同文件）</span></span>
<span class="line"><span>├── Codex 1 文件</span></span>
<span class="line"><span>│   └── CodexUsageProvider.cs          # type: codex.usage（本机 rollout 会话统计，source=local 如实标注）+ CodexConnectionProvider（同文件）</span></span>
<span class="line"><span>├── Copilot 1 文件</span></span>
<span class="line"><span>│   └── CopilotUsageProvider.cs        # type: copilot.usage（官方扩展同款配额端点直连，quota_snapshots 三槽）+ CopilotConnectionProvider（同文件）</span></span>
<span class="line"><span>├── OpenCode 1 文件</span></span>
<span class="line"><span>│   └── OpenCodeUsageProvider.cs       # type: opencode.usage（官方 Console Budgets API 直连，micro-cents 金额）+ OpenCodeConnectionProvider（同文件）</span></span>
<span class="line"><span>├── Qwen 1 文件</span></span>
<span class="line"><span>│   └── QwenUsageProvider.cs           # type: qwen.usage（百炼 Token Plan 四档探测：官方口→自定义→配额探针→模型目录兜底）+ QwenConnectionProvider（同文件）</span></span>
<span class="line"><span>├── Moonshot 1 文件</span></span>
<span class="line"><span>│   └── MoonshotBalanceProvider.cs     # type: moonshot.balance（开放平台余额，区别于 Kimi For Coding 套餐）+ MoonshotConnectionProvider（同文件）</span></span>
<span class="line"><span>└── SenseNova 1 文件</span></span>
<span class="line"><span>    └── SenseNovaUsageProvider.cs     # type: sensenova.usage（控制台 Token Plan 积分池：多池 × 5h/7d 双窗，最差窗判级）+ SenseNovaWidgetDescriptors + SenseNovaConnectionProvider（同文件）</span></span></code></pre></div><h3 id="_3-3-src-beacon-actions-class-library" tabindex="-1">3.3 src/Beacon.Actions (class library) <a class="header-anchor" href="#_3-3-src-beacon-actions-class-library" aria-label="Permalink to &quot;3.3 src/Beacon.Actions (class library)&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.Actions/                        # 5 文件</span></span>
<span class="line"><span>├── ActionVars.cs                      # 参数模板占位符替换（{repo} 等）</span></span>
<span class="line"><span>├── OpenUrlExecutor.cs                 # open.url（系统默认浏览器）</span></span>
<span class="line"><span>├── HttpExecutor.cs                    # http（GET/POST + 头/体模板）</span></span>
<span class="line"><span>├── WebhookExecutor.cs                 # webhook（POST + HMAC-SHA256 签名头，密钥 credentialRef 只进 DPAPI、取不到即 fail closed）</span></span>
<span class="line"><span>└── LocalCommandExecutor.cs            # local.command（powershell/cmd/exe，工作目录/Env/超时/捕获输出）</span></span></code></pre></div><ul><li>The three executors <code>gh.workflow_dispatch / gh.workflow_rerun / gh.workflow_cancel</code> are in <code>Beacon.Connections/GitHubWorkflowExecutors.cs</code>.</li></ul><h3 id="_3-4-src-beacon-storage-class-library" tabindex="-1">3.4 src/Beacon.Storage (class library) <a class="header-anchor" href="#_3-4-src-beacon-storage-class-library" aria-label="Permalink to &quot;3.4 src/Beacon.Storage (class library)&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.Storage/                        # 6 文件</span></span>
<span class="line"><span>├── JsonConfigurationStore.cs          # config/connections/widgets/pins.json 原子写 + 备份</span></span>
<span class="line"><span>├── AppConfigFile.cs                   # config.json 快速读写（App 启动主题等早期读取）</span></span>
<span class="line"><span>├── JsonNotificationRecordStore.cs     # notifications.json 通知记录 + 已读态（滚动保留跨重启）</span></span>
<span class="line"><span>├── JsonCacheStore.cs                  # cache\\{connId}\\states.json last-known-state</span></span>
<span class="line"><span>├── DpapiSecretStore.cs                # DPAPI CurrentUser → secrets.bin（唯一默认实现）</span></span>
<span class="line"><span>└── ImportExport.cs                    # 打包导出（不含 secrets）/导入</span></span></code></pre></div><p>Alternative (not implemented): Windows Credential Manager secret storage — same <code>ISecretStore</code> interface, code not landed.</p><h3 id="_3-5-src-beacon-app-winui-3-packaging-none" tabindex="-1">3.5 src/Beacon.App (WinUI 3, packaging=None) <a class="header-anchor" href="#_3-5-src-beacon-app-winui-3-packaging-none" aria-label="Permalink to &quot;3.5 src/Beacon.App (WinUI 3, packaging=None)&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.App/</span></span>
<span class="line"><span>├── App.xaml / App.xaml.cs             # 入口：单实例、全局异常兜底、DI 组装、窗口装配、退出清理</span></span>
<span class="line"><span>├── app.manifest / Assets/             # PerMonitorV2 DPI；图标与托盘 logo 掩膜（嵌入资源）</span></span>
<span class="line"><span>├── Infrastructure/</span></span>
<span class="line"><span>│   ├── MonitorService.cs              # 显示器枚举/工作区/按像素定位（多屏）</span></span>
<span class="line"><span>│   ├── NativeMethods.cs               # P/Invoke（NOTIFYICONDATA、窗口扩展样式、DPI、热键）</span></span>
<span class="line"><span>│   └── Win32MessageWindow.cs          # 自驻留消息窗（托盘/热键/单实例事件回调）</span></span>
<span class="line"><span>├── Services/                          # 17 文件</span></span>
<span class="line"><span>│   ├── BeaconRuntime.cs               # 运行时装配：Provider 解析器、调度、聚合、启停联动</span></span>
<span class="line"><span>│   ├── UpdateCheckService.cs          # 检查更新（B-806）：GitHub releases/latest，10s 超时 + 进程内缓存 + 失败降级不抛异常</span></span>
<span class="line"><span>│   ├── TrayIconService.cs             # Win32 Shell_NotifyIcon 自实现：菜单、按配置色着色、气泡</span></span>
<span class="line"><span>│   ├── ToastNotificationService.cs    # Microsoft.Toolkit.Uwp.Notifications（Compat 激活路由、深链 L3）</span></span>
<span class="line"><span>│   ├── TopmostGuard.cs                # 置顶哨兵（Watch 单窗 / WatchAll 动态窗集）</span></span>
<span class="line"><span>│   ├── HotkeyService.cs               # RegisterHotKey，可配置（默认 Ctrl+Alt+B）</span></span>
<span class="line"><span>│   ├── StartupService.cs              # HKCU Run 键开关</span></span>
<span class="line"><span>│   ├── SingleInstanceGuard.cs         # Mutex + 命名事件（二次启动通知既有实例弹气泡）</span></span>
<span class="line"><span>│   ├── FileLoggerProvider.cs  CrashLog.cs    # beacon-YYYYMMDD.log / crash-*.log</span></span>
<span class="line"><span>│   ├── UiPalette.cs  MotionEngine.cs  ShellStateStore.cs  UiDispatcher.cs</span></span>
<span class="line"><span>│   ├── DispatcherQueueGuard.cs        # UI 派发兜底（TryEnqueue 回调异常不闪退：落日志+CrashLog，包装 Core DispatchGuard）</span></span>
<span class="line"><span>│   ├── DesignTokens.cs                # 设计令牌（间距/圆角/字阶/尺寸，UI 升级）</span></span>
<span class="line"><span>│   └── AppIcon.cs                     # 应用图标唯一来源（逐窗 AppWindow.SetIcon——unpackaged 任务栏图标坑）</span></span>
<span class="line"><span>└── Windows/                           # 11 文件（9 .cs + 2 .xaml，UI 多为代码构建）</span></span>
<span class="line"><span>    ├── CapsuleWindow.xaml|cs          # L1 聚合胶囊（独立置顶常驻窗，位置记忆；右键直达设置）</span></span>
<span class="line"><span>    ├── QuickPanelWindow.xaml|cs       # L2 快捷面板（可激活，ESC 关；通知中心/组件列表）</span></span>
<span class="line"><span>    ├── PinnedHostWindow.cs            # ★ L0 宿主面板形态（默认）：每显示器一窗，代码建 UI，</span></span>
<span class="line"><span>    │                                  #   Topmost+NoActivate+空白点击穿透；含 PinTile 控件</span></span>
<span class="line"><span>    ├── FloatingTileWindow.cs          # ★ L0 独立悬浮框形态（pinDisplayMode=floating）：</span></span>
<span class="line"><span>    │                                  #   每钉选组件一窗，拖放位置按组件持久化（FloatingX/Y）</span></span>
<span class="line"><span>    ├── BrandIconFactory.cs            # 品牌 path → Path(Stretch=Uniform)（设置目录/悬浮 tile 共用，</span></span>
<span class="line"><span>    │                                  #   空几何/渲染失败退 null 让调用方字形兜底）</span></span>
<span class="line"><span>    ├── DetailWindow.cs                # L3 详情 + 全部 Action（代码建 UI）</span></span>
<span class="line"><span>    ├── ThemeColors.cs                 # 主题色桥：Core 高对比调色板 → WinUI 画刷（配对断言见 ContrastMathTests）；语义色板 StatusOk/Warn/Crit/Off/Accent（UI 升级）</span></span>
<span class="line"><span>    ├── SampleGalleryWindow.cs         # 组件中心（组件库预览/参数表单/实时渲染，A-16 复核用）</span></span>
<span class="line"><span>    └── SettingsWindow.cs              # B-801/802/805：PowerToys 形态配置中心（模块目录+启停+连接/组件 CRUD，</span></span>
<span class="line"><span>                                       #   悬浮形态二选一、导入导出、通知规则；代码建 UI）</span></span></code></pre></div><h3 id="_3-5b-src-beacon-app-components-组件库渲染层-2026-10-16-组件化重构" tabindex="-1">3.5b src/Beacon.App/Components（组件库渲染层，2026-10-16 组件化重构） <a class="header-anchor" href="#_3-5b-src-beacon-app-components-组件库渲染层-2026-10-16-组件化重构" aria-label="Permalink to &quot;3.5b src/Beacon.App/Components（组件库渲染层，2026-10-16 组件化重构）&quot;">​</a></h3><div class="language-text vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>Beacon.App/Components/                 # 19 文件</span></span>
<span class="line"><span>├── ComponentSurface.cs  ComponentSurfaceRenderer.cs   # 表面 + 声明式渲染器（注册表在渲染侧）</span></span>
<span class="line"><span>├── ComponentAction.cs  ComponentBadge.cs  ComponentCard.cs  ComponentChart.cs</span></span>
<span class="line"><span>├── ComponentDivider.cs  ComponentFeedback.cs  ComponentIcon.cs  ComponentKeyValue.cs</span></span>
<span class="line"><span>├── ComponentLayout.cs  ComponentList.cs  ComponentMetric.cs  ComponentProgress.cs</span></span>
<span class="line"><span>├── ComponentStatus.cs  ComponentTable.cs  ComponentText.cs  ComponentTimeline.cs</span></span>
<span class="line"><span>└── StatusRing.cs                        # 状态圆环（轨道+弧长描边，胶囊健康环/面板卡共用）</span></span></code></pre></div><p>Note: no <code>Controls/</code>, <code>ViewModels/</code>, <code>Themes/</code>, <code>Notifications/</code> directories — 但 Components/（组件库渲染层）存在，见 §3.5b；PinTile 在 PinnedHostWindow.cs 内 — status lights/PinTile etc. are composed as code classes (<code>PinTile</code> is inside <code>PinnedHostWindow.cs</code>), no XAML resource dictionaries; Toast is in <code>Services/</code>.</p><h3 id="_3-6-tests" tabindex="-1">3.6 tests/ <a class="header-anchor" href="#_3-6-tests" aria-label="Permalink to &quot;3.6 tests/&quot;">​</a></h3><p>xUnit; <code>Beacon.Core.Tests</code> (aggregation/scheduling/backoff/rules/notification engine/ActionRunner, clock injection; SvgPathParser, full parseability and aliases of the BrandIcons dictionary, WidgetValueHint numeric mapping), <code>Beacon.Connections.Tests</code> (fake HttpMessageHandler + API fixtures + ETag branches, assembly serialization to avoid shared client cache races), <code>Beacon.Storage.Tests</code> (round-trip/atomic write/DPAPI, requires Windows environment), <code>Beacon.Actions.Tests</code> (open.url/local.command/http/webhook executors, Windows-only test cases OS-gated). All wired into coverlet.collector (CI reports to Codecov). Currently 4 projects with a total of 1674 tests (Core 939 / Connections 667 / Actions 27 / Storage 41), i.e. the local gate (2026-11-05 recount). The Core side also covers QuickPanelSections three-section classification, ContrastMath/HighContrastPalette contrast pairings, WidgetTypeNames short-name mapping, PinLayoutMath/DpiLayoutMath conversion and clamping, and UpdateCheck version parsing and SemVer comparison (v prefix/build metadata/pre-release rules/rate-limit degraded body).</p><h2 id="_4-key-nuget-packages-unified-versions-in-directory-packages-props" tabindex="-1">4. Key NuGet Packages (unified versions in Directory.Packages.props) <a class="header-anchor" href="#_4-key-nuget-packages-unified-versions-in-directory-packages-props" aria-label="Permalink to &quot;4. Key NuGet Packages (unified versions in Directory.Packages.props)&quot;">​</a></h2><table tabindex="0"><thead><tr><th>Package</th><th>Used for</th></tr></thead><tbody><tr><td><code>Microsoft.WindowsAppSDK</code></td><td>WinUI 3 runtime</td></tr><tr><td><code>Microsoft.Windows.SDK.BuildTools</code></td><td>Win32 interop (NoActivate/hotkeys, etc.)</td></tr><tr><td><code>CommunityToolkit.Mvvm</code></td><td>Referenced in App project but currently unused (UI is code-behind, see §6)</td></tr><tr><td>Tray</td><td>No third-party library; Win32 <code>Shell_NotifyIcon</code> implemented in-house (TrayIconService)</td></tr><tr><td><code>Microsoft.Toolkit.Uwp.Notifications</code></td><td>Toast (unpackaged AUMID self-healing and activation routing)</td></tr><tr><td><code>System.Drawing.Common</code></td><td>Tray icon drawn at runtime with configured color (GDI+ bitmap → HICON, B-706)</td></tr><tr><td>GitHub API</td><td>Hand-written REST client (ETag/rate-limit controllable, B-301 decision, no Octokit)</td></tr><tr><td><code>System.Security.Cryptography.ProtectedData</code></td><td>DPAPI secret storage</td></tr><tr><td><code>Microsoft.Extensions.Hosting</code> / <code>.Logging.Console</code>+<code>.Logging.Abstractions</code>+<code>.DependencyInjection.Abstractions</code></td><td>DI/logging (<code>Logging.Abstractions</code> is Core&#39;s only direct reference; <code>Hosting</code>/<code>Logging.Console</code>/DI abstractions are directly referenced by App and the test projects)</td></tr><tr><td><code>xunit</code> / <code>NSubstitute</code> / <code>coverlet.collector</code> / <code>Microsoft.NET.Test.Sdk</code> / <code>xunit.runner.visualstudio</code></td><td>Testing and coverage</td></tr></tbody></table><p>(Versions centralized in Directory.Packages.props; the table excludes unreferenced packages.)</p><h2 id="_5-scaffolding-commands-on-windows-historical-archive-project-already-built" tabindex="-1">5. Scaffolding Commands on Windows (historical archive, project already built) <a class="header-anchor" href="#_5-scaffolding-commands-on-windows-historical-archive-project-already-built" aria-label="Permalink to &quot;5. Scaffolding Commands on Windows (historical archive, project already built)&quot;">​</a></h2><blockquote><p>The following are the commands actually used when initially creating the repository, retained for reference; the current structure and files are authoritative as in §1/§3.</p></blockquote><div class="language-powershell vp-adaptive-theme"><button title="Copy Code" class="copy"></button><span class="lang">powershell</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new sln </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">n Beacon</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new classlib </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o src</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Core </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-f</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;"> net10.</span><span style="--shiki-light:#005CC5;--shiki-dark:#79B8FF;">0</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new classlib </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o src</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Connections </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-f</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;"> net10.</span><span style="--shiki-light:#005CC5;--shiki-dark:#79B8FF;">0</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new classlib </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o src</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Actions </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-f</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;"> net10.</span><span style="--shiki-light:#005CC5;--shiki-dark:#79B8FF;">0</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new classlib </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o src</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Storage </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-f</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;"> net10.</span><span style="--shiki-light:#005CC5;--shiki-dark:#79B8FF;">0</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new winui </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o src</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.App        </span><span style="--shiki-light:#6A737D;--shiki-dark:#6A737D;"># 模板可用缺失时：手建 csproj（WindowsAppSDKSelfContained，打包=None）</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new xunit </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o tests</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Core.Tests</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new xunit </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o tests</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Connections.Tests</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new xunit </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o tests</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Storage.Tests</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet new xunit </span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">o tests</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">/</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">Beacon.Actions.Tests</span></span>
<span class="line"><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">dotnet sln add (git ls</span><span style="--shiki-light:#D73A49;--shiki-dark:#F97583;">-</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">files </span><span style="--shiki-light:#032F62;--shiki-dark:#9ECBFF;">&quot;**/*.csproj&quot;</span><span style="--shiki-light:#24292E;--shiki-dark:#E1E4E8;">)</span></span></code></pre></div><p>Note (.NET 10 LTS, RFC §16 decision 8): the four class libraries target <code>net10.0</code> (DPAPI via the <code>System.Security.Cryptography.ProtectedData</code> package, no Windows-specific TFM); <code>Beacon.App</code> targets <code>net10.0-windows10.0.19041.0</code>, <code>TargetPlatformMinVersion=10.0.17763.0</code> (WinUI 3 / Windows App SDK). The SDK version is pinned to <code>10.0.100</code> by the root <code>global.json</code> (rollForward latestFeature).</p><h2 id="_6-coding-conventions" tabindex="-1">6. Coding Conventions <a class="header-anchor" href="#_6-coding-conventions" aria-label="Permalink to &quot;6. Coding Conventions&quot;">​</a></h2><ul><li><code>nullable enable</code>, file-scoped namespace, C# 12.</li><li>UI is primarily <strong>code-built + code-behind</strong> (WinUI <code>Window</code> subclasses directly compose controls), state comes from event bus subscriptions (<code>BeaconRuntime</code>/<code>Subscribe</code> inside windows); the <code>CommunityToolkit.Mvvm</code> package is referenced but currently unused (no ViewModel layer).</li><li>All time-related logic is injected via <code>IClock</code>.</li><li>Configuration JSON uses <code>System.Text.Json</code>: <code>BeaconJson</code> centralizes <code>JsonSerializerOptions</code> (camelCase properties + enum strings), not source-gen.</li><li>Logging via <code>ILogger</code>, Secrets never go into logs (RFC-001 §13).</li></ul>`,35)])])}const u=n(t,[["render",p]]);export{g as __pageData,u as default};
