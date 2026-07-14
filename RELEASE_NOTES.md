# Quiver v2.3.7

Faster, more reliable gamepad navigation, a new sort mode that ignores leading articles, and safer app removal. Controller focus no longer gets stuck after emptying a catalog filter, and rapid D-pad taps register correctly.

## Sorting

- **Name (ignore The/A/An)** — New library and catalog-review sort option. Titles like *The Legend of Zelda* sort under **L** while still displaying their full name. Existing Name A–Z / Z–A stay literal.

## Gamepad navigation

- **No grid wrap** — On Library and App Catalog card grids, Right on the last column and Down on the last row no longer wrap around; focus stays on the edge (Up/Left still escape to top bar / sidebar).
- **Rapid D-pad taps** — Discrete presses no longer get dropped by a leftover 250 ms gate after release. Hold-to-scroll is slightly faster (500 ms first repeat, then 250 ms).
- **Stale orange focus** — Moving from App List review filters (or empty-state Back buttons) into the list clears the previous control’s focus ring.
- **Empty Disabled filter** — Enabling the last disabled App List no longer leaves Confirm (A) dead. Focus returns to the filter chips.
- **Dialog defaults** — Modal gamepad focus prefers the dialog’s `IsDefault` button when set.
- **Hints** — Controller hint text uses clearer `(Select)` / `(Options)` / `(Back)` / `(Navigate)` labels.
- **Menu focus** — Context menu items show a clearer accent focus border.

## Steam Deck / input

- **On-screen keyboard** — Focusing text fields on Steam Deck can open Steam’s OSK when available.

## App management

- **Recycle Bin / Trash** — Removing an app moves its install folder to the Recycle Bin (Windows), Trash (Linux), or Trash (macOS) instead of permanently deleting it.
