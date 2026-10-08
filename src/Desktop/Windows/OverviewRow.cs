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
    private enum RowKind
    {
        Character,
        FarmAnimal,
        Object,
        Furniture,
        Terrain,
        LargeTerrain,
        Clump,
        Building,
        Seat,
        StaticTile,
        DynamicTile,
        Fridge
    }

    internal enum StaticKind
    {
        Action,
        BusTicket,
        Warp
    }

    internal sealed record StaticTile(Point Tile, StaticKind Kind, string Name, string Category,
        string? Action, int Index, string Layer, string Sheet);

    internal sealed class OverviewRow
    {
        internal string Name { get; }
        internal string Category { get; }
        internal float Distance { get; }
        internal string DistanceText { get; }
        internal string StableKey { get; }
        internal object? Entity { get; }
        private readonly GameLocation location;
        private readonly RowKind kind;
        private readonly Point tile;
        private readonly Vector2 tileKey;
        private readonly StaticTile? staticTile;
        private readonly string? layer;
        private readonly string? sheet;
        private readonly int tileIndex;
        private readonly string? action;
        private readonly Point fridgePosition;

        private OverviewRow(GameLocation location, RowKind kind, object? entity, Point tile, Vector2 tileKey,
            string name, string category, float distance, string stableKey,
            StaticTile? staticTile = null, string? layer = null, string? sheet = null, int tileIndex = 0,
            string? action = null, Point fridgePosition = default)
        {
            this.location = location;
            this.kind = kind;
            Entity = entity;
            this.tile = tile;
            this.tileKey = tileKey;
            Name = name;
            Category = category;
            Distance = distance;
            DistanceText = $"{distance:0.#}m";
            StableKey = stableKey;
            this.staticTile = staticTile;
            this.layer = layer;
            this.sheet = sheet;
            this.tileIndex = tileIndex;
            this.action = action;
            this.fridgePosition = fridgePosition;
        }

        private static float TileDistance(Point farmerTile, Point tile)
            => Vector2.Distance(new Vector2(farmerTile.X, farmerTile.Y), new Vector2(tile.X, tile.Y));

        internal static OverviewRow ForCharacter(GameLocation loc, NPC npc, string name, string category, Point farmerTile)
        {
            Point tile;
            try { tile = npc.TilePoint; }
            catch { tile = farmerTile; }
            return new OverviewRow(loc, RowKind.Character, npc, tile, new Vector2(tile.X, tile.Y),
                name, category, TileDistance(farmerTile, tile),
                $"char:{RuntimeHelpers.GetHashCode(npc)}");
        }

        internal static OverviewRow ForFarmAnimal(GameLocation loc, FarmAnimal animal, string name, string category, Point farmerTile)
        {
            Point tile;
            try { tile = animal.TilePoint; }
            catch { tile = farmerTile; }
            return new OverviewRow(loc, RowKind.FarmAnimal, animal, tile, new Vector2(tile.X, tile.Y),
                name, category, TileDistance(farmerTile, tile),
                $"animal:{RuntimeHelpers.GetHashCode(animal)}");
        }

        internal static OverviewRow ForObject(GameLocation loc, StardewValley.Object obj, Vector2 key, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Object, obj, tile, key, name, category, TileDistance(farmerTile, tile),
                $"obj:{RuntimeHelpers.GetHashCode(obj)}");

        internal static OverviewRow ForFurniture(GameLocation loc, Furniture furniture, string name, string category, Point farmerTile)
        {
            Point tile;
            try { tile = new Point((int)furniture.TileLocation.X, (int)furniture.TileLocation.Y); }
            catch { tile = farmerTile; }
            return new OverviewRow(loc, RowKind.Furniture, furniture, tile, new Vector2(tile.X, tile.Y),
                name, category, TileDistance(farmerTile, tile),
                $"furn:{RuntimeHelpers.GetHashCode(furniture)}");
        }

        internal static OverviewRow ForTerrain(GameLocation loc, TerrainFeature feature, Vector2 key, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Terrain, feature, tile, key, name, category, TileDistance(farmerTile, tile),
                $"terrain:{RuntimeHelpers.GetHashCode(feature)}");

        internal static OverviewRow ForLargeTerrain(GameLocation loc, LargeTerrainFeature large, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.LargeTerrain, large, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"large:{RuntimeHelpers.GetHashCode(large)}");

        internal static OverviewRow ForClump(GameLocation loc, ResourceClump clump, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Clump, clump, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"clump:{RuntimeHelpers.GetHashCode(clump)}");

        internal static OverviewRow ForBuilding(GameLocation loc, Building building, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Building, building, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"building:{RuntimeHelpers.GetHashCode(building)}");

        internal static OverviewRow ForSeat(GameLocation loc, MapSeat seat, Point tile,
            string name, string category, Point farmerTile)
            => new(loc, RowKind.Seat, seat, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"seat:{RuntimeHelpers.GetHashCode(seat)}");

        internal static OverviewRow ForStaticTile(GameLocation loc, StaticTile tile, Point farmerTile)
            => new(loc, RowKind.StaticTile, null, tile.Tile, new Vector2(tile.Tile.X, tile.Tile.Y),
                tile.Name, tile.Category, TileDistance(farmerTile, tile.Tile),
                $"static:{tile.Kind}:{tile.Tile.X},{tile.Tile.Y}",
                staticTile: tile, layer: tile.Layer, sheet: tile.Sheet, tileIndex: tile.Index, action: tile.Action);

        internal static OverviewRow ForDynamicTile(GameLocation loc, Point tile, string name, string category,
            string layer, string sheet, int index, Point farmerTile)
            => new(loc, RowKind.DynamicTile, null, tile, new Vector2(tile.X, tile.Y), name, category,
                TileDistance(farmerTile, tile), $"dynamic:{tile.X},{tile.Y}:{index}",
                layer: layer, sheet: sheet, tileIndex: index);

        internal static OverviewRow ForFridge(GameLocation loc, Point fridge, Point farmerTile)
            => new(loc, RowKind.Fridge, null, fridge, new Vector2(fridge.X, fridge.Y), "Fridge", "Appliance",
                TileDistance(farmerTile, fridge), $"fridge:{fridge.X},{fridge.Y}",
                fridgePosition: fridge);

        internal Point GetTile()
        {
            switch (kind)
            {
                case RowKind.Character when Entity is NPC npc:
                    return npc.TilePoint;
                case RowKind.FarmAnimal when Entity is FarmAnimal animal:
                    return animal.TilePoint;
                case RowKind.Furniture when Entity is Furniture furniture:
                    return new Point((int)furniture.TileLocation.X, (int)furniture.TileLocation.Y);
                default:
                    return tile;
            }
        }

        internal bool IsValid()
        {
            if (!Context.IsWorldReady || Game1.currentLocation != location)
                return false;
            switch (kind)
            {
                case RowKind.Character when Entity is NPC npc:
                    return location.characters.Any(candidate => ReferenceEquals(candidate, npc));
                case RowKind.FarmAnimal when Entity is FarmAnimal animal:
                    return location.animals.Values.Any(candidate => ReferenceEquals(candidate, animal));
                case RowKind.Object when Entity is StardewValley.Object obj:
                    return location.Objects.TryGetValue(tileKey, out var live) && ReferenceEquals(live, obj);
                case RowKind.Furniture when Entity is Furniture furniture:
                    return location.furniture.Contains(furniture);
                case RowKind.Terrain when Entity is TerrainFeature feature:
                    return location.terrainFeatures.TryGetValue(tileKey, out var liveFeature) && ReferenceEquals(liveFeature, feature);
                case RowKind.LargeTerrain when Entity is LargeTerrainFeature large:
                    return location.largeTerrainFeatures.Contains(large);
                case RowKind.Clump when Entity is ResourceClump clump:
                    return location.resourceClumps.Contains(clump);
                case RowKind.Building when Entity is Building building:
                    return location.buildings.Contains(building);
                case RowKind.Seat when Entity is MapSeat seat:
                    return location.mapSeats.Contains(seat);
                case RowKind.StaticTile when staticTile != null:
                    if (!location.isTileOnMap(tile))
                        return false;
                    if (staticTile.Kind == StaticKind.Action)
                    {
                        try { return location.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings") == action; }
                        catch { return false; }
                    }
                    try { return location.getTileIndexAt(new xTile.Dimensions.Location(tile.X, tile.Y), layer!, sheet!) == tileIndex; }
                    catch { return false; }
                case RowKind.DynamicTile:
                    if (!location.isTileOnMap(tile))
                        return false;
                    try { return location.getTileIndexAt(new xTile.Dimensions.Location(tile.X, tile.Y), layer!, sheet!) == tileIndex; }
                    catch { return false; }
                case RowKind.Fridge:
                    return location is FarmHouse house && house.fridgePosition == fridgePosition;
                default:
                    return false;
            }
        }
    }

}
