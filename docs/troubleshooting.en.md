
# Troubleshooting (FAQ)

> The troubleshooting manual for installation and launch problems. Use it together with the [README download section](https://github.com/cuihairu/beacon#download) and the [B-803 end-to-end walkthrough](acceptance-B803-e2e-walkthrough.en.md).

## Nothing happens on double-click?

Beacon is a **tray-resident application with no main window** — the visible proof of a successful start is: a status capsule in the bottom-right corner + a startup notification bubble + the tray icon (a red circle b). Only if none of the three appear should you work through this order:

1. **SmartScreen blocked it**: an unsigned exe is blocked on first run — click "More info" → "Run anyway".
2. **Confirm the process exists**: search Task Manager for `Beacon.App`. If it is there but the capsule is invisible, save the log and file an issue.
3. **Check the logs**: `%AppData%\Beacon\logs\beacon-*.log` (runtime logs, rolled daily).
4. **Startup failure dialog / crash log**: an error during startup **pops an error dialog** (with the stack and the log path) and also writes `%AppData%\Beacon\logs\crash-*.log` — file an issue with the dialog screenshot or the log content and it can be located.

## Behaviour did not change after an upgrade?

Beacon is **single-instance resident**: the installer overwrote the files, but the tray still holds the old process, and a second launch only notifies the old instance — "it says updated but behaves the same" is usually this.

1. Right-click the tray icon → **Exit** (the only exit path), then start again.
2. Settings → bottom of the General section, read the **version line**: a nightly looks like `2026.10.9+<full commit sha>` (a four-segment `yyyy.M.d` date taken from the build day plus `GITHUB_SHA` at publish time; the earlier `0.0.yyyyMMdd` shape triggered CS7034 and was changed) — compare it with the build date.
3. The version line still shows the old value = the new package was not installed (re-download the nightly artifact; see the verification below).

## Portable zip verification

`Get-FileHash beacon-nightly-windows-x86_64.zip -Algorithm SHA256` compared line by line against the `SHA256SUMS` in the release; with Git Bash installed, `sha256sum -c SHA256SUMS` also works.

After extracting, run `Beacon.App.exe` directly — it is a self-contained package, no .NET runtime install and no VC++ runtime needed (the published artifact was measured to depend only on components that ship with Windows).

## Installer

Installs to `%LOCALAPPDATA%\Beacon` (no administrator needed), launches from the Start menu, and uninstall goes through the system "Add or Remove Programs". The installer wizard is entirely in Simplified Chinese.

Installer checkboxes or labels that render wrong (overflow, unreadable) are usually an older build — the nightly rolls forward, so re-download the latest `setup.exe`.

## Widgets show no data (blank-panel diagnosis)

A widget added by "one-click add" or the "widget wizard" **should land on the board with numbers within seconds** (adding pins it and pulls immediately — no forced refresh or restart). If the board is blank, walk the jumps (each jump leaves a line in the runtime log):

| Jump | Log keyword | What "blank" means |
|---|---|---|
| ① widget created | `一键示例：组件 … 已创建并钉选` | This line missing = the add action never happened (a missing connection shows a red hint) |
| ② register + first pull | `组件注册已重建并立即拉取 N 个` | Missing = the scheduler never received the new widget (an older-version bug; upgrade the nightly) |
| ③ pull result | `Widget … refreshed ok: …` (success) / `refresh failed: …` (failure, with a reason) | The failure reason is displayed directly in the tile value slot ("拉取失败：…") — network / TLS / 401 / field-mapping problems are all readable at a glance |
| ④ render | the tile shows a gray light + "加载中…" → switches to the severity color once data arrives | Stuck on "加载中…" = ③ is still failing; read the failure reason line |

Two more places to check: whether the widget carries a "已钉" marker in its module's widget list (Settings → the corresponding module); and if a widget shows no data and the log is completely empty, confirm the module switch (the tree on the left) is on — widgets of a disabled module do not occupy board space.

## Xiaomi MiMo card shows Offline / no data?

First separate two things: **the "Offline" banner on the capsule is an aggregate alarm** — any unhealthy connection (including Degraded) lights it; the real failure reason is in the **tile value slot** ("拉取失败：…") and in the `refresh failed` lines of the runtime log. Three common MiMo cases:

1. **"控制台 Cookie 已失效或未录制"**: the usage card's login session expired (session cookie expiry is the normal state). Re-record: log in to `platform.xiaomimimo.com` in a browser → F12 → Application → Cookies → copy the value of `api-platform_ph` → Settings → the Xiaomi module connection, paste it (pasting the whole Cookie line works too; that key is picked out automatically).
2. **Only the API Key was entered**: the Key only authorizes model calls (a green `/v1/models` connection test ≠ a valid Cookie — they are two separate credential chains), so **the quota must be recorded separately as the console Cookie** — requests carrying only the Key as Bearer or a raw header return 401 in practice.
3. **LiteLLM gateway accounting tier**: the connection setting `litellm_spend_url` **defaults to empty = nothing is sent out (normal, not a fault)**; if it is set but the card shows "未接数据库", your local LiteLLM gateway config has no database attached — the spend side returns `no_db_connection` throughout, and a DB on the gateway side (SQLite is enough) is needed before "本月已用 $X" appears.

## Blank leftover floating window / brand icon not showing

- **After globally turning off the "count floating windows", a blank empty window remains on the desktop**: the older floating-window close only called WinUI `Window.Close`, which is not guaranteed to destroy the window handle on a borderless NOACTIVATE window — the content was torn down but the shell remained, which is the blank window. Fixed (if the handle is still alive after Close it is destroyed hard). After upgrading the nightly you must Exit from the tray and restart (single-instance resident). If it happens again it is a new problem — report it with the runtime log.
- **A brand icon in a floating window or in the settings tree is missing (only the light and text remain)**: the old `Viewbox(PathIcon)` combination measured to zero size under WinUI 3, rendered blank, and blocked the fallback glyph. Fixed (renders a `Path` shape directly, with a fallback for empty geometry).
- **Two-state screenshot evidence**: the "visual-evidence" artifact of every daily-build run — count floating windows on = the floating panel full-screen plus a six-times close-up of the icon; off = zero leftover windows on the desktop (with a hard assertion, so a regression turns the build red).

## DSL declaration file (`dsl_path`) error?

Once an `http` connection setting has a `dsl_path`, problems in the declaration file **are never silent**: the affected cards go straight to Degraded with a reason (startup and the other connections are unaffected; a failure to load declared actions is only written to the runtime log). Three common cases:

1. **"DSL 文件不存在"**: the path in Settings is wrong or the file was moved. Check the absolute path (including drive letter and username); after fixing it, the next refresh recovers.
2. **"DSL 文件解析失败（第 N 行：…）"**: the parser supports only a subset of YAML. Common traps — indentation with **Tab** (spaces only); an unquoted JSON literal (`body: {"a":1}` must be written as `body: '{"a":1}'`, and bare `{}` / `[]` are rejected as unsupported syntax); unclosed quotes; a mapping that uses a key outside the whitelist (a typo is reported, never silently ignored); list items must be `- key: value` (no bare scalar lists, no multi-document `---`). Fix the line it names; the syntax reference is the "Provider DSL" section of `configuration.md`.
3. **I changed the file but nothing happened**: the file is cached by mtime + size, so after saving (once the editor really writes to disk) the next refresh re-parses automatically. If your editor's "Save" did not change the mtime, trigger a refresh manually or use Refresh All from the tray.

## Log overview

| Log | Location | Contents |
|---|---|---|
| Runtime log | `%AppData%\Beacon\logs\beacon-YYYYMMDD.log` | refresh / notification / action execution plus per-hop widget pipeline diagnostics, rolled daily |
| Crash log | `%AppData%\Beacon\logs\crash-*.log` | unhandled exception stacks at startup and during the run |
| Configuration | `%AppData%\Beacon\config.json` and the rest | connections / widgets / pins / appearance (tokens are stored only as credentialRef, never in plaintext) |
