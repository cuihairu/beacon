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
2. **Generic HTTP Provider（杀手级能力）**：不为每个内部系统写 C# 插件，配置化接入任意 HTTP API：

   ```yaml
   provider:
     type: http
     name: company-build
     endpoint: https://build.example.com/api/latest
     mapping:
       id: $.id
       title: $.project
       status: $.status
       updatedAt: $.updatedAt
     status:
       running: warning
       success: success
       failed: error

   actions:
     retry:
       method: POST
       endpoint: https://build.example.com/api/builds/{id}/retry
   ```

   > 不管内部是自研、老旧还是「垃圾」系统，只要有 HTTP API 就能接入 Beacon。
   > 凭据一律 `credentialRef` + DPAPI（**严禁明文 token**，与连接密钥同一条安全约束）。

3. **GitHub Provider 是参考实现**：GitHub Actions REST API（workflow runs 的状态/重跑/取消/日志）
   直接落成 `GitHubProvider`；公司打包系统落成 `HttpProvider`——两者共同验证抽象是否通用。

## 4. AI Usage 一级概念：UsageMetric

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
| Usage / Machine Provider、Generic HTTP | **新增**——M5 收口后并入 [mvp-issues](mvp-issues.md) |

## 7. 排期约定

- **先收口 M5**：B-803 真机走查、B-804 干净 Win11 VM，不打断验收；
- 收口后把 §5 P0 作为增补分组并入 `mvp-issues.md`，按 P0 重排后续阶段优先级；
- 现有 43 条 issue 内容不变，只做优先级调整与增补。
