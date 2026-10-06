# Validation

Reference environment: Stardew Valley 1.6.15, SMAPI 4.5.2, .NET SDK 10 targeting `net6.0`. Confirm installed versions when changing game integration.

## Build and package

- Run `.\build.ps1`; resolve warnings and errors.
- Keep project and manifest versions in sync. Check the release archive contains only mod files and documentation, with no game/SMAPI/MonoGame/Harmony assemblies.
- When installing, exit the game normally, run `.\build.ps1 -Install`, and preserve `config.json`. Verify the installed DLL matches the release output.

## In-game checks

On the next user launch, inspect the SMAPI log for startup or patch errors and check the affected features:

- HUD: date/time (including midnight), weather/money/location, cosmetic security colors and mine depth, journal/zoom controls, F8 fallback and unrelated vanilla HUD elements.
- Toolbar/vitals: bottom alignment, icons/counts/quality, tooltips, click targets, keyboard/wheel/controller selection, row swapping, pinned fading and health/energy values.
- Desktop: rail icons/hover labels, open/focus, dragging through held input, minimize/restore and close/reopen. Covered clicks and scrolling must not reach the world or toolbar.
- Inventory: live slot order and separate stacks, readable labels, tool/object sprites, column resizing, wheel/thumb limits and Price Catalogue eligibility/stack values. Unavailable or zero prices stay blank.
- Visibility/layout: menus, events, chat, viewport resizing, UI/font scales, split-screen isolation and reset after returning to title.

Report build, isolated code checks, startup and in-game visual checks separately. A successful build does not verify appearance or input in-game. Keep this file as a checklist; record change-specific results in the task or PR instead of appending release history.
