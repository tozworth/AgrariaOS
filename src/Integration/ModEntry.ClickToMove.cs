using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private bool CanClickToMove => UsePelicom && config.ClickToMove && !clickToMoveFailed.Value
        && Context.CanPlayerMove && !HasDesktopFocus && !Game1.IsChatting && !Game1.eventUp && Game1.currentMinigame == null
        && Game1.farmEvent == null && !Game1.paused && !Game1.HostPaused && !Game1.fadeToBlack
        && !Game1.freezeControls && !Game1.panMode && Game1.locationRequest == null
        && Game1.game1.IsActiveNoOverlay && !Game1.player.UsingTool && !Game1.player.isEating
        && Game1.player.freezePause <= 0 && !Game1.player.IsSitting() && !Game1.player.isRidingHorse()
        && !Game1.player.swimming.Value && !Game1.player.isRafting
        // Weapons keep vanilla left-click (swing/aim); never start or continue pathfinding while armed.
        && Game1.player.CurrentTool is not MeleeWeapon && Game1.player.CurrentTool is not Slingshot;

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
        clickToMove.Value.Cancel();
        clickToMoveFailed.Value = true;
        Monitor.Log($"Click-to-move failed; restored vanilla left-click for this screen.\n{ex}", LogLevel.Error);
    }

    // While our own tool repeat owns the swing animation, clicks can't start new
    // movement (CanClickToMove is false while UsingTool) but must still append to
    // the queue. Every other gate stays so menus/events/weapons never queue.
    private bool CanAppendToQueue => UsePelicom && config.ClickToMove && !clickToMoveFailed.Value
        && Context.IsPlayerFree && !HasDesktopFocus && !Game1.IsChatting && !Game1.eventUp
        && Game1.currentMinigame == null && Game1.farmEvent == null && !Game1.paused && !Game1.HostPaused
        && !Game1.fadeToBlack && !Game1.freezeControls && !Game1.panMode && Game1.locationRequest == null
        && Game1.game1.IsActiveNoOverlay && !Game1.player.isEating
        && Game1.player.freezePause <= 0 && !Game1.player.IsSitting() && !Game1.player.isRidingHorse()
        && !Game1.player.swimming.Value && !Game1.player.isRafting
        && Game1.player.CurrentTool is not MeleeWeapon && Game1.player.CurrentTool is not Slingshot
        && Game1.activeClickableMenu == null
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
        // Keep brackets visible while swinging/talking so the remaining queue stays readable.
        if (!UsePelicom || clickToMoveFailed.Value || !Context.IsWorldReady)
            return;
        var queued = clickToMove.Value.GetQueueDisplay();
        if (queued.Count == 0)
            return;
        const int length = 12;
        const int width = 2;
        foreach (var (tile, color) in queued)
        {
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
