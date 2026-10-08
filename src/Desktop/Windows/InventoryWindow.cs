using AgrariaOS.Configuration;
using AgrariaOS.UI;
using AgrariaOS.Interactions;
using AgrariaOS.Integration;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace AgrariaOS.Desktop.Windows;

internal sealed partial class InventoryWindow : OsWindow
{
    internal const string WindowId = "Inventory";
    internal const string ChestWindowId = "ChestInventory";
    private const int Padding = GridView<Item>.Padding;
    private const int IconSize = 20;
    private readonly ModConfig config;
    private readonly LookupAnythingIntegration? lookupAnything;
    private readonly Func<ShopMenu?>? getTradeShop;
    private readonly GridView<Item> grid;
    private bool knowsPrices;
    private Chest? chest;
    private GameLocation? chestLocation;
    private Farmer? chestFarmer;
    private uint chestDay;
    private readonly InventorySelection selection = new();

    internal Chest? Chest => chest;
    internal string? TransferStatus { get; set; }
    internal Func<Item, bool>? IsCut { get; set; }
    internal bool ContainsDropPoint(Point cursor) => grid.RowsBounds.Contains(cursor);
    internal Item[] SelectedItems => selection.InSlotOrder((chest?.GetItemsForPlayer() ?? Game1.player.Items)
        .Where(item => item != null).ToArray());

    internal InventoryWindow(ModConfig config, Chest? chest = null,
        LookupAnythingIntegration? lookupAnything = null, Func<ShopMenu?>? getTradeShop = null)
    {
        this.config = config;
        this.lookupAnything = lookupAnything;
        this.getTradeShop = getTradeShop;
        grid = new GridView<Item>(
            GridColumn<Item>.Create("Name", 264, 44, item => item.DisplayName, item => item.DisplayName,
                comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell, id: "name"),
            GridColumn<Item>.Create("Qty", 52, 28, item => item.Stack.ToString(CultureInfo.InvariantCulture),
                item => item.Stack, rightAligned: true, id: "quantity"),
            GridColumn<Item>.Create("Category", 160, 36, item => item.getCategoryName(), item => item.getCategoryName(),
                textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase, id: "category"),
            GridColumn<Item>.Create("Value", 100, 36, item => GetValueText(item, knowsPrices),
                item => GetValue(item, knowsPrices), rightAligned: true, id: "value"));
        if (chest != null)
            ShowChest(chest);
    }

    internal void ShowChest(Chest target)
    {
        if (ReferenceEquals(chest, target) && IsValid)
            return;
        EndContentDrag();
        grid.Clear();
        selection.Clear();
        TransferStatus = null;
        chest = target;
        chestLocation = Game1.currentLocation;
        chestFarmer = Game1.player;
        chestDay = Game1.stats.DaysPlayed;
    }

    internal override string Id => chest == null ? WindowId : ChestWindowId;
    internal override string Title => chest == null ? "Inventory" : $"{chest.DisplayName} Inventory";
    internal override string TitleSubtext
        => $"{(chest?.GetItemsForPlayer() ?? Game1.player.Items).Count(item => item != null)}/{chest?.GetActualCapacity() ?? Game1.player.MaxItems}";
    internal override bool IsValid => chest == null || (Context.IsWorldReady
        && Game1.player == chestFarmer && Game1.currentLocation == chestLocation
        && Game1.stats.DaysPlayed == chestDay
        && chestLocation.Objects.TryGetValue(chest.TileLocation, out var placed)
        && ReferenceEquals(placed, chest));
    internal override int Icon => UiStyle.InventoryIcon;
    internal override Point DefaultSize => new(600, 420);

    internal override void DrawTitleIcon(SpriteBatch batch, Point position, ModConfig config)
    {
        if (chest == null)
        {
            base.DrawTitleIcon(batch, position, config);
            return;
        }
        DrawChestIcon(batch, chest, position, 18, config.Opacity);
    }

    protected override Point GetMinimumSize(ModConfig config)
        => new(320, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        // Rendering only reads the source; transfers run under the chest mutex.
        knowsPrices = Game1.player.stats.Get("Book_PriceCatalogue") != 0;
        grid.SetRows((chest?.GetItemsForPlayer() ?? Game1.player.Items).Where(item => item != null));
        selection.Refresh(grid.Rows);
        string? footer = TransferStatus ?? (selection.Count > 0 ? $"{selection.Count} selected" : null);
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        grid.Draw(batch, contentBounds, grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding),
            config, cursor, chest == null ? "Backpack empty." : "Chest empty.", selection.Contains,
            footer, isDimmed: IsCut);
    }

    internal Item? SelectRow(Point cursor, bool control, bool shift)
    {
        Item? item = grid.GetRow(cursor);
        if (item == null)
        {
            if (grid.RowsBounds.Contains(cursor) && !control && !shift)
            {
                selection.Clear();
                TransferStatus = null;
            }
            return null;
        }
        // Ignore stale rows whose stack was removed since the last render.
        var liveItems = (chest?.GetItemsForPlayer() ?? Game1.player.Items)
            .Where(current => current != null).ToArray();
        if (!liveItems.Any(current => ReferenceEquals(current, item)))
            return null;
        // Shift ranges follow the visible sort, excluding stacks removed since render.
        Item[] displayedItems = grid.Rows.Where(displayed => liveItems.Any(current => ReferenceEquals(current, displayed))).ToArray();
        selection.Click(item, displayedItems, control, shift);
        TransferStatus = null;
        return selection.Contains(item) ? item : null;
    }

    internal void CollapseSelection(Item item) => selection.SelectOnly(item);

    internal Item? GetRowAt(Point cursor) => grid.GetRow(cursor);

    internal Rectangle? GetRowBoundsAt(Point cursor) => grid.GetRowBounds(cursor);

    internal void SelectAll()
    {
        selection.SelectAll((chest?.GetItemsForPlayer() ?? Game1.player.Items)
            .Where(item => item != null).ToArray());
        TransferStatus = null;
    }

    internal void DrawItemDrag(SpriteBatch batch, Point cursor, Item[] draggedItems, bool validDrop, ModConfig config)
    {
        int count = draggedItems.Length;
        if (count == 0)
            return;
        string label = $"{count} stack{(count == 1 ? "" : "s")}";
        int width = (int)MathF.Ceiling(Game1.smallFont.MeasureString(label).X * config.FontScale) + IconSize + Padding * 4 + 1;
        Rectangle badge = new(cursor.X + 16, cursor.Y + 16, Math.Min(width, Game1.uiViewport.Width),
            grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) + Padding * 2);
        badge.X = Math.Clamp(badge.X, 0, Math.Max(0, Game1.uiViewport.Width - badge.Width));
        badge.Y = Math.Clamp(badge.Y, 0, Math.Max(0, Game1.uiViewport.Height - badge.Height));
        batch.Draw(Game1.staminaRect, badge, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, badge, config.Opacity);
        DrawItemIcon(batch, draggedItems[0], new Rectangle(badge.X + Padding,
            badge.Y + (badge.Height - IconSize) / 2, IconSize, IconSize), config.Opacity);
        Rectangle textBounds = new(badge.X + IconSize + Padding, badge.Y, badge.Width - IconSize - Padding, badge.Height);
        UiText.DrawCell(batch, label, textBounds,
            validDrop ? new Color(137, 221, 151) : UiStyle.Muted, config);
    }

    internal void DrawDropTarget(SpriteBatch batch, ModConfig config)
    {
        Rectangle rowsBounds = grid.RowsBounds;
        Color color = new Color(137, 221, 151) * config.Opacity;
        batch.Draw(Game1.staminaRect, new Rectangle(rowsBounds.X, rowsBounds.Y, rowsBounds.Width, 1), color);
        batch.Draw(Game1.staminaRect, new Rectangle(rowsBounds.X, rowsBounds.Bottom - 1, rowsBounds.Width, 1), color);
    }

    internal void DrawMergeTarget(SpriteBatch batch, Rectangle row, ModConfig config)
    {
        Color color = new Color(137, 221, 151) * config.Opacity;
        batch.Draw(Game1.staminaRect, new Rectangle(row.X, row.Y, row.Width, 1), color);
        batch.Draw(Game1.staminaRect, new Rectangle(row.X, row.Bottom - 1, row.Width, 1), color);
        batch.Draw(Game1.staminaRect, new Rectangle(row.X, row.Y, 1, row.Height), color);
        batch.Draw(Game1.staminaRect, new Rectangle(row.Right - 1, row.Y, 1, row.Height), color);
    }




    internal override void ReceiveContentClick(Point cursor, bool leftClick) => grid.ReceiveClick(cursor, leftClick);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);






}
