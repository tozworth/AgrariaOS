using AgrariaOS.Configuration;
using AgrariaOS.Hud;
using AgrariaOS.Integration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

partial class BundlesWindow : OsWindow
{
    private static void DrawBundleCell(SpriteBatch batch, BundleRow row, Rectangle bounds, ModConfig config)
    {
        Color color;
        bool strike;
        if (row.BundleComplete)
        {
            color = UiStyle.Muted;
            strike = true;
        }
        else if (row.Completed)
        {
            color = UiStyle.Muted;
            strike = false;
        }
        else
        {
            color = UiStyle.Primary;
            strike = false;
        }
        UiText.DrawCell(batch, row.BundleName, bounds, color, config);
        if (!strike)
            return;
        const int padding = 4;
        int width = bounds.Width - padding * 2 - 1;
        if (width <= 0 || string.IsNullOrEmpty(row.BundleName))
            return;
        string fitted = UiText.Fit(row.BundleName, width, config.FontScale);
        if (string.IsNullOrEmpty(fitted))
            return;
        Vector2 size;
        try { size = UiText.Measure(fitted) * config.FontScale; }
        catch { return; }
        if (size.X <= 0)
            return;
        int x = bounds.X + padding;
        int y = bounds.Y + bounds.Height / 2;
        batch.Draw(Game1.staminaRect, new Rectangle(x, y, Math.Max(1, (int)MathF.Round(size.X)), 1), color * config.Opacity);
    }

    private static void DrawItemCell(SpriteBatch batch, BundleRow row, Rectangle bounds, ModConfig config)
    {
        if (!string.IsNullOrEmpty(row.IconId))
        {
            try
            {
                var data = ItemRegistry.GetDataOrErrorItem(row.IconId);
                Rectangle source = data.GetSourceRect(0, row.SpriteIndex);
                if (source.Width > 0 && source.Height > 0)
                {
                    float scale = Math.Min((float)IconSize / source.Width, (float)IconSize / source.Height);
                    if (scale >= 1)
                        scale = MathF.Floor(scale);
                    int width = Math.Max(1, (int)(source.Width * scale));
                    int height = Math.Max(1, (int)(source.Height * scale));
                    Rectangle destination = new(bounds.X + Padding + (IconSize - width) / 2,
                        bounds.Y + (bounds.Height - height) / 2, width, height);
                    batch.Draw(data.GetTexture(), destination, source, Color.White * config.Opacity);
                    if (row.Quality > 0)
                    {
                        Rectangle star = row.Quality < 4 ? new Rectangle(338 + (row.Quality - 1) * 8, 400, 8, 8) : new Rectangle(346, 392, 8, 8);
                        batch.Draw(Game1.mouseCursors, new Rectangle(destination.Right - 8, destination.Bottom - 8, 8, 8), star, Color.White * config.Opacity);
                    }
                }
            }
            catch { }
        }
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.ItemName, bounds, row.Completed ? UiStyle.Muted : UiStyle.Primary, config);
    }

    private static void DrawStatusCell(SpriteBatch batch, BundleRow row, Rectangle bounds, ModConfig config)
    {
        if (!row.Completed)
        {
            UiText.DrawCell(batch, row.StatusText, bounds, UiStyle.Muted, config);
            return;
        }
        // Pixel checkmark keeps donated rows independent of font glyph coverage.
        Point center = bounds.Center;
        Color color = VitalsRenderer.FullColor * config.Opacity;
        for (int i = 0; i < 4; i++)
        {
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 4 + i, center.Y + i, 1, 1), color);
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 4 + i, center.Y + i + 1, 1, 1), color);
        }
        for (int i = 0; i < 7; i++)
        {
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 1 + i, center.Y + 3 - i, 1, 1), color);
            batch.Draw(Game1.staminaRect, new Rectangle(center.X - 1 + i, center.Y + 4 - i, 1, 1), color);
        }
    }

}
