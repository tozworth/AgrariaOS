# Agraria OS

A SMAPI mod bringing a compact, EVE-inspired HUD and desktop windows to Stardew Valley.

The Pelicom rail opens Inventory, Mail and Notifications. Windows support dragging, resizing,
minimizing and restoring, with placement saved per farmer. Other rail entries are placeholders.

Notifications replace vanilla HUD toasts with dark, pixel-aligned bubbles. The rail badge shows
unread arrivals (up to 99; hover for the full count). Opening the history clears the badge while
the window is visible. History lists every arrival through Stardew's `addHUDMessage` API newest
first, with the sending season/day/year, in-game time and pickup quantity. Repeated live toasts
still merge and expire as vanilla does; history keeps each arrival separately. History remains
across days and window closes, and resets on returning to the title screen. It is separate for
each local screen and continues recording while F8 displays the vanilla HUD.
Use Clear to empty history, or hover a notification and click its `x` to dismiss that arrival.

## Build

Run `./build.ps1` to build and package into `dist/`. Run `./build.ps1 -Install` after exiting the
game normally to deploy; the existing config is preserved. Use `-GamePath 'path'` if needed.
Target: .NET 6, with game/SMAPI/Harmony references supplied by ModBuildConfig.

## Source map

- `src/ModEntry.cs`: SMAPI events, per-screen state and F8 fallback.
- `src/Integration/`: narrow Harmony patches for HUD elements and notifications.
- `src/Configuration/`: HUD settings and saved window placement.
- `src/Hud/`: status readout, toolbar/vitals, security colors and notification bubbles.
- `src/Desktop/`: Pelicom rail, window lifecycle, input priority and shared window chrome.
- `src/Desktop/Windows/`: Inventory, Mail and Notifications content.
- `src/Notifications/`: immutable session arrivals and unread tracking.
- `src/UI/`: shared colors/icons and pixel-aligned text fitting/wrapping.

See [VALIDATION.md](VALIDATION.md) for runtime checks and [the original proposal](docs/stardeve.md)
for the archived StardEVE concept.
