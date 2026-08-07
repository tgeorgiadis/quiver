# Quiver v2.4.3-rc.2 (prerelease)

Velopack packaging and self-update across Windows / Linux / macOS. Marked as a GitHub **pre-release** for testing — not promoted to `/releases/latest`.

## Distribution

- **Windows (primary):** `Quiver-Portable.zip` — extract anywhere; self-updating; library data stays in the folder root (sibling of `current/`)
- **Windows (optional):** `Quiver-Setup.exe`
- **Linux:** `.AppImage` (x64 / ARM64) — library beside the AppImage when that folder is writable; otherwise `~/.local/share/Quiver/`
- **macOS:** Velopack package (x64 / ARM64) — library beside `Quiver.app` when that folder is writable; otherwise `~/Library/Application Support/Quiver/`

Also includes Velopack feed files (`releases.*.json`, `.nupkg`, etc.) required for in-app updates.

## Updates

- Self-update via Velopack + GitHub Releases (no separate `Quiver.Updater.exe`)
- Prerelease Quiver updates: opt-in in Settings (**Include prerelease Quiver updates**); RC builds (version with `-`) already follow prereleases automatically
- User data is never stored inside the replaced app content (`current/`, AppImage mount, or `.app` bundle)

## Windows signing

When Azure Artifact Signing is enabled in CI, Velopack signs Windows packages during `vpk pack`.

```powershell
Get-AuthenticodeSignature .\Quiver.exe
# Expect Status = Valid when signed builds are published
```
