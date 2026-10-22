
# Store submission pre-checklist

Beacon itself remains **unpackaged and self-contained** (RFC §10, `WindowsPackageType=None`) — a store submission only adds an MSIX package that wraps the whole publish output, with an in-package Identity for the store to recognize; the app's runtime shape does not change. This page records three things: how the MSIX is produced, what fields the privacy-policy page needs, and what store assets to prepare.

## Status (2026-10-12)

| Item | Status |
|---|---|
| SignPath free signing | **Parameters wired for real** (2026-10-12): `api-token` and `organization-id` go through repository secrets (`SIGNPATH_API_TOKEN` / `SIGNPATH_ORGANIZATION_ID`, nothing hard-coded), with `project-slug: beacon` and `signing-policy-slug: beacon`; `artifact-configuration-slug` is an optional parameter — omitting it uses the project's default Artifact Configuration (fill it in later if needed, once a Portable Executable + deep signing configuration has been created in the console). **One user-side step remains (not done by an agent)**: in the SignPath console, add the user who owns the API token to the **Submitters** list of the "beacon" signing policy — once done, the next `v*` tag onward signs automatically; until then a version-tag build goes red at the signing step (fail closed; an unsigned official release is never shipped silently). Policy in the README "Code signing policy" section |
| MSIX packaging | **Verified on a Windows runner with byte-level package checks** (2026-10-12, two green dry runs, manifest version replacement observed to happen): MakeAppx in place, the manifest accepted, a package of about 66 MB produced. The packaging logic shares `.github/workflows/scripts/msix-pack.ps1` with the daily-build version-tag channel |
| Store app reservation | Not started. `Identity Name` / `Publisher` in the `appxmanifest` can only be fixed after reserving the app name in Partner Center |
| Privacy policy page | **Ready**: <https://cuihairu.github.io/beacon/privacy-policy/> (`docs/privacy-policy.md`, published by the docs workflow; the field mapping is in section 1) |
| Store assets | Icon prepared; screenshots / description / category / rating questionnaire not prepared (see section 2) |

## 1. Required fields of the privacy policy page

Store rule: an app that accesses personal data or has network capability must provide a **publicly accessible** privacy-policy URL. Beacon has network access (the GitHub API + AI-platform usage endpoints) and stores API tokens locally, so this is mandatory.

The page must state at least these fields (left column = the field, right column = Beacon's actual answer, copyable):

| Field | Beacon's actual answer |
|---|---|
| What data is collected | API tokens (GitHub PAT, AI-platform keys); user configuration (connections, refresh intervals, theme, widget layout); local machine readings (CPU / memory / disk / network speed / IP) |
| Where the data lives | All local: tokens go into the DPAPI secret store; configuration goes to `%AppData%\Beacon\config.json` (only a credentialRef is stored, **never a plaintext token**); logs go to `%AppData%\Beacon\logs` |
| How data leaves the machine | Sent only to the API endpoints the user configured (GitHub API, AI-platform console/usage endpoints); sent to no third party, no telemetry reporting |
| What the data is used for | Local display of state and local execution of actions only; tokens are used only to authenticate queries against the corresponding platforms |
| Retention and deletion | Removed with the install directory; uninstall through the system "Add or Remove Programs"; no cloud copy |
| Contact | To fill in (email or GitHub profile) |

Landing requirements:

- [x] The public URL is ready: `https://cuihairu.github.io/beacon/privacy-policy/` (`docs/privacy-policy.md`, published by the docs workflow — submit exactly this)
- [ ] Before submission the URL must already be reachable (the store crawls it)
- [ ] It must not be a `localhost` / intranet address / a page that requires login

## 2. Store asset checklist

| Item | Requirement | Beacon status |
|---|---|---|
| App icon | 310x310 PNG; the manifest `Logo` (small store-listing icon) uses 50x50 | Ready: `packaging/msix/Square310x310Logo.png`, `StoreLogo.png` (rasterized from `docs/img/logo.svg`; the same batch also produced 44/71/150/310x150 for in-package tiles) |
| Screenshots | At least 1, 4+ recommended; PNG or JPG; common size 1920x1080 (1366x768 / 2560x1440 also accepted); per-file size limit per the submission page | Not prepared. Bases can be picked from existing evidence screenshots in `docs/img/evidence/`, but those are mostly dark-theme close-ups; light-theme, multi-screen, and settings shots are still needed |
| App description | Up to 10000 characters; cover positioning / features / supported scenarios, keywords allowed | Not prepared. Can be adapted from the README install section + the positioning section of `docs/mission.md` |
| Category | Primary + secondary category, chosen from the store's list (Developer tools / Productivity are candidates) | Not decided |
| Age rating questionnaire | The IARC questionnaire answered item by item: violence, horror or fear, crude language, tobacco/alcohol, sexual content or nudity, user interaction, in-app purchases | Unanswered. Beacon has no content-risk items; the expected result is "Everyone" |
| Support URL | Required (GitHub issues works) | Not decided |

> Exact size limits, allowed category values, and questionnaire wording follow the Partner Center page at submission time — the numbers in this table are the common conventions; check against the page once before submitting.

## 3. MSIX packaging: blockers and alternatives

The packaging chain (`.github/workflows/nightly.yml`, `v*` tag channel only):

```
publish/ (self-contained output) + packaging/msix/appxmanifest.xml (version replaced per tag)
  → staging directory with AppxManifest.xml + all publish files
  → makeappx pack /d <staging> /p <asset>.msix
  → into SHA256SUMS + gh release upload
```

- **Version alignment**: `Version="0.0.0.0"` in the manifest is a placeholder, replaced by the workflow with the four-segment form of the `v*` tag (`v0.1.3` → `0.1.3.0`); the exe's own `Version` gets the same tag version injected at the publish step, so the two are always equal.
- **The unsigned MSIX is deliberate**: the store signs it at submission (Microsoft's signature); locally it is not signed. Sideload testing needs your own development certificate (`makeappx pack /o` + a self-signed certificate) — that is a local pre-submission verification step, not done in CI.
- **Placeholders that must be replaced before submission**: `Identity Name="Beacon.CuiHairu"` → the app name reserved in Partner Center; `Publisher="CN=cuihairu"` → the submission certificate subject (consistent with `PublisherDisplayName`).
- **Windows runner field test (2026-10-12, two green dry runs: 38230149697 + 38233094172)**:
  - MakeAppx is present on the runner: `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x86\makeappx.exe`
  - The hand-written manifest was accepted by MakeAppx (namespaces / `uap:VisualElements` / `uap:DefaultTile` / `rescap:runFullTrust` all valid); packing completes in about 10 s
  - Package-level byte verification (artifact downloaded and unpacked locally): the zip container is valid, about 66 MB / 502 entries; `AppxManifest.xml` + 6 Logos are all at the package root with byte counts matching the repository files one by one; `Beacon.App.exe` and `Assets\` entered the package from publish
  - The version-replacement path was observed live (run 38233094172, the dry-run harness deliberately passed 9.9.9.9 ≠ the placeholder): the in-package Identity actually became `Version="9.9.9.9"` and the placeholder `0.0.0.0` disappeared — the "version aligned to the `v*` tag" mechanism is proven to really happen, not an empty replacement
- **The only blocker at the time was "no way to dry-run"**: the MSIX step runs only in the `v*` tag channel, and version tags are given manually by the author (a repository red line: no tags); dry-running with an existing tag would upload another commit's assets onto an already-published release, violating delivery integrity. The solution was to extract the shared script `.github/workflows/scripts/msix-pack.ps1` plus a dry-run harness `.github/workflows/msix-dry-run.yml` that only builds the package and never touches the release or tags.

If the packaging chain breaks in the future, alternatives (in recommended order):

1. **Sparse package** (`WindowsPackageType=Sparse`) — the WinUI 3-supported mode of "has package identity but still runs unpackaged": add `WindowsPackageType=Sparse` + `PackageCertificateKeyFile` to the csproj and the appxmanifest is generated by MSBuild automatically (no hand-writing). Cost: local development and the portable form would have to switch mode along with it.
2. **App Installer distribution** (`.appinstaller`) — an enterprise / personal distribution channel that bypasses the store.
3. **Zip + setup.exe only** — the store is not a distribution requirement: the portable zip + Inno Setup installer already cover personal distribution; MSIX exists purely for listing.
