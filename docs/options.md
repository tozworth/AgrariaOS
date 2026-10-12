# Options window

The Pelicom Options button uses the vanilla options-tab sprite (`mouseCursors`, 96, 368, 16 × 16). It opens a draggable, resizable desktop window with the usual minimize, restore, close, grouping and per-farmer persistence.

The left pane uses the shared `TreeView`. Its categories and localized names come directly from the installed game's `OptionsPage` headers. The right pane shows only the selected category's options, with wheel and thumb scrolling. Vanilla supplies which settings are available to this player, including host and split-screen restrictions. Returning from a native menu or changing multiplayer context refreshes the available categories.

The category pane and action buttons use measured widths from the active font and localized labels. Slider percentages have a stable column sized for every value from 0% through 100%, with space reserved for the slider track. The window's minimum width includes both panes so resizing does not cut off category names, screenshot actions or percentage suffixes while the viewport can accommodate that minimum.

Checkboxes, dropdowns, sliders, zoom/UI-scale steppers, key bindings and action buttons use Agraria styling. Changes call vanilla setting APIs and save default options; sliders save when the drag ends. Key rebinding preserves vanilla duplicate-key validation and Escape cancellation. F8 remains the HUD fallback. Screenshot and multiplayer actions keep their native behavior and dialogs.

## Shared controls

`UI/FormControls.cs` draws fields, buttons, checkboxes, dropdown anchors and sliders. Dropdown indicators use pixel geometry so font size and text fitting cannot hide the arrow. Owners supply values, enablement, hit testing and persistence. `TextEntry` uses the same field styling for editable inputs. `UI/DropdownList.cs` owns a bounded popup with wheel scrolling, arrow/Home/End navigation, Enter selection and Escape/outside-click dismissal. Draw a dropdown after the owning pane's other contents; its layout and hit bounds stay within that pane. These controls use the existing SpriteBatch and pixel-aligned text.

Options tracks keyboard capture intent separately from its dispatcher's `Selected` flag. Vanilla clears that flag during `UpdateOther` outside menus; the desktop restores the subscriber while Options still wants focus. A deliberate desktop blur releases that intent, with dismissal deferred until content clicks can select or cancel the open popup.

## Verification

The release build uses Stardew Valley 1.6.15 and SMAPI 4.5.2. Isolated checks against the compiled mod cover dropdown bounds, long lists, wheel limits, keyboard selection/cancellation, click selection/dismissal, Options capture across vanilla subscriber resets and deliberate blur, and slider endpoints. Text sizing checks use the installed SmallFont assets at 1×, 1.5× and 2× to verify category labels, screenshot buttons, all percentages and the combined minimum width. They do not verify graphics or loaded-save behavior.

After a normal launch:

1. Open Options from Pelicom. Switch categories; resize, move, minimize, restore and group the window. Reload the save and check its placement/open state.
2. Toggle a gameplay setting, change audio sliders, and select dropdown values. Check the corresponding vanilla menu and persistence after relaunch. Confirm Multiplayer and Screenshot remain fully readable and slider values retain the percent sign, including 55% and 100%.
3. Change UI scale, window mode and zoom. Confirm the window and dropdowns remain within the viewport, and the HUD zoom controls still update the displayed setting.
4. Rebind a key; reject a duplicate; cancel with Escape; reset bindings. Confirm ordinary keyboard input resumes after each action and F8 still works.
5. Take a map screenshot and open its folder. In co-op, check host-only settings, invite dialogs and local co-op startup.
