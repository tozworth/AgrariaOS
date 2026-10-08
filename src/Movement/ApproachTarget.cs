using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;

namespace AgrariaOS.Movement;

/// <summary>A live footprint to approach, without automatically interacting with it.</summary>
internal record ApproachTarget(Rectangle Tiles, Func<bool> IsValid)
{
    internal IEnumerable<Point> ApproachTiles()
    {
        for (int x = Tiles.Left; x < Tiles.Right; x++)
        {
            yield return new Point(x, Tiles.Top - 1);
            yield return new Point(x, Tiles.Bottom);
        }
        for (int y = Tiles.Top; y < Tiles.Bottom; y++)
        {
            yield return new Point(Tiles.Left - 1, y);
            yield return new Point(Tiles.Right, y);
        }
    }

    /// <summary>All walkable candidates within vanilla right-click reach (Chebyshev 1,
    /// including diagonals). Vanilla's tileWithinRadiusOfPlayer allows diagonal
    /// collection, but <see cref="ApproachTiles"/> is orthogonal-only for tool swings
    /// which require Manhattan adjacency.</summary>
    internal IEnumerable<Point> InteractTiles()
    {
        for (int x = Tiles.Left - 1; x <= Tiles.Right; x++)
        {
            for (int y = Tiles.Top - 1; y <= Tiles.Bottom; y++)
            {
                if (x >= Tiles.Left && x < Tiles.Right && y >= Tiles.Top && y < Tiles.Bottom)
                    continue;
                yield return new Point(x, y);
            }
        }
    }

    internal Point HitTile(Point farmerTile) => new(Math.Clamp(farmerTile.X, Tiles.Left, Tiles.Right - 1),
        Math.Clamp(farmerTile.Y, Tiles.Top, Tiles.Bottom - 1));

    private static Rectangle TileBounds(Rectangle pixels, Point fallback)
    {
        if (pixels.Width <= 0 || pixels.Height <= 0)
            return new Rectangle(fallback.X, fallback.Y, 1, 1);
        int left = (int)MathF.Floor(pixels.Left / 64f);
        int top = (int)MathF.Floor(pixels.Top / 64f);
        return new Rectangle(left, top, (int)MathF.Ceiling(pixels.Right / 64f) - left,
            (int)MathF.Ceiling(pixels.Bottom / 64f) - top);
    }

    internal static ApproachTarget? ResolveSolid(GameLocation location, Farmer farmer, Point tile,
        Func<Point, bool> canWalk)
    {
        Vector2 key = new(tile.X, tile.Y);
        if (location.Objects.TryGetValue(key, out var obj) && !obj.isPassable())
        {
            Rectangle box = obj.GetBoundingBox();
            return new ApproachTarget(TileBounds(box, tile), () => location.Objects.TryGetValue(key, out var live)
                && ReferenceEquals(live, obj) && obj.GetBoundingBox() == box);
        }
        if (location.GetFurnitureAt(key) is { } furniture && !furniture.isPassable())
        {
            Rectangle box = furniture.GetBoundingBox();
            return new ApproachTarget(TileBounds(box, tile), () => location.furniture.Contains(furniture)
                && furniture.GetBoundingBox() == box);
        }
        // The built-in fridge is a map tile, not an entry in Objects. Its upper sprite tile
        // belongs to the same target; approach the lower interaction tile rather than the wall.
        if (location is FarmHouse house && house.fridgePosition != Point.Zero
            && (tile == house.fridgePosition || tile == house.fridgePosition + new Point(0, -1)))
        {
            Point fridge = house.fridgePosition;
            return new ApproachTarget(new Rectangle(fridge.X, fridge.Y, 1, 1), () => house.fridgePosition == fridge);
        }
        foreach (var clump in location.resourceClumps)
        {
            if (!clump.occupiesTile(tile.X, tile.Y))
                continue;
            Rectangle box = clump.getBoundingBox();
            return new ApproachTarget(TileBounds(box, tile), () => location.resourceClumps.Contains(clump)
                && clump.getBoundingBox() == box);
        }
        if (location.terrainFeatures.TryGetValue(key, out var feature) && !feature.isPassable(farmer))
        {
            Rectangle box = feature.getBoundingBox();
            return new ApproachTarget(TileBounds(box, tile), () => location.terrainFeatures.TryGetValue(key, out var live)
                && ReferenceEquals(live, feature) && feature.getBoundingBox() == box);
        }
        foreach (var largeFeature in location.largeTerrainFeatures)
        {
            Rectangle box = largeFeature.getBoundingBox();
            if (!box.Contains(tile.X * 64 + 32, tile.Y * 64 + 32) || largeFeature.isPassable(farmer))
                continue;
            return new ApproachTarget(TileBounds(box, tile), () => location.largeTerrainFeatures.Contains(largeFeature)
                && largeFeature.getBoundingBox() == box);
        }
        if (location.getBuildingAt(key) is { } building && !building.isTilePassable(key))
        {
            Rectangle box = building.GetBoundingBox();
            return new ApproachTarget(TileBounds(box, tile), () => location.buildings.Contains(building)
                && building.GetBoundingBox() == box);
        }
        // Static walls and other blocked map tiles have no live entity to resolve.
        return canWalk(tile) ? null : new ApproachTarget(new Rectangle(tile.X, tile.Y, 1, 1),
            () => location.isTileOnMap(tile));
    }
}
