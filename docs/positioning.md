# 定位与首批场景（P0 方向）

> 2026-10-07 定向记录：把 **公司打包工具 + AI 套餐额度** 确立为 Beacon 的两个第一类场景，
> Provider 体系划分为 Status / Usage / Machine 三类，并以 Generic HTTP Provider 作为通用接入能力。
> 本文固化方向与优先级，**不改动已排期的 M5 验收**（B-803/B-804 收口后再并入 [mvp-issues](mvp-issues.md)）。

## 1. 定位

**Beacon = 个人开发者的本地「状态与额度」雷达——Status & Action Center，不是 DevOps Dashboard。**

它不替代 GitHub、Jenkins、公司打包系统、Claude、Codex，只负责把这些系统里
**「我现在需要知道什么 / 我现在需要做什么」** 统一带到 Windows 桌面上：

- **Signal**：状态、额度、生命周期的统一抽象（现有 `WidgetState` / `Severity` / `LifecycleState` 已承载）
- **Action**：失败后的下一步（查看日志 / 重跑 / 取消）直达桌面

## 2. 首批三类真实场景

```text
Beacon
│
├── 🏗 Build / CI          公司打包工具（首位）、GitHub Actions、Jenkins、GitLab CI
├── 🤖 AI Usage            Claude、OpenAI/Codex、Gemini、其他 AI 服务
└── 🖥 Developer Environment 服务器、Docker、Agent、开发机（第二阶段）
```

- **公司打包工具排在 GitHub 之前**：每天真实提交、排队、打包、上传的流程，
  Beacon 只把它抽象成 `BuildProvider`（Build #18231 · 🟡 Building · Game-A · develop · a82f91c · 18m），
  不需要理解整套内部系统。
- 失败即 Action 场景：`🔴 Build Failed → [查看日志] [重新打包]`。

## 3. Provider 分类（三类 + 通用接入）

```text
Provider
├── Status Provider    GitHub Actions、公司打包、Jenkins、GitLab、Docker …
├── Usage Provider     Claude、OpenAI/Codex、Gemini …
└── Machine Provider   服务器、PC、Agent
```

原则：

1. **不为公司内部系统写死接口**——公司打包工具实现 Status Provider 适配自己的 API，
   转换成统一模型后进 Core，系统换了 Core 不动。
2. **Generic HTTP Provider（杀手级能力）**：不为每个内部系统写 C# 插件，配置化接入任意 HTTP API。
   **已落地形态**：组件类型 `http.status`（状态灯：状态词表→级别映射）与 `http.quota`（额度数值卡：
   total/used/remaining/reset 字段映射，见 §8）——widgets.json 内 JSON 配置（连接 endpoint + 凭据 + 点路径）：

   ```jsonc
   {
     "type": "http.status",
     "connectionId": "ci-local",
     "config": {
       "label": "company-build",
       "status_path": "$.status",       // $.a.b[0] 点路径提取
       "summary_path": "$.message",
       "url_path": "$.html_url",
       "success_values": "success,ok,passed,healthy",
       "warning_values": "running,building,pending,queued",
       "error_values": "failed,failure,error,critical"
     }
   }
   ```

   **已落地（批3-补42，2026-10-12）**：YAML provider DSL 已实现——连接 Settings 配 `dsl_path` 指向声明文件即启用，**不配置即零变化**（既有 widget.Config 路径逐字节等价）。口径：声明层只供**数据提取缺省值**（status/summary/url 路径、状态词表、额度路径与单位），`widget.Config` 实例值仍优先（声明层不夺实例覆盖权）；`label/icon/阈值` 属组件实例，不进 DSL；`actions` 走既有 Action 执行器链路（`open.url`/`http`/`webhook`），`retry`（max_attempts 1–10、backoff_seconds 0–300）落到 Runner 重试参数（默认 1 次=不重试，重试共享同一总超时预算，用户取消/超时不重试）。零第三方依赖：内置极小 YAML 子集解析器（缩进映射 / `- ` 列表 / 引号 / 行内注释），支持外的语法（flow、块标量、Tab、多文档）fail-loud 带行号，坏文件落 Degraded 卡面、动作加载失败记警告跳过。示例：

   ```yaml
   # 连接 Settings: dsl_path: C:\Beacon\packer.yaml
   provider: company-packager
   mapping:
     status_path: $.job.state
     success_values: green,passed
   actions:
     - id: restart
       type: http
       name: 重启打包任务
       url: https://packer.local/api/jobs/42/restart
       method: POST
       confirm: true          # http/webhook 默认 true；open.url 默认 false
       retry:
         max_attempts: 3      # 1–10，默认 1（不重试）
         backoff_seconds: 5   # 0–300，默认 5
     - id: dashboard
       type: open.url
       url: https://packer.local/dashboard
   ```

   > 不管内部是自研、老旧还是「垃圾」系统，只要有 HTTP API 就能接入 Beacon。
   > 凭据一律 `credentialRef` + DPAPI（**严禁明文 token**，与连接密钥同一条安全约束）。

3. **GitHub Provider 是参考实现**：GitHub Actions REST API（workflow runs 的状态/重跑/取消/日志）
   直接落成 `GitHubProvider`；公司打包系统落成 `HttpProvider`——两者共同验证抽象是否通用。

## 4. AI Usage 一级概念：UsageMetric

> 状态：**概念设计，未落地独立类型**——当前用量组件直接以 `WidgetState`（payload + 阈值严重度）承载，不引入 `UsageMetric` 结构；下列模型为后续抽象的方向记录。

CI 与 AI 额度的本质区别：

```text
CI：   失败 → 处理
AI：   额度 → 持续消耗 → 接近上限 → 预测耗尽 → 提醒 → 切换模型 / 等待恢复
```

因此 AI Usage 单列 **Usage Provider**，且**不做统一百分比字段**——不同服务限制口径完全不同
（Claude API 有 RPM、输入/输出 tokens/分钟等组织级限制；订阅制的消费窗口又是另一套）：

```text
UsageMetric
├── name / current / limit / unit
├── window        （1h / 5h / weekly / monthly）
├── resetAt?
├── percentage?
└── status
```

同模型可表达：

| 来源 | 表达 |
| --- | --- |
| Claude | Input tokens 82M / 100M，window = 1h |
| Codex | Usage 72%，window = weekly |
| OpenAI API | Cost $18.42 / $50，window = monthly |

进阶方向（预测型 Signal，Beacon 真正有意思的点）：

```text
Claude   82% used   预计 47 min 后达到限制
Codex    71% used   weekly 8h 后重置
```

主动提醒：`⚠ Claude 5h quota 82% used` / `⚠ Codex weekly 91%` / `✓ quota recovered`。

## 5. P0（方向确认的首批清单）

1. Company Build Provider（Status Provider 适配公司打包 API）
2. GitHub Actions Provider
3. Generic HTTP Provider（YAML mapping + actions）
4. Usage Provider 抽象（`UsageMetric`）
5. Claude Usage
6. OpenAI/Codex Usage
7. Notification（复用现有通知链路）
8. L0 / L1 / L2（复用现有链路）

完成即：Beacon 成为**自己每天真正会用的工具**。

## 6. 与现有架构的对应

| 方向 | 现状（RFC-001 / 代码） |
| --- | --- |
| Status Provider | `IConnectionProvider` + `IWidgetProvider` 已在（GitHub 连接 = 参考实现） |
| 统一 Signal | `WidgetState`（id / title / severity / lifecycle / updatedAt / …）+ `StatusAggregator` |
| 机密 | `credentialRef` + `ISecretStore`（DPAPI，严禁明文 token） |
| 刷新 | `RefreshScheduler` 分层调度（状态与额度的窗口粒度可复用） |
| 通知 | `NotificationEngine` + Toast（B-5xx 已接） |
| Actions | `IActionExecutor` + `ActionRunner`（重跑/取消类动作落点已有） |
| Usage Provider、Generic HTTP | **已落地**：http.status / http.quota / bigmodel.usage / claude.usage / kimi.coding / deepseek.balance / ark.usage / mimo.usage / codex.usage / copilot.usage / opencode.usage / qwen.usage / moonshot.balance（§8，详细进度） |
| Jenkins / 公司打包槽位 | **已落地**（2026-10-11）：jenkins.build——开源标准 JSON API（批3-补31，批3-补27 挂起项关闭） |
| Machine Provider | **部分落地**（2026-10-11 批3-补32/33/36）：machine.net 本机网络（IP/↑↓速率/累计流量）+ machine.git 本机 Git 仓库（↑↓·改·stash，每仓库一张卡）+ machine.local 本机资源（CPU/内存/磁盘+阈值），均零凭据零网络；远程机器/服务器管理（§5 三类之一）按冻结记录不做 |

## 7. 排期约定

- **先收口 M5**：B-803 真机走查、B-804 干净 Win11 VM，不打断验收；
- 收口后把 §5 P0 作为增补分组并入 `mvp-issues.md`，按 P0 重排后续阶段优先级；
- 现有 43 条 issue 内容不变，只做优先级调整与增补。

## 8. 已落地（P0 进度）

- **Generic HTTP Provider（P0 #3）**：组件类型 `http.status`——点路径提取 `$.a.b[0].c`，状态词→级别映射可配；
- **通用额度数值卡（验收反馈）**：组件类型 `http.quota`——任意配额 JSON 字段映射上板：`total_path` 必配，
  `used_path`/`remaining_path` 二选一（后者自动推导已用），`reset_path` 支持 epoch 秒/毫秒/ISO 自动识别，
  `unit`/`warn_percent`（默认 60）/`error_percent`（默认 90）可配；`WidgetState.Progress` 渲染为 tile 底部
  进度条（额度占比同色填充），payload 存裸数值（% 属显示层）；设置页 bigmodel 模块附「一键添加 GLM 额度组件（示例）」，
  连接测试通过后提示经组件向导上板——绑定流：连接测试 → 选组件类型 → 选连接 → 配字段 → 钉选显示（带 source 标注）。
- **智谱 GLM 套餐额度（P0 #5 提前）**：组件类型 `bigmodel.usage`——`GET https://open.bigmodel.cn/api/monitor/usage/quota/limit`，
  裸 Key 认证（Authorization 头不带 Bearer），`data.limits[]` 归一化为 5 小时/周/月三窗口，阈值告警；
  摘要与 payload 带最近重置时间（监控接口原生 `nextResetTime` epoch ms，5h 窗优先，本地时区 MM-dd HH:mm）；
  Z.AI 国际站把连接 Endpoint 换成 `https://api.z.ai/api/monitor/usage/quota/limit` 即用；
- **Claude Code 本机用量（P0 #5）**：组件类型 `claude.usage`——读本机 `~/.claude/projects/**\/*.jsonl`（ccusage 同源数据，
  ccusage.com 口径），按窗口聚合 token 与 costUSD；零凭据零网络，`daily_cost_limit` 可选阈值告警（无上限恒 Success）；
- **Kimi For Coding 套餐余量（P0 #5）**：组件类型 `kimi.coding`——默认 `GET https://api.kimi.com/coding/v1/usages`
  Bearer 认证（sk-kimi-* Key，与 Moonshot 开放平台两套体系）；控制台真实口（2026-10-11 用户实抓）为 POST
  `www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages`（Cookie 认证，Settings 加
  `http_method=POST`/`auth_header=Cookie`/`http_body` 切换）。响应 `usages[]`（scope=FEATURE_CODING）条目
  detail=周期窗、limits[]=5h 窗，`totalQuota` 进 payload，数值为字符串（解析容忍）；旧形状（顶层
  usage+limits[]+membership.level）兼容兜底。窗口归一化 5h/周，按剩余百分比阈值告警（warn 30 / error 10）；
  卡面/进度条为已用口径（2026-10-13 用户令：已用=100−剩余反推，与 bigmodel/ark 的 Progress 语义一致）。
  **凭据模型**：Cookie 由用户从浏览器 F12 复制粘贴进设置凭据框（DPAPI 存储、请求时回放），Beacon
  **不做扫码/登录流**；会话过期后重录一次（与 MiMo 控制台 Cookie 同口径）；
- **DeepSeek 开放平台余额（P0 #5）**：组件类型 `deepseek.balance`——`GET https://api.deepseek.com/user/balance`，
  Bearer 认证，`balance_infos[]` 字符串金额，`is_available=false` 直接 Error，余额击穿下限告警（warn 20 / error 5，按余额币种）
  （2026-10-09 实测真数据：CNY 余额、is_available=true，形状逐字段吻合）；
- **火山方舟 Coding Plan Pro 额度（P0 #6）**：组件类型 `ark.usage`——火山控制面 OpenAPI `GetCodingPlanUsage`
  （open.volcengineapi.com，V4 签名火山变体；凭据为控制台 AK/SK 一次粘贴 `AccessKey:SecretKey` 只进 DPAPI——
  推理 ARK_API_KEY 实测被管控面 400 拒绝，不能互推），`Result.QuotaUsage[]` 归一化 5h/周/月三窗口已用百分比
  （接口只给百分比，绝对数不经此口）；空数组=未订阅/已回收，渲染「无套餐」Info 卡非错误；
- **小米 MiMo 用量卡（AI Usage 九家之一）**：组件类型 `mimo.usage`——三档降级：①控制台套餐用量（端点
  2026-10-10 前端 bundle 实证、**响应真身 2026-10-11 用户实抓**：GET platform.xiaomimimo.com/api/v1/tokenPlan/usage，
  Cookie `api-platform_ph` 认证 + plan-manage referer；响应 `data.monthUsage/usage` 块 + `items[]`，
  `percent` 字段是 **0-1 比例**非 0-100，used/limit 自算精确百分比；早前「/usage 等 7 端点全 404」系
  **推理域 API** 实测，控制台域例外）②本机计数（Settings `usage_endpoint`，防御解析
  total_calls/window_calls/window_minutes →「本机累计 N 次 · 近M分 K 次」）③皆无 → 额度位「无额度口」+
  plan-manage 指引、模型数降 Summary 次行（用户令：模型数是目录不是额度，不得占额度位）。
  Cookie 凭据模型同 Kimi：浏览器复制粘贴 + DPAPI 回放，不做扫码登录，过期重录；
- **OpenAI Codex 本机统计（AI Usage 九家之一）**：组件类型 `codex.usage`——读本机
  `~/.codex/sessions/**/rollout-*.jsonl` 的 token_count 事件（total_token_usage 为会话内累计值，每文件取末条求和），
  零凭据零网络，卡面如实标注「本地统计」（ChatGPT OAuth 官方用量口与「连接+凭据」模型不同构，暂不接）；
- **GitHub Copilot 套餐配额（AI Usage 第八家）**：组件类型 `copilot.usage`——官方编辑器扩展同款配额端点
  `GET api.github.com/copilot_internal/user`（PAT 需 copilot scope；Editor-Version / Copilot-Integration-Id
  两头实测必带，api.githubcopilot.com 域同样两头才通），`quota_snapshots` 三槽——高级请求（Pro 实测 1500/月
  含额）按已用百分比过阈值（默认 80/95 可配），聊天/补全 unlimited 显 ∞；Pro 月付标官方价 $10/月，
  `quota_reset_date` 为**额度重置日**（订阅续费日本接口不返回，不编造）；401/403/404→Degraded、5xx→Offline；
- **OpenCode Go 用量（AI Usage 第九家）**：组件类型 `opencode.usage`——官方 Console Budgets API 直连
  （文档面正式接口，v2 docs/console/api/budgets）：`GET opencode.ai/console/api/v2/budgets/members`
  （oc_sk Key Bearer，读 Budgets 需 All 权限；v1 已迁 v2，官方公告 v1 过渡期后下线）。
  金额为 micro-cents 十进制字符串（1 美元=1 亿，官方防精度口径），
  `limit_micro_cents` null=无上限；多成员聚合（消费求和/任一 null 即无上限/任一 exceeded 即超）；
  v2 成员对象新增 `additional_budget_rules`（共享/模型专项预算规则数组）——provider 只取主预算四字段
  （limit/spent/exceeded/resets_at），未知字段解析自然容忍（JsonDocument+TryGetProperty 不 strict）；
  官方 `exceeded` 一票 Error；`resets_at` 按 UTC 自然月；卡面「Go $10/月」为展示层套餐标注（用户订阅口径），
  payload 只存 API 真数字；401/403/404→Degraded、5xx→Offline；
- **阿里千问 百炼 Token Plan（AI Usage 第十家，2026-10-09）**：组件类型 `qwen.usage`——Token Plan
  （Coding Plan 专属 Key `sk-sp-` 前缀，Base URL `token-plan.cn-beijing.maas.aliyuncs.com`，OpenAI 兼容口
  `/compatible-mode/v1` + Anthropic 兼容口 `/apps/anthropic`）。**官方额度查询 REST 口未开放**（FAQ 只有控制台
  「我的订阅」页；社区实证 cc-switch#7484——专属 Key 只授权模型调用，/models 实测 200、控制台查询口不授权），
  三档探测后如实降级：默认显示 `/models` 模型目录真数据 + 「额度口径官方未开放」；连接 Settings/表单「用量端点」
  填自定义口（GET + Bearer），响应按防御键名解析（percent/used_percent、remaining_percent 反推、
  remaining/total_credits、reset_at/reset/period_end ISO 或秒级 epoch）即出额度卡（已用% 进度条 + 阈值告警
  + 重置时间）。窗口口径：个人版 7 天固定窗口、团队版月度（官方 FAQ），重置以端点数据为准；
- **Moonshot 开放平台余额（AI Usage 第十一家，2026-10-10）**：组件类型 `moonshot.balance`——开放平台余额监控
  （与 Kimi For Coding 套餐是两套账号体系），`GET api.moonshot.cn/v1/users/me/balance` Bearer 认证，
  响应 `{code:0, data:{available_balance}}`；余额 ≤ `warn_below`（默认 20）→ Warning、≤ `error_below`（默认 5）→ Error，
  响应不含余额数据按 Degraded 处理（连接测试同口径，代理回错误体不算连通）；
- **Jenkins 构建状态（开源 CI，master-plan §13 公司打包工具槽位）**：组件类型 `jenkins.build`——开源 Jenkins
  标准 JSON API（任意实例通用，非厂商专属）：连接 Endpoint=实例基址，凭据可空（匿名读）或 `user:apiToken`
  Basic 认证；组件 `job_path` 逐段 `job/<name>` 转义（Folder 嵌套支持）。一次请求 `/job/.../api/json?tree=…`
  按 `color` 判级（blue→Success、yellow→Warning 不稳定、red/dark_red→Error、aborted→Cancelled、gray/notbuilt
  →Info 未构建、disabled→已禁用；`_anime` 后缀=构建中 → Running）——job 级 `lastBuild` **不含 result**
  （weekly.ci.jenkins.io 真实夹具实证），不追打第二击。连接测试取根视图 `/api/json`，2xx 且是 JSON 才算
  连通（反向代理回 200 HTML 壳按 Degraded，代理假阳性不算连通）；
- **本机网络（2026-10-11 批3-补32）**：组件类型 `machine.net`——只读本机 `NetworkInterface`（零凭据零网络）：
  IP + 实时 ↓↑ 速率（相邻两次采样计数差÷时间差，machine 档 15s，首刷无基线只出 IP 与累计值）+ 累计流量
  （自开机，进 payload 走 L3 详情）；自动选卡=Up+非回环+有 IPv4、有网关者优先，点名网卡不存在按 Degraded；
  不触及冻结的远程 Machines 管理；
- **本机 Git 仓库（2026-10-11 批3-补33，用户拍板「git 都搞」）**：组件类型 `machine.git`——每仓库一张卡
  `beacon ↑2↓5 · 改 7 · stash 1`：git CLI 三连只读（status --porcelain -b / rev-list @{u}...HEAD / stash list，
  零网络零凭据，无上游不追打第二击）；behind>0 或分叉 → Warning（要动手才亮黄，仅待推 ahead 是 Info）；
  变更文件前 5 条预览（重命名取 -> 右侧）进 payload 走 L3 详情，32 DIP 装不下也不分页；
- **本机资源（批3-补32 拍板回执「本机的cpu 内存 磁盘空间」，批3-补36 落地）**：组件类型 `machine.local`——
  CPU/内存/磁盘一行三个百分比（零凭据零网络）：CPU=相邻两次累计 tick 差分（首刷无基线不出 CPU，回退重记基线）；
  Linux 读 /proc（stat 首行 + meminfo MemAvailable，老内核 MemFree 兜底）、Windows 走 GetSystemTimes +
  GlobalMemoryStatusEx，磁盘统一 DriveInfo（空=系统盘/根分区，点名不存在/未就绪 Degraded fail-loud）；
  三项阈值默认 90%（超线亮黄，Signal 不分级），按四舍五入后的显示值比线（所见即所警）；
- **Herald 接入（2026-10-11 批3-补35/37，用户拍板 incidents+feed 先行，deliveries 续批补齐）**：连接类型 `herald`——
  Endpoint=基址，凭据可空（未开 auth 匿名可用）或 API Key 走 X-API-Key 头，测试打恒公开口 `/api/v1/status`
  （2xx+JSON 才算连通）；`herald.incidents` 事件台账（open 未决>0 亮黄、含 critical 亮红，回看口径恒 Info）+
  `herald.feed` 订阅源（公开类目 RSS 或私密 token feed——token 即凭据走 DPAPI 不落配置；未读=上次刷新以来
  新增 guid，首刷只记基线）+ `herald.deliveries` 投递失败（`/api/v1/logs?status=failed`，failed>0 亮黄 =
  送信没送达要人管；日志态词表 pending/success/failed/shadow 写死四态，重试耗尽 dead 在日志层也写 failed
  不区分，shadow 被 status 过滤天然排除）。Herald 是用户自有项目（cuihairu/herald，统一订阅与投递中枢）；
- **全局检查频率（设置 → 常规）**：`AppConfig.PollIntervalSeconds`（0=按各组件刷新档策略表，默认）——
  组件级「检测间隔」优先，未设组件级的按全局走；五档（按档位/15s/30s/1m/5m）；改动经变化检测重建调度注册
  （未变不动，避免设置页随手一改全量重拉），立即生效并落日志；
- **组件类型短名（tile 标签/L2 chip 统一口径）**：`WidgetTypeNames`（Core，纯函数可单测）——此前 PinTile
  标签兜底取类型尾段（`ark.usage` → tile 名显示「usage」，字段名冒充显示名，2026-10-09 用户实测），
  现按类型前缀映射连接类型短名（火山方舟/智谱/小米 MiMo/阿里千问/…），QuickPanel 分组 chip 的 GLM/ARK
  硬编码并入同表；未知前缀仍退类型尾段（不抛）；
- **方舟 5h 窗口主口径（bug 批3）**：`ark.usage` tile 主数值/级别/进度条此前取最差窗口（周/月冒充当前窗口），
  现按 Coding Plan 5 小时窗口语义取 session 档为主口径（无 session 档退最差窗口兜底），Summary 带
  「5h x% · 重置 MM-dd HH:mm」，周/月降为 payload 明细供 L3；智谱/千问等各窗口口径不受影响；
- **PowerToys 形态配置中心**：设置页左侧模块目录（每模块独立 icon + 启停开关），开启才见对应配置页；
  启停落 `connections.json` 的 `enabled`，宿主跳过刷新，面板残留状态即时清理。
- **L0 独立悬浮框形态（B-701 扩展）**：设置「悬浮形态」二选一——宿主面板（默认，单窗多 tile）/ 独立悬浮框
  （每钉选组件一窗，桌面任意拖放，位置按组件各记 `PinLayout.FloatingX/Y` 物理像素，未拖过按序级联）；
  两形态同享置顶哨兵、点击下钻 L2、停用模块即时消失；切换重启生效。
  **产品拍板（数值类悬浮：可配置；默认开，2026-10-11 用户令）**：独立悬浮窗对信息密集组件（趋势图/状态灯组，
  descriptor `FloatingSupported`）恒生效；数值/额度类（descriptor `FloatingOptIn`）由设置「数量悬浮窗」总开关控制——
  **默认开**（`numericFloatingEnabled` 默认 true；2026-10-08 曾拍默认关，用户令推翻），
  关闭即刻收回落盘恢复宿主面板承载。当前组件库暂无 FloatingSupported 类型（状态类恒面板承载），开关只挡数值类独立悬浮窗一条路。

### 调研结论（暂缓接入）

- **阿里云百炼（DashScope/千问）**：~~无独立的余额/用量查询接口……接入成本高、信息增益低，暂缓~~
  **已被 2026-10-09 Token Plan（Coding Plan）打破**——按量计费口径的结论不再适用：Token Plan 专属 Key
  只授权模型调用、官方额度查询 REST 口仍未开放，已按「目录真数据 + 自定义用量端点」落 `qwen.usage`
  （见 §8 AI Usage 第十家）；按量计费用户的账户余额仍走阿里云 BSS OpenAPI `QueryAccountBalance`（暂缓不变）。
- **小米 MiMo**：API 开放平台（platform.xiaomimimo.com）已开放、模型权重 MIT 开源，但**未见公开的余额/用量查询接口**
  （2026-10-10 复核：/usage /quota /balance 等 7 端点全 404，响应无限流头）——`mimo.usage` 已按「无额度口 +
  本机计数」落（见 §8）；官方日后开放即按 bigmodel/kimi 同构升级。另注：小米 key 本机（litellm/relay）无调用方，
  litellm 的 mimo-v2.6-flash-free 走 DeepSeek serverless 中转非小米直连。
- **Moonshot 开放平台余额**：~~端点已确认，后续按 deepseek 同构接入~~
  **已于 2026-10-10 落 `moonshot.balance`**（见 §8 AI Usage 第十一家，按 deepseek 同构）。
- **OpenAI Codex 用量**：社区走 ChatGPT OAuth（`chatgpt.com/backend-api/wham/usage`，5h/周/月限额+Credits），
  需要本机 OAuth 凭据封装，与当前"连接+凭据"模型不同构——已按第三档落本机会话 JSONL 统计（`codex.usage`，见 §8），
  OAuth 口单独排期。

### 配置示例（config 目录 %APPDATA%/Beacon/）

连接 `connections.json`（凭据只存 `credentialRef`，Key 本体在 DPAPI，设置页录入）：

```json
[
  {
    "id": "ci-local",
    "type": "http",
    "endpoint": "http://192.168.1.10:8080/api/status",
    "credentialRef": "conn:ci-local",
    "enabled": true,
    "settings": { "auth_header": "X-Token", "auth_prefix": "" }
  },
  {
    "id": "zhipu",
    "type": "bigmodel",
    "endpoint": null,
    "credentialRef": "conn:zhipu",
    "enabled": true
  },
  {
    "id": "claude-local",
    "type": "claude",
    "endpoint": null,
    "enabled": true
  },
  {
    "id": "kimi",
    "type": "kimi",
    "endpoint": null,
    "credentialRef": "conn:kimi",
    "enabled": true
  },
  {
    "id": "deepseek",
    "type": "deepseek",
    "endpoint": null,
    "credentialRef": "conn:deepseek",
    "enabled": true
  },
  {
    "id": "ark",
    "type": "ark",
    "endpoint": null,
    "credentialRef": "conn:ark",
    "enabled": true
  },
  {
    "id": "mimo",
    "type": "mimo",
    "endpoint": null,
    "credentialRef": "conn:mimo",
    "enabled": true,
    "settings": { "usage_endpoint": "" }
  },
  {
    "id": "codex-local",
    "type": "codex",
    "endpoint": null,
    "enabled": true
  },
  {
    "id": "copilot",
    "type": "copilot",
    "endpoint": null,
    "credentialRef": "conn:copilot",
    "enabled": true
  },
  {
    "id": "opencode",
    "type": "opencode",
    "endpoint": null,
    "credentialRef": "conn:opencode",
    "enabled": true
  }
]
```

`http` 连接 settings：`auth_header`（默认 `Authorization`）、`auth_prefix`（默认 `"Bearer "`，可置空）；
`bigmodel` 连接 endpoint 可空（自动用官方监控接口），认证默认裸 Key（不带前缀）；
`claude` 连接零凭据零网络（endpoint 可填自定义会话目录）；`kimi`/`deepseek` endpoint 可空走官方接口，Bearer 认证；
`ark` 凭据为控制台 AK/SK（`AccessKey:SecretKey` 一次粘贴，只进 DPAPI）；`mimo` endpoint 可空走官方推理域，
`usage_endpoint` 填自定义用量口（计数/百分比形状出数，认不出的形状显式 Degraded——2026-10-13 用户令不透传远端原文上卡面）；`codex` 零凭据零网络（endpoint 可填自定义会话目录）；
`copilot` 凭据为 GitHub PAT（需 copilot scope），endpoint 可空走官方配额端点（官方扩展同款两头自动带）；
`opencode` 凭据为 OpenCode Key（oc_sk，读 Budgets 需 All 权限），endpoint 可空走官方 Console Budgets 接口。

组件 `widgets.json`：

```json
[
  {
    "id": "http.status:pack-a",
    "type": "http.status",
    "connectionId": "ci-local",
    "refreshTier": "ci",
    "pinned": true,
    "config": {
      "label": "打包机A",
      "status_path": "$.status",
      "summary_path": "$.message",
      "url_path": "$.html_url",
      "success_values": "success,ok,done,passed,healthy,finished",
      "warning_values": "running,building,pending,queued,in_progress,deploying,starting",
      "error_values": "failed,failure,error,critical,broken"
    }
  },
  {
    "id": "http.quota:custom",
    "type": "http.quota",
    "connectionId": "ci-local",
    "refreshTier": "ci",
    "config": {
      "label": "自定义额度",
      "total_path": "$.data.total",
      "used_path": "$.data.used",
      "reset_path": "$.data.reset_at",
      "unit": "tokens",
      "warn_percent": "60",
      "error_percent": "90"
    }
  },
  {
    "id": "bigmodel.usage:GLM",
    "type": "bigmodel.usage",
    "connectionId": "zhipu",
    "refreshTier": "ci",
    "config": { "label": "GLM", "warn_percent": "60", "error_percent": "90" }
  },
  {
    "id": "claude.usage:claude",
    "type": "claude.usage",
    "connectionId": "claude-local",
    "refreshTier": "static",
    "config": { "label": "Claude", "days": "1", "daily_cost_limit": "35" }
  },
  {
    "id": "kimi.coding:kimi",
    "type": "kimi.coding",
    "connectionId": "kimi",
    "refreshTier": "ci",
    "config": { "label": "Kimi", "warn_percent": "30", "error_percent": "10" }
  },
  {
    "id": "deepseek.balance:deepseek",
    "type": "deepseek.balance",
    "connectionId": "deepseek",
    "refreshTier": "default",
    "config": { "label": "DeepSeek", "warn_below": "20", "error_below": "5" }
  },
  {
    "id": "ark.usage:ark",
    "type": "ark.usage",
    "connectionId": "ark",
    "refreshTier": "ci",
    "config": { "label": "方舟", "warn_percent": "60", "error_percent": "90" }
  },
  {
    "id": "mimo.usage:mimo",
    "type": "mimo.usage",
    "connectionId": "mimo",
    "refreshTier": "ci",
    "config": { "label": "MiMo" }
  },
  {
    "id": "codex.usage:codex",
    "type": "codex.usage",
    "connectionId": "codex-local",
    "refreshTier": "static",
    "config": { "label": "Codex", "days": "1" }
  },
  {
    "id": "copilot.usage:copilot",
    "type": "copilot.usage",
    "connectionId": "copilot",
    "refreshTier": "default",
    "config": { "label": "Copilot", "warn_percent": "80", "error_percent": "95" }
  },
  {
    "id": "opencode.usage:opencode",
    "type": "opencode.usage",
    "connectionId": "opencode",
    "refreshTier": "default",
    "config": { "label": "OpenCode Go $10/月", "warn_percent": "80", "error_percent": "95" }
  }
]
```

`http.status` 状态词表可整体省略（用上例默认值）；未知状态词按 Warning 兜底（宁报勿漏）。
`bigmodel.usage` 摘要形如 `GLM Coding Pro · 5h 42.5% · 周 8% · 月 3%`，级别取各窗口最差者过阈值。
`claude.usage` 摘要形如 `Claude · 今日 · $1.00 · 2.5K tok · 12 条`（costUSD 缺失时只报 token，不自行估价）。
`kimi.coding` 摘要形如 `Pro · 5h 已用75% · 周 已用45%`（社区口带套餐等级；控制台口无等级，head 落 label 或「Kimi」）；卡面/多窗口/进度条为已用口径（2026-10-13 用户令，与 bigmodel/ark 统一，已用=剩余反推），**级别判定仍按剩余百分比**（越低越差，阈值 warn 30 / error 10 不变）。
`deepseek.balance` 摘要形如 `DeepSeek · ¥110.00（含赠 ¥10.00）`，币种 CNY/USD 自适应。
`ark.usage` 摘要形如 `方舟 · 5h 42% · 周 8% · 月 3%`（接口只给百分比，级别取各窗口最差者过阈值）；`mimo.usage`
摘要形如 `MiMo · 本机累计 128 次 · 近60分 6 次 · 官方无额度接口（本机计数）`（配计数源）或
`MiMo · 官方无额度接口 · 8 模型可用`（模型数次行，额度位「无额度口」）；`codex.usage` 摘要形如 `Codex · 今日 · 8 会话 · 12.3K tok · 本地统计`
（`days` 默认 7，`days=1` 显示「今日」）；`copilot.usage` 摘要形如 `Copilot · Pro $10/月 · 高级 1/1500 · 聊天 ∞ · 补全 ∞ · 11-01 重置`
（级别取高级请求已用百分比过阈值）；`opencode.usage` 摘要形如 `OpenCode Go $10/月 · $0.00 · 无上限 · 11-01 重置`
（有上限时 `已用 / 上限` 同段展示）。

