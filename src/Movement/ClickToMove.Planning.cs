using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using StardewValley.Characters;

namespace AgrariaOS.Movement;

partial class ClickToMove
{
    private bool CanWalk(Point tile)
    {
        // Vanilla checks movement warps before tile collisions. Permit this one endpoint,
        // including a trigger just outside the map, without making other off-map tiles walkable.
        if (tile == warpDestination)
            return true;
        if (!location!.isTileOnMap(tile))
            return false;
        Rectangle box = new(tile.X * 64 + 1, tile.Y * 64 + 1, 62, 62);
        if (location.isCollidingPosition(box, Game1.viewport, isFarmer: true, 0, glider: false,
                farmer!, pathfinding: true, skipCollisionEffects: true))
            return false;
        // Vanilla's pathfinding flag excludes NPCs; account for their live hitboxes too.
        return !location.characters.Any(npc => !npc.IsInvisible && !npc.farmerPassesThrough
            && npc.GetBoundingBox().Intersects(box) && !npc.GetBoundingBox().Intersects(farmer!.GetBoundingBox()));
    }

    internal static bool CanWalkFor(GameLocation location, Farmer who, Point tile)
    {
        if (!location.isTileOnMap(tile))
            return false;
        Rectangle box = new(tile.X * 64 + 1, tile.Y * 64 + 1, 62, 62);
        if (location.isCollidingPosition(box, Game1.viewport, isFarmer: true, 0, glider: false,
                who, pathfinding: true, skipCollisionEffects: true))
            return false;
        return !location.characters.Any(npc => !npc.IsInvisible && !npc.farmerPassesThrough
            && npc.GetBoundingBox().Intersects(box) && !npc.GetBoundingBox().Intersects(who.GetBoundingBox()));
    }

    private bool Plan()
    {
        Point start = farmer!.TilePoint;
        Point[] goals = target == null ? new[] { destination } : target.InteractTiles().ToArray();
        // Moving targets or new obstacles can invalidate the snapshot's chosen side.
        if (plannedInteractionTile is { } preferred && (!goals.Contains(preferred)
            || preferred != start && !CanWalk(preferred)))
            plannedInteractionTile = null;
        // Native mouse tool targeting accepts diagonals too. Already-reachable legs
        // arrive in place: a path controller would first center the farmer on a tile.
        Point? hit = interactTile?.Invoke() ?? target?.HitTile(start);
        bool inReach = interactRequest?.InReach is { } reach ? reach(farmer)
            : target is ToolTarget tool && interactTile == null
            ? tool.CanReach(farmer)
            : hit is { } reachable && Utility.tileWithinRadiusOfPlayer(reachable.X, reachable.Y, 1, farmer);
        if (inReach && (plannedInteractionTile == null || plannedInteractionTile == start))
        {
            if (controller != null && farmer.controller == controller)
                farmer.controller = null;
            controller = null;
            farmer.Halt();
            arrived = true;
            routeEnd = start;
            stalledMilliseconds = 0;
            return true;
        }
        if (plannedInteractionTile is { } endpoint)
            goals = new[] { endpoint };
        goals = goals.Where(tile => tile == start || CanWalk(tile)).ToArray();
        Stack<Point>? path = TilePathfinder.Find(start, goals, CanWalk);
        if (path == null && plannedInteractionTile != null)
        {
            plannedInteractionTile = null;
            // An obstacle along the chosen route may leave another side reachable.
            return Plan();
        }
        if (path == null && interactTile == null)
        {
            // Unreachable tree/wall/ground clicks walk as close as possible instead of
            // playing cancel: dense canopies and fences can hide every walkable approach
            // while keyboard-sized movement still squeezes through nearby. Right-click
            // arrivals skip this so far inspection keeps vanilla facing plus Lookup.
            Point desired = destination;
            Stack<Point>? closest = TilePathfinder.FindClosest(start, desired, CanWalk);
            if (closest != null)
            {
                Point fallback = closest.Last();
                destination = fallback;
                target = null;
                warpDestination = null;
                path = closest;
                LastDebug = $"Fallback click={desired} dest={fallback} loc={location?.Name} playerTile={start}";
            }
        }
        if (path == null)
            return false;
        Point end = path.Last();
        routeEnd = end;
        arrived = false;
        controller = new PathFindController(path, location!, farmer, end)
        {
            finalFacingDirection = -1,
            // nonDestructivePathing also invokes NPC-only warp handling. Farmer.MovePosition
            // already checks collisions and performs the player's ordinary map transitions.
            nonDestructivePathing = false,
            endBehaviorFunction = (_, _) => arrived = true
        };
        farmer.Halt();
        farmer.controller = controller;
        previousPosition = farmer.Position;
        stalledMilliseconds = 0;
        return true;
    }

}
