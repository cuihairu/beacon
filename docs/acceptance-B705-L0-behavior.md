
# B-705 L0 behavior invariants check (show only, never interrupt — RFC §6.2.7)

> Status: the code-side static audit is complete (recorded in this document); per-item real-machine verification belongs to the B-803 end-to-end walkthrough (run on the first Windows machine).

## Invariant table

| # | Invariant | Code guarantee | Result |
|---|---|---|---|
| 1 | L0 only shows state; a click drills down (to L2 or L3) | `PinnedHostWindow.TileActivated` is the only click exit → `QuickPanelWindow.Toggle()` (L2); there is no other behavior entry | ✅ audit passed |
| 2 | Produces no dialog, no Toast, no sound | `PinnedHostWindow.cs` contains no Toast/ContentDialog/MessageBox/Sound/Play call anywhere; the only way L0 expresses an alert is a status-light color change (`PinTile.Update`) | ✅ audit passed |
| 3 | Carries no action that requires confirmation | L0 has no action entry (no `ActionRunner` reference); drill-down only | ✅ audit passed |
| 4 | Never steals focus, never appears in Alt-Tab or the taskbar | `WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW`, `IsShownInSwitchers=false`, topmost, and drag/hover all use pointer events (no activation) | ✅ audit passed (real-machine recheck belongs to B-803) |

## Critical expression (supplement to invariant 2)

- Current state: Critical only turns the status light red (`SeverityPalette.Color(Critical)`), with no interruption of any kind.
- The pulse animation (≤1 Hz × intensity) belongs to the **B-707 motion system** scope (RFC §6.2.8: reduced mode defaults to pulsing). Once B-707 lands, this table is re-checked for "a pulsing light still produces no sound and no dialog".

## Alert channel separation

- Alerts always go through `NotificationEngine` (B-502) → Toast and sound are driven by the rule table; L0 does not subscribe to `NotificationRaised` and never touches `INotificationSink` (grep evidence above).

## Real-machine checklist (checked off when B-803 runs)

- [ ] Inject a Critical state: L0 only changes color (pulses after B-707), with no dialog or sound interruption; the Toast is still emitted by the notification engine as usual
- [ ] Network disconnected: the tile shows a gray hollow light + Last update (B-704), with no exception dialog; it resets automatically on recovery
- [ ] Click a tile → L2 opens and takes focus; it closes automatically on focus loss; ESC closes it
- [ ] Neither the Alt-Tab list nor the taskbar shows the L0 host; clicking a tile does not interrupt the current input focus
- [ ] Drag flush against a screen edge → collapses to a strip; hover expands, a click on the drop target pops back to expanded, a click while expanded drills into L2; releasing near an edge does not collapse by mistake (B-703 + the reversible-collapse revision)
