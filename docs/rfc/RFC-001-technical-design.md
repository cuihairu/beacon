
# RFC-001: Beacon Technical Design

| | |
|---|---|
| Status | Draft (pending review) |
| Date | 2026-10-07 |
| Upstream | [Mission](../mission.md) (positioning / phasing / acceptance criteria) · [Product & Technical Implementation Master Plan](../master-plan.md) (finalized 2026-10-11; conflicts adjudicated, see the mvp-issues decision log) |
| Downstream | [Solution project layout](../solution-structure.md) · [MVP issue list](../mvp-issues.md) |
| Platform | Windows 10 1809 (17763)+ / Windows 11, Windows only (`TargetPlatformMinVersion=10.0.17763.0`) |

---

## 1. Background & Positioning (Summary)

Beacon is a **resident developer status & action center on the Windows desktop** (Developer Status & Action Center). Its core is **Signal + Action**: it watches the development environment (GitHub / CI / machines / Agents) on the developer's behalf, proactively raises a signal when attention is needed, and provides one-click actions. It is not a Web Dashboard and does not replace GitHub / Jenkins / Grafana; it only does **aggregation + alerts + low-cost next actions**.

Product positioning, scenarios, phasing and MVP acceptance criteria are governed by the mission document; this RFC does not repeat them and settles technical decisions only.

## 2. Goals and Non-Goals

### 2.1 MVP Goals

1. Auto-start on boot, always resident (Tray + desktop floating layer), non-intrusive.
2. GitHub (PR / Actions) status aggregated into a unified Severity, visible on the desktop.
3. Proactive Toast when a failure occurs; one click reaches the failure details.
4. One-click Run / Retry / Cancel / Open, never opening a browser along the way.
5. Local First: configuration lives on the machine, Secrets go into the system secure store.

### 2.2 Non-Goals (explicitly out of scope for MVP)

A complex plugin marketplace, workflow DSL/engine, AI Agent, full Jenkins/Docker management, databases, cloud accounts, a user system, team collaboration, cloud sync, complex charts, complex theming. **Beacon connects tools; it does not rebuild them.**

## 3. Overall Architecture

A single-process WinUI 3 app with four layers of separated responsibilities:

```text
┌──────────────────── Beacon.App (WinUI 3 Shell, the only exe) ─────────────────┐
│  Tray │ Hotkey │ PinnedHost(L0/L1) │ QuickPanel(L2) │ Detail(L3) │ Settings │
│                    Toast │ startup │ single instance │ multi-monitor/DPI      │
└──────────────┬───────────────────────────────────────────────┬──────────────┘
               │ Event Bus (UI only consumes state, never pulls data; code-built UI + code-behind) │
┌──────────────▼──────────── Beacon.Core ───────────────────────▼──────────────┐
│ Models(Severity/LifecycleState…) │ StatusAggregator │ RefreshScheduler        │
│ NotificationEngine │ ActionRunner │ CacheStore │                              │
│ Abstractions: IConnectionProvider / IWidgetProvider / IActionExecutor / ISecretStore │
└──────┬─────────────────┬──────────────────────┬─────────────────┬────────────┘
       │                 │                      │                 │
 Beacon.Connections   Beacon.Actions        Beacon.Storage      (Phase 2: Plugin split)
  GitHub/HTTP/usage    Url/Http/Command       JSON config/Cache/
 (six connection types)                       DPAPI Secrets/import-export
```

Data flow (one-way):

```text
RefreshScheduler → Connection/WidgetProvider pulls
    → WidgetState(Severity + payload + fetchedAt)
    → Event Bus
        ├→ StatusAggregator → capsule / tray / L0 status lights
        ├→ NotificationEngine → rule matching → Toast + notification center
        └→ UI (L0/L2/L3) renders cache-first, incremental updates after background refresh
```

## 4. Core Domain Model

### 4.1 Severity (five unified levels, the single yardstick across the product)

| Level | Visual | Semantics |
|---|---|---|
| `Info` | Blue (system accent color) | Neutral information (PR opened, release) |
| `Success` | 🟢 Green | Normal (build succeeded, healthy) |
| `Warning` | 🟡 Yellow | Needs attention (running overtime, slow, retrying) |
| `Error` | 🔴 Red | Failure (build failed, connection error) |
| `Critical` | 🔴 Red pulse | Needs immediate handling (deployment failed, agent crash) |
| (Derived) `Offline` | ⚪ Gray outline | Connection unavailable, shows Last update |

**Providers may not define custom states**; they can only map onto the enums above + `LifecycleState`.

**Colors are configurable** (user-confirmed requirement: 「icon 颜色我都可以配置」): the table above is the default palette; `config.json appearance.severityColors` overrides per level globally, and `widgets.json colorOverride` overrides per widget instance (precedence Widget > level > default; illegal values fall back to the default). L0 tiles / the L1 capsule / L2/L3 status dots / the tray icon all share the same post-override palette (schema in §9.1).

Unified CI/Workflow lifecycle: `Queued / Running / Success / Failed / Cancelled / Skipped / Unknown` (UI decoupled from CI systems).

### 4.2 Configuration Model (JSON)

```jsonc
// connections.json —— stores only credentialRef, never tokens
{
  "id": "conn-github-main",
  "type": "github",
  "endpoint": "https://api.github.com",
  "credentialRef": "beacon/github-main",
  "settings": { "owner": "cuihairu", "repos": ["example"] }
}

// widgets.json
{
  "id": "w-ci-client",
  "type": "github.actions.runs",
  "connectionId": "conn-github-main",
  "config": { "repo": "cuihairu/example", "workflow": "build-client.yml", "branch": "main" },
  "refreshTier": "ci",
  "pinned": false,
  "pinLayout": null          // layout is carried by pins.json when pinned=true (here it is a convenience reference)
}

// pins.json (L0 pinned widget layout, see §6.2; nested under tile.layout, enums serialized camelCase)
{
  "tiles": [{
    "widgetId": "w-ci-client",
    "layout": {
      "monitor": "DEL-U2723QE-3840x2160",
      "anchor": "bottomRight",
      "offsetDips": { "x": 24, "y": 120 },
      "collapsed": false,
      "floatingX": null,     // window position in floating-frame form (physical pixels); not written in panel mode
      "floatingY": null
    }
  }]
}
```

### 4.3 WidgetState (Runtime State)

```text
WidgetState = { widgetId, widgetType, connectionId, severity, lifecycle?, summary,
                detailUrl?, progress?, payload, fetchedAt, isStale, connectionHealthy }
```

### 4.4 Status Aggregation

Overall status = the max severity across all widgets (Critical > Error > Warning > Info > Success). The capsule/tray only answers 「有没有事」, L2 answers 「什么事」 after a click, and L3 answers 「细节与操作」.

## 5. Provider Abstraction

```csharp
public enum Severity { Info, Success, Warning, Error, Critical }
public enum LifecycleState { Queued, Running, Success, Failed, Cancelled, Skipped, Unknown }

public interface IConnectionProvider {
    string ConnectionType { get; }                       // "github" | "http" | "bigmodel" | "claude" | "kimi" | "deepseek"
    Task<ConnectionHealthState> TestAsync(ConnectionConfig connection, ConnectionContext context, CancellationToken ct);
}

public interface IWidgetProvider {
    WidgetTypeDescriptor Descriptor { get; }             // incl. pinSupported, refreshTier suggestion
    Task<WidgetState?> GetStateAsync(WidgetConfig widget, ConnectionConfig connection, ConnectionContext context, CancellationToken ct);
}

public interface IActionExecutor {
    string ActionType { get; }                           // "open.url" | "http" | "gh.workflow_dispatch" | "local.command"
    Task<ActionResult> ExecuteAsync(ActionConfig a, CancellationToken ct);
}

public interface ISecretStore {
    Task<string?> GetAsync(string credentialRef);
    Task SetAsync(string credentialRef, string secret);
    Task DeleteAsync(string credentialRef);
}
```

Rules:

- **Provider First**: GitHub does not enter Core; Core holds only interfaces + scheduling + aggregation.
- The same Connection is reused by multiple widgets; credentials are configured once.
- The GitHub Provider must implement **ETag conditional requests** (304s do not count against quota) and rate-limit awareness (the interval is automatically stretched when remaining quota drops below a threshold).
- CI Providers (GitHub Actions / Jenkins / GitLab) map uniformly onto `LifecycleState`.
- Phase 2 splits Providers into standalone assemblies (Plugins); for the MVP it suffices to keep the interface boundary clean.

## 6. Presentation-Layer Architecture: Status Display & Pinned Widget Design (Dedicated Section)

### 6.1 Four-Layer Interaction Model (Progressive Disclosure)

Beneath the original three layers (capsule / quick panel / detail) an **L0 pinned widget** is added, forming four layers:

```text
                    Always-resident entry: Tray (reachable from any layer) + Global Hotkey (Ctrl+Alt+B)

 L0  Pinned Widgets   —— two forms (pick one via Settings 「悬浮形态」): host panel (default,
                          one transparent always-on-top host window per monitor, many tiles per window) / floating frames
                          (one window per pinned widget, freely draggable on the desktop); both NoActivate — view only, no popups
 ┌────────────┐ ┌────────────┐ ┌──────────────┐
 │ 🔴 CI   1  │ │ 🟡 PR   3  │ │ ⏳ build 62% │    single widget pinned: five-level status light + minimal digits
 └─────┬──────┘ └─────┬──────┘ └──────┬───────┘    change-sensitive content only, tiered refresh; shows Last update offline
       │ click         │ click          │ click
       ▼              ▼               ▼
 L1  Status Capsule   —— aggregates all widgets: overall = max severity
 ┌─────────────────────────────┐
 │ 🟢 18   🟡 3   🔴 1         │
 └─────────────┬───────────────┘
               ▼ click
 L2  Quick Panel   —— overview + recent events + one-click actions
 ┌────────────────────────────────────────┐
 │ Overview   GitHub 12 · CI 3 · Agents 4 │
 │ Recent     🔴 Server Build Failed      │
 │            🟡 Client Build Running     │
 │ [Build]  [Test]  [Deploy]              │
 └─────────────┬──────────────────────────┘
               ▼ need details
 L3  Detail Window   —— full details and all actions for a single object
 ┌────────────────────────────────────────┐
 │ Build #1024 · Failed · 12m31s · main   │
 │ Stage: Compile   Error: …              │
 │ [Open GitHub]  [Retry]  [Cancel]       │
 └────────────────────────────────────────┘
```

| Layer | Carrier | Visibility | Responsibility | Focus |
|---|---|---|---|---|
| L0 Pinned Widget | Host panel form: tiles inside a transparent always-on-top host window; floating-frame form: one window per widget | Always resident (collapsible) | Change-sensitive state of a single widget | **Never takes focus** |
| L1 Status Capsule | Independent always-on-top resident window (`CapsuleWindow`) | Always resident (can be turned off in Settings) | Global aggregation of 「有没有事」 (whether anything is wrong) | Never takes focus |
| L2 Quick Panel | Independent Flyout window | Appears on click, closes on focus loss/ESC | Overview + quick actions | Activatable |
| L3 Detail Window | Independent window | Appears on click | Single-object details + all Actions | Activatable |

The layers form a **drill-down relationship**, not side-by-side dashboards: each layer up is smaller and quieter; when a problem appears it turns red and floats an entry up one layer.

### 6.2 L0 Pinned Widget Design

#### 6.2.1 Creation and Toggling

- **Per-widget toggle in Settings**: Settings → Widgets list, one 「钉到桌面」 toggle per row.
- **Right-click Pin**: right-click a widget item in L2 → Pin to desktop / Unpin.
- Both entries write the same config field (the `pinned` field in `widgets.json`); layout lands in `pins.json`.
- Only widget types with `Descriptor.pinSupported == true` can be pinned (admission policy in 6.2.5).

#### 6.2.2 Form Factor: Mini Capsule

```text
 Normal           Warning          Failure          Collapsed (edge)  Offline
┌──────────┐   ┌──────────┐    ┌──────────┐
│ 🟢 CI  1 │   │ 🟡 CI 12m│    │ 🔴 CI  1 │    ‖(thin edge strip/dot)  ⚪ CI  Last 10:31
└──────────┘   └──────────┘    └──────────┘
```

- Size: 32 DIP tall, width follows content (48–120 DIP), rounded capsule, acrylic/semi-transparent background, follows the theme (Light/Dark).
- Composition = **status light + label + minimal digits/progress**, no body text. Progress-type (building) shows a percentage or a thin progress bar.
- The status light uses the §4.1 unified visuals (default `Info blue · Success green · Warning yellow · Error red · Critical red pulse · Offline gray outline`); colors support **per-level/per-widget user overrides** (§4.1 palette rules).
- Motion follows §6.2.8: the default (reduced tier) has only severity color transitions + Critical pulse (≤1Hz); richer motion families (breathing/blinking/slide-in) are enabled by the user in Settings and can be turned off wholesale.

#### 6.2.3 Window and Position Behavior

| Behavior | Spec |
|---|---|
| Topmost | Topmost, but **WS_EX_NOACTIVATE**: clicks do not steal focus, and current input focus is never interrupted |
| Draggable | Left-button drag on the tile (3px dead zone); snaps to the four edges while dragging (`SnapDips=16`); dragging to a screen edge collapses it into a thin strip |
| Edge-snap collapse | Drop flush against an edge → collapses into a thin strip/dot (releasing near an edge springs it back open to prevent mistouch); hover expands it, a click in droplet state = spring back open, a click in expanded state enters L2; the collapsed state is persisted |
| Multi-monitor position memory | Persisted as "monitor identity + anchor corner + DIP offset"; restored by anchor after monitor hotplug/RDP disconnect; if restore fails (e.g. out of bounds after a resolution shrink) it is automatically recycled to the nearest legal anchor corner |
| DPI adaptation | PerMonitorV2; listens to DPIChanged, tiles are laid out in DIPs scaled per monitor |
| Multi-monitor | Host panel form: **one host window per monitor**; tiles can only live inside their own monitor's host; dragging across monitors = migrating the pinLayout record. Floating-frame form: one window per widget, position recorded per widget (`floatingX/Y` physical pixels) |

#### 6.2.4 Rendering Architecture: Single Window, Multi-Widget Shared Rendering

- By default (host panel form) we deliberately do **not** create one HWND per pinned widget (window-count explosion, per-window DPI/hit-test upkeep). **Instead**: one transparent layered host window per monitor (`PinnedHostWindow`) that renders all pinned widgets of that monitor as a tile list.
- **Floating-frame form** (`pinDisplayMode=floating`, effective after restart, B-701 extension): one independent window per pinned widget (`FloatingTileWindow`, 168×32 DIP, Topmost + `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW`), freely draggable on the desktop, position recorded per widget (`PinLayout.FloatingX/Y` physical pixels, cascaded in order if never dragged). The original "window-count explosion" concern is defused by "explicit user choice + few widgets (pinned sets are usually <10)"; both forms share the same `PinTile` rendering and click drill-down chain.
- **The L1 capsule (`CapsuleWindow`) is an independent window** and does not change with the floating form; in host panel form it is, like the L0 tiles, an always-on-top NoActivate resident — "single-window multi-widget shared rendering" refers to the L0 tiles inside the host panel sharing one rendering pipeline, event handling, and DPI handling.
- **The L1 capsule = one resident aggregate tile inside the host window**, sharing the same rendering pipeline, event handling and DPI handling as the L0 tiles — that is the concrete meaning of "single-window multi-widget shared rendering".
- The host window has `ClickThrough=false` (it must receive clicks); only tile rectangles participate in hit-testing; clicks on empty areas pass through to the desktop.
- Tile rendering goes through Composition (visual layer + property animations); a state change updates only that tile's visual properties, never rebuilding the whole window.

#### 6.2.5 Content Admission: Only 「变化敏感」 Content

L0 carries only information whose changes the user needs to passively notice, declared by widget-type metadata:

| Pinnable (pinSupported) | Not suitable for pinning |
|---|---|
| CI status lights, PR counts, build progress, Agent online status, machine health summaries | Releases lists, commit streams, purely static/low-frequency content, long-text types |

- Refreshing follows the §7 tiered strategy (CI 10–30s, PR 30–120s…); **L0 introduces no new refresh frequencies** — unpinned widgets refresh as usual, they just don't reach the desktop.
- When several tiles share one Connection they share one poll (the scheduler merges by `(connection, tier)`); N tiles never means N× the requests.

#### 6.2.6 Offline and Error Degradation

- When a connection is unavailable: the tile shows the `Offline gray outline light` plus `Last update HH:mm` (hover tooltip or small type); **it never pops an error dialog**.
- It returns to normal automatically after recovery. `isStale` (data not updated for more than 2× the refresh period) is flagged in the tooltip.
- The corresponding widget in L2/L3 shows "⚠ Unable to refresh · Last update 2 minutes ago · [Retry]".

#### 6.2.7 The View-Only, No-Popup Principle (L0 Iron Rules)

1. **L0 only shows status and supports click drill-down** (click a tile → open L2 focused on that widget, or go straight to L3).
2. **L0 never produces any popup/Toast/sound** — Alerts belong to the NotificationEngine (§8); L0's only expression of an Alert is the status light changing color.
3. **L0 carries no Actions requiring confirmation** (Deploy/Restart only ever appear in L2/L3); at most zero-risk "open link" actions are allowed.
4. L0 never steals focus, never appears in Alt-Tab, and never enters the taskbar.

#### 6.2.8 Colors and Motion (the presentation layer of 「桌面角落在替我盯着」 — a desktop corner keeping watch for me)

User effect confirmation (original words): 「我关心的状态直接悬浮在桌面显示，icon 颜色我都可以配置，最好加点动效。」 Accordingly the presentation layer was revised from "purely static" to "configurable colors + moderate default motion, adjustable and switchable":

| Item | Spec |
|---|---|
| Palette | Default five levels + Offline color table; `appearance.severityColors` overrides globally per level, `colorOverride` per widget (precedence Widget > level > default; illegal values fall back to default); L0/L1/L2/L3/tray recolor from the same source (§4.1) |
| Motion tiers | `appearance.motion.mode`: `full / reduced (default) / off`; `motion.intensity` 0.5–2.0 scales duration and amplitude |
| Motion families | (1) severity color transition (cross-fade old color → new color, ~200ms) (2) alert blink (two short blinks after a state change) (3) breathing light (slow bright/dim loop, resident in full tier) (4) pulse (Critical by default, ≤1Hz) (5) slide-in (tile first appearance / collapse-expand) |
| Constraint revision | The former "desktop residents must not run continuous animations (power drain)" iron rule is **downgraded to a default**: the reduced tier has only color transitions + Critical pulse; the full tier allows resident motion (power cost borne by the user); off is fully static. **Unchanged**: motion never steals focus, makes sound, or pops anything (the §6.2.7 iron rules remain) |
| Implementation | Composition property animations (ColorKeyFrame/ExpressionAnimation) act only on the given tile's visual layer, never rebuilding the window; animation loops pause when the window is invisible or collapsed |

### 6.3 L1 Status Capsule (Summary)

An independent always-on-top resident window (`CapsuleWindow`, bottom-right of the screen, position remembered) showing per-severity counts; clicking opens L2. The capsule can be turned off in Settings leaving only L0, and vice versa (`showCapsule`).

### 6.4 L2 Quick Panel (Summary)

A Flyout window appearing near the capsule/tray, **380 × 540 DIP** (centered on the master-plan §3.2 recommendation, batch-3 supplement 27④):
Overview (counts by source) + **NEEDS ATTENTION / RUNNING three current-status sections** (Core `QuickPanelSections` decides:
Warning and above / stale data / broken connection = needs attention; Running/Queued without alerts = running; empty sections hidden) +
Recent Events (scrolling recent state changes, i.e. the master-plan's 「最近状态」) + the notification center (hydrated from notifications.json on disk, single click marks read).
Single-click a row to enter L3; right-click Pin to desktop lands in widgets.json. Activatable for keyboard support (ESC closes, Tab navigation);
**resident tier** (`config.keepQuickPanelOpen`, tray menu Keep Panel Open checked) does not auto-hide on focus loss; Esc or clicking the tray again still closes it explicitly.

### 6.5 L3 Detail Window (Summary)

Full single-object view: status, repo/branch, duration, failing stage, error summary + all Actions (Open / Retry / Cancel / Open Logs). Entered from an L2 event or an L0 tile click.

### 6.6 Cross-Layer Data Consistency

All four layers render the same `WidgetState` (broadcast on the Event Bus); no layer has its own pull logic — guaranteeing that capsule, tiles, panel and detail show exactly the same state and timestamps.

## 7. Refresh, Caching and Offline

### 7.1 Tiered Refresh (the one strategy table)

| Data category | Period | Notes |
|---|---|---|
| GitHub PR | 30–120s (default 60s) | ETag conditional requests |
| CI / Actions | 10–30s (default 15s) | Running takes the lower bound, Queued the upper |
| Machine | 15s (designed range 5–30s, config-overridable) | Implemented |
| Agent | 10s (designed range 5–15s, config-overridable) | Implemented |
| Workflow execution | Event-first / 10s follow-up | Short polling after trigger until terminal state |
| Static config | Manual | — |

- Jitter ±20% to avoid thundering herds; exponential backoff on failure (×2^n, capped at 10×), reset on success.
- Scheduling key = `(connectionId, tier)`; widgets on the same connection and tier are merged into one fetch, then fanned out.

### 7.2 Cache-First Startup

```text
Startup → read cache\{connectionId}\last-known-state → render immediately (mark fetchedAt)
     → background tiered refresh → state changes incrementally update the UI via the Event Bus
```

**Beacon never opens to a blank screen waiting on the network.**

### 7.3 Offline Mode

Losing the network / connection failure enters a degraded state: the last known state stays visible + an `Offline · Last update 10:31` banner (L1/L2) or a gray light (L0); when the network recovers it reconnects automatically and resets the backoff.

## 8. Notification Engine

Notifications are not bare Toasts — they are **rule evaluation**:

```jsonc
{
  "id": "rule-ci-failed",
  "widgetType": "github.actions.runs",   // null = matches all types; widgetId gives exact matching
  "severityAtLeast": "Error",            // pairs with optional severityAtMost to form a band (e.g. 「仅 Warning」, Warning only)
  "toast": true,
  "sound": true,
  "cooldown": "00:10:00"                 // serialized TimeSpan; null = remind only once per threshold crossing
}
```

- The rule engine's input = WidgetState change events; table-driven and unit-testable.
- Built-in default rules (effective with no configuration): CI Failed → Toast; running over 15 min → Warning.
- Dedup: no repeated alerts for the same widget at the same severity within the cooldown; a turn for the better may raise a Success notification.
- Notification records kept on a rolling basis (200 by default) + read/unread state; L2 Recent Events and the notification center share one source. **Landed (2026-10-09)**: `notifications.json` persists the rolling records and read state (hydrated at startup + saved whenever RecordsChanged; after a restart, widgets that were inside an alert period remind once more under threshold-crossing semantics — by design).
- Channels: Windows Toast (§10) + tray icon tinting + L0/L1 status lights.

## 9. Configuration, Secrets and Import/Export

### 9.1 Local Storage (Local First, %AppData%\Beacon\)

```text
%AppData%\Beacon\
├── config.json            # global settings (hotkey, startup, appearance, refresh overrides, notification rules)
├── connections.json       # connection metadata (credentialRef, no plaintext keys; enabled toggling)
├── widgets.json           # widget instances (incl. the pinned flag and the pinLayout convenience reference)
├── pins.json              # L0 tile layout (nested layout: anchor/offset/collapsed state + floating-frame position)
├── notifications.json     # notification records + read state (rolling, survives restarts, JsonNotificationRecordStore)
├── cache\{connId}\states.json  # last-known-state
├── secrets.bin            # DPAPI (CurrentUser) encrypted secret vault
└── logs\beacon-YYYYMMDD.log  # rolls daily (7 days kept) + crash-*.log crash logs; tokens always redacted
```

Write strategy: temp file + atomic replace + previous-version backup (protects against corrupted writes).

The appearance/motion section of config.json (palette and motion, §4.1/§6.2.8; implemented by B-706/B-707, surfaced on the B-805 settings page):

```json
{
  "appearance": {
    "severityColors": { "info": "#58a6ff", "success": "#3fb950", "warning": "#d29922",
                        "error": "#f85149", "critical": "#ff3b30", "offline": "#8b949e" },
    "motion": { "mode": "reduced", "intensity": 1.0 }
  }
}
```

### 9.2 Secret Storage

- Default implementation: a **DPAPI CurrentUser** encrypted file (`secrets.bin`); multiple keys with no length limit; roaming with the user profile is limited appropriately.
- Alternative implementation: Windows Credential Manager (the same `ISecretStore` interface).
- Config files only ever contain `credentialRef`; **plaintext tokens are strictly forbidden in any JSON** (same for import/export, see 9.3).
- Logs/exception/telemetry output is always redacted.

### 9.3 Import/Export

Export = a packaged JSON of config + connections + widgets + pins, **without secrets**; after import, each `credentialRef` prompts for re-entry.

## 10. Windows Platform Integration Points

| Capability | Approach | Notes |
|---|---|---|
| Packaging form | **Unpackaged** (self-contained exe) | Portable, writes %AppData% directly, fast dev iteration; MSIX can come in phase 2 |
| System Tray | Hand-rolled Win32 `Shell_NotifyIcon` (TrayIconService, no third-party tray library) | NOTIFYICONDATA + message-window callback; the icon is drawn at runtime with GDI+ in the configured color (B-706) |
| Toast notifications | Microsoft.Toolkit.Uwp.Notifications (`ToastNotificationManagerCompat`) | For unpackaged apps, Compat Initialize writes the HKCU\Software\Classes\AppUserModelId\<aumid> registry key (incl. CustomActivator) on first touch; rebuilt self-healing at next start if missing; toolkit 7.1.3 creates no .lnk (Show only calls CreateToastNotifier().Show); activation routes back to Beacon (deep link to L3) |
| Global hotkey | `RegisterHotKey` (default Ctrl+Alt+B) | Changeable in Settings; prompts to rebind on conflict |
| Autostart | HKCU `Run` key | Switch to StartupTask once MSIX |
| Topmost / no-activate | `AppWindow.IsAlwaysOnTop` + `WS_EX_NOACTIVATE` interop | See §16 risk 1 |
| DPI | PerMonitorV2 manifest | Host windows handle DPIChanged |
| Multi-monitor | Host panel form: one PinnedHostWindow per monitor; floating-frame form: one FloatingTileWindow per widget | Rebuild / recycle out-of-bounds tiles on display-topology changes |
| Single instance | Named Mutex | A second launch summons the existing instance |
| Crash fallback | Global exception → write log + tray balloon, **no flash-quit, no zombie windows** | |

## 11. Process and Threading Model

- Single process. The UI thread (DispatcherQueue) only renders and handles interaction; all network/command work stays on background threads.
- The Event Bus is thread-safe; UI subscribers are responsible for marshaling back to the UI thread.
- Action execution: standalone Task + timeout + cancellation; local commands capture output into the cache for L3 "Open Logs".
- Long-running actions (a build after workflow_dispatch) are not awaited inside the Beacon process — the UI is driven by state polling; Beacon does not block.

## 12. Error Handling

- Layered degradation: a network error affects only that Connection's widgets (they go Offline/yellow); **never crash Beacon or error out a whole window**.
- One unified user-facing message: "⚠ Unable to refresh · Last update 2 minutes ago · [Retry]" — always giving the last-known time and a retry entry.
- All exceptions go to the rolling log; GitHub 401/403 (token expired / rate-limited) gets a dedicated prompt (「Token 已失效，去设置更新」 — token expired, update it in Settings).

## 13. Security Considerations

- GitHub PAT least privilege: `repo` + `workflow`; admin scopes are never requested.
- Secrets only pass through `ISecretStore`; in-memory tokens never reach logs/crash dumps.
- local command Actions require **confirmation before execution** by default (can be disabled per Action); command contents are visible in plaintext in Settings.
- Export files contain no secrets (§9.3); uninstalling leaves no sensitive data behind beyond secrets.bin.

## 14. Extensibility Roadmap (Provider → Plugin)

- MVP: Providers organized as folders inside the assembly (`Beacon.Connections/GitHub|Rest`); the interface is the boundary.
- Phase 2: split Providers into standalone assemblies (`Beacon.Connections.Jenkins` etc.) loaded as .NET plugins (AssemblyLoadContext) + manifest registration; Plugins can contribute Connection/Widget/Action/Trigger. **【Frozen 2026-10-11】** (master-plan §2.3 verdict 「不做复杂插件平台」): keep the interface boundary clean; assembly splitting and plugin loading are revisited as needed.
- Agents / a central server also connect via Connection + Provider, **never hard-coded into Core**. **【Frozen 2026-10-11】** (master-plan §2.3 verdict 「不做 AI Agent 管理/远程机器管理/控制中心」).
- Phase 2/3 enhancement layer: **local AI support** (Ollama / OpenAI-compatible API) — "Ask Beacon" gathers CI results, logs and commit context for the model to analyze (present in the original plan, deferred to phase 2, not dropped); AI is only an enhancement layer and does not enter the Core architecture. **【Frozen 2026-10-11】** (master-plan §2.3 verdict: maintenance cost first).

## 15. Test Strategy

| Layer | Approach |
|---|---|
| Core (aggregation/scheduling/rule engine/backoff) | xUnit unit tests, table-driven; clock injection (to simulate backoff/cooldown) |
| Connections | Fake HttpMessageHandler + GitHub API response fixtures; ETag/rate-limit branch coverage |
| Storage | Config read/write round-trips, atomic writes, secret encrypt/decrypt (on Windows) |
| UI (four-layer interaction) | Manual acceptance checklist (attached to the Issues); WinUI cannot run on Linux CI |
| CI | GitHub Actions `windows-latest`: build + test (established at P0) |

## 16. Risks and Open Questions

| # | Risk | Mitigation |
|---|---|---|
| 1 | WinUI 3's Topmost + NoActivate + click-through combination needs Win32 interop; behavior must be verified | Isolated into a single `PinnedHostWindow` class; fallback: host the Composition content in a pure Win32 layered window |
| 2 | The unpackaged Toast AUMID shortcut fails to register in some environments | Self-check and repair the shortcut at startup; fallback degrades to a tray balloon + sound |
| 3 | GitHub rate limits (many repos, many widgets) | ETag mandatory + (connection,tier) merged polling + quota-aware backoff (§5/§7) |
| 4 | Monitor hotplug/RDP losing tiles | Anchor-corner restore strategy + out-of-bounds recycling (§6.2.3); display-topology-change-driven rebuild |
| 5 | Scope creep back into a "big Dashboard" | The non-goals list (§2.2) goes into every Issue's review baseline; anything outside the Issue list goes to phase 2 |
| 6 | ~~Open question: may the capsule and L0 sit on different monitors~~ **Decided: allowed** — the capsule is itself a resident tile, and pins.json stores anchors per monitor, so a capsule and L0 tiles spread across monitors comes for free | Each monitor's host restores independently on display-topology changes |
| 7 | ~~Desktop residents must not animate continuously (power drain)~~ **Decided (user effect confirmation): downgraded to a default** — 「我关心的状态悬浮桌面、颜色可配、最好带动效」: motion in three tiers full/reduced/off (default reduced) + adjustable intensity; colors overridable per level/per widget | The settings page (B-805) offers three tiers + intensity + the palette; animations pause when windows are invisible/collapsed; the L0 iron rules (no focus stealing / no sound / no popups) unchanged |
| 8 | ~~Tech stack: .NET 8~~ **Decided (user-named + verdict, 2026-10-07): upgrade to .NET 10 LTS** — .NET 8 hits EOL 2026-11 imminently, .NET 10 is the current LTS, the fleet standard pinned to the latest official stable | All TFMs `net10.0` / `net10.0-windows10.0.19041.0`; `global.json` pins `10.0.100` (rollForward latestFeature); CI setup-dotnet `10.0.x`; WindowsAppSDK/.NET 10 compatibility is verified by CI compilation — if incompatible, bump the version and record it back on this row |

## 17. Milestones and Downstream

| Milestone | Scope | Issues |
|---|---|---|
| M1 Resident | Engineering foundation + Shell (Tray/capsule/Hotkey/autostart) | P0–P1 |
| M2 Has a kernel | Config/Secrets/Event Bus/scheduling/aggregation/cache/offline | P2 |
| M3 GitHub loop closed | PR/Actions Provider + the four Action types | P3–P4 |
| M4 Can alert | Toast + notification engine + Quick Panel/Detail | P5–P6 |
| M5 = MVP | L0 pinned widgets + Settings + import/export + acceptance release | P7–P8 |

- Project/assembly layout and scaffolding commands: [solution project layout](../solution-structure.md)
- Executable task breakdown (43 Issues, with acceptance criteria): [MVP Issue list](../mvp-issues.md)
