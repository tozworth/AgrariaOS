# Prototype validation

## 0.1.3 update

Reduced the strip to 484x44 with 40px slots and fixed 32px standard item sprites. Selected, hovered and row-swapped items use the same sprite scale. Stack counts use 1x tiny pixel digits with a one-pixel shadow, placed inside the slot; quality stars are also smaller. Shortcut labels remain hidden.

Fixed input geometry: `Utility.makeSafeMarginY` depends on the active render target and can yield different margins during input at non-native UI scaling. Only drawing computes the layout now. Hit tests read the last drawn rectangle, and the resize/first-hover hook reapplies its slot bounds without recalculating the margin. Vanilla left/right-click actions remain intact.

Built with zero warnings and errors. An ignored local regression harness exercised the actual shipped renderer's cached hit area and resize recovery without a GraphicsDevice: all 96 slot-center checks passed across top/bottom positions and UI scales 0.75, 1, 1.25 and 1.5; outside clicks were excluded. This verifies geometry rather than in-game mouse-event dispatch. Smaller sprites/counts, selection without zoom and actual left-click selection still need confirmation on the next user launch.

Installed 0.1.3 while the game was closed. The installed DLL hash matches the build output and config.json's hash is unchanged.

## 0.1.2 update

Replaced the vanilla 800x96 toolbar with a 724x64 dark gray strip (12 slots), square corners and a lighter selected-slot background. Removed shortcut labels while preserving shortcut inputs, vanilla item scales, hover/swap animations, stack counts, quality icons and tooltips. The compact click targets follow the strip, including after resizing and vanilla's initial hover layout reset. Pinned fading and automatic top/bottom placement follow vanilla behavior. F8 switches both the clock and toolbar back to vanilla; renderer state remains per screen.

Verified toolbar drawing, input callers, UI batch setup and item sprite origins against the installed Stardew Valley 1.6.15 assembly using local ILSpy output. Built and packaged with zero warnings and errors. After the game exited normally, installed 0.1.2 with `build.ps1 -Install`; the installed DLL hash matches the release output and config.json's hash is unchanged. No game was closed or launched by this update.

Startup and in-game visual verification are pending the next user launch. Check selected-slot fill, unchanged sprite sizes, stack/quality badges, hover tooltips, clicking first/last slots, keyboard/scroll/controller selection, toolbar row swapping, top/bottom placement, pinned fading, F8 restoration, resizing/UI scaling and split-screen.

## 0.1.1 update

Whole-number bitmap font scaling and pixel-aligned text replace the original 0.85 scaling. Added cosmetic security ratings, mine floor labels and the requested cyan-to-purple palette. Build passed with zero warnings and errors. The running game locks its DLL, so installation is staged through a one-time background helper which waits for Stardew to exit normally, then copies the four release files and preserves config.json. In-game appearance of this update still needs verification after a restart. No running game is closed or restarted by the update.

The game subsequently exited and the one-time helper reported successful installation of 0.1.1. The installed DLL matches the build output. Existing config.json was preserved. The user subsequently confirmed the updated HUD looks good in-game. Individual security mappings, deeper Skull Cavern floors and split-screen still need in-game checks.

## 0.1.0 baseline

- Built with the already installed .NET SDK 10.0.400, targeting net6.0: zero warnings and errors.
- Installed into the local Stardew Valley Mods/AgrariaOS directory.
- Launched with Stardew Valley 1.6.15 and SMAPI 4.5.2.
- SMAPI detected Agraria OS 0.1.0 and reported: `Text HUD ready. F8 switches between Agraria OS and the vanilla clock.`
- SMAPI reported all mods loaded and ready, with no Agraria OS startup error.
- Release archive contains the mod DLL, manifest, README and MIT license; game/SMAPI/MonoGame/Harmony DLLs are not bundled.

Save loading, appearance, clicks, F8 toggling, midnight formatting and UI scaling still need an in-game check. The launched game has been left open for that check.
