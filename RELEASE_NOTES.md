# Quiver v3.0.0-rc.1 (prerelease)

Major packaging release: Quiver now ships with [Velopack](https://docs.velopack.io/) for self-update across Windows / Linux / macOS. Marked as a GitHub **pre-release** for testing. Not promoted to `/releases/latest`.

## Breaking: upgrading from 2.4.x

In-app update from **2.4.2 or older** cannot install Velopack 3.0. Download a fresh portable from this release and copy your library over manually.

**See [MIGRATING.md](https://github.com/tgeorgiadis/quiver/blob/v3.0.0-rc.1/MIGRATING.md)** for what to copy (`apps.json`, `settings.json`, `Apps/`) and per-OS folder layouts.

## Why Velopack?

- **Standard self-updates** across Windows, Linux, and macOS (replaces the custom zip-and-helper-script flow).
- **Fewer false threat detections.** The old helper script (for example a `.cmd` on Windows) often tripped OS / antivirus heuristics. Velopack applies updates without that script.
- **Signed Windows builds.** Packages are digitally signed. As reputation builds, SmartScreen warnings should become less common — though you may still see some early on.

## Distribution

- **Windows (primary):** `Quiver-win-Portable.zip`. Extract anywhere. Self-updating. Library data stays in the folder root (sibling of `current/`).
- **Linux:** `Quiver-linux-*.tar.gz` (x64 / ARM64) containing the AppImage in a folder. Extract, then run the AppImage. Library beside the AppImage when that folder is writable. Otherwise `~/.local/share/Quiver/`.
- **macOS:** `Quiver-osx-*-Portable.zip` (x64 / ARM64). Library beside `Quiver.app` when that folder is writable. Otherwise `~/Library/Application Support/Quiver/`.

Also includes Velopack feed files (`releases.*.json`, `.nupkg`) required for in-app updates. Setup installers are omitted (portable-first). Bare Linux AppImages are not published — use the `.tar.gz`.

## Updates (3.0 and later)

- Self-update via Velopack and GitHub Releases (no separate `Quiver.Updater.exe` or apply scripts)
- Prerelease Quiver updates: opt-in in Settings → Advanced (**Include prerelease Quiver updates**). RC builds (version with `-`) already follow prereleases automatically.
- User data is never stored inside the replaced app content (`current/`, AppImage mount, or `.app` bundle)

## App releases from GitLab

- Optional `repositorySource` on app entries (`github` default when omitted, or `gitlab`)
- Add/Edit App includes a **Repository Source** dropdown (GitHub first)
- GitLab apps use gitlab.com release asset links hosted on GitLab only
- Optional **GitLab API Token** in Settings → Advanced

## Windows signing

When Azure Artifact Signing is enabled, CI signs Windows packages during `vpk pack` using the GitHub Environment **`signing`** (OIDC subject `repo:…/quiver:environment:signing`).

```powershell
Get-AuthenticodeSignature .\Quiver.exe
# Expect Status = Valid when signed builds are published
```
