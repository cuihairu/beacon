
# Beacon mission brief (distilled from the original plan; the original text is in mission-excerpt.md)

## Positioning

**Beacon = a developer status and action center resident on the Windows desktop (Developer Status & Action Center)**, not a Web Dashboard.
Tagline: See what needs your attention. / Your development signal on Windows.
Core: **Signal + Action** — it watches for you instead of making you go look (It watches for you).

## Core product model (7 concepts)

- **Widget**: the basic unit of information and interaction (GitHub PR Widget, CI Status Widget…)
- **Provider**: the data source (GitHub first; Jenkins/GitLab/Agent etc. in phase 2) — **Provider First, never hard-wired into Core**; credentials are configured once centrally instead of re-entering a token per widget
- **Action**: an operation that can be executed immediately (Run/Cancel/Retry/Open/Notify); the MVP does only simple actions (for example `workflow_dispatch`)
- **Workflow**: an advanced capability; the MVP does not build a complex engine
- **Notification**: proactive alerts (from CI / machine / workflow / widget thresholds), delivered through Windows Notifications
- The remaining concepts are completed per the original text (Machine/Run etc.)

## Three interaction levels (progressive disclosure — no big panel up front)

1. **Status Capsule**: a small always-resident capsule on the desktop, showing only "is there anything"
2. Quick layer: one-click action buttons [Build] [Test] [Deploy]
3. **Detail Window**: expands the details when needed (Build #1024 Status Failed Repo/Branch…)

Entry points: **Tray / Hotkey / Dock**.

## Key design pieces

- **Notification Engine**: rule-driven (if CI status == Failed → notify), not a bare Toast; Severity is one unified five-level scale (Info/Success/Warning/Error/Critical) and providers are not allowed to define their own states
- **Tiered refresh strategy**: PR 30–120 s, CI 10–30 s… never polling everything once a second; on failure show "Unable to refresh, Last update 2min ago [Retry]" rather than throwing the exception at the user
- **Offline Mode**: runnable while offline
- **Local First**: configuration lives on the machine (`%AppData%\Beacon`: config/connections/widgets/pins.json — the original plan's `workflows.json` was never implemented, following the "MVP does not do Workflow" decision), **secrets go to the Windows Secret Storage and are strictly forbidden in plain JSON**
- **Architecture principle**: Local First + Provider First; the Agent Server serves only as a connector and is not hard-wired

## Phasing

- **Phase 1 (MVP)**: GitHub Provider (PR/CI), Status Capsule, simple Actions, Notification, Tray/Hotkey, local configuration. **Explicitly not done**: a complex Workflow Engine, multiple Providers, agent coordination.
- **Phase 2**: Jenkins/GitLab/Docker/Machines/Agents/Generic REST Widget, Schedule, Webhook, Workflow, [Run Task] sent to an Agent — heading toward a Developer Control Plane.
  - **Frozen record (2026-10-11, master-plan §2.3 decision ②)**: GitLab/Docker/Machines/Agents/Workflow/[Run Task]/Control Plane are all **frozen as not done** (a long-term product boundary, not a postponement). Generic REST (`http.status`/`http.quota`) and the Webhook Action are already landed and unaffected. Jenkins is already landed (`jenkins.build`, an open-source standard JSON API, 2026-10-11 批3-补31).

## MVP success criteria (first-version acceptance)

A developer can: auto-start on boot → see the state in a corner of the desktop at all times → a one-click Run/Retry succeeds, all without **opening a browser or signing in to GitHub**.

## Implementation order (explicitly required by the original plan)

**The first step is not to write code**: produce ① a technical RFC ② a Solution / project layout ③ an MVP Issue list, then implement Phase 1 → Phase 2 incrementally — to stop it becoming an over-engineered Dashboard from the start. **Documentation first.**
