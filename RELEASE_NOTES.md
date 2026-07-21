# Quiver v2.3.12

Polish for updates and controller/keyboard flows: live progress when updating apps from the review screen, clearer update prompts, tag-filter Options with reorder, and keyboard navigation on Yes/No dialogs.

## App updates review

- **Progress while updating** — The App Updates review shows status text, percent, and a progress bar on each row while an update downloads and installs. Action buttons disable mid-update; in-progress rows stay visible until finished.
- **Clearer prompt** — The updates available dialog asks “Review this update now?” / “Review these updates now?” instead of the longer “Open the App Updates review…” wording.

## Tag filters (sidebar)

- **Options menu** — With a tag filter focused, Options (gamepad Y / keyboard Options) opens Edit, Move Up, Move Down, and Delete.
- **Reorder without dragging** — Move Up / Move Down work from that menu (mouse overflow menu too). First/last items disable the unavailable direction. Drag-to-reorder is unchanged.
- **Focus chrome** — Overflow and reorder handles appear when a filter row is gamepad-focused.

## Modal dialogs

- **Keyboard navigation** — Arrow keys move between Yes/No (and other dialog buttons) while a modal is open; Confirm and Cancel use your bound keys. Focus rings show for keyboard navigation the same way they do for gamepad.
