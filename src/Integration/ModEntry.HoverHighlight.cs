using AgrariaOS.Hud;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS;

public sealed partial class ModEntry
{
    private void DrawHoverHighlight(SpriteBatch batch)
    {
        if (!UsePelicom || !config.HoverHighlight || !Context.IsWorldReady)
            return;
        try
        {
            if (Game1.currentLocation == null || Game1.player == null)
                return;
            if (Game1.activeClickableMenu != null || Game1.IsChatting || Game1.eventUp || Game1.currentMinigame != null)
                return;
            Point cursorUi = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
            try
            {
                if (Game1.onScreenMenus.Any(menu => (Game1.IsHudDrawn || menu == Game1.chatBox)
                        && menu.isWithinBounds(cursorUi.X, cursorUi.Y)))
                    return;
            }
            catch { }
            try
            {
                if (pelicom.Value.IsInteractive && pelicom.Value.isWithinBounds(cursorUi.X, cursorUi.Y))
                    return;
            }
            catch { }
            try
            {
                if (windowManager.Value.IsContextMenuOpen || windowManager.Value.IsOverWindow(cursorUi))
                    return;
            }
            catch { }
            int worldX = Game1.getMouseX(ui_scale: false) + Game1.viewport.X;
            int worldY = Game1.getMouseY(ui_scale: false) + Game1.viewport.Y;
            if (!HoverHighlight.TryGetBounds(Game1.currentLocation, worldX, worldY, out Rectangle worldBounds))
                return;
            HoverHighlight.Draw(batch, worldBounds);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Hover highlight failed.\n{ex}", LogLevel.Trace);
        }
    }
}
