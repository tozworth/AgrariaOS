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

internal sealed partial class TradeWindow : OsWindow
{
    internal const string WindowId = "Trade";
    private const int Padding = GridView<ISalable>.Padding;
    private const int IconSize = 20;
    private readonly ModConfig config;
    private readonly LookupAnythingIntegration? lookupAnything;
    private readonly Action<ISalable[]> requestBuy;
    private readonly GridView<ISalable> grid;
    private ShopMenu? shop;
    private uint shopDay;
    private readonly HashSet<ISalable> selected = new(ReferenceEqualityComparer.Instance);
    private ISalable? anchor;

    internal ShopMenu? Shop => shop;
    internal string? TransferStatus { get; set; }
    internal bool ContainsDropPoint(Point cursor) => grid.RowsBounds.Contains(cursor);

    internal TradeWindow(ModConfig config, ShopMenu shop,
        LookupAnythingIntegration? lookupAnything, Action<ISalable[]> requestBuy)
    {
        this.config = config;
        this.lookupAnything = lookupAnything;
        this.requestBuy = requestBuy;
        grid = new GridView<ISalable>(
            GridColumn<ISalable>.Create("Name", 264, 44, item => item.DisplayName, item => item.DisplayName,
                comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell, id: "name"),
            GridColumn<ISalable>.Create("Price", 110, 36, item => shop == null ? "" : TradeActions.PriceText(shop, item),
                item => shop == null ? null : TradeActions.PriceValue(shop, item),
                rightAligned: true, id: "price"),
            GridColumn<ISalable>.Create("Stock", 60, 28, item => shop == null ? "" : TradeActions.StockText(shop, item),
                item => shop == null ? 0 : TradeActions.StockSort(shop, item),
                rightAligned: true, id: "stock"),
            GridColumn<ISalable>.Create("Category", 150, 36, GetCategoryName, GetCategoryName,
                textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase, id: "category"));
        ShowShop(shop);
    }

    internal void ShowShop(ShopMenu target)
    {
        if (ReferenceEquals(shop, target) && IsValid)
            return;
        EndContentDrag();
        grid.Clear();
        selected.Clear();
        anchor = null;
        TransferStatus = null;
        shop = target;
        shopDay = Game1.stats.DaysPlayed;
    }

    internal override string Id => WindowId;
    internal override string Title => shop == null ? "Trade" : $"Trade ({shop.ShopId})";
    internal override string TitleSubtext
        => shop == null ? "No shop" : $"{shop.forSale.Count} for sale";
    internal override bool IsValid => shop != null && Context.IsWorldReady
        && Game1.stats.DaysPlayed == shopDay;
    internal override int Icon => UiStyle.InventoryIcon;
    internal override Point DefaultSize => new(600, 420);

    protected override Point GetMinimumSize(ModConfig config)
        => new(320, TitleHeight + 2 + Padding * 2 + grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        if (shop == null)
        {
            UiText.DrawCell(batch, "No shop.", contentBounds, UiStyle.Muted, config);
            return;
        }
        grid.SetRows(shop.forSale);
        RefreshSelection(shop.forSale);
        string? footer = TransferStatus ?? (selected.Count > 0 ? $"{selected.Count} selected" : null);
        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        grid.Draw(batch, contentBounds, grid.GetRowHeight(Game1.smallFont, config.FontScale, IconSize + Padding),
            config, cursor, "Sold out.", selected.Contains, footer);
    }











    internal override void ReceiveContentClick(Point cursor, bool leftClick) => grid.ReceiveClick(cursor, leftClick);
    internal override void ConfigureColumnVisibility(IEnumerable<string> hiddenColumns, Action<string[]> onChanged)
        => grid.ConfigureColumnVisibility(hiddenColumns, onChanged);

    internal override void ConfigureColumnWidths(IReadOnlyDictionary<string, int> widths, Action<Dictionary<string, int>> onChanged)
        => grid.ConfigureColumnWidths(widths, onChanged);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    private ISalable[] ContextTargets(ISalable item)
    {
        if (shop == null)
            return Array.Empty<ISalable>();
        RefreshSelection(shop.forSale);
        if (!selected.Contains(item))
        {
            selected.Clear();
            selected.Add(item);
            anchor = item;
        }
        return shop.forSale.Where(selected.Contains).ToArray();
    }

    private void RefreshSelection(IReadOnlyList<ISalable> items)
    {
        selected.RemoveWhere(item => !items.Any(current => ReferenceEquals(current, item)));
        if (anchor != null && !items.Any(item => ReferenceEquals(item, anchor)))
            anchor = null;
    }

    private void Click(ISalable item, IReadOnlyList<ISalable> items, bool control, bool shift)
    {
        RefreshSelection(items);
        int start = anchor == null ? -1 : IndexOf(items, anchor);
        if (shift && start >= 0)
        {
            if (!control)
                selected.Clear();
            int end = IndexOf(items, item);
            for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                selected.Add(items[i]);
            return;
        }
        anchor = item;
        if (control)
        {
            if (!selected.Remove(item))
                selected.Add(item);
        }
        else if (!selected.Contains(item))
        {
            selected.Clear();
            selected.Add(item);
        }
    }

    private static int IndexOf(IReadOnlyList<ISalable> items, ISalable target)
    {
        for (int i = 0; i < items.Count; i++)
            if (ReferenceEquals(items[i], target))
                return i;
        return -1;
    }

    private static string GetCategoryName(ISalable salable)
        => salable is Item item ? item.getCategoryName() : "";

    private static void DrawNameCell(SpriteBatch batch, ISalable salable, Rectangle bounds, ModConfig config)
    {
        if (salable is Item item)
            DrawItemIcon(batch, item, new Rectangle(bounds.X + Padding,
                bounds.Y + (bounds.Height - IconSize) / 2, IconSize, IconSize), config.Opacity);
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, salable.DisplayName, bounds, UiStyle.Primary, config);
    }

    private static void DrawItemIcon(SpriteBatch batch, Item item, Rectangle bounds, float opacity)
    {
        var data = ItemRegistry.GetDataOrErrorItem(item is MeleeWeapon weapon ? weapon.GetDrawnItemId() : item.QualifiedItemId);
        Rectangle source = data.GetSourceRect(0, GetIconSpriteIndex(item));
        float scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
        Color tint = item is ColoredObject colored && colored.ColorSameIndexAsParentSheetIndex ? colored.color.Value : Color.White;
        batch.Draw(data.GetTexture(), destination, source, tint * opacity);
        if (item is ColoredObject overlay && !overlay.ColorSameIndexAsParentSheetIndex)
            batch.Draw(data.GetTexture(), destination, data.GetSourceRect(1, item.ParentSheetIndex), overlay.color.Value * opacity);

        if (item.Quality > 0)
        {
            Rectangle star = item.Quality < 4 ? new Rectangle(338 + (item.Quality - 1) * 8, 400, 8, 8) : new Rectangle(346, 392, 8, 8);
            batch.Draw(Game1.mouseCursors, new Rectangle(bounds.Right - 8, bounds.Bottom - 8, 8, 8), star, Color.White * opacity);
        }
    }

    private static int? GetIconSpriteIndex(Item item)
        => item is StardewValley.Object ? item.ParentSheetIndex : null;
}
