using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

// Shared desktop form primitives. Owners supply values, hit testing and persistence;
// drawing never changes SpriteBatch state or uses fractional sprite scaling.
internal static class FormControls
{
    internal static void Field(SpriteBatch batch, Rectangle bounds, ModConfig config,
        bool hovered = false, bool focused = false, bool enabled = true)
    {
        batch.Draw(Game1.staminaRect, bounds, (hovered && enabled ? UiStyle.Border : Color.Black)
            * (config.Opacity * (hovered && enabled ? 0.45f : 0.2f)));
        UiStyle.DrawBorder(batch, bounds, config.Opacity * (enabled ? 1f : 0.45f));
        if (focused)
            batch.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1),
                UiStyle.Primary * config.Opacity);
    }

    internal static void Button(SpriteBatch batch, Rectangle bounds, string text, ModConfig config,
        Point cursor, bool enabled = true, bool focused = false)
    {
        Field(batch, bounds, config, bounds.Contains(cursor), focused, enabled);
        CenteredText(batch, text, bounds, enabled ? UiStyle.Primary : UiStyle.Muted, config);
    }

    internal static void Checkbox(SpriteBatch batch, Rectangle bounds, bool value, ModConfig config,
        Point cursor, bool enabled = true)
    {
        int size = Math.Min(16, Math.Min(bounds.Width, bounds.Height));
        Rectangle box = new(bounds.Center.X - size / 2, bounds.Center.Y - size / 2, size, size);
        Field(batch, box, config, bounds.Contains(cursor), enabled: enabled);
        if (!value) return;
        Color color = (enabled ? UiStyle.Primary : UiStyle.Muted) * config.Opacity;
        for (int i = 0; i < 4; i++)
            batch.Draw(Game1.staminaRect, new Rectangle(box.X + 3 + i, box.Y + 7 + i, 2, 2), color);
        for (int i = 0; i < 7; i++)
            batch.Draw(Game1.staminaRect, new Rectangle(box.X + 6 + i, box.Y + 10 - i, 2, 2), color);
    }

    internal static void Dropdown(SpriteBatch batch, Rectangle bounds, string value, ModConfig config,
        Point cursor, bool enabled = true, bool focused = false)
    {
        Field(batch, bounds, config, bounds.Contains(cursor), focused, enabled);
        Rectangle text = new(bounds.X, bounds.Y, Math.Max(0, bounds.Width - 20), bounds.Height);
        Color color = enabled ? UiStyle.Primary : UiStyle.Muted;
        UiText.DrawCell(batch, value, text, color, config);
        // Draw the indicator in pixels so narrow fields and larger fonts cannot
        // trim it away through text fitting.
        for (int i = 0; i < 5; i++)
            batch.Draw(Game1.staminaRect,
                new Rectangle(bounds.Right - 14 + i, bounds.Center.Y - 2 + i, 9 - i * 2, 1),
                color * config.Opacity);
    }

    private static void CenteredText(SpriteBatch batch, string text, Rectangle bounds, Color color, ModConfig config)
    {
        text = UiText.Fit(text, Math.Max(0, bounds.Width - 9), config.FontScale);
        Vector2 size = UiText.Measure(text) * config.FontScale;
        if (text.Length > 0 && size.Y + 1 <= bounds.Height)
            UiText.Draw(batch, text, new Vector2(bounds.Center.X - size.X / 2, bounds.Center.Y - size.Y / 2), color, config);
    }

    internal static Rectangle SliderTrack(Rectangle bounds)
        => new(bounds.X + 6, bounds.Center.Y - 2, Math.Max(1, bounds.Width - 12), 4);

    internal static int SliderValue(Rectangle bounds, Point cursor)
    {
        Rectangle track = SliderTrack(bounds);
        return (int)Math.Round(Math.Clamp((cursor.X - track.X) / (double)Math.Max(1, track.Width - 1), 0, 1) * 100);
    }

    internal static void Slider(SpriteBatch batch, Rectangle bounds, int value, ModConfig config,
        Point cursor, bool enabled = true)
    {
        Field(batch, bounds, config, bounds.Contains(cursor), enabled: enabled);
        Rectangle track = SliderTrack(bounds);
        int width = (int)Math.Round((track.Width - 1) * Math.Clamp(value, 0, 100) / 100d);
        Color color = (enabled ? UiStyle.Primary : UiStyle.Muted) * config.Opacity;
        batch.Draw(Game1.staminaRect, track, UiStyle.Border * config.Opacity);
        batch.Draw(Game1.staminaRect, new Rectangle(track.X, track.Y, width, track.Height), color * 0.65f);
        batch.Draw(Game1.staminaRect, new Rectangle(track.X + width - 2, bounds.Center.Y - 6, 5, 12), color);
    }
}
