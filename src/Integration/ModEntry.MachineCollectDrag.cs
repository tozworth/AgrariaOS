using AgrariaOS.Movement;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private static bool IsMachineCollectButtonHeld(IInputHelper input)
        // SMAPI retains suppression until physical release, but IsDown becomes false
        // on the tick after Suppress. Use both public states, scoped to this screen.
        => input.IsDown(SButton.MouseRight) || input.IsSuppressed(SButton.MouseRight);

    private bool TryBeginMachineCollectDrag(Vector2 pixel, Point tile)
    {
        try
        {
            if (worldInteractionsFailed.Value || Game1.player.ActiveObject is StardewValley.Objects.Furniture)
                return false;
            InteractRequest? request = InteractProbe.TryResolveCollection(Game1.currentLocation, Game1.player, tile);
            if (request == null)
            {
                Game1.currentLocation.Objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj);
                Monitor.Log($"CollectDrag skipped start tile={tile} object={obj?.QualifiedItemId} ready={obj?.readyForHarvest.Value} output={obj?.heldObject.Value?.QualifiedItemId}", LogLevel.Trace);
                return false;
            }
            if (!clickToMove.Value.HasQueuedRequest(request)
                && !clickToMove.Value.EnqueueInteract(request, tile, startImmediately: CanClickToMove))
                return false;
            machineCollectDrag.Value.Begin(Game1.currentLocation, Game1.player, Game1.stats.DaysPlayed, pixel);
            // The initiating click already queued this tile.
            foreach (Point _ in machineCollectDrag.Value.Sample(pixel)) { }
            Monitor.Log($"CollectDrag started loc={Game1.currentLocation.Name} tile={tile} queue={clickToMove.Value.TotalCount}", LogLevel.Trace);
            return true;
        }
        catch (Exception ex)
        {
            OnClickToMoveFailed(ex);
            return true;
        }
    }

    private void UpdateMachineCollectDrag()
    {
        MachineCollectDrag drag = machineCollectDrag.Value;
        if (!drag.IsActive)
            return;
        if (!IsMachineCollectButtonHeld(Helper.Input))
        {
            Monitor.Log("CollectDrag ended: mouse released; queued collections continue.", LogLevel.Trace);
            drag.Reset();
            return;
        }
        if (!CanSelectCollections || worldInteractionsFailed.Value
            || Helper.Input.IsDown(SButton.LeftShift) || Helper.Input.IsDown(SButton.RightShift)
            || HasManualMovement() || Game1.activeClickableMenu != null
            || !drag.Matches(Game1.currentLocation, Game1.player, Game1.stats.DaysPlayed))
        {
            Monitor.Log($"CollectDrag ended: input guard move={CanClickToMove} append={CanAppendToQueue} manual={HasManualMovement()} menu={Game1.activeClickableMenu?.GetType().Name} shift={Helper.Input.IsDown(SButton.LeftShift) || Helper.Input.IsDown(SButton.RightShift)}", LogLevel.Trace);
            drag.Reset();
            return;
        }
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (windowManager.Value.IsOverWindow(cursor)
            || Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                && menu.isWithinBounds(cursor.X, cursor.Y)))
        {
            Monitor.Log("CollectDrag ended: cursor entered UI.", LogLevel.Trace);
            drag.Reset();
            return;
        }
        try
        {
            // Keep native right-click repeats suppressed throughout the collection stroke.
            Helper.Input.Suppress(SButton.MouseRight);
            foreach (Point tile in drag.Sample(Helper.Input.GetCursorPosition().AbsolutePixels))
            {
                InteractRequest? request = InteractProbe.TryResolveCollection(Game1.currentLocation, Game1.player, tile);
                if (request != null && !clickToMove.Value.HasQueuedRequest(request))
                {
                    clickToMove.Value.EnqueueInteract(request, tile, startImmediately: CanClickToMove);
                    Monitor.Log($"CollectDrag crossed tile={tile} queue={clickToMove.Value.TotalCount} {clickToMove.Value.LastDebug}", LogLevel.Trace);
                }
            }
        }
        catch (Exception ex)
        {
            OnClickToMoveFailed(ex);
        }
    }
}
