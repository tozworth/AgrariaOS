using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private bool TryBeginBoxSelectDrag(Vector2 pixel, Point tile)
    {
        if (worldInteractionsFailed.Value || Game1.player.ActiveObject is StardewValley.Objects.Furniture
            || !Game1.currentLocation.isTileOnMap(tile) || HasManualMovement())
            return false;
        boxSelectDrag.Value.Begin(Game1.currentLocation, Game1.player, Game1.stats.DaysPlayed, pixel);
        Game1.player.Halt();
        return true;
    }

    private void UpdateBoxSelectDrag()
    {
        BoxSelectDrag drag = boxSelectDrag.Value;
        if (!drag.IsActive)
            return;
        // Check cancellation before release, so a release over UI or after a warp
        // never commits a stale box. Suppression lasts until physical release.
        if (!CanSelectCollections || worldInteractionsFailed.Value || config.RightClickDrag != RightClickDragMode.BoxSelect
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
            drag.Update(Helper.Input.GetCursorPosition().AbsolutePixels);
            if (IsMachineCollectButtonHeld(Helper.Input))
            {
                Helper.Input.Suppress(SButton.MouseRight);
                return;
            }
            GameLocation here = Game1.currentLocation;
            Farmer who = Game1.player;
            InteractRequest? Probe(Point tile) => InteractProbe.TryResolveCollection(here, who, tile)
                ?? InteractProbe.TryResolve(here, who, tile, includeInRange: true);
            bool box = drag.HasDragged;
            Point anchor = drag.AnchorTile;
            var requests = box
                ? drag.ResolveTargets(new Rectangle(0, 0, here.Map.Layers[0].LayerWidth, here.Map.Layers[0].LayerHeight), Probe)
                : Array.Empty<InteractRequest>();
            // Release our pause before starting work. A click without a drag keeps
            // the usual single-target action, but dispatches it on mouse release.
            drag.Reset();
            if (box)
            {
                if (requests.Count > 0)
                    clickToMove.Value.EnqueueInteractions(requests);
            }
            else if (Probe(anchor) is { } request)
                clickToMove.Value.EnqueueInteract(request, anchor, startImmediately: CanClickToMove);
            else
            {
                bool previous = rightClickAction.Value;
                rightClickAction.Value = true;
                try { TryShowLookupWorldMenu(); }
                finally { rightClickAction.Value = previous; }
            }
        }
        catch (Exception ex)
        {
            OnClickToMoveFailed(ex);
        }
    }

    private void DrawBoxSelectDrag(SpriteBatch batch)
    {
        BoxSelectDrag drag = boxSelectDrag.Value;
        if (!UsePelicom || clickToMoveFailed.Value || !Context.IsWorldReady || !drag.IsActive || !drag.HasDragged)
            return;
        Rectangle world = drag.Bounds;
        Vector2 local = Game1.GlobalToLocal(Game1.viewport, new Vector2(world.X, world.Y));
        BoxSelectRenderer.Draw(batch, new Rectangle((int)local.X, (int)local.Y, world.Width, world.Height),
            new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height));
    }
}
