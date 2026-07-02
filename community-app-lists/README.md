# Community App Lists (samples)

This folder contains **sample** third-party catalog JSON files for Quiver development and testing.

The official community catalog for end users is published separately:

**[tgeorgiadis/quiver-community-app-catalog](https://github.com/tgeorgiadis/quiver-community-app-catalog)**

Fresh Quiver installs subscribe to that repo automatically as **Quiver Community App Catalog**.

External lists are **reference catalogs only**. A user's local `apps.json` is their library. Subscribe to a list in **App Catalog → Add Source**, then use **Review changes** to add or update apps deliberately.

## List file format

Each list is a JSON file with a required top-level **`version`** field and an **`apps`** array:

```json
{
  "version": "1.0.0",
  "apps": [
    {
      "name": "Example App",
      "repository": "org/repo",
      "folderName": "ExampleApp",
      "installPath": null,
      "appIconUrl": "https://example.com/icon.png",
      "preferredVersion": null,
      "tags": ["example"]
    }
  ]
}
```

- **`version`** — opaque string bumped by maintainers when content changes (semver recommended). Compared with string equality to detect updates.
- **`apps`** — same entry schema as local `apps.json`.

Lists without a `version` field still work: the launcher falls back to a content hash for update detection until maintainers add `version`.

## Publishing

Publish this folder as its own GitHub repository. Users add individual list URLs/paths as catalog sources (for example `https://raw.githubusercontent.com/YOUR_ORG/YOUR_REPO/main/n64-recomp.json`).

The bundled `index.json` is a convenience manifest for documentation and tooling; the launcher no longer browses it automatically.

## Adding a list

1. Add a new `*.json` file with `version` and `apps`.
2. Bump `version` whenever entries change.
3. Optionally add an entry to `index.json` for discoverability.

## Update detection

When a user adds a source, the launcher caches the fetched JSON and records `CachedListVersion` and `AcknowledgedListVersion`. On refresh, if the upstream `version` differs (or the content hash changes for legacy lists), **Review changes** shows a per-repository diff with actions:

- **Add** — copy an external-only app into local `apps.json`
- **Replace** — overwrite local catalog fields from external
- **Merge** — external metadata + union of tags; keeps local `skippedUpdateVersion`
- **Dismiss** — acknowledge the version without applying changes

Installed files on disk are never deleted when apps are removed from the library.
