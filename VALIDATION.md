# Prototype validation

## 0.1.7 update

Enlarged Pelicom to a 35px rail with 35px slots and crisp 27px icons (3x native sprites). Dashboard through Mail stay at the top; Notifications and Exit anchor eight pixels above the viewport bottom, with an inset one-pixel separator eight pixels above their group. Hover labels follow the actual top/bottom slot positions, and short viewports skip overlapping top entries. HUD spacing follows the wider rail automatically; no-op clicks and F8 remain intact.

Built, packaged and installed while the game was closed with zero warnings and errors. Installed DLL hash matches the release output and config.json is unchanged. In-game appearance, bottom anchoring after resizing/UI scaling, and hover labels remain to be checked on the next user launch.

## 0.1.6 update

Added Pelicom: a 26px-wide dark rail flush with the left UI viewport edge, with eight 18px chat emoji icons arranged from the top in the requested order: dashboard (107), inventory (152), livestock (74), mining ledger (121), wallet (41), mail (144), notifications (151), exit (131). Hovering highlights a slot and displays its label; left/right clicks are silent no-ops, including Exit. Native on-screen menu dispatch consumes rail clicks before world actions. The text HUD reserves an eight-pixel gap beside the rail.

Pelicom uses per-screen state, the existing HUD toggle/config, vanilla HUD visibility and event/menu guards. Its geometry follows the UI viewport and is cached from drawing; window resizing discards stale hit bounds. It reuses the game's emoji texture and PointClamp UI batch without adding assets or changing rendering state.

Verified sprite coordinates, on-screen menu rendering/input callers, UI mouse coordinates and visibility guards against installed Stardew Valley 1.6.15 / SMAPI 4.5.2 using local ILSpy output. Built, packaged and installed with zero warnings and errors while the game was closed. Installed DLL hash matches the release output; config.json is unchanged. The archive contains only the mod DLL, manifest and license. No game was launched or save loaded.

Startup and in-game visual/input checks remain for the next user launch: icon order/appearance, hover labels, no-op clicks without world actions, HUD spacing, F8, event/menu hiding, resizing, UI scaling and split-screen.

## 0.1.5 update

The compact toolbar is now always centered at the bottom, flush with the UI viewport's bottom edge. Player position and the vanilla pin setting no longer move it; no external bottom margin remains. Health/energy text follows the toolbar above its right edge. Cached click geometry, configured pinned fading and F8 vanilla restoration are preserved.

Built and installed with zero warnings and errors; installed DLL matches the release output and config.json is unchanged. In-game placement and resizing remain to be checked on the next user launch. The game was left untouched.

## 0.1.4 update

Replaced the health/energy bars with a single text row aligned to the compact toolbar's right edge, four pixels above it. Both current/max values remain visible at full health and energy. The game's chat sprite sheet supplies the solid heart (43) and lightning bolt (102), drawn at whole pixel scales. Each value independently follows a muted red (empty), amber (half), green (full) gradient; negative stamina displays as zero, matching vanilla's numeric readout. Numbers use the existing HUD font scale, opacity and pixel-aligned shadow.

The unpinned top toolbar reserves room for this row; bottom placement and cached click geometry are preserved. A guarded Harmony transpiler bypasses only the bar block in `Game1.drawHUD`, retaining event visibility guards, unrelated HUD menus and tracking indicators. Vanilla low-health flash flags are maintained. F8, disabled config, unavailable toolbar and patch failures retain the vanilla bars.

Verified signatures, callers, bar boundaries, health flash flags and chat sprite coordinates against installed Stardew Valley 1.6.15 / SMAPI 4.5.2. Build passed with zero warnings and errors. An ignored local check applied the shipped transpiler to actual game instructions, verified the event guards and entire unrelated HUD tail, and executed the emitted bar block with controlled active/inactive gates. Gradient endpoints, midpoint and bounds checks passed. This is isolated code verification, not in-game visual or startup verification.

In-game checks on the next user launch: sprite/text alignment, full and depleted values, gradual colors, top/bottom placement, toolbar hit targets, F8 restoration, pinned fading, UI/font scaling and split-screen.

Installed 0.1.4 with `build.ps1 -Install`; installed DLL hash matches the release output and config.json's hash is unchanged. The game was left running untouched; the update takes effect on its next launch.

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
