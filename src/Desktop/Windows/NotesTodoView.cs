using AgrariaOS.Configuration;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;

namespace AgrariaOS.Desktop.Windows;

partial class NotesWindow : OsWindow
{
    private void DrawTodoItems(SpriteBatch batch, NoteRecord doc, Rectangle bounds, Point cursor, ModConfig config)
    {
        lastItemCount = doc.Items.Count;
        lastVisibleItems = 1;
        if (bounds.Width <= 0 || bounds.Height <= 0 || rowHeight <= 0)
            return;
        int visible = Math.Max(1, bounds.Height / rowHeight);
        lastVisibleItems = visible;
        firstItem = Math.Clamp(firstItem, 0, Math.Max(0, doc.Items.Count - visible));
        int end = Math.Min(doc.Items.Count, firstItem + visible);
        if (doc.Items.Count == 0)
        {
            DrawCenteredText(batch, "Nothing to do.", bounds, UiStyle.Muted, config);
            return;
        }
        bool needsScroll = doc.Items.Count > visible;
        int rowsWidth = needsScroll ? Math.Max(0, bounds.Width - 8) : bounds.Width;
        for (int i = firstItem; i < end; i++)
        {
            TodoRecord item = doc.Items[i];
            Rectangle row = new(bounds.X, bounds.Y + (i - firstItem) * rowHeight, rowsWidth,
                Math.Min(rowHeight, bounds.Bottom - (bounds.Y + (i - firstItem) * rowHeight)));
            bool hovered = row.Contains(cursor);
            if (hovered)
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.55f));
            else if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));

            Rectangle check = new(row.X + 6, row.Center.Y - 5, 10, 10);
            UiStyle.DrawBorder(batch, check, config.Opacity);
            if (item.Done)
                batch.Draw(Game1.staminaRect, new Rectangle(check.X + 3, check.Y + 3, 4, 4), UiStyle.Primary * config.Opacity);

            int deleteWidth = 18;
            Rectangle delete = new(row.Right - deleteWidth - 2, row.Y, deleteWidth, row.Height);
            Rectangle textBounds = new(check.Right + 6, row.Y, Math.Max(0, delete.X - check.Right - 6), row.Height);
            Color color = item.Done ? UiStyle.Muted : UiStyle.Primary;
            string fitted = UiText.Fit(item.Text, Math.Max(0, textBounds.Width - Padding * 2 - 1), config.FontScale);
            Vector2 size = UiText.Measure(fitted) * config.FontScale;
            if (!string.IsNullOrEmpty(fitted) && size.Y + 1 <= textBounds.Height)
            {
                Vector2 position = new(textBounds.X + Padding, textBounds.Y + (textBounds.Height - size.Y) / 2);
                UiText.Draw(batch, fitted, position, color, config);
                if (item.Done && size.X > 0)
                {
                    float y = textBounds.Y + (textBounds.Height - 1) / 2f;
                    batch.Draw(Game1.staminaRect, new Rectangle(textBounds.X + Padding, (int)MathF.Round(y),
                        (int)MathF.Ceiling(size.X), 1), color * config.Opacity);
                }
            }
            if (hovered)
            {
                string glyph = "X";
                Vector2 glyphSize = UiText.Measure(glyph) * config.FontScale;
                UiText.Draw(batch, glyph, new Vector2(delete.Center.X - glyphSize.X / 2,
                    delete.Center.Y - glyphSize.Y / 2),
                    delete.Contains(cursor) ? UiStyle.Primary : UiStyle.Muted, config);
            }
            drawnItems.Add((item, row, delete));
        }
        if (needsScroll && bounds.Height > 0)
        {
            itemScrollTrack = new Rectangle(bounds.Right - 6, bounds.Y, 4, visible * rowHeight);
            int maxFirst = Math.Max(1, doc.Items.Count - visible);
            int thumbHeight = Math.Min(itemScrollTrack.Height, Math.Max(16, itemScrollTrack.Height * visible / Math.Max(1, doc.Items.Count)));
            int thumbY = itemScrollTrack.Y + (int)Math.Round((itemScrollTrack.Height - thumbHeight) * (double)firstItem / maxFirst);
            itemScrollThumb = new Rectangle(itemScrollTrack.X, thumbY, itemScrollTrack.Width, thumbHeight);
            batch.Draw(Game1.staminaRect, itemScrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, itemScrollThumb, UiStyle.Muted * config.Opacity);
        }
    }

}
