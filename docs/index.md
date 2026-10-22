
# Beacon

<p align="center">
  <img src="img/logo.svg" width="64" alt="Beacon logo">
</p>

> **Windows Developer Status & Action Center** — See what needs your attention.

A developer status and action center that lives on the Windows desktop: it aggregates the state of your development environment — GitHub, CI and more — surfaces it proactively, and hands you one-click actions. **Signal + Action, not a Dashboard.**

## Documentation

| Document | Contents |
|---|---|
| [Mission brief](mission.md) | Product positioning / core concepts / phases / MVP acceptance criteria (the original plan is archived in the excerpt in the same directory) |
| [Positioning and first scenarios](positioning.md) | P0 direction: the company build pipeline and AI usage quotas as scenarios, the three Provider categories, landed progress and configuration examples |
| [RFC-001 technical design](rfc/RFC-001-technical-design.md) | Technical design: architecture / domain model / display layer (L0 floating components + four interaction levels) / refresh / notification / storage / risks |
| [Product & technical master plan](master-plan.md) | Design baseline (2026-10-11): boundaries / interaction / architecture / acceptance / agent constraints |
| [Solution and project layout](solution-structure.md) | Solution / project structure / dependency rules / NuGet manifest |
| [MVP issue list](mvp-issues.md) | 43 MVP issues (P0–P8, each with acceptance criteria and dependencies) |
| [Troubleshooting](troubleshooting.md) | Installation and launch FAQ: log locations, SmartScreen, SHA256 verification |
| [Configuration reference](configuration.md) | Every setting itemized: files and when they take effect, global settings, appearance and motion, notification rules, endpoint / credential / quota conventions for 17 connection types, component fields, pinned layout, action library, import and export |
| [Documentation and source consistency audit](审计-文档一致性.md) | Ledger of documentation-versus-source drift (34 baseline items plus 24 dated 2026-10-11, with the P0 list) |
| [Acceptance execution checklist](acceptance-checklist.md) | 71 acceptance items in executable form: preconditions / verification steps / expected results for each, plus the shared preparation pack (C-*) and notes on local automation coverage |
| [B-705 L0 behavior audit](acceptance-B705-L0-behavior.md) · [B-803 end-to-end walkthrough](acceptance-B803-e2e-walkthrough.md) | Acceptance records and the real-machine checklist |

## Roadmap

1. ✅ Mission brief + technical RFC + solution layout + MVP issue list
2. ✅ M1 Windows shell (tray / capsule / hotkey / auto-start)
3. ✅ M2 Core kernel (refresh scheduling / state aggregation / cache / event bus)
4. ✅ M3 GitHub loop (PR and Actions providers, ETag and rate-limit aware)
5. ✅ M4 Notifications and panel (action executors / Toast / notification center / tray coloring)
6. 🔨 M5 L0 floating components + Quick Panel = MVP (code complete; remaining: the B-803 real-machine walkthrough and the B-804 clean-VM acceptance)

## Build requirements

Windows + .NET 10 SDK + Windows App SDK (WinUI 3 does not build cross-platform).
