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

internal sealed partial class BundlesWindow : OsWindow
{
    internal const string WindowId = "Bundles";
    internal const int BundleIcon = 84;
    private const int Padding = GridView<BundleRow>.Padding;
    private const int IconSize = 20;
    private const int VaultArea = 4;

    private readonly TreeView tree = new();
    private readonly LookupAnythingIntegration? lookupAnything;
    private readonly GridView<BundleRow> grid = new(
        GridColumn<BundleRow>.Create("Bundle", 140, 36, row => row.BundleName, row => row.BundleName,
            comparer: StringComparer.CurrentCultureIgnoreCase, textColor: UiStyle.Muted, id: "bundle"),
        GridColumn<BundleRow>.Create("Item", 220, 44, row => row.ItemName, row => row.ItemName,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawItemCell, id: "item"),
        GridColumn<BundleRow>.Create("Qty", 64, 28, row => row.QtyText, row => row.Qty,
            rightAligned: true, id: "quantity"),
        GridColumn<BundleRow>.Create("Quality", 80, 36, row => row.QualityText, row => row.Quality, id: "quality"),
        GridColumn<BundleRow>.Create("Status", 70, 32, row => row.StatusText, row => row.Completed ? 1 : 0,
            drawCell: DrawStatusCell, id: "status"));

    private Rectangle treeBounds = Rectangle.Empty;
    private Rectangle gridBounds = Rectangle.Empty;
    private Snapshot cachedSnapshot = new();
    private double lastSnapshotMs = double.NegativeInfinity;
    private const double SnapshotIntervalMs = 500;
    private static readonly Dictionary<int, string> RepresentativeCache = new();

    internal BundlesWindow(LookupAnythingIntegration? lookupAnything = null)
    {
        this.lookupAnything = lookupAnything;
    }

    internal override string Id => WindowId;
    internal override string Title => "Bundles";
    internal override string TitleSubtext
    {
        get
        {
            try
            {
                Snapshot snapshot = GetSnapshot();
                return snapshot.TotalBundles == 0 ? "" : $"{snapshot.CompletedBundles}/{snapshot.TotalBundles} bundles";
            }
            catch
            {
                return "";
            }
        }
    }
    internal override int Icon => BundleIcon;
    internal override Point DefaultSize => new(700, 440);

    protected override Point GetMinimumSize(ModConfig config)
        => new(480, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    // Mirrors InventoryPage.ShouldShowJunimoNoteIcon: the rail only offers
    // bundles while the vanilla inventory would show its junimo button.
    internal static bool IsAvailable()
    {
        try
        {
            if (!Context.IsWorldReady)
                return false;
            return InventoryPage.ShouldShowJunimoNoteIcon();
        }
        catch
        {
            return false;
        }
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        Snapshot snapshot;
        try { snapshot = GetSnapshot(); }
        catch { snapshot = new Snapshot(); }
        tree.SetRoots(snapshot.Roots);
        tree.EnsureSelection();

        BundleRow[] rows;
        string footer;
        try
        {
            (rows, footer) = BuildRows(snapshot, tree.SelectedNode);
        }
        catch
        {
            rows = Array.Empty<BundleRow>();
            footer = "";
        }
        grid.SetRows(rows);

        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int treeWidth = Math.Clamp(contentBounds.Width * 35 / 100, 180, 280);
        if (contentBounds.Width < 320)
            treeWidth = Math.Max(0, contentBounds.Width / 2 - 8);
        treeBounds = new Rectangle(contentBounds.X, contentBounds.Y, Math.Max(0, treeWidth), contentBounds.Height);
        int dividerX = contentBounds.X + treeWidth + 3;
        gridBounds = new Rectangle(Math.Min(contentBounds.Right, dividerX + 4), contentBounds.Y,
            Math.Max(0, contentBounds.Right - dividerX - 4), contentBounds.Height);

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        tree.Draw(batch, treeBounds, rowHeight, config, cursor, Context.IsWorldReady ? "No bundles." : "No save loaded.");
        if (gridBounds.Width > 0 && gridBounds.Height > 0)
        {
            batch.Draw(Game1.staminaRect, new Rectangle(dividerX, contentBounds.Y, 1, contentBounds.Height), UiStyle.Border * config.Opacity);
            string empty = tree.SelectedNode == null ? "Select a bundle." : "No ingredients.";
            grid.Draw(batch, gridBounds, rowHeight, config, cursor, empty, footerText: footer);
        }
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (treeBounds.Contains(cursor) && tree.ReceiveClick(cursor, leftClick))
            return;
        if (gridBounds.Contains(cursor))
            grid.ReceiveClick(cursor, leftClick);
    }

    internal override void DragContent(Point cursor)
    {
        tree.Drag(cursor);
        grid.Drag(cursor);
    }

    internal override void EndContentDrag()
    {
        tree.EndDrag();
        grid.EndDrag();
    }

    internal override void ReceiveScroll(int delta)
    {
        Point cursor;
        try { cursor = new Point(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true)); }
        catch { cursor = Point.Zero; }
        if (treeBounds.Contains(cursor))
            tree.ReceiveScroll(delta);
        else
            grid.ReceiveScroll(delta);
    }

    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        if (gridBounds.Contains(cursor))
        {
            if (grid.GetHeaderContextMenuItems(cursor) is { } columns)
                return columns;
            BundleRow? row;
            try { row = grid.GetRow(cursor); }
            catch { return null; }
            if (row == null || string.IsNullOrEmpty(row.IconId) || !IsRowCurrent(row))
                return null;
            // No domain actions; the manager appends Show Info via GetContextMenuInfo.
            if (lookupAnything?.Available != true)
                return null;
            return Array.Empty<ContextMenuItem>();
        }
        return null;
    }

    internal override ContextMenuInfo? GetContextMenuInfo(Point cursor)
    {
        if (lookupAnything?.Available != true || !gridBounds.Contains(cursor))
            return null;
        BundleRow? row;
        try { row = grid.GetRow(cursor); }
        catch { return null; }
        if (row == null || string.IsNullOrEmpty(row.IconId) || !IsRowCurrent(row))
            return null;
        string iconId = row.IconId;
        int quality = row.Quality;
        Func<bool> isCurrent = GetContextItemGuard(row);
        return new ContextMenuInfo(() =>
        {
            try
            {
                Item item = ItemRegistry.Create(iconId, 1, quality);
                return lookupAnything.GetInventorySubject(item);
            }
            catch { return null; }
        }, () => isCurrent() && Context.IsPlayerFree);
    }

    private Func<bool> GetContextItemGuard(BundleRow row)
    {
        Farmer farmer = Game1.player;
        uint day = Game1.stats.DaysPlayed;
        return () => Context.IsWorldReady && Game1.player == farmer
            && Game1.stats.DaysPlayed == day && IsValid && !IsMinimized;
    }

    private bool IsRowCurrent(BundleRow row)
    {
        try
        {
            if (!Context.IsWorldReady || !IsValid)
                return false;
            Snapshot snapshot = GetSnapshot();
            return snapshot.Bundles.Any(bundle => bundle.BundleIndex == row.BundleIndex
                && bundle.Rows.Any(candidate => candidate.IconId == row.IconId
                    && candidate.Qty == row.Qty && candidate.Quality == row.Quality
                    && candidate.ItemName == row.ItemName));
        }
        catch { return false; }
    }













}
