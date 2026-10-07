using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

internal static class UiText
{
    internal static void DrawCell(SpriteBatch batch, string text, Rectangle cell, Color color, ModConfig config,
        bool rightAligned = false, int padding = 4)
    {
        int width = cell.Width - padding * 2 - 1; // Reserve the shadow and divider pixel too.
        if (width <= 0 || string.IsNullOrEmpty(text))
            return;
        string fitted = Fit(text, width, config.FontScale);
        Vector2 size = Game1.smallFont.MeasureString(fitted) * config.FontScale;
        if (size.Y + 1 > cell.Height)
            return;
        float x = rightAligned ? cell.Right - padding - 1 - size.X : cell.X + padding;
        Draw(batch, fitted, new Vector2(x, cell.Y + (cell.Height - size.Y) / 2), color, config);
    }

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

    internal static int LineHeight(float scale)
        => (int)MathF.Ceiling(Math.Max(Game1.smallFont.LineSpacing,
            Game1.smallFont.Glyphs.Max(glyph => glyph.Cropping.Height)) * scale) + 2;

    internal static List<string> Wrap(string text, int width, float scale)
    {
        var lines = new List<string>();
        if (width <= 0 || string.IsNullOrEmpty(text))
            return lines;
        string line = "";
        foreach (string word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (Game1.smallFont.MeasureString(candidate).X * scale <= width)
            {
                line = candidate;
                continue;
            }
            if (line.Length > 0)
                lines.Add(line);
            line = "";
            // Split long words too, without cutting a surrogate pair.
            for (int i = 0; i < word.Length; i++)
            {
                string glyph = char.IsHighSurrogate(word[i]) && i + 1 < word.Length
                    ? word.Substring(i++, 2) : word[i].ToString();
                if (line.Length > 0 && Game1.smallFont.MeasureString(line + glyph).X * scale > width)
                {
                    lines.Add(line);
                    line = "";
                }
                line += glyph;
            }
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    internal static void Draw(SpriteBatch batch, string text, Vector2 position, Color color, ModConfig config, float opacity = 1f)
    {
        position = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        // Whole font scales and pixel-aligned positions avoid resampling the bitmap glyphs.
        // The game's HUD batch already uses PointClamp; don't restart or alter its shared batch.
        batch.DrawString(Game1.smallFont, text, position + new Vector2(1, 1), Color.Black * (config.Opacity * opacity * 0.8f), 0,
            Vector2.Zero, config.FontScale, SpriteEffects.None, 0.9f);
        batch.DrawString(Game1.smallFont, text, position, color * (config.Opacity * opacity), 0,
            Vector2.Zero, config.FontScale, SpriteEffects.None, 0.91f);
    }

    internal static void DrawBold(SpriteBatch batch, string text, Vector2 position, Color color, ModConfig config)
    {
        position = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        // Thicken the bitmap by one UI pixel, keeping glyphs and both shadow passes aligned.
        Color shadow = Color.Black * (config.Opacity * 0.8f);
        for (int offset = 0; offset <= 1; offset++)
            batch.DrawString(Game1.smallFont, text, position + new Vector2(offset + 1, 1), shadow, 0,
                Vector2.Zero, config.FontScale, SpriteEffects.None, 0.9f);
        for (int offset = 0; offset <= 1; offset++)
            batch.DrawString(Game1.smallFont, text, position + new Vector2(offset, 0), color * config.Opacity, 0,
                Vector2.Zero, config.FontScale, SpriteEffects.None, 0.91f);
    }
}
