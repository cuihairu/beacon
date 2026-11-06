# Real-machine verification harness (tools/realmachine)

Everything in `todo.md` that says "needs a Windows desktop" is verified from this repo by driving a
real Windows machine over SSH — no interactive login at the keyboard is required for the parts
below, because the checks never depend on what the console currently shows:

| Piece | What it does |
|---|---|
| `extract-lib.sh` | Regenerates `lib.ps1` from `.github/workflows/scripts/visual-evidence.ps1` (preamble + helper functions), so the checks and the CI harness share one implementation. |
| `run-remote.sh` | Copies a check directory to the target box, runs it **inside the interactive session** (`schtasks /create /IT`), waits for the `###DONE` marker, prints the log and pulls `C:\beacon-verify\evidence\` back. |
| `checks/panel-upgrade.ps1` | PANEL-UPGRADE-1 acceptance: default 720x560 board, timeline/OVERVIEW/NOTIFICATIONS/NEEDS ATTENTION sections, header drag, edge resize, restart persistence, capsule click → board. |
| `checks/behaviour.ps1` | URL end-to-end open, Toast delivery + routing, explorer logon scheduling, RFC §6.2.7 structural checks. |

## Usage

```bash
# 1. one-time on the Windows box: a published build + a mock-capable config
#    (see tools/doc-gif/capture-config-effects.ps1 for the config conventions)

# 2. run a check
mkdir -p /tmp/run && cp tools/realmachine/lib.ps1 /tmp/run/   # from extract-lib.sh
cp tools/realmachine/checks/panel-upgrade.ps1 /tmp/run/run.ps1
tools/realmachine/run-remote.sh /tmp/run 10.240.0.5 admin 600 "C:/beacon-verify/evidence/"
```

Evidence lands in `C:\beacon-verify\evidence\` on the target and is pulled into `./evidence-out/`.

## Capture notes (why not plain screenshots)

`Graphics.CopyFromScreen` on the verification box returns a **stale, window-less frame** while the
console session shows the lock screen (the foreground window is `Windows 默认锁屏界面`), so
full-screen capture and injected mouse drags cannot be used as evidence there. The checks therefore
use two mechanisms that are independent of the displayed desktop:

* **`PrintWindow(..., PW_RENDERFULLCONTENT)`** — renders the live window content of the app itself
  (board, tiles, capsule, detail, tray window), used for every screenshot;
* **UIA (`Invoke`, `Transform.Resize`)** and **`PostMessage`** — used for every interaction that would
  otherwise need a mouse or keyboard.

Anything that genuinely requires a human at an unlocked, multi-monitor desktop (real drag gestures,
toast banner click, screen recording, cross-DPI two-screen moves) stays open in `todo.md` with the
reason recorded, instead of being ticked off on weaker evidence.
