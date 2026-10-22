
# B-803 MVP end-to-end walkthrough (mission brief §Success criteria)

> Dependency: run this on **one Windows machine** only after B-801..802 and B-805 are code-complete (CI green).
> Success criteria (mission brief §MVP): auto-start on boot → see the state in a corner of the desktop at all times → a one-click Run/Retry succeeds, all without **opening a browser or signing in to GitHub**.
> The invariant items in detail are in [acceptance-B705-L0-behavior.en.md](acceptance-B705-L0-behavior.md) (its 5 real-machine checklist items are ticked off inside B-803).
> Output: recorded screenshots/archive plus either a fix or an issue for anything found; B-804 additionally needs a **clean Win11 VM** on top of this.

## 0. Preparation (5 minutes)

- [ ] Download `beacon-nightly-windows-x86_64-setup.exe` (or the portable zip) and verify SHA256SUMS
- [ ] Start screen recording (continuous for the whole run; take separate screenshots of problem points)
- [ ] A machine with GitHub repository access plus one PAT (used for connection entry and Retry)
- [ ] A controllable workflow ready (can be failed on demand and re-run, for example a test workflow in the repository)

## 1. Script main chain (each link of the mission brief success criteria)

| # | Step | Action | Pass criteria | Result |
|---|---|---|---|---|
| 1 | Install / extract | Run setup.exe (or extract the zip) | No errors; Beacon appears in the Start menu (the zip route shows a `Beacon.App.exe` icon directly) | ☐ |
| 2 | First-launch feedback | Start Beacon | Startup notification bubble + tray icon + status capsule in the bottom-right corner; **no main window, and it does not enter the taskbar/Alt-Tab** | ☐ |
| 3 | Auto-start on boot | Check `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | A Beacon entry pointing at the install path (can be turned off in settings) | ☐ |
| 4 | Enter a connection | Tray menu → Settings → Connections → type github → fill in Id + Token | "✓ 已保存"; the connection test returns Healthy | ☐ |
| 5 | **Token red line** | Open `%AppData%\Beacon\connections.json` and search the whole file for the PAT itself | **The plaintext token is not found**, only `credentialRef` | ☐ |
| 6 | Add widgets | Settings → Widgets: `github.actions.runs` + `github.pull_requests` (pick a connection, fill the required fields) | Two entries appear in the list; the L2 panel shows the corresponding rows and counts | ☐ |
| 7 | Pin to the desktop | Turn on the "钉" switch on the widget row | A tile appears on L0 (status light + label) | ☐ |
| 8 | Cause a failure | Fail the controllable workflow once | The capsule turns red / the count changes + the tray icon is recolored + **a Toast pops up** (with sound per the rule) | ☐ |
| 9 | Drill into the detail | Click the Toast (or the L2 row) to enter L3 | The detail window shows status/repository/phase/error summary; L0 is not interrupted | ☐ |
| 10 | **One-click Retry** | Click [Retry] in L3 (no browser opened at any point) | The workflow re-runs successfully → Success Toast + the light turns green | ☐ |
| 11 | Settings immediacy | Change a severity color / motion tier / opacity / capsule toggle | Takes effect immediately (a theme change taking effect on restart is expected); still in force after a restart | ☐ |
| 12 | Import / export | Settings → export the configuration; then import it after breaking things | Behaviour is restored; the exported file contains no token; it prompts to re-enter the secret | ☐ |
| 13 | Offline degradation | Disconnect the network | The tile shows a gray hollow light + Last update; no exception dialog; resets automatically on recovery | ☐ |

## 2. Four interaction levels checklist (RFC §6)

- [ ] An L0 tile single click → L2 opens and takes focus; ESC closes it; it closes automatically on focus loss
- [ ] An L2 row single click → L3 detail; closing L3 returns to the always-resident L0
- [ ] Hovering, dragging, and collapsing/expanding L0 never activates the window and never interrupts the current input focus
- [ ] Every tray menu entry works (panel / settings / notifications / refresh / exit); the hotkey `Ctrl+Alt+B` summons the panel
- [ ] All 5 items of the B-705 invariant real-machine checklist are ticked (see that document)

## 3. GLM quota end to end (blocking acceptance feedback: connect the source → add a widget → real numbers on the board)

| # | Step | Action | Pass criteria | Result |
|---|---|---|---|---|
| G1 | Enter the GLM connection | Settings → Connections → type `bigmodel` → Id + PAT (or a raw key) | The connection test returns Healthy; it suggests "在下方组件向导选该连接，或用『一键添加』即可上板" | ☐ |
| G2 | One-click example | Settings → bigmodel module → "一键添加 GLM 额度组件（示例）" | `bigmodel.usage:GLM` appears in the widget list; Pinned=true pins it automatically; the L0 capsule in the bottom-right corner shows a **value card + progress bar** | ☐ |
| G3 | First number on screen | Wait ≤10 s (the CI/Ci tier) | The capsule shows "GLM 额度 · 已用 XX% · 剩 YY tokens · MM-DD HH:MM 重置"; the progress bar fills according to the used percentage; right-clicking the L2 row shows a `Source: GLM` annotation | ☐ |
| G4 | Field mapping self-check | Settings → widget `bigmodel.usage:GLM` → edit → confirm the `total_path/used_path/reset_path` mapping is correct | The field paths match the GLM `quota/limit` response shape (`$.data.total` / `$.data.used` / `$.data.reset_at`) | ☐ |
| G5 | Forced manual refresh | Tray menu → Refresh All | The value and progress bar refresh live; the log `%APPDATA%\Beacon\logs\beacon-*.log` has one line per hop (one-click add → fetch → field mapping → render) | ☐ |

> ⚠️ **Screenshots must be produced on a real Windows machine** (Linux can only run unit tests / builds; WinUI 3 does not run in that environment). After finishing G1–G5, capture one screenshot of the L0 capsule and one of the L2 panel, save them to `docs/img/`, and attach them to the PR or reply.

## 4. Archive

- [ ] Save recordings and screenshots to `docs/img/` (large recordings may go into a Release asset with a link from the README)
- [ ] For every failed item: fix it on the spot (through the gate, pushed) or file an issue
- [ ] B-804: run the same script once on a **clean Win11 VM** (download → run → this checklist), recording first-install environment behaviour such as VC++ and SmartScreen
