using AgrariaOS.Configuration;
using AgrariaOS.UI;
using AgrariaOS.Interactions;
using AgrariaOS.Integration;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;

namespace AgrariaOS.Desktop.Windows;

internal sealed class InventoryWindow : OsWindow
{
    internal const string WindowId = "Inventory";
    internal const string ChestWindowId = "ChestInventory";
    private const int Padding = GridView<Item>.Padding;
    private const int IconSize = 20;
    private readonly ModConfig config;
    private readonly LookupAnythingIntegration? lookupAnything;
    private readonly GridView<Item> grid;
    private bool knowsPrices;
    private Chest? chest;
    private GameLocation? chestLocation;
    private Farmer? chestFarmer;
    private uint chestDay;
    private readonly InventorySelection selection = new();

    internal Chest? Chest => chest;
    internal string? TransferStatus { get; set; }
    internal bool ContainsDropPoint(Point cursor) => grid.RowsBounds.Contains(cursor);
    internal Item[] SelectedItems => selection.InSlotOrder((chest?.GetItemsForPlayer() ?? Game1.player.Items)
        .Where(item => item != null).ToArray());

    internal InventoryWindow(ModConfig config, Chest? chest = null,
        LookupAnythingIntegration? lookupAnything = null)
    {
        this.config = config;
        this.lookupAnything = lookupAnything;
        grid = new GridView<Item>(
            GridColumn<Item>.Create("Name", 264, 44, item => item.DisplayName, item => item.DisplayName,
                comparer: StringComparer.CurrentCultureIgnoreCase, drawCell: DrawNameCell),
            GridColumn<Item>.Create("Qty", 52, 28, item => item.Stack.ToString(CultureInfo.InvariantCulture),
                item => item.Stack, rightAligned: true),
            GridColumn<Item>.Create("Category", 160, 36, item => item.getCategoryName(), item => item.getCategoryName(),
                textColor: UiStyle.Muted, comparer: StringComparer.CurrentCultureIgnoreCase),
            GridColumn<Item>.Create("Value", 100, 36, item => GetValueText(item, knowsPrices),
                item => GetValue(item, knowsPrices), rightAligned: true));
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
            footer, showRange: TransferStatus == null);
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

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
    {
        Item? item = grid.GetRow(cursor);
        if (item == null)
            return null;
        Farmer farmer = Game1.player;
        Func<bool> IsCurrent = GetContextItemGuard(item);
        if (!IsCurrent())
            return null;
        var entries = new List<ContextMenuItem>();
        if (chest != null)
            return lookupAnything?.Available == true ? Array.Empty<ContextMenuItem>() : null;
        Item[] targets = selection.ContextTargets(item, farmer.Items.Where(current => current != null).ToArray());
        entries.AddRange(new ContextMenuItem[]
        {
            new("Split", () =>
            {
                if (targets.Length != 1 || !IsCurrent() || !Context.IsPlayerFree)
                    return;
                if (item.maximumStackSize() <= 1 || item.Stack <= 1)
                {
                    TransferStatus = "This item cannot be split.";
                    return;
                }
                Game1.activeClickableMenu = new NumberEntryMenu(config, $"Split {item.DisplayName}",
                    () => $"Quantity for the new stack (1-{item.Stack - 1})", amount =>
                    {
                        string? error = InventoryStackActions.Split(farmer.Items, farmer.MaxItems, item, amount);
                        if (error == null)
                            TransferStatus = null;
                        return error;
                    }, IsCurrent);
            }, IsEnabled: targets.Length == 1),
            new("Jettison", () =>
            {
                if (!IsCurrent() || !Context.IsPlayerFree)
                    return;
                Item[] liveTargets = targets.Where(target => InventoryTransferBatch.IndexOf(farmer.Items, target) >= 0).ToArray();
                if (liveTargets.Any(target => !target.canBeTrashed()))
                {
                    TransferStatus = "The selection contains an item that cannot be trashed.";
                    return;
                }
                // Match vanilla's canBeTrashed guard and trash helper, including
                // upgraded-can reclamation and special-item bookkeeping.
                foreach (Item target in liveTargets)
                {
                    farmer.removeItemFromInventory(target);
                    Utility.trashItem(target);
                }
                selection.Refresh(farmer.Items.Where(current => current != null).ToArray());
                TransferStatus = null;
            }, IsEnabled: targets.All(target => target.canBeTrashed()))
        });
        return entries.ToArray();
    }

    internal override ContextMenuInfo? GetContextMenuInfo(Point cursor)
    {
        if (lookupAnything?.Available != true || grid.GetRow(cursor) is not { } item)
            return null;
        Func<bool> isCurrent = GetContextItemGuard(item);
        return new ContextMenuInfo(() => lookupAnything.GetInventorySubject(item),
            () => isCurrent() && Context.IsPlayerFree);
    }

    private Func<bool> GetContextItemGuard(Item item)
    {
        Farmer farmer = Game1.player;
        GameLocation location = Game1.currentLocation;
        uint day = Game1.stats.DaysPlayed;
        return () => Context.IsWorldReady && Game1.player == farmer
            && Game1.currentLocation == location && Game1.stats.DaysPlayed == day
            && IsValid && !IsMinimized
            && (chest?.GetItemsForPlayer() ?? farmer.Items).Any(current => ReferenceEquals(current, item));
    }

    internal override void ReceiveContentClick(Point cursor, bool leftClick) => grid.ReceiveClick(cursor, leftClick);
    internal override void DragContent(Point cursor) => grid.Drag(cursor);
    internal override void EndContentDrag() => grid.EndDrag();
    internal override void ReceiveScroll(int delta) => grid.ReceiveScroll(delta);

    private static void DrawNameCell(SpriteBatch batch, Item item, Rectangle bounds, ModConfig config)
    {
        DrawItemIcon(batch, item, new Rectangle(bounds.X + Padding,
            bounds.Y + (bounds.Height - IconSize) / 2, IconSize, IconSize), config.Opacity);
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, item.DisplayName, bounds, UiStyle.Primary, config);
    }

    private static long? GetValue(Item item, bool knowsPrices)
    {
        // Stardew 1.6.15 IClickableMenu.drawHoverText's Price Catalogue branch.
        if (!knowsPrices || item is Furniture or Clothing or Wallpaper || !item.CanBeLostOnDeath()
            || (item is StardewValley.Object obj && obj.bigCraftable.Value))
            return null;

        int unitPrice = item.sellToStorePrice(-1L);
        long value = (long)unitPrice * item.Stack;
        return unitPrice > 0 && value > 0 ? value : null;
    }

    private static string GetValueText(Item item, bool knowsPrices)
        => GetValue(item, knowsPrices) is { } value ? value.ToString("N0", CultureInfo.InvariantCulture) + " g" : "";

    private static void DrawItemIcon(SpriteBatch batch, Item item, Rectangle bounds, float opacity)
    {
        // Registry sprites fit inside the cell without vanilla's 64px recipe/bar overlays.
        // Keep normal 16px item sprites at their native size and fit larger item types.
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
        // Object menu sprites support per-instance indices. Tools use the registry's
        // default sprite; their legacy ParentSheetIndex addresses a different layout.
        => item is StardewValley.Object ? item.ParentSheetIndex : null;
}
