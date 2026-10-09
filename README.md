[English](README.md) | [中文](README.zh.md)

<p align="center">
  <img src="docs/img/logo.svg" width="64" alt="Beacon logo">
</p>

<h1 align="center">Beacon</h1>

<p align="center">
  <a href="https://github.com/cuihairu/beacon/actions/workflows/ci.yml"><img src="https://github.com/cuihairu/beacon/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://codecov.io/gh/cuihairu/beacon"><img src="https://codecov.io/gh/cuihairu/beacon/branch/main/graph/badge.svg" alt="Code coverage"></a>
  <a href="https://cuihairu.github.io/beacon/"><img src="https://img.shields.io/badge/docs-online-8A2BE2" alt="Online docs"></a>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?logo=windows11&logoColor=white" alt="Windows 10/11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-3DA639" alt="License: Apache-2.0"></a>
  <a href="https://github.com/cuihairu/beacon/actions/workflows/daily-build.yml"><img src="https://github.com/cuihairu/beacon/actions/workflows/daily-build.yml/badge.svg" alt="Daily Build"></a>
</p>

> **Windows Developer Status & Action Center** — See what needs your attention.

A developer status and action center that lives on the Windows desktop: it aggregates GitHub / CI and other development-environment states, alerts you proactively, and offers one-click actions. **Signal + Action, not a Dashboard.**


<img width="688" height="1408" alt="demo" src="https://github.com/user-attachments/assets/db0ae7e4-2725-4f2d-9446-ae306afdac49" />


## Online Documentation

**[https://cuihairu.github.io/beacon/](https://cuihairu.github.io/beacon/)** — published automatically to GitHub Pages (mkdocs-material) by the [docs workflow](.github/workflows/docs.yml).

## Download

**[Nightly build](https://github.com/cuihairu/beacon/releases/tag/nightly)** — rolling builds from the main branch. The `nightly` tag is fixed and refreshed in place (old assets removed, new ones uploaded), so this page always reflects the current main branch. Self-contained Windows x86_64 packages (bundling the .NET 10 + Windows App SDK runtimes), with a `SHA256SUMS` checksum file. Direct links: [Installer setup.exe](https://github.com/cuihairu/beacon/releases/download/nightly/beacon-nightly-windows-x86_64-setup.exe) · [Portable zip](https://github.com/cuihairu/beacon/releases/download/nightly/beacon-nightly-windows-x86_64.zip).

Published automatically every early morning (Beijing time) by the [daily-build workflow](.github/workflows/daily-build.yml), and can also be [triggered manually](https://github.com/cuihairu/beacon/actions/workflows/daily-build.yml).

### Installation (Windows 10 1809+ / Windows 11)

**Option A · Installer**: download `beacon-nightly-windows-x86_64-setup.exe` and double-click to install — installs to `%LOCALAPPDATA%\Beacon` (no administrator rights required), adds a Start-menu entry, with an optional desktop shortcut and startup with Windows; uninstall through the system "Add or remove programs" page.

**Option B · Portable zip**: download the zip and verify it (`Get-FileHash beacon-nightly-windows-x86_64.zip -Algorithm SHA256` against `SHA256SUMS` in the Release), extract it to any directory and run `Beacon.App.exe` — the package is self-contained and requires no .NET runtime installation.

On first launch the app settles into the system tray and shows the status capsule (bottom-right of the screen) plus a startup notification balloon. Enter your GitHub PAT under tray menu → Settings → Connections (the token is written only to the DPAPI-protected secret store; the configuration JSON stores only a credentialRef and never plaintext). The global hotkey defaults to `Ctrl+Alt+B` to open the panel; startup with Windows is enabled by default (HKCU Run key, can be turned off in Settings). Permissions are regular user-mode only — read/write access to `%AppData%\Beacon\`, DPAPI encryption, `Shell_NotifyIcon`, and `RegisterHotKey`. No administrator rights required.

**Double-click does nothing?** → [Troubleshooting guide](docs/troubleshooting.md) (SmartScreen / tray with no main window / log locations / startup-failure dialog and crash logs — all on one page).

## Documentation

| Document | Content |
|---|---|
| [docs/mission.md](docs/mission.md) | Product positioning / core concepts / phased plan / MVP acceptance criteria (original brief excerpts in the same directory) |
| [docs/rfc/RFC-001-technical-design.md](docs/rfc/RFC-001-technical-design.md) | Technical design: architecture / domain model / **display-layer architecture (L0 floating widgets + four interaction layers)** / refresh / notifications / storage / risks |
| [docs/solution-structure.md](docs/solution-structure.md) | Solution / project structure / dependency rules / scaffold commands (runnable on Windows machines) |
| [docs/mvp-issues.md](docs/mvp-issues.md) | 43 MVP issues (P0–P8, with acceptance criteria and dependencies) |

## Roadmap

1. ✅ Task brief + technical RFC + solution structure + MVP issue list
2. ✅ M1 Windows shell (tray / capsule / hotkey / startup with Windows)
3. ✅ M2 Core kernel (refresh scheduling / status aggregation / cache / event bus)
4. ✅ M3 GitHub loop (PR / Actions providers, ETag + rate-limit awareness)
5. ✅ M4 Notifications and panel (action executors / toast / notification center / tray coloring)
6. 🔨 M5 L0 floating widgets + Quick Panel = MVP (code-complete; remaining: [B-803 on-machine walkthrough](docs/acceptance-B803-e2e-walkthrough.md) + B-804 clean-VM acceptance, pending a Windows machine)

> Build requirements: Windows + .NET 10 SDK + Windows App SDK (WinUI 3 does not support cross-platform builds).
