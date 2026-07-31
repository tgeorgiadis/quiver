# Quiver v2.4.2

Keep Quiver running in the tray with scheduled background update checks, opt apps into Auto Update, configure per-app Windows runners on Linux, and fix single-root archive installs.

## Background / tray

- **Close to system tray** — Closing the window keeps Quiver in the notification area instead of quitting (Settings → General)
- Tray menu includes **Open Quiver**, **Check for updates**, and **Exit**; click the tray icon to restore the window
- With **Close After Launch** and tray enabled, launching an app hides Quiver to the tray instead of exiting

## Auto Update

- **Check for app updates in the background** — Scan for updates on a schedule while Quiver is running, including in the tray (Settings → General)
- **Background check interval** — Choose how often background checks run (every 15 minutes through every day; default every hour)
- **Enable Auto Update for newly added apps** — New apps from the catalog or Add Entry start with Auto Update enabled
- **Auto Update** (app ⋯ menu) — Per-app toggle to install updates without the review prompt; apps without Auto Update stay pending for review

## Linux Windows runner

- **Per-app Windows Runner** — For Windows-only apps on Linux: ⋯ → **Launch Options** → **Windows Runner…**
  - **Auto (prefer Proton, then Wine)** — Uses the global Settings command when set; otherwise Proton, then Wine
  - **System Wine**, a detected **Proton** install, or a **Custom command** (`{exe}`, `{gamePath}`, `{exeDir}`)
  - Optional per-app **prefix / compatdata** path (defaults under the app folder)
- Prompted when downloading a Windows build if a runner is available (**Save & Download**)
- Settings → **LINUX WINDOWS RUNNER** remains a global override for apps set to Auto (blank = auto-detect Proton then Wine)

## Install / downloads

- **Single-root zip / tar.gz** — If an archive root is only one directory (no sibling files), its contents are extracted into the app folder (fixes nested wrappers such as `LodRecomp-v*-linux-x64/`)
