using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace AgrariaOS.Hud;

internal static partial class HoverHighlight
{
    internal static void Draw(SpriteBatch batch, Rectangle worldBounds)
    {
        if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
            return;
        Vector2 local = Game1.GlobalToLocal(Game1.viewport, new Vector2(worldBounds.X, worldBounds.Y));
        Rectangle rect = new((int)local.X, (int)local.Y, worldBounds.Width, worldBounds.Height);
        if (rect.Right < -64 || rect.Bottom < -64 || rect.X > Game1.viewport.Width + 64 || rect.Y > Game1.viewport.Height + 64)
            return;
        batch.Draw(Game1.staminaRect, rect, Color.White * 0.10f);
        Color edge = Color.White * 0.30f;
        batch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, 1), edge);
        batch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), edge);
        batch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, 1, rect.Height), edge);
        batch.Draw(Game1.staminaRect, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edge);
    }

}
