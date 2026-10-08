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
    private readonly List<int> visibleColumns;
    private Action<string[]>? columnVisibilityChanged;
    private Action<Dictionary<string, int>>? columnWidthsChanged;
    private bool widthsChangedDuringDrag;
    private readonly List<TRow> rows = new();
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private Rectangle footerBounds;
    private int rowHeight;
    private int layoutFirstRow;
    private int layoutEndRow;
    private int resizingColumn = -1;
    private int resizingRightColumn = -1;
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
        if (columns.Select(column => column.Id).Distinct(StringComparer.Ordinal).Count() != columns.Length)
            throw new ArgumentException("Column IDs must be unique within a grid.", nameof(columns));
        this.columns = columns.ToArray();
        preferredWidths = columns.Select(column => column.PreferredWidth).ToArray();
        columnWidths = new int[columns.Length];
        headers = new Rectangle[columns.Length];
        visibleColumns = Enumerable.Range(0, columns.Length).ToList();
    }

    internal void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
    {
        EndDrag();
        for (int i = 0; i < columns.Length; i++)
            preferredWidths[i] = widths.TryGetValue(columns[i].Id, out int width) && width >= 0
                ? width : columns[i].PreferredWidth;
        ClearLayout();
        columnWidthsChanged = onChanged;
    }

    // Missing/new columns are visible by default. Ignore obsolete saved IDs, and
    // recover a usable header if a hand-edited config hides every known column.
    internal void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
    {
        var hidden = hiddenColumns.ToHashSet(StringComparer.Ordinal);
        visibleColumns.Clear();
        for (int i = 0; i < columns.Length; i++)
        {
            if (!hidden.Contains(columns[i].Id))
                visibleColumns.Add(i);
        }
        if (visibleColumns.Count == 0)
            visibleColumns.Add(0);
        ResetHiddenSort();
        EndDrag();
        ClearLayout();
        columnVisibilityChanged = onChanged;
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
        bool showFooter = reserveFooter;
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
        foreach (int i in visibleColumns)
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

    internal void Draw(SpriteBatch batch, Rectangle bounds, int rowHeight, ModConfig config, Point cursor,
        string emptyText, Func<TRow, bool>? isSelected = null, string? footerText = null)
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

        for (int i = layoutFirstRow; i < layoutEndRow; i++)
        {
            Rectangle row = new(RowsBounds.X, RowsBounds.Y + (i - layoutFirstRow) * rowHeight, RowsBounds.Width, rowHeight);
            if (isSelected?.Invoke(rows[i]) == true)
                batch.Draw(Game1.staminaRect, row, new Color(54, 101, 129) * config.Opacity);
            else if (row.Contains(cursor))
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.55f));
            else if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));
            foreach (int column in visibleColumns)
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
            UiText.DrawCell(batch, footerText ?? "", footerBounds, UiStyle.Muted, config);
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

    internal Rectangle? GetRowBounds(Point cursor)
    {
        if (!RowsBounds.Contains(cursor))
            return null;
        int index = layoutFirstRow + (cursor.Y - RowsBounds.Y) / rowHeight;
        if (index >= layoutEndRow)
            return null;
        return new Rectangle(RowsBounds.X, RowsBounds.Y + (index - layoutFirstRow) * rowHeight,
            RowsBounds.Width, rowHeight);
    }

    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return false;
        for (int index = 0; index < visibleColumns.Count - 1; index++)
        {
            int i = visibleColumns[index];
            int right = visibleColumns[index + 1];
            if (!DividerBounds(i, right).Contains(cursor))
                continue;
            foreach (int column in visibleColumns)
                preferredWidths[column] = columnWidths[column];
            resizingColumn = i;
            resizingRightColumn = right;
            resizeStartX = cursor.X;
            resizeLeftWidth = columnWidths[i];
            resizeRightWidth = columnWidths[right];
            return true;
        }
        foreach (int i in visibleColumns)
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

    // All columns remain available here, including ones whose hit boxes are hidden.
    internal ContextMenuItem[]? GetHeaderContextMenuItems(Point cursor)
    {
        if (!visibleColumns.Any(column => headers[column].Contains(cursor)))
            return null;
        return columns.Select((column, index) => new ContextMenuItem(column.Heading,
            () => ToggleColumn(index), IsChecked: visibleColumns.Contains(index),
            IsEnabled: visibleColumns.Count > 1 || !visibleColumns.Contains(index))).ToArray();
    }

    private void ToggleColumn(int column)
    {
        if (visibleColumns.Contains(column))
        {
            // Keep a header available to reopen the menu and restore hidden columns.
            if (visibleColumns.Count == 1)
                return;
            visibleColumns.Remove(column);
        }
        else
        {
            int next = visibleColumns.FindIndex(index => index > column);
            visibleColumns.Insert(next < 0 ? visibleColumns.Count : next, column);
        }
        ResetHiddenSort();
        EndDrag();
        ClearLayout();
        columnVisibilityChanged?.Invoke(columns.Where((_, index) => !visibleColumns.Contains(index))
            .Select(column => column.Id).ToArray());
    }

    private void ResetHiddenSort()
    {
        if (SortColumn >= 0 && !visibleColumns.Contains(SortColumn))
        {
            SortColumn = -1;
            SortDescending = false;
            FirstRow = 0;
        }
    }

    internal void Drag(Point cursor)
    {
        if (resizingColumn >= 0)
        {
            int delta = Math.Clamp(cursor.X - resizeStartX,
                GetMinimumWidth(resizingColumn, RowsBounds.Width) - resizeLeftWidth,
                resizeRightWidth - GetMinimumWidth(resizingRightColumn, RowsBounds.Width));
            widthsChangedDuringDrag |= preferredWidths[resizingColumn] != resizeLeftWidth + delta;
            preferredWidths[resizingColumn] = resizeLeftWidth + delta;
            preferredWidths[resizingRightColumn] = resizeRightWidth - delta;
        }
        else if (scrollDragOffset.HasValue && scrollTrack.Height > scrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - scrollDragOffset.Value - scrollTrack.Y) / (scrollTrack.Height - scrollThumb.Height);
            FirstRow = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxFirstRow);
        }
    }

    internal void EndDrag()
    {
        bool saveWidths = widthsChangedDuringDrag;
        widthsChangedDuringDrag = false;
        resizingColumn = -1;
        resizingRightColumn = -1;
        scrollDragOffset = null;
        if (saveWidths)
            columnWidthsChanged?.Invoke(columns.Select((column, index) => (column.Id, Width: preferredWidths[index]))
                .ToDictionary(column => column.Id, column => column.Width, StringComparer.Ordinal));
    }

    internal void ReceiveScroll(int delta)
    {
        if (delta != 0 && !RowsBounds.IsEmpty)
            FirstRow = (int)Math.Clamp((long)FirstRow - Math.Sign(delta) * Math.Max(1L, Math.Abs((long)delta) / 120), 0, MaxFirstRow);
    }

    internal void ResetScroll() => FirstRow = 0;

    private void ClearLayout()
    {
        Array.Clear(headers);
        RowsBounds = scrollTrack = scrollThumb = footerBounds = Rectangle.Empty;
        VisibleRows = layoutFirstRow = layoutEndRow = 0;
    }

    private Rectangle DividerBounds(int column, int rightColumn)
        => headers[column].Width == 0 || headers[rightColumn].Width == 0 ? Rectangle.Empty
            : new Rectangle(headers[column].Right - 3, headers[column].Y, 7, headers[column].Height);

    private int GetMinimumWidth(int column, int tableWidth)
        => Math.Min(columns[column].MinimumWidth, tableWidth / visibleColumns.Count);

    private void FitColumns(int width)
    {
        int minimumTotal = 0;
        long weightTotal = 0;
        Array.Clear(columnWidths);
        foreach (int i in visibleColumns)
        {
            columnWidths[i] = GetMinimumWidth(i, width);
            minimumTotal += columnWidths[i];
            weightTotal += Math.Max(0, preferredWidths[i] - columnWidths[i]);
        }
        int used = 0;
        foreach (int i in visibleColumns)
        {
            int weight = Math.Max(0, preferredWidths[i] - columnWidths[i]);
            columnWidths[i] += weightTotal == 0 ? (width - minimumTotal) / visibleColumns.Count
                : (int)((long)(width - minimumTotal) * weight / weightTotal);
            used += columnWidths[i];
        }
        columnWidths[visibleColumns[0]] += width - used;
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
