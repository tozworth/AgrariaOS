using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

namespace AgrariaOS.Movement;

internal sealed class ClickToMove
{
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
    private Farmer? toolFarmer;
    private Tool? startedTool;
    private int releaseTicks;
    private Func<Point>? interactTile;
    private Func<bool>? interactValid;
    private Action<Farmer>? interactPerform;
    private Point? arrivedInteractTile;
    private Action<Farmer>? arrivedInteractPerform;

    internal Point? Destination => controller != null ? target?.HitTile(destination) ?? destination : null;

    internal bool Start(Point tile)
    {
        Cancel();
        Farmer who = Game1.player;
        GameLocation here = Game1.currentLocation;
        // Never replace an event's or another mod's movement controller.
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
        // Exit clicks take priority over toolable ground at the edge of the map.
        target = exit == null ? ToolTarget.Resolve(here, who, tile)
            ?? ApproachTarget.ResolveSolid(here, who, tile, CanWalk) : null;
        if (!Plan())
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
        // Right-click arrivals never swing tools or cross map-edge warps; the stored
        // action performs the vanilla right-click path directly on arrival.
        target = request.Move;
        interactTile = request.Tile;
        interactValid = request.Valid;
        interactPerform = request.Perform;
        if (!Plan())
        {
            Cancel();
            Game1.playSound("cancel");
            // Unreachable: fall through to vanilla facing-tile plus Lookup info.
            return false;
        }
        return true;
    }

    internal bool TryConsumeInteractArrival(out Point tile, out Action<Farmer> perform)
    {
        if (arrivedInteractTile is { } want && arrivedInteractPerform is { } action)
        {
            tile = want;
            perform = action;
            arrivedInteractTile = null;
            arrivedInteractPerform = null;
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

    private bool Plan()
    {
        Point start = farmer!.TilePoint;
        Point[] goals = target == null ? new[] { destination } : target.ApproachTiles().ToArray();
        goals = goals.Where(tile => tile == start || CanWalk(tile)).ToArray();
        Stack<Point>? path = TilePathfinder.Find(start, goals, CanWalk);
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

    internal void BeforeUpdate(bool canMove, bool manualMovement)
    {
        if (controller == null)
            return;
        if (!canMove || manualMovement || farmer != Game1.player || location != Game1.currentLocation
            || day != Game1.stats.DaysPlayed || farmer.controller != controller || target?.IsValid() == false
            || interactValid?.Invoke() == false
            || (warpDestination is { } exit && WarpDestination.Resolve(location!, farmer, exit) != exit))
            Cancel();
    }

    internal void AfterUpdate(bool canMove)
    {
        ReleaseTool();
        if (controller == null)
            return;
        if (!canMove || farmer != Game1.player || location != Game1.currentLocation || day != Game1.stats.DaysPlayed)
        {
            Cancel();
            return;
        }
        if (farmer!.controller != null && farmer.controller != controller)
        {
            Cancel();
            return;
        }
        if (arrived)
        {
            Farmer who = farmer!;
            // Right-click arrivals queue the vanilla right-click action for ModEntry to
            // perform with its routing flag held. Moving targets that wandered off get
            // one chase replan instead of interacting with empty air.
            if (interactTile != null && interactPerform != null)
            {
                Func<bool>? valid = interactValid;
                Func<Point>? tileGetter = interactTile;
                Action<Farmer>? perform = interactPerform;
                if (valid?.Invoke() == false)
                {
                    Cancel();
                    return;
                }
                Point want = tileGetter!();
                if (!Utility.tileWithinRadiusOfPlayer(want.X, want.Y, 1, who))
                {
                    if (++replans > 3)
                    {
                        Cancel();
                        Game1.playSound("cancel");
                    }
                    else if (InteractProbe.TryResolve(location!, who, want) is { } chase)
                    {
                        target = chase.Move;
                        destination = want;
                        interactTile = chase.Tile;
                        interactValid = chase.Valid;
                        interactPerform = chase.Perform;
                        if (!Plan())
                        {
                            Cancel();
                            Game1.playSound("cancel");
                        }
                    }
                    else
                    {
                        Cancel();
                        Game1.playSound("cancel");
                    }
                    return;
                }
                Cancel();
                arrivedInteractTile = want;
                arrivedInteractPerform = perform;
                return;
            }
            ApproachTarget? approached = target;
            ToolTarget? action = target as ToolTarget;
            Cancel();
            if (approached?.IsValid() == true)
            {
                Point hit = approached.HitTile(who.TilePoint);
                who.faceDirection(who.getGeneralDirectionTowards(new Vector2(hit.X * 64 + 32, hit.Y * 64 + 32)));
            }
            if (action?.IsValid() == true && who.Items.Contains(action.Tool))
            {
                Point hit = action.HitTile(who.TilePoint);
                if (Math.Abs(hit.X - who.TilePoint.X) + Math.Abs(hit.Y - who.TilePoint.Y) != 1)
                    return;
                who.CurrentToolIndex = who.Items.IndexOf(action.Tool);
                Vector2 pixel = new(hit.X * 64 + 32, hit.Y * 64 + 32);
                who.faceDirection(who.getGeneralDirectionTowards(pixel));
                who.lastClick = pixel;
                who.toolPower.Value = 0;
                who.toolHold.Value = 0;
                toolFarmer = who;
                startedTool = action.Tool;
                releaseTicks = 0;
                // Use the game's animation/net-event path, preserving stamina, upgrades and drops.
                who.BeginUsingTool();
            }
            return;
        }
        if (farmer!.controller != controller)
        {
            Cancel();
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
            Cancel();
            Game1.playSound("cancel");
        }
    }

    private void ReleaseTool()
    {
        if (toolFarmer == null)
            return;
        releaseTicks++;
        if (toolFarmer == Game1.player && toolFarmer.CurrentTool == startedTool && toolFarmer.UsingTool)
        {
            // Upgraded hoes/cans normally wait for a mouse release; this is one uncharged use.
            if (toolFarmer.canReleaseTool)
                toolFarmer.EndUsingTool();
        }
        else if (releaseTicks < 3 && toolFarmer == Game1.player && toolFarmer.CurrentTool == startedTool)
            return;
        toolFarmer = null;
        startedTool = null;
    }

    internal void Cancel()
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
        arrivedInteractTile = null;
        arrivedInteractPerform = null;
    }
}
