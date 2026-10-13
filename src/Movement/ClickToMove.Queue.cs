using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using StardewValley.Characters;

namespace AgrariaOS.Movement;

partial class ClickToMove
{
    internal IReadOnlyList<QueueIndicator> GetQueueDisplay()
    {
        List<QueueIndicator> list = new();
        Point? active = controller != null || arrived ? ActiveDisplayTile()
            : repeatTool != null ? RepeatDisplayTile()
            : toolRequest?.Tile() ?? arrivedInteractTile;
        if (active is { } head)
            list.Add(new QueueIndicator(head, QueueColor(0), QueueAnimal(interactRequest ?? toolRequest ?? arrivedInteractRequest)));
        foreach (QueuedStep step in pending)
        {
            Point tile = StepDisplayTile(step);
            list.Add(new QueueIndicator(tile, QueueColor(list.Count), QueueAnimal(step.Request)));
        }
        return list;
    }

    // Nearby hues draw a continuous rainbow as a stroke grows. Keep the hue step
    // fixed so appending targets doesn't recolor the existing queue; wrap smoothly.
    internal static Color QueueColor(int index)
    {
        float hue = (0.5f + index % 30 / 30f) % 1f;
        float sector = hue * 6f;
        const float high = 0.95f;
        const float low = high * (1f - 0.65f);
        float rising = low + (high - low) * (sector - MathF.Floor(sector));
        float falling = high - (rising - low);
        (float r, float g, float b) = (int)sector switch
        {
            0 => (high, rising, low),
            1 => (falling, high, low),
            2 => (low, high, rising),
            3 => (low, falling, high),
            4 => (rising, low, high),
            _ => (high, low, falling)
        };
        return new Color((int)MathF.Round(r * 255), (int)MathF.Round(g * 255), (int)MathF.Round(b * 255));
    }

    private static Character? QueueAnimal(InteractRequest? request)
        => request?.Identity is FarmAnimal or Pet ? (Character)request.Identity : null;

    private Point? ActiveDisplayTile()
    {
        try
        {
            if (interactTile != null)
                return interactTile();
        }
        catch { }
        try
        {
            if (target != null)
                return target.HitTile(destination);
        }
        catch { }
        return destination;
    }

    private Point? RepeatDisplayTile()
    {
        try
        {
            if (repeatTool != null && Game1.currentLocation != null)
                return repeatTool.HitTile(Game1.player.TilePoint);
        }
        catch { }
        return null;
    }

    private static Point StepDisplayTile(QueuedStep step)
    {
        try
        {
            if (step.Request != null)
                return step.Request.Tile();
        }
        catch { }
        try
        {
            if (step.Target != null)
                return step.Target.HitTile(step.Clicked);
        }
        catch { }
        return step.Clicked;
    }

    private bool EnqueueStep(QueuedStep step, bool startImmediately = true)
    {
        // Idle with no active leg: start immediately so single clicks keep the old feel.
        if (!IsBusy && startImmediately)
        {
            LastDebug = $"Enqueue idle->Start click={step.Clicked}";
            if (step.Request != null)
                return StartInteract(step.Request, step.Clicked);
            return Start(step.Clicked);
        }
        if (!extendedQueue() && TotalCount >= MaxQueued)
        {
            LastDebug = $"Enqueue rejected (full): total={TotalCount} click={step.Clicked}";
            Game1.playSound("cancel");
            return true;
        }
        pending.Enqueue(step);
        LastDebug = $"Enqueued click={step.Clicked} target={step.Target?.GetType().Name ?? (step.Request != null ? "interact" : "move")} total={TotalCount}";
        Game1.playSound("smallSelect");
        return true;
    }

    private bool StartNextQueued()
    {
        while (pending.Count > 0)
        {
            QueuedStep step = pending.Dequeue();
            Farmer who = Game1.player;
            GameLocation here = Game1.currentLocation;
            if (who.controller != null && who.controller != controller)
            {
                Cancel();
                return false;
            }
            if (step.Location != here || step.Day != Game1.stats.DaysPlayed)
                continue;
            if (step.Request != null)
            {
                bool valid;
                try { valid = step.Request.Valid(); }
                catch { continue; }
                if (!valid)
                    continue;
                farmer = who;
                location = here;
                day = Game1.stats.DaysPlayed;
                warpDestination = step.Request.WarpDestination;
                destination = step.Request.WalkDestination ?? step.Clicked;
                target = step.Request.Move;
                interactTile = step.Request.Tile;
                interactValid = step.Request.Valid;
                interactPerform = step.Request.Perform;
                interactRequest = step.Request;
                plannedInteractionTile = step.PlannedTile;
                replans = 0;
                if (Plan())
                    return true;
                CancelActive();
                continue;
            }
            if (step.Target != null)
            {
                bool valid;
                try { valid = step.Target.IsValid(); }
                catch { continue; }
                if (!valid)
                    continue;
                farmer = who;
                location = here;
                day = Game1.stats.DaysPlayed;
                warpDestination = null;
                destination = step.Clicked;
                target = step.Target;
                interactTile = null;
                interactValid = null;
                interactPerform = null;
                replans = 0;
                if (Plan())
                    return true;
                CancelActive();
                continue;
            }
            farmer = who;
            location = here;
            day = Game1.stats.DaysPlayed;
            warpDestination = null;
            destination = step.Clicked;
            target = null;
            interactTile = null;
            interactValid = null;
            interactPerform = null;
            replans = 0;
            if (Plan())
                return true;
            CancelActive();
        }
        return false;
    }

    /// <summary>Queue an approach-only leg. Warps replace instead, like explicit queues.</summary>
    internal bool EnqueueApproach(Point tile)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null && who.controller != controller)
            return false;
        if (!here.isTileOnMap(tile))
            return false;
        if (WarpDestination.Resolve(here, who, tile) != null)
            return false;
        if (!IsBusy)
            return StartApproach(tile);
        ApproachTarget? resolved = ApproachTarget.ResolveSolid(here, who, tile,
            (candidate) => CanWalkFor(here, who, candidate));
        return EnqueueStep(new QueuedStep(tile, resolved, null, null, here, Game1.stats.DaysPlayed));
    }

    /// <summary>Explicit Ctrl+click queue for any left-click tile, including plain moves.</summary>
    internal bool EnqueueLeftExplicit(Point tile)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null && who.controller != controller)
            return false;
        if (!here.isTileOnMap(tile))
            return false;
        // Warps change location and invalidate the rest of the queue; replace instead.
        if (WarpDestination.Resolve(here, who, tile) != null)
            return false;
        ApproachTarget? resolved = ToolTarget.Resolve(here, who, tile)
            ?? ApproachTarget.ResolveSolid(here, who, tile, (candidate) => CanWalkFor(here, who, candidate));
        return EnqueueStep(new QueuedStep(tile, resolved, null, null, here, Game1.stats.DaysPlayed));
    }

    internal bool EnqueueInteract(InteractRequest request, Point clicked, bool startImmediately = true)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null && who.controller != controller)
            return false;
        if (!here.isTileOnMap(clicked))
            return false;
        if (request.Identity != null && HasQueuedRequest(request))
            return true;
        return EnqueueStep(new QueuedStep(clicked, request.Move, request, null, here, Game1.stats.DaysPlayed), startImmediately);
    }

    /// <summary>Append a whole overview batch in planned walking order. Existing work
    /// retains its order; route optimization begins at its projected final endpoint.</summary>
    internal bool EnqueueInteractions(IReadOnlyList<InteractRequest> requests)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null && who.controller != controller || warpDestination != null)
            return false;
        var batch = new List<InteractRequest>();
        foreach (InteractRequest request in requests)
        {
            if (!request.Valid() || !here.isTileOnMap(request.Tile()) || HasQueuedRequest(request))
                continue;
            if (batch.Any(other => request.Identity != null ? SameRequest(other, request)
                : other.Tile() == request.Tile()))
                continue;
            batch.Add(request);
        }
        if (batch.Count == 0)
            return true;
        // Reject the whole batch rather than silently dropping selected targets.
        if (!extendedQueue() && batch.Count > MaxQueued - TotalCount)
        {
            Game1.playSound("cancel");
            return false;
        }
        var walkable = new Dictionary<Point, bool>();
        bool Walk(Point tile)
        {
            if (!walkable.TryGetValue(tile, out bool allowed))
                walkable[tile] = allowed = ((batch.Any(request => request.WarpDestination == tile)
                    || pending.Any(step => step.Request?.WarpDestination == tile))
                    && WarpDestination.Resolve(here, who, tile) == tile) || CanWalkFor(here, who, tile);
            return allowed;
        }
        Point start = controller != null || arrived ? routeEnd : who.TilePoint;
        foreach (QueuedStep step in pending)
        {
            if (step.Location != here || step.Day != Game1.stats.DaysPlayed
                || step.Target?.IsValid() == false || step.Request?.Valid() == false)
                continue;
            Point[] goals = step.Target?.InteractTiles().ToArray()
                ?? new[] { step.Request?.WalkDestination ?? step.Clicked };
            if (step.PlannedTile is { } preferred && goals.Contains(preferred) && (preferred == start || Walk(preferred)))
                goals = new[] { preferred };
            Stack<Point>? path = TilePathfinder.Find(start, goals.Where(tile => tile == start || Walk(tile)).ToArray(), Walk);
            if (path != null)
                start = path.Last();
        }
        var targets = batch.Select(request =>
        {
            IEnumerable<Point> tiles = request.Move?.InteractTiles() ?? new[] { request.WalkDestination ?? request.Tile() };
            Point hit = request.Tile();
            // Already standing inside an animal's footprint is also a valid zero-cost
            // interaction. Normal paths still use the walkable perimeter.
            if (request.WalkDestination == null && Math.Abs(start.X - hit.X) <= 1 && Math.Abs(start.Y - hit.Y) <= 1)
                tiles = tiles.Append(start);
            return (IReadOnlyCollection<Point>)tiles.Distinct().ToArray();
        }).ToArray();
        var route = TilePathfinder.FindRoute(start, targets, Walk);
        if (route.Count == 0)
        {
            Game1.playSound("cancel");
            return false;
        }
        foreach (var stop in route)
        {
            InteractRequest request = batch[stop.TargetIndex];
            pending.Enqueue(new QueuedStep(request.Tile(), request.Move, request, null,
                here, Game1.stats.DaysPlayed) { PlannedTile = stop.Tile });
        }
        LastDebug = $"Queued planned batch: requested={batch.Count} reachable={route.Count} total={TotalCount}";
        Game1.playSound("smallSelect");
        return true;
    }

}
