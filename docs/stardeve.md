# Draft StardEVE design (Agraria OS)

## Basic HUD
* Transparent text, top-left, EVE-style, toggleable layers. Can be "Time and location only" to "Time, date, location, region, active events/quests".
Example HUD:
```
**Pierre's** < Pelican Town *0.9* < Stardew Valley
12:40 PM | Spring Thu 5 Y1
[🚩] Introduction: 7/29 NPCs (details...)
[🚩] Find Purple Shorts (details...)
[🌸] Events: Flower Dance in 6d
```
## Overview pane
Just like EVE overview, it will list NPCs and key PoIs by default, but can be extended to show everything currently visible on map. Categories include "NPC", "Animal", "Forageable", "Quest Item", "Tile Entity" (chests, machines..), "Map Link" (e.g. side of the road). Player can ctrl+click to auto-approach object, or "jump" as in the case of Map Links. The extent of the overview will be the currently loaded map section (Farm, Town1, AdventurersGuild etc) and not the entire world. Filter objects by state (e.g. "Harvestable Crops", "Active Machines", "NPCs Not Gifted"), or filter by entire categories ("Enemies" during Skull Cavern, "Forage" in town). Tag/highlight/color any object (or class) at will.
Example of right-click menu for Abigail:
```
Talk
Deliver Quest Item
Gift [>]
Date... [>]
-
Approach
Follow (0 m)
Highlight NPC Schedule
-
View Character Info
View Friendship Status
List Valid Gifts
Add Tag
Add Note
Block... [>] // prevents phonecalls and letters
Hide 'NPC' From Overview
-
```
Example of right-click menu for Parsnip:
```
Harvest (2 days)
Destroy
-
Fertilize... [>]
Speed-Gro... [>]
Retaining Soil... [>]
-
Approach
Orbit (0 m)
-
View Crop Info
View Market Details
Show in Crop Codex
Hide 'Crop' From Overview
-
```
Agraria OS will allow players to orbit a parsnip while checking Pierre/Joja pricing parity for sunflower seeds and using the Tilemap's pathfinding calculator to figure out the time in minutes between grabbing their upgraded axe from Clint and heading to the Night Market submarine all while adding Goat Cheese to their Skull Cavern loadout inventory filter. This is, without a doubt, the future of gaming.

## Inventory
Same as EVE, that is to say, same as Windows file manager. Tree/tab navigation. Can show in Stardew-style grid, plain dense list, or detailed list. Headers include `Name, Category, Amount, Price, Date Added, Needed for Bundle, Needed for Quest, Consumable, Sellable, Tool`. Every chest or storage medium within 10 tiles becomes added as a tree in the left-panel. Drag-and-drop transfer between inventories. Keyboard cut/paste shortcuts, Ctrl+A/Ctrl+X/Ctrl+V to move entire inventory to a chest. EVE-style semantic DnD extended to cover the entire game: drag Amethyst to Abigail to gift directly,  drag geode to Clint's anvil to crack directly, drag wood to broken bridge for repair, drag Robin's icon to farmhouse to start upgrade, drag the day's produce to shipping bin.

## In-game codex
This will be similar to the popular "Lookup Anything" mod extended to the point where it engulfs the wiki. Semantic navigation by item category, instant right-click lookup in-game, bookmarks/search. Will show everything from NPC schedules to quarry rock distribution. Directly queries XNB files, pre-compatible with many types of future content updates.

## Settings
Every window resizeable, transparency configurable, pinnable always-on-top, can shade/unshade Linux-style, minimizable to left-pane "taskbar". Ctrl-Tab cycles windows, Shift-Esc shows "task manager". Pixel perfect DPI scaling (like Minecraft UI scale), optional fallback non-pixelart font (Segoe UI?) for PCs where it won't scale just right.

## Multiplayer enhancements
Farmhands can form "fleets". Live location and inventory updates. Ability to pathfind to any fleet member. Semantic DnD still applies: drop 999 wood to a nearby player to instantly trade, drop Ancient Fruit to Fleet Chat to instantly show everyone you got it, drag player out of Fleet Chat to kick (c'mon, dying to a bat?). Standings system to manage complex farmhand relations.

# Development

## UI
Use C# ImGui / Dear ImGui bindings, replace colors/fonts with Stardew. Prioritize absolute efficiency and functionality over "polish". 

## Features
Approach/Jump: Implement standard 2D A* pathfinding.
Fleets: Use SMAPI multiplayer extensions to share an array of players.
Semantic DnD: Poll drag events for valid sprites / sprite classes.
Codex: Fork Lookup Anything (it's MIT). Add simple search UI.
Overview: Loop active XNA map.

## Performance
Update every second. This both mimics EVE (Tranquility ticks every second) and makes performance a non-issue, allowing for faster prototyping.

# License
Agraria OS will be MIT licensed and you will pay with your soul. Mod creator not liable for when you inevitably start hating Stardew, or better, enter a state of absolute AuDHD trance and spend your leisure time multiboxing 6 farmhands. Remove yourself from the shackles of the cutesy UI at last, and go orbit a parsnip.