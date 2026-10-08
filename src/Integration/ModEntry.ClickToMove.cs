using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using AgrariaOS.Hud;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private readonly AnimalQueueOutline animalQueueOutline = new();
    private bool CanQueueClickToMove => UsePelicom && config.ClickToMove && !clickToMoveFailed.Value
        && Context.IsPlayerFree && !HasDesktopFocus && !Game1.IsChatting && !Game1.eventUp && Game1.currentMinigame == null
        && Game1.farmEvent == null && !Game1.paused && !Game1.HostPaused && !Game1.fadeToBlack
        && !Game1.freezeControls && !Game1.panMode && Game1.locationRequest == null
        && Game1.game1.IsActiveNoOverlay && !Game1.player.isEating
        && Game1.player.freezePause <= 0 && !Game1.player.IsSitting() && !Game1.player.isRidingHorse()
        && !Game1.player.swimming.Value && !Game1.player.isRafting
        // Weapons keep vanilla left-click (swing/aim); never start or continue pathfinding while armed.
        && Game1.player.CurrentTool is not MeleeWeapon && Game1.player.CurrentTool is not Slingshot;

    private bool CanClickToMove => CanQueueClickToMove && Context.CanPlayerMove && !Game1.player.UsingTool
        && !Game1.player.FarmerSprite.PauseForSingleAnimation;

    // Crop harvesting sets CanMove=false until its pickup animation finishes.
    // Selection continues; deferred collection legs wait for normal movement to resume.
    private bool CanSelectCollections => CanQueueClickToMove
        && (!Game1.player.UsingTool || clickToMove.Value.IsToolActive);

    private static bool HasManualMovement()
    {
        KeyboardState keys = Game1.GetKeyboardState();
        var options = Game1.options;
        if (Game1.isOneOfTheseKeysDown(keys, options.moveUpButton)
            || Game1.isOneOfTheseKeysDown(keys, options.moveRightButton)
            || Game1.isOneOfTheseKeysDown(keys, options.moveDownButton)
            || Game1.isOneOfTheseKeysDown(keys, options.moveLeftButton))
            return true;
        GamePadState pad = Game1.input.GetGamePadState();
        return options.gamepadControls && (Math.Abs(pad.ThumbSticks.Left.X) > 0.2f
            || Math.Abs(pad.ThumbSticks.Left.Y) > 0.2f || pad.IsButtonDown(Buttons.DPadUp)
            || pad.IsButtonDown(Buttons.DPadRight) || pad.IsButtonDown(Buttons.DPadDown) || pad.IsButtonDown(Buttons.DPadLeft));
    }

    private static bool IsFurnitureLeftClick(GameLocation? location, Farmer farmer, Point tile)
        => farmer.ActiveObject is StardewValley.Objects.Furniture
            || location?.GetFurnitureAt(new Vector2(tile.X, tile.Y)) != null;

    private static bool ShouldUseVanillaLeftClick(GameLocation? location, Farmer farmer, Point tile)
    {
        // Furniture owns left-click at any distance. Decoratable locations allow
        // remote pickup; vanilla retains removal restrictions and outdoor reach.
        // Held furniture also keeps invalid placement clicks out of pathfinding.
        if (IsFurnitureLeftClick(location, farmer, tile))
            return true;
        if (location == null)
            return false;
        Vector2 key = new(tile.X, tile.Y);

        if (!Utility.tileWithinRadiusOfPlayer(tile.X, tile.Y, 1, farmer)
            || farmer.CurrentTool is not (Axe or Pickaxe))
            return false;

        // An explicitly equipped removal tool may hit placed objects. Debris keeps
        // automatic tool selection/repeat; never probe by performing a tool action.
        if (location.Objects.TryGetValue(key, out var obj))
            return !obj.IsTwig() && !obj.IsBreakableStone() && !obj.IsWeeds();

        return location.terrainFeatures.TryGetValue(key, out var feature)
            && (feature is Flooring || feature is Tree tree && tree.tapped.Value);
    }

    private void UpdateClickToMove(bool beforeUpdate)
    {
        try
        {
            if (beforeUpdate)
                clickToMove.Value.BeforeUpdate(CanClickToMove, Context.IsWorldReady && HasManualMovement());
            else
            {
                clickToMove.Value.AfterUpdate(CanClickToMove);
                if (clickToMove.Value.TryConsumeInteractArrival(out Point tile, out var perform))
                    TryPerformInteractArrival(tile, perform);
            }
        }
        catch (Exception ex)
        {
            OnClickToMoveFailed(ex);
        }
    }

    private void OnClickToMoveFailed(Exception ex)
    {
        machineCollectDrag.Value.Reset();
        rockMiningDrag.Value.Reset();
        animalHarvestDrag.Value.Reset();
        clickToMove.Value.Cancel();
        clickToMoveFailed.Value = true;
        Monitor.Log($"Click-to-move failed; restored vanilla left-click for this screen.\n{ex}", LogLevel.Error);
    }

    // While our own tool repeat owns the swing animation, clicks can't start new
    // movement (CanClickToMove is false while UsingTool) but must still append to
    // the queue. Every other gate stays so menus/events/weapons never queue.
    private bool CanAppendToQueue => CanQueueClickToMove
        && clickToMove.Value.IsBusy && (Game1.player.UsingTool || clickToMove.Value.IsToolActive);

    private bool TryQueueLeftClick(Point tile)
    {
        // Ctrl+click always queues (plain moves included). Plain tool/approach clicks
        // auto-queue while a route runs so "click 5 rocks" mines in succession; plain
        // ground and warp clicks still replace. Shift keeps vanilla handling above.
        bool explicitQueue = Helper.Input.IsDown(SButton.LeftControl) || Helper.Input.IsDown(SButton.RightControl);
        if (explicitQueue)
        {
            // Warps change location and invalidate the queue; let Start replace instead.
            if (Movement.WarpDestination.Resolve(Game1.currentLocation, Game1.player, tile) != null)
                return false;
            return clickToMove.Value.EnqueueLeftExplicit(tile);
        }
        if (!clickToMove.Value.IsBusy)
            return false;
        return clickToMove.Value.TryEnqueueToolOrApproach(tile);
    }

    private void DrawMoveDestination(SpriteBatch batch)
    {
        // Keep indicators visible while swinging/talking so the queue stays readable.
        if (!UsePelicom || clickToMoveFailed.Value || !Context.IsWorldReady)
            return;
        var queued = clickToMove.Value.GetQueueDisplay();
        if (queued.Count == 0)
            return;
        const int length = 12;
        const int width = 2;
        Dictionary<Character, int> animalLayers = new();
        foreach (var indicator in queued)
        {
            Point tile = indicator.Tile;
            Color color = indicator.Color;
            if (indicator.Animal is { } animal)
            {
                animalLayers.TryGetValue(animal, out int layer);
                animalLayers[animal] = layer + 1;
                try { animalQueueOutline.Draw(batch, animal, color, layer); }
                catch (Exception ex) { Monitor.Log($"Animal queue outline failed.\n{ex}", LogLevel.Trace); }
                continue;
            }
            Vector2 pixel = Game1.GlobalToLocal(Game1.viewport, new Vector2(tile.X * 64, tile.Y * 64));
            // Cull far off-screen brackets instead of drawing at clamped viewport edges.
            if (pixel.X < -80 || pixel.Y < -80 || pixel.X > Game1.viewport.Width + 80 || pixel.Y > Game1.viewport.Height + 80)
                continue;
            Color tint = color * 0.85f;
            int x = (int)pixel.X + 3;
            int y = (int)pixel.Y + 3;
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    batch.Draw(Game1.staminaRect, new Rectangle(x + column * (58 - length),
                        y + row * (58 - width), length, width), tint);
                    batch.Draw(Game1.staminaRect, new Rectangle(x + column * (58 - width),
                        y + row * (58 - length), width, length), tint);
                }
            }
        }
    }
}
