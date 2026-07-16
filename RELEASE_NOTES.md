# Quiver v2.3.9

Steam Deck Gaming Mode detection is corrected, text fields are easier to navigate with a controller, and Add New Entry opens the on-screen keyboard again. Linux app removal also prefers the system Trash more reliably.

## Steam Deck

- **Gaming Mode detection** — Minimize is hidden only under Gamescope (`XDG_*` / `GAMESCOPE_WAYLAND_DISPLAY`). Bare `SteamOS` / `SteamGamepadUI` no longer count, so Desktop Mode (KDE) keeps the minimize button.
- **Press A to edit** — D-pad highlights text fields without opening Steam’s keyboard. Press A (Confirm) to focus the field and open the OSK. First B leaves edit mode; second B closes the overlay.
- **Add New Entry / Edit Entry + OSK** — Opening the create/edit form on Steam/Deck focuses Name and opens the keyboard. Moving between fields stays highlight-until-A.
- **Overlay Confirm** — While Entry Form, Edit Tags, or Display Filter is open, A always activates the highlighted control even if gamepad focus briefly drifted underneath.

## Gamepad

- **Add Catalog Source** — Modal navigation includes the URL field, no odd edge wrapping, proper `gamepad-focused` rings, and OSK on Confirm for the text box.
- **Top bar Up** — Up from the top bar is consumed so Sort By no longer falsely highlights underneath.

## Linux

- **Trash on remove** — Prefer `gio trash` when available, with a FreeDesktop Trash fallback (`XDG_DATA_HOME`, volume trash, copy across filesystems) so removals land in Trash more often instead of hard-deleting.
