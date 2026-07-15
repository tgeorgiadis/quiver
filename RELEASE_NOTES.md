# Quiver v2.3.8

Hidden apps are easy to find again, Edit Tags works better with a controller (especially on Steam Deck), and ignoring “The/A/An” when sorting is now a Settings toggle instead of a separate sort mode.

## Library

- **Show → Hidden** — Manually hidden apps appear under a new sidebar filter. Customize → **Unhide App** restores them; hiding an app shows a short tip pointing here.
- **Ignore The/A/An when sorting** — Moved from a dedicated sort option into Settings (General). Applies to Name sorts in the library and catalog review. Legacy `NameIgnoreArticles` settings migrate to Name automatically.

## Gamepad

- **Edit Tags overlay** — Opening Edit Tags traps controller focus on the prompt (textbox / Cancel / Save) instead of leaving the library ring active underneath.
- **Nested menus close** — Choosing Catalog → Edit Tags (and similar nested items) dismisses the whole context menu.
- **Review lists don’t wrap** — Down on the last Catalog Review / App Updates review row no longer jumps to the top.

## Steam Deck

- **Hide Minimize in Gaming Mode** — The minimize button is hidden under Gamescope / SteamOS gaming sessions where it isn’t useful (Desktop Mode unchanged).
- **Edit Tags + OSK** — In Gaming Mode the Edit Tags card pins to the top and starts on Cancel so Steam’s keyboard doesn’t cover Save/Cancel when the dialog opens.
