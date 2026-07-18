# Quiver v2.3.10

Gamepad and keyboard focus are cleaner: orange rings only appear when a controller is connected or you are arrow-navigating, mouse clicks no longer leave stuck rings on the top bar, and catalog/library keyboard confirm is consistent.

## Focus chrome

- **No pad, no orange rings** — Gamepad focus styles only paint when the window has `gamepad-chrome` (Enable Gamepad + a connected controller). Modal dialogs share the same gate.
- **Keyboard arrows still show rings** — Arrow-key navigation turns chrome on without a controller so library/sidebar/top-bar selection stays visible. A mouse click clears keyboard chrome when no pad is plugged in.
- **Top bar / buttons** — Plain `:focus` orange rings are gated under chrome; `:focus-visible` remains for Tab. Mouse clicks on Add New Entry, Settings, and window controls no longer leave a stuck ring.

## Keyboard confirm

- **Space and Enter** — Both confirm the highlighted control everywhere (except inside a TextBox). Matching KeyUp is suppressed so a newly focused CheckBox does not also toggle.
- **Catalog App List** — Confirm on a source card drills into Review (first button), not the Enabled checkbox, so Space no longer disables the list by accident. Enabled is still reachable with Left/Right in the action strip.

## Navigation fixes

- **Library + Continue** — Arrowing between library cards no longer dual-highlights the sidebar Continue button (Avalonia focus is cleared; arrows handled on Tunnel; no FocusFirstElement fallback while the gamepad interceptor is active).
- **Catalog review actions** — Moving across Hide / Remove from Library (and other row actions) no longer traps focus on the far-right button; Left moves back, and Left from the first action returns to the row.
