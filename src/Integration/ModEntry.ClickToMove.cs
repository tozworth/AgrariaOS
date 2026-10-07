using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private bool CanClickToMove => UsePelicom && config.ClickToMove && !clickToMoveFailed.Value
        && Context.CanPlayerMove && !HasDesktopFocus && !Game1.IsChatting && !Game1.eventUp && Game1.currentMinigame == null
        && Game1.farmEvent == null && !Game1.paused && !Game1.HostPaused && !Game1.fadeToBlack
        && !Game1.freezeControls && !Game1.panMode && Game1.locationRequest == null
        && Game1.game1.IsActiveNoOverlay && !Game1.player.UsingTool && !Game1.player.isEating
        && Game1.player.freezePause <= 0 && !Game1.player.IsSitting() && !Game1.player.isRidingHorse()
        && !Game1.player.swimming.Value && !Game1.player.isRafting;

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
                clickToMove.Value.AfterUpdate(CanClickToMove);
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

    private void DrawMoveDestination(SpriteBatch batch)
    {
        if (!CanClickToMove || clickToMove.Value.Destination is not { } tile)
            return;
        Vector2 pixel = Game1.GlobalToLocal(Game1.viewport, new Vector2(tile.X * 64, tile.Y * 64));
        Color color = new Color(93, 214, 221) * 0.7f;
        const int length = 12;
        const int width = 2;
        int x = (int)pixel.X + 3;
        int y = (int)pixel.Y + 3;
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                batch.Draw(Game1.staminaRect, new Rectangle(x + column * (58 - length),
                    y + row * (58 - width), length, width), color);
                batch.Draw(Game1.staminaRect, new Rectangle(x + column * (58 - width),
                    y + row * (58 - length), width, length), color);
            }
        }
    }
}
