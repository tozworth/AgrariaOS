# Prototype validation

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
