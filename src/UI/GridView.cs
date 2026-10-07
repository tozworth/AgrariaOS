using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

// Shared table behavior. Windows own row identity, selection, and domain actions;
// this view only orders a snapshot and never rearranges its source collection.
internal sealed class GridView<TRow> where TRow : class
{
    internal const int Padding = 4;
    private const int ScrollbarWidth = 10;
    private readonly GridColumn<TRow>[] columns;
    private readonly int[] preferredWidths;
    private readonly int[] columnWidths;
    private readonly Rectangle[] headers;
    private readonly List<TRow> rows = new();
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private Rectangle footerBounds;
    private int rowHeight;
    private int layoutFirstRow;
    private int layoutEndRow;
    private int resizingColumn = -1;
    private int resizeStartX;
    private int resizeLeftWidth;
    private int resizeRightWidth;
    private int? scrollDragOffset;
    private SpriteFont? measuredFont;
    private int glyphHeight;

    internal IReadOnlyList<TRow> Rows => rows;
    internal Rectangle RowsBounds { get; private set; }
    internal int FirstRow { get; private set; }
    internal int VisibleRows { get; private set; }
    internal int SortColumn { get; private set; } = -1;
    internal bool SortDescending { get; private set; }
    private int MaxFirstRow => Math.Max(0, rows.Count - VisibleRows);

    internal GridView(params GridColumn<TRow>[] columns)
    {
        if (columns.Length == 0)
            throw new ArgumentException("A grid needs at least one column.", nameof(columns));
        this.columns = columns.ToArray();
        preferredWidths = columns.Select(column => column.PreferredWidth).ToArray();
        columnWidths = new int[columns.Length];
        headers = new Rectangle[columns.Length];
    }

    // Call with the live source before drawing so stack/value changes are sorted too.
    internal void SetRows(IEnumerable<TRow> source)
    {
        TRow[] snapshot = (SortColumn < 0 ? source : columns[SortColumn].Sort(source, SortDescending)).ToArray();
        rows.Clear();
        rows.AddRange(snapshot);
        ClearLayout();
    }

    // Changing the source (e.g. opening another chest) preserves column preferences.
    internal void Clear()
    {
        rows.Clear();
        FirstRow = 0;
        ClearLayout();
        EndDrag();
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

    internal bool Layout(Rectangle bounds, int rowHeight, bool reserveFooter = false)
    {
        if (rowHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowHeight));
        ClearLayout();
        this.rowHeight = rowHeight;
        int availableHeight = Math.Max(0, bounds.Height - Padding * 2 - rowHeight - 1);
        bool showFooter = reserveFooter || rows.Count > availableHeight / rowHeight;
        if (showFooter)
            availableHeight = Math.Max(0, availableHeight - rowHeight);
        int tableWidth = Math.Max(0, bounds.Width - Padding * 2 - ScrollbarWidth);
        if (availableHeight < rowHeight || tableWidth == 0)
        {
            EndDrag();
            return false;
        }

        FitColumns(tableWidth);
        int x = bounds.X + Padding;
        for (int i = 0; i < headers.Length; i++)
        {
            headers[i] = new Rectangle(x, bounds.Y + Padding, columnWidths[i], rowHeight);
            x += columnWidths[i];
        }
        VisibleRows = availableHeight / rowHeight;
        FirstRow = Math.Clamp(FirstRow, 0, MaxFirstRow);
        layoutFirstRow = FirstRow;
        layoutEndRow = Math.Min(rows.Count, FirstRow + VisibleRows);
        RowsBounds = new Rectangle(headers[0].X, headers[0].Bottom + 1, tableWidth, availableHeight);
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

    internal void Draw(SpriteBatch batch, Rectangle bounds, int rowHeight, ModConfig config, Point cursor,
        string emptyText, Func<TRow, bool>? isSelected = null, string? footerText = null, bool showRange = true)
    {
        if (!Layout(bounds, rowHeight, reserveFooter: !string.IsNullOrEmpty(footerText)))
        {
            UiText.DrawCell(batch, "Not enough space.", bounds, UiStyle.Muted, config);
            return;
        }
        foreach (Rectangle header in headers)
            batch.Draw(Game1.staminaRect, header, UiStyle.Border * (config.Opacity * 0.45f));
        for (int i = 0; i < headers.Length; i++)
        {
            DrawHeader(batch, i, config);
            if (i < headers.Length - 1)
            {
                Rectangle divider = new(headers[i].Right - 1, headers[i].Y, 1, rowHeight + 1 + RowsBounds.Height);
                batch.Draw(Game1.staminaRect, divider, UiStyle.Border * config.Opacity);
                if (resizingColumn == i || DividerBounds(i).Contains(cursor))
                    batch.Draw(Game1.staminaRect, new Rectangle(divider.X, divider.Y, 2, rowHeight), UiStyle.Primary * config.Opacity);
            }
        }
        batch.Draw(Game1.staminaRect, new Rectangle(RowsBounds.X, RowsBounds.Y - 1, RowsBounds.Width, 1), UiStyle.Border * config.Opacity);

        for (int i = layoutFirstRow; i < layoutEndRow; i++)
        {
            Rectangle row = new(RowsBounds.X, RowsBounds.Y + (i - layoutFirstRow) * rowHeight, RowsBounds.Width, rowHeight);
            if (isSelected?.Invoke(rows[i]) == true)
                batch.Draw(Game1.staminaRect, row, new Color(54, 101, 129) * config.Opacity);
            else if (row.Contains(cursor))
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.55f));
            else if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));
            for (int column = 0; column < columns.Length; column++)
                columns[column].DrawCell(batch, rows[i], new Rectangle(headers[column].X, row.Y, headers[column].Width, row.Height), config);
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
            string summary = footerText ?? "";
            if (showRange && MaxFirstRow > 0)
                summary += (summary.Length > 0 ? "   |   " : "") + $"{layoutFirstRow + 1}-{layoutEndRow} of {rows.Count}";
            UiText.DrawCell(batch, summary, footerBounds, UiStyle.Muted, config);
        }
    }

    internal TRow? GetRow(Point cursor)
    {
        if (!RowsBounds.Contains(cursor))
            return null;
        // Hit testing follows the last layout, even if scrolling changed before
        // the next render. Partial rows and the empty area aren't clickable rows.
        int index = layoutFirstRow + (cursor.Y - RowsBounds.Y) / rowHeight;
        return index < layoutEndRow ? rows[index] : null;
    }

    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return false;
        for (int i = 0; i < headers.Length - 1; i++)
        {
            if (!DividerBounds(i).Contains(cursor))
                continue;
            Array.Copy(columnWidths, preferredWidths, columnWidths.Length);
            resizingColumn = i;
            resizeStartX = cursor.X;
            resizeLeftWidth = columnWidths[i];
            resizeRightWidth = columnWidths[i + 1];
            return true;
        }
        for (int i = 0; i < headers.Length; i++)
        {
            if (!headers[i].Contains(cursor))
                continue;
            SortDescending = SortColumn == i && !SortDescending;
            SortColumn = i;
            FirstRow = 0;
            return true;
        }
        if (!scrollThumb.IsEmpty && scrollTrack.Contains(cursor))
        {
            scrollDragOffset = scrollThumb.Contains(cursor) ? cursor.Y - scrollThumb.Y : scrollThumb.Height / 2;
            Drag(cursor);
            return true;
        }
        return false;
    }

    internal void Drag(Point cursor)
    {
        if (resizingColumn >= 0)
        {
            int delta = Math.Clamp(cursor.X - resizeStartX,
                GetMinimumWidth(resizingColumn, RowsBounds.Width) - resizeLeftWidth,
                resizeRightWidth - GetMinimumWidth(resizingColumn + 1, RowsBounds.Width));
            preferredWidths[resizingColumn] = resizeLeftWidth + delta;
            preferredWidths[resizingColumn + 1] = resizeRightWidth - delta;
        }
        else if (scrollDragOffset.HasValue && scrollTrack.Height > scrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - scrollDragOffset.Value - scrollTrack.Y) / (scrollTrack.Height - scrollThumb.Height);
            FirstRow = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxFirstRow);
        }
    }

    internal void EndDrag()
    {
        resizingColumn = -1;
        scrollDragOffset = null;
    }

    internal void ReceiveScroll(int delta)
    {
        if (delta != 0 && !RowsBounds.IsEmpty)
            FirstRow = (int)Math.Clamp((long)FirstRow - Math.Sign(delta) * Math.Max(1L, Math.Abs((long)delta) / 120), 0, MaxFirstRow);
    }

    private void ClearLayout()
    {
        Array.Clear(headers);
        RowsBounds = scrollTrack = scrollThumb = footerBounds = Rectangle.Empty;
        VisibleRows = layoutFirstRow = layoutEndRow = 0;
    }

    private Rectangle DividerBounds(int column)
        => headers[column].Width == 0 || headers[column + 1].Width == 0 ? Rectangle.Empty
            : new Rectangle(headers[column].Right - 3, headers[column].Y, 7, headers[column].Height);

    private int GetMinimumWidth(int column, int tableWidth)
        => Math.Min(columns[column].MinimumWidth, tableWidth / columns.Length);

    private void FitColumns(int width)
    {
        int minimumTotal = 0;
        int weightTotal = 0;
        for (int i = 0; i < columnWidths.Length; i++)
        {
            columnWidths[i] = GetMinimumWidth(i, width);
            minimumTotal += columnWidths[i];
            weightTotal += Math.Max(0, preferredWidths[i] - columnWidths[i]);
        }
        int used = 0;
        for (int i = 0; i < columnWidths.Length; i++)
        {
            int weight = Math.Max(0, preferredWidths[i] - columnWidths[i]);
            columnWidths[i] += weightTotal == 0 ? (width - minimumTotal) / columnWidths.Length
                : (int)((long)(width - minimumTotal) * weight / weightTotal);
            used += columnWidths[i];
        }
        columnWidths[0] += width - used;
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
