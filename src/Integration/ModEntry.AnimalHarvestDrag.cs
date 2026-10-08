using AgrariaOS.Movement;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private bool TryBeginAnimalHarvestDrag(Vector2 pixel, Point tile)
    {
        if (HasManualMovement())
            return false;
        InteractRequest? request = InteractProbe.TryResolveAnimalHarvest(Game1.currentLocation, Game1.player, tile);
        if (request == null)
            return false;
        if (!clickToMove.Value.HasQueuedRequest(request)
            && !clickToMove.Value.EnqueueInteract(request, tile, startImmediately: CanClickToMove))
            return false;
        MachineCollectDrag drag = animalHarvestDrag.Value;
        drag.Begin(Game1.currentLocation, Game1.player, Game1.stats.DaysPlayed, pixel);
        drag.Tool = request.Tool;
        foreach (Point _ in drag.Sample(pixel)) { }
        Monitor.Log($"AnimalHarvestDrag started tool={request.Tool?.Name} tile={tile} queue={clickToMove.Value.TotalCount}", LogLevel.Trace);
        return true;
    }

    private void UpdateAnimalHarvestDrag()
    {
        MachineCollectDrag drag = animalHarvestDrag.Value;
        if (!drag.IsActive)
            return;
        if (!IsRockMiningButtonHeld(Helper.Input) || !CanSelectCollections
            || Game1.player.CurrentTool != drag.Tool
            || Helper.Input.IsDown(SButton.LeftShift) || Helper.Input.IsDown(SButton.RightShift)
            || HasManualMovement() || Game1.activeClickableMenu != null
            || !drag.Matches(Game1.currentLocation, Game1.player, Game1.stats.DaysPlayed))
        {
            drag.Reset();
            return;
        }
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        if (windowManager.Value.IsOverWindow(cursor)
            || Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                && menu.isWithinBounds(cursor.X, cursor.Y)))
        {
            drag.Reset();
            return;
        }
        try
        {
            Helper.Input.Suppress(SButton.MouseLeft);
            foreach (Point tile in drag.Sample(Helper.Input.GetCursorPosition().AbsolutePixels))
            {
                InteractRequest? request = InteractProbe.TryResolveAnimalHarvest(Game1.currentLocation, Game1.player, tile);
                if (request != null && !clickToMove.Value.HasQueuedRequest(request))
                {
                    clickToMove.Value.EnqueueInteract(request, tile, startImmediately: CanClickToMove);
                    Monitor.Log($"AnimalHarvestDrag crossed tile={tile} queue={clickToMove.Value.TotalCount} {clickToMove.Value.LastDebug}", LogLevel.Trace);
                }
            }
        }
        catch (Exception ex)
        {
            OnClickToMoveFailed(ex);
        }
    }
}
