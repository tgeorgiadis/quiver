# Quiver v2.3.6

Steam Deck Gaming Mode popup fix and more reliable controller dialogs. Download file pickers stay over Quiver instead of opening as a black fullscreen window, and Yes/No prompts behave correctly when several dialogs appear at startup.

## Steam Deck / Linux

- **Download button file menu** — Choosing a download from Install Latest no longer opens as a separate Gamescope fullscreen surface with a black border. Quiver stays visible behind the picker, same as the card options menu path.
- **In-window popups** — On Linux, ContextMenus and similar popups render inside the main window (`OverlayPopups`) so Gaming Mode does not treat them as their own app window.
- **Stable menu anchors** — After the release-asset fetch, download and executable menus attach to the options button or card instead of the action button that briefly changes during download status updates.

## Gamepad dialogs

- **Dialog stack** — Nested or back-to-back modal dialogs keep focus on the topmost window instead of losing track of which prompt is active.
- **Yes / No results** — Confirm and Cancel on question dialogs set the intended result reliably (including Wine download warnings and similar prompts).
- **Startup prompts** — The Quiver self-update check finishes (or is skipped) before catalog startup prompts, so the two dialogs do not stack on top of each other.
