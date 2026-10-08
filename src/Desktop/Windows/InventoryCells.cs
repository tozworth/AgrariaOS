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

partial class InventoryWindow : OsWindow
{
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

    private static void DrawChestIcon(SpriteBatch batch, Chest chest, Point position, int iconSize, float opacity)
    {
        try
        {
            Texture2D? sheet = Game1.bigCraftableSpriteSheet;
            if (sheet == null)
                throw new InvalidOperationException("Chest sprite sheet unavailable.");
            // Vanilla Chest.draw layers a 16x32 base plus its closed lid. Standard
            // chests (130 wood, 232 stone) use a separate colored set at 168 when
            // the player picks a non-default color.
            int baseIndex = chest.ParentSheetIndex;
            int lidIndex = chest.startingLidFrame.Value;
            if (lidIndex <= 0)
                lidIndex = baseIndex + 1;
            Color tint = chest.Tint;
            Color choice = chest.playerChoiceColor.Value;
            float scale = (float)iconSize / 32f;
            Vector2 basePosition = new(position.X + (iconSize - 16 * scale) / 2f,
                position.Y + (iconSize - 32 * scale) / 2f);
            if (chest.playerChest.Value && (baseIndex == 130 || baseIndex == 232) && !choice.Equals(Color.Black))
            {
                int coloredBase = baseIndex == 130 ? 168 : baseIndex;
                int coloredLid = baseIndex == 130 ? lidIndex + 38 : lidIndex;
                int whiteLid = baseIndex == 130 ? lidIndex + 46 : lidIndex + 8;
                Rectangle baseSource = Game1.getSourceRectForStandardTileSheet(sheet, coloredBase, 16, 32);
                Rectangle lidColorSource = Game1.getSourceRectForStandardTileSheet(sheet, coloredLid, 16, 32);
                Rectangle lidWhiteSource = Game1.getSourceRectForStandardTileSheet(sheet, whiteLid, 16, 32);
                Rectangle detailSource = new(0, coloredBase / 8 * 32 + 53, 16, 11);
                Vector2 detailPosition = basePosition + new Vector2(0, 21 * scale);
                batch.Draw(sheet, basePosition, baseSource, choice * opacity, 0f, Vector2.Zero,
                    scale, SpriteEffects.None, 0.91f);
                batch.Draw(sheet, basePosition, lidColorSource, choice * opacity, 0f, Vector2.Zero,
                    scale, SpriteEffects.None, 0.91f);
                batch.Draw(sheet, detailPosition, detailSource, Color.White * opacity, 0f, Vector2.Zero,
                    scale, SpriteEffects.None, 0.91f);
                batch.Draw(sheet, basePosition, lidWhiteSource, Color.White * opacity, 0f, Vector2.Zero,
                    scale, SpriteEffects.None, 0.91f);
                return;
            }
            Rectangle plainBase = Game1.getSourceRectForStandardTileSheet(sheet, baseIndex, 16, 32);
            Rectangle plainLid = Game1.getSourceRectForStandardTileSheet(sheet, lidIndex, 16, 32);
            batch.Draw(sheet, basePosition, plainBase, tint * opacity, 0f, Vector2.Zero,
                scale, SpriteEffects.None, 0.91f);
            batch.Draw(sheet, basePosition, plainLid, tint * opacity, 0f, Vector2.Zero,
                scale, SpriteEffects.None, 0.91f);
        }
        catch
        {
            UiStyle.DrawIcon(batch, UiStyle.InventoryIcon, position, 2, opacity);
        }
    }

    private static int? GetIconSpriteIndex(Item item)
        // Object menu sprites support per-instance indices. Tools use the registry's
        // default sprite; their legacy ParentSheetIndex addresses a different layout.
        => item is StardewValley.Object ? item.ParentSheetIndex : null;

}
