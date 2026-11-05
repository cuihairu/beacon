# Display Modes Analysis: Capsule Panel / Floating Island / Taskbar (+ extended research)

> Status: draft. Analysis only — no implementation in this change. The decision this doc records as settled is the interaction model (§1), given verbatim by the user; mode recommendations in §7 are agent decisions under the standing authorization (代决，效果后审).
> Evidence basis: real code in this repo, cited `file:line` as of commit a498ba7. Windows-OS-side facts are marked as such and not presented as repo facts.
> Chinese counterpart: [docs/zh/design/display-modes-analysis.md](/zh/design/display-modes-analysis) (content-identical).

## 0. Question and method

Beacon needs to decide how its aggregate status reaches the user's eye. Three candidate forms were asked about — ① floating panel, ② floating island (Dynamic-Island-style top capsule), ③ taskbar — and the scope was later extended with six more surfaces (§5). Each mode is analyzed in the same four steps: **pick the surface form → pick the display components → wire the data → interaction**, plus feasibility (WinUI 3 / Win32 API level), effort (S/M/L) and risk.

Method note: every claim about this repo carries a `file:line` citation; claims about Windows itself (API existence, OS behavior) are marked OS-side and are the only uncited ones. Where a mode conflicts with what Windows actually exposes to third parties, this doc says so instead of designing around a fantasy surface.

## 1. Interaction model (user decision; binds every mode below)

User order, quoted: **所有形态只展示状态（只读），不做形态内操作；点击任一形态 → 唤起主面板/看板，面板内操作一律弹二次确认。** (All surfaces display status read-only, no in-surface operations; clicking any surface invokes the main panel/board; every in-panel operation pops a confirmation.)

Consequences of this contract, audited against the current tree:

- **Already compliant**
  - Capsule click → Quick Panel: `src/Beacon.App/Windows/CapsuleWindow.cs:49-50` (event), wired at `src/Beacon.App/App.xaml.cs:197` (`quickPanel.Toggle`). Hover preview is pure display (`CapsuleWindow.cs:156-227`).
  - Tray left-click → panel: `src/Beacon.App/Services/TrayIconService.cs:238-241`.
  - Toast click = deep-link navigation, not an in-surface action: `src/Beacon.App/Services/ToastNotificationService.cs:9-10` (`ToastActivation` payload → widget L3).
  - Confirmation gate for actions already exists at the execution layer: `src/Beacon.Core/Services/ActionRunner.cs:69-76` (`RequireConfirmation` → injected `ConfirmationHandler`; without a channel it conservatively refuses, `:10`).
- **Needs revision to comply**
  - Tray context menu currently carries operations (Notifications / Refresh All / Components, `TrayIconService.cs:275-277`). Under the contract the menu shrinks to navigation + Exit (`Exit` stays — it is the sole quit path, `TrayIconService.cs:11`).
  - Panel-side actions must all route through `ActionRunner` so the confirm gate is single-sourced; any XAML-level direct execution path would bypass the contract (audit item, not new code).
- **Cost effect**: the contract *removes* per-surface menus/actions from scope for every new mode in this doc, so mode efforts below exclude interaction menus and count only one shared confirm UX in the panel.

## 2. Mode ① Floating panel (悬浮框)

**Verdict: already implemented. This mode is the repo's current center of gravity.**

- **Feasibility**: resident borderless always-on-top acrylic window is shipped — `CapsuleWindow.cs:62` (`DesktopAcrylicBackdrop`), `:79-89` (`OverlappedPresenter.IsAlwaysOnTop`, `SetBorderAndTitleBar(false,false)`, `IsShownInSwitchers=false`, `WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`). The expandable board is a second window with flyout semantics — `QuickPanelWindow.cs:24-31` (deactivate-to-hide, ESC close `:116-118`, fixed 380×540 `:34-35`), zero-network open via event subscription + cache hydration (`:67-71`, `:74-80`). A third variant — one floating window per pinned widget — also ships (`src/Beacon.App/Windows/FloatingTileWindow.cs:18-25`, `app.pinDisplayMode = "floating"`).
- **Display components**: status dot + health ring + severity-colored counts + offline banner (`CapsuleWindow.cs:428-472`, ring via `src/Beacon.App/Components/StatusRing.cs:8-11`); per-source preview rows on hover (`CapsuleWindow.cs:55-56`, `:193-227`).
- **Data wiring** (all existing, no new services):
  - Aggregate: `StatusAggregator` → `AggregateStatusChanged` (`src/Beacon.Core/Services/StatusAggregator.cs:31` snapshot; `src/Beacon.Core/Events/Events.cs:14`), consumed at `App.xaml.cs:208`.
  - Per-widget: `WidgetStateChanged` (`Events.cs:6`) over `WidgetState` (`src/Beacon.Core/Models/WidgetState.cs:9-21`: Severity, Summary, Progress, Payload, FetchedAt, IsStale, ConnectionHealthy), grouped per source at `App.xaml.cs:487-494`.
- **Interaction under §1**: click → board (done); hover preview (done, read-only); drag reposition is window management, not a status operation — it stays (`CapsuleWindow.cs:141-277`, per-monitor anchor persistence `:332-395`).
- **Effort**: S (residual polish only). **Risk**: low — the historically hard pitfalls are already solved and documented in-code (DPI physical-pixel math `:293-301`, non-client chrome gap `:303-312`, drag-vs-DPI-change interplay `:110-127`).

## 3. Mode ② Floating island (悬浮岛, Dynamic-Island-style top capsule)

**Verdict: feasible as an evolution of CapsuleWindow, not a new window class. Effort M.**

- **Feasibility (WinUI 3 / Win32)**:
  - Window chassis: identical recipe to the capsule — topless always-on-top non-activating `OverlappedPresenter` (`CapsuleWindow.cs:79-89` pattern is proven, including focus-free hover).
  - Top-center anchoring: the per-monitor anchor + offset persistence (`PinLayout`, `CapsuleWindow.cs:332-395` via `src/Beacon.App/Infrastructure/MonitorService.cs` work areas) already supports any corner; a top-center anchor is a new anchor value, not new machinery.
  - Capsule morph (collapsed ↔ expanded width/height): the repo already resizes to measured content and re-pins to the anchor — `MeasureSizePx`/`ApplySize` (`CapsuleWindow.cs:293-330`); the expansion can animate via `MotionEngine` (`CapsuleWindow.cs:94`, `:450-460` show Slide-in/Flash/transition hooks with an off tier). Morph fires only on state change/hover — low frequency, so per-frame `AppWindow.Resize` cost is acceptable.
  - Rounded look: XAML `Border.CornerRadius` over `DesktopAcrylicBackdrop` (`CapsuleWindow.cs:62`) is the proven substrate. OS-side honesty: a WinUI 3 `Window` is rectangular; true per-pixel alpha is not offered, so the corner pixels carry acrylic. `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE)` (OS-side, Win11) can round the window frame itself; either treatment looks like an island — pick during implementation.
  - Fullscreen apps: a TOPMOST-band window floats over most fullscreen windows; exclusive-fullscreen games can cover it. Acceptable for a status pill; same behavior class as today's capsule.
- **Display components**: one-line collapsed state (`「2 ok · 1 warn」` counts + severity dot — the exact aggregate already rendered at `CapsuleWindow.cs:430-448`) + optional unread-notification badge (count from `NotificationEngine.Records`, `src/Beacon.Core/Services/NotificationEngine.cs:50`, rolling cap 200 `:17`); expanded state = the hover preview rows generalized (`:193-227`).
- **Data wiring**: same three sources as mode ① (`AggregateStatusChanged`, `WidgetStateChanged`, `NotificationEngine`); no new providers.
- **Interaction under §1**: hover expands (read-only), collapse on leave, click → Quick Panel. No in-island menus, no in-island actions.
- **Effort**: M (new anchor value + morph animation + badge; chassis/data reused). **Risk**: medium-low — morph + anchor interplay is the only genuinely new geometry code; everything else is a re-run of proven patterns.

## 4. Mode ③ Taskbar

**Verdict: tray icon is already the taskbar mode and it is done; the ITaskbarList3 overlays are real API but a poor fit; third-party taskbar widgets are not open. Do not build (a)/(b) beyond what ships.**

- **(a) Notification-area icon — shipped**: `Shell_NotifyIcon` self-implemented, no third-party tray library (`TrayIconService.cs:10-12`, NIM_ADD `:84`); severity-colored icon runtime-drawn from the logo mask with per-color HICON cache (`:117-162`, `:168-195`, palette-driven B-706); tooltip carries the same aggregate counts the capsule shows (`:206-216`, string built at `App.xaml.cs:480-482`); Explorer-restart recovery via the `TaskbarCreated` broadcast (`:90-91`, `:252-259`); balloon fallback for the pre-Toast path (`:95-115`).
- **(b) ITaskbarList3** (overlay icon / progress in button / thumbnail toolbar — OS-side API, documented): **requires a window that actually owns a taskbar button.** Beacon deliberately has none — every window is excluded from taskbar and Alt-Tab (`CapsuleWindow.cs:87-89`, `QuickPanelWindow.cs:100-101`, `FloatingTileWindow.cs:409` tool-window styles), and the repo contains zero `ITaskbarList3` usage (grep `src/` — empty). Adopting it means keeping a dedicated taskbar-button window purely to host an overlay that duplicates what the tray icon already shows. Honest cost/benefit: architecture churn for redundant signal. Not recommended.
- **(c) True taskbar widgets**: Windows reserves the taskbar surface for OS content; there is no public third-party API to pin a live widget onto the taskbar itself (OS-side fact). The sanctioned third-party surface in this space is the Widgets Board — analyzed in §5.4. Any plan that assumes "put a live tile in the taskbar" is designing against a surface Windows does not sell.
- **Data wiring**: already wired — `tray.SetSeverity(status.Overall)` on every aggregate change (`App.xaml.cs:208-209`).
- **Interaction under §1**: left-click → panel (done); menu shrinks per §1.
- **Effort**: S already sunk; (b) would be M with negative payoff. **Risk**: tray-only apps live or die with the notification area's per-app "hidden icon" state (OS-side user setting) — this is why the tray is a *fallback* in the recommendation, never the primary signal.

## 5. Extended research: six more candidate surfaces

### 5.1 Wallpaper integration

- **Feasibility**: two routes. (i) Static swap: `SystemParametersInfoW(SPI_SETDESKWALLPAPER)` or the per-monitor `IDesktopWallpaper` COM interface (OS-side, Win8+): render a bitmap with status baked in, write to disk, set. Every refresh = full-image encode + disk write + shell reload — visible flash, I/O and CPU per update. For per-widget changes arriving at refresh-tier cadence (`src/Beacon.Core/Models/RefreshTiers.cs`) this thrashes. (ii) Live render: the Wallpaper-Engine route injects a window behind desktop icons via the undocumented Progman/WorkerW `0x052C` message trick (OS-side, unsupported). It breaks on Explorer restarts — a fragility class the repo already fights for the tray (`TrayIconService.cs:90-91`) — and draws full-screen composition cost continuously.
- **Components**: none exist; would need a bitmap compositor and a raster layout for widget cards.
- **Data wiring**: aggregate + per-source states — trivial to obtain, irrelevant given the surface cost.
- **Interaction under §1**: a wallpaper cannot be clicked into the panel without an invisible full-screen hit-test window — extra machinery for one click.
- **Verdict**: technically possible, productively wrong: interference **high** (the whole desktop repaints for one badge), performance **high**, risk **high**, legibility hostage to the user's wallpaper. **Not recommended.**

### 5.2 Tray tooltip / balloon

- **Feasibility**: shipped. Tooltip `NIF_TIP` (`TrayIconService.cs:206-216`), balloon `NIF_INFO` (`:95-115`). OS-side limits: tooltip format/duration is OS-controlled, single-line; since Windows 10 the shell renders tray balloons as Toasts, so this surface collapses into §5.3 at runtime.
- **Components / data**: none new — counts string already composed (`App.xaml.cs:480-482`).
- **Interaction under §1**: read-only ✓.
- **Verdict**: keep as the Toast-unready fallback exactly as coded (RFC §16 risk 2, cited in-code at `TrayIconService.cs:94`). Effort S, risk low, interference low (transient). Already done.

### 5.3 Windows Toast notifications (event push)

- **Feasibility**: shipped, including the hard part — unpackaged activation. `ToastNotificationManagerCompat` writes the AUMID registry key on first touch with self-heal (`ToastNotificationService.cs:14-20`, `:38-39`, `:74`); click activation deep-links to the widget detail (`:9`); delivery is rule-gated with dedup/cooldown semantics upstream (`NotificationEngine.cs:11-14`).
- **Components / data**: `NotificationRecord` → toast text; no new components.
- **Interaction under §1**: compliant — toasts are display + navigation only; no in-toast action buttons ship today, and none may per the contract.
- **Verdict**: keep. This is the *push* channel; it complements (never replaces) a glanceable constant surface. Effort S (shipped), risk low (AUMID self-heal already battle-tested, CI scenario F5t), interference medium (intrusive by design — hence rule-gated).

### 5.4 Windows 11 Widgets Board (third-party)

- **Openness status (OS-side)**: third-party widget providers are a documented, shipped platform: implement the `IWidgetProvider` COM interface, author content as Adaptive Cards JSON (Windows App SDK 1.2+; the widget appears in the Widgets Board, opened via Win+W or the taskbar icon). Requires packaged identity — the repo already carries `packaging/msix/`, so the prerequisite exists. The content surface is Adaptive Cards, **not** XAML — none of `src/Beacon.App/Components/` is reusable; it is a second rendering path.
- **`ICardsWidget234`**: an undocumented COM interface observed in Windows Insider builds of Microsoft's in-flight redesigned widgets/"cards" experience. It is closed-source upstream and not publicly documented; third parties cannot target it. Treat its existence as a *flux signal* — the board's contract may churn — not as an API to adopt.
- **Components**: new provider host + Adaptive Card templates per size; updates pushed on `WidgetStateChanged`.
- **Data wiring**: straightforward (same `WidgetState` stream serialized into card JSON).
- **Interaction under §1**: ship the card read-only (strip `execute` actions); card click → deep link into the panel — supported by the activation model.
- **Verdict**: feasible but not now. Interference low (board is user-opened, never self-popping — so it is a *deliberate* check surface, not a glanceable one), cost M–L (second rendering path), risk medium-high (platform churn; `ICardsWidget234` evidence the ground is moving). **Observe; revisit if the board stabilizes.**

### 5.5 Lock screen

- **Feasibility**: **not feasible — stated as-is.** Third parties cannot replace or host UI on the Windows lock screen (OS-side fact). The legacy Win8-era lock-screen app API (badge/text via `LockScreenManager`, background tasks) is a deprecated path that effectively does not accept modern desktop apps on Windows 11. No design work is warranted here.

### 5.6 Second screen / independent small window

- **Feasibility**: **already shipped.** The floating mode gives each pinned widget its own always-on-top, draggable, per-widget-position window (`FloatingTileWindow.cs:18-25`, positions in `pins.json`), and multi-monitor placement/monitor naming is handled by `MonitorService` + `PinLayout` (same anchor math as `CapsuleWindow.cs:332-395`). Putting tiles on a second screen = drag them there. Additionally, `config.keepQuickPanelOpen` already turns the board into a resident window (`QuickPanelWindow.cs:26`) — a second-screen "mini dashboard" is that switch plus placement.
- **Verdict**: keep as-is; effort S residual polish (e.g. remember per-monitor keep-open placement). Interference low (screen is dedicated), risk low.

## 6. Comparison table (all modes)

| Mode | Visibility | Info density | Interference | Cost | Risk | §1 contract | Verdict |
|---|---|---|---|---|---|---|---|
| ① Floating panel (capsule + board) | High (resident) | High (expandable board) | Low (small, corner) | S (shipped) | Low (known pitfalls solved) | Compliant | **Keep — primary** |
| ② Floating island | High (resident, top-center) | Medium (1 line → preview) | Medium (eye-catching position) | M | Med-low (morph geometry new) | Compliant by design | **Recommended evolution** |
| ③a Tray icon + tooltip | Medium (may be hidden by OS) | Low (icon + 1 line) | Low | S (shipped) | Low (icon hiding is OS-controlled) | Compliant | **Keep — fallback** |
| ③b ITaskbarList3 overlay | Medium | Low | Low | M | Medium (needs taskbar-button window → architecture change) | Compliant | **Reject** |
| ③c True taskbar widget | — | — | — | — | — | — | **Not possible** (surface not open) |
| 5.1 Wallpaper | Very high | Low | **Very high** (full-desktop repaints) | M | High (unsupported injection route) | Poor (cannot be clicked cleanly) | **Reject** |
| 5.2 Tray tooltip/balloon | Medium | Very low | Low (transient) | S (shipped) | Low | Compliant | Keep as fallback channel |
| 5.3 Toast push | Medium (momentary) | Medium (1 event) | Medium (intrusive) | S (shipped) | Low (AUMID self-heal tested) | Compliant | **Keep — event channel** |
| 5.4 Widgets Board | Low (user-opened) | Medium | Low | M–L (second rendering path) | Med-high (platform churn, `ICardsWidget234` flux) | Compliant (read-only card) | Observe |
| 5.5 Lock screen | — | — | — | — | — | — | **Not possible** |
| 5.6 Second-screen small windows | High (dedicated screen) | High (many tiles) | Low | S (shipped) | Low | Compliant | Keep as-is |

## 7. Recommendation (revised under the §1 interaction model)

**Recommended combination: resident capsule (evolving toward the island form) + click → Quick Panel board + tray severity icon as fallback + Toast for threshold events.** Concretely:

1. **Primary signal = the capsule** (`CapsuleWindow`), evolved in the island direction (top-center anchor option, morph expansion, unread badge). It is the only surface that is simultaneously resident, glanceable, low-interference, and already 80% built.
2. **Depth = the board** (`QuickPanelWindow`), opened by clicking *any* mode — the single interaction the contract mandates.
3. **Fallback = tray icon** (severity color + counts tooltip): survives when the capsule is disabled (`showCapsule=false`, `CapsuleWindow.cs:130-134`) or the monitor is blind-sided; degraded only when the user hides tray icons (OS-side setting).
4. **Push = Toast**: threshold-crossing events with dedup/cooldown (`NotificationEngine`), never a constant-state substitute.
5. **Rejected**: wallpaper (§5.1), ITaskbarList3 overlay (§4b), lock screen (§5.5, impossible). **On watch**: Widgets Board (§5.4), revisited only after its platform stops churning.

**Phasing** (each phase independently shippable; all read-only per §1):

- **Phase 1 (S) — contract sweep**: shrink the tray menu to navigation + Exit; audit that every panel action routes through `ActionRunner`'s confirm gate (`ActionRunner.cs:69-76`); no new surfaces.
- **Phase 2 (M) — island-ize the capsule**: top-center anchor value, hover/expand morph via `MotionEngine`, unread-notification badge from `NotificationEngine.Records`.
- **Phase 3 (optional) — second-screen polish**: keep-open board placement memory. **Watchlist (no schedule)**: Widgets Board provider spike behind `packaging/msix`.

Decision records: §1 is the user's order (quoted verbatim). The mode verdicts and phasing above are agent decisions under the standing authorization (代决，效果后审) — challenge any of them and this doc gets the counter-verdict.

## 8. Next step

**The user picks the mode(s) first; only then does an implementation plan get written.** This document deliberately stops at feasibility + recommendation. The natural pick from §7 is Phase 1 → Phase 2; a one-line user order («按 Phase 1/2 走» or a different combination) unlocks the implementation plan as the next document.
