using AgrariaOS.Configuration;
using AgrariaOS.Integration;
using AgrariaOS.Interactions;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace AgrariaOS.Desktop.Windows;

partial class TradeWindow : OsWindow
{
    internal ISalable? SelectRow(Point cursor, bool control, bool shift)
    {
        if (shop == null)
            return null;
        ISalable? item = grid.GetRow(cursor);
        if (item == null)
        {
            if (grid.RowsBounds.Contains(cursor) && !control && !shift)
            {
                selected.Clear();
                anchor = null;
                TransferStatus = null;
            }
            return null;
        }
        if (!shop.forSale.Any(current => ReferenceEquals(current, item)))
            return null;
        ISalable[] displayed = grid.Rows.Where(displayed => shop.forSale.Any(current => ReferenceEquals(current, displayed))).ToArray();
        Click(item, displayed, control, shift);
        TransferStatus = null;
        return selected.Contains(item) ? item : null;
    }

    internal void CollapseSelection(ISalable item)
    {
        selected.Clear();
        selected.Add(item);
        anchor = item;
    }

    internal ISalable? GetRowAt(Point cursor) => grid.GetRow(cursor);

    internal Rectangle? GetRowBoundsAt(Point cursor) => grid.GetRowBounds(cursor);

    internal void SelectAll()
    {
        if (shop == null)
            return;
        selected.Clear();
        foreach (ISalable item in shop.forSale)
            selected.Add(item);
        anchor = shop.forSale.Count > 0 ? shop.forSale[0] : null;
        TransferStatus = null;
    }

    internal ISalable[] SelectedItems
    {
        get
        {
            if (shop == null)
                return Array.Empty<ISalable>();
            return shop.forSale.Where(selected.Contains).ToArray();
        }
    }

}
