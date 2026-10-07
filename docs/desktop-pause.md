# Desktop pause

Opening or clicking a desktop window gives it focus and pauses single-player simulation. Movement and world tool/action inputs are blocked while managing windows. Switching windows, scrolling, dragging, resizing and transferring items remain available. The clock displays a small pause icon beside the time whenever the game's time gate is paused.

Click the world to resume play with the windows still open. Hovering does not change focus. Native HUD controls retain their actions without releasing desktop focus. Minimized and unfocused windows do not pause. An active drag or pending chest transfer keeps focus until it finishes or is canceled. F8, hidden UI, layout invalidation and returning to title release the desktop pause.

Online multiplayer keeps the shared world running, with only the local farmer's controls blocked during desktop interaction. Split screen uses Stardew's existing pause requests: each focused desktop requests a pause, and the shared world pauses when every local player requests it.

## Implementation

`Desktop/WindowManager.cs` owns focus and gesture/transfer capture. `ModEntry.cs` releases focus only after desktop, Pelicom and native HUD hit testing has ruled out a UI click. `Integration/ModEntry.DesktopPause.cs` extends `Game1.shouldTimePass`, guards local farmer movement and world actions, and consumes world control bindings before vanilla input runs. It does not replace the active menu or mutate the game's movement/pause flags. The window update loop and chest mutex updates keep running.

## Verification

Release build targets the installed Stardew Valley 1.6.15 and SMAPI 4.5.2 assemblies. Isolated checks install the Harmony hooks on those assemblies and exercise focus/resume, preserved player-free context, blocked movement/actions, minimization, F8/disabled fallback, multiplayer time authority, drag capture and existing vanilla pauses. These checks do not replace loaded-save or visual verification.

After the next normal launch:

1. Open Inventory, Mail and Livestock. Confirm the clock stops, the pause icon appears, and held movement/tool inputs do not affect the world.
2. Switch, scroll, move and resize windows while paused. Transfer items between backpack and chest, including stack splitting and context actions.
3. Click the world. Confirm movement and time resume while windows stay open; clicking a window pauses again.
4. Minimize/close the focused window, toggle F8, and open the journal or a vanilla menu. Confirm pause ownership and journal/zoom actions remain correct.
5. In a mine, confirm monsters and projectiles stop during desktop management. In online co-op, confirm another player keeps playing. In split screen, confirm shared time pauses only when all local players pause.
