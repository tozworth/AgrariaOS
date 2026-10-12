using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;

namespace AgrariaOS.Movement;

internal enum MapTransitionKind { Warp, Action, TouchAction, Building }

/// <summary>A discovered entrance. Discovery never runs an action or tests a door's unlock conditions.</summary>
internal sealed class MapTransition
{
    internal MapTransitionKind Kind { get; }
    internal Point[] Tiles { get; }
    internal string Name { get; }
    internal string Key { get; }
    private readonly GameLocation location;
    private readonly Func<bool> valid;

    internal MapTransition(GameLocation location, MapTransitionKind kind, Point[] tiles,
        string name, string key, Func<bool> valid)
    {
        this.location = location;
        Kind = kind;
        Tiles = tiles;
        Name = name;
        Key = key;
        this.valid = valid;
    }

    internal bool IsValid() => Game1.currentLocation == location && valid();

    internal Point VisibleTile(Point source)
        => new(Math.Clamp(source.X, 0, location.Map.Layers[0].LayerWidth - 1),
            Math.Clamp(source.Y, 0, location.Map.Layers[0].LayerHeight - 1));

    internal Point NearestTile(Point farmerTile) => Tiles.Select(VisibleTile)
        .OrderBy(tile => Vector2.DistanceSquared(tile.ToVector2(), farmerTile.ToVector2())).First();
}

internal static class MapTransitions
{
    private sealed record Entry(MapTransitionKind Kind, Point Tile, string Destination, string Signature, Func<bool> Valid);

    internal static IReadOnlyList<MapTransition> Scan(GameLocation location)
    {
        var entries = new List<Entry>();
        int width = location.Map.Layers[0].LayerWidth;
        int height = location.Map.Layers[0].LayerHeight;
        foreach (Warp warp in location.warps)
        {
            if (warp.npcOnly.Value || warp.X < -1 || warp.Y < -1 || warp.X > width || warp.Y > height)
                continue;
            Point tile = new(warp.X, warp.Y);
            string destination = warp.TargetName;
            int targetX = warp.TargetX, targetY = warp.TargetY;
            entries.Add(new(MapTransitionKind.Warp, tile, destination, destination,
                () => location.warps.Contains(warp) && !warp.npcOnly.Value && warp.X == tile.X
                    && warp.Y == tile.Y && warp.TargetName == destination && warp.TargetX == targetX && warp.TargetY == targetY));
        }

        // Read the live map, including properties inherited from tile sheets. This also
        // finds doors on custom maps and entrances added/removed by map patches.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Point tile = new(x, y);
                string? action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                string? destination = ActionDestination(location, tile, action);
                if (destination != null)
                    entries.Add(new(MapTransitionKind.Action, tile, destination, action!,
                        () => location.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings") == action));
                string? touch = location.doesTileHaveProperty(x, y, "TouchAction", "Back");
                if (!string.IsNullOrWhiteSpace(touch))
                {
                    string[] args = ArgUtility.SplitBySpace(touch);
                    if (args.Length >= 4 && args[0] is "Warp" or "MagicWarp"
                        && int.TryParse(args[2], out _) && int.TryParse(args[3], out _))
                        entries.Add(new(MapTransitionKind.TouchAction, tile, args[1], touch,
                            () => location.doesTileHaveProperty(tile.X, tile.Y, "TouchAction", "Back") == touch));
                }
            }
        }
        foreach (Building building in location.buildings)
        {
            if (!building.HasIndoors())
                continue;
            Point tile = building.getPointForHumanDoor();
            if (!location.isTileOnMap(tile))
                continue;
            string name = building.buildingType.Value;
            entries.Add(new(MapTransitionKind.Building, tile, name,
                $"building:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(building)}",
                () => location.buildings.Contains(building) && building.HasIndoors()
                    && building.getPointForHumanDoor() == tile));
        }

        // A road boundary or a multi-tile door is one row; separate entrances to the
        // same destination remain separate. Different native actions never merge.
        var result = new List<MapTransition>();
        HashSet<Point> actionTiles = entries.Where(entry => entry.Kind is MapTransitionKind.Action or MapTransitionKind.Building)
            .Select(entry => entry.Tile).ToHashSet();
        entries.RemoveAll(entry => entry.Kind == MapTransitionKind.Warp && actionTiles.Contains(entry.Tile));
        foreach (var group in entries.GroupBy(entry => (entry.Kind, entry.Signature)))
        {
            var remaining = group.ToList();
            while (remaining.Count > 0)
            {
                var component = new List<Entry> { remaining[0] };
                remaining.RemoveAt(0);
                for (int i = 0; i < component.Count; i++)
                {
                    Point from = component[i].Tile;
                    for (int j = remaining.Count - 1; j >= 0; j--)
                    {
                        Point to = remaining[j].Tile;
                        if (Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) <= 1)
                        {
                            component.Add(remaining[j]);
                            remaining.RemoveAt(j);
                        }
                    }
                }
                Entry[] sources = component.OrderBy(entry => entry.Tile.X).ThenBy(entry => entry.Tile.Y).ToArray();
                Entry anchor = sources[0];
                string name = anchor.Kind == MapTransitionKind.Building ? anchor.Destination : DestinationName(anchor.Destination);
                result.Add(new MapTransition(location, anchor.Kind, sources.Select(entry => entry.Tile).Distinct().ToArray(),
                    name, $"warp:{anchor.Kind}:{anchor.Tile.X},{anchor.Tile.Y}:{anchor.Signature}",
                    () => sources.All(entry => entry.Valid())));
            }
        }
        return result;
    }

    internal static string? ActionDestination(GameLocation location, Point tile, string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return null;
        string[] args = ArgUtility.SplitBySpace(action);
        string first = args.FirstOrDefault() ?? "";
        if (first.Contains("Warp", StringComparison.Ordinal) && args.Length >= 4
            && int.TryParse(args[1], out _) && int.TryParse(args[2], out _))
            return args[3];
        // These native actions encode their destination in game code, not map data.
        // Recognize action types only; their positions still come from the current map.
        string? destination = first switch
        {
            "EnterSewer" => "Sewer",
            "WarpCommunityCenter" => "CommunityCenter",
            "Warp_Sunroom_Door" => "Sunroom",
            "WarpBoatTunnel" => "BoatTunnel",
            "WarpGreenhouse" => "Greenhouse",
            "WizardHatch" => "WizardHouseBasement",
            "MasteryRoom" => "MasteryCave",
            "Minecart" => "Minecart",
            _ => null
        };
        if (destination == null && first.Contains("Warp", StringComparison.Ordinal)
            && location.doors.TryGetValue(tile, out string doorDestination))
            destination = doorDestination;
        return destination;
    }

    private static string DestinationName(string destination)
    {
        try
        {
            // Only inspect already loaded locations: getLocationFromName can create
            // a mine level, which must not happen just to label an Overview row.
            string? display = Game1.locations.FirstOrDefault(candidate => candidate.NameOrUniqueName == destination)?.DisplayName;
            if (!string.IsNullOrWhiteSpace(display))
                return display;
        }
        catch { }
        return System.Text.RegularExpressions.Regex.Replace(destination.Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
    }
}
