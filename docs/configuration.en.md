[English](configuration.en.md) | [中文](configuration.md)

# Configuration reference

All of Beacon's configuration lives in JSON files under `%APPDATA%\Beacon\`. What the settings page changes and what import packages carry is the content of these files. This page lists every setting's key name, type, default, and actual effect.

## Configuration files and when they take effect

| File | Contents | After manual edit | After settings-page change |
|---|---|---|---|
| config.json | Global settings, appearance, notification rules | After restart | Immediate (except theme and floating mode, which need a restart) |
| connections.json | Connection definitions and credential references | After restart | Immediate (saving a connection rebuilds the registry) |
| widgets.json | Widget instances and field configuration | After restart | Immediate |
| pins.json | Pinned layout and floating window positions | With widget loading | Immediate |
| actions.json | Custom action library | After restart | Immediate |
| notifications.json | Notification records (read-only cache, deletable) | Rehydrated on restart | No editor provided |

Read failures are never silent: when a main file is corrupt it first tries the `.bak` backup, then falls back to defaults and archives `*.corrupt`, with an explanation in both the tray bubble and the logs. Diagnostic logs live at `%APPDATA%\Beacon\logs\beacon-*.log`.

## Global settings (config.json)

| Key | Type | Default | Effect | Takes effect |
|---|---|---|---|---|
| hotkey | string | `Ctrl+Alt+B` | Global hotkey for the quick panel. Changeable in the settings page "Global hotkey"; a registration failure (already taken) shows a tray bubble | Immediate |
| launchOnStartup | bool | `true` | Auto-start on login. Written to the registry Run key, kept in sync with the settings page "Launch on startup" switch | Immediate |
| theme | `system` / `light` / `dark` | `system` | Light / dark / follow system. The WinUI theme can only be set at process start (2026-10-10 decision to keep; see the decision record in mvp-issues) | Restart |
| uiOpacity | number | `1.0` | Unified opacity for the four floating surfaces (capsule, host panel, standalone floating window, quick panel). Settings slider range 0.2–1.0 in 0.05 steps (matching the runtime floor); below 0.2 is treated as 0.2 | Immediate |
| showCapsule | bool | `false` | Show/hide the L1 status capsule (the always-resident capsule in the bottom-right corner). Off by default per the 2026-10-09 decision; older configs on upgrade are flattened once | Immediate |
| keepQuickPanelOpen | bool | `false` | Keep the L2 quick panel resident (the "Keep Panel Open" check item in the tray right-click menu, 2026-10-11 user order). On = the panel no longer auto-hides on focus loss and stays on the desktop (Esc or clicking the tray again still closes it; the checkmark survives restart); off = Flyout transient semantics (closes when you click outside) | Immediate |
| pinDisplayMode | `panel` / `floating` | `panel` | L0 floating form: `panel` is a single window with multiple tiles; `floating` gives each pinned widget its own window, draggable anywhere on the desktop (2026-10-10 decision kept; takes effect on restart) | Restart |
| numericFloatingEnabled | bool | `true` | Whether numeric / quota widgets (AI quota cards etc.) may go on standalone floating windows. On by default (flipped by the 2026-10-11 user order); when off, such pins still show in the host panel | Immediate |
| pollIntervalSeconds | int (seconds) | `0` | Global check frequency; `0` = each widget's refresh tier uses its default period. Settings options: 15 s / 30 s / 1 min / 5 min. A widget with its own "检测间隔" overrides this | Immediate (rebuilds the refresh schedule) |
| settingsSidebarWidth | number (DIP) | `264` | Settings window left-column width. Dragging the splitter between the columns (turns blue on hover) persists automatically and restores on launch; clamped to [200, 420] | Restored on restart (saved right after the drag) |
| settingsWindowWidth / settingsWindowHeight | number (DIP) | `0` | Settings window size memory; `0` = use the default 880×640. Resizing the window edge persists after 0.8 s at rest and restores on launch | Restored on restart |
| notificationRules | rule array | `[]` | Custom notification rules; an empty array uses the four built-in defaults. See the next section | Immediate (import-package path); manual file edits need a restart |
| appearance | object | see next section | Severity color overrides, motion, and density | Immediate |
| configVersion | int | `1` | Configuration structure version, used by program migrations — do not edit by hand | Maintained by the program |
| numericFloatingResetDone | bool | `false` | Deprecated (the one-time set-to-off migration was removed when the 2026-10-11 numeric floating window switched to on by default); the field is kept only for old-config compatibility — do not edit by hand | Maintained by the program |

### Appearance (appearance)

Severity colors apply to the tray dot, the capsule, and widget status lights. When a key is absent the default palette is used and tray / capsule / L0 all follow it.

| Key | Type | Default | Effect |
|---|---|---|---|
| appearance.severityColors.info | `#RRGGBB` | `#58a6ff` | Info color |
| appearance.severityColors.success | `#RRGGBB` | `#3fb950` | Success color |
| appearance.severityColors.warning | `#RRGGBB` | `#d29922` | Warning color |
| appearance.severityColors.error | `#RRGGBB` | `#f85149` | Error color |
| appearance.severityColors.critical | `#RRGGBB` | `#ff3b30` | Critical color |
| appearance.severityColors.offline | `#RRGGBB` | `#8b949e` | Connection offline color |
| appearance.motion.mode | `full` / `reduced` / `off` | `reduced` | Motion tier: full / moderate / off. On off, every animation is still |
| appearance.motion.intensity | number 0.5–2.0 | `1.0` | Scales motion duration and magnitude. Settings slider steps by 0.1 |
| appearance.progressStyle | `"bar"` / `"pool"` | `"bar"` | How tiles show quota usage: **progress bar** (2026-10-11 redesign default: a 4 DIP rounded bar at the bottom + an NN% readout, ≥80% warning, ≥98% Error red) / **circular pool fill** (the legacy effect: water level = progress, full = Error red). Takes effect live, no restart needed |
| appearance.density | `"regular"` / `"compact"` / `"spacious"` | `"regular"` | Component spacing density tier: Regular (identity) / Compact (spacing ×0.75) / Spacious (spacing ×1.25), rounded half away from zero. Surfaces pick the tier up when rebuilt (tiles and the panel on their next render, detail and quick panel on next open); unknown values fall back to Regular |

Color changes take effect immediately (tray / capsule / L0 re-render with the new palette). Formats `#RRGGBB` and `#AARRGGBB` are accepted; invalid values report an error and are not saved. A widget can additionally override its own light via colorOverride in widgets.json.

### Notification rules (notificationRules)

Rule semantics: when a widget's state reaches a threshold band, fire a Toast / sound, with no repeat inside the cooldown. Fields:

| Field | Type | Default | Description |
|---|---|---|---|
| id | string | required | Rule identifier, used for record de-duplication |
| widgetType | string | `null` | Widget type to match; `null` = all types |
| widgetId | string | `null` | Match one specific widget instance |
| severityAtLeast | enum | `warning` | Minimum triggering severity, one of `success`/`info`/`warning`/`error`/`critical` |
| severityAtMost | enum | `null` | Maximum severity, forming a band with the floor (e.g. "warning only"); `null` = no ceiling |
| toast | bool | `true` | Pop a system Toast |
| sound | bool | `false` | Play a sound |
| cooldown | `hh:mm:ss` | `null` | Cooldown between repeat alerts (e.g. `00:30:00`). `null` = alert once per threshold episode |
| enabled | bool | `true` | Rule switch |

When the list is empty, built-in defaults apply:

| Rule id | Matches | Trigger | Cooldown |
|---|---|---|---|
| builtin.ci-failed | github.actions.runs | ≥ error | Once per threshold |
| builtin.ci-stuck | github.actions.runs | warning only (stuck runs) | 30 minutes |
| builtin.pr-review | github.pull_requests | ≥ warning (a review is waiting on you) | 30 minutes |
| builtin.connection-degraded | all widgets (refresh failure) | ≥ warning | 15 minutes |

The settings page "Advanced → Notification rules" shows the currently effective rule set (including the expanded built-in defaults).

## Connections (connections.json)

| Field | Type | Default | Description |
|---|---|---|---|
| id | string | required | Unique connection name. Credentials are stored in DPAPI under `conn:{id}` |
| type | string | required | Connection type, see the table below |
| endpoint | string | `null` | Service endpoint; `null` = the type's default endpoint. Overridable (self-hosted proxy / region / acceptance mock) |
| credentialRef | string | per type | DPAPI credential reference. The secret plaintext goes only into DPAPI; the JSON holds only the reference |
| enabled | bool | `true` | Module on/off. When `false`, its widgets stop refreshing, the UI drops their state, and pins leave the desktop |
| settings | object | `{}` | Type-specific keys, see the per-type notes |

### Per-type credentials and endpoints

The settings-page token box hints per type; secrets are always written to DPAPI only:

| Type | Default endpoint | What goes in the token box | Connectivity test |
|---|---|---|---|
| github | api.github.com | GitHub PAT | GET /rate_limit (does not consume core quota) |
| jenkins | required instance base URL (e.g. jenkins.example.com) | user:apiToken (empty = anonymous read; create under Manage Jenkins → Users → API Token, Basic auth) | GET /api/json (2xx and JSON = connected; guards against reverse-proxy false positives) |
| machine | none (leave empty) | none (local machine data, zero credentials zero network) | Not applicable: the connection is always up (the machine is itself) |
| herald | Herald base URL (required, e.g. `http://127.0.0.1:8080`) | API Key (empty = anonymous; required when `auth.enabled` is on; X-API-Key header, written to DPAPI only) | GET `/api/v1/status` (always public): 2xx and JSON = connected; a reverse-proxy HTML shell counts as Degraded |
| bigmodel | open.bigmodel.cn | Zhipu API key (the monitoring endpoint takes the raw key directly, no Bearer prefix) | GET quota/limit |
| ark | open.volcengineapi.com | Volcengine `AccessKey:SecretKey`, colon-joined, pasted at once (created in the console under "API 访问密钥"; the inference ARK_API_KEY cannot call the control plane) | V4-signed call to GetCodingPlanUsage |
| claude | none (reads local machine) | leave empty | Local session directory readable |
| kimi | api.kimi.com/coding/v1/usages (the community GET endpoint, default). Console endpoint `www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages`: paste the whole string as endpoint + add `http_method=POST`, `auth_header=Cookie` in Settings (supply `http_body` per F12 → Network → that request) | Either one: Kimi For Coding key (sk-kimi- prefix), or a browser Cookie (F12 → Cookies, copy the value and paste). **Beacon does no QR-scan / login flow** — the Cookie credential is copied from the browser by the user, stored encrypted in DPAPI, and replayed verbatim on requests; after session expiry (browser logout / expiry) just record it once more. Separate accounts from the Moonshot open platform | GET usages / POST GetUsages (2xx = connected) |
| moonshot | api.moonshot.cn/v1/users/me/balance | Moonshot open-platform API key (Bearer; separate accounts from Kimi For Coding) | GET balance (response must contain balance data) |
| sensenova | platform.sensenova.cn/lite/console/v1/tokenplan/pool-usage | SenseNova console JWT (Bearer). This is a **console session token, not an API key** — copy it after logging into the platform.sensenova.cn console; it carries an `exp` and expires | GET pool-usage (response must contain credit-pool data) |
| deepseek | api.deepseek.com | DeepSeek API key | GET /user/balance |
| mimo | token-plan-cn.xiaomimimo.com/v1 | Xiaomi API key (Bearer; only authorizes model calls — the quota additionally needs the console Cookie) | GET /models |
| qwen | token-plan.cn-beijing.maas.aliyuncs.com/compatible-mode/v1 | Bailian Token Plan key (sk-sp- prefix) | GET /models |
| codex | none (reads local machine) | leave empty | Local sessions directory readable |
| copilot | api.github.com/copilot_internal/user | GitHub PAT (copilot scope required) | Hits the quota endpoint directly with the same request headers |
| opencode | opencode.ai/console/api/v2/budgets/members | OpenCode key (oc_sk prefix; reading Budgets needs the All permission) | GET budgets/members |
| http | required | API key (optional) | GET Endpoint, 2xx = healthy |

### Common Settings keys

Connection types that go through the unified HTTP channel (`HttpEndpoint.FetchAsync`, `src/Beacon.Connections/HttpEndpoint.cs`) — bigmodel/deepseek/kimi/moonshot/sensenova/http — support:

> copilot/qwen/opencode/mimo **do not use the unified channel** (each builds its own requests, with its own hard-coded endpoints and headers): they only honor the connection-level `endpoint` (copilot/opencode) or the Settings `usage_endpoint` (qwen/mimo) plus credentials, not the four channel keys in the table below.

| Key | Default | Description |
|---|---|---|
| auth_header | `Authorization` | Auth header name (for Cookie credentials, set `Cookie`) |
| auth_prefix | `Bearer` | Value prefix; can be explicitly emptied (the bigmodel raw key and verbatim Cookie replay are the set-empty cases) |
| http_method | `GET` | Request method (POST etc.; the kimi console GetUsages is a POST endpoint) |
| http_body | empty | Request body (JSON text, sent only when configured; content per the request payload in the browser's F12 → Network) |

### Type-specific Settings keys

| Type | Key | Description |
|---|---|---|
| ark | region | Signing region, default `cn-beijing` |
| qwen | usage_endpoint | Custom usage endpoint (GET + Bearer). When empty, the model catalog is shown with an honest note that the official quota endpoint is not open |
| mimo | usage_endpoint | Custom usage endpoint (GET + Bearer): a counter-shaped response yields "本机累计 N 次"; a percentage shape (used/total etc.) yields used / percent; an unrecognized shape goes explicitly Degraded with the raw response attached (never passed through to the card) |
| mimo | litellm_spend_url | LiteLLM gateway accounting endpoint (e.g. `http://127.0.0.1:4000/spend/logs`). **Empty by default = nothing is sent out** (localhost 127.0.0.1 accounting does not count as outgoing); requests carry the month's start/end `start_date/end_date + summarize=true` (the gateway's openapi parameter names) |
| mimo | litellm_key_ref | Credential reference for the LiteLLM gateway key, default `mimo:litellm` (Bearer when present, plain call otherwise — local accounting usually needs no auth); when the gateway has no DB the spend side returns `no_db_connection` throughout and the card shows Degraded with "未接数据库" |
| mimo | console_usage_url | Console plan-usage API path (default `/api/v1/tokenPlan/usage`, GET + api-platform_ph Cookie + plan-manage referer) |
| mimo | console_credential_ref | Credential reference for the console Cookie, default `mimo:console` (Cookie value = the browser F12 → Cookies → api-platform_ph value; pasting the whole Cookie line also works — api-platform_ph is picked out automatically; **paste + DPAPI replay, no QR-scan login**; re-record on session expiry) |
| http | dsl_path | Path to a Provider DSL declaration file (YAML, see the "Provider DSL" section below). **Not configured = zero change**; when configured, mapping supplies data-extraction defaults (widget.Config instance values still win) and actions enter the widget detail page's action area. A missing / unparseable file → the affected cards go Degraded with an explicit reason, never silently |

### Provider DSL (declarative mapping and actions for http connections, 批3-补42)

Connection type `http` can point Settings `dsl_path` at a YAML declaration file (zero third-party dependencies; a tiny built-in YAML subset: indented mappings / `- ` lists / quotes / inline comments; flow `{}` `[]`, block scalars, Tab indentation, and multi-document are unsupported and fail loudly with a line number):

```yaml
provider: company-packager        # required; used by logs and validation
mapping:                          # optional: data-extraction defaults (keys per the table below; unknown keys error)
  status_path: $.job.state
  success_values: green,passed
actions:                          # optional: declared actions (into the detail page action area, through the existing executor chain)
  - id: restart                   # required
    type: http                    # open.url / http / webhook
    name: 重启打包任务
    url: https://packer.local/api/jobs/42/restart   # required; supports {var} templates
    method: POST                  # http defaults to GET
    body: '{"force": true}'       # optional (quote literals containing { })
    confirm: true                 # default: open.url=false, http/webhook=true
    retry:                        # optional; default 1 attempt (no retry)
      max_attempts: 3             # 1–10
      backoff_seconds: 5          # 0–300
```

| mapping key | Applies to | Default |
|---|---|---|
| status_path / summary_path / url_path | http.status status / summary / detail-link extraction | `$.status` / empty / empty |
| success_values / warning_values / error_values | http.status status vocabularies (comma-separated) | see the http.status notes |
| total_path / used_path / remaining_path / reset_path | http.quota numeric extraction | empty (total stays required) |
| unit | http.quota unit | empty |

Conventions: **the declaration layer only supplies data-extraction defaults** — `widget.Config` instance values win (the declaration layer never takes over instance overrides); `label/icon/warn_percent/error_percent` belong to the widget instance and are not in the DSL. Action ids automatically get the `dsl.{connection}.{id}` prefix and appear only on the detail pages of that connection's widgets (connection_id scoping); retry happens at the ActionRunner layer — the default 1 attempt = no retry (byte-for-byte equal to existing behavior), retries share one total timeout budget, user cancel / timeout never retries, and a message that still fails after several attempts carries a "已重试 N 次" annotation.

### Per-vendor quota conventions and data sources

| Connection | Widget types | Quota convention | Data source and degradation behavior |
|---|---|---|---|
| ark | ark.usage | Used percentage across a 5 h rolling window + weekly window + monthly window; **worst window wins** (2026-10-13 user order): severity / progress bar / main value take the highest-used window and label the window name; warn/error thresholds still apply to the worst window's used % | Control-plane OpenAPI (V4 signing). Any window at 100% shows "额度用尽（窗名，重置时间）" for the whole card; a 429 quota-exhausted code (AccountQuotaExceeded etc.) is treated directly as that window spent with the reset time; a pure rate-limit 429 does not flip |
| bigmodel | bigmodel.usage | Used percentage across a 5 h rolling + weekly + monthly window (the windows have no absolute token figure; the monitoring endpoint gives percentages only); **worst window wins** (2026-10-13 user order): severity / progress bar / main value take the highest-used window and label the window name; thresholds still apply to the worst window's used % | Monitoring endpoint quota/limit. The main value no longer falls back through rolling → weekly → monthly availability; it takes the highest-used window |
| claude | claude.usage | Tokens and cost in the window (cost only when costUSD is present; Beacon does not price by itself) | Reads local `~/.claude/projects/**/*.jsonl`, zero credentials zero network. Only when daily_cost_limit is set does usage raise the severity |
| codex | codex.usage | Tokens over the last N days (per session file, the last cumulative entry is summed; honestly labeled "本地统计，非官方口径") | Reads local `~/.codex/sessions/**/rollout-*.jsonl`. The official endpoint needs OAuth, so this is the local-statistics tier |
| kimi | kimi.coding | Card / progress bar = used % (remaining derived); severity still judged on remaining (0% remaining shows "已用尽"); **worst window wins** (2026-10-13 user order): severity / progress bar / main value take the lowest-remaining (= highest-used) window and label the window name; thresholds still on remaining percentage | `usages[]` (scope=FEATURE_CODING) entries: detail = period window, in-entry limits[] = 5 h window, `totalQuota` into payload; values are strings (captured live 2026-10-11; parsing tolerates). The old shape (top-level usage + limits[]) remains a compatible fallback |
| moonshot | moonshot.balance | Balance amount (¥); balance ≤ warn_below (default 20) → Warning, ≤ error_below (default 5) → Error | GET /v1/users/me/balance (Bearer). A response without balance data goes Degraded |
| sensenova | sensenova.tokenplan | Console Token Plan credit-pool used percentage (multiple pools × 5 h/7 d double windows); **worst window of the worst pool wins**: main value / progress bar / severity take the highest-used window and label pool and window names; thresholds (warn_percent 80 / error_percent 95) apply to the worst window's used %; the summary shows all pools; bonus-expiry warning (nearest expiry within 7 days, or expiring-balance ≤10% of that pool's bonus) raises the severity floor to Warning | Console pool-level endpoint (Bearer JWT). JWT-expired 401 → Degraded explicitly saying "控制台 token 已过期，请重新获取", never silent empty data; no pools / no window data is a legal state (an honest Info about the subscription, no fake red); values are decimal strings (with thousands separators) and parsing tolerates; multiple accounts = multiple connections (one console JWT each, each with its own pools) |
| deepseek | deepseek.balance | Balance amount (CNY/USD); severity judged on total ≤ threshold | GET /user/balance. is_available=false goes straight to Error |
| mimo | mimo.usage | Plan used / total / percentage (shown once the console Cookie is recorded); no Cookie but litellm_spend_url set → "本月已用 $X（网关记账）" (the gateway has no quota denominator, so only the used amount is shown — honest, not invented); otherwise the local counter; otherwise an honest "无额度口" | Four descending tiers: console API → LiteLLM gateway accounting → local counter → model-catalog count (the model count is a catalog, not a quota — it does not pretend). Console 401/403 shows "Cookie 已失效或未录制" on the card + a re-record guide (Degraded, not a silent downgrade). The true console response, captured 2026-10-11: data.monthUsage / usage blocks + items[], where the percent field is a **0–1 ratio** (used/limit computes the exact percentage; ≥90% Warn / ≥100% Error). The LiteLLM response tolerates four shapes: root array / response[] / results[] / summarize rollup object |
| qwen | qwen.usage | Token Plan used percentage + reset day (personal: a fixed 7-day window; team: monthly) | Four-stage probing: official usage API absent → custom usage_endpoint → quota probe (when exhausted, any POST gets a 429 AllocationQuota carrying the reset time; non-exhaustion 429s do not flip) → model catalog fallback |
| copilot | copilot.usage | premium_interactions slot used percentage (chat/completions unlimited, not counted); quota reset day shown | The same quota endpoint the official client uses, requiring the Editor-Version / Copilot-Integration-Id headers |
| opencode | opencode.usage | Monthly budget (amount in micro-cents); with a cap set, card / progress bar = cap-converted used % (Progress = used share); multi-member aggregation: spending summed, any member uncapped = uncapped, any overspender = over | Console Budgets API. exceeded → Error (full bar); uncapped → Success baseline, no invented percentage |
| github | github.pull_requests / github.actions.runs | Not a quota: PR-list state / workflow-run state | REST + ETag conditional requests, rate-limit aware |
| jenkins | jenkins.build | Not a quota: latest build state of a job (success / failure / unstable / building / not built / disabled) | Open-source standard JSON API (works for any instance): the job endpoint escapes each `job/<name>` segment for Folder support; one request judges severity by `color` (job-level lastBuild lacks result — measured); the `_anime` suffix = building |
| machine | machine.net / machine.git / machine.local | Not a quota: local network (IP / live ↑↓ speed / cumulative traffic) + local Git repositories (↑↓ · changed · stash, one card per repository) + local resources (CPU / memory / disk, three percentages in a row) | Network: reads only the local `NetworkInterface` (IPv4 send/receive counters accumulated since boot); speed = the difference between adjacent samples ÷ the time delta (machine tier 15 s; the first refresh has no baseline and shows only IP and cumulative values); a counter reset / NIC change re-baselines; cumulative traffic lives in the L3 detail. Git: three read-only git CLI calls (status --porcelain -b / rev-list / stash list, zero network zero credentials; repositories with no upstream do not show ↑↓); behind>0 or diverged turns yellow (you need to pull / rebase); a preview of the first 5 changed files lives in L3. Resources: Linux reads /proc (first line of stat + meminfo), Windows goes through GetSystemTimes + GlobalMemoryStatusEx, disk uniformly DriveInfo (empty = system drive / root partition; a named-but-missing drive fails loudly); CPU uses the same differencing (no baseline on first refresh, no CPU shown); the three thresholds default to 90%, over the line turns yellow (compared against the rounded display value — what you see is what warns). **Optional display items** (2026-10-12): the widget config `show` is a comma-separated multi-select — machine.local can pick `cpu_percent / mem_percent / disk_percent`, machine.net can pick `down_bps / up_bps / ip`; empty = show all (the default layout unchanged); unselected items leave the floating tile value slot, the quick panel Summary, and the detail page payload (all three share one source), while raw ledger keys (mem_used / total_sent etc.) are not selectable and always stay in the detail page. The floating tile width is auto-derived from the worst value row of the selected items (`TileSizing`, floor 168); changing show takes effect on the next refresh, no restart. Everything reads the local machine only and never touches the frozen remote machine management |
| http | http.status / http.quota | Custom: a status-word mapping or a JSONPath numeric card | Any HTTP JSON endpoint; all field paths configurable |
| herald | herald.incidents / herald.feed / herald.deliveries | Not a quota: the incident ledger (open / acknowledged / resolved / all counts + the latest one) + the feed (RSS entries / unread count) + delivery failures (failed count + the latest one's destination and error) | incidents: `/api/v1/incidents?status=…` (default open; a nonsense value fails loudly), open>0 yellow, containing critical red, retrospective views (acked/resolved) always Info; feed: public category `/feeds/{category}.xml` (slug validated against injection) or private `/feeds/private/{token}.xml` (the token is the credential, goes to DPAPI, never into widgets.json nor the payload; category wins); unread = guids new since the last refresh (baseline kept per widget; the first refresh only records the baseline, no unread); deliveries: `/api/v1/logs?status=failed&limit=200[&provider=X]`, failed>0 yellow (undelivered mail needs a human; retries exhausted dead is also written failed at the log layer, not distinguished; shadow is naturally excluded by status=failed, pending is transient and not reported), the count comes from the response total (the filtered total over the whole window), filterable by provider; RFC1123Z timestamps are parsed with the weekday name stripped (invariant-globalization compatible) |

**Multi-window quota display** (2026-10-12; unified to the used-amount convention by the 2026-10-13 user order): when more than one window of bigmodel/ark/kimi has values at the same time, the tile value slot renders the `5%/5h 70%/w 80%/M` form (all-used convention, no prefix; only windows that exist are shown; window order 5 h → weekly → monthly; for kimi, used = derived from remaining). Progress bars are likewise the used share everywhere (provider Progress is the same source); the kimi severity convention is unchanged — thresholds still on the remaining percentage. In the multi-window form the tile collapses the duplicate "NN%" progress readout (the value text already carries it); the single-window form is unchanged. Tile width follows each type's worst value-row text budget (`TileSizing`); the value column is never truncated. sensenova is the same family but a different form: the tile value slot takes the **worst window of the worst pool** (pool name + window name + used %, e.g. "Flash-Lite池 5h 已用100%"), and the summary shows every pool (each "short name 5h X% · 7d Y%", pools separated by " / ").

## Widgets (widgets.json)

| Field | Type | Default | Description |
|---|---|---|---|
| id | string | required | Unique widget instance name |
| type | string | required | Widget type, e.g. `ark.usage` (see the widget-type column above; also `github.pull_requests` etc.) |
| connectionId | string | `""` | The bound connection id |
| config | object | `{}` | Type-specific fields, see the next section; common keys: `label` (tile display name), `icon` (override the brand glyph), `progressStyle` (`pool` = quota progress switches to the legacy circular-pool-fill effect; absent = follow the appearance setting's "额度显示方式") |
| refreshTier | string | `default` | Refresh tier, see the tier table |
| refreshIntervalSeconds | int | `null` | Detection-interval override (seconds). `null` = the refresh tier's period; settings options 30 s / 1 / 2 / 5 / 15 min / 1 hour |
| pinned | bool | `false` | Whether pinned to the desktop (L0). The context menu, the settings page, and the quick panel all write this one field |
| colorOverride | `#RRGGBB` | `null` | Status-light color override; priority: widget > global palette; invalid values fall back to the default |
| pinLayout | object | `null` | Layout convenience reference (pins.json is authoritative; both are written together) |

### Refresh tiers (refreshTier)

| Tier | Period | Applies to |
|---|---|---|
| pr | 60 s | GitHub PRs |
| ci | 15 s | CI runs, AI quota widgets |
| machine | 15 s | Machine state |
| agent | 10 s | Agent activity |
| workflow | 10 s | Workflow events |
| static | no auto refresh | Manual / event-triggered |
| default | 60 s | Fallback |

Failure backoff: multiplies the tier period, capped at 10×; the period carries 20% jitter.

### Widget config fields (by type)

Common fields (most quota widgets have them):

| Field | Applies to | Default | Description |
|---|---|---|---|
| label | all | per type | Display name, on the tile and the card title |
| icon | types with a brand slot | the connection's brand | Icon (icon picker) |
| warn_percent | used-% quota widgets | 60/80 per type | Turns Warning when used reaches this |
| error_percent | used-% quota widgets | 90/95 per type | Turns Error when used reaches this |

github.pull_requests:

| Field | Default | Description |
|---|---|---|
| repo | required | `owner/repo` |
| warnOnReviewRequested | — | When `true`, alerts on "PRs waiting for your review" |
| warnOnRedCi | `false` | When `true`, a PR's red CI (combined status `failure`) counts toward the alert. CI state is remembered per PR: terminal and unchanged-commit PRs reuse the memory (steady state, zero extra requests); probes go out only for new pushes or in-flight PRs; the first sweep after enabling costs one request per PR — expensive on huge repositories at first refresh |

github.actions.runs:

| Field | Default | Description |
|---|---|---|
| repo | required | `owner/repo` |
| workflow | required | Workflow file name (e.g. `ci.yml`) |
| branch | repository default branch | Branch filter |

claude.usage:

| Field | Default | Description |
|---|---|---|
| days | `1` | Statistics window (days) |
| daily_cost_limit | none | USD per window. Only when set does usage raise severity; otherwise always Success |
| warn_percent / error_percent | 60 / 90 | Pairs with the cost cap |

codex.usage:

| Field | Default | Description |
|---|---|---|
| days | `7` | Statistics window (days) |

kimi.coding (thresholds judge the remaining amount; card / progress bar show used %):

| Field | Default | Description |
|---|---|---|
| warn_percent | `30` | Warns when remaining drops below this |
| error_percent | `10` | Errors when remaining drops below this |

deepseek.balance (balance convention):

| Field | Default | Description |
|---|---|---|
| warn_below | `20` | Warns when the balance drops below this (in the balance's currency) |
| error_below | `5` | Errors when the balance drops below this |

http.status:

| Field | Default | Description |
|---|---|---|
| status_path | required | Status field JSONPath (e.g. `$.status`) |
| summary_path | — | Summary field path |
| url_path | — | Detail-link path |
| success_values | `success,ok,done,passed,healthy,finished` | Success status words (comma-separated) |
| warning_values | `running,building,pending,queued,in_progress,deploying,starting` | In-progress status words |
| error_values | `failed,failure,error,critical,broken` | Failure status words; empty / unknown words count as Warning (better to warn than to miss) |

http.quota:

| Field | Default | Description |
|---|---|---|
| total_path | required | Total field path |
| used_path | one of the two | Used field path |
| remaining_path | one of the two | Remaining field path |
| reset_path | — | Reset-time path (epoch seconds / milliseconds / ISO auto-detected) |
| unit | — | Unit label (e.g. tokens) |
| warn_percent / error_percent | 60 / 90 | Thresholds; missing data counts as Warning |

ark.usage / bigmodel.usage / qwen.usage / copilot.usage / opencode.usage: common fields only (label, warn_percent, error_percent); each vendor's default thresholds are in the quota-convention table (bigmodel/ark/qwen 60/90, copilot/opencode 80/95). mimo.usage has only label (thresholds fixed at 90/100, not configurable).

## Pinned layout (pins.json)

| Field | Type | Description |
|---|---|---|
| tiles[].widgetId | string | The corresponding widget id |
| tiles[].layout.monitor | string | Monitor identifier (device name + resolution); restored by anchor after hot-plug |
| tiles[].layout.anchor | enum | Anchor corner: topLeft/topRight/bottomLeft/bottomRight |
| tiles[].layout.offsetDips | object | Offset from the anchor corner (DIP, scales with DPI) |
| tiles[].layout.collapsed | bool | Edge-collapsed state (strip / dot; hover expands) |
| tiles[].layout.floatingX | int | Window X in standalone-floating mode (physical pixels, kept per widget; null = never dragged, cascades in order) |
| tiles[].layout.floatingY | int | Window Y in standalone-floating mode |

## Custom actions (actions.json)

| Field | Type | Default | Description |
|---|---|---|---|
| id | string | required | Unique action name |
| type | enum | required | `open.url` / `http` / `gh.workflow_dispatch` / `local.command` / `webhook` |
| name | string | `null` | Display name |
| requireConfirmation | bool | `true` | Confirm before executing dangerous operations; can be turned off per action |
| parameters | object | `{}` | Parameters per type (URL / command / workflow file etc.) |

Actions appear in the L3 detail window's ACTIONS area filtered by widgetType; the editor lives in settings "Advanced → Custom actions".

## Configuration import and export

Export packs config/connections/widgets/pins/actions into one JSON package (settings "Advanced → Import/Export"):

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

Red line: secrets never enter the package. The exported file contains no token anywhere; connections carry only credentialRef references. After import, each credentialRef in the package prompts once to re-enter the secret (the new machine's DPAPI has no matching plaintext). The `actions` field was added later; an older package without it still imports (empty action library, no error). Import is a wholesale replacement: what is in the package lands; keys absent from the package fall back to defaults.

## Floating display policy

Three switches decide where a widget finally appears:

| Switch | Location | Effect |
|---|---|---|
| pinDisplayMode | Settings → General | L0 form, one of two: host panel (one window, multiple tiles) / standalone floating windows (one window per widget). Switching needs a restart |
| numericFloatingEnabled | Settings → General | Whether numeric / quota widgets may go on standalone floating windows. On by default (2026-10-11 user order); off means such pins show only in the host panel, data unaffected |
| pinned (per widget) | Context menu / settings page / quick panel | Whether the widget is pinned to the desktop |

Decision rules: information-dense widgets (trend charts / light groups) are not governed by the numeric floating master switch; numeric / quota widgets (FloatingOptIn — all AI quota cards, HTTP numeric cards) are. In floating mode, a widget already shown in a floating window does not repeat in the host panel; in panel mode the panel is the only L0 and the master switch gates only the standalone-floating route. A widget whose connection is disabled (enabled=false) leaves the desktop, and its state and events are cleared together — no stale light is left behind.
