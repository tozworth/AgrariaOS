using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using AgrariaOS.Desktop.Windows;
using AgrariaOS.UI;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;

namespace AgrariaOS.Integration;

partial class LookupAnythingIntegration
{
    internal object? GetInventorySubject(Item item) => GetSubject(() =>
        buildItem.Invoke(itemProvider, new object?[] { item, inventoryContext, null, true }));

    internal object? GetWorldSubject() => GetSubject(() =>
        worldSubject.Invoke(factory, new object[] { Game1.player, Game1.currentLocation, true }));

    internal object? GetSubjectEntity(object subject) => AccessTools.Field(subject.GetType(), "Target")?.GetValue(subject);

    internal object? GetEntitySubject(object target, GameLocation location) => GetSubject(() =>
        entitySubject.Invoke(factory, new object[] { target, location }));

    internal object? GetTileSubject(GameLocation location, Vector2 tile) => GetSubject(() =>
    {
        object? target = tileTarget.Invoke(factory, new object[] { location, tile });
        return target == null ? null : ((Delegate)targetSubject.GetValue(target)!).DynamicInvoke();
    });

    private object? GetSubject(Func<object?> get)
    {
        if (!Available)
            return null;
        try { return get(); }
        catch (Exception ex)
        {
            monitor.Log($"Couldn't get Lookup Anything information.\n{ex}", LogLevel.Warn);
            return null;
        }
    }

    internal string GetName(object subject) => subjectName.GetValue(subject)?.ToString() ?? "Info";

    internal bool DrawSubjectIcon(object subject, SpriteBatch batch, int size)
    {
        object? entity = GetSubjectEntity(subject);
        // Character portraits can scale by width alone; fit the actual sprite in both axes.
        if (entity is Character { Sprite: { } sprite })
        {
            DrawFittedSprite(batch, sprite.Texture, sprite.SourceRect, size, Color.White);
            return true;
        }
        // Tree.drawInMenu uses world-sized offsets which put its portrait outside the slot.
        if (entity is Tree tree && tree.texture.Value is { } treeTexture)
        {
            Rectangle source = tree.growthStage.Value switch
            {
                0 => new Rectangle(32, 128, 16, 16),
                1 => new Rectangle(0, 128, 16, 16),
                2 => new Rectangle(16, 128, 16, 16),
                < 5 => new Rectangle(0, 96, 16, 32),
                _ => tree.stump.Value ? Tree.stumpSourceRect : Tree.treeTopSourceRect
            };
            DrawFittedSprite(batch, treeTexture, source, size,
                tree.growthStage.Value < 5 && tree.fertilized.Value ? Color.HotPink : Color.White,
                tree.flipped.Value ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            return true;
        }
        if ((bool)subjectPortrait.Invoke(subject, new object[] { batch, Vector2.Zero, new Vector2(size) })!)
            return true;

        // LA has no portrait for map tiles. Composite the inspected tile's own map layers.
        if (!tileSubjectType.IsInstanceOfType(subject))
            return false;
        var location = (GameLocation)tileLocation.GetValue(subject)!;
        var tile = (Vector2)tilePosition.GetValue(subject)!;
        bool drawn = false;
        foreach (var layer in location.Map.Layers)
        {
            int x = (int)tile.X, y = (int)tile.Y;
            if (x < 0 || y < 0 || x >= layer.LayerWidth || y >= layer.LayerHeight || layer.Tiles[x, y] is not { } mapTile)
                continue;
            var image = mapTile.TileSheet.GetTileImageBounds(mapTile.TileIndex);
            Texture2D texture = Game1.content.Load<Texture2D>(mapTile.TileSheet.ImageSource);
            batch.Draw(texture, new Rectangle(0, 0, size, size),
                new Rectangle(image.X, image.Y, image.Width, image.Height), Color.White);
            drawn = true;
        }
        return drawn;
    }

    private static void DrawFittedSprite(SpriteBatch batch, Texture2D texture, Rectangle source, int size,
        Color color, SpriteEffects effects = SpriteEffects.None)
    {
        float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
        Vector2 position = new((size - source.Width * scale) / 2, (size - source.Height * scale) / 2);
        batch.Draw(texture, position, source, color, 0, Vector2.Zero, scale, effects, 0);
    }

}
