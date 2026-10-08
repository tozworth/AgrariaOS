using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using StardewValley.Characters;

namespace AgrariaOS.Movement;

/// <summary>A single pending leg. Tool legs carry a ToolTarget, approach legs a plain
/// ApproachTarget, interact legs an InteractRequest, and plain moves neither.</summary>
internal sealed record QueuedStep(
    Point Clicked,
    ApproachTarget? Target,
    InteractRequest? Request,
    Point? Warp,
    GameLocation Location,
    uint Day);

internal readonly record struct QueueIndicator(Point Tile, Color Color, Character? Animal);

internal sealed class ClickToMove
{
    internal const int MaxQueued = 9;
    private const int MaxSwingsPerTool = 30;

    internal static readonly Color[] QueuePalette =
    {
        new(93, 214, 221),   // 0 cyan (current target)
        new(255, 210, 60),   // 1 yellow
        new(255, 140, 40),   // 2 orange
        new(120, 220, 80),   // 3 green
        new(255, 110, 180),  // 4 pink
        new(170, 130, 255),  // 5 purple
        new(255, 90, 80),    // 6 red
        new(110, 170, 255),  // 7 blue
        new(230, 230, 230),  // 8 white
    };

    private Farmer? farmer;
    private GameLocation? location;
    private PathFindController? controller;
    private ApproachTarget? target;
    private Point? warpDestination;
    private Point destination;
    private uint day;
    private bool arrived;
    private Vector2 previousPosition;
    private double stalledMilliseconds;
    private int replans;
    internal string LastDebug { get; private set; } = "";
    private Farmer? toolFarmer;
    private Tool? startedTool;
    private int releaseTicks;
    private Func<Point>? interactTile;
    private Func<bool>? interactValid;
    private Action<Farmer>? interactPerform;
    private InteractRequest? interactRequest;
    private Point? arrivedInteractTile;
    private Action<Farmer>? arrivedInteractPerform;
    private InteractRequest? arrivedInteractRequest;
    private InteractRequest? toolRequest;
    private ToolTarget? repeatTool;
    private int repeatSwings;
    private readonly Queue<QueuedStep> pending = new();

    internal Point? Destination => controller != null || arrived ? ActiveDisplayTile() ?? destination : null;

    internal bool IsBusy => controller != null || arrived || repeatTool != null || toolFarmer != null
        || arrivedInteractTile != null || pending.Count > 0;

    internal int TotalCount => pending.Count
        + (controller != null || arrived || repeatTool != null || toolFarmer != null || arrivedInteractTile != null ? 1 : 0);

    internal bool HasQueuedTool(ToolTarget tool) => target is ToolTarget active && SameTool(active, tool)
        || repeatTool != null && SameTool(repeatTool, tool)
        || pending.Any(step => step.Target is ToolTarget queued && SameTool(queued, tool));

    private static bool SameTool(ToolTarget a, ToolTarget b) => a.Tiles == b.Tiles && a.IsValid() && b.IsValid();

    internal bool EnqueueRock(Point tile, bool startImmediately)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (!here.isTileOnMap(tile) || who.controller != null && who.controller != controller)
            return false;
        ToolTarget? rock = ToolTarget.ResolveRock(here, who, tile);
        if (rock == null)
            return false;
        return HasQueuedTool(rock) || EnqueueStep(
            new QueuedStep(tile, rock, null, null, here, Game1.stats.DaysPlayed), startImmediately);
    }

    internal bool HasQueuedInteraction(Point tile) => arrivedInteractTile == tile
        || interactTile?.Invoke() == tile || pending.Any(step => step.Request?.Tile() == tile);

    internal bool HasQueuedRequest(InteractRequest request) => request.Identity == null
        ? HasQueuedInteraction(request.Tile())
        : SameRequest(interactRequest, request) || SameRequest(arrivedInteractRequest, request)
            || SameRequest(toolRequest, request) || pending.Any(step => SameRequest(step.Request, request));

    private static bool SameRequest(InteractRequest? a, InteractRequest b)
        => a != null && a.Kind == b.Kind && ReferenceEquals(a.Identity, b.Identity);

    internal IReadOnlyList<QueueIndicator> GetQueueDisplay()
    {
        List<QueueIndicator> list = new();
        Point? active = controller != null || arrived ? ActiveDisplayTile()
            : repeatTool != null ? RepeatDisplayTile()
            : toolRequest?.Tile() ?? arrivedInteractTile;
        if (active is { } head)
            list.Add(new QueueIndicator(head, QueuePalette[0], QueueAnimal(interactRequest ?? toolRequest ?? arrivedInteractRequest)));
        foreach (QueuedStep step in pending)
        {
            Point tile = StepDisplayTile(step);
            int index = Math.Min(list.Count, QueuePalette.Length - 1);
            list.Add(new QueueIndicator(tile, QueuePalette[index], QueueAnimal(step.Request)));
            if (list.Count >= MaxQueued)
                break;
        }
        return list;
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

    internal bool Start(Point tile)
    {
        Cancel();
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        // Never replace an event's or another mod's movement controller.
        if (who.controller != null)
        {
            LastDebug = $"Start {tile} blocked: controller owned by {who.controller?.GetType().Name}";
            return false;
        }
        Point? exit = WarpDestination.Resolve(here, who, tile);
        if (!here.isTileOnMap(tile) && exit == null)
        {
            LastDebug = $"Start {tile} off-map, no warp";
            return true;
        }
        farmer = who;
        location = here;
        day = Game1.stats.DaysPlayed;
        warpDestination = exit;
        destination = exit ?? tile;
        // Exit clicks take priority over toolable ground at the edge of the map.
        target = exit == null ? ToolTarget.Resolve(here, who, tile)
            ?? ApproachTarget.ResolveSolid(here, who, tile, CanWalk) : null;
        bool planned = Plan();
        LastDebug = $"Start click={tile} exit={exit} dest={destination} target={target?.GetType().Name ?? "move"} planned={planned} loc={here.Name} playerTile={who.TilePoint}";
        if (!planned)
        {
            Cancel();
            Game1.playSound("cancel");
        }
        return true;
    }

    internal bool StartInteract(InteractRequest request, Point clicked)
    {
        Cancel();
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        // Never replace an event's or another mod's movement controller.
        if (who.controller != null)
            return false;
        if (!here.isTileOnMap(clicked))
            return true;
        farmer = who;
        location = here;
        day = Game1.stats.DaysPlayed;
        warpDestination = null;
        destination = clicked;
        // Stored interactions don't cross map-edge warps; their callbacks perform
        // the selected native action on arrival, including single livestock tool uses.
        target = request.Move;
        interactTile = request.Tile;
        interactValid = request.Valid;
        interactPerform = request.Perform;
        interactRequest = request;
        if (!Plan())
        {
            Cancel();
            Game1.playSound("cancel");
            // Unreachable: fall through to vanilla facing-tile plus Lookup info.
            return false;
        }
        return true;
    }

    /// <summary>Queue a left-click tile without replacing the active route. Warps and
    /// plain ground are not auto-queued; the caller should Start those instead.</summary>
    internal bool TryEnqueueToolOrApproach(Point tile)
    {
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null && who.controller != controller)
            return false;
        if (!here.isTileOnMap(tile))
            return false;
        if (WarpDestination.Resolve(here, who, tile) != null)
            return false;
        ApproachTarget? resolved = ToolTarget.Resolve(here, who, tile)
            ?? ApproachTarget.ResolveSolid(here, who, tile, (candidate) => CanWalkFor(here, who, candidate));
        if (resolved == null)
            return false;
        return EnqueueStep(new QueuedStep(tile, resolved, null, null, here, Game1.stats.DaysPlayed));
    }

    /// <summary>Walk to a tile's reachable side without swinging tools. Used by the
    /// overview's approach action (Ctrl-click, double-click, Approach menu).</summary>
    internal bool StartApproach(Point tile)
    {
        Cancel();
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        if (who.controller != null)
            return false;
        Point? exit = WarpDestination.Resolve(here, who, tile);
        if (!here.isTileOnMap(tile) && exit == null)
            return true;
        farmer = who;
        location = here;
        day = Game1.stats.DaysPlayed;
        warpDestination = exit;
        destination = exit ?? tile;
        target = exit == null ? ApproachTarget.ResolveSolid(here, who, tile, CanWalk) : null;
        if (!Plan())
        {
            Cancel();
            Game1.playSound("cancel");
        }
        return true;
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
        if (TotalCount >= MaxQueued)
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

    internal bool IsToolActive => repeatTool != null || toolFarmer != null;

    internal bool TryConsumeInteractArrival(out Point tile, out Action<Farmer> perform)
    {
        if (arrivedInteractTile is { } want && arrivedInteractPerform is { } action)
        {
            tile = want;
            perform = action;
            arrivedInteractTile = null;
            arrivedInteractPerform = null;
            arrivedInteractRequest = null;
            return true;
        }
        tile = default;
        perform = _ => { };
        return false;
    }

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

    private static bool CanWalkFor(GameLocation location, Farmer who, Point tile)
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
        // Native mouse tool targeting accepts diagonals too. Already-reachable legs
        // arrive in place: a path controller would first center the farmer on a tile.
        Point? hit = interactTile?.Invoke() ?? target?.HitTile(start);
        bool inReach = interactRequest?.InReach is { } reach ? reach(farmer)
            : target is ToolTarget tool && interactTile == null
            ? tool.CanReach(farmer)
            : hit is { } reachable && Utility.tileWithinRadiusOfPlayer(reachable.X, reachable.Y, 1, farmer);
        if (inReach)
        {
            if (controller != null && farmer.controller == controller)
                farmer.controller = null;
            controller = null;
            farmer.Halt();
            arrived = true;
            stalledMilliseconds = 0;
            return true;
        }
        Point[] goals = target == null ? new[] { destination }
            : target.InteractTiles().ToArray();
        goals = goals.Where(tile => tile == start || CanWalk(tile)).ToArray();
        Stack<Point>? path = TilePathfinder.Find(start, goals, CanWalk);
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
                warpDestination = null;
                destination = step.Clicked;
                target = step.Request.Move;
                interactTile = step.Request.Tile;
                interactValid = step.Request.Valid;
                interactPerform = step.Request.Perform;
                interactRequest = step.Request;
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

    internal void BeforeUpdate(bool canMove, bool manualMovement)
    {
        if (manualMovement && IsBusy)
        {
            Cancel();
            return;
        }
        if (controller == null && !arrived)
            return;
        if (farmer != Game1.player || location != Game1.currentLocation || day != Game1.stats.DaysPlayed
            || farmer.controller != null && farmer.controller != controller)
        {
            Cancel();
            return;
        }
        // A warp fade restarts every time the farmer touches the trigger: release our
        // controller at once so vanilla can finish fading, otherwise the exit stalls for
        // seconds of replans (and warp spam) instead of warping instantly.
        if (Game1.locationRequest != null || Game1.fadeToBlack)
        {
            CancelActive();
            return;
        }
        // Pause for menus/dialogues/desktop focus: keep the controller so the walk
        // (and queue) resumes afterwards instead of dropping the current rock/NPC.
        if (!canMove)
            return;
        try
        {
            if (target?.IsValid() == false || interactValid?.Invoke() == false
                || (warpDestination is { } exit && WarpDestination.Resolve(location!, farmer, exit) != exit))
                CancelActive();
        }
        catch
        {
            CancelActive();
        }
    }

    internal void CancelCurrentLeg() => CancelActive();

    internal void AfterUpdate(bool canMove)
    {
        ReleaseTool();
        if (controller == null && !arrived)
        {
            if (repeatTool != null)
            {
                UpdateToolRepeat(canMove);
                return;
            }
            if (arrivedInteractTile != null)
                return;
            if (pending.Count == 0)
                return;
            // Don't start the next leg while a menu/dialogue/tool owns the player.
            if (!canMove || Game1.player.UsingTool || toolFarmer != null)
                return;
            if (farmer != null && (farmer != Game1.player || location != Game1.currentLocation || day != Game1.stats.DaysPlayed))
            {
                Cancel();
                return;
            }
            if (Game1.player.controller != null && Game1.player.controller != controller)
            {
                Cancel();
                return;
            }
            if (!StartNextQueued() && pending.Count == 0 && controller == null && repeatTool == null)
            {
                // All remaining legs were stale or unreachable; stay silent unless something was queued.
            }
            return;
        }
        if (farmer != Game1.player || location != Game1.currentLocation || day != Game1.stats.DaysPlayed)
        {
            Cancel();
            return;
        }
        if (farmer!.controller != null && farmer.controller != controller)
        {
            Cancel();
            return;
        }
        // Same warp-fade release as BeforeUpdate: never drive into a trigger mid-fade.
        if (Game1.locationRequest != null || Game1.fadeToBlack)
        {
            CancelActive();
            return;
        }
        // Pause (menu/dialogue/desktop/focus lost): keep the controller and skip stall
        // buildup so the walk resumes instead of replanning away the queue.
        if (!canMove)
            return;
        if (arrived)
        {
            Farmer who = farmer!;
            // Interaction arrivals retain their selected action. Right-click actions
            // go through ModEntry's routing flag; livestock tools start here and wait
            // for their native animation. Moving targets get bounded chase replans.
            if (interactTile != null && interactPerform != null)
            {
                Func<bool>? valid = interactValid;
                Func<Point>? tileGetter = interactTile;
                Action<Farmer>? perform = interactPerform;
                bool stillValid;
                try { stillValid = valid?.Invoke() != false; }
                catch { stillValid = false; }
                if (!stillValid)
                {
                    CancelActive();
                    return;
                }
                Point want;
                try { want = tileGetter!(); }
                catch
                {
                    CancelActive();
                    return;
                }
                if (!(interactRequest?.InReach?.Invoke(who)
                        ?? Utility.tileWithinRadiusOfPlayer(want.X, want.Y, 1, who)))
                {
                    if (++replans > 3)
                    {
                        CancelActive();
                        Game1.playSound("cancel");
                    }
                    else if (target?.LiveTiles != null)
                    {
                        // Keep the exact selected animal and its action callbacks as it moves.
                        // Resolving a new generic right-click here could inspect an animal
                        // or select a different animal sharing the new tile.
                        destination = want;
                        if (!Plan())
                        {
                            CancelActive();
                            Game1.playSound("cancel");
                        }
                    }
                    else if (InteractProbe.TryResolve(location!, who, want) is { } chase)
                    {
                        target = chase.Move;
                        destination = want;
                        interactTile = chase.Tile;
                        interactValid = chase.Valid;
                        interactPerform = chase.Perform;
                        interactRequest = chase;
                        if (!Plan())
                        {
                            CancelActive();
                            Game1.playSound("cancel");
                        }
                    }
                    else
                    {
                        CancelActive();
                        Game1.playSound("cancel");
                    }
                    return;
                }
                InteractRequest? request = interactRequest;
                CancelActive();
                if (request?.Tool is { } harvestTool)
                {
                    perform(who);
                    toolFarmer = who;
                    startedTool = harvestTool;
                    toolRequest = request;
                    releaseTicks = 0;
                    return;
                }
                arrivedInteractTile = want;
                arrivedInteractPerform = perform;
                arrivedInteractRequest = request;
                return;
            }
            ApproachTarget? approached = target;
            ToolTarget? action = target as ToolTarget;
            bool approachValid;
            try { approachValid = approached?.IsValid() == true; }
            catch { approachValid = false; }
            CancelActive();
            if (approachValid && approached != null)
            {
                try
                {
                    Point hit = approached.HitTile(who.TilePoint);
                    who.faceDirection(who.getGeneralDirectionTowards(new Vector2(hit.X * 64 + 32, hit.Y * 64 + 32)));
                }
                catch { }
            }
            bool toolValid;
            try { toolValid = action?.IsValid() == true && who.Items.Contains(action.Tool); }
            catch { toolValid = false; }
            if (toolValid && action != null)
            {
                Point hit;
                try { hit = action.HitTile(who.TilePoint); }
                catch { return; }
                if (!action.CanReach(who))
                    return;
                // Water refills stay single-use: the tile remains refillable after filling.
                bool singleUse = action.Tool is WateringCan
                    && Game1.currentLocation.CanRefillWateringCanOnTile(hit.X, hit.Y);
                if (SwingOnce(who, action, hit))
                {
                    toolFarmer = who;
                    startedTool = action.Tool;
                    releaseTicks = 0;
                    if (!singleUse)
                    {
                        // Keep the session alive across swings: UpdateToolRepeat validates
                        // against these (CancelActive above cleared them).
                        farmer = who;
                        location = Game1.currentLocation;
                        day = Game1.stats.DaysPlayed;
                        repeatTool = action;
                        repeatSwings = 1;
                        LastDebug = $"Repeat start tool={action.Tool?.Name} target={hit}";
                    }
                }
            }
            return;
        }
        if (farmer!.controller != controller)
        {
            CancelActive();
            return;
        }
        if (Vector2.DistanceSquared(previousPosition, farmer.Position) < 0.01f)
            stalledMilliseconds += Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
        else
            stalledMilliseconds = 0;
        previousPosition = farmer.Position;
        // Recompute around moving NPCs or newly placed obstacles; never wait indefinitely.
        if (stalledMilliseconds >= 600 && (++replans > 3 || !Plan()))
        {
            CancelActive();
            Game1.playSound("cancel");
        }
    }

    private static bool SwingOnce(Farmer who, ToolTarget action, Point hit)
    {
        try
        {
            if (!who.Items.Contains(action.Tool))
                return false;
            who.CurrentToolIndex = who.Items.IndexOf(action.Tool);
            Vector2 pixel = new(hit.X * 64 + 32, hit.Y * 64 + 32);
            who.faceDirection(who.getGeneralDirectionTowards(pixel));
            who.lastClick = pixel;
            who.toolPower.Value = 0;
            who.toolHold.Value = 0;
            // Use the game's animation/net-event path, preserving stamina, upgrades and drops.
            who.BeginUsingTool();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateToolRepeat(bool canMove)
    {
        Farmer who = Game1.player;
        if (location != Game1.currentLocation || day != Game1.stats.DaysPlayed)
        {
            LastDebug = "Repeat cleared: location/day changed";
            Cancel();
            return;
        }
        if (who.controller != null && who.controller != controller)
        {
            LastDebug = "Repeat cleared: foreign controller";
            Cancel();
            return;
        }
        // Wait for the previous swing's release before starting the next one.
        if (toolFarmer != null)
            return;
        if (who.UsingTool)
            return;
        if (!canMove)
            return;
        ToolTarget? tool = repeatTool;
        if (tool == null)
            return;
        bool valid;
        try { valid = tool.IsValid() && who.Items.Contains(tool.Tool); }
        catch { valid = false; }
        if (!valid || repeatSwings >= MaxSwingsPerTool)
        {
            LastDebug = $"Repeat done: valid={valid} swings={repeatSwings}";
            repeatTool = null;
            farmer = null;
            location = null;
            return;
        }
        Point hit;
        try { hit = tool.HitTile(who.TilePoint); }
        catch
        {
            repeatTool = null;
            farmer = null;
            location = null;
            return;
        }
        if (!tool.CanReach(who))
        {
            repeatTool = null;
            farmer = null;
            location = null;
            return;
        }
        if (SwingOnce(who, tool, hit))
        {
            toolFarmer = who;
            startedTool = tool.Tool;
            releaseTicks = 0;
            repeatSwings++;
        }
        else
        {
            repeatTool = null;
        }
    }

    private void ReleaseTool()
    {
        if (toolFarmer == null)
            return;
        releaseTicks++;
        if (toolFarmer == Game1.player && toolFarmer.CurrentTool == startedTool && toolFarmer.UsingTool)
        {
            // Livestock tools finish through their own animation and finish net event.
            // Keep ownership/identity throughout so drag selection can append safely.
            if (startedTool is MilkPail or Shears)
                return;
            // Upgraded hoes/cans normally wait for a mouse release; this is one uncharged use.
            if (toolFarmer.canReleaseTool)
                toolFarmer.EndUsingTool();
        }
        else if (releaseTicks < 3 && toolFarmer == Game1.player && toolFarmer.CurrentTool == startedTool)
            return;
        toolFarmer = null;
        startedTool = null;
        toolRequest = null;
    }

    private void CancelActive()
    {
        if (controller != null && farmer?.controller == controller)
        {
            farmer.controller = null;
            farmer.Halt();
        }
        farmer = null;
        location = null;
        controller = null;
        target = null;
        warpDestination = null;
        arrived = false;
        stalledMilliseconds = 0;
        replans = 0;
        interactTile = null;
        interactValid = null;
        interactPerform = null;
        interactRequest = null;
    }

    internal void Cancel()
    {
        CancelActive();
        pending.Clear();
        repeatTool = null;
        repeatSwings = 0;
        arrivedInteractTile = null;
        arrivedInteractPerform = null;
        arrivedInteractRequest = null;
    }
}
