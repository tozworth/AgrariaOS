using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

internal static class UiText
{
    internal static void Draw(SpriteBatch batch, string text, Vector2 position, Color color, ModConfig config)
    {
        position = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        // Whole font scales and pixel-aligned positions avoid resampling the bitmap glyphs.
        // The game's HUD batch already uses PointClamp; don't restart or alter its shared batch.
        batch.DrawString(Game1.smallFont, text, position + new Vector2(1, 1), Color.Black * (config.Opacity * 0.8f), 0,
            Vector2.Zero, config.FontScale, SpriteEffects.None, 0.9f);
        batch.DrawString(Game1.smallFont, text, position, color * config.Opacity, 0,
            Vector2.Zero, config.FontScale, SpriteEffects.None, 0.91f);
    }
}
