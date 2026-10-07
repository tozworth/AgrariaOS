# Validation

Build with `./build.ps1`; install only after normal game exit. Compile and local logic checks do
not verify visual behavior. Verify in-game with Stardew 1.6.15 and SMAPI 4.5.2:

- Check the SMAPI log for notification startup and drawing errors.
- Load a save or start a new day: expect one "Welcome to Agraria OS test-0184. Enjoy your stay."
  toast, an unread badge, and a timestamped history entry through the normal game toast path.
- Trigger tool-ready, train/fish-frenzy, warning, achievement and repeated item-pickup toasts.
  Check Agraria styling, wrapping, quantities, original sounds/expiry and no duplicate bubbles.
- Check that the unread badge increments, opening history clears it, and new arrivals badge
  again while history is minimized or closed. The tooltip should show counts above 99.
- Check full messages and sending date/time in newest-first history; advance a day and test
  post-midnight and 24-hour timestamps. Closing/reopening should retain all session entries.
- Scroll a long history with wheel/thumb, receive a toast while scrolled, then resize at UI/text
  scales 1 and 2. Check text stays inside the content area and input does not reach the world.
- Hover a notification to reveal its `x`; clicking it should remove only that arrival, including
  duplicates and partially visible messages. Click Clear to empty history and the unread badge;
  new toasts should appear normally afterward. Check right-clicks do not clear entries.
- Drag, resize, minimize, restore and close Notifications; check placement after reloading.
- Toggle F8 during active toasts: vanilla bubbles return, history continues recording. Check
  HUD visibility, menus, events, journal/zoom, toolbar and unrelated HUD elements still work.
- Return to title and load another save: history/badge reset. In split-screen, check separate
  histories, unread counts and windows for each local player.
