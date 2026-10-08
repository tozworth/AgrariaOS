using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

partial class GridView<TRow> where TRow : class
{
    internal void Draw(SpriteBatch batch, Rectangle bounds, int rowHeight, ModConfig config, Point cursor,
        string emptyText, Func<TRow, bool>? isSelected = null, string? footerText = null,
        Func<TRow, bool>? isDimmed = null)
    {
        if (!Layout(bounds, rowHeight, reserveFooter: !string.IsNullOrEmpty(footerText)))
        {
            UiText.DrawCell(batch, "Not enough space.", bounds, UiStyle.Muted, config);
            return;
        }
        for (int index = 0; index < visibleColumns.Count; index++)
        {
            int i = visibleColumns[index];
            batch.Draw(Game1.staminaRect, headers[i], UiStyle.Border * (config.Opacity * 0.45f));
            DrawHeader(batch, i, config);
            if (index < visibleColumns.Count - 1)
            {
                Rectangle divider = new(headers[i].Right - 1, headers[i].Y, 1, rowHeight + 1 + RowsBounds.Height);
                batch.Draw(Game1.staminaRect, divider, UiStyle.Border * config.Opacity);
                if (resizingColumn == i || DividerBounds(i, visibleColumns[index + 1]).Contains(cursor))
                    batch.Draw(Game1.staminaRect, new Rectangle(divider.X, divider.Y, 2, rowHeight), UiStyle.Primary * config.Opacity);
            }
        }
        batch.Draw(Game1.staminaRect, new Rectangle(RowsBounds.X, RowsBounds.Y - 1, RowsBounds.Width, 1), UiStyle.Border * config.Opacity);

        ModConfig? dimmedConfig = null;
        if (isDimmed != null)
        {
            dimmedConfig = new ModConfig
            {
                Opacity = config.Opacity * 0.5f,
                TextScale = config.TextScale,
                PixelPerfectText = config.PixelPerfectText,
            };
        }
        for (int i = layoutFirstRow; i < layoutEndRow; i++)
        {
            Rectangle row = new(RowsBounds.X, RowsBounds.Y + (i - layoutFirstRow) * rowHeight, RowsBounds.Width, rowHeight);
            if (isSelected?.Invoke(rows[i]) == true)
                batch.Draw(Game1.staminaRect, row, new Color(54, 101, 129) * config.Opacity);
            else if (row.Contains(cursor))
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.55f));
            else if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));
            ModConfig rowConfig = isDimmed?.Invoke(rows[i]) == true && dimmedConfig != null ? dimmedConfig : config;
            foreach (int column in visibleColumns)
                columns[column].DrawCell(batch, rows[i], new Rectangle(headers[column].X, row.Y, headers[column].Width, row.Height), rowConfig);
        }

        if (rows.Count == 0)
        {
            emptyText = UiText.Fit(emptyText, Math.Max(0, RowsBounds.Width - 1), config.FontScale);
            Vector2 size = Game1.smallFont.MeasureString(emptyText) * config.FontScale;
            if (emptyText.Length > 0 && size.Y + 1 <= RowsBounds.Height)
                UiText.Draw(batch, emptyText, new Vector2(RowsBounds.X + (RowsBounds.Width - size.X) / 2,
                    RowsBounds.Y + (RowsBounds.Height - size.Y) / 2), UiStyle.Muted, config);
        }
        if (!scrollThumb.IsEmpty)
        {
            batch.Draw(Game1.staminaRect, scrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, scrollThumb, UiStyle.Muted * config.Opacity);
        }
        if (!footerBounds.IsEmpty)
        {
            UiText.DrawCell(batch, footerText ?? "", footerBounds, UiStyle.Muted, config);
        }
    }

    private void DrawHeader(SpriteBatch batch, int column, ModConfig config)
    {
        Rectangle textBounds = headers[column];
        if (SortColumn == column && textBounds.Width >= Padding * 2 + 6)
        {
            textBounds.Width -= 9;
            int centerX = headers[column].Right - Padding - 4;
            int top = headers[column].Y + (headers[column].Height - 3) / 2;
            for (int y = 0; y < 3; y++)
            {
                int halfWidth = SortDescending ? 2 - y : y;
                batch.Draw(Game1.staminaRect, new Rectangle(centerX - halfWidth, top + y, halfWidth * 2 + 1, 1),
                    UiStyle.Primary * config.Opacity);
            }
        }
        UiText.DrawCell(batch, columns[column].Heading, textBounds, UiStyle.Primary, config, columns[column].RightAligned);
    }

}
