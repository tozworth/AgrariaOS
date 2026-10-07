# Agraria OS

A SMAPI mod bringing a compact, EVE-inspired HUD and desktop windows to Stardew Valley.

The Pelicom rail opens Inventory, Mail, Chat and Notifications. Windows support dragging, resizing,
minimizing and restoring, with placement saved per farmer. Other rail entries are placeholders.

Right-click a placed storage chest to open its live contents in the same Inventory table,
including big and Junimo chests. Open Inventory from Pelicom and arrange the two windows to
transfer items. Click a row to select it, Ctrl-click to toggle stacks, Shift-click for a range,
or Ctrl+Shift-click to add a range. Ctrl+A selects every stack in the focused inventory window,
including rows outside the visible area. Drag a selected row into the other window's table body to
move the selected stacks in slot order. Stacks merge using vanilla rules; full inventories and
special chest filters leave unaccepted items in the source. Transfers require vanilla reach and
briefly acquire the chest's multiplayer lock. Escape or dropping elsewhere cancels the drag.
Chest item menus and replacement while holding another chest remain disabled. Right-click another
chest to switch the view, or the same chest to restore it after minimizing. Leaving the location,
starting a new day or removing the chest closes the view. F8 keeps vanilla chest interactions;
gift boxes and reward chests retain their original behavior.

Right-click the Pelicom rail for a checkable Auto-hide placeholder (session-only, with no hiding
behavior), or an Inventory item row for Split and Jettison placeholders. These item actions do
not change the backpack. Click outside or press Escape to dismiss a context menu.

Right-click a TV to choose from its currently available channels, or a telephone to see all
contacts together (including contacts supplied by other mods). These world menus use a
"Choose Option" heading; long lists scroll with the wheel. A ringing phone also offers Answer-call.
Right-click the repaired bus ticket machine while Pam is at her driving spot for Buy-ticket (500g),
which purchases directly through vanilla logic. Without Pam, the machine keeps its vanilla interaction.
Keyboard/controller actions and F8 retain vanilla interaction menus. Moving to another tile,
changing location or removing the target dismisses its popup. Broadcasts, calls and ticket outcomes
still use the game's original callbacks; follow-up choices remain vanilla.

Notifications replace text-only HUD toasts with dark, pixel-aligned bubbles. Item pickups and
icon toasts (including inventory-full, health/energy, quest and achievement messages) stay vanilla
and do not enter history or affect the unread badge. The rail badge shows
unread arrivals (up to 99; hover for the full count). Opening the history clears the badge while
the window is visible. History lists text-only arrivals through Stardew's `addHUDMessage` API newest
first, with the sending season/day/year and in-game time. Repeated live toasts
still merge and expire as vanilla does; history keeps each arrival separately. History remains
across days and window closes, and resets on returning to the title screen. It is separate for
each local screen and continues recording while F8 displays the vanilla HUD.
Use Clear to empty history, or hover a notification and click its `x` to dismiss that arrival.

Chat has Session and Local tabs using a shared tab bar. In Session, click the composer and press
Enter or Send to echo a message locally as `Name: Message`, with a muted, right-aligned time such
as `6:40 AM` and no date. Speaker names are bold and use a consistent color in both tabs;
message text stays neutral. Messages appear oldest first; the history follows the latest message
until you scroll up with the wheel or drag the scrollbar. Sending a message returns to the bottom.
No messages or slash commands reach
vanilla chat or other players. Session history survives days and window closes, resets at the
title screen, and is separate for each local screen. The composer supports paste, Backspace,
Delete, arrows and Home/End; Escape, Tab or clicking elsewhere releases keyboard focus.

Local is read-only. NPC and plain-text dialogue opens/restores Chat on Local and records each prepared
line as `Name: Message` with its sending time. Spoken pages advance immediately through vanilla
dialogue handling, preserving rewards, completion callbacks and event progression. Multiple-choice
prompts remain vanilla and wait for your answer; their follow-ups route to Local. Portraitless
NPC lines keep the NPC name; unnamed text (including TV broadcasts) uses `System`. TV scenes,
recipe learning and other dialogue completion effects still run. F8 restores normal dialogue. Each tab keeps
its own scroll position, and both histories survive window closes until returning to title.

## Build

Run `./build.ps1` to build and package into `dist/`. Run `./build.ps1 -Install` after exiting the
game normally to deploy; the existing config is preserved. Use `-GamePath 'path'` if needed.
Target: .NET 6, with game/SMAPI/Harmony references supplied by ModBuildConfig.

## Source map

- `src/ModEntry.cs`: SMAPI events, per-screen state and F8 fallback.
- `src/Integration/`: narrow Harmony patches for HUD elements/notifications, world mouse interactions and dialogue routing event integration.
- `src/Interactions/`: world context-menu providers, inventory selection and stack transfer handling.
- `src/Configuration/`: HUD settings and saved window placement.
- `src/Hud/`: status readout, toolbar/vitals, security colors and notification bubbles.
- `src/Desktop/`: Pelicom rail, window lifecycle, input priority and shared window chrome.
- `src/Desktop/Windows/`: Inventory, Mail, Chat and Notifications content.
- `src/Chat/`: Session/Local histories, sending timestamps and native dialogue advancement.
- `src/Notifications/`: immutable session arrivals and unread tracking.
- `src/UI/`: shared context menus, colors/icons, tab selection, text composer and pixel-aligned text fitting/wrapping.

See [VALIDATION.md](VALIDATION.md) for runtime checks and [the original proposal](docs/stardeve.md)
for the archived StardEVE concept.
