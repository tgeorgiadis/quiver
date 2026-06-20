# Community App Lists

This folder contains a publishable registry of third-party `apps.json` catalogs for Quiver.

## Publishing

You can publish this folder as its own GitHub repository (for example `QuiverCommunityAppLists`). Users point **Settings → General → Community index URL** at your hosted `index.json`:

```
https://raw.githubusercontent.com/YOUR_ORG/YOUR_REPO/main/index.json
```

For local development, use a file path instead:

```
C:\Projects\Quiver\community-app-lists\index.json
```

## Adding a list

1. Add a new `*.json` file in this folder using the same schema as `apps.json` (an `apps` array of entries).
2. Add an entry to `index.json`:

```json
{
  "id": "my-list",
  "name": "My List",
  "description": "Short description shown in the browse dialog",
  "location": "https://raw.githubusercontent.com/YOUR_ORG/YOUR_REPO/main/my-list.json",
  "listVersion": "1.0.0"
}
```

- **`id`** — stable identifier; used to detect duplicate subscriptions.
- **`location`** — URL or path to the list file (same format as manual catalog sources).
- **`listVersion`** — informational version for maintainers. The launcher detects updates via content hash, not `listVersion`.

## Update detection

When a user subscribes to a list, the launcher stores an **accepted snapshot** of that catalog. On each fetch, it compares a SHA-256 hash of the remote list to the accepted snapshot. If they differ, the user is prompted to:

- **Apply All** — sync to the new list (removals drop apps exclusive to that source)
- **Apply New Only** — add/update entries but keep previously listed apps even if removed remotely
- **Keep Current** — dismiss until the next startup or manual refresh

Installed files on disk are never deleted when apps are removed from the library.
