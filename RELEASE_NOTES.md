# Quiver v2.4.3-rc.1 (prerelease)

Signing and Windows self-update smoke release. Marked as a GitHub **pre-release** so installed Quiver apps do not auto-update from `/releases/latest`.

## Windows signing / updater

- **Authenticode signing** — CI signs `Quiver.exe` and `Quiver.Updater.exe` with Azure Artifact Signing (Nova Labs)
- **Quiver.Updater.exe** — Windows self-update launches a signed updater instead of a temp `.cmd` script
- User data preserved across launcher updates: `apps.json`, `settings.json`, `games.json`, `Cache`

## Verify this build

```powershell
Get-AuthenticodeSignature .\Quiver.exe
Get-AuthenticodeSignature .\Quiver.Updater.exe
# Expect Status = Valid, publisher Nova Labs
```
