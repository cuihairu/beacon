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

   **计划中（未实现）**：原设计的 YAML provider DSL（`mapping` 声明式字段映射 + per-provider `actions.retry` 动作配置）——当前动作仍走既有 Action 执行器链路，声明式 DSL 待后续排期。

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
| Usage Provider、Generic HTTP | **已落地**：http.status / http.quota / bigmodel.usage / claude.usage / kimi.coding / deepseek.balance / ark.usage / mimo.usage / codex.usage（§8，详细进度） |
| Machine Provider | 未实现——M5 收口后并入 [mvp-issues](mvp-issues.md) |

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
- **Kimi For Coding 套餐余量（P0 #5）**：组件类型 `kimi.coding`——`GET https://api.kimi.com/coding/v1/usages`，
  Bearer 认证（sk-kimi-* Key，与 Moonshot 开放平台两套体系），窗口 `{duration,timeUnit}` 归一化为 5h/周，
  顶层 `usage` 兜底为周限，按剩余百分比阈值告警（warn 30 / error 10）；
- **DeepSeek 开放平台余额（P0 #5）**：组件类型 `deepseek.balance`——`GET https://api.deepseek.com/user/balance`，
  Bearer 认证，`balance_infos[]` 字符串金额，`is_available=false` 直接 Error，余额击穿下限告警（warn 20 / error 5，按余额币种）；
- **火山方舟 Coding Plan Pro 额度（P0 #6）**：组件类型 `ark.usage`——火山控制面 OpenAPI `GetCodingPlanUsage`
  （open.volcengineapi.com，V4 签名火山变体；凭据为控制台 AK/SK 一次粘贴 `AccessKey:SecretKey` 只进 DPAPI——
  推理 ARK_API_KEY 实测被管控面 400 拒绝，不能互推），`Result.QuotaUsage[]` 归一化 5h/周/月三窗口已用百分比
  （接口只给百分比，绝对数不经此口）；空数组=未订阅/已回收，渲染「无套餐」Info 卡非错误；
- **小米 MiMo 模型目录卡（AI Usage 七家之一）**：组件类型 `mimo.usage`——官方未开放用量接口（推理域
  /usage 路由实测 404），卡片显示 `/v1/models` 模型目录真数据并如实标注「用量口径官方未开放」（不编数字）；
  连接 Settings 可填 `usage_endpoint`，官方日后开放即透传显示；
- **OpenAI Codex 本机统计（AI Usage 七家之一）**：组件类型 `codex.usage`——读本机
  `~/.codex/sessions/**/rollout-*.jsonl` 的 token_count 事件（total_token_usage 为会话内累计值，每文件取末条求和），
  零凭据零网络，卡面如实标注「本地统计」（ChatGPT OAuth 官方用量口与「连接+凭据」模型不同构，暂不接）；
- **PowerToys 形态配置中心**：设置页左侧模块目录（每模块独立 icon + 启停开关），开启才见对应配置页；
  启停落 `connections.json` 的 `enabled`，宿主跳过刷新，面板残留状态即时清理。
- **L0 独立悬浮框形态（B-701 扩展）**：设置「悬浮形态」二选一——宿主面板（默认，单窗多 tile）/ 独立悬浮框
  （每钉选组件一窗，桌面任意拖放，位置按组件各记 `PinLayout.FloatingX/Y` 物理像素，未拖过按序级联）；
  两形态同享置顶哨兵、点击下钻 L2、停用模块即时消失；切换重启生效。
  **产品拍板（数值类悬浮：可配置+默认关，2026-10-08 修正）**：独立悬浮窗对信息密集组件（趋势图/状态灯组，
  descriptor `FloatingSupported`）恒生效；数值/额度类（descriptor `FloatingOptIn`）由设置「数量悬浮窗」总开关控制——
  **默认关**（新装/升级零残留，关=桌面不出水滴/窗口），开启即刻生效恢复数值类悬浮窗；关闭态下钉选由宿主面板承载
  （一次性迁移提示指路设置）。当前组件库暂无 FloatingSupported 类型，开关关=悬浮形态等效面板。

### 调研结论（暂缓接入）

- **阿里云百炼（DashScope/千问）**：无独立的余额/用量查询接口——账户余额要走阿里云 BSS OpenAPI
  `QueryAccountBalance`（AccessKey/Secret 签名，非 Bearer REST），且统计的是整个阿里云账号消费而非百炼模型单独口径；
  接入成本高、信息增益低，暂缓。过渡方案：自建代理转发再配 `http.status`。
- **小米 MiMo**：API 开放平台（platform.xiaomimimo.com）已开放、模型权重 MIT 开源，但**未见公开的余额/用量查询接口**
  （2026-10-08 实测推理域 /usage 404）——已先落 `mimo.usage` 模型目录卡（见 §8）；真正的用量接口公开后，
  在连接 Settings 填 `usage_endpoint` 或按 bigmodel/kimi 同构升级。
- **Moonshot 开放平台余额**（区别于 Kimi For Coding 套餐）：`GET https://api.moonshot.cn/v1/users/me/balance`
  Bearer 认证，响应 `{code:0, data:{available_balance}}`（dsh-plugin-llm-balance 验证）——端点已确认，后续按 deepseek 同构接入。
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
  }
]
```

`http` 连接 settings：`auth_header`（默认 `Authorization`）、`auth_prefix`（默认 `"Bearer "`，可置空）；
`bigmodel` 连接 endpoint 可空（自动用官方监控接口），认证默认裸 Key（不带前缀）；
`claude` 连接零凭据零网络（endpoint 可填自定义会话目录）；`kimi`/`deepseek` endpoint 可空走官方接口，Bearer 认证；
`ark` 凭据为控制台 AK/SK（`AccessKey:SecretKey` 一次粘贴，只进 DPAPI）；`mimo` endpoint 可空走官方推理域，
`usage_endpoint` 留给官方日后开放的用量口（填了即透传显示）；`codex` 零凭据零网络（endpoint 可填自定义会话目录）。

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
  }
]
```

`http.status` 状态词表可整体省略（用上例默认值）；未知状态词按 Warning 兜底（宁报勿漏）。
`bigmodel.usage` 摘要形如 `GLM Coding Pro · 5h 42.5% · 周 8% · 月 3%`，级别取各窗口最差者过阈值。
`claude.usage` 摘要形如 `Claude · 今日 · $1.00 · 2.5K tok · 12 条`（costUSD 缺失时只报 token，不自行估价）。
`kimi.coding` 摘要形如 `Pro · 5h 剩25% · 周 剩55%`，剩余百分比越低越差。
`deepseek.balance` 摘要形如 `DeepSeek · ¥110.00（含赠 ¥10.00）`，币种 CNY/USD 自适应。
`ark.usage` 摘要形如 `方舟 · 5h 42% · 周 8% · 月 3%`（接口只给百分比，级别取各窗口最差者过阈值）；`mimo.usage`
摘要形如 `MiMo · 3 模型可用 · 用量口径官方未开放`；`codex.usage` 摘要形如 `Codex · 今日 · 8 会话 · 12.3K tok · 本地统计`
（`days` 默认 7，`days=1` 显示「今日」）。

