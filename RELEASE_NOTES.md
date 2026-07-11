# Quiver v2.3.5

App updates, install extras, and Steam Deck / Linux polish. Checking for updates opens an in-window review instead of a broken modal, and catalog installs can ship small companion files like `portable.txt`.

## App Updates review

- **In-window review** — Pending app updates open in the main Library view (like catalog review), not a separate popup. Update, Skip, Versions, and download/executable menus stay on MainWindow so they work with mouse and controller.
- **Update All** — Runs updates one after another; auto-picks the recommended platform download when it can, otherwise pauses on that app’s download menu until you choose.
- **Skip all / Back to Library** — Clear the queue or leave review without updating.
- **Gamepad** — Navigate the update list, row actions (Update / Skip / Versions), and toolbar without the old modal Confirm-closes-everything behavior.

## Files to add

- **`filesToAdd` in catalogs** — Catalog entries can list small files to create next to an install (for example `portable.txt` for N64 Recomps). Synced on install, edit, and catalog apply; shown in the Create/Edit Entry form and catalog review diffs.

## Fixes & UX

- **Continue + Close After Launch** — Continue now respects Close After Launch the same way other launch paths do.
- **Download picker width** — Release download menus no longer crush long asset names (especially on Steam Deck).
- **Linux on-screen keyboard** — Dismisses text focus before closing after launch so the OSK does not stick around.
- **Catalog review filters** — Up from filter chips goes to bulk actions, then Top Bar (two-row navigation instead of jumping straight out).
