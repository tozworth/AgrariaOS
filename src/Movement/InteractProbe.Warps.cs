using Microsoft.Xna.Framework;
using StardewValley;

namespace AgrariaOS.Movement;

internal static partial class InteractProbe
{
    internal static InteractRequest? TryResolveTransition(GameLocation location, Farmer farmer, MapTransition transition)
    {
        if (!transition.IsValid())
            return null;
        bool walking = transition.Kind is MapTransitionKind.Warp or MapTransitionKind.TouchAction;
        var candidates = new Dictionary<Point, Point>();
        foreach (Point source in transition.Tiles)
        {
            if (transition.Kind == MapTransitionKind.Warp
                && WarpDestination.Resolve(location, farmer, source) != source)
                continue;
            if (walking)
                candidates.TryAdd(source, source);
            else
            {
                // Map doors are tile actions: use the entrance tile, never a building's
                // whole footprint or an unrelated object which overlaps the entrance.
                var target = new ApproachTarget(new Rectangle(source.X, source.Y, 1, 1), transition.IsValid);
                foreach (Point side in target.InteractTiles())
                    candidates.TryAdd(side, source);
            }
        }
        bool Walk(Point tile) => transition.Kind == MapTransitionKind.Warp && candidates.ContainsKey(tile)
            || CanWalk(location, farmer, tile);
        Point start = farmer.TilePoint;
        Point[] goals = candidates.Keys.Where(tile => tile == start || Walk(tile)).ToArray();
        Stack<Point>? path = TilePathfinder.Find(start, goals, Walk);
        if (path == null)
            return null;
        Point sourceTile = candidates[path.Last()];
        Point visible = transition.VisibleTile(sourceTile);
        bool Valid() => transition.IsValid() && (transition.Kind != MapTransitionKind.Warp
            || WarpDestination.Resolve(location, farmer, sourceTile) == sourceTile);
        void Perform(Farmer who)
        {
            // Walking exits are triggered by Farmer.MovePosition / location updates.
            // Never call warpFarmer or replay TouchAction on arrival.
            if (!walking && who == farmer && Valid()
                && Utility.tileWithinRadiusOfPlayer(sourceTile.X, sourceTile.Y, 1, who))
                location.checkAction(new xTile.Dimensions.Location(sourceTile.X, sourceTile.Y), Game1.viewport, who);
        }
        return new InteractRequest(walking ? null
            : new ApproachTarget(new Rectangle(sourceTile.X, sourceTile.Y, 1, 1), Valid),
            () => visible, Valid, Perform)
        {
            WalkDestination = walking ? sourceTile : null,
            WarpDestination = transition.Kind == MapTransitionKind.Warp ? sourceTile : null,
            InReach = walking ? who => who.TilePoint == sourceTile : null
        };
    }
}
