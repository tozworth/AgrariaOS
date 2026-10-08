using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace AgrariaOS.Hud;

internal static partial class HoverHighlight
{
    internal static bool TryGetBounds(GameLocation location, int worldX, int worldY, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        try
        {
            int tileX = (int)MathF.Floor(worldX / 64f);
            int tileY = (int)MathF.Floor(worldY / 64f);
            Point tile = new(tileX, tileY);
            Vector2 key = new(tileX, tileY);
            bool onMap = false;
            try { onMap = location.isTileOnMap(tile); }
            catch { onMap = true; }

            // Location-override tiles first so the sheen previews what right-click walks to.
            try
            {
                if (location is BusStop && onMap)
                {
                    int index = location.getTileIndexAt(new xTile.Dimensions.Location(tileX, tileY), "Buildings", "outdoors");
                    if (index is 1057 or 958 or 1080 or 1081)
                    {
                        bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                        return true;
                    }
                }
                else if (location is MineShaft && onMap)
                {
                    int index = location.getTileIndexAt(new xTile.Dimensions.Location(tileX, tileY), "Buildings", "mine");
                    if (index is 284 or 112 or 115 or 173 or 174 or 194 or 315 or 316 or 317)
                    {
                        bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                        return true;
                    }
                }
                else if (location is VolcanoDungeon && onMap)
                {
                    int index = location.getTileIndexAt(new xTile.Dimensions.Location(tileX, tileY), "Buildings", "dungeon");
                    if (index is 367 or 77)
                    {
                        bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                        return true;
                    }
                }
                else if (location is CommunityCenter && onMap)
                {
                    int index = location.getTileIndexAt(new xTile.Dimensions.Location(tileX, tileY), "Buildings", "indoors");
                    if (index is 1799 or (>= 1824 and <= 1833))
                    {
                        bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                        return true;
                    }
                }
                else if (location is Sewer && onMap)
                {
                    int index = location.getTileIndexAt(new xTile.Dimensions.Location(tileX, tileY), "Buildings", "st");
                    if (index is 21 or 84)
                    {
                        bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                if (location is FarmHouse house && house.fridgePosition != Point.Zero
                    && (tile == house.fridgePosition || tile == house.fridgePosition + new Point(0, -1)))
                {
                    bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                    return true;
                }
            }
            catch { }

            try
            {
                foreach (FarmAnimal animal in location.animals.Values)
                {
                    if (animal == null)
                        continue;
                    Rectangle box;
                    try { box = animal.GetBoundingBox(); }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    if (box.Contains(worldX, worldY))
                    {
                        bounds = box;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                if (onMap && location.getBuildingAt(key) is { } building)
                {
                    _ = building;
                    bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                    return true;
                }
            }
            catch { }

            // NPC boxes are feet-only; expand upward so heads count and the sheen covers the body.
            try
            {
                foreach (NPC npc in location.characters)
                {
                    if (npc == null || npc.IsInvisible)
                        continue;
                    Rectangle box;
                    try { box = npc.GetBoundingBox(); }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    Rectangle expanded = new(box.X - 8, box.Y - 80, box.Width + 16, box.Height + 80);
                    if (expanded.Contains(worldX, worldY))
                    {
                        bounds = expanded;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                foreach (Farmer farmer in location.farmers)
                {
                    if (farmer == null || ReferenceEquals(farmer, Game1.player))
                        continue;
                    Rectangle box;
                    try { box = farmer.GetBoundingBox(); }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    Rectangle expanded = new(box.X - 8, box.Y - 80, box.Width + 16, box.Height + 80);
                    if (expanded.Contains(worldX, worldY))
                    {
                        bounds = expanded;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                if (onMap && location.Objects.TryGetValue(key, out var obj) && obj != null)
                {
                    try
                    {
                        Rectangle box = obj.GetBoundingBox();
                        if (box.Width > 0 && box.Height > 0)
                        {
                            bounds = box;
                            return true;
                        }
                    }
                    catch { }
                    bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                    return true;
                }
            }
            catch { }

            try
            {
                foreach (ResourceClump clump in location.resourceClumps)
                {
                    if (clump == null)
                        continue;
                    Rectangle box;
                    try
                    {
                        box = new Rectangle((int)clump.Tile.X * 64, (int)clump.Tile.Y * 64,
                            clump.width.Value * 64, clump.height.Value * 64);
                    }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    if (box.Contains(worldX, worldY))
                    {
                        bounds = box;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                if (onMap && location.terrainFeatures.TryGetValue(key, out var feature) && feature != null)
                {
                    bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                    return true;
                }
            }
            catch { }

            try
            {
                foreach (LargeTerrainFeature large in location.largeTerrainFeatures)
                {
                    if (large == null)
                        continue;
                    Rectangle box;
                    try { box = large.getBoundingBox(); }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    if (box.Contains(worldX, worldY))
                    {
                        bounds = box;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                if (onMap && location.doesTileHaveProperty(tileX, tileY, "Action", "Buildings") != null)
                {
                    bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                    return true;
                }
            }
            catch { }

            try
            {
                if (onMap)
                {
                    foreach (MapSeat seat in location.mapSeats)
                    {
                        bool occupies;
                        try { occupies = seat.OccupiesTile(tileX, tileY); }
                        catch { continue; }
                        if (!occupies)
                            continue;
                        bool blocked;
                        try { blocked = seat.IsBlocked(location); }
                        catch { blocked = false; }
                        if (blocked)
                            continue;
                        bounds = new Rectangle(tileX * 64, tileY * 64, 64, 64);
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                foreach (Furniture furniture in location.furniture)
                {
                    if (furniture == null || furniture.furniture_type.Value == 12)
                        continue;
                    Rectangle box;
                    try { box = furniture.boundingBox.Value; }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    if (box.Contains(worldX, worldY))
                    {
                        bounds = box;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }
        catch
        {
            bounds = Rectangle.Empty;
            return false;
        }
    }

}
