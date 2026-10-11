using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

partial class GridView<TRow> where TRow : class
{
    internal bool ReceiveClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return false;
        List<int> effective = GetEffectiveColumns();
        for (int index = 0; index < effective.Count - 1; index++)
        {
            int i = effective[index];
            int right = effective[index + 1];
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
        foreach (int i in effective)
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
        if (SortColumn >= 0 && (!visibleColumns.Contains(SortColumn) || autoHidden.Contains(SortColumn)))
        {
            SortColumn = -1;
            SortDescending = false;
            FirstRow = 0;
        }
    }

    internal ContextMenuItem[]? GetHeaderContextMenuItems(Point cursor)
    {
        if (!visibleColumns.Any(column => headers[column].Contains(cursor)))
            return null;
        return columns.Select((column, index) => new ContextMenuItem(column.Heading,
            () => ToggleColumn(index), IsChecked: visibleColumns.Contains(index),
            IsEnabled: visibleColumns.Count > 1 || !visibleColumns.Contains(index))).ToArray();
    }

}
