# Quiver v2.3.4

Controller and catalog-review polish release. Focus rings, overlays, and confirm actions behave more consistently across Settings, Library, and App Catalog.

## Gamepad / controller

- **Custom bindings** — Rebind Confirm, Cancel, Options, and navigation in Settings; refresh controllers and reset to defaults when needed.
- **Settings** — Left/Right switch tabs; A toggles checkboxes correctly; focus rings only show on the active control.
- **Overlays stay trapped** — Display Filter, Create/Edit Entry, and Changelog keep stick input inside the overlay. B / Options dismisses them and returns to the Library.
- **Changelog** — Opening Show Changelog closes the options menu, adds a Close button, and lets B return to the Library.
- **Context menus** — Options dismisses an open menu instead of stacking another; library card focus stays visible while a menu is open.
- **Combo boxes** — Opening a dropdown with A actually opens it for controller selection.

## App Catalog review

- **Add / row actions** — A on a review row enters the action strip first; a second A confirms Add/Replace/etc. After Add, the next app keeps a clear focus ring (and Add stays ready).
- **Filter chips** — Left/Right across All / Needs review / New / … shows an orange focus ring and starts on the filter chips (not bulk actions).
- **Needs review complete** — Back to Library and Back to sources are reachable with the controller and activate with A.

## Fixes

- Settings tick options no longer ignore A (CheckBox was incorrectly treated as a plain Button).
- Changelog no longer leaves the context menu stuck on screen with no way back.
- Review list no longer silently “selects” the next Add with no visible focus after adding an app.
