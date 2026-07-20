# Quiver v2.3.11

Keyboard controls are now rebindable alongside gamepad bindings in Settings → Controls, with sensible defaults (Enter Select, Escape Back, O Options) and context menus that respect your Back key.

## Rebindable keyboard controls

- **Settings → Controls** — Each action shows Gamepad and Keyboard rows with separate Rebind. Reset restores both maps to defaults.
- **Defaults** — Select = Enter, Back = Escape, Options = O, arrows for navigation.
- **Runtime** — Confirm, Cancel, Options, and navigation use stored keyboard bindings (no more hardcoded Left Shift cancel). Keyboard works even when Enable Gamepad Input is off.
- **Hints bar** — Shows keyboard bindings (and gamepad glyphs when gamepad input is enabled).

## Context menu Back

- Closing an open context menu uses the bound **Back** key, not Avalonia’s hardcoded Escape alone.
- If you rebind Back away from Escape, Escape no longer dismisses the menu; your bound key does.

## Other

- Text boxes still allow normal typing (including Backspace); Escape still leaves text edit when focused.
- Esc cancels a rebind listen session without assigning Escape as the new binding.
