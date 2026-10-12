# Overview warps

Overview lists map transitions in the existing **Warp** category: road/map links, clickable doors, the sewer manhole, building entrances and warp touch tiles. Rows use the destination's display name when an existing location supplies one, with a readable map-name fallback. Building entrances use the building type. Adjacent tiles with the same transition action or road destination share one row; separate entrances remain separate. Regular doors between rooms on the same map retain their previous Door rows.

Discovery reads the current location's live `warps`, `doors`, map `Action` / `TouchAction` properties (including tile-sheet properties), and buildings' human doors on each normal Overview scan. It has no list of entrance coordinates. Changed/removed actions, warp destinations and moved building doors invalidate existing rows and queued interactions. NPC-only warps are excluded. Existing mine ladders, shafts, elevators and location-specific tile-index interactions remain supported.

The game does not expose a destination for every scripted action. A small action-type adapter labels native `EnterSewer`, Community Center, sunroom, boat tunnel, greenhouse, Wizard hatch, mastery cave and minecart actions. Custom maps using ordinary warp properties work automatically; opaque custom scripted transitions need an action-type adapter. Discovery never executes an action or loads/generates a destination to identify it.

Right-click **Interact** chooses a reachable entrance tile and calls the location's normal `checkAction` on arrival. Native opening hours, friendship/key requirements, construction restrictions, unlock messages and location overrides therefore remain responsible for access. Locked entrances are still listed. Road exits walk into the native collision trigger, including a trigger just outside the map; touch warps walk onto their tile and let the normal location update handle them. No direct teleport or repeated touch action is dispatched. Map changes invalidate remaining queued work as usual.

## Verification

Built with `build.ps1` against installed Stardew Valley 1.6.15 / SMAPI 4.5.2. The ignored `.artifacts/warp-check` harness passed 53 checks using seven installed maps and a synthetic custom map: grouped manholes/doors/roads, separate exits, tile-sheet properties, NPC-only exclusions, stable keys, reachable door sides, building door movement/removal, changed actions, native callback dispatch, off-map endpoints for direct and queued Interact, disabled warps, touch behavior, map-change invalidation and Overview row integration. Native lock responses are simulated; visible movement, fades and dialogs need a loaded-save check. Existing Overview (59), collection/animal/bulk queue (135), and path selection/route (167 plus existing path checks) harnesses passed.

After normal installation and launch, check:

- Town: one Sewer row for the manhole and one row per road opening; multi-tile doors such as Pierre's share a row. Interact with a shop during and outside opening hours.
- Sewer: Interact without the Rusty Key, with the key before first unlocking, and after unlocking. Confirm vanilla messages and the normal follow-up interaction.
- Roads: Interact from nearby and far away at each map edge. Confirm walking crosses the boundary, the fade completes once, and queued work from the old map is discarded.
- Farm: use a coop/barn entrance and a moved building's new door. Confirm the Warp row targets the human door.
- Custom/touch warps: check required-mail/other access conditions still apply. Verify hidden Warp categories, selection, Approach, manual cancellation and F8 continue to work.
