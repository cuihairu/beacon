
# Positioning and First Scenarios (P0 Direction)

> Direction-setting record, 2026-10-07: establishes **company packaging tool + AI plan quota** as Beacon's two first-class scenarios,
> divides the Provider system into three categories — Status / Usage / Machine — with the Generic HTTP Provider as the general-purpose integration capability.
> This document fixes the direction and priorities, **without touching the already-scheduled M5 acceptance** (merge into [mvp-issues](mvp-issues.en.md) after B-803/B-804 close out).

## 1. Positioning

**Beacon = the individual developer's local "status & quota" radar — a Status & Action Center, not a DevOps Dashboard.**

It does not replace GitHub, Jenkins, the company packaging system, Claude, or Codex; it only takes
**"what I need to know right now / what I need to do right now"** from these systems and brings it uniformly onto the Windows desktop:

- **Signal**: the unified abstraction of status, quota, and lifecycle (already carried by the existing `WidgetState` / `Severity` / `LifecycleState`)
- **Action**: the next step after a failure (view logs / rerun / cancel) delivered straight to the desktop

## 2. First Batch of Three Real Scenarios

```text
Beacon
│
├── 🏗 Build / CI          公司打包工具（首位）、GitHub Actions、Jenkins、GitLab CI
├── 🤖 AI Usage            Claude、OpenAI/Codex、Gemini、其他 AI 服务
└── 🖥 Developer Environment 服务器、Docker、Agent、开发机（第二阶段）
```

- **The company packaging tool ranks ahead of GitHub**: the daily flow of real commits, queueing, packaging, and uploading.
  Beacon merely abstracts it into a `BuildProvider` (Build #18231 · 🟡 Building · Game-A · develop · a82f91c · 18m),
  with no need to understand the whole internal system.
- Failure is an Action scenario: `🔴 Build Failed → [查看日志] [重新打包]`.

## 3. Provider Taxonomy (Three Categories + General-Purpose Integration)

```text
Provider
├── Status Provider    GitHub Actions、公司打包、Jenkins、GitLab、Docker …
├── Usage Provider     Claude、OpenAI/Codex、Gemini …
└── Machine Provider   服务器、PC、Agent
```

Principles:

1. **No hard-coded interfaces for internal company systems** — the company packaging tool implements the Status Provider to adapt to its own API,
   converts into the unified model and enters Core; if the system is replaced, Core does not move.
2. **Generic HTTP Provider (the killer capability)**: instead of writing a C# plugin for every internal system, integrate any HTTP API via configuration.
   **Landed form**: component types `http.status` (status light: status word list → level mapping) and `http.quota` (quota number card:
   total/used/remaining/reset field mapping, see §8) — JSON config in widgets.json (connection endpoint + credentials + point paths):

   ```jsonc
   {
     "type": "http.status",
     "connectionId": "ci-local",
     "config": {
       "label": "company-build",
       "status_path": "$.status",       // $.a.b[0] point-path extraction
       "summary_path": "$.message",
       "url_path": "$.html_url",
       "success_values": "success,ok,passed,healthy",
       "warning_values": "running,building,pending,queued",
       "error_values": "failed,failure,error,critical"
     }
   }
   ```

   **Landed (batch 3, supplement 42, 2026-10-12)**: the YAML provider DSL is implemented — a connection configured in Settings with a `dsl_path` pointing at a declaration file enables it, **no config means zero change** (the existing widget.Config path is byte-for-byte equivalent). Semantics: the declaration layer only supplies **default values for data extraction** (status/summary/url paths, status word lists, quota paths and units); instance values in `widget.Config` still win (the declaration layer does not seize instance override authority); `label`/`icon`/`thresholds` belong to the component instance and do not go into the DSL; `actions` go through the existing Action executor chain (`open.url`/`http`/`webhook`), and `retry` (max_attempts 1–10, backoff_seconds 0–300) lands in the Runner retry parameters (default 1 attempt = no retry; retries share the same total timeout budget; user cancel/timeout does not retry). Zero third-party dependencies: a built-in minimal YAML subset parser (indentation mappings / `- ` lists / quotes / inline comments); syntax outside the supported subset (flow, block scalars, Tab, multiple documents) fails loud with a line number, a bad file lands on a Degraded card face, and action loading failures are logged as warnings and skipped. Example:

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

   > Whether the internals are homegrown, legacy, or "garbage" systems — anything with an HTTP API can be integrated into Beacon.
   > Credentials are always `credentialRef` + DPAPI (**plaintext tokens strictly forbidden**; the same security constraint as connection secrets).

3. **The GitHub Provider is the reference implementation**: the GitHub Actions REST API (workflow runs status/rerun/cancel/logs)
   lands directly as `GitHubProvider`; the company packaging system lands as `HttpProvider` — together they validate whether the abstraction is truly general.

## 4. AI Usage as a First-Class Concept: UsageMetric

> Status: **conceptual design, not landed as a standalone type** — the current usage components are carried directly by `WidgetState` (payload + threshold severity), with no `UsageMetric` structure introduced; the model below is a direction record for a later abstraction.

The essential difference between CI and AI quota:

```text
CI：   失败 → 处理
AI：   额度 → 持续消耗 → 接近上限 → 预测耗尽 → 提醒 → 切换模型 / 等待恢复
```

That is why AI Usage stands alone as a **Usage Provider**, and **no unified percentage field is defined** — different services have completely different limit semantics
(the Claude API has organization-level limits like RPM and input/output tokens per minute; subscription-based consumption windows are yet another scheme):

```text
UsageMetric
├── name / current / limit / unit
├── window        （1h / 5h / weekly / monthly）
├── resetAt?
├── percentage?
└── status
```

The same model can express:

| Source | Expression |
| --- | --- |
| Claude | Input tokens 82M / 100M, window = 1h |
| Codex | Usage 72%, window = weekly |
| OpenAI API | Cost $18.42 / $50, window = monthly |

Advanced direction (predictive Signal, where Beacon gets genuinely interesting):

```text
Claude   82% used   预计 47 min 后达到限制
Codex    71% used   weekly 8h 后重置
```

Active alerts: `⚠ Claude 5h quota 82% used` / `⚠ Codex weekly 91%` / `✓ quota recovered`.

## 5. P0 (First-Batch List After Direction Confirmation)

1. Company Build Provider (Status Provider adapting the company packaging API)
2. GitHub Actions Provider
3. Generic HTTP Provider (YAML mapping + actions)
4. Usage Provider abstraction (`UsageMetric`)
5. Claude Usage
6. OpenAI/Codex Usage
7. Notification (reuse the existing notification chain)
8. L0 / L1 / L2 (reuse the existing chains)

When done: Beacon becomes **a tool you genuinely use yourself every day**.

## 6. Mapping to the Existing Architecture

| Direction | Current state (RFC-001 / code) |
| --- | --- |
| Status Provider | `IConnectionProvider` + `IWidgetProvider` already in place (GitHub connection = reference implementation) |
| Unified Signal | `WidgetState` (id / title / severity / lifecycle / updatedAt / …) + `StatusAggregator` |
| Secrets | `credentialRef` + `ISecretStore` (DPAPI, plaintext tokens strictly forbidden) |
| Refresh | `RefreshScheduler` layered scheduling (the window granularity of status and quota is reusable) |
| Notification | `NotificationEngine` + Toast (B-5xx already wired) |
| Actions | `IActionExecutor` + `ActionRunner` (rerun/cancel-type actions already have a landing point) |
| Usage Provider, Generic HTTP | **Landed**: http.status / http.quota / bigmodel.usage / claude.usage / kimi.coding / deepseek.balance / ark.usage / mimo.usage / codex.usage / copilot.usage / opencode.usage / qwen.usage / moonshot.balance / sensenova.tokenplan (§8, detailed progress) |
| Jenkins / company packaging slot | **Landed** (2026-10-11): jenkins.build — the open-source standard JSON API (batch 3, supplement 31; the suspended item of batch 3, supplement 27 closed) |
| Machine Provider | **Partially landed** (2026-10-11 batch 3, supplements 32/33/36): machine.net local network (IP/↑↓ rate/cumulative traffic) + machine.git local Git repos (↑↓·changed·stash, one card per repo) + machine.local local resources (CPU/memory/disk + thresholds), all zero-credential zero-network; remote machine/server management (one of the three categories in §5) is not done, per the frozen record |

## 7. Scheduling Agreement

- **Close out M5 first**: B-803 real-machine walkthrough, B-804 clean Win11 VM — acceptance must not be interrupted;
- After close-out, merge the §5 P0 into `mvp-issues.md` as an additional group, and re-rank subsequent phase priorities by P0;
- The existing 43 issues keep their content unchanged — only priority adjustment and additions.

## 8. Landed (P0 Progress)

- **Generic HTTP Provider (P0 #3)**: component type `http.status` — point-path extraction `$.a.b[0].c`, configurable status-word → level mapping;
- **Generic quota number card (acceptance feedback)**: component type `http.quota` — map arbitrary quota JSON fields onto the board: `total_path` required,
  `used_path`/`remaining_path` pick one of the two (the latter auto-derives used), `reset_path` auto-detects epoch seconds/milliseconds/ISO,
  `unit`/`warn_percent` (default 60)/`error_percent` (default 90) configurable; `WidgetState.Progress` renders as the progress bar at the bottom of the
  tile (quota fraction filled in the same color), payload stores raw numbers (% belongs to the display layer); the bigmodel module on the settings page attaches 「一键添加 GLM 额度组件（示例）」,
  and after the connection test passes it prompts onboarding via the component wizard — binding flow: connection test → choose component type → choose connection → configure fields → pin the display (with source annotation).
- **Zhipu GLM plan quota (P0 #5, ahead of schedule)**: component type `bigmodel.usage` — `GET https://open.bigmodel.cn/api/monitor/usage/quota/limit`,
  raw Key auth (Authorization header without Bearer), `data.limits[]` normalized into the 5h/weekly/monthly three windows, threshold alerts;
  summary and payload carry the nearest reset time (the monitor API's native `nextResetTime` epoch ms, 5h window preferred, local timezone MM-dd HH:mm);
  for the Z.AI international site, just switch the connection Endpoint to `https://api.z.ai/api/monitor/usage/quota/limit` and it works;
- **Claude Code local usage (P0 #5)**: component type `claude.usage` — reads local `~/.claude/projects/**\/*.jsonl` (the same-source data as ccusage,
  ccusage.com semantics), aggregates tokens and costUSD by window; zero credentials zero network, `daily_cost_limit` optional threshold alert (no limit stays Success forever);
- **Kimi For Coding plan remaining (P0 #5)**: component type `kimi.coding` — default `GET https://api.kimi.com/coding/v1/usages`
  Bearer auth (sk-kimi-* Key, a separate account system from the Moonshot open platform); the console's real endpoint (captured live by the user, 2026-10-11) is POST
  `www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages` (Cookie auth; add
  `http_method=POST`/`auth_header=Cookie`/`http_body` in Settings to switch). Response `usages[]` (scope=FEATURE_CODING) entries
  detail=period window, limits[]=5h window, `totalQuota` into payload, values are strings (parsing is tolerant); the old shape (top-level
  usage+limits[]+membership.level) is a compatibility fallback. Windows normalized to 5h/weekly, threshold alerts by remaining percentage (warn 30 / error 10);
  card face/progress bar follow the used semantics (user directive, 2026-10-13: used=100−remaining back-computed, consistent with bigmodel/ark's Progress semantics).
  **Credential model**: the Cookie is copy-pasted by the user from the browser's F12 into the settings credential box (DPAPI storage, replayed on request); Beacon
  **does not build QR-scan/login flows**; after session expiry it is re-entered once (same approach as the MiMo console Cookie);
- **DeepSeek open platform balance (P0 #5)**: component type `deepseek.balance` — `GET https://api.deepseek.com/user/balance`,
  Bearer auth, `balance_infos[]` string amounts, `is_available=false` goes straight to Error, alert when balance breaches the lower bound (warn 20 / error 5, by balance currency)
  (verified against real data, 2026-10-09: CNY balance, is_available=true, shape matched field by field);
- **Volcano Ark Coding Plan Pro quota (P0 #6)**: component type `ark.usage` — the Volcano control-plane OpenAPI `GetCodingPlanUsage`
  (open.volcengineapi.com, the Volcano variant of V4 signing; credentials are the console AK/SK pasted once as `AccessKey:SecretKey` and only ever into DPAPI —
  the inference ARK_API_KEY was tested and rejected 400 by the control plane, so the two cannot stand in for each other), `Result.QuotaUsage[]` normalized into 5h/weekly/monthly three windows of used percentage
  (the API only gives percentages; absolute numbers do not come through this endpoint); empty array = unsubscribed/reclaimed, rendered as a 「无套餐」 Info card, not an error;
- **Xiaomi MiMo usage card (one of the nine AI Usage providers)**: component type `mimo.usage` — three-tier degradation: ① console plan usage (endpoint
  evidenced in the front-end bundle 2026-10-10, **response captured live by the user 2026-10-11**: GET platform.xiaomimimo.com/api/v1/tokenPlan/usage,
  Cookie `api-platform_ph` auth + plan-manage referer; response `data.monthUsage/usage` blocks + `items[]`,
  the `percent` field is a **0-1 ratio**, not 0-100; used/limit computed by us into exact percentages; the earlier 「/usage 等 7 端点全 404」 came from live testing of the
  **inference-domain API** — the console domain is the exception) ② local counting (Settings `usage_endpoint`, defensive parsing of
  total_calls/window_calls/window_minutes → 「本机累计 N 次 · 近M分 K 次」) ③ neither present → quota slot shows 「无额度口」 +
  plan-manage guidance, model count demoted to a Summary sub-line (user directive: model count is a catalog, not a quota, and must not occupy the quota slot).
  Cookie credential model same as Kimi: browser copy-paste + DPAPI replay, no QR-scan login, re-enter on expiry;
- **OpenAI Codex local stats (one of the nine AI Usage providers)**: component type `codex.usage` — reads token_count events from local
  `~/.codex/sessions/**/rollout-*.jsonl` (total_token_usage is a within-session cumulative; take the last record per file and sum), 
  zero credentials zero network, card face honestly labeled 「本地统计」 (the ChatGPT OAuth official usage endpoint is not isomorphic with the "connection + credentials" model, not integrating it for now);
- **GitHub Copilot plan quota (eighth of AI Usage)**: component type `copilot.usage` — the same quota endpoint as the official editor extension
  `GET api.github.com/copilot_internal/user` (PAT needs the copilot scope; Editor-Version / Copilot-Integration-Id
  both headers proven required by live testing, and the api.githubcopilot.com domain likewise only works with both), `quota_snapshots` three slots — premium requests (1,500/month on Pro per live testing,
  included) threshold-checked by used percentage (default 80/95, configurable), chat/completion unlimited show ∞; Pro monthly labeled with the official price $10/month,
  `quota_reset_date` is the **quota reset day** (the subscription renewal day is not returned by this endpoint, and is not fabricated); 401/403/404→Degraded, 5xx→Offline;
- **OpenCode Go usage (ninth of AI Usage)**: component type `opencode.usage` — direct connection to the official Console Budgets API
  (the documented official interface, v2 docs/console/api/budgets): `GET opencode.ai/console/api/v2/budgets/members`
  (oc_sk Key Bearer, reading Budgets requires the All permission; v1 has migrated to v2, official announcement says v1 is retired after the transition period).
  Amounts are micro-cents decimal strings (1 dollar = 100 million, the official precision-safe convention),
  `limit_micro_cents` null = no limit; multi-member aggregation (spend summed / any null means no limit / any exceeded means over);
  the v2 member object adds `additional_budget_rules` (an array of shared/model-specific budget rules) — the provider takes only the main budget's four fields
  (limit/spent/exceeded/resets_at); unknown fields are naturally tolerated during parsing (JsonDocument+TryGetProperty, not strict);
  official `exceeded` is a single vote of Error; `resets_at` follows the UTC calendar month; the card-face 「Go $10/月」 is display-layer plan labeling (the user's subscription semantics),
  payload stores only the API's real numbers; 401/403/404→Degraded, 5xx→Offline;
  card face/progress bar (user directive, 2026-10-13): when there is a limit the payload carries percent (used % converted from the limit) and Progress = used fraction 0-1
  (same semantics as ark/bigmodel); exceeded fills the progress bar (aligned with ark's 「已尽=满格」); no limit keeps `$spent · 无上限`, no fabricated percentage;
- **Alibaba Qwen Bailian Token Plan (tenth of AI Usage, 2026-10-09)**: component type `qwen.usage` — Token Plan
  (Coding Plan dedicated Key with `sk-sp-` prefix, Base URL `token-plan.cn-beijing.maas.aliyuncs.com`, OpenAI-compatible
  `/compatible-mode/v1`). **The official quota-query REST endpoint is not open** (the FAQ only has the console
  「我的订阅」 page; community evidence cc-switch#7484 — the dedicated Key only authorizes model calls; /models returns 200 in live testing while console query endpoints are not authorized),
  after four-tier probing it degrades honestly: by default show real `/models` model-catalog data + 「额度口径官方未开放」; fill a custom endpoint in connection Settings/form 「用量端点」
  (GET + Bearer), and the response is parsed with defensive key names (percent/used_percent, remaining_percent back-computed,
  remaining/total_credits, reset_at/reset/period_end ISO or second-level epoch) to produce a quota card (used % progress bar + threshold alerts
  + reset time). Window semantics: personal edition a fixed 7-day window, team edition monthly (official FAQ), reset follows the endpoint data;
- **Moonshot open platform balance (eleventh of AI Usage, 2026-10-10)**: component type `moonshot.balance` — open-platform balance monitoring
  (a separate account system from the Kimi For Coding plan), `GET api.moonshot.cn/v1/users/me/balance` Bearer auth,
  response `{code:0, data:{available_balance}}`; balance ≤ `warn_below` (default 20) → Warning, ≤ `error_below` (default 5) → Error,
  a response without balance data is treated as Degraded (connection test uses the same rule; a proxy returning an error body does not count as reachable);
- **SenseTime SenseNova Token Plan credit pool (twelfth of AI Usage, user directive 2026-10-13)**: component type `sensenova.tokenplan` —
  console pool-level quota `GET platform.sensenova.cn/lite/console/v1/tokenplan/pool-usage`, Bearer **console JWT**
  (a session token, not an API Key; carries `exp` and expires); response plan + pools[], each pool window_5h/window_7d (limit/used/remaining/reset_at)
  plus grant (grant_balance/nearest_grant_expiry); **worst pool's worst window wins** (highest used; ties broken by pool order → 5h → 7d) driving the main value/
  progress bar/level; summary shows all pools together; tile value slot shows 「池名 窗名 已用%」; grant-expiry warning (within 7 days or remaining fraction ≤10%) never below Warning;
  JWT expiry 401 → Degraded with explicit re-acquisition guidance, not silent empty data; multiple accounts = multiple connections;

- **Jenkins build status (open-source CI, the master-plan §13 company packaging tool slot)**: component type `jenkins.build` — the open-source Jenkins
  standard JSON API (works for any instance, not vendor-specific): connection Endpoint = instance base URL, credentials optional (anonymous read) or `user:apiToken`
  Basic auth; the component's `job_path` escapes each `job/<name>` segment (Folder nesting supported). One request `/job/.../api/json?tree=…`
  is graded by `color` (blue→Success, yellow→Warning unstable, red/dark_red→Error, aborted→Cancelled, gray/notbuilt
  →Info not built, disabled→disabled; `_anime` suffix = building → Running) — job-level `lastBuild` **does not include result**
  (proven with a real fixture from weekly.ci.jenkins.io), no second hit is chased. The connection test hits the root view `/api/json`; only 2xx and JSON count as
  reachable (a reverse proxy answering 200 with an HTML shell counts as Degraded; a proxy false-positive does not count as reachable);
- **Local network (2026-10-11, batch 3 supplement 32)**: component type `machine.net` — reads only local `NetworkInterface` (zero credentials zero network):
  IP + real-time ↓↑ rate (adjacent sample counter delta ÷ time delta, machine tier 15s; the first refresh has no baseline so only IP and cumulative values appear) + cumulative traffic
  (since boot, into payload for the L3 detail); auto card pick = Up + non-loopback + has IPv4, gateway-having preferred; a named NIC that does not exist counts as Degraded;
  does not touch the frozen remote Machines management;
- **Local Git repos (2026-10-11, batch 3 supplement 33, user decision 「git 都搞」)**: component type `machine.git` — one card per repo
  `beacon ↑2↓5 · 改 7 · stash 1`: git CLI triple read-only (status --porcelain -b / rev-list @{u}...HEAD / stash list,
  zero network zero credentials; no upstream means no second hit); behind>0 or diverged → Warning (yellow only when action is needed; ahead-only waiting to push is Info);
  the top 5 changed-file previews (renames take the right side of ->) go into payload for the L3 detail; if 32 DIP cannot fit them, still no pagination;
- **Local resources (batch 3 supplement 32 decision receipt 「本机的cpu 内存 磁盘空间」, landed in batch 3 supplement 36)**: component type `machine.local` —
  CPU/memory/disk as three percentages in one line (zero credentials zero network): CPU = delta of adjacent cumulative ticks (first refresh has no baseline so no CPU; on fallback the baseline is re-recorded);
  Linux reads /proc (stat first line + meminfo MemAvailable, old-kernel MemFree fallback), Windows goes through GetSystemTimes +
  GlobalMemoryStatusEx, disk uniformly DriveInfo (empty = system disk/root partition; named-but-missing or not-ready → Degraded, fail-loud);
  the three thresholds default to 90% (over the line lights yellow; Signal is not graded), compared against the rounded display value (what you see is what alerts);
- **Herald integration (2026-10-11, batch 3 supplements 35/37; user decision: incidents+feed first, deliveries to follow in later batches)**: connection type `herald` —
  Endpoint = base URL, credentials optional (anonymous works if auth is off) or an API Key via the X-API-Key header; the test hits the always-public endpoint `/api/v1/status`
  (only 2xx+JSON counts as reachable); `herald.incidents` event ledger (open unresolved >0 lights yellow, contains critical lights red; lookback view stays Info) +
  `herald.feed` subscription feeds (public-category RSS or private token feed — the token is the credential, goes to DPAPI, never into config; unread = new guid
  since the last refresh, first refresh only records the baseline) + `herald.deliveries` delivery failures (`/api/v1/logs?status=failed`, failed>0 lights yellow =
  mail sent but not delivered, needs human attention; the log status vocabulary pending/success/failed/shadow is a hardcoded four states; retry-exhausted dead is also written failed
  at the log layer with no distinction, and shadow is naturally excluded by the status filter). Herald is the user's own project (cuihairu/herald, a unified subscription and delivery hub);
- **Global check frequency (Settings → General)**: `AppConfig.PollIntervalSeconds` (0 = follow each component's refresh-tier policy table, the default) —
  the component-level 「检测间隔」 takes priority; components without one follow the global; five tiers (by tier 15s/30s/1m/5m); changes rebuild the schedule registration through change detection
  (no change, no action — avoiding a casual settings-page tweak triggering a full re-pull); takes effect immediately and is logged;
- **Component type short names (tile label/L2 chip unified vocabulary)**: `WidgetTypeNames` (Core, pure function, unit-testable) — previously the PinTile
  label fallback took the type's tail segment (`ark.usage` → tile name showed 「usage」, a field name impersonating a display name; user live test, 2026-10-09);
  now the type prefix maps to the connection-type short name (Volcano Ark/Zhipu/Xiaomi MiMo/Alibaba Qwen/…), and the QuickPanel group chip's hardcoded GLM/ARK
  is merged into the same table; unknown prefixes still fall back to the type tail (no throw);
- **Ark 5h window primary rule (bug batch 3, revised by user directive 2026-10-13)**: the `ark.usage` tile main value/level/progress bar originally followed
  the Coding Plan 5-hour window semantics with the session tier as primary; now unified to **worst-window-wins** — level/progress bar/main value take
  the highest-used window (5h/weekly/monthly) with the window name labeled; any window ≥100% goes to the exhausted branch, constant Error; Summary carries 「最差窗 x% · 其余窗 · 重置」,
  weekly/monthly demoted to payload detail for L3; Zhipu/Qwen and other per-window rules are unaffected;
- **PowerToys-style configuration center**: the settings page's left-side module catalog (each module with its own icon + on/off switch); only enabled modules show their config page;
  enable/disable lands in `connections.json`'s `enabled`; the host skips refresh, and residual panel state is cleaned up immediately.
- **L0 standalone floating window form (B-701 extension)**: settings offer a 「悬浮形态」 pick-one — host panel (default, single window with multiple tiles) / standalone floating window
  (one window per pinned component, freely draggable on the desktop; position recorded per component as `PinLayout.FloatingX/Y` physical pixels, never-dragged ones cascade in order);
  both forms share the always-on-top sentinel, click drills into L2, disabled modules disappear instantly; switching takes effect after restart.
  **Product decision (numeric-type floating: configurable; default on, user directive 2026-10-11)**: standalone floating windows for information-dense components (trend charts/status-light groups,
  descriptor `FloatingSupported`) always take effect; numeric/quota types (descriptor `FloatingOptIn`) are controlled by the settings' 「数量悬浮窗」 master switch —
  **default on** (`numericFloatingEnabled` defaults to true; 2026-10-08 had decided default off, overturned by user directive),
  turning it off immediately retracts and persists back to host-panel carrying. The current component library has no FloatingSupported types yet (status types are always panel-carried); the switch only gates the numeric standalone floating window path.

### Research Conclusions (Integration Deferred)

- **Alibaba Cloud Bailian (DashScope/Qwen)**: ~~No independent balance/usage query interface… high integration cost, low information gain, deferred~~
  **Overturned by the 2026-10-09 Token Plan (Coding Plan)** — the pay-as-you-go conclusion no longer applies: the Token Plan dedicated Key
  only authorizes model calls, and the official quota-query REST endpoint is still not open; `qwen.usage` has landed as 「目录真数据 + 自定义用量端点」
  (see §8, tenth of AI Usage); pay-as-you-go users' account balance still goes through the Alibaba Cloud BSS OpenAPI `QueryAccountBalance` (deferral unchanged).
- **Xiaomi MiMo**: the API open platform (platform.xiaomimimo.com) is open, model weights are MIT open-source, but **no public balance/usage query interface has been seen**
  (2026-10-10 recheck: /usage /quota /balance and 7 endpoints all 404, responses lack rate-limit headers) — `mimo.usage` has landed as 「无额度口 +
  本机计数」 (see §8); once the official one opens, it upgrades isomorphically like bigmodel/kimi. Side note: the Xiaomi key has no local caller (litellm/relay);
  litellm's mimo-v2.6-flash-free goes through the DeepSeek serverless relay, not a direct Xiaomi connection.
- **Moonshot open platform balance**: ~~Endpoint confirmed; integrate later isomorphic to deepseek~~
  **`moonshot.balance` landed on 2026-10-10** (see §8, eleventh of AI Usage, isomorphic to deepseek).
- **OpenAI Codex usage**: the community goes through ChatGPT OAuth (`chatgpt.com/backend-api/wham/usage`, 5h/weekly/monthly limits + Credits),
  which needs local OAuth credential wrapping and is not isomorphic with the current "connection + credentials" model — the third tier, local session JSONL stats, has landed (`codex.usage`, see §8);
  the OAuth endpoint is scheduled separately.

### Configuration Examples (config directory %APPDATA%/Beacon/)

Connection `connections.json` (credentials store only `credentialRef`; the Key body lives in DPAPI, entered on the settings page):

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

`http` connection settings: `auth_header` (default `Authorization`), `auth_prefix` (default `"Bearer "`, can be emptied);
`bigmodel` connection endpoint can be empty (the official monitor API is used automatically), auth defaults to raw Key (no prefix);
`claude` connection is zero-credential zero-network (endpoint can point at a custom session directory); `kimi`/`deepseek` endpoints can be empty to use the official APIs, Bearer auth;
`ark` credentials are the console AK/SK (`AccessKey:SecretKey` pasted once, into DPAPI only); `mimo` endpoint can be empty to use the official inference domain,
`usage_endpoint` points at a custom usage endpoint (counting/percentage shapes produce numbers; unrecognized shapes show explicit Degraded — user directive 2026-10-13: no passing remote raw text onto the card face); `codex` is zero-credential zero-network (endpoint can point at a custom session directory);
`copilot` credentials are a GitHub PAT (needs the copilot scope), endpoint can be empty to use the official quota endpoint (both headers of the official extension carried automatically);
`opencode` credentials are an OpenCode Key (oc_sk; reading Budgets needs the All permission), endpoint can be empty to use the official Console Budgets API.

Component `widgets.json`:

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

The `http.status` status word lists can be omitted entirely (the default values above are used); unknown status words fall back to Warning (better to over-report than to miss).
The `bigmodel.usage` summary looks like `GLM Coding Pro · 5h 窗口已用 42.5% · 周 8% · 月 3%`; **worst-window-wins** (user directive 2026-10-13): level/progress bar/main value always take the highest-used window with the window name labeled, and the warn/error thresholds still apply to the worst window's used % (semantics unchanged).
The `claude.usage` summary looks like `Claude · 今日 · $1.00 · 2.5K tok · 12 条` (when costUSD is missing only tokens are reported; no self-pricing).
The `kimi.coding` summary looks like `Pro · 周 已用95% · 5h 已用5%` (the community endpoint carries the plan tier; the console endpoint has no tier, and the head falls back to the label or 「Kimi」); card face/multi-window/progress bar follow the used semantics (user directive 2026-10-13, unified with bigmodel/ark: used = remaining back-computed), **worst-window-wins**: level/progress bar/main value take the lowest-remaining (= highest-used) window with the window name labeled; thresholds warn 30 / error 10 are still judged by remaining percentage (lower is worse; semantics unchanged).
The `deepseek.balance` summary looks like `DeepSeek · ¥110.00（含赠 ¥10.00）`, currency CNY/USD adaptive.
The `ark.usage` summary looks like `方舟 · 月 92% · 5h 42.5% · 周 8% · 待重置` (the API only gives percentages; **worst-window-wins**, user directive 2026-10-13: level/progress bar/main value take the highest-used window with the window name labeled, other windows follow, reset follows the worst window; any window ≥100% → exhausted branch, constant Error + full bar); the `mimo.usage`
summary looks like `MiMo · 本机累计 128 次 · 近60分 6 次 · 官方无额度接口（本机计数）` (with a counting source) or
`MiMo · 官方无额度接口 · 8 模型可用` (model count as sub-line, quota slot 「无额度口」); the `codex.usage` summary looks like `Codex · 今日 · 8 会话 · 12.3K tok · 本地统计`
(`days` defaults to 7; `days=1` shows 「今日」); the `copilot.usage` summary looks like `Copilot · Pro $10/月 · 高级 1/1500 · 聊天 ∞ · 补全 ∞ · 11-01 重置`
(level takes the premium requests' used percentage against the threshold); the `opencode.usage` summary looks like `OpenCode Go $10/月 · $0.00 · 无上限 · 11-01 重置`
(when there is a limit, `已用 / 上限` is shown in the same segment).

