# Quiver 2.1.1

Bug-fix release for the launcher self-updater. If you are on **2.0.0** and never received a prompt to update to 2.1.0, install this release once manually — after that, in-app updates should work again.

## Fixes

### Launcher self-update

- **Manual “Check for updates”** now actually checks for Quiver updates (the App instance was never wired to the update button)
- **Startup update checks** no longer stop querying GitHub forever after the first “up to date” result; re-checks run after the 5-minute throttle
- **Manual checks** always fetch a fresh response from GitHub (no stale `If-None-Match` / 304 short-circuit)
- **Installed version** is read from `version.txt` instead of stale `update_check.json` cache

### Tests

- Added unit tests for update-check throttling, stale cache handling, and `version.txt` as the installed-version source of truth

---

## Upgrading

| From | What to do |
|------|------------|
| **2.0.0** | Download [Quiver 2.1.1](https://github.com/tgeorgiadis/quiver/releases/tag/v2.1.1) manually (auto-update from 2.0.0 was broken). Future updates should prompt in-app. |
| **2.1.0** | Use **Check for updates** or wait for the startup prompt — you should be offered 2.1.1 automatically. |
| **2.1.1** | You are up to date. |

## Download

Platform archives are attached to the [v2.1.1 GitHub release](https://github.com/tgeorgiadis/quiver/releases/tag/v2.1.1):

- `Quiver-Windows-x64.zip`
- `Quiver-Linux-X64.tar.gz`
- `Quiver-Linux-ARM64.tar.gz`
- `Quiver-macOS-x64.zip`

## Publishing (maintainers)

1. Commit and push the version bump to `main`
2. Create and push tag: `git tag v2.1.1 && git push origin v2.1.1`
3. CI builds platform assets and attaches them to the GitHub release
4. Paste this file (or the **Fixes** section) into the release description on GitHub
