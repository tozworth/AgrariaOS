using AgrariaOS.Configuration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgrariaOS.UI;

// A column supplies its text and typed sort key; custom cells can draw icons or badges.
internal sealed class GridColumn<TRow> where TRow : class
{
    internal string Id { get; }
    internal string Heading { get; }
    internal int PreferredWidth { get; }
    internal int MinimumWidth { get; }
    internal bool RightAligned { get; }
    private readonly Color textColor;
    private readonly Func<TRow, string> getText;
    private readonly Action<SpriteBatch, TRow, Rectangle, ModConfig>? drawCell;
    private readonly Func<IEnumerable<TRow>, bool, IEnumerable<TRow>> orderRows;

    private GridColumn(string id, string heading, int preferredWidth, int minimumWidth, Func<TRow, string> getText,
        bool rightAligned, Color textColor, Action<SpriteBatch, TRow, Rectangle, ModConfig>? drawCell,
        Func<IEnumerable<TRow>, bool, IEnumerable<TRow>> orderRows)
    {
        if (minimumWidth < 0 || preferredWidth < minimumWidth)
            throw new ArgumentOutOfRangeException(nameof(preferredWidth), "Column widths must be nonnegative and meet the minimum.");
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A column needs a stable ID.", nameof(id));
        Id = id;
        Heading = heading;
        PreferredWidth = preferredWidth;
        MinimumWidth = minimumWidth;
        RightAligned = rightAligned;
        this.textColor = textColor;
        this.getText = getText;
        this.drawCell = drawCell;
        this.orderRows = orderRows;
    }

    internal static GridColumn<TRow> Create<TKey>(string heading, int preferredWidth, int minimumWidth,
        Func<TRow, string> getText, Func<TRow, TKey> getSortKey, bool rightAligned = false,
        Color? textColor = null, IComparer<TKey>? comparer = null,
        Action<SpriteBatch, TRow, Rectangle, ModConfig>? drawCell = null, string? id = null)
    {
        IEnumerable<TRow> OrderRows(IEnumerable<TRow> rows, bool descending)
        {
            // Evaluate each key once. Stable ties preserve source order, and null
            // keys (unavailable values) stay at the bottom in either direction.
            var keyedRows = rows.Select(row => (Row: row, Key: getSortKey(row)))
                .OrderBy(row => row.Key is null);
            return (descending ? keyedRows.ThenByDescending(row => row.Key, comparer)
                : keyedRows.ThenBy(row => row.Key, comparer)).Select(row => row.Row);
        }

        return new GridColumn<TRow>(id ?? heading, heading, preferredWidth, minimumWidth, getText, rightAligned,
            textColor ?? UiStyle.Primary, drawCell, OrderRows);
    }

    internal IEnumerable<TRow> Sort(IEnumerable<TRow> rows, bool descending) => orderRows(rows, descending);

    internal void DrawCell(SpriteBatch batch, TRow row, Rectangle bounds, ModConfig config)
    {
        if (drawCell != null)
            drawCell(batch, row, bounds, config);
        else
            UiText.DrawCell(batch, getText(row), bounds, textColor, config, RightAligned);
    }
}
