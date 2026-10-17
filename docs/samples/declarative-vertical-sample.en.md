
# Importable sample: GitHub Actions vertical slice (P3-04)

`declarative-vertical-sample.json` is a **config bundle** (same format as Settings → Advanced → Import/Export) demonstrating one vertical declarative loop: a single `github.actions.runs` state feeds the L0 tile, the L2 panel row, and the L3 detail from the same source of truth, while the L3 detail additionally aggregates a second source (`http.quota`) to demonstrate cross-source bindings and missing-source display.

## What is inside

| Item | Notes |
|---|---|
| `w-sample-ci` | GitHub Actions run card: tile / row / detail faces all declarative; detail aggregates the quota source |
| `w-sample-quota` | `http.quota` card: points at the public GitHub `rate_limit` endpoint, works without any credential; its detail face is an HTML carrier face (controlled subset, 2026-10-17 ruling) |
| `conn-sample-github` | GitHub connection: bundle carries a credentialRef only; enter your PAT after importing |
| `conn-sample-quota` | HTTP connection: replace with any JSON quota endpoint you like |
| `open-quota-doc` | Bundled action (open.url): referenced by the HTML face button's `data-action`; clickable right after import |

## How to use

1. **Back up first**: importing replaces all five config files (including the action library) — run Settings → Advanced → Export first.
2. Settings → Advanced → Import configuration, pick this file.
3. On the connections page enter a PAT for `conn-sample-github`; change `repository` / `workflow` of `w-sample-ci` to your repo.
4. Pin it and walk the three faces: tile (L0) → panel row (L2) → detail (L3); one state, three consistent views.

## What to verify

- [ ] No C# changes: edit only the JSON component trees; saving updates the target face immediately (save redraws without refetching).
- [ ] Three faces, one source: L0 / L2 / L3 all show the same `w-sample-ci` state.
- [ ] Two independent sources: quota refreshes never trigger CI fetches; the QUOTA section in detail updates on its own cadence.
- [ ] Missing-source display: disable or delete `w-sample-quota` and the QUOTA section shows "⚠ 缺少数据源：quota" — no faked zeros, no stale healthy values; it refills automatically once restored.
- [ ] Clear-to-fallback: remove the `components` / `sources` keys and save; all three faces return to traditional rendering.
- [ ] Offline / recovery: while offline the tile and detail show stale timestamps and self-heal after reconnection.
- [ ] HTML face fallback: delete `open-quota-doc` from the action library and re-import; the detail's button slot degrades to a "⚠ 动作不可用" diagnostic placeholder while the other faces keep working.

The runtime items above require acceptance on real Windows. The CI structural gate is kept evergreen by `DeclarativeSampleTests`: trees and sources validate, additional sources close over bundled widgets, and the file carries no secret-like tokens; the HTML carrier face parses through the closed vocabulary and its `data-action` closes over bundled actions.
