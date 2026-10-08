using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace AgrariaOS.UI;

// Shared table behavior. Windows own row identity, selection, and domain actions;
// this view only orders a snapshot and never rearranges its source collection.
internal sealed partial class GridView<TRow> where TRow : class
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







    // All columns remain available here, including ones whose hit boxes are hidden.











}
