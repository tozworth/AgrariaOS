using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace AgrariaOS.Movement;

/// <summary>A specific live target, never a destructive probe using performToolAction.</summary>
internal sealed record ToolTarget(Rectangle Tiles, Tool Tool, Func<bool> IsValid) : ApproachTarget(Tiles, IsValid)
{
    internal static ToolTarget? Resolve(GameLocation location, Farmer farmer, Point tile)
    {
        Tool? FindTool(Func<Tool, bool> matches) => farmer.CurrentTool is { } held && matches(held)
            ? held : farmer.Items.OfType<Tool>().FirstOrDefault(matches);
        Vector2 key = new(tile.X, tile.Y);
        Rectangle bounds = new(tile.X, tile.Y, 1, 1);
        foreach (ResourceClump clump in location.resourceClumps)
        {
            if (!clump.occupiesTile(tile.X, tile.Y))
                continue;
            bool wood = clump is GiantCrop || clump.parentSheetIndex.Value is 600 or 602 or 44 or 46;
            bool stone = clump.parentSheetIndex.Value is 622 or 672 or 752 or 754 or 756 or 758 or 148;
            Tool? tool = wood ? FindTool(t => t is Axe) : stone ? FindTool(t => t is Pickaxe) : null;
            return tool == null ? null : new ToolTarget(new Rectangle((int)clump.Tile.X, (int)clump.Tile.Y,
                clump.width.Value, clump.height.Value), tool, () => location.resourceClumps.Contains(clump));
        }
        if (location.Objects.TryGetValue(key, out var obj))
        {
            // Only debris: clicking a chest or machine must never automatically break it.
            Tool? tool = obj.IsTwig() ? FindTool(t => t is Axe)
                : obj.IsBreakableStone() ? FindTool(t => t is Pickaxe)
                : obj.IsWeeds() ? FindTool(t => t.isScythe()) : null;
            return tool == null ? null : new ToolTarget(bounds, tool,
                () => location.Objects.TryGetValue(key, out var live) && ReferenceEquals(live, obj));
        }
        if (location.terrainFeatures.TryGetValue(key, out TerrainFeature feature))
        {
            Crop? originalCrop = (feature as HoeDirt)?.crop;
            Tool? tool = feature switch
            {
                Tree tree when !tree.tapped.Value && tree.health.Value > 0 => FindTool(t => t is Axe),
                FruitTree when farmer.CurrentTool is Axe => farmer.CurrentTool,
                Grass => FindTool(t => t.isScythe()),
                HoeDirt dirt when dirt.crop?.dead.Value == true => FindTool(t => t.isScythe()),
                HoeDirt dirt when !dirt.isWatered() && (dirt.needsWatering() || farmer.CurrentTool is WateringCan)
                    => FindTool(t => t is WateringCan),
                _ => null
            };
            return tool == null ? null : new ToolTarget(bounds, tool,
                () => location.terrainFeatures.TryGetValue(key, out var live) && ReferenceEquals(live, feature)
                    && (feature is not Tree tree || (!tree.tapped.Value && tree.health.Value > 0))
                    && (feature is not HoeDirt dirt || (ReferenceEquals(dirt.crop, originalCrop)
                        && (dirt.crop?.dead.Value == true || !dirt.isWatered()))));
        }
        // Bare ground remains movement unless the player explicitly equips a hoe/can.
        // Furniture and the farmhouse fridge must remain approach-only even with a tool equipped.
        if (location.GetFurnitureAt(key) != null || (location is StardewValley.Locations.FarmHouse house
                && house.fridgePosition != Point.Zero && (tile == house.fridgePosition
                    || tile == house.fridgePosition + new Point(0, -1))) || location.getBuildingAt(key) != null)
            return null;
        if (farmer.CurrentTool is Hoe hoe && location.isTilePassable(key)
            && location.doesTileHaveProperty(tile.X, tile.Y, "Diggable", "Back") != null)
            return new ToolTarget(bounds, hoe, () => !location.terrainFeatures.ContainsKey(key) && !location.Objects.ContainsKey(key));
        if (farmer.CurrentTool is WateringCan can && location.CanRefillWateringCanOnTile(tile.X, tile.Y))
            return new ToolTarget(bounds, can, () => location.CanRefillWateringCanOnTile(tile.X, tile.Y));
        return null;
    }
}
