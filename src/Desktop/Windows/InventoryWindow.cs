using AgrariaOS.Configuration;
using AgrariaOS.UI;
using AgrariaOS.Interactions;
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
    private const int Padding = 4;
    private const int IconSize = 20;
    private const int ScrollbarWidth = 10;
    private static readonly string[] Headings = { "Name", "Qty", "Category", "Value" };
    private static readonly int[] MinimumWidths = { 44, 28, 36, 36 };
    private static readonly ContextMenuItem[] ItemMenu = { new("Split"), new("Jettison") };
    private readonly int[] preferredWidths = { 264, 52, 160, 100 };
    private readonly int[] columnWidths = new int[4];
    private readonly Rectangle[] headers = new Rectangle[4];
    private readonly List<Item> items = new();
    private readonly List<Rectangle> itemRowBounds = new();
    private Rectangle rowsBounds;
    private Rectangle scrollTrack;
    private Rectangle scrollThumb;
    private int visibleRows;
    private int firstRow;
    private int resizingColumn = -1;
    private int resizeStartX;
    private int resizeLeftWidth;
    private int resizeRightWidth;
    private int? scrollDragOffset;
    private SpriteFont? measuredFont;
    private int glyphHeight;
    private Chest? chest;
    private GameLocation? chestLocation;
    private Farmer? chestFarmer;
    private uint chestDay;
    private readonly InventorySelection selection = new();

    internal Chest? Chest => chest;
    internal string? TransferStatus { get; set; }
    internal bool ContainsDropPoint(Point cursor) => rowsBounds.Contains(cursor);
    internal Item[] SelectedItems => selection.InSlotOrder(items);

    internal InventoryWindow(Chest? chest = null)
    {
        if (chest != null)
            ShowChest(chest);
    }

    internal void ShowChest(Chest target)
    {
        if (ReferenceEquals(chest, target) && IsValid)
            return;
        EndContentDrag();
        firstRow = 0;
        items.Clear();
        itemRowBounds.Clear();
        selection.Clear();
        TransferStatus = null;
        chest = target;
        chestLocation = Game1.currentLocation;
        chestFarmer = Game1.player;
        chestDay = Game1.stats.DaysPlayed;
    }

    internal override string Id => chest == null ? WindowId : ChestWindowId;
    internal override string Title => chest == null ? "Inventory" : $"{chest.DisplayName} Inventory";
    internal override bool IsValid => chest == null || (Context.IsWorldReady
        && Game1.player == chestFarmer && Game1.currentLocation == chestLocation
        && Game1.stats.DaysPlayed == chestDay
        && chestLocation.Objects.TryGetValue(chest.TileLocation, out var placed)
        && ReferenceEquals(placed, chest));
    internal override int Icon => UiStyle.InventoryIcon;
    internal override Point DefaultSize => new(600, 420);
    private int MaxFirstRow => Math.Max(0, items.Count - visibleRows);

    protected override Point GetMinimumSize(ModConfig config)
        => new(320, TitleHeight + 2 + Padding * 2 + GetRowHeight(Game1.smallFont, config.FontScale) * 3 + 1);

    protected override void DrawContent(SpriteBatch batch, Rectangle contentBounds, ModConfig config)
    {
        // Rendering only reads the source; transfers run under the chest mutex.
        items.Clear();
        itemRowBounds.Clear();
        foreach (Item? item in chest?.GetItemsForPlayer() ?? Game1.player.Items)
        {
            if (item != null)
                items.Add(item);
        }
        selection.Refresh(items);

        int rowHeight = GetRowHeight(Game1.smallFont, config.FontScale);
        if (contentBounds.Height < rowHeight * 3 + Padding * 2 + 1)
        {
            Array.Clear(headers);
            rowsBounds = scrollTrack = scrollThumb = Rectangle.Empty;
            visibleRows = 0;
            EndContentDrag();
            DrawCellText(batch, "Not enough space.", contentBounds, UiStyle.Muted, config);
            return;
        }
        int tableWidth = Math.Max(0, contentBounds.Width - Padding * 2 - ScrollbarWidth);
        FitColumns(tableWidth);
        int x = contentBounds.X + Padding;
        for (int i = 0; i < headers.Length; i++)
        {
            headers[i] = new Rectangle(x, contentBounds.Y + Padding, columnWidths[i], rowHeight);
            x += columnWidths[i];
        }

        int availableHeight = Math.Max(0, contentBounds.Height - Padding * 2 - rowHeight * 2 - 1);
        visibleRows = availableHeight / rowHeight;
        firstRow = Math.Clamp(firstRow, 0, MaxFirstRow);
        rowsBounds = new Rectangle(headers[0].X, headers[0].Bottom + 1, tableWidth, availableHeight);
        scrollTrack = new Rectangle(rowsBounds.Right + 2, rowsBounds.Y, ScrollbarWidth - 4, rowsBounds.Height);
        scrollThumb = Rectangle.Empty;
        if (MaxFirstRow > 0 && rowsBounds.Height > 0)
        {
            int thumbHeight = Math.Min(scrollTrack.Height, Math.Max(16, scrollTrack.Height * visibleRows / items.Count));
            int thumbY = scrollTrack.Y + (int)Math.Round((scrollTrack.Height - thumbHeight) * (double)firstRow / MaxFirstRow);
            scrollThumb = new Rectangle(scrollTrack.X, thumbY, scrollTrack.Width, thumbHeight);
        }

        Point cursor = new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));
        foreach (Rectangle header in headers)
            batch.Draw(Game1.staminaRect, header, UiStyle.Border * (config.Opacity * 0.45f));
        for (int i = 0; i < headers.Length; i++)
        {
            DrawCellText(batch, Headings[i], headers[i], UiStyle.Primary, config, rightAligned: i is 1 or 3);
            if (i < headers.Length - 1)
            {
                Rectangle divider = new(headers[i].Right - 1, headers[i].Y, 1, rowHeight + 1 + rowsBounds.Height);
                batch.Draw(Game1.staminaRect, divider, UiStyle.Border * config.Opacity);
                if (resizingColumn == i || DividerBounds(i).Contains(cursor))
                    batch.Draw(Game1.staminaRect, new Rectangle(divider.X, divider.Y, 2, rowHeight), UiStyle.Primary * config.Opacity);
            }
        }
        batch.Draw(Game1.staminaRect, new Rectangle(rowsBounds.X, rowsBounds.Y - 1, tableWidth, 1), UiStyle.Border * config.Opacity);

        bool knowsPrices = Game1.player.stats.Get("Book_PriceCatalogue") != 0;
        int endRow = Math.Min(items.Count, firstRow + visibleRows);
        for (int i = firstRow; i < endRow; i++)
        {
            Item item = items[i];
            Rectangle row = new(rowsBounds.X, rowsBounds.Y + (i - firstRow) * rowHeight, tableWidth, rowHeight);
            itemRowBounds.Add(row);
            if (selection.Contains(item))
                batch.Draw(Game1.staminaRect, row, new Color(54, 101, 129) * config.Opacity);
            else if (row.Contains(cursor))
                batch.Draw(Game1.staminaRect, row, UiStyle.Border * (config.Opacity * 0.55f));
            else if ((i & 1) != 0)
                batch.Draw(Game1.staminaRect, row, Color.White * (config.Opacity * 0.025f));

            Rectangle name = new(headers[0].X, row.Y, headers[0].Width, row.Height);
            DrawItemIcon(batch, item, new Rectangle(name.X + Padding, row.Y + (row.Height - IconSize) / 2, IconSize, IconSize), config.Opacity);
            name.X += IconSize + Padding;
            name.Width -= IconSize + Padding;
            DrawCellText(batch, item.DisplayName, name, UiStyle.Primary, config);
            DrawCellText(batch, item.Stack.ToString(CultureInfo.InvariantCulture), CellBounds(1, row), UiStyle.Primary, config, rightAligned: true);
            DrawCellText(batch, item.getCategoryName(), CellBounds(2, row), UiStyle.Muted, config);
            DrawCellText(batch, GetValueText(item, knowsPrices), CellBounds(3, row), UiStyle.Primary, config, rightAligned: true);
        }

        if (items.Count == 0)
            DrawCenteredText(batch, chest == null ? "Backpack empty." : "Chest empty.", rowsBounds, UiStyle.Muted, config);
        if (!scrollThumb.IsEmpty)
        {
            batch.Draw(Game1.staminaRect, scrollTrack, UiStyle.Border * (config.Opacity * 0.45f));
            batch.Draw(Game1.staminaRect, scrollThumb, UiStyle.Muted * config.Opacity);
        }

        Rectangle footer = new(rowsBounds.X, contentBounds.Bottom - Padding - rowHeight, tableWidth, rowHeight);
        string summary = $"{items.Count} / {chest?.GetActualCapacity() ?? Game1.player.MaxItems} slots";
        if (selection.Count > 0)
            summary += $"   |   {selection.Count} selected";
        if (MaxFirstRow > 0 && visibleRows > 0)
            summary += $"   |   {firstRow + 1}-{endRow} of {items.Count}";
        if (TransferStatus != null)
            summary = TransferStatus;
        DrawCellText(batch, summary, footer, UiStyle.Muted, config);
    }

    internal Item? SelectRow(Point cursor, bool control, bool shift)
    {
        int row = itemRowBounds.FindIndex(bounds => bounds.Contains(cursor));
        if (row < 0 || firstRow + row >= items.Count)
        {
            if (rowsBounds.Contains(cursor) && !control && !shift)
            {
                selection.Clear();
                TransferStatus = null;
            }
            return null;
        }
        Item item = items[firstRow + row];
        // Ignore stale rows whose stack was removed since the last render.
        var liveItems = (chest?.GetItemsForPlayer() ?? Game1.player.Items)
            .Where(current => current != null).ToArray();
        if (!liveItems.Any(current => ReferenceEquals(current, item)))
            return null;
        selection.Click(item, liveItems, control, shift);
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
            GetRowHeight(Game1.smallFont, config.FontScale) + Padding * 2);
        badge.X = Math.Clamp(badge.X, 0, Math.Max(0, Game1.uiViewport.Width - badge.Width));
        badge.Y = Math.Clamp(badge.Y, 0, Math.Max(0, Game1.uiViewport.Height - badge.Height));
        batch.Draw(Game1.staminaRect, badge, UiStyle.Background * config.Opacity);
        UiStyle.DrawBorder(batch, badge, config.Opacity);
        DrawItemIcon(batch, draggedItems[0], new Rectangle(badge.X + Padding,
            badge.Y + (badge.Height - IconSize) / 2, IconSize, IconSize), config.Opacity);
        Rectangle textBounds = new(badge.X + IconSize + Padding, badge.Y, badge.Width - IconSize - Padding, badge.Height);
        DrawCellText(batch, label, textBounds,
            validDrop ? new Color(137, 221, 151) : UiStyle.Muted, config);
    }

    internal void DrawDropTarget(SpriteBatch batch, ModConfig config)
    {
        Color color = new Color(137, 221, 151) * config.Opacity;
        batch.Draw(Game1.staminaRect, new Rectangle(rowsBounds.X, rowsBounds.Y, rowsBounds.Width, 1), color);
        batch.Draw(Game1.staminaRect, new Rectangle(rowsBounds.X, rowsBounds.Bottom - 1, rowsBounds.Width, 1), color);
    }

    internal override ContextMenuItem[]? GetContextMenuItems(Point cursor)
        => chest == null && itemRowBounds.Any(row => row.Contains(cursor)) ? ItemMenu : null;

    internal override void ReceiveContentClick(Point cursor, bool leftClick)
    {
        if (!leftClick)
            return;

        for (int i = 0; i < headers.Length - 1; i++)
        {
            if (!DividerBounds(i).Contains(cursor))
                continue;
            Array.Copy(columnWidths, preferredWidths, columnWidths.Length);
            resizingColumn = i;
            resizeStartX = cursor.X;
            resizeLeftWidth = columnWidths[i];
            resizeRightWidth = columnWidths[i + 1];
            return;
        }

        if (!scrollThumb.IsEmpty && scrollTrack.Contains(cursor))
        {
            scrollDragOffset = scrollThumb.Contains(cursor) ? cursor.Y - scrollThumb.Y : scrollThumb.Height / 2;
            DragContent(cursor);
        }
    }

    internal override void DragContent(Point cursor)
    {
        if (resizingColumn >= 0)
        {
            // A divider shares width between its neighbors. Column order and table width stay fixed.
            int delta = Math.Clamp(cursor.X - resizeStartX,
                GetMinimumWidth(resizingColumn, rowsBounds.Width) - resizeLeftWidth,
                resizeRightWidth - GetMinimumWidth(resizingColumn + 1, rowsBounds.Width));
            preferredWidths[resizingColumn] = resizeLeftWidth + delta;
            preferredWidths[resizingColumn + 1] = resizeRightWidth - delta;
        }
        else if (scrollDragOffset.HasValue && scrollTrack.Height > scrollThumb.Height)
        {
            double fraction = (double)(cursor.Y - scrollDragOffset.Value - scrollTrack.Y) / (scrollTrack.Height - scrollThumb.Height);
            firstRow = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MaxFirstRow);
        }
    }

    internal override void EndContentDrag()
    {
        resizingColumn = -1;
        scrollDragOffset = null;
    }

    internal override void ReceiveScroll(int delta)
    {
        if (delta != 0)
            firstRow = (int)Math.Clamp((long)firstRow - Math.Sign(delta) * Math.Max(1L, Math.Abs((long)delta) / 120), 0, MaxFirstRow);
    }

    private Rectangle CellBounds(int column, Rectangle row) => new(headers[column].X, row.Y, headers[column].Width, row.Height);
    private Rectangle DividerBounds(int column) => new(headers[column].Right - 3, headers[column].Y, 7, headers[column].Height);
    private static int GetMinimumWidth(int column, int tableWidth) => Math.Min(MinimumWidths[column], tableWidth / 4);

    private int GetRowHeight(SpriteFont font, float scale)
    {
        if (!ReferenceEquals(measuredFont, font))
        {
            measuredFont = font;
            glyphHeight = 0;
            foreach (SpriteFont.Glyph glyph in font.Glyphs)
                glyphHeight = Math.Max(glyphHeight, glyph.Cropping.Height);
        }
        // Stardew reduces LineSpacing below some glyph heights. MeasureString still
        // includes those heights, so reserve them plus the one-pixel text shadow.
        return Math.Max(IconSize + Padding, (int)MathF.Ceiling(Math.Max(font.LineSpacing, glyphHeight) * scale) + 2);
    }

    private void FitColumns(int width)
    {
        int minimumTotal = 0;
        int weightTotal = 0;
        for (int i = 0; i < columnWidths.Length; i++)
        {
            columnWidths[i] = GetMinimumWidth(i, width);
            minimumTotal += columnWidths[i];
            weightTotal += Math.Max(0, preferredWidths[i] - columnWidths[i]);
        }

        int used = 0;
        for (int i = 0; i < columnWidths.Length; i++)
        {
            int weight = Math.Max(0, preferredWidths[i] - columnWidths[i]);
            columnWidths[i] += weightTotal == 0 ? (width - minimumTotal) / columnWidths.Length
                : (int)((long)(width - minimumTotal) * weight / weightTotal);
            used += columnWidths[i];
        }
        columnWidths[0] += width - used;
    }

    private static string GetValueText(Item item, bool knowsPrices)
    {
        // Stardew 1.6.15 IClickableMenu.drawHoverText's Price Catalogue branch.
        if (!knowsPrices || item is Furniture or Clothing or Wallpaper || !item.CanBeLostOnDeath()
            || (item is StardewValley.Object obj && obj.bigCraftable.Value))
            return "";

        int unitPrice = item.sellToStorePrice(-1L);
        long value = (long)unitPrice * item.Stack;
        return unitPrice > 0 && value > 0 ? value.ToString("N0", CultureInfo.InvariantCulture) + " g" : "";
    }

    private static void DrawCellText(SpriteBatch batch, string text, Rectangle cell, Color color, ModConfig config, bool rightAligned = false)
    {
        int width = cell.Width - Padding * 2 - 1; // Reserve the shadow and divider pixel too.
        if (width <= 0 || string.IsNullOrEmpty(text))
            return;
        string fitted = UiText.Fit(text, width, config.FontScale);
        Vector2 size = Game1.smallFont.MeasureString(fitted) * config.FontScale;
        if (size.Y + 1 > cell.Height)
            return;
        float x = rightAligned ? cell.Right - Padding - 1 - size.X : cell.X + Padding;
        UiText.Draw(batch, fitted, new Vector2(x, cell.Y + (cell.Height - size.Y) / 2), color, config);
    }

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
