# Agraria OS

A piecemeal SMAPI mod aiming to bring Stardew's interaction and UX philosophy closer to micromanagement MMORPGs, especially EVE Online. Original name was "StardEVE". `docs/stardeve.md` is the archived OG proposal. Favor small, usable additions and a dense EVE-like UI.

Current features: transparent top-left status readout replacing the clock/money panel, journal/zoom text controls, F8 vanilla toggle, pixel-aligned fonts, cosmetic security ratings and mine depth labels, compact bottom toolbar with text vitals, and the slim left Pelicom rail. Mail and Inventory open draggable, resizable desktop windows with minimize/restore/close and per-farmer position/size saved in config across reloads; other rail buttons remain placeholders. Inventory is a live backpack sheet with small icons, sortable Name/Qty/Category/Value columns, paired column resizing and wheel/thumb scrolling; it defaults to slot order and shares its grid with chest inventories. Value follows vanilla Price Catalogue unlock, eligibility and stack sell-price rules, leaving unavailable/zero values blank.

Source lives in `src/`: `ModEntry.cs` coordinates per-screen state and SMAPI events, `Integration/ModEntry.HudPatches.cs` contains Harmony installation and hooks, `Configuration/` owns settings, and `Hud/` owns HUD rendering and cosmetic security; `Hud/StatusReadoutRenderer.cs` renders the top-left location/security, date/time, weather/money and journal/zoom readout. `Desktop/WindowManager.cs` owns window lifecycle and input priority, `Desktop/OsWindow.cs` owns shared chrome, and `Desktop/Windows/` contains individual windows. `UI/UiStyle.cs` owns shared colors/icons and `UI/UiText.cs` owns pixel-aligned text drawing. `UI/GridView.cs` and `UI/GridColumn.cs` own the reusable typed grid (sorting, resizing, scrolling and cell rendering); use them for future tables such as Livestock. See `docs/grid-view.md` for the window integration contract.

## Build

- Target `net6.0`; the installed .NET 10 SDK builds it successfully. Stardew supplies MonoGame and its runtime. No Visual Studio, separate MonoGame SDK, or ImGui needed.
- `Pathoschild.Stardew.ModBuildConfig` supplies game/SMAPI/Harmony references. Keep dependencies small and don't bundle game assemblies.
- `.\build.ps1` builds and packages into `dist/`; `.\build.ps1 -Install` also deploys to the game's Mods folder. Use `-GamePath 'path'` if auto-detection fails. Intermediates/tools belong in `.artifacts/`.
- Local baseline: Stardew 1.6.15, SMAPI 4.5.2, game folder `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley`. Check installed versions before assuming APIs match.

## Inspect the game code

Use ILSpy against the locally installed DLLs to verify signatures and behavior instead of guessing from old examples. Keep all decompiled code local and ignored; don't commit or package it.

Install the CLI once in this repo, then decompile a specific type:

```powershell
dotnet tool install ilspycmd --tool-path .artifacts/tools --allow-roll-forward
$stardewPath = 'C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley'
.\.artifacts\tools\ilspycmd.exe -t StardewValley.Menus.DayTimeMoneyBox "$stardewPath\Stardew Valley.dll" -o .artifacts/decompiled
rg -n 'draw\(|updatePosition|receiveLeftClick|isWithinBounds' .artifacts/decompiled
```

Other useful types: `StardewValley.Game1` for render/input call sites, `StardewValley.GameLocation` for location/weather APIs, and `StardewValley.Locations.MineShaft` for floor/area handling. Skull floors use internal levels above 120; the quarry's special level is 77377, so check mine area before interpreting depth.

For SMAPI internals, use the same command against `StardewModdingAPI.dll`; `StardewModdingAPI.Framework.SGame` and `.SCore` show render-event routing. `ilspycmd.exe -l c <dll>` lists class names when needed. Decompile relevant types, then use `rg` and focused reads rather than dumping entire assemblies into context.

Read both the method and its callers before patching. For rendering, check UI versus world coordinates, scaling, render targets, SpriteBatch lifetime and sampler state. For interactions, inspect hit boxes and vanilla actions. Prefer public properties over raw net fields and narrow Harmony patches over replacing broad game systems.

## Working conventions

- Preserve vanilla HUD visibility, journal/zoom actions, unrelated HUD elements, F8 fallback and per-screen state. Keep shared rendering state intact; fractional bitmap scaling and final UI compositing can soften text.
- Security is cosmetic: farm 1.0, town highsec, caves lowsec, Skull Cavern down to -1.0 at floor 100. Preserve the cyan/green/yellow/orange/red/purple progression.
- Leave game lifetime and save loading to the user unless asked. A loaded mod DLL may be locked; install after normal exit and preserve `config.json`. Changes take effect on the next launch.
- Build after code/build changes. Use SMAPI logs and focused in-game checks for runtime behavior; distinguish compile/startup checks from visual verification. No test scaffolding for trivial reversible edits.
- Keep project and manifest versions in sync when bumping a release. Don't commit or push unless requested.
