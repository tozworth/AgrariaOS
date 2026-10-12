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
    private OverviewRow[] GetRows()
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null)
        {
            cachedRows = Array.Empty<OverviewRow>();
            scanLocation = null;
            return cachedRows;
        }
        double now;
        try { now = Game1.currentGameTime.TotalGameTime.TotalMilliseconds; }
        catch { now = 0; }
        GameLocation loc = Game1.currentLocation;
        if (cachedRows.Length > 0 && ReferenceEquals(loc, scanLocation) && now - lastScanMs < ScanIntervalMs)
            return cachedRows;
        OverviewRow[] rows = Scan(loc);
        cachedRows = rows;
        scanLocation = loc;
        lastScanMs = now;
        return rows;
    }

    private OverviewRow[] Scan(GameLocation loc)
    {
        Farmer farmer = Game1.player;
        Point farmerTile = farmer.TilePoint;
        var list = new List<OverviewRow>();
        try
        {
            EnsureStaticTiles(loc);
        }
        catch { }

        foreach (NPC npc in loc.characters)
        {
            try
            {
                if (npc == null || npc.IsInvisible)
                    continue;
                string name = !string.IsNullOrWhiteSpace(npc.displayName) ? npc.displayName
                    : !string.IsNullOrWhiteSpace(npc.Name) ? npc.Name : "NPC";
                string category = npc.IsMonster ? "Monster"
                    : npc is Pet ? "Pet" : npc is Horse ? "Horse" : "NPC";
                list.Add(OverviewRow.ForCharacter(loc, npc, name, category, farmerTile));
            }
            catch { }
        }

        try
        {
            foreach (FarmAnimal animal in loc.animals.Values)
            {
                try
                {
                    if (animal == null)
                        continue;
                    string name = !string.IsNullOrWhiteSpace(animal.displayName) ? animal.displayName
                        : GetAnimalTypeName(animal);
                    list.Add(OverviewRow.ForFarmAnimal(loc, animal, name, GetAnimalTypeName(animal), farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var pair in loc.Objects.Pairs)
            {
                try
                {
                    StardewValley.Object obj = pair.Value;
                    if (obj == null)
                        continue;
                    Point tile = new((int)pair.Key.X, (int)pair.Key.Y);
                    string name = string.IsNullOrWhiteSpace(obj.DisplayName) ? "Object" : obj.DisplayName;
                    string category;
                    try { category = obj.getCategoryName(); }
                    catch { category = "Object"; }
                    if (string.IsNullOrWhiteSpace(category))
                        category = "Object";
                    list.Add(OverviewRow.ForObject(loc, obj, pair.Key, tile, name, category, farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (Furniture furniture in loc.furniture)
            {
                try
                {
                    if (furniture == null)
                        continue;
                    string name = string.IsNullOrWhiteSpace(furniture.DisplayName) ? "Furniture" : furniture.DisplayName;
                    list.Add(OverviewRow.ForFurniture(loc, furniture, name, "Furniture", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var pair in loc.terrainFeatures.Pairs)
            {
                try
                {
                    TerrainFeature feature = pair.Value;
                    if (feature == null)
                        continue;
                    Point tile = new((int)pair.Key.X, (int)pair.Key.Y);
                    if (feature is HoeDirt dirt)
                    {
                        if (dirt.crop == null)
                            continue;
                        string name = GetCropName(dirt);
                        list.Add(OverviewRow.ForTerrain(loc, feature, pair.Key, tile, name, "Crop", farmerTile));
                    }
                    else if (feature is FruitTree)
                    {
                        list.Add(OverviewRow.ForTerrain(loc, feature, pair.Key, tile, "Fruit Tree", "Tree", farmerTile));
                    }
                    else if (feature is Tree tree)
                    {
                        string name = tree.stump.Value ? "Tree Stump" : tree.tapped.Value ? "Tapped Tree" : "Tree";
                        list.Add(OverviewRow.ForTerrain(loc, feature, pair.Key, tile, name, "Tree", farmerTile));
                    }
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (LargeTerrainFeature large in loc.largeTerrainFeatures)
            {
                try
                {
                    if (large == null)
                        continue;
                    Rectangle box;
                    try { box = large.getBoundingBox(); }
                    catch { continue; }
                    if (box.Width <= 0 || box.Height <= 0)
                        continue;
                    Point tile = new(box.Center.X / 64, box.Center.Y / 64);
                    if (large is Bush bush)
                    {
                        bool ready;
                        try { ready = bush.readyForHarvest(); }
                        catch { ready = false; }
                        list.Add(OverviewRow.ForLargeTerrain(loc, large, tile, ready ? "Bush (Ready)" : "Bush", "Bush", farmerTile));
                    }
                    else
                        list.Add(OverviewRow.ForLargeTerrain(loc, large, tile, "Terrain", "Terrain", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (ResourceClump clump in loc.resourceClumps)
            {
                try
                {
                    if (clump == null)
                        continue;
                    Point tile = new((int)clump.Tile.X, (int)clump.Tile.Y);
                    list.Add(OverviewRow.ForClump(loc, clump, tile, GetClumpName(clump), "Resource", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (Building building in loc.buildings)
            {
                try
                {
                    if (building == null)
                        continue;
                    Point tile = new(building.tileX.Value, building.tileY.Value);
                    string name = string.IsNullOrWhiteSpace(building.buildingType.Value) ? "Building" : building.buildingType.Value;
                    list.Add(OverviewRow.ForBuilding(loc, building, tile, name, "Building", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (MapSeat seat in loc.mapSeats)
            {
                try
                {
                    if (seat == null)
                        continue;
                    Point tile = new((int)seat.tilePosition.X, (int)seat.tilePosition.Y);
                    list.Add(OverviewRow.ForSeat(loc, seat, tile, "Seat", "Seat", farmerTile));
                }
                catch { }
            }
        }
        catch { }

        IReadOnlyList<MapTransition> transitions = MapTransitions.Scan(loc);
        foreach (MapTransition transition in transitions)
            list.Add(OverviewRow.ForTransition(loc, transition, farmerTile));
        HashSet<Point> transitionTiles = transitions.SelectMany(transition => transition.Tiles).ToHashSet();
        foreach (StaticTile tile in staticTiles)
        {
            if (!transitionTiles.Contains(tile.Tile) && (tile.Kind != StaticKind.Action
                || loc.doesTileHaveProperty(tile.Tile.X, tile.Tile.Y, "Action", "Buildings") == tile.Action))
                list.Add(OverviewRow.ForStaticTile(loc, tile, farmerTile));
        }

        list.AddRange(ScanDynamicTiles(loc, farmerTile));

        if (loc is FarmHouse house)
        {
            try
            {
                if (house.fridgePosition != Point.Zero)
                    list.Add(OverviewRow.ForFridge(loc, house.fridgePosition, farmerTile));
            }
            catch { }
        }

        list.Sort((a, b) =>
        {
            int compare = a.Distance.CompareTo(b.Distance);
            return compare != 0 ? compare : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });
        return list.ToArray();
    }






}
