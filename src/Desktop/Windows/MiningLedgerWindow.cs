using AgrariaOS.Configuration;
using AgrariaOS.Mining;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System.Globalization;

namespace AgrariaOS.Desktop.Windows;

internal sealed partial class MiningLedgerWindow : OsWindow
{
    internal const string WindowId = "MiningLedger";
    private const int Padding = GridView<MiningRow>.Padding;
    private const int IconSize = 20;
    private const int GraphMinHeight = 110;
    private static readonly int[] LabeledBuckets = { 0, 4, 8, 12, 16, 19 };

    private readonly MiningLedger ledger;
    private readonly GridView<MiningRow> grid = new(
        GridColumn<MiningRow>.Create("Name", 200, 44, row => row.Name, row => row.Name,
            comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell, id: "name"),
        GridColumn<MiningRow>.Create("Qty", 52, 28, row => row.Qty.ToString(CultureInfo.InvariantCulture),
            row => row.Qty, rightAligned: true, id: "quantity"),
        GridColumn<MiningRow>.Create("Value", 100, 36, row => GetValueText(row.Value),
            row => row.Value, rightAligned: true, id: "value"));

    internal MiningLedgerWindow(MiningLedger ledger) => this.ledger = ledger;

    internal override string Id => WindowId;
    internal override string Title => "Mining Ledger";
    internal override int Icon => 121;
    internal override Point DefaultSize => new(600, 420);

    protected override Point GetMinimumSize(ModConfig config)
    {
        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        return new(340, TitleHeight + 2 + GraphMinHeight + Padding * 2 + rowHeight * 4 + 1);
    }

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        if (!Context.IsWorldReady)
            return;
        MiningLedger.FarmerDay day = ledger.GetDay(Game1.player.UniqueMultiplayerID);
        bool knowsPrices = Game1.player.stats.Get("Book_PriceCatalogue") != 0;
        MiningRow[] rows = BuildRows(day, knowsPrices);
        int rowHeight = grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding);
        int gridMinHeight = Padding * 2 + rowHeight * 4 + 1;
        int graphHeight = Math.Clamp(contentBounds.Height * 48 / 100, GraphMinHeight,
            Math.Max(GraphMinHeight, contentBounds.Height - gridMinHeight));
        Rectangle graphBounds = new(contentBounds.X, contentBounds.Y, contentBounds.Width, graphHeight);
        Rectangle gridBounds = new(contentBounds.X, contentBounds.Y + graphHeight,
            contentBounds.Width, Math.Max(0, contentBounds.Height - graphHeight));
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        DrawGraph(batch, graphBounds, day, rows, cursor, config);
        grid.SetRows(rows);
        grid.Draw(batch, gridBounds, rowHeight, config, cursor, "No ore recorded today.",
            footerText: day.Total == 0 ? "No ore recorded today." : $"Total {day.Total} ore");
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick) => grid.ReceiveClick(cursor, leftClick);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);
    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor) => grid.GetHeaderContextMenuItems(cursor);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);













}
