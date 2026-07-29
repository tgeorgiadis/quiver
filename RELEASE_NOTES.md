# Quiver v2.4.1

Maintenance release: Linux single-file installs, Discord footer link, Mod Manager focus polish, and gamepad input recovery after launching a game.

## Install / downloads

- **Extensionless Linux binaries** — Release assets without an extension (for example `CrashBandicoot_Linux`) are installed into the app folder instead of being left in temp and deleted
- **Linux-X64 matching** — Arch-unspecified Linux assets (name contains `linux` but no `x64`/`amd64`) now match Linux-X64; ARM and 32-bit builds stay excluded
- **Unsupported assets** — Unknown typed assets fail with a clear error instead of silently marking the install complete

## UI

- **Discord** — Sidebar footer Discord icon opens the Quiver community invite; GitHub is icon-only beside it
- **Footer navigation** — Gamepad/keyboard Left/Right moves between GitHub and Discord; Up/Down leave the footer strip instead of stepping between the two icons

## Mod Manager

- **Focus after install/filter** — Installing or uninstalling a mod updates status in place; list focus is restored after filter rebuilds instead of jumping or reloading card images

## Gamepad / input

- **Return from game** — Focusing Quiver again always reclaims gamepad/keyboard input (avoids stuck “launched game owns input” when process wait hangs)

## Other

- **Image cache** — Remote images use an on-disk AsyncImageLoader cache under `Cache/Images`
