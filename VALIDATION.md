# Validation

Build with `./build.ps1`; install only after normal game exit. Compile and local logic checks do
not verify visual behavior. Verify in-game with Stardew 1.6.15 and SMAPI 4.5.2:

- Check the SMAPI log for notification/world-interaction startup, drawing and routing errors.
- Load a save or start a new day: expect one "Welcome to Agraria OS test-0184. Enjoy your stay."
  toast, an unread badge, and a timestamped history entry through the normal game toast path.
- Trigger text-only tool-ready and train/fish-frenzy toasts; check Agraria styling, wrapping,
  history, original sounds/expiry and no duplicate bubbles.
- Trigger repeated item pickups, inventory-full warnings, health/energy recovery, quest and
  achievement icon toasts. Check vanilla icons, quantities, merging and expiry; none should
  enter Notifications history or increment the unread badge. Check mixed text/icon toasts stack.
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
- Toggle F8 during active toasts: vanilla bubbles return, text-only history continues recording. Check
  HUD visibility, menus, events, journal/zoom, toolbar and unrelated HUD elements still work.
- Return to title and load another save: history/badge reset. In split-screen, check separate
  histories, unread counts and windows for each local player.
- Open Chat from the emoji-135 rail button: check the Session tab, empty history and composer.
  Send with Enter and Send; check inline `Name: Message`, muted right-aligned `6:40 AM` time
  without a date, oldest-first order and blank rejection. Long messages should wrap clear of time.
  Speaker names should be bold and consistently colored in both tabs, including wrapped long
  names; message text stays neutral and timestamps muted. Check pixel alignment at text scales 1/2.
  Slash commands must echo locally without execution or transmission to another player.
- Type movement/menu/tool/chat hotkeys in the composer; check no world actions. Test paste,
  Backspace, Delete, arrows, Home/End, long messages and non-English keyboard input. Escape,
  Tab, outside clicks, F8, menus and window controls should release focus normally.
- Fill Chat history, scroll with wheel/thumb and resize at UI/text scales 1 and 2. Check wrapping,
  scroll position, auto-follow at the bottom and return to bottom after sending. Minimize/restore
  and close/reopen; check retained history, saved placement, and separate split-screen sessions.
  Returning to title should reset both Chat histories and release its keyboard subscriber.
- Switch Session/Local: Local has no composer or Send button, uses the extra history space, and
  preserves separate scroll positions. A Session draft should survive switching tabs.
- Talk to NPCs and give gifts: portrait speech should open/restore Chat on Local, with localized
  names, parsed text and time. Check long/multiple pages log once per spoken line, friendship,
  gift/item rewards, follow-up menus and movement after dialogue completion.
- Test NPC dialogue leading to a choice, a choice first, and speech after answering. Questions
  and answer buttons must remain vanilla, wait for input, and preserve their normal outcomes.
- Test NPC speech in heart events/festivals: scripts should continue after each skipped speech
  without skipping non-dialogue actions or choices. Toggle F8 and check ordinary dialogue returns. Review SMAPI logs
  for routing errors and verify Local histories remain separate in split-screen.
- Use the TV via keyboard/controller: channel selection stays vanilla; Fortune Teller, Weather, Livin' Off the Land,
  Queen of Sauce and Fishing broadcasts go to Local as `System`, including follow-up pages.
  Check recipes are learned and the TV turns off normally. Test portraitless NPC/narration and
  plain object text, repeated identical pages, and completion callbacks leading to a choice;
  text should log once per page while choices still wait for input.
- Right-click Pelicom buttons and blank rail space: Auto-hide starts unchecked, toggles on click,
  and retains its check when reopened without hiding the rail. Check separate split-screen state.
- Right-click visible Inventory item rows, including after wheel/thumb scrolling: Split and
  Jettison should appear and close on selection without changing items or quantities. Headers,
  empty rows, footer and scrollbar should not open an item menu.
- Check context menus draw above overlapping windows and stay inside viewport edges at UI/text
  scales 1 and 2. Outside clicks and Escape should dismiss without triggering underlying actions;
  wheel input should not scroll content while a menu is open. F8, vanilla menus, viewport resize
  and returning to title should dismiss popups; a new save starts with Auto-hide unchecked.
- Right-click a TV: Choose Option lists the same available channels as vanilla on Monday/Thursday,
  Sunday and Wednesday (including early-save rerun restrictions, Fishing unlock and the cursed
  channel). Selecting a channel should start its original broadcast, learn recipes and turn off normally.
- Right-click a telephone: list all contacts without Next/Previous pages, including custom phone
  handlers. Call a contact and check dialing, store-hours responses and follow-up choices. With an
  incoming call, check Answer-call; opening/dismissing preserves ringing, choosing an outgoing call stops it.
  Green-rain telephone restrictions should remain vanilla.
- Right-click the repaired bus machine with Pam at tile (21,10): expect only Buy-ticket (500g).
  Selecting deducts once and boards normally; insufficient funds uses vanilla feedback. With Pam
  elsewhere/absent or before bus repair, expect vanilla interaction. Check horse dismount behavior.
- Test the three targets with a rebound keyboard action, controller A and F8: vanilla menus return.
  Hold right-click, cancel, click outside and press Escape: no duplicate menus, calls or purchases.
- Check long contact lists with the wheel at UI/text scales 1/2 and viewport edges; every contact
  stays reachable. Moving a tile, warping, removing a target, Pam leaving, F8 and returning to title
  dismiss world popups. Split-screen players should keep separate popups and input scopes.
