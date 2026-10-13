using AgrariaOS.Configuration;
using AgrariaOS.Chat;
using AgrariaOS.Desktop;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.Hud;
using AgrariaOS.Notifications;
using AgrariaOS.UI;
using AgrariaOS.Integration;
using AgrariaOS.Mining;
using AgrariaOS.Movement;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace AgrariaOS;

partial class ModEntry : Mod
{
    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button == SButton.MouseRight && Context.IsWorldReady)
            Monitor.Log($"RightClick loc={Game1.currentLocation?.Name} pixel={e.Cursor.AbsolutePixels} move={CanClickToMove} append={CanAppendToQueue} suppressed={e.IsSuppressed()} tool={Game1.player.CurrentTool?.QualifiedItemId}", LogLevel.Trace);
        if (e.Button is not (SButton.LeftControl or SButton.RightControl or SButton.LeftAlt or SButton.RightAlt))
        {
            machineCollectDrag.Value.Reset();
            boxSelectDrag.Value.Reset();
            rockMiningDrag.Value.Reset();
            animalHarvestDrag.Value.Reset();
        }
        // New destinations, UI clicks and ordinary controls all release our route first.
        // This only clears a controller we own, never one installed by an event/other mod.
        // Mouse clicks manage the queue themselves (replace vs append); clearing here
        // would drop the queue before an explicit Ctrl+click or auto-queued rock can append.
        if (e.Button is not (SButton.LeftShift or SButton.RightShift or SButton.LeftControl or SButton.RightControl
            or SButton.LeftAlt or SButton.RightAlt or SButton.MouseLeft or SButton.MouseRight))
        {
            // Opening the overview is UI chrome, not a new destination or tool action.
            if (e.Button != config.OverviewKey)
                clickToMove.Value.Cancel();
        }
        // Release the modal before evaluating the usual player-free F8 fallback.
        if (e.Button == config.ToggleKey && Game1.activeClickableMenu is NumberEntryMenu numberEntry)
            numberEntry.exitThisMenu(playSound: false);

        if (UsePelicom && !e.IsSuppressed() && e.Button is SButton.MouseLeft or SButton.MouseRight)
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            bool leftClick = e.Button == SButton.MouseLeft;
            if (windowManager.Value.ReceiveClick(cursor, leftClick, Helper.Input, () =>
            {
                if (Game1.IsChatting || !pelicom.Value.isWithinBounds(cursor.X, cursor.Y))
                    return false;
                if (leftClick)
                    pelicom.Value.ReceivePress(cursor);
                else
                    pelicom.Value.receiveRightClick(cursor.X, cursor.Y);
                return true;
            }))
            {
                Helper.Input.Suppress(e.Button);
                return;
            }
            if (!Game1.IsChatting && pelicom.Value.IsInteractive && statusReadoutRenderer.Value.Contains(cursor))
            {
                if (leftClick)
                    // Invoke before vanilla hover handling can reposition the clock's controls.
                    Game1.dayTimeMoneyBox.receiveLeftClick(cursor.X, cursor.Y);
                else
                    windowManager.Value.ShowContextMenu(cursor,
                        statusReadoutRenderer.Value.GetContextMenuItems(config, () => Helper.WriteConfig(config)),
                        title: "Status readout", isValid: () => UsePelicom && pelicom.Value.IsInteractive);
                Helper.Input.Suppress(e.Button);
                return;
            }
            if (!Game1.IsChatting && !Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                    && menu.isWithinBounds(cursor.X, cursor.Y)))
                windowManager.Value.FocusWorld();
        }

        if (UsePelicom && e.Button != config.ToggleKey && !e.IsSuppressed()
            && windowManager.Value.ReceiveKeyboardButton(e.Button, Helper.Input))
        {
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (e.Button == SButton.MouseLeft && !e.IsSuppressed() && (CanSelectCollections || CanClickToMove || CanAppendToQueue))
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            // Keep clock/journal/zoom, toolbar and other mods' HUD menus in vanilla's hands.
            if (!Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                    && menu.isWithinBounds(cursor.X, cursor.Y)))
            {
                try
                {
                    Vector2 pixel = e.Cursor.AbsolutePixels;
                    Point tile = new Point((int)MathF.Floor(pixel.X / 64), (int)MathF.Floor(pixel.Y / 64));
                    bool furnitureClick = IsFurnitureLeftClick(Game1.currentLocation, Game1.player, tile);
                    if (!furnitureClick && (Helper.Input.IsDown(SButton.LeftShift) || Helper.Input.IsDown(SButton.RightShift)))
                        return;
                    if (!furnitureClick && CanSelectCollections && TryBeginAnimalHarvestDrag(pixel, tile))
                    {
                        Helper.Input.Suppress(e.Button);
                        return;
                    }
                    bool busyBefore = clickToMove.Value.IsBusy;
                    bool moveAllowed = CanClickToMove;
                    if ((furnitureClick || moveAllowed) && (furnitureClick
                            || !Helper.Input.IsDown(SButton.LeftControl) && !Helper.Input.IsDown(SButton.RightControl))
                        && ShouldUseVanillaLeftClick(Game1.currentLocation, Game1.player, tile))
                    {
                        // Release our route before vanilla swings or places/picks up furniture.
                        // Leave the mouse unsuppressed so native targeting and removal
                        // rules run exactly as they do for the keyboard tool binding.
                        clickToMove.Value.Cancel();
                        return;
                    }
                    bool queued = TryQueueLeftClick(tile);
                    bool started = false;
                    // Append-only mode (mid-swing) must never Start: that would hijack
                    // the swing with a fresh route and wipe the repeat session.
                    if (!queued && moveAllowed)
                        started = clickToMove.Value.Start(tile);
                    Monitor.Log($"LeftClick loc={Game1.currentLocation?.Name} tile={tile} playerTile={Game1.player.TilePoint} busyBefore={busyBefore} moveAllowed={moveAllowed} queued={queued} started={started} {clickToMove.Value.LastDebug}", LogLevel.Trace);
                    if (queued || started)
                    {
                        TryBeginRockMiningDrag(pixel, tile);
                        Helper.Input.Suppress(e.Button);
                    }
                }
                catch (Exception ex)
                {
                    OnClickToMoveFailed(ex);
                }
                return;
            }
        }

        if (e.Button == SButton.MouseRight && !e.IsSuppressed() && (CanSelectCollections || CanClickToMove || CanAppendToQueue)
            && !Helper.Input.IsDown(SButton.LeftShift) && !Helper.Input.IsDown(SButton.RightShift))
        {
            Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            // Same HUD reservation as left-click; in-range clicks fall through to vanilla.
            if (!Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                    && menu.isWithinBounds(cursor.X, cursor.Y)))
            {
                Vector2 pixel = e.Cursor.AbsolutePixels;
                Point tile = new((int)MathF.Floor(pixel.X / 64), (int)MathF.Floor(pixel.Y / 64));
                if (CanSelectCollections && config.RightClickDrag == RightClickDragMode.BoxSelect
                    && TryBeginBoxSelectDrag(pixel, tile))
                {
                    Helper.Input.Suppress(e.Button);
                    return;
                }
                if ((CanSelectCollections && TryBeginMachineCollectDrag(pixel, tile))
                    || ((CanClickToMove || CanAppendToQueue) && TryStartRightClickInteract(tile)))
                    Helper.Input.Suppress(e.Button);
                // Unactionable far tiles keep vanilla's facing-tile action plus Lookup info.
            }
        }

        if (UsePelicom && !Game1.IsChatting && !e.IsSuppressed() && e.Button == config.OverviewKey)
        {
            windowManager.Value.OpenOverview();
            Helper.Input.Suppress(e.Button);
            return;
        }

        if (!patchInstalled || !config.Enabled || !Context.IsWorldReady || !Context.IsPlayerFree || e.Button != config.ToggleKey)
            return;

        osHudActive.Value = !osHudActive.Value;
        windowManager.Value.InvalidateLayout();
        hardwareCursor.Reset();
        Helper.Input.Suppress(e.Button);
        // Restore click targets before the next input update, including while the HUD is hidden.
        foreach (Toolbar toolbar in Game1.onScreenMenus.OfType<Toolbar>())
        {
            if (!UseCompactToolbar)
                toolbarRenderer.Value.RestoreLayout(toolbar);
        }
        Monitor.Log(osHudActive.Value ? "Agraria OS HUD enabled." : "Vanilla HUD enabled.", LogLevel.Info);
    }

}
