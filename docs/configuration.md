[English](configuration.en.md) | [中文](configuration.md)

# 配置项参考

Beacon 的全部配置落在 `%APPDATA%\Beacon\` 下的 JSON 文件里。设置页改的、导入包导的，都是这几份文件的内容。本文逐项列出每个配置的键名、类型、默认值和实际效果。

## 配置文件与生效时机

| 文件 | 内容 | 手改后生效 | 设置页改动 |
|---|---|---|---|
| config.json | 全局设置、外观、通知规则 | 重启后 | 即时（主题、悬浮形态除外，重启） |
| connections.json | 连接定义与凭据引用 | 重启后 | 即时（保存连接即重建注册） |
| widgets.json | 组件实例与字段配置 | 重启后 | 即时 |
| pins.json | 钉选布局与悬浮窗位置 | 随组件加载 | 即时 |
| actions.json | 自定义动作库 | 重启后 | 即时 |
| notifications.json | 通知记录（只读缓存，可删） | 重启水合 | 不提供编辑 |

读取失败不静默：主文件损坏时先试 `.bak` 备份，再不行落默认值并留档 `*.corrupt`，托盘气泡和日志里都会说明。诊断日志在 `%APPDATA%\Beacon\logs\beacon-*.log`。

## 全局设置（config.json）

| 键 | 类型 | 默认 | 效果 | 生效时机 |
|---|---|---|---|---|
| hotkey | 字符串 | `Ctrl+Alt+B` | 快捷面板全局热键。设置页「全局热键」可改；注册失败（被占用）托盘气泡提示 | 即时 |
| launchOnStartup | 布尔 | `true` | 开机自启。落注册表 Run 键，与设置页「开机自启」开关同步 | 即时 |
| theme | `system` / `light` / `dark` | `system` | 浅色/深色/跟随系统。WinUI 主题只能在进程启动时设定（2026-10-10 拍板维持，见 mvp-issues 拍板记录） | 重启 |
| uiOpacity | 数值 | `1.0` | 四类悬浮面（胶囊、宿主面板、独立悬浮框、快捷面板）统一透明度。设置页滑杆范围 0.2–1.0 步进 0.05（与运行时下限一致）；低于 0.2 按 0.2 处理 | 即时 |
| showCapsule | 布尔 | `false` | L1 状态胶囊（右下角常驻胶囊）显隐。默认关是 2026-10-09 的拍板，升级安装的老配置会被一次性拉平 | 即时 |
| keepQuickPanelOpen | 布尔 | `false` | L2 快捷面板常驻（托盘右键菜单「Keep Panel Open」打勾项，2026-10-11 用户令）。开 = 面板失焦不再自动隐藏、一直留在桌面（Esc/再次点托盘仍可关，重启保持勾选）；关 = Flyout 瞬态语义（点外面自动收） | 即时 |
| pinDisplayMode | `panel` / `floating` | `panel` | L0 悬浮形态：`panel` 单窗多 tile 面板；`floating` 每个钉选组件一个独立窗，可拖到桌面任意位置（2026-10-10 拍板维持重启生效） | 重启 |
| numericFloatingEnabled | 布尔 | `true` | 数值/额度类组件（AI 额度卡等）能否上独立悬浮窗。默认开（2026-10-11 用户令翻转）；关的时候这类钉选仍在宿主面板显示 | 即时 |
| pollIntervalSeconds | 整数（秒） | `0` | 全局检查频率，`0` = 按各组件刷新档的默认周期。设置页选项 15 秒/30 秒/1 分/5 分。组件单独设了「检测间隔」的以组件为准 | 即时（重建刷新调度） |
| settingsSidebarWidth | 数值（DIP） | `264` | 设置窗口左栏宽度。拖拽左右栏之间的分隔条（悬停变蓝）自动落盘，启动恢复；钳位 [200, 420] | 重启恢复（拖完即存） |
| settingsWindowWidth / settingsWindowHeight | 数值（DIP） | `0` | 设置窗口尺寸记忆，`0` = 用默认 880×640。拖动窗口边缘改大小、静止 0.8 秒后落盘，启动恢复 | 重启恢复 |
| notificationRules | 规则数组 | `[]` | 自定义通知规则，空数组用内置默认四条。见下节 | 即时（导入包路径）；手改文件重启 |
| appearance | 对象 | 见下节 | 级别色覆盖与动效 | 即时 |
| configVersion | 整数 | `1` | 配置结构版本号，程序迁移用，不要手改 | 程序维护 |
| numericFloatingResetDone | 布尔 | `false` | 已废弃（2026-10-11 数量悬浮窗改默认开，一次性置关迁移撤除），字段仅为旧配置兼容保留，不要手改 | 程序维护 |

### 外观（appearance）

级别色作用于托盘圆点、胶囊、组件状态灯。键缺省时用默认色表，托盘/胶囊/L0 全部跟随。

| 键 | 类型 | 默认 | 效果 |
|---|---|---|---|
| appearance.severityColors.info | `#RRGGBB` | `#58a6ff` | Info 级颜色 |
| appearance.severityColors.success | `#RRGGBB` | `#3fb950` | Success 级颜色 |
| appearance.severityColors.warning | `#RRGGBB` | `#d29922` | Warning 级颜色 |
| appearance.severityColors.error | `#RRGGBB` | `#f85149` | Error 级颜色 |
| appearance.severityColors.critical | `#RRGGBB` | `#ff3b30` | Critical 级颜色 |
| appearance.severityColors.offline | `#RRGGBB` | `#8b949e` | 连接离线颜色 |
| appearance.motion.mode | `full` / `reduced` / `off` | `reduced` | 动效档位：完全 / 适度 / 关闭。off 档全部动画静止 |
| appearance.motion.intensity | 数值 0.5–2.0 | `1.0` | 动效时长与幅度缩放。设置页滑杆步进 0.1 |
| appearance.progressStyle | `"bar"` / `"pool"` | `"bar"` | tile 额度显示方式：**进度条**（2026-10-11 重设计默认：底部 4 DIP 圆角条 + NN% 读数，≥80% warning、≥98% Error 红）/**圆池注水**（旧特效：水位=进度，满格 Error 红）。实时生效，无需重启 |

改色即时生效（托盘/胶囊/L0 按新色表重渲染），格式支持 `#RRGGBB` 和 `#AARRGGBB`，非法值报错不落盘。组件可以用 widgets.json 的 colorOverride 单独再覆盖某盏灯。

### 通知规则（notificationRules）

规则语义：某组件状态达到阈值频带时，发 Toast/声音，冷却期内不重复。字段如下：

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| id | 字符串 | 必填 | 规则标识，记录去重用 |
| widgetType | 字符串 | `null` | 匹配的组件类型，`null` = 所有类型 |
| widgetId | 字符串 | `null` | 匹配指定组件实例 |
| severityAtLeast | 枚举 | `warning` | 最低触发级别，取值 `success`/`info`/`warning`/`error`/`critical` |
| severityAtMost | 枚举 | `null` | 最高级别，与下限构成频带（如「仅 warning」），`null` = 无上限 |
| toast | 布尔 | `true` | 弹系统 Toast |
| sound | 布尔 | `false` | 提示音 |
| cooldown | `hh:mm:ss` | `null` | 重复提醒冷却期（如 `00:30:00`）。`null` = 同一阈值期间只提醒一次 |
| enabled | 布尔 | `true` | 规则开关 |

列表为空时用内置默认：

| 规则 id | 匹配 | 触发 | 冷却 |
|---|---|---|---|
| builtin.ci-failed | github.actions.runs | ≥ error | 每阈值一次 |
| builtin.ci-stuck | github.actions.runs | 仅 warning（卡住的运行） | 30 分钟 |
| builtin.pr-review | github.pull_requests | ≥ warning（待你 review） | 30 分钟 |
| builtin.connection-degraded | 所有组件（刷新失败） | ≥ warning | 15 分钟 |

设置页「高级 → 通知规则」展示当前生效的规则集（含内置默认的展开结果）。

## 连接（connections.json）

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| id | 字符串 | 必填 | 连接唯一名。凭据在 DPAPI 里按 `conn:{id}` 存 |
| type | 字符串 | 必填 | 连接类型，见下表 |
| endpoint | 字符串 | `null` | 服务端点，`null` = 该类型默认端点。可覆盖（自建代理/区域/验收 mock） |
| credentialRef | 字符串 | 类型默认 | DPAPI 凭据引用。密钥明文只进 DPAPI，JSON 里只有引用 |
| enabled | 布尔 | `true` | 模块启停。`false` 时组件停止刷新、UI 丢弃其状态、钉选不占桌面 |
| settings | 对象 | `{}` | 类型专属键，见各类型说明 |

### 各类型凭据与端点

设置页连接表单的 Token 框按类型给出提示，密钥一律只写 DPAPI：

| 类型 | 默认端点 | Token 框填什么 | 测试连通 |
|---|---|---|---|
| github | api.github.com | GitHub PAT | GET /rate_limit（不占核心配额） |
| jenkins | 必填实例基址（如 jenkins.example.com） | user:apiToken（可空 = 匿名读；Manage Jenkins → Users → API Token 创建，Basic 认证） | GET /api/json（2xx 且是 JSON 才算连通，防反向代理假阳性） |
| machine | 无（留空） | 无（本机数据，零凭据零网络） | 不适用：连接恒通（本机即本机） |
| herald | Herald 基址（必填，如 `http://127.0.0.1:8080`） | API Key（可空 = 匿名；`auth.enabled` 开启时必填，X-API-Key 头，只写 DPAPI） | GET `/api/v1/status`（恒公开口）：2xx 且是 JSON 才算连通，反代 HTML 壳按 Degraded |
| bigmodel | open.bigmodel.cn | 智谱 API Key（监控接口裸 Key 直传，无 Bearer 前缀） | GET quota/limit |
| ark | open.volcengineapi.com | 火山 `AccessKey:SecretKey` 冒号分隔一次粘贴（控制台「API 访问密钥」创建；推理 ARK_API_KEY 调不了管控面） | V4 签名调 GetCodingPlanUsage |
| claude | 无（读本机） | 留空 | 本机会话目录可读 |
| kimi | api.kimi.com/coding/v1/usages（社区 GET 口，默认）。控制台口 `www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages`：endpoint 整串直填 + Settings 加 `http_method=POST`、`auth_header=Cookie`（载荷照 F12 → 网络 → 该请求补 `http_body`） | 二选一：Kimi For Coding Key（sk-kimi- 前缀），或浏览器 Cookie（F12 → Cookies 复制值粘贴）。**Beacon 不做扫码/登录流**——Cookie 凭据由用户从浏览器复制粘贴，DPAPI 加密存储，请求时原样回放；会话过期（浏览器登出/过期）后重录一次即可。与 Moonshot 开放平台是两套账号 | GET usages / POST GetUsages（2xx 即连通） |
| moonshot | api.moonshot.cn/v1/users/me/balance | Moonshot 开放平台 API Key（Bearer；与 Kimi For Coding 是两套账号） | GET balance（响应须含余额数据） |
| sensenova | platform.sensenova.cn/lite/console/v1/tokenplan/pool-usage | 商汤控制台 JWT（Bearer）。这是**控制台会话令牌不是 API Key**——在 platform.sensenova.cn 控制台登录后复制；带 `exp` 会过期 | GET pool-usage（响应须含积分池数据） |
| deepseek | api.deepseek.com | DeepSeek API Key | GET /user/balance |
| mimo | token-plan-cn.xiaomimimo.com/v1 | 小米 API Key（Bearer；只授权模型调用，额度另录控制台 Cookie） | GET /models |
| qwen | token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1 | 百炼 Token Plan 专属 Key（sk-sp- 前缀） | GET /models |
| codex | 无（读本机） | 留空 | 本机 sessions 目录可读 |
| copilot | api.github.com/copilot_internal/user | GitHub PAT（需 copilot scope） | 同款请求头直连配额端点 |
| opencode | opencode.ai/console/api/v2/budgets/members | OpenCode Key（oc_sk 前缀，读 Budgets 需 All 权限） | GET budgets/members |
| http | 必填 | API Key（可选） | GET Endpoint，2xx 即健康 |

### 通用 Settings 键

走统一 HTTP 通道（`HttpEndpoint.FetchAsync`，`src/Beacon.Connections/HttpEndpoint.cs`）的连接类型（bigmodel/deepseek/kimi/moonshot/sensenova/http）支持：

> copilot/qwen/opencode/mimo **不走统一通道**（各自专用请求构造，各自端点与请求头硬编码）：只认连接级 `endpoint`（copilot/opencode）或 Settings `usage_endpoint`（qwen/mimo）与凭据，不认下表四个通道键。

| 键 | 默认 | 说明 |
|---|---|---|
| auth_header | `Authorization` | 认证头名称（Cookie 凭据即填 `Cookie`） |
| auth_prefix | `Bearer` | 值前缀，可显式置空（bigmodel 裸 Key、Cookie 原样回放即置空场景） |
| http_method | `GET` | 请求方法（POST 等；kimi 控制台 GetUsages 即 POST 口） |
| http_body | 空 | 请求体（JSON 文本，配置了才携带；内容照浏览器 F12 → 网络 → 该请求的载荷） |

### 类型专属 Settings 键

| 类型 | 键 | 说明 |
|---|---|---|
| ark | region | 签名区域，默认 `cn-beijing` |
| qwen | usage_endpoint | 自定义用量端点（GET + Bearer）。留空则显示模型目录并如实标注官方额度口未开放 |
| mimo | usage_endpoint | 自定义用量口（GET + Bearer）：计数形状出「本机累计 N 次」；百分比形状（used/total 等）出已用/百分比；认不出的形状显式 Degraded 并附原始响应（不透传上卡面） |
| mimo | litellm_spend_url | LiteLLM 网关记账端点（如 `http://127.0.0.1:4000/spend/logs`）。**默认空=不外发**（本机 127.0.0.1 记账不算外发）；请求带本月起止 `start_date/end_date + summarize=true`（网关 openapi 参数名） |
| mimo | litellm_key_ref | LiteLLM 网关 key 的凭据引用，默认 `mimo:litellm`（有则带 Bearer，无则裸调——本机记账常免鉴权）；网关无 DB 时 spend 面全回 `no_db_connection`，卡按 Degraded 明示「未接数据库」 |
| mimo | console_usage_url | 控制台套餐用量 API 地址（默认 `/api/v1/tokenPlan/usage`，GET + api-platform_ph Cookie + plan-manage referer） |
| mimo | console_credential_ref | 控制台 Cookie 的凭据引用，默认 `mimo:console`（Cookie 值 = 浏览器 F12 → Cookies → api-platform_ph 的值（整行 Cookie 粘进来也行，自动取 api-platform_ph）；**粘贴 + DPAPI 回放，不做扫码登录**，会话过期重录） |
| http | dsl_path | Provider DSL 声明文件路径（YAML，见下节「Provider DSL」）。**不配即零变化**；配了则 mapping 供数据提取缺省值（widget.Config 实例值仍优先），actions 进组件详情页动作区。文件缺失/解析失败 → 相关卡按 Degraded 明示，绝不静默 |

### Provider DSL（http 连接声明式映射与动作，批3-补42）

连接类型 `http` 可在 Settings 配 `dsl_path` 指向 YAML 声明文件（零第三方依赖，内置极小 YAML 子集：缩进映射 / `- ` 列表 / 引号 / 行内注释；flow `{}` `[]`、块标量、Tab 缩进、多文档均不支持并 fail-loud 带行号）：

```yaml
provider: company-packager        # 必填，日志与校验用
mapping:                          # 可选：数据提取缺省值（键见下表，未知键报错）
  status_path: $.job.state
  success_values: green,passed
actions:                          # 可选：声明动作（进详情页动作区，走既有执行器链路）
  - id: restart                   # 必填
    type: http                    # open.url / http / webhook
    name: 重启打包任务
    url: https://packer.local/api/jobs/42/restart   # 必填，支持 {var} 模板
    method: POST                  # http 默认 GET
    body: '{"force": true}'       # 可选（含 { } 的字面量请加引号）
    confirm: true                 # 默认：open.url=false，http/webhook=true
    retry:                        # 可选，默认 1 次（不重试）
      max_attempts: 3             # 1–10
      backoff_seconds: 5          # 0–300
```

| mapping 键 | 作用于 | 缺省 |
|---|---|---|
| status_path / summary_path / url_path | http.status 状态/摘要/详情链接提取 | `$.status` / 空 / 空 |
| success_values / warning_values / error_values | http.status 状态词表（逗号分隔） | 见 http.status 说明 |
| total_path / used_path / remaining_path / reset_path | http.quota 数值提取 | 空（total 必填语义不变） |
| unit | http.quota 单位 | 空 |

口径：**声明层只管数据提取缺省值**，`widget.Config` 实例值优先（声明层不夺实例覆盖权）；`label/icon/warn_percent/error_percent` 属组件实例，不进 DSL。动作 Id 自动带 `dsl.{连接}.{id}` 前缀且只在该连接的组件详情页显示（connection_id 限定）；重试在 ActionRunner 层生效——默认 1 次=不重试（既有行为逐字节等价），重试共享同一总超时预算，用户取消/超时不重试，多次尝试后仍失败的消息带「已重试 N 次」标注。

### 各家额度口径与数据源

| 连接 | 组件类型 | 额度口径 | 数据源与降级行为 |
|---|---|---|---|
| ark | ark.usage | 5h 滚动窗 + 周窗 + 月窗三窗已用百分比；**最差窗口优先**（2026-10-13 用户令）：级别/进度条/主数值取已用最高窗并标窗名，warn/error 阈值仍作用在最差窗已用 % 上 | 管控面 OpenAPI（V4 签名）。任一窗 100% 整卡显示「额度用尽（窗名，重置时间）」；收到 429 配额耗尽码（AccountQuotaExceeded 等）直接按该窗已尽处理并带重置时间；纯限流 429 不翻转 |
| bigmodel | bigmodel.usage | 5h 滚动窗 + 周窗 + 月窗已用百分比（窗口上限无绝对 token 口，监控接口只给百分比）；**最差窗口优先**（2026-10-13 用户令）：级别/进度条/主数值取已用最高窗并标窗名，阈值仍作用在最差窗已用 % 上 | 监控接口 quota/limit。主数值不再按 rolling → weekly → monthly 可用性兜底，改取已用最高窗 |
| claude | claude.usage | 窗口内 token 与成本（costUSD 存在才计成本，不自行估价） | 读本机 `~/.claude/projects/**/*.jsonl`，零凭据零网络。设了 daily_cost_limit 才按用量升级别 |
| codex | codex.usage | 近 N 天 token（每会话文件取最后一条累计值求和，如实标注「本地统计，非官方口径」） | 读本机 `~/.codex/sessions/**/rollout-*.jsonl`。官方口需 OAuth，落本地统计档 |
| kimi | kimi.coding | 卡面/进度条=已用%（剩余反推），级别仍按剩余判（余 0% 显示「已用尽」）；**最差窗口优先**（2026-10-13 用户令）：级别/进度条/主数值取剩余最低（=已用最高）窗并标窗名，阈值仍按剩余百分比 | `usages[]`（scope=FEATURE_CODING）条目 detail=周期窗、条目内 limits[]=5h 窗，`totalQuota` 进 payload；数值为字符串（2026-10-11 实抓，解析容忍）。旧形状（顶层 usage+limits[]）兼容兜底 |
| moonshot | moonshot.balance | 余额金额（¥），余额 ≤ warn_below（默认 20）→ Warning、≤ error_below（默认 5）→ Error | GET /v1/users/me/balance（Bearer）。响应不含余额数据按 Degraded 处理 |
| sensenova | sensenova.tokenplan | 控制台 Token Plan 积分池已用百分比（多池 × 5h/7d 双窗）；**最差池的最差窗优先**：主数值/进度条/级别取已用最高窗并标池名与窗名，阈值（warn_percent 80 / error_percent 95）作用在最差窗已用 % 上；摘要多池齐显；赠额到期预警（最近到期日 7 天内，或即将到期余额占该池赠额 ≤10%）级别不低于 Warning | 控制台池级口（Bearer JWT）。JWT 过期 401 → Degraded 明示「控制台 token 已过期，请重新获取」不静默空数据；无池/无窗数据是合法状态（Info 明示订阅状态，不造假红）；数值为字符串小数（含千分位）解析容忍；多账号 = 多连接（每连接一个控制台 JWT、各自一组池） |
| deepseek | deepseek.balance | 余额金额（CNY/USD），total ≤ 阈值判级 | GET /user/balance。is_available=false 直接 Error |
| mimo | mimo.usage | 套餐已用/总量/百分比（控制台 Cookie 录了才显示）；无 Cookie 且录了 litellm_spend_url → 「本月已用 $X（网关记账）」（网关无额度分母，只显已用量，如实不造）；否则本机计数；再否则如实「无额度口」 | 四档降级：控制台 API → LiteLLM 网关记账 → 本机计数 → 模型目录数降次（模型数是目录不是额度，不冒充）。控制台 401/403 卡面明示「Cookie 已失效或未录制」+ 重录指引（Degraded 中止，不静默降档）。控制台响应真身 2026-10-11 实抓：data.monthUsage/usage 块 + items[]，percent 字段是 **0-1 比例**（used/limit 自算精确百分比，≥90% Warn / ≥100% Error）。LiteLLM 响应兼容 根数组 / response[] / results[] / summarize 汇总对象 四形 |
| qwen | qwen.usage | Token Plan 已用百分比 + 重置日（个人版 7 天固定窗、团队版月度） | 四档探测：官方用量 API 无 → 自定义 usage_endpoint → 配额探针（耗尽时任意 POST 得 429 AllocationQuota 携重置时间，非耗尽型 429 不翻转）→ 模型目录兜底 |
| copilot | copilot.usage | premium_interactions 槽已用百分比（chat/completions 无限不参与）；配额重置日展示 | 官方客户端同款配额端点，需 Editor-Version / Copilot-Integration-Id 两个请求头 |
| opencode | opencode.usage | 月度预算（金额 micro-cents）；有限额时卡面/进度条=限额换算已用%（Progress=已用占比）；多成员聚合：消费求和、任一无上限即无上限、任一超支即超 | Console Budgets API。exceeded → Error（进度条满格）；无上限 → Success 基线不编百分比 |
| github | github.pull_requests / github.actions.runs | 非额度：PR 列表状态 / Workflow 运行状态 | REST + ETag 条件请求，限额感知 |
| jenkins | jenkins.build | 非额度：Job 最新构建状态（成功/失败/不稳定/构建中/未构建/已禁用） | 开源标准 JSON API（任意实例通用）：job 端点逐段 `job/<name>` 转义支持 Folder；一次请求按 `color` 判级（job 级 lastBuild 不含 result，实测），`_anime` 后缀=构建中 |
| machine | machine.net / machine.git / machine.local | 非额度：本机网络（IP / ↑↓实时速率 / 累计流量）+ 本机 Git 仓库（↑↓ · 改 · stash，每仓库一张卡）+ 本机资源（CPU / 内存 / 磁盘，一行三个百分比） | 网络：只读本机 `NetworkInterface`（IPv4 收发计数自开机累计）；速率=相邻两次采样差分÷时间差（machine 档 15s，首刷无基线只出 IP 与累计值），计数回退/换卡重记基线；累计流量在 L3 详情。Git：git CLI 三连只读（status --porcelain -b / rev-list / stash list，零网络零凭据，无上游不上板 ↑↓），behind>0 或分叉亮黄（要动手拉取/rebase）；变更文件前 5 条预览在 L3。资源：Linux 读 /proc（stat 首行 + meminfo）、Windows 走 GetSystemTimes + GlobalMemoryStatusEx，磁盘统一 DriveInfo（空=系统盘/根分区，点名不存在 fail-loud）；CPU 同差分口径（首刷无基线不出 CPU）；三项阈值默认 90%，超线亮黄（按四舍五入后的显示值比线，所见即所警）。**可选展示项**（2026-10-12）：组件 config `show` 逗号分隔多选——machine.local 可选 `cpu_percent / mem_percent / disk_percent`，machine.net 可选 `down_bps / up_bps / ip`，留空 = 全显（默认版式不变）；未选中的项不出悬浮 tile 数值位、不出快捷面板 Summary、不出详情页 payload（三处同源），原始台账键（mem_used / total_sent 等）不参与选择恒在详情页。悬浮 tile 宽度按所选展示项的最坏值行自动推算（`TileSizing`，下限 168），改 show 下次刷新即生效、免重启。全部只读本机，不触及冻结的远程机器管理 |
| http | http.status / http.quota | 自定义：状态词映射或 JSONPath 数值卡 | 任意 HTTP JSON 接口，字段路径全部可配 |
| herald | herald.incidents / herald.feed / herald.deliveries | 非额度：事件台账（未决/已认领/已解决/全部计数 + 最新一条）+ 订阅源（RSS 条目/未读数）+ 投递失败（失败计数 + 最新一条去向与错误） | incidents：`/api/v1/incidents?status=…`（默认 open，乱填 fail-loud），open>0 亮黄、含 critical 亮红、回看口径（acked/resolved）恒 Info；feed：公开类目 `/feeds/{category}.xml`（slug 校验防注入）或私密 `/feeds/private/{token}.xml`（token 即凭据走 DPAPI，不落 widgets.json 不进 payload；类目优先）；未读=自上一次刷新新增 guid（按组件记基线，首刷只记基线不出未读）；deliveries：`/api/v1/logs?status=failed&limit=200[&provider=X]`，failed>0 亮黄（送信没送达要人管；重试耗尽 dead 在日志层也写 failed，不区分；shadow 被 status=failed 天然排除，pending 是瞬态不报），计数取响应 total（全窗口过滤总数），可按提供方筛选；RFC1123Z 时间戳剥星期名解析（invariant globalization 兼容） |

**多窗口额度展示**（2026-10-12；2026-10-13 用户令统一已用口径）：bigmodel/ark/kimi 的 tile 数值位在两个以上窗口同时有值时拼成 `5%/5h 70%/w 80%/M` 形态（全部已用口径无前缀，只出实际存在的窗口，窗序 5h → 周 → 月；kimi 的已用=剩余反推）。进度条各家同为已用占比（provider Progress 同源）；kimi 判级口径不变——仍按剩余百分比过阈值。多窗口形态下 tile 收起重复的「NN%」进度读数（数值文本已含该百分比），单窗形态维持现状。tile 宽度按各类型最坏值行字宽预算（`TileSizing`），数值列恒不裁字。sensenova 同族但形态不同：tile 数值位取**最差池的最差窗**（池名 + 窗名 + 已用%，如「Flash-Lite池 5h 已用100%」），摘要多池齐显（每池「短名 5h X% · 7d Y%」，池间「 / 」分隔）。

## 组件（widgets.json）

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| id | 字符串 | 必填 | 组件实例唯一名 |
| type | 字符串 | 必填 | 组件类型，如 `ark.usage`（见上表组件类型列，另有 `github.pull_requests` 等） |
| connectionId | 字符串 | `""` | 绑定的连接 id |
| config | 对象 | `{}` | 类型专属字段，见下节；通用键：`label`（tile 显示名）、`icon`（覆盖品牌图）、`progressStyle`（`pool` = 额度进度改用圆池注水旧特效；缺省 = 遵循外观设置里的「额度显示方式」） |
| refreshTier | 字符串 | `default` | 刷新档，见档位表 |
| refreshIntervalSeconds | 整数 | `null` | 检测间隔覆盖（秒）。`null` = 按刷新档周期；设置页选项 30 秒/1/2/5/15 分钟/1 小时 |
| pinned | 布尔 | `false` | 是否钉到桌面（L0）。右键菜单、设置页、快捷面板三处入口共写此字段 |
| colorOverride | `#RRGGBB` | `null` | 状态灯颜色覆盖，优先级：组件 > 全局色表；非法值回退默认 |
| pinLayout | 对象 | `null` | 布局便利引用（权威在 pins.json，两者同步写） |

### 刷新档（refreshTier）

| 档 | 周期 | 适用 |
|---|---|---|
| pr | 60 秒 | GitHub PR |
| ci | 15 秒 | CI 运行、AI 额度类 |
| machine | 15 秒 | 机器状态 |
| agent | 10 秒 | Agent 活动 |
| workflow | 10 秒 | Workflow 事件 |
| static | 不自动刷新 | 手动/事件触发 |
| default | 60 秒 | 兜底 |

失败退避：按档位周期倍增，上限 10 倍；周期带 20% 抖动。

### 组件 config 字段（按类型）

通用字段（多数额度类组件都有）：

| 字段 | 适用类型 | 默认 | 说明 |
|---|---|---|---|
| label | 全部 | 按类型 | 显示名，tile 与卡片标题 |
| icon | 有品牌位的类型 | 按连接品牌 | 图标（图标选择器） |
| warn_percent | 额度已用% 类 | 60/80 视类型 | 已用达到此值转 Warning |
| error_percent | 额度已用% 类 | 90/95 视类型 | 已用达到此值转 Error |

github.pull_requests：

| 字段 | 默认 | 说明 |
|---|---|---|
| repo | 必填 | `owner/repo` |
| warnOnReviewRequested | — | `true` 时「有待你 review 的 PR」告警 |
| warnOnRedCi | `false` | `true` 时 PR 的 CI 红灯（combined status `failure`）计入告警。按 PR 记忆 CI 状态：终态且未换 commit 复用记忆（稳态零额外请求），仅新 push 或运行中的 PR 发探测；开启后首次全量每 PR 一请求，大仓库首刷代价高 |

github.actions.runs：

| 字段 | 默认 | 说明 |
|---|---|---|
| repo | 必填 | `owner/repo` |
| workflow | 必填 | Workflow 文件名（如 `ci.yml`） |
| branch | 仓库默认分支 | 分支过滤 |

claude.usage：

| 字段 | 默认 | 说明 |
|---|---|---|
| days | `1` | 统计窗口（天） |
| daily_cost_limit | 无 | 美元/窗口。设了才按用量升级别，否则恒 Success |
| warn_percent / error_percent | 60 / 90 | 配合成本上限 |

codex.usage：

| 字段 | 默认 | 说明 |
|---|---|---|
| days | `7` | 统计窗口（天） |

kimi.coding（阈值按余量判，卡面/进度条显示已用%）：

| 字段 | 默认 | 说明 |
|---|---|---|
| warn_percent | `30` | 余量低于此值告警 |
| error_percent | `10` | 余量低于此值报错 |

deepseek.balance（余额口径）：

| 字段 | 默认 | 说明 |
|---|---|---|
| warn_below | `20` | 余额低于此值告警（按余额币种） |
| error_below | `5` | 余额低于此值报错 |

http.status：

| 字段 | 默认 | 说明 |
|---|---|---|
| status_path | 必填 | 状态字段 JSONPath（如 `$.status`） |
| summary_path | — | 摘要字段路径 |
| url_path | — | 详情链接路径 |
| success_values | `success,ok,done,passed,healthy,finished` | 成功状态词（逗号分隔） |
| warning_values | `running,building,pending,queued,in_progress,deploying,starting` | 进行中状态词 |
| error_values | `failed,failure,error,critical,broken` | 失败状态词；空/未知词按 Warning（宁报勿漏） |

http.quota：

| 字段 | 默认 | 说明 |
|---|---|---|
| total_path | 必填 | 总量字段路径 |
| used_path | 与剩余二选一 | 已用字段路径 |
| remaining_path | 与已用二选一 | 剩余字段路径 |
| reset_path | — | 重置时间路径（epoch 秒/毫秒/ISO 自动识别） |
| unit | — | 单位文案（如 tokens） |
| warn_percent / error_percent | 60 / 90 | 阈值；缺数据按 Warning |

ark.usage / bigmodel.usage / qwen.usage / copilot.usage / opencode.usage：只有通用字段（label、warn_percent、error_percent），各家阈值默认见「额度口径」表（bigmodel/ark/qwen 60/90，copilot/opencode 80/95）。mimo.usage 只有 label（阈值固定 90/100，不可配）。

## 钉选布局（pins.json）

| 字段 | 类型 | 说明 |
|---|---|---|
| tiles[].widgetId | 字符串 | 对应组件 id |
| tiles[].layout.monitor | 字符串 | 显示器标识（设备名+分辨率），热插拔后按锚点恢复 |
| tiles[].layout.anchor | 枚举 | 锚点角：topLeft/topRight/bottomLeft/bottomRight |
| tiles[].layout.offsetDips | 对象 | 相对锚点角的偏移（DIP，随 DPI 缩放） |
| tiles[].layout.collapsed | 布尔 | 吸边收起态（细条/圆点，悬停展开） |
| tiles[].layout.floatingX | 整数 | 独立悬浮框模式的窗口 X（物理像素，按组件各记；null = 未拖过按序级联） |
| tiles[].layout.floatingY | 整数 | 独立悬浮框模式的窗口 Y |

## 自定义动作（actions.json）

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| id | 字符串 | 必填 | 动作唯一名 |
| type | 枚举 | 必填 | `open.url` / `http` / `gh.workflow_dispatch` / `local.command` / `webhook` |
| name | 字符串 | `null` | 显示名 |
| requireConfirmation | 布尔 | `true` | 危险操作执行前弹确认，可按动作关闭 |
| parameters | 对象 | `{}` | 按类型给参数（URL/命令/workflow 文件等） |

动作在 L3 详情窗 ACTIONS 区按 widgetType 过滤出现，编辑入口在设置页「高级 → 自定义动作」。

## 配置导入导出

导出把 config/connections/widgets/pins/actions 五份打成一个 JSON 包（设置页「高级 → 导入导出」）：

```json
{
  "version": 1,
  "exportedAt": "2026-10-10T12:00:00+08:00",
  "app": { "hotkey": "Ctrl+Alt+B", "...": "全局设置与外观、通知规则" },
  "connections": [ { "id": "ark-main", "credentialRef": "conn:ark-main", "...": "" } ],
  "widgets": [],
  "pins": { "tiles": [] },
  "actions": []
}
```

红线：密钥绝不入包。导出文件全文无 token，连接只带 credentialRef 引用；导入后按包里的 credentialRef 列表逐个提示重录密钥（新机器 DPAPI 里没有对应明文）。`actions` 字段是后加的，旧包没有此字段也能导入（动作库为空，不报错）。导入是整体替换：包内有什么就落什么，包内没有的键回落默认。

## 悬浮显示策略

三个开关决定组件最终出现在哪：

| 开关 | 位置 | 作用 |
|---|---|---|
| pinDisplayMode | 设置 → 常规 | L0 形态二选一：宿主面板（单窗多 tile）/ 独立悬浮框（每组件一窗）。切换重启 |
| numericFloatingEnabled | 设置 → 常规 | 数值/额度类能否上独立悬浮窗。默认开（2026-10-11 用户令）；关时这类钉选只在宿主面板显示，数据不受影响 |
| pinned（组件级） | 右键/设置页/快捷面板 | 组件是否钉到桌面 |

判定规则：信息密集型（趋势图/灯组）不受数量悬浮窗总闸；数值/额度类（FloatingOptIn，全部 AI 额度卡、HTTP 数值卡）受总闸。悬浮形态下，悬浮窗已展示的组件不在宿主面板重复出现；panel 形态面板是唯一 L0，总闸只挡独立悬浮窗一条路。组件被连接停用（enabled=false）时不占桌面，状态与事件一并清掉，不留旧灯。
