
# todo 42-item triage (2026-10-16 user order)

> **Historical snapshot, archive only** (DOC-01): the line numbers below are `todo.md` line numbers as of 2026-10-16 and have since drifted with new entries; do not dispatch work by them. The current authoritative ledger is the repo-root [`todo.md`](../todo.md); judge by item text.
> Criteria: classify each of the 42 unchecked items in `todo.md` into A (implementable directly) / B (needs the user's environment or a user decision) / C (already proxy-verified, can be closed).
> Red line per the order: a C-class checkoff must cite evidence (scenario name or a test file at `file:line`); no checkoffs to clear the ledger.
> This version's conclusion is **C is the empty set** — rationale in the next section. The 6 items the user named (37/39/57/62/65/67) do carry「已代验」 in their parentheses, but each parenthesis also retains its own「需装机半」(installation half) — and per the acceptance criteria todo.md sets for itself ("a change that was not verified does not count as done"), checkoff would be self-contradictory. All 6 go to B, with their proxy-verified halves listed separately in the B table along with the matching installation-checklist row.
> Line numbers refer to `todo.md` as of this document's creation; adding or removing todo rows shifts them, so judge by the item text itself.

## Overview

| Class | Count | Distribution |
|---|---|---|
| A implementable directly | 5 | All of Phase 5「完善组件目录与用户体验」(todo.md:184-188) |
| B needs the user's environment or a user decision | 37 | MVP 21 (:37-83) + 后续可选 1 (:91) + 验收 3 (:96-99) + the 6 Phase 4 rows (:175-180, need a decision) + the 6 Phase 6 rows (:192-197, real-machine acceptance) |
| C already proxy-verified, can be closed | 0 | No single item meets "evidence covering the whole row" |

## Why C is the empty set

Criterion: for an item to qualify for C, the existing proxy evidence must cover the row's **entire** acceptance criteria. Checking them one by one:

1. The parentheses of all 21 unchecked MVP items end with「需装机半」, and most additionally depend on real credentials (:44/:46/:48/:54/:99), physical hardware (:70/:71), or OS-surface behavior (:37/:57/:96/:97). The CI evidence (`daily-build` visual-evidence artifacts; a failed assertion is a red build) covers only the mechanical half.
2. The 6 items the user named are the best-evidenced group, and still do not cover their whole rows:
   - todo.md:37 the process half is proxy-verified (scenario E1, process count stays 1); the wake bubble is an OS Toast surface (E1b probe built, degrades to warning when invisible in a headless session, run 38619441591).
   - todo.md:39 position restore + the drag gesture chain are proxy-verified (scenarios R/R3, run 38750239155); cross-DPI topology needs physical multiple monitors.
   - todo.md:57 the AUMID self-heal half is proxy-verified (scenario F5t, `Initialize()` rebuilds after the registry key is pre-deleted, run 38494525200); click/activation routing is an OS Toast surface.
   - todo.md:62 cache-without-white-screen + Esc keyboard close are proxy-verified (scenario K3 hard assertion, run 38743014862); precise 100ms timing needs a stopwatch.
   - todo.md:65 the blank-area hit-test ruling is proxy-verified (H5b `WM_NCHITTEST`: tile row = HTCLIENT, outer blank band = HTTRANSPARENT, run 38750239155); how a real mouse click lands on the window below is an installation-side impression.
   - todo.md:67 position restore + either/or + the drag chain are proxy-verified (scenarios H/H6, run 38750239155); drag feel needs an installation.
3. Engine-level evidence is indeed complete (`tests/Beacon.Core.Tests/EventBusTests.cs:9` Publish_InvokesAllSubscribers, `:25` HandlerException_DoesNotAffectOtherSubscribers, `:38` Unsubscribe_StopsDelivery, `:52` publish snapshot semantics, `:86` SubscribeOnUi_MarshalsToDispatcher; `tests/Beacon.Core.Tests/NotificationEngineTests.cs` engine de-duplication and cooldown), but those items are consumed by rows todo.md already has checked (:58/:59) and do not sit among the 42 unchecked.

Conclusion: checkoff-ing the 6 items to clear the ledger would violate both the order's red line and todo.md's own acceptance criteria, so it is not done. If you consider the installation half of one of them droppable (e.g. downgrade "bubble impression" or "100ms timing" to out of scope), that is a change of acceptance criteria — say so explicitly and I will check it off under the new criteria with a note.

## Class A (5 items): Phase 5 refining the component catalog and user experience

Baseline facts: Phase 2 already prepared the design tokens and vocabulary components (`ComponentFeedback` — the four vocabulary components Empty/Loading/Error/Offline — is landed but has no call sites; see `docs/design/redesign-2026-10-16-组件库落地.md`; `SampleGalleryWindow` already has live specimens of all 14 factories), so class A is mainly **wiring already-prepared components onto real surfaces**, not building new ones from scratch.

| Row | Item | Implementation points | Local gate |
|---|---|---|---|
| 187 | Unified empty / offline / error states | Wire the four `ComponentFeedback` components into the QuickPanel empty areas and the Detail no-data state, replacing today's two competing conventions ("empty area hidden" + "XAML banner"); after wiring, delete the duplicate text branches | No Core pure-function change, so no new unit tests; App side hand-audited + CI |
| 185 | Richer layout and conditional display | Add call sites for the `ComponentLayout` Column vocabulary component beyond the existing Row/Columns/SectionLabel/When; drive conditional display from state fields rather than hard-coding it | Layout invariants are already guarded (`ComponentLayerInvariantTests`); add one case for the new shape |
| 184 | Timeline, table, chart and trend components | Add three component factories (timeline over event history, table over multi-row payload, chart over trend series); data flows through the existing binding engine (state / selector / const three states) with no new Provider | One set of Core-side parse and empty-state tests each |
| 186 | Component instance editing, sizing and desktop layout configuration | Add an instance surface to the settings page (component-tree editing, size tier, desktop layout region); writes go through the existing `Config.UpsertWidget` + tree-validation fail-closed convention | Add Storage-side tests for the validation branches |
| 188 | Theme, density and accessibility refinement | Wire a density scale (compact/regular) into the `DesignTokens` spacing scale; fill in accessibility — UIA names and focus order (current known pitfall: a Border with only AutomationId does not enter the UIA tree, `SetName` is required to create a peer) | After UIA names are filled, scenario X-class assertions need a matching recheck |

Priority order: 187 → 185 → 184 → 186 → 188. Rationale: 187 is pure wiring with no new semantics, lowest risk; 185 depends on 187's state semantics; 184's three new components have the largest surface but are purely additive; 186 touches the settings page and the configuration surface (widest change blast radius, so later); 188's accessibility fill-in should happen after the new surfaces of the previous four are settled, otherwise it is rework.

**Blocker (this machine is Linux)**: the App side (`src/Beacon.App/`, WinUI 3) cannot be built on Linux — `XamlCompiler` dies before `Csc`, so there is no local C# gate; App changes are only hand-audited here and validated by CI (`windows-latest`). Core / Storage unit tests can run locally in full. This does not block delivery; it is the established delivery path — every previous App change landed this way.

## Class B (37 items): what the user needs to provide

| Needed from the user | Rows covered | Exactly what is missing | Proxy-verified half (evidence) |
|---|---|---|---|
| A real GitHub PAT | 44, 46, 48, 54, 99 | A revocable real PAT + a real repository with PR and Actions activity | mock chain scenario D; API-layer fake-Handler unit tests (GitHubPullRequestsProviderTests 18, GitHubActionsProviderTests 9, GitHubApiClientTests 10, GitHubWorkflowExecutorTests 6); mapping layer table-driven |
| A real vendor API and its credential fields | 91 | The vendor-private packaging tool's API docs, credential field names, and quota conventions | The Jenkins Provider on an open standard API is already landed (`jenkins.build`) |
| Installation-side impression / timing / audio inspection | 37, 52, 57, 60, 62, 65, 67, 73, 75, 79, 80, 97 | A headed desktop session: bubble/Toast presentation, default-browser navigation, a stopwatch for the 100ms check, listening, tray-icon inspection, drag feel | E1 process half, F5t AUMID self-heal half, K3 Esc keyboard close, H1 foreground unchanged, H5b `WM_NCHITTEST` hit-test ruling, G7a tile-layer reflection, G4/G5 color change, G6/G7b/G7c/V2 four pulse halves, V1/V2/V4 injection and notification chain (runs in each parenthesis) |
| Physical or virtual display stack (multiple monitors, different DPI, hot-plug) | 39, 70, 71 | At least two monitors at different scales; CI is a single-monitor session and cannot drive topology events | R/R3 position and drag chain, H/H6 floating drag and restore (run 38750239155) |
| A real human end-to-end walk | 74, 82, 83 | A clean Windows 11 VM + a recording; the RFC §6.2.7 four items involve a real network and human steps | Screenshots already archived in the `visual-evidence` artifacts; the cache hydration full-cycle unit test has passed |
| A user decision (the HTML boundary ruling) | 175, 176, 177, 178, 179, 180 | Phase 4 must get an architecture decision before starting: the repo holds a countervailing record (`docs/master-plan.md:769`), and about 60% of the design draft's claims about "existing implementation" do exist in the repo while two of its underlying premises are largely absent | None (no code before the decision) |
| Real-machine regression acceptance | 192, 193, 194, 195, 196, 197 | Depends on class A landing + the Phase 4 ruling, then the 71-item installation checklist and scenario-evidence regression | The 71-item installation checklist is in place (`docs/acceptance-checklist.md`), and CI visual-evidence scenarios A-Z are already the regression gate |

## Sync convention with todo.md

- This document does not check off any todo row (C is the empty set).
- Class B rows are left verbatim (their parentheses already carry "proxy-verified half + installation half", consistent with the B table here).
- Class A is implemented one item at a time in priority order; after each item lands, its commit hash is appended to the matching `todo.md` parenthesis and it is checked off, on the same convention as Phase 0-3.
- When an installation half clears, the matching date annotation in `docs/acceptance-checklist.md` is updated in sync.
