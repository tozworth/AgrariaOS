using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

internal static class UiText
{
    internal static string Fit(string text, int width, float scale)
    {
        if (width <= 0 || string.IsNullOrEmpty(text))
            return "";
        if (Game1.smallFont.MeasureString(text).X * scale <= width)
            return text;
        const string suffix = "...";
        if (Game1.smallFont.MeasureString(suffix).X * scale > width)
            return "";
        int length = text.Length;
        while (length > 0)
        {
            length--;
            if (length > 0 && char.IsHighSurrogate(text[length - 1]))
                length--;
            string shortened = text[..length] + suffix;
            if (Game1.smallFont.MeasureString(shortened).X * scale <= width)
                return shortened;
        }
        return suffix;
    }

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
