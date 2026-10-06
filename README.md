# Agraria OS: first HUD prototype

Replaces Stardew's top-right clock, date, weather and money panel with transparent text at the top left. Draws with the game's SpriteBatch and small font; no ImGui, custom textures, Visual Studio, or separate MonoGame installation. The original StardEVE proposal is kept in [docs/stardeve.md](docs/stardeve.md) as a design reference.

Example (actual location, date, weather and money come from the game):

```text
Farmhouse 1.0
6:00 AM | Spring Mon 1 Y1
DAY | CLEAR | 500 g
[Journal]
```

F8 switches between this HUD and the vanilla panel during normal gameplay. The journal and optional zoom controls are text links which use the game's existing actions. The toolbar, energy bar, health bar and other HUD elements still draw normally. Time turns orange after midnight; 2400..2600 format as 12:00..2:00 AM. The location's own weather context is used, including Ginger Island.

## Install / run

Extract `AgrariaOS 0.1.1.zip` into the game's `Mods` directory, then launch with `StardewModdingAPI.exe`. Load a save to see the HUD. Remove the `Mods/AgrariaOS` folder to uninstall. This mod doesn't modify save data. An installed update takes effect on the next game restart.

Requires the desktop game (built against Stardew 1.6.15) and SMAPI 4.4+. The initial build was made using .NET SDK 10.0.400 against the locally installed game and SMAPI 4.5.2. Compilation and startup loading are recorded separately from in-game visual checks below.

## Build without an IDE

From this source folder:

```powershell
.\build.ps1           # build and create a release zip in dist/
.\build.ps1 -Install  # also copy the mod into the detected game's Mods folder
```

If game auto-detection fails:

```powershell
.\build.ps1 -Install -GamePath 'D:\SteamLibrary\steamapps\common\Stardew Valley'
```

The modern .NET SDK can restore the .NET 6 reference assemblies needed by this project. Stardew supplies its runtime and MonoGame; do not target net10.0 for the mod. Pathoschild.Stardew.ModBuildConfig adds the game/SMAPI/Harmony references and packages only the mod files. Build intermediates are kept in `.artifacts/`, and release ZIPs in `dist/`; both are ignored by Git.

## Configuration

After startup, edit `Mods/AgrariaOS/config.json` while the game is closed:

| Setting | Default | Meaning |
|---|---|---|
| Enabled | true | Set false to leave the vanilla panel enabled. |
| ToggleKey | F8 | Session toggle during normal gameplay. |
| X / Y | 24 / 20 | Position in game UI coordinates, clamped to the viewport. |
| TextScale | 1.0 | Font size multiplier, clamped to 0.5..2. Also follows Stardew's UI scale. |
| PixelPerfectText | true | Round font scale to 1 or 2 and align text to pixels; old 0.85 configs also render at 1.0. Set false to allow fractional font sizes. |
| Opacity | 0.95 | Text opacity, clamped to 0.1..1. |
| Use24HourClock | false | Use 00:00..23:50 formatting. |
| ShowLocation | true | Include the current location's display name. |
| ShowSecurity | true | Colored fictional security rating beside the location. |
| ShowWeather | true | Include current weather. |

F8 doesn't persist a config change. Each split-screen player has independent toggle state and cached text. English labels and season names are used in this first prototype; the game's location and weekday names follow its language.

## Fake security

Cosmetic ratings only, with no changes to enemies, farming or multiplayer:

- Farm, farmhouse, cabins and farm building interiors: 1.0. Farm cave: 0.4.
- Town: 0.9; Pierre's, hospital and community center: 1.0; houses/saloon: 0.8; beach: 0.7; forest/mountain: 0.6; desert: 0.5.
- Normal mines: 0.4, then 0.3 after floor 40, then 0.2 after floor 80. Quarry mine and volcano dungeon: 0.1; other caves/sewer: 0.3.
- Skull Cavern entrance: 0.0. Floors use `-min(floor, 100) / 100`, displayed to one decimal place: floor 10 = -0.1, floor 50 = -0.5, floor 100 and deeper = -1.0. Floors 1-4 display 0.0, with their underlying negative rating controlling the color.
- Unmapped locations default to 0.7.

Palette follows cyan at 1.0, green at 0.8, yellow at 0.5, orange at 0.3, red at 0.0, and purple at -1.0, interpolated between those stops. Mine floors include their depth in the location label.

Version 0.1.1 removes the original 85% glyph scaling and aligns text to pixels. Stardew's own final UI scaling can still soften the entire HUD at fractional UI scales; this mod doesn't alter the rest of the game's rendering.

## Scope and checks

This replaces `DayTimeMoneyBox.draw` through a single Harmony prefix. The game still controls when the HUD is visible and which UI scale is active. Journal/zoom hit boxes are moved to the rendered text and restored by vanilla drawing when toggled off. If patching or rendering fails, it falls back to the vanilla panel and logs the error.

The original panel's animated money changes and temporary quest/item popup drawings are omitted in this first version. `[Journal !]` indicates new quest activity. Health, energy, inventory and other notification systems are outside this change.

Manual in-game checks:

- Load a save: text visible at top left, vanilla dial absent, toolbar and energy bar present.
- Use F8 twice: vanilla panel and its controls return; replacement returns without invisible buttons in the old position.
- Click Journal and optional +/- controls; verify journal access and zoom still work.
- Spend/receive money, change location, and wait for a clock update: text follows immediately.
- Check midnight and different weather, menus/cutscenes, window resizing and UI scaling.
- Split-screen and interactions with other clock-replacement mods remain unverified.

Reference: [SMAPI mod build package](https://smapi.io/package/readme).
