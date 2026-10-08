using AgrariaOS.Movement;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private static bool IsRockMiningButtonHeld(IInputHelper input)
        => input.IsDown(SButton.MouseLeft) || input.IsSuppressed(SButton.MouseLeft);

    private void TryBeginRockMiningDrag(Vector2 pixel, Point tile)
    {
        if (HasManualMovement() || ToolTarget.ResolveRock(Game1.currentLocation, Game1.player, tile) == null)
            return;
        rockMiningDrag.Value.Begin(Game1.currentLocation, Game1.player, Game1.stats.DaysPlayed, pixel);
        // The initiating click already selected this rock.
        foreach (Point _ in rockMiningDrag.Value.Sample(pixel)) { }
        Monitor.Log($"MiningDrag started tile={tile} queue={clickToMove.Value.TotalCount}", LogLevel.Trace);
    }

    private void UpdateRockMiningDrag()
    {
        MachineCollectDrag drag = rockMiningDrag.Value;
        if (!drag.IsActive)
            return;
        if (!IsRockMiningButtonHeld(Helper.Input) || !CanSelectCollections
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
                if (clickToMove.Value.EnqueueRock(tile, startImmediately: CanClickToMove))
                    Monitor.Log($"MiningDrag crossed tile={tile} queue={clickToMove.Value.TotalCount} {clickToMove.Value.LastDebug}", LogLevel.Trace);
            }
        }
        catch (Exception ex)
        {
            OnClickToMoveFailed(ex);
        }
    }
}
