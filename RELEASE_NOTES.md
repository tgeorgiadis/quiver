# Quiver v2.4.3-rc.3 (prerelease)

Velopack packaging and self-update across Windows / Linux / macOS. Marked as a GitHub **pre-release** for testing — not promoted to `/releases/latest`.

## Distribution

- **Windows (primary):** `Quiver-win-Portable.zip` — extract anywhere; self-updating; library data stays in the folder root (sibling of `current/`)
- **Linux:** `.AppImage` (x64 / ARM64) — library beside the AppImage when that folder is writable; otherwise `~/.local/share/Quiver/`
- **macOS:** Portable zip (x64 / ARM64) — library beside `Quiver.app` when that folder is writable; otherwise `~/Library/Application Support/Quiver/`

Also includes Velopack feed files (`releases.*.json`, `.nupkg`) required for in-app updates. Setup installers are omitted (portable-first).

## Updates

- Self-update via Velopack + GitHub Releases (no separate `Quiver.Updater.exe`)
- Prerelease Quiver updates: opt-in in Settings → Advanced (**Include prerelease Quiver updates**); RC builds (version with `-`) already follow prereleases automatically
- User data is never stored inside the replaced app content (`current/`, AppImage mount, or `.app` bundle)

## Windows signing

When Azure Artifact Signing is enabled, CI signs Windows packages during `vpk pack` using the GitHub Environment **`signing`** (OIDC subject `repo:…/quiver:environment:signing`).

```powershell
Get-AuthenticodeSignature .\Quiver.exe
# Expect Status = Valid when signed builds are published
```
