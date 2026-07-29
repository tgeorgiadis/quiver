# Quiver v2.4.0

Mod Manager is now in **Beta**, with Thunderstore and GameBanana support. Configure mod sources from the App Catalog (or Add New Entry), then open **Mods** from an app’s options.

## Mod Manager (Beta)

- **Thunderstore & GameBanana** — Browse, search, install, update, and uninstall mods for apps that define `mods.path` and `mods.sources`
- **Multi-source catalogs** — Infinite scroll / load-more across providers; merged search when multiple sources are configured
- **Dependencies** — Thunderstore dependency packages resolve and install with download enrichment when listing URLs are missing
- **Archives** — Zip and 7z installs (including GameBanana multi-file pickers)
- **NSFW filter** — Content-rated mods hidden by default; optional Include NSFW chip (persisted)
- **Card details** — Provider shown after the author as `[Thunderstore]` / `[GameBanana]`; Open uses the correct community package page

## Other

- **Announcement banner** — Dismissible notice under the top bar, loaded from remote `announcement.json` (update without a Quiver release)
- **Gamepad / keyboard** — Modal dialog navigation and focus improvements
- **Catalog** — Mod source fields in catalog sync / review when present on remote entries
