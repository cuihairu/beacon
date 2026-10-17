
# Beacon privacy policy

Last updated: 2026-10-12 · Applies to: v0.1.x (including daily builds)

## In one sentence

**Beacon does not collect data and uploads nothing to the developer's servers.** All data lives only on your own computer; network requests go only to the API endpoints you configure yourself and to this project's official repository update-check endpoint.

## What data is collected

Beacon stores three kinds of data **locally on your computer**, all of it entered by you or produced by running on this machine:

| Category | Contents | Source |
|---|---|---|
| Credentials | GitHub PAT, API keys of AI platforms (Zhipu / Kimi / DeepSeek / Volcengine Ark / Alibaba Qwen / Xiaomi MiMo / Claude / Codex / Copilot / OpenCode etc., depending on the connections you configure) | Entered by you in the settings UI |
| Configuration | Connection settings, refresh intervals, theme, widgets and pinned layout, actions (including optional webhooks) | Adjusted by you in the settings UI |
| Local machine readings | CPU / memory / disk / network speed / IP address etc., used only for local display | Sampled on this machine |

Beacon has **no** account system, **no** telemetry or analytics, and **does not** report any data to the developer or to any third party.

## Where the data lives

Everything is in the local user directory `%AppData%\Beacon\` (that is, `%APPDATA%\Beacon`, a normal user profile, no administrator needed):

| File / directory | Contents | How it is protected |
|---|---|---|
| `secrets.bin` | All credentials | **Encrypted with DPAPI (CurrentUser)** — only the same Windows account can decrypt it; the file itself contains no plaintext |
| `connections.json` | Connection settings | **Contains no plaintext credentials**: it stores only credentialRef references; the secrets themselves are in `secrets.bin` |
| `config.json` / `widgets.json` / `pins.json` / `actions.json` | Global settings / widgets / pins / actions | Plaintext JSON, no credentials |
| `cache\` | The most recent cached state of each widget | Plaintext JSON |
| `notifications.json` | Notification history and read state | Plaintext JSON |
| `logs\` | Runtime logs | Plain text (for troubleshooting) |

A configuration export/import package **never contains credentials** — the exported file holds only credentialRef references, and you must re-enter the secrets on the new machine.

## How data leaves this machine

Beacon makes only three kinds of outgoing network requests, all expected and all switchable:

1. **Update check** (on by default, no switch yet): 20 seconds after startup it sends one GET to this project's public repository endpoint `api.github.com/repos/cuihairu/beacon/releases/latest` (once per process lifetime; the manual "Check for updates" button forces a re-check) — the request carries only a User-Agent (`Beacon-Update-Check`) and your IP (which any network request carries), **and no personal data whatsoever**.
2. **Connection queries**: sent only to the API endpoints **you configured yourself** (the GitHub API, the console / usage endpoints of the AI platforms), authenticated with the key you entered. Beacon contacts exactly the endpoints you configured; delete the connection and it stops.
3. **Actions**: only when you have configured HTTP / webhook actions does Beacon send requests to the URL you specified (webhooks carry an HMAC-SHA256 signature header; the secret likewise lives only in DPAPI).

Apart from these three, Beacon opens no other network connection.

## What the data is used for

Local data is used only to: display state on your screen (the capsule, floating widgets, the quick panel), execute the local actions you configured, and maintain settings and notification history. Credentials are used only to authenticate queries against the corresponding platforms and for nothing else.

## Retention and deletion

- Data lives as long as the install directory does; uninstall through the system's "Add or Remove Programs" (or just delete the portable directory), then delete `%AppData%\Beacon\` to **completely remove all local data**, including the encrypted credentials.
- Beacon has no cloud copy — deleted is deleted.
- The import/export feature in the settings UI moves configuration only (no credentials) and does not affect this item.

## Contact

Questions about this policy or data handling:

- GitHub: [github.com/cuihairu](https://github.com/cuihairu) (filing an issue on [cuihairu/beacon](https://github.com/cuihairu/beacon) works)
