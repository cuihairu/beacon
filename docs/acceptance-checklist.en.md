
# Installation acceptance checklist (71 items · 2026-10-12)

> Source: the three ledgers in `docs/mvp-issues.md`「装机对照清单」(✅ verified 1 + 🔧 fixed-pending-verify 17 + ⬜ not fixed 53 = **71 items**; 2026-10-12 eleventh re-sort corrected the original 51/68 sum drift; 批3-补47 added 2.17 and then line-by-line recount).
> This version expands each item into a three-part form: **preconditions / verification steps / expected results**. Every step can be followed as written and every item can be receipted individually.
> Items with local automation evidence annotate the corresponding unit tests under "Appendix: local automation coverage notes"; the real machine only supplies the UI/interaction half.

## 0. General preparation (referenced by number by each item, not repeated)

| Number | Preparation |
|---|---|
| **E0 install** | Download the latest green-run nightly assets from daily-build (zip + SHA256SUMS verified consistent), extract to a clean directory and run. **Version gate**: packages after 2026-10-12 (批3-补42 DSL, 批3-补45 floating widening, 批3-补47 update check landed after that) contain the new items; earlier packages cannot exercise them |
| **E1 logs** | `%APPDATA%\Beacon\logs\beacon-YYYYMMDD.log`; failing items attach log-line evidence |
| **E2 receipt** | Record each item as "passed / failed + phenomenon + screenshot or log line"; on pass, annotate the date on the corresponding line in `docs/mvp-issues.md`; on failure, record only the phenomenon, do not self-repair |
| **C-GH** | A GitHub connection (real PAT) + one `github.pull_requests` and one `github.actions.runs` widget (pointing at a real repository with PR and Actions activity) |
| **C-HTTP** | An http connection (any reachable JSON endpoint; a LAN packager or a public echo service both work) |
| **C-MACHINE** | A machine connection (zero credentials) |
| **C-HERALD** | A self-hosted herald instance connection (cuihairu/herald) |
| **C-MIMO** | A Xiaomi MiMo connection + browser login to platform.xiaomimimo.com as backup (get the Cookie via F12) |
| **C-JENKINS** | A Jenkins connection (the public anonymous instance `https://weekly.ci.jenkins.io` suffices, or self-hosted) |
| **C-MOONSHOT** | A Moonshot open platform Key (platform.moonshot.cn; not interchangeable with Kimi For Coding) |
| **C-DISCONNECT** | A way to cut the network: turn off Wi-Fi / unplug the cable; recovery the same |
| **C-DPI** | A 200%/250% scaled display (or change scaling in Windows Settings); with multiple monitors, at least two monitors at different scales |

Overall receipt criteria: MVP acceptance (B-803) closes once everything passes; archive recordings/screenshots under `docs/`.

---

## 1. ✅ Verified (1 item, archived, not executed again)

| Item | Conclusion |
|---|---|
| Floating transparency floor 0.4→0.2 (批3-补10/补13⑤) | User confirmed fixed on installation 2026-10-10; keep it as is |

---

## 2. 🔧 Fixed, pending verification (17 items — focus on these with the latest nightly)

### 2.1 B-301 | Real PAT connection test success/failure paths
- **Preconditions**: E0, E2; one revocable real GitHub PAT.
- **Steps**: ① Settings → Connections → new GitHub, mistype the PAT by one character → "Test connection"; ② leave the PAT empty → "Test connection"; ③ enter the correct PAT → "Test connection"; ④ swap in a revoked PAT → "Test connection".
- **Expected**: ①④ clearly report authentication failure (not "connection normal"); ② goes through an anonymous request and honestly shows the anonymous result (does not falsely report authenticated); ③ the connection is healthy and the rate-limit headers are observed.

### 2.2 B-302 | Real repository PR manual verification (count and yellow highlight)
- **Preconditions**: E0, C-GH.
- **Steps**: ① add a `github.pull_requests` widget pointing at an active repository; ② compare the Open PR count with the GitHub web page; ③ refresh (tray Refresh All); ④ create a new PR or close a PR and refresh again.
- **Expected**: The count matches the web page; yellow while there are open PRs, green when it hits zero; additions/closures are reflected within ≤1 refresh cycle.

### 2.3 B-302 | Real repository red CI criterion
- **Preconditions**: Same as 2.2, and the repository needs an open PR with red CI (or run a check red on some PR).
- **Steps**: ① observe the PR card summary; ② (optional) toggle the `warn_on_red_ci` switch and refresh to compare.
- **Expected**: The red-CI PR summary shows an "N red ci" section; after a 304 re-probe the terminal state is reused without another request; when the switch is off, red CI does not affect it.

### 2.4 B-303 | Real workflow manual verification (success/failure/running)
- **Preconditions**: E0, C-GH (a `github.actions.runs` widget).
- **Steps**: ① trigger one successful run, wait for it to finish and refresh; ② trigger one failing run; ③ trigger a long run (>2 minutes) and refresh while it is running.
- **Expected**: Success→green, failure→red, running→yellow with increasing duration; a run stuck at the 16-minute level formats the duration correctly (no negatives / garbled text); a malformed JSON response is shown explicitly as Degraded without crashing.

### 2.5 B-303 | Real workflow mapping review
- **Preconditions**: Same as 2.4.
- **Steps**: Cross-check each Beacon card against the GitHub web run status one by one (queued/running/success/failure/cancelled).
- **Expected**: The state mapping corresponds one-to-one with the web page; cancelled is not misjudged as failure.

### 2.6 批3-补8 | Xiaomi console conventions + GLM 5h primary slot
- **Preconditions**: E0, C-MIMO; a GLM real-Key connection.
- **Steps**: ① record the MiMo console Cookie (F12 → Application → Cookies → paste the whole `api-platform_ph` line); ② refresh the MiMo card; ③ observe the GLM card's main value.
- **Expected**: MiMo shows plan used / total / percentage (data from the console API, not a "no quota slot" placeholder); GLM's main slot is the 5h rolling-window percentage.

### 2.7 批3-补9/17/18/23 | Floating progress bar + quick panel bottom
- **Preconditions**: E0; a numeric widget (e.g. GLM/bigmodel quota) with "numeric floating window" on.
- **Steps**: ① look at the floating tile progress-bar form; ② push usage to ≥80% (or adjust the threshold); ③ open the Quick Panel and look at the RECENT EVENTS section.
- **Expected**: The progress bar is a 4 DIP rounded bar + an "NN% " readout (default form); yellow at ≥80%, red when full; the "pool water" effect only appears when selected in settings; RECENT EVENTS scrolls inside the Grid and does not break the panel.

### 2.8 批3-补11 | Ark quota-exhausted display
- **Preconditions**: E0; an ark connection (Coding Plan). Natural exhaustion is hard to trigger, so you can temporarily drag the widget threshold very low to simulate the judgment.
- **Steps**: ① wait for some window (5h/weekly/monthly) to exhaust, or simulate it with the threshold; ② observe when a 429 AccountQuotaExceeded-class response arrives.
- **Expected**: The card shows "额度用尽（窗名，重置时间）" (quota exhausted (window name, reset time)); the 429 exhaustion special case is treated as that window spent with the reset time; an ordinary rate-limit 429 does not flip the state.

### 2.9 批3-补12 | Notification rule wiring
- **Preconditions**: E0; add one custom rule under Settings → Notifications (e.g. Toast when some widget is Warning+).
- **Steps**: ① save the rule; ② manufacture a state that satisfies the condition (e.g. cut that connection or inject a failure); ③ change the rule and retest.
- **Expected**: The custom rule actually fires a Toast (before the wiring it was an empty promise); after modifying the rule the next evaluation takes effect, no restart needed.

### 2.10 批3-补13 | UI/UX audit 6 groups of discoverability fixes (⑤ transparency verified)
- **Preconditions**: E0.
- **Steps**: Walk each group: ① the action standalone page is discoverable; ② the detail window scroll region is resident (does not rely on noticing a scrollbar); ③ the settings form is laid out compactly; ④ the refresh tiers have Chinese labels; ⑤ the L2 tile two-column layout; ⑥ screenshot the settings page for the archive.
- **Expected**: All groups match the designs recorded in 批3-补13; no buttons/regions with "invisible" problems.

### 2.11 批3-补19 | Edge-collapsed strip cannot be recovered
- **Preconditions**: E0.
- **Steps**: ① drag the capsule / floating panel to a screen edge so it collapses to a strip; ② hover the strip with the mouse; ③ move away and hover again.
- **Expected**: Hovering the strip expands it back to the full panel (no more death loop / unrecoverable); the strip and the expanded panel share the same Place math, geometry does not jump.

### 2.12 批3-补20 | 5K / high scaling clipped text (DPI)
- **Preconditions**: E0, C-DPI (200%/250%).
- **Steps**: ① open the detail window at 200% scaling; ② look at the capsule and floating tile text; ③ repeat at 250%.
- **Expected**: The detail window sizes to content and text is complete, not clipped; the capsule / floating panel DIP layout is correct (the earlier 5K clipping root cause was not resizing and falling back to fixed physical pixels).

### 2.13 批3-补20 | Drag follow-the-cursor + cross-screen flicker
- **Preconditions**: E0, multiple monitors (a high-refresh-rate mouse is better).
- **Steps**: ① drag the capsule slowly; ② fling it quickly; ③ drag across two monitors with different DPIs.
- **Expected**: Follows the cursor the whole way without crawling (the start-point absolute positioning fix removes per-event accumulation rounding); no re-layout flicker while dragging across DPIs (re-layout skips WM_DPICHANGED during a drag).

### 2.14 批3-补21 | Moonshot balance card
- **Preconditions**: E0, C-MOONSHOT.
- **Steps**: ① create a Moonshot connection (platform.moonshot.cn Key) → test connection; ② add a `moonshot.balance` widget; ③ compare the balance with the web page; ④ raise `warn_below` above the current balance.
- **Expected**: The connection is healthy and the balance matches the web page; balance ≤ warn_below (default 20) turns yellow, ≤ error_below (default 5) turns red.

### 2.15 批3-补22 | Drag across three screens follow-the-cursor
- **Preconditions**: E0, three monitors at different scales (if no three-screen setup, approximate with two).
- **Steps**: Slowly drag the capsule across the three screens in turn, pausing on each to observe the size.
- **Expected**: The anchor always follows the cursor (physical-pixel convention); no scaling mismatch and no size jumps across different-scale screens.

### 2.16 批3-补22 | Detail window still clips text at 200%
- **Preconditions**: E0, C-DPI (200%).
- **Steps**: Open any widget detail window at 200% scaling; check the title, body, and bottom button area.
- **Expected**: Complete display without clipping (Resize adds back the non-client difference: GetWindowRect−GetClientRect).

### 2.17 批3-补47 | Update check three lines + failure degradation
- **Preconditions**: E0 (a package after 2026-10-12), C-DISCONNECT (means to cut and restore the network).
- **Steps**: ① open Settings → About, click "Check for updates" and wait for a conclusion; ② cut the network and click "Check for updates" again; ③ restore the network and click again. When there is a new version, check that the "Open download page" button appears and points at releases/latest.
- **Expected**: ① the three lines are complete — current version / remote latest version (tag_name) / check result (already latest, or "new version vN" + "Open download page"), with the last check time updated to the minute; ② with the network cut it shows "无法检查更新（HH:mm）" without an exception box; ③ after restoring, it returns to the normal conclusion.

---

## 3. ⬜ Not fixed (53 items: code in place, first verification; of these, the app side of B-103/B-104/B-502 was fully CI/unit-test proxied on 2026-10-13 patrol-B and checked off in the todo/mvp-issues ledgers — this chapter keeps the corresponding entries only as installation walkthrough recheck steps)

### A Installation and process

#### A1 | B-101 | Second launch does not duplicate the process
- **Preconditions**: E0.
- **Steps**: Double-click the exe for a second launch.
- **Expected**: No second process appears; the existing instance is woken and shows a tip bubble.

#### A2 | B-101 | Tray residency
- **Preconditions**: E0.
- **Steps**: Close all Beacon windows → look in Task Manager; then Exit from the tray menu.
- **Expected**: After closing windows the process stays in the tray; only Exit truly quits and the process disappears.

#### A3 | 批3-补27① | Tray Explorer restart recovery
- **Preconditions**: E0, Beacon running.
- **Steps**: End and restart `explorer.exe` in Task Manager.
- **Expected**: After Explorer recovers, the tray icon comes back by itself and the severity color is not lost.

#### A4 | 批3-补27② | Sleep/wake refresh-backfill
- **Preconditions**: E0, at least one widget.
- **Steps**: Sleep (or simulate with lock screen / network cut) → wake → observe the data update time.
- **Expected**: Within ≤1 refresh cycle after waking there is new data (does not depend on a manual force-refresh).

#### A5 | 批3-补28 | Capsule right-click goes straight to settings
- **Preconditions**: E0.
- **Steps**: Right-click the capsule; then left-click-drag and left-click.
- **Expected**: Right-click pops the settings window; drag / click-to-open-panel behavior is not disturbed.

#### A6 | B-104 | Launch on startup
- **Preconditions**: E0.
- **Steps**: Turn on auto-start in settings → reboot; turn it off → reboot again; finally uninstall and check for leftovers.
- **Expected**: On → starts automatically after login; off → no longer auto-starts; no auto-start leftovers after uninstall.

#### A7 | B-105 | Crash fallback
- **Preconditions**: E0, E1.
- **Steps**: Manufacture an exception (e.g. point a config at an endpoint that throws) → observe the process and logs.
- **Expected**: The process survives, the logs contain a stack trace, no zombie windows.

#### A8 | B-804 | Clean VM install
- **Preconditions**: A clean Windows 11 virtual machine.
- **Steps**: Download the nightly → run → complete the B-803 walkthrough (see `docs/acceptance-B803-e2e-walkthrough.md`).
- **Expected**: Installs, starts, the walkthrough passes.

#### A9 | B-102 | Capsule position memory / no mess across DPIs
- **Preconditions**: E0, C-DPI.
- **Steps**: ① drag the capsule to a non-default position → restart; ② drag across screens at different scales.
- **Expected**: After restart the capsule returns to its last position; dragging across DPIs has no size mess (mutual corroboration with 2.13/2.15).

#### A10 | B-103 | Global hotkey
- **Preconditions**: E0.
- **Steps**: ① press the hotkey while any foreground app is active (Notepad / browser / fullscreen games excepted); ② change the key in settings → restart → use the new key.
- **Expected**: The hotkey works in any foreground app and opens the panel; the changed key persists, the old key stops working and the new key works.

### B Connections and widget data sources

#### B1 | 批3-补31 | Jenkins build card
- **Preconditions**: E0, C-JENKINS.
- **Steps**: Create a Jenkins connection (anonymous or user:apiToken) → test → add a `jenkins.build` widget and fill in job_path.
- **Expected**: The build light / build number follows refreshes; building turns yellow; success green / failure red (color-based judgment).

#### B2 | 批3-补32 | Local machine network card
- **Preconditions**: E0, C-MACHINE.
- **Steps**: Create a machine connection (zero credentials) → add a `machine.net` widget → refresh twice or more.
- **Expected**: IP and cumulative traffic are visible in L3; no rate on the first refresh, from the second refresh the ↓↑ rate changes with each refresh.

#### B3 | 批3-补33 | Local machine Git card
- **Preconditions**: E0, C-MACHINE, the path of any local Git repository.
- **Steps**: Add a `machine.git` widget with repo_path set to the local repository → refresh; then make the local branch fall behind the remote (e.g. revert a commit that exists on the remote).
- **Expected**: The ↑↓ / changed / stash counts follow refreshes; falling behind the remote turns yellow.

#### B4 | 批3-补36 | Local machine resource card
- **Preconditions**: E0, C-MACHINE.
- **Steps**: Add a `machine.local` widget → refresh continuously; lower the CPU threshold (e.g. to 20).
- **Expected**: CPU shows no value on the first refresh and the percentage follows refreshes from the second; memory / disk show instantaneous percentages; below the threshold, yellow judgment takes effect.

#### B5 | 批3-补34 | MiMo whole-line Cookie
- **Preconditions**: E0, C-MIMO.
- **Steps**: Paste the whole Cookie line into the console credential box (not just the single api-platform_ph value) → refresh.
- **Expected**: Automatically extracts api-platform_ph and produces data (previously whole-line pasting left the card Offline).

#### B6 | 批3-补38① | MiMo 401 explicit + re-record guidance
- **Preconditions**: E0, C-MIMO.
- **Steps**: Deliberately enter an expired / scrambled console Cookie → refresh → look at the card's failure-reason line.
- **Expected**: The Degraded card explicitly states "Cookie 已失效或未录制" and gives the re-record guidance verbatim (including the F12 → api-platform_ph path).

#### B7 | 批3-补38② | MiMo LiteLLM gateway accounting tier
- **Preconditions**: E0; the local LiteLLM gateway **has a DB attached** (the current `no_db_connection` is a gateway-side gap — this item is bound to the gateway-side fix; before a DB is attached only the explicit Degraded half is verified).
- **Steps**: Record `litellm_spend_url` in Settings (e.g. `http://127.0.0.1:4000/spend/logs`) → refresh.
- **Expected**: With a gateway DB the card shows "本月已用 $X.XX（网关记账）" (spent $X.XX this month (gateway accounting)); without a DB it explicitly shows the "LiteLLM 未接数据库" Degraded state, no silent downgrade.

#### B8 | 批3-补42 | YAML Provider DSL end-to-end
- **Preconditions**: E0, C-HTTP; write a DSL file (see the "Provider DSL" section example in `docs/configuration.md`).
- **Steps**: ① set `dsl_path` in the http connection Settings to point at the declaration file → refresh an `http.status` widget (without filling data keys in widget.Config); ② fill the same-named keys in widget.Config and refresh again; ③ open that widget's detail page, look at the action area, and click an action with retry (make the target endpoint fail once first); ④ break the YAML (Tab indentation) and refresh again; ⑤ remove the file and refresh; ⑥ delete `dsl_path` and refresh.
- **Expected**: ① data comes out per the DSL mapping; ② instance values override the DSL; ③ DSL actions appear only on that connection's widgets, failures auto-retry, and a still-failing message carries "已重试 N 次" (retried N times); ④ the card goes Degraded with the line number explicit, startup is unaffected, other connections are normal; ⑤ likewise Degraded with "文件不存在" (file does not exist); ⑥ behavior is exactly the same as a connection that never used the DSL (zero change).

#### B9 | B-206 | Offline startup has data
- **Preconditions**: E0, at least one widget that has produced data, C-DISCONNECT.
- **Steps**: Produce data normally once → exit → cut the network → start.
- **Expected**: The UI has cached data labeled Last update; after restoring the network it automatically refreshes new data (the cache hydration full-cycle unit test has passed; this item verifies the UI half).

#### B10 | 批3-补35① | Herald incident ledger card
- **Preconditions**: E0, C-HERALD.
- **Steps**: Create a herald connection → add a `herald.incidents` widget → manufacture / resolve one open incident on the herald side.
- **Expected**: The open count follows the ledger; open turns yellow, critical turns red; resolved turns green.

#### B11 | 批3-补35② | Herald subscription card
- **Preconditions**: E0, C-HERALD.
- **Steps**: Add a `herald.feed` widget (public category or private token) → wait for new entries to be delivered.
- **Expected**: The unread count of new entries increases with refreshes; the token goes to DPAPI and no plaintext lands anywhere.

#### B12 | 批3-补37 | Herald delivery-failure card
- **Preconditions**: E0, C-HERALD.
- **Steps**: Add a `herald.deliveries` widget → manufacture one failed delivery (stop the target provider and then notify).
- **Expected**: Shows "无失败" (no failures) when there are none; turns yellow after a failure and shows the failure count.

#### B13 | B-301 supplement | Anonymous connection degradation (companion item, verified together with 2.1, not counted in the 53)
- **Preconditions**: Same as 2.1.
- **Steps**: Create a GitHub connection without a PAT and observe the degradation behavior.
- **Expected**: Shows honestly per the anonymous limit, does not impersonate authenticated. (Overlaps with step ② of 2.1; can be verified together)

### C Actions

#### C1 | B-401 | Action failure UI feedback
- **Preconditions**: E0; an action that will fail (e.g. a URL pointing at a dead port).
- **Steps**: Click that action on the detail page.
- **Expected**: The UI gives visible failure feedback (result text / color), does not go silent.

#### C2 | B-402 | Open URL in browser
- **Preconditions**: E0; a widget with a DetailUrl.
- **Steps**: Click Open on the detail page.
- **Expected**: The default browser opens the correct URL (the template-rendering unit test has passed; this item verifies the browser half).

#### C3 | B-403 | Workflow Dispatch full journey
- **Preconditions**: E0, C-GH, a repository with dispatch permission.
- **Steps**: Dispatch a workflow → observe the whole Run→running→terminal-state journey.
- **Expected**: The whole state journey is visible in Beacon (no need to open the GitHub web page).

#### C4 | B-404 | Local command confirm dialog + timeout persistence
- **Preconditions**: E0; configure a local.command action.
- **Steps**: ① trigger the action and look at the confirm dialog; ② configure a long command to verify timeout termination and log persistence.
- **Expected**: Confirm dialog by default (can be turned off per action config); the timeout kills the process and the output is still persisted to disk (the executor unit test has passed; this item verifies the dialog UI half).

### D Notifications

#### D1 | B-501 | Toast full chain
- **Preconditions**: E0; a notification rule hits.
- **Steps**: ① trigger a Toast; ② click the Toast; ③ delete the AUMID registration key under HKCU\Software\Classes\AppUserModelId (with CustomActivator) and restart the app to retry.
- **Expected**: Send / click / activation routing all work; missing AUMID registration self-heals (the toolkit Initialize rebuilds the registration key on next startup).

#### D2 | B-502 | Toast cooldown
- **Preconditions**: E0; same as above.
- **Steps**: Inject a Failed state (e.g. point some connection at a bad endpoint to trigger Error).
- **Expected**: Exactly one Toast is received (no repeated bombardment inside the cooldown window).

#### D3 | B-503 | Tray coloring
- **Preconditions**: E0; controllable widget states.
- **Steps**: Manufacture Warning/Error → look at the tray icon.
- **Expected**: Aggregated state changes are reflected in the tray icon color within ≤1 refresh cycle.

#### D4 | B-504 | Capsule real aggregation
- **Preconditions**: E0; controllable widget states.
- **Steps**: Cut the network (C-DISCONNECT) and look at the capsule; after restoring, verify the counts.
- **Expected**: Offline shows an Offline banner + time when the network is cut; the counts match the actual Widget states.

### E L2 / L3

#### E1 | B-601 | Quick Panel open/close
- **Preconditions**: E0.
- **Steps**: Open the panel with the hotkey → close with Esc → click outside to close on focus loss; traverse fully with keyboard Tab/arrow keys and close; open and close repeatedly.
- **Expected**: Opens in <100ms with no white screen (cache first); fully operable and closable by keyboard; repeated toggling is stable.

#### E2 | B-602 | Overview/Pin persistence
- **Preconditions**: E0.
- **Steps**: Compare the Overview counts with the actual states; right-click Pin one widget → restart.
- **Expected**: Counts / events are consistent; Pin persists (even without L0 UI effects in this phase it must land in widgets.json/pins.json).

#### E3 | B-603 | Detail + Retry chain
- **Preconditions**: E0; a run with a failing job.
- **Steps**: Open the detail page in one click from an L2 event → click [Retry].
- **Expected**: The detail page goes straight to the corresponding object; the Retry full chain works (including confirmation and result feedback).

### F L0 tile behavior

#### F1 | B-701① | Click does not steal focus
- **Preconditions**: E0; an L0 floating tile (settings toggle or Pin).
- **Steps**: Focus a foreground app (e.g. Notepad) → click the tile.
- **Expected**: The foreground app's focus is not stolen (you can keep typing).

#### F2 | B-701①b | Blank click falls through to the desktop (same ledger line as F1)
- **Preconditions**: Same as F1.
- **Steps**: Click the blank area of the tile window.
- **Expected**: The click passes through to the desktop (the tile does not eat clicks).

#### F3 | B-701② | Two tiles update independently
- **Preconditions**: Tiles of two different-source widgets.
- **Steps**: Change only one source's state.
- **Expected**: The two tiles update independently of each other; the windows do not enter the taskbar / Alt-Tab.

#### F4 | B-701②b | Floating-form drag placement restore
- **Preconditions**: Floating form enabled.
- **Steps**: Drag a tile → restart.
- **Expected**: One window per widget is draggable; positions are restored after restart; mutually exclusive with the host panel (either/or).

#### F5 | B-702① | Pin dual-entry consistency
- **Preconditions**: E0.
- **Steps**: Pin / unpin the same widget via the settings toggle and the context menu respectively.
- **Expected**: Both entries behave identically and persist.

#### F6 | B-702② | Non-admitted types cannot be pinned
- **Preconditions**: E0.
- **Steps**: Try pinning a non-PinSupported type (e.g. a claude.usage-class local card) in both entries.
- **Expected**: Both entries refuse to pin (the policy unit test has passed; this item verifies the UI gating).

#### F7 | B-703① | Position / collapsed-state restore + monitor unplug
- **Preconditions**: Two or more L0 tiles.
- **Steps**: Drag positions, collapse → restart; then unplug the monitor where a tile lives → plug it back.
- **Expected**: After restart, positions / collapsed states are restored; after unplug/plug the tile is not lost (recycled or returned to a visible area).

#### F8 | B-703② | Cross-DPI dragging and recycling
- **Preconditions**: C-DPI multiple monitors.
- **Steps**: Drag a tile across DPI-different monitors; shrink the secondary monitor's resolution.
- **Expected**: Dragging sizes are correct (DpiLayoutMath as the single source of truth); after shrinking the resolution the tile is recycled into a visible area.

#### F9 | B-704 | Offline self-healing
- **Preconditions**: L0 tile; C-DISCONNECT.
- **Steps**: Cut the network and observe the whole flow → restore.
- **Expected**: No popup windows at any point (Offline state only); after restoring the tile self-heals with new data (the socket-level integration test has passed; this item verifies the UI half).

#### F10 | B-705① | Critical only pulses
- **Preconditions**: E0; inject Critical (drag the threshold to an extreme or manufacture an Error+ state).
- **Steps**: Observe L0 after injecting.
- **Expected**: L0 only changes color and pulses, no interruption (no popup, no focus stealing); the Toast is still emitted by the notification engine as usual.

#### F11 | B-705② | RFC §6.2.7 four-item checklist
- **Preconditions**: Same as F10.
- **Steps**: Execute the four-item checklist in `docs/acceptance-B705-L0-behavior.md` item by item and record.
- **Expected**: All four items pass with records kept.

### G Appearance and motion

#### G1 | B-706① | Severity colors sync across three surfaces
- **Preconditions**: E0; L0 tile + capsule + tray all visible at once.
- **Steps**: Change a severity color in settings (e.g. Warning to orange).
- **Expected**: The L0 tile, capsule, and tray icon all change color in sync.

#### G2 | B-706② | colorOverride single tile
- **Preconditions**: Two or more tiles.
- **Steps**: Set colorOverride on one Widget only.
- **Expected**: Only that tile changes color; priority Widget > severity > default is correct.

#### G3 | B-706③ | Illegal color fallback
- **Preconditions**: Same as G2.
- **Steps**: Enter an illegal color value (e.g. `#GGG`) → restart.
- **Expected**: Falls back to the default color without crashing; keeps the fallback state after restart.

#### G4 | B-707① | Color-change transition
- **Preconditions**: Motion tier reduced.
- **Steps**: Manufacture a Success→Error change.
- **Expected**: Smooth color-change transition (not an instant jump).

#### G5 | B-707② | Critical pulse three tiers
- **Preconditions**: Motion settings page.
- **Steps**: Look at the pulse on the default tier; look at full stillness on the off tier; look at the breathing light on the full tier and adjust intensity.
- **Expected**: Pulse on by default; off is fully still; full shows a visible breathing light and the intensity is adjustable.

#### G6 | B-707③ | Motion boundaries
- **Preconditions**: Motion enabled.
- **Steps**: Listen for sound / try to steal focus during motion; after hiding / collapsing the tile, check power draw (Task Manager CPU/GPU).
- **Expected**: No focus stealing, no sound; no animation loop while hidden / collapsed (power draw falls back).

### H Import/export and settings

#### H1 | B-802 | Import/export end-to-end
- **Preconditions**: E0; an instance with credentials configured.
- **Steps**: Export → search the whole file for tokens; import in another directory (or VM) → re-enter secrets → compare behavior.
- **Expected**: The exported file contains no token anywhere (JSON stores only credentialRef, secrets are not packaged); after import + re-entry the behavior matches the original machine (the storage layer's 5 tests have passed; this item verifies the end-to-end half).

#### H2 | B-805① | Palette / motion live preview persistence
- **Preconditions**: E0.
- **Steps**: Change color / change tier → look at the live preview → restart.
- **Expected**: Changes take effect immediately and persist, still there after restart.

#### H3 | B-805② | Reset defaults + off tier
- **Preconditions**: Same as H2.
- **Steps**: Click reset defaults; select the off tier and look at the preview.
- **Expected**: One-click restore of defaults; the off tier preview is fully still.

### I Final acceptance

#### I1 | B-803 | MVP full-journey walkthrough
- **Preconditions**: Everything above (or key samples) passed.
- **Steps**: Record screenshots / a recording per the task-book §success criteria, archive under `docs/` (the walkthrough script is in `docs/acceptance-B803-e2e-walkthrough.md`).
- **Expected**: Full recording archived; all found problems fixed or filed as Issues.

#### I2 | SHA256 and timestamp cross-check (companion item, not counted in the 53)
- **Preconditions**: E0.
- **Steps**: Compare the download-page hash with the local zip.
- **Expected**: Consistent (the release assertion has been verified; this item is the installation-side recheck).

---

## Appendix: local automation coverage notes (2026-10-10 triage commit 4b04205 + 批3-补39/42/45/47/48/49 additions, currently 991/991 green)

Windows runtime behavior is governed by the installation walkthrough in this checklist; the following records what has already been proven locally, to avoid repeated troubleshooting when receipting:

- **Two items already checked (fully proven by local automation)**:
  - B-801: SerializationRoundTripTests (AppConfig full-field round-trip + defaults invariant) + JsonConfigurationStoreTests.AppConfig_AllFields_SurviveSaveAndReload (startup path field-by-field comparison) + the token-does-not-land-in-JSON unit tests (DpapiSecretStore/ImportExport).
  - B-207: OutageRecoveryIntegrationTests — real HttpClient connection refused→Offline+stale state (old cache kept); recovery auto-resets; RefreshWidgetAsync returns false without throwing; first failure with no cache shows "拉取失败" directly.
- **Local halves of unchecked items (UI/interaction halves need a real machine, keep them on the ledger without proxy verification)**:
  - B-706×3: PaletteResolverTests (priority Widget>severity>default / illegal value fallback / offline key) — color linkage and persistence UI half pending a real machine.
  - B-702 second item: WidgetDisplayPolicyTests.PanelCarries_NonPinSupported_NeverCarried — both entries' interaction pending a real machine.
  - B-703 geometry: PinLayoutMathTests (anchor / DPI / out-of-bounds recycling / topology judgment) — dragging and monitor unplug/plug pending a real machine.
  - B-207 branch supplement: WidgetHostTests fake Handler (Degraded / backoff / healthy de-duplication).
  - B-301/B-302/B-303: GitHubApiClient/GitHubPullRequests/GitHubActions fake Handler full branches (批3-补14/15) — real credentials and real repository halves pending a real machine.
  - B-401/B-404: ActionRunnerTests 15 tests (routing/confirm/timeout/cancel/retry six states) + LocalCommandExecutorTests (timeout kills process + persistence) — dialog and feedback UI halves pending a real machine.
  - 批3-补42 DSL: ProviderDslTests 46 tests (parse/validate/cache/MappingValue/end-to-end) + ActionRunner retry 6 tests — real-machine dsl_path configuration and action-click halves pending.
  - 批3-补47 update check: UpdateCheckTests 48 tests (version parsing shapes and rejection table, SemVer 12 comparison groups including pre-release rules, Evaluate including nightly date conventions, rate-limit degradation) — the GitHub real-endpoint half (three-line display, button semantics, offline degradation) pending a real machine.
  - 批3-补48 tile text-width budget + multi-window quota: TileSizingTests 32 tests (text-width model per type / per-type worst-row → total width / label priority aligned to PinTile / machine show linkage) + WidgetValueHintTests multi-window 8 tests (three-window / two-window / out-of-order / remaining convention / single-window not entering / readout collapse judgment) — the visual half that the floating panel's numeric column no longer clips text pending the installation walkthrough.
