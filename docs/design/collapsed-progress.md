
# Collapsed-State Progress Display Design (logo + progress ring)

> User design order, 2026-10-15. Reference: [jazzenchen/agent-hud-open](https://github.com/jazzenchen/agent-hud-open) (a resident macOS HUD, Swift/SwiftUI).
> This document fixes form and semantics only; it contains no implementation code.
> The 2026-10-15 supplementary order 「组件可选展示」 is merged into §10 of this document (same-day user directive; not spun off into a separate doc).

## 0. Conventions (locked by the user; unchanged in this doc)

In the collapsed state, the component is down to two things:

1. **The logo in the center**
2. **A ring of progress around the logo**

No full panel, no extra controls, no history text. It is **not** a scrolling log of text and **not** a miniature log panel — the logo body sits centered, with the progress ring as its outer band.

Ring visibility condition: **progress present → show the ring; no progress → no ring** (the collapsed state is down to the logo body alone). The ring is conditionally rendered, not a permanent decoration — when there is no quantifiable progress, no empty ring is drawn, nothing spins, and no placeholder arc is left behind.

---

## 1. Findings from the reference project (from reading the source, not imagination)

The reference project is a macOS menu-bar / Dynamic-Island style AI usage HUD. Its collapsed state is implemented by `Sources/AgentHUDDesktop/Notch/Simple/SimpleMeterView.swift`, geometry is computed by `SimpleMeterConfig` in `Sources/AgentHUDDesktop/Notch/Simple/SimpleMeter.swift`, and defaults live in `Sources/AgentHUDCore/Models/Settings/ScreenPlacement.swift`.

### 1.1 What draws the ring

```swift
// SimpleMeterView.swift:42-49
ZStack {
    Circle().strokeBorder(.white.opacity(0.14), lineWidth: config.ringWidth)   // 轨道
    Circle().inset(by: config.ringWidth / 2)
        .trim(from: 0, to: min(1, max(0, (item.usedPct ?? 0) / 100)))          // 进度弧
        .stroke(tint, style: StrokeStyle(lineWidth: config.ringWidth, lineCap: .round))
        .rotationEffect(.degrees(-90))                                        // 起点 12 点
}
```

Findings:

- **Pure vector arcs** — not a bitmap, not a mask, not a conic gradient. `trim(from:to:)` cuts a fraction of the circle's circumference = the progress value.
- **Track ring** (same color at ~20% opacity, `white.opacity(0.14)`) sits permanently beneath the progress arc, giving a visual reference for the "total".
- **`inset(by: ringWidth/2)`** is the key detail: the stroke's centerline is inset by half the stroke width, guaranteeing the whole stroke falls inside the ring's outer diameter — otherwise the arc would grow past the outer diameter.
- **`rotationEffect(-90°)`** moves the start point from 3 o'clock to 12 o'clock, growing clockwise.
- **`lineCap: .round`** caps the ends round.
- **Ring color = status severity color** (`StatusPalette`: ok `#3ddc84` / warning `#ffd23f` / critical `#ff453a` / idle `#9a9aa0`, `StatusPalette.swift:4-13`). The ring carries two channels at once: **length = progress, color = severity**.

### 1.2 How the ring and the center element coexist

Within the same `ZStack`, the ring and the center element **share one square frame**, with the center element inset by a fixed ratio:

```swift
// SimpleMeter.swift:106-108
var logo: CGFloat { (size * (showsRings ? 0.55 : 0.68)).rounded() }  // 中心元素边长
var mark: CGFloat { showsRings ? size : logo }                       // 含环的正方形边长
```

- Ring outer diameter = `size` (16…40 pt, default 22); center element = `size × 0.55` (when the ring shows) → **the center element is always 55% of the outer diameter**.
- The `ZStack` frame is `mark × mark`, and both the ring and the center element are centered on it — **the ring is the outer boundary, the center element insets proportionally**; the two never overlap.
- Font size `fontSize = max(9, size × 0.59)` and label spacing `labelSpacing = size × 0.23` — every length derives from the single quantity `size` (`SimpleMeter.swift:105-113` comment: 「Every length follows from `size`, so the one slider scales the whole panel」).
- The percentage number is a **separate column outside the ring**, never overlapping it (separated by `labelSpacing`), and can be turned off entirely (`showsNumbers`).

### 1.3 The reference project's progress semantics

`SimpleMeterItem.usedPct` = **the used share (0…100) of that agent's tightest quota window**, `nil` when there is no reading (`SimpleMeter.swift:5-9`). `items(rows:)` merges windows sharing a brand icon by `markKey`, taking the one with the **highest consumption** (`tightest[key]`, `SimpleMeter.swift:47-61`).

That is: the reference's ring is a **resource-consumption ring**, not a task-completion ring.

### 1.4 How the reference handles "no progress" (opposite of this repo's convention; see §2)

`trim(to: (usedPct ?? 0) / 100)` trims to length 0 when there is no reading — **but the track ring is still drawn**, i.e. "an empty ring". The reference has no "hidden ring" state: as long as `showsRings` is on, every marker carries a track ring.

### 1.5 The reference's two-state switching and motion

- Expand/collapse is driven by the pure state machine `HoverMachine`: `hoverDelayMs = 400` (hover 400 ms to open), `collapseDelayMs = 200` (leave 200 ms to collapse) (`AgentHUDCore/Models/Settings/Settings.swift:87-88`).
- The geometric transition `IslandAnimation`: **0.4 s, `timingCurve(0.2, 0.8, 0.2, 1)`** (strong ease-out); content enters offset 5 pt from the docked edge (`IslandAnimation.swift:6-28`).
- **The ring itself never moves**: no progress animation, no rotation. Motion belongs only to the geometric transition and the "working-state marker turns into a robot" effect.
- Accessibility: each cell gets `.accessibilityLabel("\(item.vendor) \(item.label)")` (`SimpleMeterView.swift:68`), and the container `.accessibilityElement(children: .combine)` (`:36`) — **the ring's equivalent is the text label; the ring itself carries no separate a11y value**.

---

## 2. Adaptation differences for this repo

| # | Dimension | Reference project | This repo (Beacon) | Handling |
|---|---|---|---|---|
| D1 | Ring persistence | Always draws the track ring; no reading = empty arc | **Conditional rendering**: `Progress == null` draws no ring, no track, no placeholder (user's convention) | Follow this repo's convention, **deviating from the reference** |
| D2 | Progress semantics | Used share of the tightest quota window | Isomorphic: `WidgetState.Progress` (0…1, used share) | Same source; see §5 |
| D3 | Center element | Full-color brand icon (bitmap/multicolor) | `BrandIcons` single-path solid `Path`, **monochrome and tintable** (`BrandIconFactory.TryCreate`, 16 DIP on the tile, `ThemeColors.Label()` + same-color stroke for weight) | Logo and ring can each take color independently → channel assignment in §4.4 |
| D4 | Text | Optional percentage column outside the ring | **No text** in the collapsed state (user's convention) | The equivalent moves to the accessibility label; see §8 |
| D5 | Collapsed form | Floating capsule/strip including a ring row | Dragged flush to the screen edge → currently a **12 DIP thin strip** (`PinnedHostWindow.CollapseStripDips = 12`) | **Largest structural change**: the collapsed form changes from a thin strip to a badge disc; see §3 |
| D6 | Motion convention | Fixed 0.4 s curve | `MotionEngine`: three tiers `off/reduced/full` + `Intensity` 0.5–2.0; progress transition 300 ms × intensity, 0 for the `off` tier (`MotionEngine.cs:35-53`) | The ring's appear/disappear/advance uniformly follow this repo's tiers; see §3.2 |
| D7 | Multiple markers | A row of independent rings | Isomorphic, but the merge rule uses this repo's own multi-window convention | See §6 |
| D8 | Progress style configuration | None | Already has `appearance.progressStyle`: `bar` (default) / `pool`; 2026-10-11 user directive 「圆池绝不做默认路径」 | The ring is a **projection of the collapsed state**, not a third tile style; see §5.3 |

---

## 3. Two-state switching

### 3.1 Triggers (reuse this repo's existing gestures; nothing new)

| Action | Current behavior (`src/Beacon.App/Windows/PinnedHostWindow.cs`) |
|---|---|
| Collapse | Drag flush against the screen edge (`edge <= max(2, 2 × dpi)`, `:976`) → `_layout.Collapsed = true` |
| Temporary expand | Cursor hovers the collapsed body (`_hoverExpanded`, `:1018`); collapses again on leave |
| Sticky expand | Click while collapsed (`_hoverPinned`, `:992`), until a drag re-evaluation or the toggle turns off |
| Master toggle | `app.edgeCollapseEnabled` (default on, `AppConfig.cs:35`); when off, clears residual collapsed state in `pins.json` (`:1059`) |
| Power saving | Pauses all animation loops while collapsed (`:452`) |

**This round changes only the form of the collapsed body; trigger semantics are untouched.** Hover-expand, click-to-pin, and drag re-evaluation stay as they are.

### 3.2 Size transition and animation

- **Anchoring unchanged**: the collapsed body and the expanded panel share the anchor edge (current convention: "strip edge coincides with panel edge", `:799-802`) — the badge's center falls inside the expanded panel's footprint, keeping the invariant "any hover position on the collapsed body lies inside the expanded panel" true.
- **Geometric transition**: panel ⇄ badge, width and height converging/enlarging along the anchor edge. Duration = **300 ms × Intensity**, `off` tier = 0 (instant set) — the same convention as the tile progress-bar transition (`MotionEngine.BarTransitionDuration`).
- **Ring appear/disappear**: within the same duration, animate `trim` from the current value to the target (appear: 0 → progress value; disappear: progress value → 0, then removal from the visual tree). Instant under the `off` tier. **No faking it with fades** — the arc-length animation and the panel-size animation share duration and curve, so it reads as one morph, not two actions.
- **Progress advance** (`Progress` changes while the ring is shown): only the arc length moves; no repaint, no jump.
- **The ring never spins on its own**: the collapsed-state ring is a **static** reading. A spinning animation carries no semantics (user's convention), and the reference likewise has no ring animation (§1.5).
- **Reference comparison**: the reference uses 0.4 s `timingCurve(0.2, 0.8, 0.2, 1)` + hover 400/200 ms. This repo's duration follows the `Intensity` tier, and the curve reuses this repo's existing ease-out convention; the two sets of numbers differ, but both are "strong ease-out, sub-second" and read as the same family.

### 3.3 Implementation impact (not implemented this round; logged)

`CollapseStripDips = 12`, the hit-test width (`:1139` uses `CollapseStripDips`), and `ApplyLayout`'s collapsed geometry (`:799`) all change with it; with multiple components, the collapsed body's length goes from "panel height" to "n × badge + spacing".

---

## 4. Collapsed-state layout and geometry

### 4.1 Dimension table (defaults + adjustable ranges)

| Quantity | Symbol | Default | Range | Rationale |
|---|---|---|---|---|
| Ring outer diameter | `D` | 32 DIP | 24–40 | Same family as the 32 DIP tile height (`PinnedHostWindow.TileHeightDips = 32`) and the RFC §6.2.2 capsule height of 32 DIP — the collapsed badge reads as the same family as the L0 tile and the L1 capsule |
| Ring stroke | `w` | 3 DIP | 2–4 | Reference range 1–4 (`ringWidthRange`); at D=32, 3 DIP stays legible on acrylic/translucent backgrounds |
| Logo drawing box | `L` | 18 DIP | ≥16 | `L = round(0.55 × D)` (reference ratio, §1.2); lower bound 16 DIP = this repo's existing tile icon size (`BrandIconFactory` call site `size: 16`) |
| Net gap | `g` | 4 DIP | ≥ w | `g = (D − L)/2 − w = (32−18)/2 − 3` |
| Badge footprint | — | 32 × 32 DIP | — | Hit-testing and layout |
| Badge spacing | — | 6 DIP | — | Same as the tile grid `ColumnSpacing = 6` (`PinnedHostWindow.cs:243`) |
| Ring start / direction | — | 12 o'clock / clockwise | — | Reference `rotationEffect(-90°)` (§1.1) |

### 4.2 The logo must not overlap the ring (hard rule)

- **`g ≥ w`**: the net gap is never smaller than the stroke width — at least one stroke width of empty space between the ring's inner edge and the logo's drawing box.
- **Measure by ink, not by drawing box**: Simple Icons' 24×24 viewbox carries built-in padding; a solid path's actual ink is about `0.8 L` (verified in `BrandIconFactory.cs:19-20`: after thin-stroke geometry scales down to 14 DIP, foreground coverage is only 7–22%). At `L = 18`, ink ≈ 14.4 DIP and the actual ink gap ≈ 5.8 DIP, wider than the nominal `g` — **layout computes on the drawing box, visuals judge by the ink**, safe at both ends.
- The stroke's centerline must inset by `w/2` (reference `inset(by: ringWidth/2)`), keeping the whole stroke within `D`.

### 4.3 Logo legibility

- `L ≥ 16 DIP` is the floor (this repo's existing tile icon size, and the empirically verified floor for "thin-stroke geometry stays readable").
- The logo keeps `BrandIconFactory`'s **same-color stroke weighting** (`strokeWidth > 0`); at collapsed-state sizes it goes grayish without the extra weight.
- The ring never covers the logo: guaranteed by the gap rule in §4.2 — the ring is the logo's **outer band**, not an overlay.

### 4.4 Color channel assignment (the ring and the logo never fight over the same pixel)

This repo's logo is a monochrome path (D3), so the two channels can be **assigned to two different elements**:

| State | Ring | Logo |
|---|---|---|
| Progress present | Ring color = **severity color** (§4.1 palette rule: Widget > severity > default, `WidgetConfig.ColorOverride` in effect); arc length = progress | **Label color** (`ThemeColors.Label()`), never competing with the severity color |
| No progress | Not drawn | The logo is **tinted with the severity color** — severity information is not lost, and no new element appears (still "logo body only") |

The result: **the severity color always has exactly one carrier in the collapsed state**; progress and severity never contend for the same pixels. The reference's logo is a full-color brand image, so its ring can only differentiate through its own color (§1.1); this repo is freer because its logo is tintable — a benefit that comes with D3.

---

## 5. Progress semantics definition

### 5.1 Definition

> **The ring shows "the quota-consumption share (used %) of the component's tightest window"**, value = `WidgetState.Progress` (0…1), **same source, same value** as the L0 tile's progress bar.

### 5.2 Rationale

1. **It is the only quantifiable 0…1 value in the model that providers supply** (`WidgetState.cs:13-14`: `/// 0..1 归一化进度（构建进度等），无进度为 null`). Every landed value provider is quota-type: `bigmodel` / `ark` / `opencode` / `kimi` / `sensenova` / `mimo` / `httpQuota`, uniformly conventioned as "used share of the worst window" (`BigModelUsageProvider.cs:96`, `HttpQuotaProvider.cs:74`, `MiMoUsageProvider.cs:520`, etc.).
2. **"Task completion" has no data source in this repo.** GitHub Actions explicitly sets `Progress = null` (`GitHubActionsProvider.cs:52`: 「GitHub API 无进度百分比 → UI 按 Lifecycle 显示不确定进度」), and Jenkins likewise (`JenkinsBuildProvider.cs:78`: 「无进度口径：正在构建按 Lifecycle 显示不确定进度」). **Inventing a completion value would be false information.**
3. **Same convention as the reference project** (§1.3: tightest-window used share), so readings compare across projects and its interpretation experience carries over.
4. **Same source as the L0 tile**: the user sees the same number in three places — the L0 floating widget, the L1 capsule, and the collapsed badge — so two conventions can never appear. Change `WidgetState.Progress` once, and all three agree in the same frame.

### 5.3 Relationship to the existing `progressStyle`

- `appearance.progressStyle` (`bar` default / `pool`) governs **the L0 tile's progress presentation**; the collapsed badge's ring is **the only viable projection of that same `Progress` in collapsed form** — the collapsed state has no horizontal bar to hold.
- Hence the ring is **not a third tile style** and does not change the existing `bar`/`pool` defaults (2026-10-11 user directive 「圆池绝不做默认路径」). The collapsed-state ring and the tile's bar coexist at the same value; neither replaces the other.

### 5.4 Deciding "progress present" (hard-coded)

| Case | Verdict | Behavior |
|---|---|---|
| Task with a known total, 0…100% | Progress present | Draw the ring; arc length = value |
| Quota-type reading (used share) | Progress present | Draw the ring |
| **Long task with unknown total** (a start but no denominator: CI building, Jenkins building) | **No progress** | **No ring**; when feedback is needed, use existing signals — severity color + the `Lifecycle`-driven pulse (this repo's existing indeterminate-progress convention), **never occupying the ring's semantics** |
| No task / no reading | No progress | No ring; the badge is logo only (tinted with the severity color) |

### 5.5 Known ambiguity and mitigation (recorded as-is)

The ring's semantics are **consumption** — **a full ring = quota exhausted = danger** — yet most users will read a full ring as "done". The reference project has the same ambiguity and mitigates it with color (§1.1: ring color = severity color). This repo reuses the same mitigation:

- A full ring is necessarily warning/critical colored; color and length move together (the fuller, the redder), so a "full ring yet green" combination never occurs;
- The accessibility label carries the percentage text (§8), so readings never hinge on guessing from geometry;
- If the user later asks to "show remaining", that is **the same value inverted** — added as an option, not changing this definition's value source.

---

## 6. Conventions for multiple components / queuing

**One badge per component (or per merged group), each showing its own progress; no cross-component aggregation.**

- **Arrangement**: laid out in order along the docked edge, 6 DIP spacing (same as the tile grid), order = the existing pin order in `pins.json`. With multiple components, the collapsed body's length = `n × 32 + (n−1) × 6` DIP.
- **Merging**: multiple windows of the same brand/connection merge into one badge (following the reference's `markKey` + `tightest` convention, §1.3), aligned with this repo's existing `WidgetValueHint.MultiWindow(state.Payload)` multi-window convention — one badge reports the window with the **highest consumption**.
- **Some have progress, some don't**: those with `Progress` draw the ring; those without show only the logo. Who has progress is visible at a glance — exactly the intent behind the user's convention.
- **Why no aggregation**: progress units of different components are incomparable (one is quota %, another is build %); averaging or extremizing is **unjustified arithmetic** that turns readings into decoration. This matches the user's requirement: "don't build something that looks like it's moving yet can't say what it is".
- **Queuing**: the queue itself is not progress — queued components are treated as "no progress" (no ring). If queue position ever needs expressing, that is a separate information channel and does not occupy the ring.

---

## 7. Idle state

| Case | Ring | Logo |
|---|---|---|
| No tasks at all / no readings | **Not drawn** (no empty ring, no track, no placeholder arc, no spinning) | Logo body only, tinted with the severity color (§4.4) |
| Component offline | Not drawn | Logo gray (Offline convention: `Offline 灰空心`, RFC §4.1) + reuse of the existing offline banner convention |

Difference from the reference project (D1): the reference always draws the track ring, so idle shows an empty ring; per the user's convention this repo **does not draw** — an empty ring in the collapsed state would be read as "task present but 0%", which misleads.

The collapsed state itself is the result of a user gesture (dragged to the screen edge), so the badge still exists while idle (the user parked it there) — it just **carries no ring**.

---

## 8. Accessibility

The collapsed state **has no text**, so the ring cannot be the only information channel. The equivalent goes through UIA:

1. **The badge must have a UIA peer.** Setting only `AutomationId` keeps it out of the UIA tree (verified in this repo's CI: a `Border` with only AutomationId stays out of the tree; `SetName` must be added to create a peer) — `AutomationProperties.Name` must be set (or an equivalent peer-creation means).
2. **Name = the text equivalent**:
   - With progress: `"{组件名} 已用 {n}%（{级别}）"` (e.g. `"GLM 已用 62%（警告）"`);
   - Without progress: `"{组件名}"` (no percentage, no "0%").
3. **`HelpText`** carries the details: window name + reset time / deep link (same source as the tile's tooltip convention).
4. **Expose the RangeValue pattern only when progress exists** (Value = n, Min = 0, Max = 100); **it must not be exposed without progress** — otherwise screen readers read the previous round's residual value, consistent with the user's requirement that "when there is no ring, no residual attribute like `aria-valuenow` may be left behind".
5. **Keyboard reachability**: the badge must be focusable; Enter/Space = expand. **Current limitation**: the host window holding the collapsed body is `WS_EX_NOACTIVATE` (RFC §6.2.3; clicks don't steal focus), so keyboard focus is limited — therefore **the L2 panel must provide an equivalent keyboard entry** (the component list inside the panel); keyboard users must not be left with mouse hover alone.
6. **Color blindness**: severity must not ride on color alone — the Name carries the severity word and the percentage (item 2); ring thickness carries no information (only arc length and color carry information, and arc length has its own text equivalent).

---

## 9. Points pending decision

| # | Question | Recommendation |
|---|---|---|
| P1 | Is the `D` default of 32 DIP right (vs. the reference's 22)? | Recommend 32: same family as tile height / capsule height; if it feels large, extend the range down to 24 |
| P2 | Should the multi-component collapsed body be allowed to wrap/stack (the along-edge run gets very long with many components)? | Recommend single row/column first, truncating by available edge length when exceeded (reusing the tile overflow-recycle convention); judge the look after the on-machine walkthrough |
| P3 | Does tinting the logo with severity color when there is no progress (§4.4) conflict with "the logo keeps brand recognition"? | Recommend keeping it: the logo is a monochrome path, and severity tinting doesn't harm its shape; if the user requires the logo to always be label-colored, add a 4 DIP severity dot (corner marker) for the no-progress case |
| P4 | Should the ring's color be user-overridable? | Recommend reusing `WidgetConfig.ColorOverride` (Widget > severity > default); no new toggle |

---

## 10. Optional component display (2026-10-15 supplementary order, merged into this doc)

> User directive: 「beacon 能选择展示的组件最好」 ("it would be best if beacon could choose which components to show"). This section covers the three-layer taxonomy, the component inventory, default configuration, collapsed-state linkage, and persistence.

### 10.1 How the reference project organizes optional components (conclusion first)

Reading through the `agent-hud-open` source (`Sources/AgentHUDCore` + `AgentHUDDesktop`): its optional components are **not one mechanism but three layers**, each with exactly one mechanism:

| Layer | Contents | Sole mechanism |
|---|---|---|
| **Built-in resident** | The island window itself, the hover panel footer, the alert system, the stats window, the settings window, the onboarding page, bots bodies | **No switches** — only data or mode changes their content; no configuration can remove them |
| **Preference toggles** | The hover panel's three zones (`showIslandQuota/Tokens/Sessions`), the reset countdown (`showResetCountdown`), the menu-bar icon (`showMenuBarIcon`), the bot avatar (`botLogos`); the collapsed meter's rings/numbers/bot (`showsRings/showsNumbers/simpleBots`, per-screen `ScreenPlacement`) | Flat booleans centralized in `Settings` (`Settings.swift:42`, 「every field has a default so older stored JSON still decodes」); form toggles live in per-screen `ScreenPlacement`; the view side uniformly applies `if toggle && !data empty` (`HoverPanelView.swift:101/111/119`, three verified instances) |
| **Data-driven rows** | One row per agent/quota window | `AgentDescriptor.enabled` as the per-row toggle (`agents.v5` ordered-array persistence), provider-discovery merging (`SettingsStore.mergeDiscovered`); a new vendor's default visibility is set by `VendorCatalog.startsHidden` — **all shown by default, the sole exception DeepSeek** (the API balance row stays out of the HUD by default, `:48`); account-level hiding is a hybrid mechanism (`hiddenAccountIDs` forces all of that account's rows off, `SettingsStore.swift:220-222`) |

Persistence lives in exactly one place: **three UserDefaults JSON keys** (`settings.v1` / `agents.v5` / `onboarding.completed.v1`, `SettingsStore.swift:21-25`), re-encoded and flushed on every change; data (the SQLite ledger, caches) lives in Application Support, separate from config. The onboarding page **asks no component choices at all** (「no choices」, `OnboardingView.swift:5`); every choice lives in the settings window. The only "memory-only" toggle is the runtime glow hiding (`UsageStore.glowHidden`), deliberately not persisted.

Three transferable conclusions for Beacon:

1. **Toggles follow "content", not "windows"** — panel zones, meter parts, and per-row agents each use their own toggle layer; there is no master switch.
2. **The "preference ∧ has-data" double gate** is the whole secret of its panel never idling: off = one fewer zone; empty data = likewise one fewer zone; both cases render identically, so "toggle on but the zone sits blank" never exists.
3. **Defaults = the status quo**: preference toggles all default to true (the DeepSeek row is the data-layer exception); "off" is an escape hatch for people bothered by a particular part, not the initial state.

### 10.2 This repo's three-layer component taxonomy and inventory

| Layer | Components | Notes |
|---|---|---|
| **Resident (no switch)** | L1 status capsule, tray icon + tray menu, L2 panel skeleton and open/close entry, settings window, collapsed host (gesture target) | Remove any one and the app loses an operating entry point, so none is optional (the reference's same layer, same reasoning) |
| **Preference toggles (new in this order)** | tile sub-parts: progress bar, percentage readout, status light, multi-window detail; the collapsed badge's progress ring (`components.collapsedRing`); the three L2 zones: RECENT EVENTS / 需要关注 / 正在运行; Toast notifications | Mechanism: a new `components` section in `config.json` (same level as `appearance`); existing toggle precedents: `app.edgeCollapseEnabled` (default on, `AppConfig.cs:35`), `appearance.progressStyle` (bar default / pool optional; 2026-10-11 user directive 「圆池绝不做默认路径」), `uiOpacity` same value on all four sides (批3-补10), three motion tiers |
| **Data-driven rows (existing, untouched)** | widget instances themselves (`widgets.json`) → floating tiles (dual pin entries), panel rows, and collapsed-badge generation; notification rules (`notifications.json`) | Isomorphic to the reference's `agents.v5` + `enabled`: rows are user-created, row-level visibility already exists (pin/unpin); no new mechanism needed |

The L0 tile's `bar|pool` is a **choice between styles, not between presence and absence**, so the progress-bar toggle's semantics = "don't draw the bar" (with it off, the readout/light remain); multi-window detail is already conditionally rendered (only shown at ≥2 windows, `WidgetValueHint.MultiWindow`), and the toggle merely adds another preference gate.

### 10.3 Default configuration (recommendation: everything = current behavior, all on)

| Toggle | Default | Rationale |
|---|---|---|
| tile progress bar / readout / status light / multi-window detail | On | Zero drift from shipped behavior; upgrading users see no sudden change |
| `components.collapsedRing` | On | The ring is already conditional rendering (no progress, no ring — §0/§7); on by default produces no permanent element |
| L2 three zones | On | All three zones are conditional rendering (empty zones not rendered is the existing convention); off = one fewer zone, empty = likewise one fewer zone |
| Toast notifications | On (per existing notification rules) | rulesProvider and the rule engine already exist; no separate master switch |

**Why all-on defaults are not noise**: noise comes from "permanent elements without semantics", and every part above is conditionally rendered — no progress, no bar/ring; no data, no zone; no rows, no badge. What is actually default-off is the `full` motion tier (current: `reduced`) and the `pool` style (current: `bar`); both were settled by existing directives and are not restated in this order. The reference's comparable preference toggles (`showsRings/showsNumbers/botLogos`) also all default to true; the only default-hidden DeepSeek is a data-driven row's `startsHidden`, not a preference toggle. If the on-machine walkthrough proves multi-window detail is too noisy on narrow tiles, lower the `tileMultiWindow` default then (see 10.6 P8); don't pre-decide.

### 10.4 Collapsed-state linkage rules (the direct answer to user directive #3)

The collapsed badge is not a thumbnail of the tile; it is an independent form — **the collapsed state consumes exactly two inputs: widget existence (data layer) and `collapsedRing` (preference layer)**. L0 sub-part toggles (light/readout/multi-window) do not spill into the collapsed state, which never had those slots to begin with:

| Component visibility state | Collapsed body behavior |
|---|---|
| Widget pinned normally | Badge = logo + ring (ring drawn when `Progress != null` and `collapsedRing` is on) |
| Widget unpinned / deleted (data layer off) | **No badge**, no empty slot left (badges are generated with widgets; no widget, no badge) |
| `components.collapsedRing` off (preference layer closes the progress channel) | Every badge is **logo only**, tinted with the severity color — i.e. the §4.4 "no progress" row's form reused as-is (the user's example: turn off the progress component and the collapsed state is logo-only — exactly this row) |
| All pins empty | **Collapse disabled**: dragging to the edge doesn't collapse, and an already-collapsed body pops back immediately (same semantics as `app.edgeCollapseEnabled=false`) — an empty collapsed body is an invisible hit area, an interaction black hole; the panel stays expanded |
| L2 zone toggles off | The panel has one fewer zone with no placeholder (rendered the same as empty data); the collapsed state is unaffected (panel zones and the collapsed body are decoupled) |

Severity-color channel self-check: with `collapsedRing` off, the sole carrier of severity color on the badge = the tinted logo (§4.4, right column), so the channel isn't lost; the tile status-light toggle affects only the tile itself, and severity color has an independent carrier on the capsule / tray / collapsed badge — closing any single point never opens a window where "severity color goes fully dark".

### 10.5 Persistence and restore (user directive #5)

- **The single source of truth = the new `components` section in `config.json`** (same level as `appearance`/motion); atomic writes reuse the existing settings flush chain (SaveGeneral/SaveApp → SettingsApplied → flush); the settings page's toggles are just an editor — isomorphic to the reference's "UserDefaults as the single source of truth, the settings window merely an editor".
- **Missing fields fall back to defaults**: an old config without a `components` section → per-key defaults (the same principle as `Settings.swift:42`: every field carries a default value, so old JSON always decodes); zero-migration upgrades.
- **No loss across restarts** = satisfied by the config.json flush alone; no extra mechanism.
- **Import/export carries it automatically**: config.json is already an export-package member (B-802); component choices migrate with the package and restore after import, with no new logic.
- The settings page lands on the 「外观」 page (same family as opacity/motion/progress style); strings go into the `UiStrings` bilingual dictionary (批3-补53 mechanism).
- No "memory-only" temporary-hide toggles (the reference's glowHidden is the sole exception and is deliberately unpersisted; this repo has no such need — not introduced).

### 10.6 New points pending decision

| # | Question | Recommendation |
|---|---|---|
| P5 | Should collapse be disabled when all pins are empty (10.4, row 4)? | Recommend disabling collapse (panel stays expanded) — an empty collapsed body is invisible and can't be hit-tested, violating the spirit of the §3.2 invariant "any hover position on the collapsed body lies inside the expanded panel" |
| P6 | Granularity of the L2 zone toggles | Recommend one per zone (RECENT / 需要关注 / 正在运行), no whole-group master switch — the reference's same layer is exactly per-zone booleans |
| P7 | Should the tile's "progress bar + readout" merge into one toggle? | Recommend two separate toggles: bar off + readout on (numbers-only form) and bar on + readout off (bar-only form) are both legitimate end states; merging would foreclose one of them |
| P8 | `tileMultiWindow` default | Recommend on for now (conditional rendering already shows it only at ≥2 windows); if the on-machine walkthrough finds it noisy on narrow tiles, lower the default then — don't change the mechanism |
