# Click-to-move

With Agraria OS active, left-click a ground tile to walk to it. A cyan bracket marks the destination. Another ground click replaces the route. Movement keys, controller movement, other buttons, map transitions, day changes and F8 cancel it; menus, scrolling and desktop focus pause it and resume afterwards. Shift+left-click retains vanilla tool/item handling. Set `ClickToMove` to `false` in config to disable this feature independently.

Queue up to 9 interactions: each queued tile gets its own bracket color (cyan, yellow, orange, green, pink, purple, red, blue, white in order). Click 5 rocks and they are mined in succession; right-click 2 NPCs and both dialogues play in order into Local. While a route runs — including mid-swing during mining — plain left-clicks on tool/approach targets and plain right-clicks on interactables append to the queue automatically. Ctrl+click queues explicitly, including plain ground waypoints. Plain ground left-clicks, warps and Shift+clicks still replace (or bypass) instead of queuing. A full queue plays `cancel` and ignores the extra click. Movement keys, tool/action buttons, warps and day changes still clear the queue.

Click the last visible tile in a map exit to continue across its boundary and warp normally. This includes the farm's bus stop/forest/backwoods exits and farmhouse doorways whose triggers sit one tile beyond the map. Clicking directly on a warp trigger also works. Other edge tiles remain ordinary movement destinations.

Click a solid tile or object, such as a chest, TV, fridge or furnace, to walk to the closest reachable tile beside it and face it. Multi-tile objects use their full ground footprint. The built-in farmhouse fridge accepts either its upper or lower sprite tile. Right-click keeps its existing interactions after you arrive. Unreachable left-clicks walk as close as possible toward the clicked tile instead of playing `cancel`, so repeated clicks step around dense trees and fences; only a fully stuck position with no closer walk still plays `cancel`.

Clicking a supported tool target walks to the closest reachable cardinally adjacent tile, selects the appropriate tool from the backpack and swings until that target is gone (up to 30 swings), then continues with the queue. Single clicks still do one target fully: click again only for another rock/tree. Water refills stay single-use. Tool animations, stamina costs, upgrade requirements, drops and multiplayer synchronization remain the game's responsibility.

Supported targets:

- Untapped wild trees, their stumps, twigs, rocks, weeds and grass.
- Large stumps, logs, boulders, meteorites, giant crops and green rain clumps. Any tile in the clump works; the hit is aimed at its reachable edge.
- Dry crops which need water, plus dry tilled ground when holding a watering can, and dead crops for scything.
- Bare diggable ground when holding a hoe, and refillable water when holding a watering can.
- Fruit trees when explicitly holding an axe.

The appropriate tool must already be in the backpack. Chests, machines and tapped trees are not automatically dismantled. Right-click interactions keep their existing behavior. This first implementation supports walking on foot; mounted movement, NPC approach/actions, automatic harvesting and item placement remain vanilla. It does not reproduce the entire mobile interaction system.

## Implementation

`Movement/TilePathfinder.cs` implements four-way A* with integer coordinates, a 20,000-node budget and cached collision results per search. It avoids vanilla `PathNode` coordinate and path-cost limits. `TilePathfinder.FindClosest` runs the same search toward an unreachable click while tracking the visited walkable tile with the smallest Manhattan distance, returning the path there so dense canopies, fences and walls step closer instead of playing `cancel`. `Movement/ClickToMove.cs` supplies the resulting stack to the desktop game's `PathFindController`; its ordinary `Farmer.MovePosition` handles collision, speed and player warps. NPC-only controller warp handling is deliberately disabled. The search uses `GameLocation.isCollidingPosition` with `pathfinding` and `skipCollisionEffects`, plus live NPC hitboxes. A stalled route can replan three times, then stops.

`Movement/WarpDestination.cs` resolves exit clicks against the location's live player warps. The search permits only the selected trigger as an off-map endpoint, then vanilla movement activates the warp before checking ordinary tile collisions. NPC-only and location-disabled warps stay excluded. A removed/disabled exit cancels the route.

`Movement/ToolTarget.cs` resolves target identity without invoking destructive tool probes. The target and carried tool are checked again on arrival and before every queued swing. `BeginUsingTool` starts the native animation/event sequence; upgraded hoes and watering cans receive an explicit release for one uncharged use. Each queue leg runs its own A* from the previous arrival tile with live collision plus NPC hitboxes, so chained rocks/NPCs pathfind sequentially; stale, unreachable or wandered-off legs are skipped with replans and chase re-resolves.

`Movement/ApproachTarget.cs` shares perimeter and facing geometry with tool targets. It resolves solid objects, furniture, the built-in fridge, terrain features, clumps and buildings to their live footprint; unknown blocked map tiles use their four cardinal neighbors. A moved or removed entity cancels the approach. Ordinary walkable ground still uses its exact tile, and supported tool targets retain their tool action.

Input routing in `ModEntry.cs` gives desktop windows, context menus, Pelicom and native on-screen menus priority. `Integration/ModEntry.ClickToMove.cs` handles movement guards and the world-space destination marker. Routes and failures are per screen. A failure restores vanilla left-click for that screen until returning to title; other mods' controllers are never cleared.

## Verification

Built against locally installed Stardew Valley 1.6.15 / SMAPI 4.5.2. Headless checks compare A* to an independent breadth-first search over randomized blocked maps and cover large coordinates/long routes, search limits, changed obstacles, multi-tile approaches, tool selection and stale targets. These checks do not verify animation/rendering or an actual loaded save.

Exit regression checks read the installed Farm, BusStop and three farmhouse maps. They run vanilla's path follower with simulated farmer movement against the real warp collision API, confirming the extra step reaches east/west/north/south and farmhouse triggers. NPC-only/disabled warps and unrelated edge rows are also checked.

Solid-target checks cover single-tile machines, multi-tile furniture, alternative reachable sides, enclosed targets stepping closer, fully stuck positions still playing `cancel`, stale entities, ordinary ground, map-tile fallback, both fridge sprite tiles and tool-equipped furniture/fridge clicks.

Focused in-game checks after installation and the next normal launch:

1. Walk around fences, buildings, water, rocks and trellis crops; click enclosed/blocked tiles.
2. Click a tree, rock and large stump from a distance, including a clump's interior tile. Confirm fully aimed swings until each breaks, normal stamina cost and normal upgrade restrictions.
3. Water a dry crop with an upgraded can, hoe bare ground with an upgraded hoe, and refill at water. Confirm the animation completes without holding the mouse; refill does exactly one fill.
4. Interrupt with a new ground click (replaces), movement/tool/action keys and controller stick (clears), menu/scroll/desktop focus (pauses/resumes), map warp and F8. Try an NPC walking across the route.
5. Click/drag desktop windows and context menus, the toolbar, journal and zoom controls. Check world targeting with different zoom/UI scales and split-screen.
6. Leave the farmhouse and the farm by clicking their final visible exit tiles, without keyboard movement. Check bus stop returns and farm exits in every direction.
7. Approach a chest, furnace, TV and fridge with left-click, including multi-tile furniture away from its anchor. Block the nearest side and check that another reachable side is chosen. Right-click afterward to interact.
8. Queue 5 rocks (click each while walking): confirm 5 brackets in queue colors, sequential mining, and skip-on-unreachable. Right-click 2 NPCs: confirm both dialogues land in Local in order. Ctrl+click 3 ground waypoints: confirm ordered walk. Fill to 9 and confirm the 10th plays `cancel`.
