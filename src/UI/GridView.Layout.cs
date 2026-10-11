using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

partial class GridView<TRow> where TRow : class
{
    internal bool Layout(Rectangle bounds, int rowHeight, bool reserveFooter = false)
    {
        if (rowHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowHeight));
        ClearLayout();
        this.rowHeight = rowHeight;
        int availableHeight = Math.Max(0, bounds.Height - Padding * 2 - rowHeight - 1);
        bool showFooter = reserveFooter;
        if (showFooter)
            availableHeight = Math.Max(0, availableHeight - rowHeight);
        int tableWidth = Math.Max(0, bounds.Width - Padding * 2 - ScrollbarWidth);
        if (availableHeight < rowHeight || tableWidth == 0)
        {
            EndDrag();
            return false;
        }

        List<int> effective = GetEffectiveColumns();
        FitColumns(tableWidth, effective);
        int x = bounds.X + Padding;
        foreach (int i in effective)
        {
            headers[i] = new Rectangle(x, bounds.Y + Padding, columnWidths[i], rowHeight);
            x += columnWidths[i];
        }
        VisibleRows = availableHeight / rowHeight;
        FirstRow = Math.Clamp(FirstRow, 0, MaxFirstRow);
        layoutFirstRow = FirstRow;
        layoutEndRow = Math.Min(rows.Count, FirstRow + VisibleRows);
        RowsBounds = new Rectangle(bounds.X + Padding, bounds.Y + Padding + rowHeight + 1, tableWidth, availableHeight);
        scrollTrack = new Rectangle(RowsBounds.Right + 2, RowsBounds.Y, ScrollbarWidth - 4, RowsBounds.Height);
        if (MaxFirstRow > 0)
        {
            int thumbHeight = Math.Min(scrollTrack.Height, Math.Max(16, scrollTrack.Height * VisibleRows / rows.Count));
            int thumbY = scrollTrack.Y + (int)Math.Round((scrollTrack.Height - thumbHeight) * (double)FirstRow / MaxFirstRow);
            scrollThumb = new Rectangle(scrollTrack.X, thumbY, scrollTrack.Width, thumbHeight);
        }
        if (showFooter)
            footerBounds = new Rectangle(RowsBounds.X, bounds.Bottom - Padding - rowHeight, tableWidth, rowHeight);
        return true;
    }

    private void FitColumns(int width, List<int> effective)
    {
        int minimumTotal = 0;
        long weightTotal = 0;
        Array.Clear(columnWidths);
        foreach (int i in effective)
        {
            columnWidths[i] = GetMinimumWidth(i, width, effective.Count);
            minimumTotal += columnWidths[i];
            weightTotal += Math.Max(0, preferredWidths[i] - columnWidths[i]);
        }
        int used = 0;
        foreach (int i in effective)
        {
            int weight = Math.Max(0, preferredWidths[i] - columnWidths[i]);
            columnWidths[i] += weightTotal == 0 ? (width - minimumTotal) / effective.Count
                : (int)((long)(width - minimumTotal) * weight / weightTotal);
            used += columnWidths[i];
        }
        columnWidths[effective[0]] += width - used;
    }

    private int GetMinimumWidth(int column, int tableWidth, int visibleCount = 0)
        => Math.Min(columns[column].MinimumWidth, tableWidth / Math.Max(1, visibleCount > 0 ? visibleCount : GetEffectiveColumns().Count));

    private Rectangle DividerBounds(int column, int rightColumn)
        => headers[column].Width == 0 || headers[rightColumn].Width == 0 ? Rectangle.Empty
            : new Rectangle(headers[column].Right - 3, headers[column].Y, 7, headers[column].Height);

    private void ClearLayout()
    {
        Array.Clear(headers);
        RowsBounds = scrollTrack = scrollThumb = footerBounds = Rectangle.Empty;
        VisibleRows = layoutFirstRow = layoutEndRow = 0;
    }

    internal int GetRowHeight(SpriteFont font, float scale, int minimumHeight = 0)
    {
        if (!ReferenceEquals(measuredFont, font))
        {
            measuredFont = font;
            glyphHeight = 0;
            foreach (SpriteFont.Glyph glyph in font.Glyphs)
                glyphHeight = Math.Max(glyphHeight, glyph.Cropping.Height);
        }
        // Stardew's LineSpacing can be smaller than its glyphs. Include their full
        // height and the pixel shadow without touching the shared font or batch.
        return Math.Max(minimumHeight, (int)MathF.Ceiling(Math.Max(font.LineSpacing, glyphHeight) * scale) + 2);
    }

}
