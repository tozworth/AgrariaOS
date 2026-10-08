using AgrariaOS.Configuration;
using AgrariaOS.Integration;
using AgrariaOS.Interactions;
using AgrariaOS.Movement;
using AgrariaOS.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System.Runtime.CompilerServices;

namespace AgrariaOS.Desktop.Windows;

partial class OverviewWindow : OsWindow
{
    private static void DrawNameCell(SpriteBatch batch, OverviewRow row, Rectangle bounds, ModConfig config)
    {
        try
        {
            Rectangle icon = new(bounds.X + Padding, bounds.Y + (bounds.Height - IconSize) / 2, IconSize, IconSize);
            TryDrawIcon(batch, row, icon, config.Opacity);
        }
        catch { }
        bounds.X += IconSize + Padding;
        bounds.Width -= IconSize + Padding;
        UiText.DrawCell(batch, row.Name, bounds, UiStyle.Primary, config);
    }

    private static bool TryDrawIcon(SpriteBatch batch, OverviewRow row, Rectangle bounds, float opacity)
    {
        try
        {
            if (row.Entity is Item item)
            {
                DrawItemIcon(batch, item, bounds, opacity);
                return true;
            }
            if (row.Entity is FarmAnimal farmAnimal)
            {
                if (TryGetAnimalSprite(farmAnimal, out Texture2D? texture, out Rectangle source) && texture != null)
                {
                    DrawFitted(batch, texture, source, bounds, Color.White * opacity);
                    return true;
                }
                return false;
            }
            if (row.Entity is NPC npc)
            {
                var sprite = npc.Sprite;
                if (sprite?.Texture == null || sprite.SourceRect.Width <= 0 || sprite.SourceRect.Height <= 0)
                    return false;
                DrawFitted(batch, sprite.Texture, sprite.SourceRect, bounds, Color.White * opacity);
                return true;
            }
            if (row.Entity is Tree tree && tree.texture.Value is { } treeTexture)
            {
                Rectangle source = tree.growthStage.Value switch
                {
                    0 => new Rectangle(32, 128, 16, 16),
                    1 => new Rectangle(0, 128, 16, 16),
                    2 => new Rectangle(16, 128, 16, 16),
                    < 5 => new Rectangle(0, 96, 16, 32),
                    _ => tree.stump.Value ? Tree.stumpSourceRect : Tree.treeTopSourceRect
                };
                Color tint = tree.growthStage.Value < 5 && tree.fertilized.Value ? Color.HotPink : Color.White;
                DrawFitted(batch, treeTexture, source, bounds, tint * opacity,
                    tree.flipped.Value ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static void DrawFitted(SpriteBatch batch, Texture2D texture, Rectangle source, Rectangle bounds,
        Color color, SpriteEffects effects = SpriteEffects.None)
    {
        if (source.Width <= 0 || source.Height <= 0)
            return;
        float scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
        batch.Draw(texture, destination, source, color, 0f, Vector2.Zero, effects, 0.91f);
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

    private static bool TryGetAnimalSprite(Character animal, out Texture2D? texture, out Rectangle source)
    {
        texture = null;
        source = Rectangle.Empty;
        try
        {
            switch (animal)
            {
                case FarmAnimal farmAnimal:
                {
                    var sprite = farmAnimal.GetOrLoadTexture();
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    if (sprite.SourceRect.Height > 16)
                    {
                        source = string.Equals(farmAnimal.type.Value, "Ostrich", StringComparison.Ordinal)
                            ? new Rectangle(0, sprite.SourceRect.Height * 2 - 32, sprite.SourceRect.Width, 28)
                            : new Rectangle(0, sprite.SourceRect.Height * 2 - 28, sprite.SourceRect.Width, 28);
                    }
                    else
                        source = new Rectangle(0, 16, 16, 16);
                    return true;
                }
                case Pet pet:
                {
                    var sprite = pet.Sprite;
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    source = new Rectangle(0, sprite.SourceRect.Height * 2 - 24, sprite.SourceRect.Width, 24);
                    return true;
                }
                case Horse horse:
                {
                    var sprite = horse.Sprite;
                    if (sprite?.Texture == null)
                        return false;
                    texture = sprite.Texture;
                    source = new Rectangle(0, sprite.SourceRect.Height * 2 - 26, sprite.SourceRect.Width, 24);
                    return true;
                }
            }
        }
        catch
        {
            texture = null;
            source = Rectangle.Empty;
        }
        return false;
    }

}
